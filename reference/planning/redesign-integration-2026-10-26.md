# Redesign integration and branch review, October 26, 2026

The preserved working changes were integrated onto
`feature/nla-redesign-integration`, starting from the verified remediation
revision `00e5ddc0bf441fcc7e89ece6a931e47e3ee40768`. The original checkout
on `feature/nla-guided-interpretation` remains untouched, including its three
modified files and four temporary evaluation-page images.

## Changes and corrections

| Commit | Change | Review result |
|---|---|---|
| `25a605f` | Mobile inspection-row border and spacing | Presentation only. Existing server completion-time labels, offline work handling and workflow transitions remain. Dart format check passed with zero changes. |
| `c82b309` | NLA report-review redesign, scheduled-cycle consent and scope corrections | Explicit sort/ranking survives the falling-behind default. Every active aggregate threshold appears in both review and preview. Three regressions cover explicit sorting, explicit ranking and combined thresholds. |
| `e8bb1dd` | Browser expectations for revised report labels and expandable details | Existing preview, execution and source-record checks remain. The two browser workflows still check PM execution and inventory without a PM period. |
| `3a0548e` | Rename pure scheduled-cycle wording helper | The `use` prefix caused the React hook lint rule to reject a regular string function. Renamed it without introducing hook state or changing behavior. |
| `1183331` | Type-safe review navigation and configured-month filtering | Guarded the last clarification field and narrowed null entries explicitly. New regressions wait for the existing catalog-readiness condition before clicking Generate. Scope assertions remain. |
| `545b531` | Visible source-record drilldown status | Browser verification caught a status paragraph hidden with the earlier panel. Moved the notice outside that panel so it stays visible during preview. The existing visibility assertion remains unchanged. |

The patch was captured from the original checkout with `git diff --binary`
and applied using `git apply --3way --index` over the remediation baseline.
All three files applied cleanly. This preserves intervening remediation
changes instead of replacing the files with older full-file copies.
Temporary images were not imported or committed.

Backend source, migrations, API contracts, provider routing and offline
storage implementation did not change. The frontend still submits plans to
backend validation, preview, confirmation and deterministic execution.
The redesigned review identifies scheduled PM cycles and requires explicit
consent before substituting them for an unsupported inspection-date request.
No new live-provider or model-accuracy result is established here.

## Verification boundary

TEST-164 records the current commands and results. TEST-163 remains the
earlier full remediation regression on its own source revision. Current
focused UI checks do not replace institutional, device or provider acceptance.
No branch was merged, deleted, reset or force-pushed during this review.

## Original working copies

The approved drafts are committed on the new integration branch. The original
checkout still has the older working versions so that no uncommitted work is
discarded. Use the committed integration revision when selecting a build.
The untracked `tmp/` images remain local research artifacts.

## Branch recommendation

The [complete branch matrix](branch-integration-review-2026-10-26.md) groups
all local and origin branch tips. The recommended consolidation line is the
current integration branch. Its tested source `545b531` includes the complete
remediation branch and preserves main ancestry. Existing styling, environment,
CPMP and older UX branches are already represented in main. Older NLA and
work-tracking branches contain versions that omit later safeguards; selecting
them during conflict resolution would regress those safeguards. Saved reports
remain on their separate lineage and require a selective port, including
native SQL migration verification. The English corpus experiment and staging
configuration remain separate. No older branch was merged or deleted.

Final focused results at `545b531` were 24/24 NLA component tests and 2/2
browser workflows. Changed-component ESLint and the production build passed.
The [execution record](../evidence/test-runs/TEST-164-preserved-redesign-integration.md)
contains exact commands, source identities and earlier failures.
