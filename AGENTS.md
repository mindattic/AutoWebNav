# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's README.md.
Common prompt commands are implemented by the shared runner; do not add a second copy under a
provider-specific command folder.

Project context: AutoWebNav is the one shared browser-automation library. Automata, Prose's
KdpPublish and JobHunt consume it as NuGet packages. Keep it site-agnostic — anything that knows
about a specific website belongs in the consuming app. The page globals `window.__automata*` are a
wire protocol; do not rename them.
