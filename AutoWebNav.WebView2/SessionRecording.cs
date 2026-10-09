using MindAttic.Export.Artifacts;
using Microsoft.Web.WebView2.Core;

namespace AutoWebNav.WebView2;

/// <summary>
/// Always-on Spectator Mode for a whole app session: from the moment a pane is attached, every
/// click, typed value (passwords masked), selection, upload and navigation in it is captured —
/// by the person or by the app's own automation — and saved as one portable
/// <c>*.autowebnav-recording.json</c> (<see cref="RecordingExport"/>) in Downloads. So there is
/// always a most-recent record of exactly what went on in an AutoWebNav app, without anyone having
/// remembered to press Record first.
/// <para>
/// The file is named once, when the session starts, and rewritten in place: every
/// <see cref="FlushInterval"/> while there is something new, and a final time on
/// <see cref="Dispose"/> (window close). A crash therefore still leaves a record at most one
/// interval stale.
/// </para>
/// <para>
/// Independent of an explicit Spectator/Record arm (<see cref="RecorderSession"/>, Automata's
/// ● Record): the page-side recorder captures while EITHER is on, and neither turns the other off.
/// Built once here and adopted by every AutoWebNav host (JobHunt, Automata, KdpPublish).
/// </para>
/// </summary>
public sealed class SessionRecording : IDisposable
{
    /// <summary>How often a changed session is rewritten to disk.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    private const string SessionFlagJs = "window.__awnSessionRecording = true;";

    // Each flush replaces this session's one file; the name is already exact.
    private static readonly ArtifactOptions FlushOptions = new()
    {
        Existing = ExistingArtifact.Overwrite,
        SanitizeName = false
    };

    private readonly List<RecorderEvent> events = [];
    private readonly object gate = new();
    private readonly Timer timer;
    private readonly DateTimeOffset startedAt = DateTimeOffset.Now;
    private int writtenCount;
    private bool disposed;

    public string App { get; }

    /// <summary>Where this session's recording is written (fixed for the whole session).</summary>
    public string OutputPath { get; }

    /// <summary>Raised for every captured event, after it is recorded — for a host that wants to
    /// show or forward it live.</summary>
    public event Action<string, RecorderEvent>? EventCaptured;

    /// <param name="app">Prefixes the file name: <c>&lt;app&gt;-session-&lt;yyyyMMdd-HHmmss&gt;</c>.</param>
    /// <param name="outputDirectory">Defaults to this account's real Downloads folder
    /// (<see cref="KnownFolders.Downloads"/>).</param>
    public SessionRecording(string app, string? outputDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(app)) throw new ArgumentException("An app name is required.", nameof(app));
        App = app.Trim();
        var dir = outputDirectory ?? KnownFolders.Downloads;
        Directory.CreateDirectory(dir);
        // <App>-session-<yyyyMMdd-HHmmss>.autowebnav-recording.json
        OutputPath = Path.Combine(dir, ArtifactNames.Timestamped($"{App}-session", startedAt.DateTime,
            RecordingExport.FileExtension));
        timer = new Timer(_ => Flush(), null, FlushInterval, FlushInterval);
    }

    /// <summary>
    /// Starts capturing <paramref name="pane"/> for the rest of the session. Call once per WebView2,
    /// right after it is created and before its first navigation.
    /// </summary>
    /// <param name="pane">A short name for the pane ("board", "target") — for <see cref="EventCaptured"/>.</param>
    /// <param name="core">The pane's WebView2.</param>
    /// <param name="installToolkit">The recorder needs <see cref="ToolkitScripts.DocumentStartJs"/>
    /// (it fingerprints each target). Pass false when the host already installs it — a
    /// <see cref="WebView2BrowserSurface"/> created with its default <c>installToolkit: true</c> does
    /// — since most of those scripts are not safe to install twice.</param>
    public async Task AttachAsync(string pane, CoreWebView2 core, bool installToolkit)
    {
        if (installToolkit) await core.AddScriptToExecuteOnDocumentCreatedAsync(ToolkitScripts.DocumentStartJs);
        // The flag first, on every future document and frame; then the recorder (a no-op if the
        // host or a RecorderSession already installed it — recorder.js guards itself).
        await core.AddScriptToExecuteOnDocumentCreatedAsync(SessionFlagJs);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(ToolkitScripts.RecorderJs);
        // And the document already loaded, if any (frames already present catch up on their next load).
        try { await core.ExecuteScriptAsync(SessionFlagJs); } catch { /* no document yet */ }

        core.WebMessageReceived += (_, args) =>
        {
            var evt = RecorderSession.TryParseEvent(args.WebMessageAsJson);
            if (evt != null) Add(pane, evt);
        };
        // Navigation is captured from the host side: a page can't reliably observe its own unload.
        core.NavigationCompleted += (_, _) =>
            Add(pane, new RecorderEvent { Kind = "navigate", Url = core.Source, Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    /// <summary>Writes the recording now if anything was captured since the last write.</summary>
    public void Flush()
    {
        RecorderEvent[] snapshot;
        lock (gate)
        {
            if (events.Count == writtenCount) return;
            snapshot = [.. events];
            writtenCount = events.Count;
        }
        try
        {
            var steps = RecordingBuilder.Build(snapshot);
            if (steps.Count == 0) return;
            var json = RecordingExport.Export(steps, startedAt);
            // Rewrite the same session file in place, atomically. Run off the caller's context:
            // Dispose is called on the UI thread at window close and must not deadlock.
            Task.Run(() => ArtifactWriter.WriteTextAsync(Path.GetDirectoryName(OutputPath)!,
                Path.GetFileName(OutputPath), json, FlushOptions)).GetAwaiter().GetResult();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Final write; call when the app's window closes.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        timer.Dispose();
        Flush();
    }

    private void Add(string pane, RecorderEvent evt)
    {
        lock (gate)
        {
            if (disposed) return;
            events.Add(evt);
        }
        EventCaptured?.Invoke(pane, evt);
    }
}
