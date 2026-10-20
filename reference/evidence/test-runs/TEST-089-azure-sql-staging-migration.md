---
id: TEST-089
type: test-run
title: Live Azure SQL staging migration and schema verification
status: executed
recordedAtUtc: 2026-10-17T17:35:12Z
testedCommit: c3f9d4a1d8eebb8db607f3d257095d9f8e20111a
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Live Azure SQL staging migration and schema verification

## Execution identity and authorization

The owner executed the existing migration command from local Windows
PowerShell against the live Azure SQL staging database. Codex prepared the
guarded scripts and reviewed the resulting local logs; Codex did not receive
the password or directly authenticate to Azure SQL. This is live Azure SQL
execution evidence, not the earlier disposable native SQL verification.

- Source branch: `deploy/azure-staging`.
- Exact source: `c3f9d4a1d8eebb8db607f3d257095d9f8e20111a`.
- Approved target: `sql-unipm-staging.database.windows.net` /
  `unipm-staging-db`, resource group `rg-unipm-staging`.
- The owner explicitly approved all 22 migrations after reviewing the fresh
  preflight and confirmed no competing migration process was running.
- Execution began at `2026-10-17T17:30:54Z`; the successful follow-up began at
  `2026-10-17T17:35:12Z`. These correspond to October 18 in the owner's UTC+8
  timezone.
- SQL authentication used hidden input and process-scoped settings. Encryption
  was enabled and server-certificate validation was retained. Settings were
  restored afterward. No credential or connection-string value is recorded.

## Preflight and command

Fresh preflight verified the expected server/database, EngineEdition 5,
`IsFullTextInstalled=1`, compatibility 150, collation
`SQL_Latin1_General_CP1_CI_AS`, database-owner and required DDL permissions,
zero existing user objects, absent migration history, and no full-text
catalogs. The guarded execution additionally required no other active user
request in the database. Its preflight marker passed. The original read-only
preflight returned exit code 0.

The executed command was:

```powershell
dotnet run --project server/server.csproj --configuration Release -- --migrate-database
```

The wrapper required the exact clean source commit, the 22-file migration
inventory, and the expected final migration before invocation. It configured
the maintenance process as Production with AI and the schedule worker
disabled. The existing command applied migrations and returned before HTTP
hosting or seeding. Logs show acquisition and release of EF's exclusive
`__EFMigrationsLock`.

## Results

| Verification | Actual result |
|---|---|
| Migration command exit code | 0 |
| Applied history versus expected IDs | Exactly all 22 IDs, no missing or extra IDs |
| Final migration | `20261010141251_AddInspectionPhotoEvidence` |
| Application tables | All 22 current model tables exist, plus EF history |
| Application data | Exactly 0 rows in each of the 22 application tables |
| Compatibility | Remains 150 |
| Foreign keys | 26; none disabled or untrusted |
| Check constraints | 45; none disabled or untrusted |
| Table indexes | 61 indexes with index_id greater than 0; none disabled |
| Primary keys | Present on every user table |
| Reference full-text storage | Enabled index on `ReferenceDocumentSections` in `UniPMReferenceRetrieval` |
| Retired maintenance storage | Both derived tables and `UniPMMaintenanceRetrieval` catalog absent |

Verified operational tables include Assets, PreventiveMaintenanceSchedules,
InspectionRecords, PreventiveMaintenanceForms,
PreventiveMaintenanceAcknowledgements, InspectionPhotoEvidence,
InspectionLocationAttempts, schedule deferrals, and corrective-handoff
tracking/audit tables. Identity's seven tables and RefreshSessions exist and
are empty. ReferenceDocuments, its applicability/sections, and section
embedding storage exist and are empty.

Exact verified migration history:

```text
20260623145921_InitialDomainSchema
20260710170229_AddMaintenanceSearchDocuments
20260711001300_EnforceDomainContracts
20260711120000_AddMaintenanceFullTextSearch
20260711123548_AddMaintenanceSearchDocumentEmbeddings
20260713001356_AddIdentityAuthentication
20260717044115_EnforceOneInspectionPerSchedule
20260717153842_AddRefreshSessions
20260725082333_AddReferenceDocumentFoundation
20260727100331_AddPreventiveMaintenanceFormDomain
20260907150158_AddWaterStationInspectionWorkItems
20260917145243_SeparatePmExecutionFromAcknowledgement
20260917172750_AddCanonicalPmCycleIdentity
20260923160152_AssignScheduleOversight
20260926064226_AddInspectionLocationVerification
20260926165354_AddInspectionLocationQualityMetadata
20260930123820_RetireMaintenanceHistoryRagStorage
20261009020518_AddUniqueScheduleAssetCycle
20261009041512_AddInspectionWmsReferralTracking
20261009182058_AddScheduleEnrollmentDeferrals
20261009234631_ReviewScheduleEnrollmentDeferrals
20261010141251_AddInspectionPhotoEvidence
```

## Verification failure and correction

The migration itself completed successfully. The first postflight batch then
failed to parse because the verification query used the reserved identifier
`RowCount` without brackets. This was a harness defect, not a failed EF
migration. No migration retry, rollback, reset, or schema modification was
performed in response.

A separate read-only script changed the alias and reference to `[RowCount]`
and reran only postflight verification. It contained no migration or seeding
invocation. Its PowerShell syntax check passed, and its actual Azure SQL
execution produced the full `POSTFLIGHT_PASS` marker and final success line.
This establishes the schema results above while preserving the original
verification failure.

Before successful execution, owner-run local attempts encountered connection
timeouts. Cloud Shell tooling was prepared, but connections encountered a
database-unavailable response and an IP firewall rejection. Those attempts
did not apply migrations. The owner switched to a phone hotspot and completed
the successful local preflight. Exact temporary firewall-rule configuration
and later cleanup were not independently inspected.

Raw, reviewed execution logs remain ignored:

- `artifacts/azure-staging/azure-sql-migration-20261017T173043Z-42fca4f2b22541e0bc9fc0574346e7c6.log`
- `artifacts/azure-staging/azure-sql-postflight-20261017T173509Z-eff5d7e4b6304df9adadd1c4458f6d2d.log`

Local helper scripts remain under ignored `artifacts/azure-staging/`. Do not
rerun the migration wrapper against the now-initialized database. Future
changes require a new source review and migration plan.

## Remaining staging tasks

The verified empty schema is ready for separately authorized fictional
demo initialization. No accounts, assets, inspections, or forms were seeded.
No production code, migrations, CPMP rules, or PM workflow was changed.

App Service was not provisioned or deployed. No Azure-backed API health or
readiness endpoint was exercised. Storage account identity, private Blob
permissions, managed identity access, App Service SQL networking/runtime,
photo durability, and actual HTTPS authentication remain unverified. Existing
dependency security advisories, scheduling-effective-date approval, staging
acceptance, and physical-device verification remain separate pending items.

Review and remove temporary administrator/Cloud Shell firewall access when
no longer needed; keep broad Azure-service access disabled. No firewall
cleanup is claimed here.

Status: 22 live Azure SQL migrations applied and verified. UNMERGED, NOT
DEPLOYED, and NOT SEEDED.
