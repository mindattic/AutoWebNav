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
| AWN-T2 | 2026-10-14 | chore | open | NUnit tests for BrowserActions (AutoWebNav/BrowserActions.cs) against FakeBrowserSurface: SetZoomAsync passes within the 0.02 tolerance (33% reading back 0.33003), fails with the measured percent outside it, and reports a refused zoom without swallowing cancellation; ParseReadback/ProbeResolvedCheckStateAsync turn ok:false, missing error and unparseable JSON into failed results; CountResolvedFilesAsync returns -1 when unreadable; UploadToResolvedAsync refuses a cross-origin resolvedFrame without calling InjectFileAsync | — |
| AWN-T3 | 2026-10-14 | chore | open | NUnit tests for BrowserActions frame routing (EvalRoutedAsync via the public actions, FakeBrowserSurface): a waitingOnFrames result is re-polled until a real answer arrives, and on the 5s deadline the pending __automataDeepAct slot is cleared and 'the frame holding the element never answered' returned - so a later action can't inherit a stale result; also SetValueOnResolvedAsync/SelectOptionOnResolvedAsync JSON-escape values with quotes/newlines in the script | — |
| AWN-T4 | 2026-10-14 | chore | open | NUnit tests for BrowserFormHelpers and WaitForSettleAsync (AutoWebNav/BrowserFormHelpers.cs, BrowserActions.cs): TickMatchingCheckboxesAsync returns BlockedByProcessing with the indicator and clicks nothing while busy, clicks each found box at its center and collects texts, stops at 10 iterations if a click never sticks; DefaultProcessingWordsPattern matches 'Uploading...' but not 'Upload complete'/'saving is done'; WaitForSettleAsync returns true once idle and false when the cap expires | — |
| AWN-T5 | 2026-10-14 | chore | open | NUnit tests for SessionRecording (AutoWebNav.WebView2/SessionRecording.cs) using a temp output dir and the event-add path: file name is '<app>-session-<yyyyMMdd-HHmmss>' fixed for the session, Flush writes only when new events exist since the last write, passwords stay masked in the export, Dispose does a final write and is idempotent, and EventCaptured fires after each recorded event (add a test seam for Add if needed, no live WebView2) | — |
| AWN-T6 | 2026-10-14 | chore | open | NUnit tests for LiveObservation (AutoWebNav.WebView2/LiveObservation.cs) without launching WebView2: WindowTitle is '<App> - PID: <n>', the AWN_<APP>_<PANE>_CDP_PORT env var (then the legacy var) overrides the preferred port, a taken port falls back to a free one and the registry records the port actually used, stale registry files for dead PIDs are pruned, and Dispose removes this instance's file - observers depend on that registry being accurate | — |
| AWN-T7 | 2026-10-15 | chore | closed | Remove duplicate command copies ( /quickload, /quicksave) now centralized at the user level (~/.claude/commands) | WF-T14 |
