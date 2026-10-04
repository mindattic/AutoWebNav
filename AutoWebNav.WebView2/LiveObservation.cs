using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace AutoWebNav.WebView2;

/// <summary>
/// Live Observation for one running host app: every WebView2 browser process it creates opens a
/// local Chrome DevTools Protocol port, and the app registers itself (name, PID, one port per pane)
/// in a shared discovery folder. An external observer — <c>tools/awn-observe.mjs</c> in the
/// AutoWebNav repo, Playwright's <c>connectOverCDP</c>, anything that speaks CDP — can then find
/// and attach to the ALREADY-RUNNING app, read its live DOM, screenshot it and follow what it does,
/// without a relaunch or special command-line flags.
/// <para>
/// Built once here and adopted the same way by every AutoWebNav host (JobHunt, Automata,
/// KdpPublish), per the house rule that an AutoWebNav-level capability is never bolted onto a
/// single app. The ports are unauthenticated and bound to 127.0.0.1 only, open for as long as the
/// app runs.
/// </para>
/// <para>
/// Usage, once per app:
/// <code>
/// observation = new LiveObservation("KdpPublish");
/// Title = observation.WindowTitle;
/// var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folder,
///     options: observation.OptionsFor("board", preferredPort: 9369));
/// // ... and observation.Dispose() when the window closes.
/// </code>
/// A page in a registered pane may also define <c>window.__awnObserve = () =&gt; ({ running,
/// summary, log })</c>; the observer reports that instead of a generic page summary, which is how
/// a host's own control panel tells an observer what the app is doing right now.
/// </para>
/// </summary>
public sealed class LiveObservation : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Dictionary<string, int> panes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private readonly DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
    private bool disposed;

    /// <summary>The shared discovery folder every AutoWebNav host registers into:
    /// <c>%LocalAppData%\MindAttic\AutoWebNav\instances</c>, one JSON file per running instance.</summary>
    public static string RegistryDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindAttic", "AutoWebNav", "instances");

    public string App { get; }
    public int ProcessId { get; } = Environment.ProcessId;

    /// <summary>This instance's registry file.</summary>
    public string RegistryFile { get; }

    /// <summary>The house window-title convention: <c>"&lt;App&gt; - PID: &lt;n&gt;"</c>, so the
    /// instance an observer is attached to is identifiable at a glance (and in Task Manager).</summary>
    public string WindowTitle => $"{App} - PID: {ProcessId}";

    /// <summary>The ports registered so far, by pane name.</summary>
    public IReadOnlyDictionary<string, int> Panes
    {
        get { lock (gate) return new Dictionary<string, int>(panes, StringComparer.OrdinalIgnoreCase); }
    }

    public LiveObservation(string app)
    {
        if (string.IsNullOrWhiteSpace(app)) throw new ArgumentException("An app name is required.", nameof(app));
        App = app.Trim();
        Directory.CreateDirectory(RegistryDirectory);
        RegistryFile = Path.Combine(RegistryDirectory, $"{Sanitize(App)}-{ProcessId}.json");
        PruneStaleEntries();
        Write();
    }

    /// <summary>
    /// Environment options for one pane's WebView2 browser process, with a CDP port added and the
    /// pane registered. The port is <paramref name="preferredPort"/> unless the environment
    /// variable <c>AWN_&lt;APP&gt;_&lt;PANE&gt;_CDP_PORT</c> overrides it (or, for compatibility,
    /// <paramref name="legacyPortEnvVar"/>), and falls back to a free OS-assigned port when that
    /// one is already taken — the registry always records the port actually used, so an observer
    /// never has to guess. <paramref name="baseOptions"/>' other browser arguments are kept.
    /// </summary>
    public CoreWebView2EnvironmentOptions OptionsFor(
        string pane, int preferredPort, CoreWebView2EnvironmentOptions? baseOptions = null, string? legacyPortEnvVar = null)
    {
        if (string.IsNullOrWhiteSpace(pane)) throw new ArgumentException("A pane name is required.", nameof(pane));

        var port = EnvPort($"AWN_{Sanitize(App).ToUpperInvariant()}_{Sanitize(pane).ToUpperInvariant()}_CDP_PORT")
                   ?? (legacyPortEnvVar is null ? null : EnvPort(legacyPortEnvVar))
                   ?? preferredPort;
        if (!IsPortFree(port)) port = FreePort();

        lock (gate) panes[pane.Trim()] = port;
        Write();

        var options = baseOptions ?? new CoreWebView2EnvironmentOptions();
        var arg = $"--remote-debugging-port={port}";
        options.AdditionalBrowserArguments = string.IsNullOrWhiteSpace(options.AdditionalBrowserArguments)
            ? arg
            : $"{options.AdditionalBrowserArguments} {arg}";
        return options;
    }

    /// <summary>Removes this instance from the registry. Called when the app's window closes; a
    /// crash leaves the file behind, which the next registration (and the observer) prunes by PID.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        try { File.Delete(RegistryFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Write()
    {
        Dictionary<string, int> snapshot;
        lock (gate)
        {
            if (disposed) return;
            snapshot = new Dictionary<string, int>(panes, StringComparer.OrdinalIgnoreCase);
        }
        var entry = new
        {
            app = App,
            pid = ProcessId,
            exe = Environment.ProcessPath,
            title = WindowTitle,
            startedUtc,
            panes = snapshot,
        };
        // Write-then-move so an observer never reads a half-written file.
        var temp = RegistryFile + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(entry, Json));
            File.Move(temp, RegistryFile, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Deletes registry files whose process is gone (a crashed instance never ran Dispose).</summary>
    private static void PruneStaleEntries()
    {
        foreach (var file in Directory.EnumerateFiles(RegistryDirectory, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var dash = name.LastIndexOf('-');
            if (dash < 0 || !int.TryParse(name[(dash + 1)..], out var pid) || IsAlive(pid)) continue;
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsAlive(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static int? EnvPort(string name) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var p) && p is > 0 and < 65536 ? p : null;

    private static bool IsPortFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException) { return false; }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string Sanitize(string s) =>
        new(s.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
