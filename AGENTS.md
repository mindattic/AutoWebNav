# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's README.md.
Common prompt commands are implemented by the shared runner; do not add a second copy under a
provider-specific command folder.

Project context: AutoWebNav is the one shared browser-automation library. Automata, KdpPublish and
JobHunt consume it as NuGet packages. Keep it site-agnostic — anything that knows about a specific
website belongs in the consuming app. The page globals `window.__automata*` and
`window.__awnObserve` / `window.__awnSessionRecording` are a wire protocol; do not rename them.

Observing a running app: every AutoWebNav host registers itself (LiveObservation) and records its
session (SessionRecording). To see what an app is doing while the person talks to you about it, use
`node tools/awn-observe.mjs` — `list`, `status <app>`, `watch <app>` (one line per event; run it
under a monitor to follow a run live), `shot <app> --pane <p>` (then read the PNG), `log <app>`.
The session recordings are in Downloads as `<App>-session-*.autowebnav-recording.json`. Read, don't
drive: `eval` can act on the page, so use it to read unless the person asked for an action.
