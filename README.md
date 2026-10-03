# AutoWebNav

Shared .NET 10 browser-automation layer for WebView2 apps: hard timeouts, trusted CDP input, dialog-free file upload and a self-healing element resolver that keeps finding controls after a site redesign.

![C#](https://img.shields.io/badge/language-C%23-239120) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![WebView2](https://img.shields.io/badge/host-WebView2-0078D4) ![Version 2.0.0](https://img.shields.io/badge/version-2.0.0-blue) ![License MIT](https://img.shields.io/badge/license-MIT-green)

```text
  your app (Automata, KdpPublish, JobHunt ...)
        |
        v
  +-------------------------- AutoWebNav (net10.0, no WebView2 dependency) ---------------+
  |  IBrowserSurface / IBrowserSurfaceFactory / IBrowserSession                           |
  |  ElementFingerprint + FingerprintResolver   BrowserActions   BrowserFormHelpers       |
  |  RecordingBuilder + RecordingExport         PageDeclutter    KnownFolders             |
  |  ToolkitScripts: stability, fingerprint, resolver, harvest, closed roots, frames,     |
  |                  recorder (injected into every frame at document creation)           |
  +---------------------------------------------------------------------------------------+
        |
        v
  +---------------------- AutoWebNav.WebView2 (net10.0-windows) --------------------------+
  |  WebView2BrowserSurface   DomFileInjector   OffscreenWebView2Factory   RecorderSession |
  +---------------------------------------------------------------------------------------+
        |
        v
  a live WebView2 page, driven over the Chrome DevTools Protocol
```

## Why

- A native `alert()` or a hung page can no longer freeze a run forever: every script call times out at 30 s and every navigation at 60 s.
- React and ARIA widgets that ignore synthetic `.click()` still respond, because clicks, keystrokes and Enter are real CDP input events.
- File uploads work without ever opening the Windows file picker.
- Automation survives a site redesign: an element is remembered eight different ways, and the resolver re-learns it when it had to fall back.
- Logic built on `IBrowserSurface` can be unit tested against a fake, with no browser at all.
- Fix browser mechanics once and every MindAttic app that drives a website gets the fix.

## Features

### Two packages

| Package | Target | Contents |
|---|---|---|
| `AutoWebNav` | net10.0 | `IBrowserSurface`, `IBrowserSurfaceFactory` and `IBrowserSession`, `ElementFingerprint` and `FingerprintResolver`, `BrowserActions`, `BrowserFormHelpers`, `ToolkitScripts`, `WebNavJson`, `PageDeclutter`, `KnownFolders`, the `Recording` namespace |
| `AutoWebNav.WebView2` | net10.0-windows | `WebView2BrowserSurface`, `DomFileInjector`, `OffscreenWebView2Factory`, `RecorderSession` |

`AutoWebNav` has no WebView2 dependency, so anything built on `IBrowserSurface` can be tested against a fake.

### Driving a page reliably

- **Hard timeouts on every page call.** WebView2's `ExecuteScriptAsync` and CDP calls have no timeout of their own. `WebView2BrowserSurface` gives each call 30 s and each navigation 60 s, and throws a `TimeoutException` that names the call. Navigation completion is matched by `NavigationId`, so an earlier redirect cannot complete the wrong navigation.
- **Trusted input.** Clicks, keystrokes and Enter go through CDP `Input.dispatchMouseEvent` and `Input.dispatchKeyEvent`.
- **File upload without a native dialog.** `DomFileInjector` uses CDP `DOM.setFileInputFiles`. It retries for a 10 s render window and targets an input either by expression (including the resolver's `window.__automataLastResolved`, which reaches into shadow roots) or by selector.
- **Element actions with read-back.** `BrowserActions` types by keystrokes, sets values through the native property setter (React-safe), clicks, probes and sets check state, selects options, reads text and values, counts attached files, uploads, zooms and waits for the page to settle.
- **Form helpers.** `BrowserFormHelpers` ticks matching checkboxes, native or `role=checkbox` widgets, and refuses to click while the page still shows a processing indicator such as an upload in progress.
- **Offscreen browsers.** `OffscreenWebView2Factory` hosts a real, never-shown WebView2 that never steals focus, for headless runs.
- **Decluttering.** `PageDeclutter.Script(selectors)` hides chat overlays, upsell asides and similar clutter with `display:none !important` before the page draws, including inside shadow roots.

### Self-healing element resolution

`ElementFingerprint` records many ways to identify an element: id, CSS selector, name, classes, XPath, ARIA role and label, nearby label text and visible text. `FingerprintResolver` tries them from most to least reliable:

```text
#id -> css selector -> tag[name] -> tag.classes -> xpath -> aria label -> label text -> visible text
```

The first strategy with exactly one visible match wins. When none is unique, candidates are scored; a clear leader wins and a near-tie is reported as ambiguous instead of guessed. When it had to fall back, the resolver returns a refreshed fingerprint so the caller can save the new identity.

### The page toolkit

`ToolkitScripts.DocumentStartJs` runs at document creation in every frame. It carries a closed-shadow-root registry, stability detection (which ids and classes are worth trusting), fingerprinting, the resolver, a `postMessage` bridge into cross-origin frames, and list harvesting. `ToolkitScripts.RecorderJs` adds capture of user actions.

### Recording (Spectator Mode)

- `RecorderSession` installs the recorder once per WebView2 pane, then arms and disarms capture and raises a `RecorderEvent` for each user action.
- `RecordingBuilder.Build(events)` coalesces the raw stream into a readable step list: keystroke bursts become one typed value, focus-clicks before typing vanish, checkbox toggles collapse to the final state, dropdown-opening clicks fold into the option picked, and submit-looking clicks are flagged as commit points. It is safe to re-run after every event, so a live preview matches the final result.
- `RecordingExport` saves and loads a recording as one portable `*.autowebnav-recording.json` file (format `autowebnav-recording`, version 1), independent of any one app's data model.
- `KnownFolders.Downloads` returns the account's real Downloads folder, honouring a relocation made in Windows Settings, as the natural place to drop a recording.

## Quick start

Prerequisites: Windows, the .NET 10 SDK, and the WebView2 Runtime for `AutoWebNav.WebView2`.

Build the packages into the local feed every MindAttic consumer's `NuGet.config` lists:

```powershell
git clone https://github.com/mindattic/AutoWebNav
cd AutoWebNav
powershell -ExecutionPolicy Bypass -File tools\pack.ps1
```

The script runs the tests first and packs only if they pass, writing `AutoWebNav.2.0.0.nupkg` and `AutoWebNav.WebView2.2.0.0.nupkg` to `C:\LocalNuGet`. Then reference them from your app:

```xml
<PackageReference Include="AutoWebNav.WebView2" Version="2.0.0" />
```

## Usage

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

The page-side globals are named `window.__automata*`, because the toolkit was first written for Automata. They are a wire protocol between the scripts and their callers, including Automata's recorder, and are not renamed.

## API

`IBrowserSurface` is the whole contract a host implements:

| Member | Does |
|---|---|
| `CurrentUrl` | The page's current address |
| `NavigateAsync(url, ct)` | Load a URL and wait for that navigation to finish |
| `EvalAsync(script, ct)` | Evaluate JavaScript on the page and return its result, under a hard timeout |
| `InjectFileAsync(filePath, elementJs, ct)` | Attach a local file to a file input without a dialog |
| `ClickAtPointAsync(x, y, ct)` | Trusted mouse click at page coordinates |
| `TypeTextAsync(text, ct)` | Trusted keystrokes into the focused element |
| `PressEnterAsync(ct)` | Trusted Enter key press |
| `SetZoomAsync(factor, ct)` | Zoom the page and return the factor the page measured afterwards |

`IBrowserSurfaceFactory.CreateAsync(profileKey, ct)` returns an `IBrowserSession` (disposable) for a named browser profile; `OffscreenWebView2Factory` is the WebView2 implementation.

`WebNavJson.Options` is the serializer configuration for everything that crosses into the page scripts. Its camelCase policy is load-bearing, because the scripts read fingerprint keys by their camelCase names.

## Building and testing

```powershell
dotnet build AutoWebNav.slnx
dotnet test AutoWebNav.Tests
powershell -ExecutionPolicy Bypass -File tools\pack.ps1
powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -CopyTo ..\Prose\lib\local-packages
```

`-CopyTo` also copies both packages into a consumer repo's checked-in package folder, so its CI can restore without `C:\LocalNuGet`.

The NUnit tests in `AutoWebNav.Tests` cover the fingerprint resolver (against a fake surface), recording coalescing and export, page decluttering, known folders and the JSON options.

Versions bump by whole numbers only (`1.0.0` to `2.0.0`); see `Directory.Build.props`. NuGet caches a package by version, so a changed library needs a new version before consumers can see it.

## Project layout

```text
AutoWebNav/              core library: surface contracts, resolver, actions, recording
AutoWebNav/Scripts/      page toolkit JS, embedded as resources
AutoWebNav.WebView2/     WebView2 host implementation
AutoWebNav.Tests/        NUnit tests
tools/pack.ps1           test, pack and publish to the local NuGet feed
Directory.Build.props    shared version, license and package metadata
```

## Scope

AutoWebNav covers how to drive a live page reliably. Anything specific to one website belongs in the app that uses it.

## Documentation

- Agent and contributor instructions: [AGENTS.md](AGENTS.md)
- The main consumer and the best worked example of this library: [Automata](https://github.com/mindattic/Automata)

## License

MIT. See [LICENSE](LICENSE).

---

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [Automata](https://github.com/mindattic/Automata).
