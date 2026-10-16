using NUnit.Framework;
using AutoWebNav;
using AutoWebNav.Tests.Fakes;

namespace AutoWebNav.Tests;

[TestFixture]
public class BrowserActionsTests
{
    // ---- SetZoomAsync ---------------------------------------------------------------------

    [Test]
    public async Task SetZoomAsync_WithinTolerance_Passes()
    {
        // 1000px at 33% reads back as 0.33003 on an emulated viewport that only has whole pixels.
        var browser = new FakeBrowserSurface { ZoomResponse = _ => 0.33003 };

        var result = await BrowserActions.SetZoomAsync(browser, 33, CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Value, Is.EqualTo("33"));
    }

    [Test]
    public async Task SetZoomAsync_OutsideTolerance_Fails()
    {
        var browser = new FakeBrowserSurface { ZoomResponse = _ => 0.47 };

        var result = await BrowserActions.SetZoomAsync(browser, 50, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("asked for 50% but the page measured 47%"));
    }

    [Test]
    public async Task SetZoomAsync_BrowserThrows_ReportsRefusalWithoutSwallowingCancellation()
    {
        var browser = new FakeBrowserSurface
        {
            ZoomResponse = _ => throw new InvalidOperationException("zoom denied"),
        };

        var result = await BrowserActions.SetZoomAsync(browser, 50, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("the browser refused to zoom: zoom denied"));
    }

    [Test]
    public void SetZoomAsync_BrowserCancels_PropagatesRatherThanReportingAsRefusal()
    {
        var browser = new FakeBrowserSurface
        {
            ZoomResponse = _ => throw new OperationCanceledException(),
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            () => BrowserActions.SetZoomAsync(browser, 50, CancellationToken.None));
    }

    // ---- ParseReadback (exercised through ClickResolvedNativelyAsync) ---------------------

    [Test]
    public async Task ClickResolvedNativelyAsync_OkFalseWithError_FailsWithThatError()
    {
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = _ => """{ "ok": false, "error": "not clickable" }""",
        };

