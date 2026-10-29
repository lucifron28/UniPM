---
id: TEST-187
type: test-run
title: Unsupported intent, inclusive comparisons, invalid plans and completed-draft countdown
status: executed
recordedAtUtc: 2026-10-29T08:36:39Z
testedCommit: 2ec9780cb3e9ae130626784c48fe21357cd4379e
sourceBranch: feature/nla-guard-countdown-fixes
evidenceLevel: locally-executed
---

# Guard and countdown corrections

## Reproduction and fixes

New focused tests reproduced the reported defects before implementation:
10 of 12 backend cases failed, and all three completed-form widget cases
displayed the unwanted overdue warning. The two existing ambiguous-OR controls
and unfinished-work widget control passed.

- The mutation guard matched singular schedule/inspection/asset names but missed
  plurals. It now recognizes both while allowing report-generation wording such
  as "create a report" and "complete list". No write endpoint was added.
- Explicit unsupported operations were checked after parser subject inference.
  They now precede subject inference and provider dispatch. Forecast rejection
  also precedes Boolean clarification, including "predict late or missed".
- Every unrecognized OR triggered Boolean clarification. Inclusive completion
  wording and numeric comparisons are distinguished from filter unions. The
  separate late-only validator also removes the inclusive completion phrase
  before looking for a late-only request. Inclusive numeric thresholds retain
  their operator and value for validation; a reversed model comparison still
  requires clarification.
- A model plan with schema errors previously raised InvalidPlan/503. When every
  error maps to an existing clarification field, the backend returns
  NeedsClarification with a null plan and InvalidPlanNeedsClarification. The
  invalid plan is never repaired or executed. Unknown schema/envelope errors
  and provider failures retain their existing failure behavior.
- BatchPmCard inferred completion from existing completed/total counts but did
  not use that state to suppress its countdown. Fully inspected Draft,
  Submitted and Acknowledged cards now show "Inspections complete" and retain
  the lifecycle badge. Incomplete work retains countdown and assignment context.
  A 393-pixel test exposed a 4.8-pixel progress-label overflow; making that text
  flexible fixes it without changing counts or urgency ordering.

No production prompts, gold labels, historical scores, schedules or lifecycle
rules were changed. The parser's supported wording and shared guards did change
as described above, before the final paired repeat.

## Executed checks

| Command | Result |
| --- | --- |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter FullyQualifiedName~PmAnalyticsV3 --artifacts-path artifacts/guard-build --no-restore` | 61 passed, three opt-in SQL numerical tests skipped |
| `flutter test test/batch_pm_card_completion_test.dart test/scanned_asset_pm_presentation_test.dart test/home_page_return_ux_test.dart` | 20 passed |
| `flutter analyze lib/ui/widgets/batch_pm_card.dart lib/ui/widgets/pm_progress_indicator.dart test/batch_pm_card_completion_test.dart` | No issues |
| `dotnet build server/server.csproj --artifacts-path artifacts/guard-build --no-restore` | Passed, zero warnings/errors on the initial frozen correction source |
| Dart formatting and C# whitespace formatting | Applied to changed source and tests |

The last backend regression run tested the final priority correction commit
listed in the metadata. Earlier runs had 60 passing cases before the additional
forecast-with-OR regression was added. The final source binary was also exercised
against native SQL Server by TEST-189.

The ordinary server output directory was locked by a running developer API.
Verification used a separate ignored build-output directory rather than stopping
that API or creating a worktree. Fake model responses in unit tests establish
orchestration and validation behavior; they are not live model quality evidence.

## Evidence boundary

The mobile checks are widget tests, including a 393-pixel viewport, not physical
device acceptance. No staging deployment or ordinary GSD database mutation was
performed. Raw logs remain ignored under `artifacts/nla-*.log` and
`artifacts/countdown-*.log`.

The first post-fix repeat is retained as TEST-188. The final guard-priority
source is evaluated separately in TEST-189. Both reuse the frozen TEST-181
development corpus and scoring without changing historical evidence.
