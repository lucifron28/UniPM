---
id: TEST-160
type: test-run
title: Current asset metadata and historical report semantics
status: executed
recordedAtUtc: 2026-10-25
testedCommit: 589bba8f9e5aa8929be17312c54d7301c354f37f
additionalTestedCommits:
  - 4d4ad5c8dde796c7dfe45477d5d1eab279d1d0d0
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Current asset metadata

Historical PM reports group by the asset's current department/building. Inspection
row asset labels also come from the current related asset. A recorded form
header is stored separately. No historical snapshot is inferred or backfilled.

Backend commit `83d47c8` adds a synthetic 2024 acknowledged-form transfer case.
Preview, execution, source records, inventory and official history are compared
before and after the current asset's department/building changes. Grouping and
source attributes move; counts, late results, completion clocks, official-history
eligibility and the recorded form header remain stable.

At `589bba8f9e5aa8929be17312c54d7301c354f37f`:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PmAnalyticsV3EndpointTests.Asset_transfer_updates_current_report_metadata_without_rewriting_acknowledged_history|FullyQualifiedName~InspectionQueryEndpointsTests.Inspection_history_route_still_returns_asset_history|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests.Submitted_rows_are_hidden_while_acknowledged_rows_are_official|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests.Acknowledged_form_includes_nonoperational_rows_and_excludes_operational_rows_without_actions" --logger "trx;LogFileName=phase6-current-metadata-and-official-history-rerun.trx" --results-directory artifacts/remediation/backend
```

Exit 0, 4 passed, zero failures/skips. This uses EF InMemory endpoint fixtures,
not native SQL. The first attempt at `4d4ad5c` passed three cases and failed the
new case because aggregate dimensions normalize to uppercase. Only those new
expected values changed in `589bba8`; calculations and source were unchanged.

Client commits `47d2208` and `892d950` label current asset/location information
and retain the recorded form header. The focused submitted-form mobile detail
case passed 1/1 at `4d4ad5c`; web inspection detail and form-row context cases
each passed 1/1 at `589bba8`. These commits contain the same client source.
Commands used the existing named cases in the draft mobile and inspection/form
review web test files, with concurrency/maxWorkers two. Exact filters and
outputs remain in the ignored phase6 client logs; no full client rerun is claimed.

Raw backend command/TRX details remain in `artifacts/remediation/backend/phase6.md`
and `phase6-current-metadata-and-official-history-rerun.trx`. Current metadata can
change historical grouping after transfer; historical department-at-inspection
reporting requires an approved snapshot policy and future schema work. This
record does not add transfer UI, snapshots, migration or institutional approval.