        var result = await BrowserActions.ClickResolvedNativelyAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("not clickable"));
    }

    [Test]
    public async Task ClickResolvedNativelyAsync_OkFalseMissingError_FailsWithUnknownError()
    {
        var browser = new FakeBrowserSurface { DefaultEvalResponse = _ => """{ "ok": false }""" };

        var result = await BrowserActions.ClickResolvedNativelyAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("unknown error"));
    }

    [Test]
    public async Task ClickResolvedNativelyAsync_UnparseableJson_FailsAsUnparseable()
    {
        var browser = new FakeBrowserSurface { DefaultEvalResponse = _ => "not json at all" };

        var result = await BrowserActions.ClickResolvedNativelyAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("unparseable action result"));
    }

    // ---- ProbeResolvedCheckStateAsync ------------------------------------------------------

    [Test]
    public async Task ProbeResolvedCheckStateAsync_OkFalseWithError_FailsWithThatError()
    {
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = _ => """{ "ok": false, "error": "no element resolved" }""",
        };

        var result = await BrowserActions.ProbeResolvedCheckStateAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("no element resolved"));
    }

    [Test]
    public async Task ProbeResolvedCheckStateAsync_OkFalseMissingError_FailsWithUnknownError()
    {
        var browser = new FakeBrowserSurface { DefaultEvalResponse = _ => """{ "ok": false }""" };

        var result = await BrowserActions.ProbeResolvedCheckStateAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("unknown error"));
    }

    [Test]
    public async Task ProbeResolvedCheckStateAsync_UnparseableJson_FailsAsUnparseable()
    {
        var browser = new FakeBrowserSurface { DefaultEvalResponse = _ => "not json at all" };

        var result = await BrowserActions.ProbeResolvedCheckStateAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("unparseable probe result"));
    }

    // ---- CountResolvedFilesAsync ------------------------------------------------------------

    [Test]
    public async Task CountResolvedFilesAsync_Readable_ReturnsTheCount()
    {
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = _ => """{ "ok": true, "value": "3" }""",
        };

        var count = await BrowserActions.CountResolvedFilesAsync(browser, CancellationToken.None);

        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task CountResolvedFilesAsync_Unreadable_ReturnsMinusOne()
    {
        var browser = new FakeBrowserSurface { DefaultEvalResponse = _ => "not json at all" };

        var count = await BrowserActions.CountResolvedFilesAsync(browser, CancellationToken.None);

        Assert.That(count, Is.EqualTo(-1));
    }

    // ---- UploadToResolvedAsync --------------------------------------------------------------

    [Test]
    public async Task UploadToResolvedAsync_CrossOriginResolvedFrame_RefusesWithoutInjecting()
    {
        var browser = new FakeBrowserSurface();
        browser.EvalResponses.Enqueue(_ => """{ "ok": false }""");

        var result = await BrowserActions.UploadToResolvedAsync(browser, @"C:\file.txt", CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Does.Contain("cross-origin frame"));
        Assert.That(browser.Calls, Has.None.Matches<(string Method, string Args)>(c => c.Method == "InjectFile"));
    }

    [Test]
    public async Task UploadToResolvedAsync_SameOrigin_CallsInjectFileAsync()
    {
        var browser = new FakeBrowserSurface();
        browser.EvalResponses.Enqueue(_ => """{ "ok": true }""");

        var result = await BrowserActions.UploadToResolvedAsync(browser, @"C:\file.txt", CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(browser.Calls, Has.One.Matches<(string Method, string Args)>(
            c => c.Method == "InjectFile" && c.Args == @"C:\file.txt -> window.__automataLastResolved"));
    }

    // ---- EvalRoutedAsync frame routing (via ClickResolvedNativelyAsync) --------------------

    [Test]
    public async Task EvalRoutedAsync_WaitingOnFrames_RePollsUntilRealAnswerArrives()
    {
        var browser = new FakeBrowserSurface();
        browser.EvalResponses.Enqueue(_ => """{ "ok": false, "waitingOnFrames": true }""");
        browser.EvalResponses.Enqueue(_ => """{ "ok": false, "waitingOnFrames": true }""");
        browser.EvalResponses.Enqueue(_ => """{ "ok": true, "value": null }""");

        var result = await BrowserActions.ClickResolvedNativelyAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(browser.Calls.Count(c => c.Method == "Eval"), Is.EqualTo(3));
    }

    [Test]
    public async Task EvalRoutedAsync_NeverAnswers_ClearsPendingSlotAndFailsAtDeadline()
    {
        // No value is ever dequeued other than "still waiting", so the 5s deadline in
        // EvalRoutedAsync is what ends the call. This test runs in real time (~5s).
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = _ => """{ "ok": false, "waitingOnFrames": true }""",
        };

        var result = await BrowserActions.ClickResolvedNativelyAsync(browser, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("the frame holding the element never answered"));
        Assert.That(browser.Calls[^1], Is.EqualTo(
            ("Eval", "(function(){ window.__automataDeepAct = null; return '{}'; })()")));
    }

    // ---- SetValueOnResolvedAsync / SelectOptionOnResolvedAsync JSON-escape their value -----

    [Test]
    public async Task SetValueOnResolvedAsync_ValueWithQuotesAndNewlines_EscapesIntoAValidJsonLiteral()
    {
        const string value = "She said \"hi\"\nnew line\ttab";
        string? capturedScript = null;
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = script =>
            {
                capturedScript = script;
                return """{ "ok": true, "value": "ok" }""";
            },
        };

        await BrowserActions.SetValueOnResolvedAsync(browser, value, CancellationToken.None);

        Assert.That(capturedScript, Does.Contain(System.Text.Json.JsonSerializer.Serialize(value)));
    }

    [Test]
    public async Task SelectOptionOnResolvedAsync_OptionTextWithQuotesAndNewlines_EscapesIntoAValidJsonLiteral()
    {
        const string optionText = "Option \"B\"\nsecond line";
        string? capturedScript = null;
        var browser = new FakeBrowserSurface
        {
            DefaultEvalResponse = script =>
            {
                capturedScript = script;
                return """{ "ok": true, "value": "Option B" }""";
            },
        };

        await BrowserActions.SelectOptionOnResolvedAsync(browser, optionText, CancellationToken.None);

        Assert.That(capturedScript, Does.Contain(System.Text.Json.JsonSerializer.Serialize(optionText)));
    }
}
