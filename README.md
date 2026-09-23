# AutoWebNav

Shared browser automation for MindAttic apps that drive websites with no API, such as
**Automata** (record/replay), **KdpPublish** (Amazon KDP) and **JobHunt** (LinkedIn Easy Apply).
It covers driving a live page reliably: waiting for navigation, trusted input, uploading files
without the native file dialog, and finding elements after a site redesign. Anything specific to
one website belongs in the app that uses it.

| Package | Target | Contents |
|---|---|---|
| `AutoWebNav` | net10.0 | `IBrowserSurface`, `IBrowserSurfaceFactory`/`IBrowserSession`, `ElementFingerprint` + `FingerprintResolver`, `BrowserActions`, `BrowserFormHelpers`, `ToolkitScripts`, `WebNavJson` |
| `AutoWebNav.WebView2` | net10.0-windows | `WebView2BrowserSurface`, `DomFileInjector`, `OffscreenWebView2Factory` |

`AutoWebNav` has no WebView2 dependency, so logic built on `IBrowserSurface` can be unit tested
against a fake.

## What it gives you

- **Hard timeouts on every page call.** WebView2's `ExecuteScriptAsync` and CDP calls have no
  timeout of their own, so a native `alert()` would otherwise hang a run forever. Each call gets
  30 s, and each navigation 60 s. Navigation completion is matched by `NavigationId`, so an
  earlier redirect can't complete the wrong navigation.
- **Trusted input.** Clicks, keystrokes and Enter are sent through CDP `Input.dispatch*`, so
  React/ARIA widgets that ignore synthetic `.click()` events still respond.
- **File upload without a native dialog.** `DomFileInjector` uses CDP `DOM.setFileInputFiles`.
  It retries for a 10 s render window, and targets an input either by expression (including the
  resolver's `window.__automataLastResolved`, which reaches into shadow roots) or by selector.
- **Self-healing element resolution.** `ElementFingerprint` records many ways to identify an
  element (id, CSS, name, classes, XPath, ARIA, label, text). `FingerprintResolver` tries them
  from most to least reliable, scores candidates when none is unique, reports near-ties as
  ambiguous instead of guessing, and returns a refreshed fingerprint when it had to fall back.
- **Page toolkit.** `ToolkitScripts.DocumentStartJs` runs at document creation in every frame: a
  closed-shadow-root registry, stability detection, fingerprinting, the resolver, a bridge into
  cross-origin frames, and list harvesting.
- **Offscreen browsers.** `OffscreenWebView2Factory` hosts a real, never-shown WebView2 for
  headless runs.

## Use

```csharp
using AutoWebNav;
using AutoWebNav.WebView2;

// installToolkit: false leaves the page exactly as the site ships it — enough for EvalAsync,
// trusted input and file injection. Keep it true (the default) to use FingerprintResolver.
IBrowserSurface browser = new WebView2BrowserSurface(webView.CoreWebView2,
    setZoom: f => Dispatcher.Invoke(() => webView.ZoomFactor = f));

await browser.NavigateAsync("https://example.com/form", ct);
var hit = await new FingerprintResolver().ResolveAsync(browser, fingerprint,
    highlight: false, refingerprint: true, timeoutMs: 10_000, ct);
if (hit.Found) await browser.ClickAtPointAsync(hit.CenterX, hit.CenterY, ct);
```

The page-side globals are named `window.__automata*`, because the toolkit was first written for
Automata. They are a wire protocol between the scripts and their callers, including Automata's
recorder, and are not renamed.

## Build, test, pack

```
dotnet build AutoWebNav.slnx
dotnet test AutoWebNav.Tests
powershell -ExecutionPolicy Bypass -File tools\pack.ps1          # -> C:\LocalNuGet
powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -CopyTo ..\Prose\lib\local-packages
```

Versions bump by whole numbers only (`1.0.0` → `2.0.0`); see `Directory.Build.props`. NuGet caches
a package by version, so a changed library needs a new version before consumers can see it.
