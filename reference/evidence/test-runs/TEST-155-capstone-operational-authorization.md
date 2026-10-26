---
id: TEST-155
type: test-run
title: Capstone operational authorization correction
status: executed
recordedAtUtc: 2026-10-25
testedCommit: a82ab836120d79b12aec97ee715145fd7dbdbf0a
additionalTestedCommit: f0ea64305862a54df0abdb6b18cae060438cac57
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Operational authorization correction

## Source and review

Backend commit `8aca6eae9b2ef153c71efd3a8208431b62dd51ed` adds one
SQL-queryable read-scope helper for forms, inspections, photos and dashboard
inputs. It protects legacy anonymous reads and removes automatic operational
privileges from Admin alone. Commit `a82ab836120d79b12aec97ee715145fd7dbdbf0a`
adds two missing feature-namespace imports in the new test fixture after the
initial compile attempt. That initial attempt executed no tests.

Web commit `2aa2a2c9287111ebeb0ac130258618fd718b2027` adds Supervisor
read-only form review and matches route/navigation guards to operational
roles. Admin alone receives a technical landing without operational requests.
Commit `6ed6bca12b83acf5520963d12e8fa358c0bb83ab` adds a Quick search
assertion. Commit `f0ea64305862a54df0abdb6b18cae060438cac57` guards an
indexed link in the new test after TypeScript rejected the unchecked lookup.
The main chat reviewed the actual diffs. No migration or lifecycle change
occurred.

## Access matrix

| Area | Enforced access |
|---|---|
| Asset detail/list/registry/options/summary/code/QR | GSD, Inspector and Supervisor share the operational master catalog. Anonymous requests receive 401; Admin alone and DepartmentHead receive 403. |
| Asset verification-location configuration | GSD, as the existing operational workflow requires. |
| Form list/detail | GSD all; Inspector creator, historical row owner or current assigned-work member; Supervisor current assigned-schedule member. Filtering precedes related-record loading. |
| Inspection history/list/detail/registry/options/summary | Same readable form/schedule scope. Official history still requires acknowledgement, with the existing legacy standalone-row exception. |
| Private photo read | Same readable form/schedule scope; absent or unreadable IDs receive 404. GSD can review Draft photos as part of administrative visibility. |
| Photo upload/delete and form mutation | Existing write, row-ownership and Draft/lifecycle checks remain. Supervisor read access does not grant mutation or acknowledgement. |
| PM dashboard cycles/data | GSD all; Inspector currently assigned schedules; Supervisor currently supervised schedules. Counts and source rows follow query scope. |
| Schedule reads | Existing GSD/Supervisor schedule-wide metadata access remains; Inspectors see assigned schedules. Admin bypasses are removed. This is distinct from the tighter form/inspection/photo scope. |
| NLA and corrective/WMS handoff | Existing GSD-only access remains. |
| Technical Admin management/demo routes | Admin-only administration remains. Admin with an operational role also receives that role's normal operational scope. |

A legitimate member sees the whole shared multi-row form, including other
rows in that same batch. This preserves whole-form receipt and does not grant
access to unrelated Drafts. Historical row ownership survives reassignment.
No historical Supervisor assignment or final institution-wide RBAC policy was
invented. The dashboard service requires the caller principal explicitly;
there is no null-principal default that silently returns unscoped data.

## Backend execution

Both commands ran at `a82ab836`:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --logger "trx;LogFileName=phase2-authorization-rerun.trx" --results-directory artifacts/remediation/backend --filter "FullyQualifiedName~AuthorizationPolicyTests|FullyQualifiedName~OperationalInspectionReadScopeEndpointsTests|FullyQualifiedName~InspectionPhotoEvidenceEndpointsTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~PreventiveMaintenanceScheduleGenerationTests|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests|FullyQualifiedName~PmPeriodDashboardTests|FullyQualifiedName~DevelopmentDemoSeederTests|FullyQualifiedName~PmAnalyticsTests"
```

Exit 0: 188 passed, 0 failed, 1 skipped, 189 total.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --logger "trx;LogFileName=phase2-read-contracts.trx" --results-directory artifacts/remediation/backend --filter "FullyQualifiedName~AssetReadEndpointsTests|FullyQualifiedName~InspectionQueryEndpointsTests|FullyQualifiedName~PreventiveMaintenanceFormRegistryQueryEndpointsTests"
```

Exit 0: 28 passed, 0 failed, 0 skipped. The single earlier skip requires the
SQL Server 2019 test setting, which was not configured for these runs. Native
SQL discovery is not a substitute for executing that test.

## Web execution

Node 22.23.4, two workers. Each command ran separately:

| Command | Source | Result |
|---|---|---|
| `npm run test:run -- src/components/layout/app-shell-access.test.tsx` | `a82ab836`, web unchanged from `6ed6bca` | Exit 0; 27 passed |
| `npm run test:run -- src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx` | `f0ea643` | Exit 0; 21 passed |
| `npm run typecheck` | `f0ea643` | Exit 0 after the test-only indexed-link correction |
| Targeted `npm exec eslint --` across the ten changed web files | `f0ea643` | Exit 0; the exact file arguments are retained in the raw log |

The Supervisor regression checks whole-form reading, hidden acknowledgement
controls, denied acknowledgement-review routing and zero acknowledgement
requests. Admin and DepartmentHead route tests assert denial before data loads.

## Artifacts and limits

Ignored backend TRX/logs and the exact endpoint matrix are under
`artifacts/remediation/backend/phase2*`. Web logs are
`artifacts/remediation/clients/phase2*`. Prior failed compile/typecheck attempts
are retained, not represented as passed executions.

These are synthetic InMemory/API and mocked-browser tests. No native SQL
authorization translation, physical acceptance, live provider request,
deployment or independent GSD approval is established by this record.

## Command-retention correction, October 25

The statement above that the Phase 2 lint raw log retained exact file arguments
was incorrect. It contains only `EXIT=0`, without the command, file arguments,
runtime or SHA. The lint outcome is executor-reported, with incomplete retained
identity. TEST-156 records this provenance gap and a separate actual Phase 3
command manifest. The backend TRX and web test/typecheck results are unaffected.
