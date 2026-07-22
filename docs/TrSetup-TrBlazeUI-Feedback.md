# TrBlazeUI Feedback — surfaced during TrSetup

## Summary (filled by /flow-master on consolidation)
- 0 blockers, 0 major, 2 minor, 0 nice-to-have
- Last consolidated: 2026-07-20

## Issues

<!-- Append entries as gaps are found. IDs are append-only, never renumbered.
     TrBlazeUI → TR-NNN · TechieRag → TR-RAG-NNN -->

### TR-001 — `Empty` component: reference example does not match the shipped API
- **Severity:** minor (documentation, not a code defect)
- **Repro:** The TrBlazeUI-AI-Reference / UIDesign design-system example shows `<Empty><EmptyIcon/><EmptyTitle/><EmptyDescription/></Empty>` (child sub-components).
- **Expected:** Using the documented sub-components compiles.
- **Actual:** The shipped `Empty` component takes `Icon` (a `RenderFragment`) plus `Title` / `Description` (`string`) parameters — the `EmptyIcon`/`EmptyTitle`/`EmptyDescription` sub-components don't exist. Building REQ-UI-006's profile-details empty state against the documented shape would not compile.
- **Encountered in:** REQ-UI-006 (Settings profile-details Empty state)
- **Workaround:** Used the real API — `<Empty Title="…" Description="…"><Icon><LucideIcon …/></Icon></Empty>`.
- **Suggested fix:** Update the reference/design-system example to the shipped `Icon`/`Title`/`Description` API (or ship the sub-components).

### TR-002 — `Select.SelectContent<T>.DisposeAsync()` throws unhandled `JSDisconnectedException` on circuit teardown
- **Severity:** minor (log noise; no user-visible breakage observed)
- **Repro:** Blazor Server host with a `Select` rendered (TrSetup `/settings`, `/` board). Close the browser tab / let the circuit drop. Every disconnect logs a `fail:` entry.
- **Expected:** Disposal during circuit teardown is a normal lifecycle event — the JS-interop call should be skipped or its `JSDisconnectedException` swallowed, per the standard Blazor guidance for `IAsyncDisposable` components.
- **Actual:** Two stack traces per disconnect:
  `warn: …RemoteRenderer[100] Unhandled exception rendering component: JavaScript interop calls cannot be issued at this time…`
  `fail: …CircuitHost[111] Unhandled exception in circuit '…'`
  both terminating at `TrBlazeUI.Primitives.Select.SelectContent\`1.DisposeAsync()`.
- **Encountered in:** TrSetup verify runs, 2026-07-20 (335-line server log was dominated by this; it masks real `fail:` entries during triage).
- **Workaround:** None applied — it is library-internal. Consumers cannot intercept the disposal.
- **Suggested fix:** Wrap the `JSObjectReference.DisposeAsync()` / interop call in `SelectContent.DisposeAsync()` with `try { … } catch (JSDisconnectedException) { }` (and ideally `catch (OperationCanceledException)`), which is the documented pattern for components holding JS references on Blazor Server.
