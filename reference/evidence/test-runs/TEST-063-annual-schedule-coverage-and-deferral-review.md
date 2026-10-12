---
id: TEST-063
type: test-run
title: Annual PM coverage safeguards and GSD deferral review verification
status: executed
recordedAtUtc: 2026-10-10T01:28:27Z
testedCommit: 7db4c34e20fd4dcca9de167b6a3acf99e8aa1c94
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Annual PM coverage safeguards and GSD deferral review verification

## Execution identity

The backend and production web source in the verification runs matched the
source commit below. Web mock fixtures were aligned with the updated API after
the backend suite and production build; the full web unit/browser runs and
final formatting, lint, typecheck, and diff checks ran after those fixture
edits. The staged tree hash was
`521fe57029c3ab20bb765334347cd5171d48fe25`, identical to the tree recorded by
tested commit `7db4c34e20fd4dcca9de167b6a3acf99e8aa1c94`. No production source
changed after its build or focused verification. The evidence files in this
record were added afterward.

Branch: `feat/pre-evaluation-ux-motion`. Environment: Windows, .NET SDK 10.0.300,
Node.js 24.15.0, npm 11.12.1. The web package declares Node.js `>=22 <23`;
local checks ran under Node 24, and CI remains the supported-runtime check. No
AI provider was called.

## Commands and results

| Command | Result |
| --- | --- |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PreventiveMaintenanceScheduleGenerationTests|FullyQualifiedName~ScheduleOpenApiContractTests|FullyQualifiedName~Sql_Server_2019_review_migration_round_trips_and_concurrent_recovery_is_idempotent" --logger "console;verbosity=normal" -m:1 -p:BuildInParallel=false` | 26 passed, 1 SQL Server 2019 fact skipped. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --logger "console;verbosity=minimal" -m:1 -p:BuildInParallel=false` | 346 passed, 0 failed, 38 skipped. SQL Server and optional provider facts were not run. |
| `dotnet build server/server.csproj --configuration Release --no-restore -m:1 --verbosity minimal` | Passed with 0 errors and one existing CS8602 warning in `PreventiveMaintenanceFormEndpoints.cs:540`, outside this change. |
| `dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build` | No pending model changes. A process-only dummy connection value allowed design-time model creation; the command did not connect to or modify a database. |
| `dotnet format whitespace server/server.csproj --verify-no-changes --no-restore --include Data/ApplicationDbContext.cs Features/Schedules/PreventiveMaintenanceScheduleGenerationService.cs Features/Schedules/PreventiveMaintenanceScheduleGenerationWorker.cs Features/Schedules/ScheduleEnrollmentDeferralReason.cs Features/Schedules/SchedulesEndpoints.cs Features/Schedules/ScheduleGenerationOptions.cs Migrations/ApplicationDbContextModelSnapshot.cs Migrations/20261009234631_ReviewScheduleEnrollmentDeferrals.cs Migrations/20261009234631_ReviewScheduleEnrollmentDeferrals.Designer.cs Models/ScheduleEnrollmentDeferral.cs Program.cs` | Passed for changed server C# files. |
| `dotnet format whitespace tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --verify-no-changes --no-restore --include Infrastructure/SqlServerDomainContractTests.cs Schedules/PreventiveMaintenanceScheduleGenerationTests.cs Schedules/ScheduleOpenApiContractTests.cs` | Passed for changed test C# files. |
| `npm run api:contract:check` | Passed. |
| `npm run format:check` | Passed. |
| `npm run lint` | Passed with zero ESLint warnings. |
| `npm run typecheck` | Passed. |
| `npm run test:run` | 27 files passed; 210 tests passed. |
| `npm run build` | Passed; TypeScript passed and Vite transformed 2,207 modules. |
| `npm run e2e -- e2e/schedules.spec.ts e2e/pm-period-dashboard.spec.ts` | 8 passed. Playwright's local server required an unsandboxed run. |
| `git diff --check` | Passed. |

The first focused backend attempt found missing test-file imports; those were
added before the passing focused and full runs. Focused web runs exposed stale
query assertions and an uncoerced generated count; the tests and count handling
were corrected. The full web suite and affected Playwright files then passed.

## SQL Server 2019 verification

Native SQL Server 2019 with Full-Text Search was unavailable. The specific
round-trip test was discovered and skipped because
`UNIPM_SQLSERVER2019_TEST_CONNECTION` was not configured; the full suite's SQL
Server facts were also skipped. Migration Up → Down → Up, compatibility level
150, persisted-data preservation, and SQL Server concurrency behavior are
therefore **NOT VERIFIED locally**. The EF model snapshot is consistent, but
that does not prove migration execution against SQL Server.

The first full-project `dotnet format whitespace` check also reported four
pre-existing whitespace findings in the unchanged
`NaturalLanguageAnalyticsInterpretationPipeline.cs` and `InspectionRecord.cs`.
Scoped formatting checks for all changed C# files passed.

## Accessibility, deployment, and acceptance limits

Reduced-motion handling and primary-navigation focus styling were source
inspected. Automated schedule/dashboard browser checks passed, but a complete
WCAG audit, screen-reader review, GSD acceptance session, deployment, and
database-backed manual workflow were not performed. The branch remains
unmerged.
