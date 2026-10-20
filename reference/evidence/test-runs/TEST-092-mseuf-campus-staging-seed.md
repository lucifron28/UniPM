---
id: TEST-092
type: test-run
title: MSEUF campus seed and staging safeguard verification
status: executed
recordedAtUtc: 2026-10-17T18:55:50Z
testedCommit: a5e890d69630130b95c7b4ebd78d4fdfe9914854
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# MSEUF campus seed and staging safeguard verification

The campus-name refinement was checked against the fixed synthetic dataset
counts, record identifiers, CPMP month sets, authentication tests and staging
initialization safeguards. Facility sources and the line between public names
and synthetic inventory are documented in
[`mseuf-campus-staging-seed.md`](../../guides/mseuf-campus-staging-seed.md).

## Execution identity

The final focused run used commit `a5e890d69630130b95c7b4ebd78d4fdfe9914854`
on `deploy/azure-staging`, starting at `2026-10-17T18:47:11Z`. It ran on
Windows with .NET 10 and local native SQL Server 2019. The
`UNIPM_SQLSERVER2019_TEST_CONNECTION` setting was process-scoped and restored
afterward; its value is not recorded here.

## Command

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~StagingDemoInitializationTests|FullyQualifiedName~DevelopmentUserSeederTests|FullyQualifiedName~DevelopmentDemoSeederTests|FullyQualifiedName~AuthEndpointsTests' --logger 'trx;LogFileName=campus-seed.trx' --results-directory artifacts/campus-seed-refinement/a5e890d -m:1 /nodeReuse:false
```

## Results

**37 passed, 0 failed, 0 skipped** in 23 seconds. The filtered run covered 25
authentication tests, 3 development-user seeder tests, 6 development-demo
seeder tests and 3 staging-initialization tests. The staging tests used
disposable native SQL Server 2019 databases. No authentication, authorization,
migration or staging-initializer code changed in the campus seed refinement.

The seed assertions confirmed 25 assets, 105 schedules, 77 inspections, 42
forms and 29 acknowledgements. The ordered inventory of all 278 record IDs
matched fingerprint
`BD47FD3BD2A79ADEB5C78B5B8E6082D3711727C42AEDEE0D67C0B2D5B3EB2722`. The
assertions checked unique asset codes and QR payloads, campus labels and form
relationships, and the existing CPMP month sets: fire extinguishers and water
drinking stations in February, May, August and November; fire alarms and
emergency lights in June and December. Labels containing `demo`, `fictional`,
`sample`, or floor-plan wording were rejected by the seed assertions.

The two native SQL staging tests each emitted 25 asset rows. The rows matched
exactly after sorting by asset code and joining each row's asset code,
category, building, department and location with `|`, followed by a newline.
Their normalized SHA-256 is
`6184D849D85C24482FF6957E53601A7CA4621030B7F163B89133413CFE834C2A`. The
captured rows are retained in ignored
`artifacts/campus-seed-refinement/a5e890d/assets.json` and summarized in
`artifacts/campus-seed-refinement/a5e890d/sample.md`. The QR-generator and
persisted-payload consistency test also passed. Physical mobile scanning was
not tested.

After the tests, this read-only inventory query against local `.` / `master`
with integrated authentication returned no `UniPM_StagingSeedTest_*` database
names (exit 0):

```powershell
sqlcmd -S . -E -d master -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'UniPM[_]StagingSeedTest[_]%' ORDER BY name;" -h -1 -W -b
```

`SQLCMDINI` was unset for the call and restored afterward. The query made no
database changes. Its first attempt from the default sandbox returned a
Named Pipes error 5 (`Could not open a connection to SQL Server [5]`) and a
login timeout because that shell could not reach the local SQL Server. The
sandbox failure was an environment access error, not a test failure.

## Failure and correction history

The initial focused run at commit
`8deb8e623c979848b9a59e6814afa8003ead4d30` passed 36 of 37 tests. The
`DevelopmentDemoSeederTests.Seed_creates_all_demo_scenarios_and_is_idempotent`
test counted three GSD schedules where it expected one: the baseline
Administration Building emergency light and two historical Gymnasium assets.
The correction assigned baseline emergency light `EL-REG-001` to the
Registrar's Office grouping, separate from historical Gymnasium GSD schedules.
The single permitted rerun at the exact final commit passed all 37 tests.

## Skipped verification and limitations

At recording, no live Azure operation, package build or ZIP smoke had occurred.
The SQL tests applied migrations only to their owned disposable databases. The
complete backend and web suites were not run.

The tested run establishes local test behavior only. It does not verify live
staging initialization, authentication configuration, physical mobile scans,
or acceptance of the final campus labels by MSEUF. At recording, the final-head
package build and local ZIP smoke had not run. Their results will be recorded
with the ignored package artifacts and task report after the documentation
commit.

Raw output is retained under ignored `artifacts/campus-seed-refinement/`:
`backend-tests.log`, `backend-tests-rerun.log`, and both TRX files. The first
run's TRX is under `8deb8e6/`; the final run's TRX is under `a5e890d/`.
