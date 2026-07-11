# TrBlazeUI Feedback — surfaced during TrSetup

## Summary (filled by /flow-master on consolidation)
- 0 blockers, 0 major, 1 minor, 0 nice-to-have
- Last consolidated: 2026-07-09

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
