using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace AutoWebNav.WebView2;

/// <summary>
/// Spectator Mode for one WebView2 pane: installs <see cref="ToolkitScripts.RecorderJs"/> once
/// per pane, then arms/disarms capture and raises a <see cref="RecorderEvent"/> for everything the
/// user does in the page while armed. Feed the captured events to
/// <see cref="RecordingBuilder.Build"/> for a readable step list, and
/// <see cref="RecordingExport.Export"/> to save it as a portable file.
/// <para>
/// This is the one shared implementation every AutoWebNav host app (JobHunt, Automata,
/// Prose.KdpPublish) wires up the same way, per the house rule that an AutoWebNav-level
/// capability is built once and adopted everywhere, not bolted onto a single app.
/// </para>
/// </summary>
public sealed class RecorderSession(CoreWebView2 core)
{
    private bool installed;
    private bool armed;

    public event Action<RecorderEvent>? EventCaptured;

    /// <summary>Registers the capture script for this pane's lifetime. Idempotent — safe to call
    /// every time a pane is (re)created even if Spectator Mode is never armed for it.</summary>
    public async Task InstallAsync()
    {
        if (installed) return;
        installed = true;
        await core.AddScriptToExecuteOnDocumentCreatedAsync(ToolkitScripts.RecorderJs);
    }

    /// <summary>Starts capturing. <see cref="InstallAsync"/> must have been called first (throws
    /// otherwise, since arming an uninstalled pane silently captures nothing).</summary>
    public async Task ArmAsync()
    {
        if (!installed) throw new InvalidOperationException($"{nameof(InstallAsync)} must be called before {nameof(ArmAsync)}.");
        if (armed) return;
        armed = true;
        core.WebMessageReceived += OnMessage;
        await core.ExecuteScriptAsync("window.__automataRecorder && window.__automataRecorder.enable()");
    }

    /// <summary>Stops capturing. Safe to call even if never armed.</summary>
    public async Task DisarmAsync()
    {
        if (!armed) return;
        armed = false;
        core.WebMessageReceived -= OnMessage;
        await core.ExecuteScriptAsync("window.__automataRecorder && window.__automataRecorder.disable()");
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var evt = TryParseEvent(args.WebMessageAsJson);
        if (evt != null) EventCaptured?.Invoke(evt);
    }

    /// <summary>Parses one posted message into a <see cref="RecorderEvent"/>, or null when it's
    /// not a trusted recorder event (not from <see cref="ToolkitScripts.RecorderJs"/>'s envelope,
    /// unparseable, or a harvest-picking message riding the same channel). Public and static: a
    /// host that routes its own <c>WebMessageReceived</c> (Automata, which also has to route
    /// harvest-picking messages on the same channel) can reuse this wire-protocol parsing without
    /// going through the rest of <see cref="RecorderSession"/>.</summary>
    public static RecorderEvent? TryParseEvent(string webMessageAsJson)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(webMessageAsJson); }
        catch (JsonException) { return null; }
        // Only the injected recorder's own envelope is trusted; anything else a page posts is noise.
        if (msg?["source"]?.GetValue<string>() != "automata-recorder") return null;
        // Harvest-picking messages ride the same channel but aren't a recorded step.
        if (msg["kind"]?.GetValue<string>() == "pick") return null;

        var evt = new RecorderEvent
        {
            Kind = msg["kind"]?.GetValue<string>() ?? "",
            TargetKind = msg["targetKind"]?.GetValue<string>(),
            Value = msg["value"]?.GetValue<string>(),
            Checked = msg["checked"]?.GetValue<bool?>(),
            SelectedText = msg["selectedText"]?.GetValue<string>(),
            Masked = msg["masked"]?.GetValue<bool>() ?? false,
            Url = msg["url"]?.GetValue<string>(),
            Ts = msg["ts"]?.GetValue<long?>() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        if (msg["fingerprint"] is JsonObject fp)
        {
            try { evt.Fingerprint = JsonSerializer.Deserialize<ElementFingerprint>(fp.ToJsonString(), WebNavJson.Options); }
            catch (JsonException) { /* a malformed fingerprint just means this event has no target */ }
        }
        return evt;
    }

    /// <summary>A host's own NavigationCompleted handler should call this — a page's own script
    /// can't reliably observe its own unload, so navigation is captured from the host side. Also
    /// re-arms capture on the fresh document: <see cref="ToolkitScripts.RecorderJs"/> re-injects
    /// dormant on every new document, and only this session knows it should stay enabled.</summary>
    public async Task OnNavigatedAsync(string url)
    {
        EventCaptured?.Invoke(new RecorderEvent { Kind = "navigate", Url = url, Ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        if (armed) await core.ExecuteScriptAsync("window.__automataRecorder && window.__automataRecorder.enable()");
    }
}
