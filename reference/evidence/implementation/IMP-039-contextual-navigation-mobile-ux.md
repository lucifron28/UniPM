---
id: IMP-039
type: implementation
title: Contextual web navigation and mobile workflow usability
status: reviewed
recordedAtUtc: 2026-10-01T03:25:21Z
sourceCommit: b54a50355c958ae3e99f0c3512847fdccecab703
sourceBranch: refactor/web-mobile-ux-audit
evidenceLevel: source-inspected
---

# Contextual web navigation and mobile workflow usability

The branch started from main `352e9076006a361c9ec724dca48f0f1f108f578c`,
after CPMP PR #80 merged. Main Backend and Web CI passed on that base.
A read-only browser session reproduced dashboard August 2026 to asset detail
showing an incorrect "Back to assets" return.

## Audit findings

No P0 defect was confirmed. P1 fixes preserve web drill-down origin and refresh
mobile PM data after a child workflow returns. P2 fixes move prominent technical
IDs below operational details, put mobile PM entry above history, rebuild the
search clear icon, and order field tasks by urgency and canonical cycle.

| Origin/action | Destination | Baseline return | Result |
|---|---|---|---|
| Assets registry / asset link or View details | Asset | Assets with registry search | Preserve filters, search and page |
| Dashboard / asset code | Asset | Assets | Dashboard with category, year, cycle, department, condition, timeliness and search |
| Asset / View source | Inspection | Inspections | Asset code with inherited origin |
| Inspections registry / View details | Inspection | Inspections with registry search | Preserve filters and page |
| Inspection / asset code | Asset | Assets | Inspection with inherited origin |
| Inspection / scheduled month | Schedule | Schedules | Inspection with inherited origin |
| Schedules registry / View details | Schedule | Schedules with registry search | Preserve filters and page |
| Schedule / asset code | Asset | Assets | Schedule with inherited origin |
| Form registry / form number | Form | Form review | Explicit form-review origin |
| Dashboard / Review batch | Batch review | Dashboard with filters | Preserve existing behavior through typed context |
| Batch review / inspection | Inspection | Batch review with filters | Preserve existing behavior through typed context |
| Batch review / full form | Form | Batch review with filters | Preserve existing behavior through typed context |
| Dashboard / View batch | Form | Dashboard with supplied filters | Preserve existing behavior through typed context |
| Direct Asset, Inspection or Schedule URL | Detail | Canonical registry | Keep deterministic fallback after refresh |

## Web implementation

`detail-navigation.ts` validates a typed URL search context against local route
kinds, UUIDs and existing filter fields. `DetailBackLink` maps that context to
TanStack Router links. There is no browser-history dependency or arbitrary
external return URL. Entity ancestry is bounded to four hops; repeated entity
visits remove duplicate ancestry while retaining the terminal origin.

Registry links, dashboard asset/batch links, asset history, inspection-to-asset
and inspection-to-schedule links, schedule-to-asset links, full forms and batch
review carry the relevant context. Error states offer the same contextual
return. Existing legacy review/dashboard search fields remain supported.

Inspection H1 now uses the asset code when available, followed by the actual
inspection date and operational outcome. Inspection, asset, schedule and worker
IDs remain in a lower Record information section. Form file number remains H1;
form and creator/submitter IDs move below the form content. Asset and schedule
headings already used operational identifiers and were retained.

Back links have explicit keyboard focus styling and a minimum height. Existing
registry Apply/Clear and page-reset behavior was consistent and was retained.
No data-table rewrite or design-system change was needed.

## Mobile implementation

Home callback types accept `FutureOr<void>`. Home awaits child routes and then
reloads forms and schedules once, including Draft resume, acknowledgement,
QR/manual/search entry, batch start and the GSD form list. The shell now awaits
the GSD list route. There is no polling.

PM entry precedes maintenance history on asset lookup. History remains available,
and Scan another QR is an outlined secondary action. Search typing immediately
rebuilds the clear icon; clearing cancels the pending debounce and reloads the
unfiltered results.

Home orders field-work cards Overdue, Ongoing and Due, then PM cycle. Active
Drafts participate as Ongoing while keeping their In Progress label and resume
action. Department/category provide deterministic ties. Assignment filtering
and Department + AssetCategory + PmCycle batch identity remain unchanged.

The GSD Create draft path was audited and retained. Acknowledgement wording
continues to distinguish receipt/noting from approval; its meaning was retained.
Inspection row saves, Draft reuse, geolocation, whole-batch gating, lifecycle,
visibility, CPMP deadlines and progress/compliance formulas were not changed.

## Verification boundary

TEST-046 records local execution, including test setup corrections and exact
commit identities. Browser checks cover representative desktop/mobile widths,
refresh, keyboard returns and page overflow. This is not a WCAG certification.
Physical Android/iOS, real camera/touch behavior and production deployment remain
unverified. No backend, API contract, database, NLA or RAG code changed.
The paused RAG worktree's saved file status and diff SHA256 matched at final audit.
