---
codex: 1
project: AutoWebNav
code: AWN
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# AutoWebNav — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| AWN-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| AWN-T2 | 2026-10-14 | chore | closed | NUnit tests for BrowserActions (AutoWebNav/BrowserActions.cs) against FakeBrowserSurface: SetZoomAsync passes within the 0.02 tolerance (33% reading back 0.33003), fails with the measured percent outside it, and reports a refused zoom without swallowing cancellation; ParseReadback/ProbeResolvedCheckStateAsync turn ok:false, missing error and unparseable JSON into failed results; CountResolvedFilesAsync returns -1 when unreadable; UploadToResolvedAsync refuses a cross-origin resolvedFrame without calling InjectFileAsync | AutoWebNav.Tests/BrowserActionsTests.cs (commit a0d178f) |
| AWN-T3 | 2026-10-14 | chore | closed | NUnit tests for BrowserActions frame routing (EvalRoutedAsync via the public actions, FakeBrowserSurface): a waitingOnFrames result is re-polled until a real answer arrives, and on the 5s deadline the pending __automataDeepAct slot is cleared and 'the frame holding the element never answered' returned - so a later action can't inherit a stale result; also SetValueOnResolvedAsync/SelectOptionOnResolvedAsync JSON-escape values with quotes/newlines in the script | aa2cc90 / AutoWebNav.Tests/BrowserActionsTests.cs::EvalRoutedAsync_WaitingOnFrames_RePollsUntilRealAnswerArrives |
| AWN-T4 | 2026-10-14 | chore | open | NUnit tests for BrowserFormHelpers and WaitForSettleAsync (AutoWebNav/BrowserFormHelpers.cs, BrowserActions.cs): TickMatchingCheckboxesAsync returns BlockedByProcessing with the indicator and clicks nothing while busy, clicks each found box at its center and collects texts, stops at 10 iterations if a click never sticks; DefaultProcessingWordsPattern matches 'Uploading...' but not 'Upload complete'/'saving is done'; WaitForSettleAsync returns true once idle and false when the cap expires | — |
| AWN-T5 | 2026-10-14 | chore | open | NUnit tests for SessionRecording (AutoWebNav.WebView2/SessionRecording.cs) using a temp output dir and the event-add path: file name is '<app>-session-<yyyyMMdd-HHmmss>' fixed for the session, Flush writes only when new events exist since the last write, passwords stay masked in the export, Dispose does a final write and is idempotent, and EventCaptured fires after each recorded event (add a test seam for Add if needed, no live WebView2) | — |
| AWN-T6 | 2026-10-14 | chore | open | NUnit tests for LiveObservation (AutoWebNav.WebView2/LiveObservation.cs) without launching WebView2: WindowTitle is '<App> - PID: <n>', the AWN_<APP>_<PANE>_CDP_PORT env var (then the legacy var) overrides the preferred port, a taken port falls back to a free one and the registry records the port actually used, stale registry files for dead PIDs are pruned, and Dispose removes this instance's file - observers depend on that registry being accurate | — |
| AWN-T7 | 2026-10-15 | chore | closed | Remove duplicate command copies ( /quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| AWN-T8 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| AWN-T9 | 2026-10-16 | chore | open | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | user request 2026-10-16: approved repo sweep |
| AWN-T10 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| AWN-T11 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| AWN-T12 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| AWN-T13 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
