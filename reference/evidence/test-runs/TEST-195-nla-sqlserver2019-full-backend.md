---
id: TEST-195
type: test-run
title: NLA final-hardening SQL Server 2019 backend verification
status: executed
recordedAtUtc: 2026-11-06T13:21:37Z
testedCommit: 2f347d99dd805159f5e72e0aa39e76b5974ab878
sourceBranch: feature/nla-final-hardening
evidenceLevel: locally-executed
---

# NLA final-hardening SQL Server 2019 backend verification

## Objective and identity

Run the full backend test project with the optional SQL Server tests enabled on
the local SQL Server 2019 instance. This verifies the native SQL translation,
compatibility, migration, and persistence paths used by the suite.

- Tested source commit: `2f347d99dd805159f5e72e0aa39e76b5974ab878`.
- Feature branch: `feature/nla-final-hardening`.
- Pull request: [#116](https://github.com/lucifron28/UniPM/pull/116), open and unmerged.
- Recorded at: 2026-11-06 13:21:37 UTC.
- Environment: Windows, .NET SDK `10.0.300`, local SQL Server 2019 Developer
  `15.0.2000.5`, Full-Text Search installed, Windows integrated authentication.
- The tests create disposable databases and set compatibility level 150 where
  required. The connection value is intentionally omitted.

## Command and result

The process-scoped environment set both `UNIPM_SQLSERVER_TEST_CONNECTION` and
`UNIPM_SQLSERVER2019_TEST_CONNECTION` to the local SQL Server instance. No
provider credentials were set.

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --logger "trx;LogFileName=backend-sqlserver2019-full.trx" --results-directory artifacts\nla-final-hardening\sql2019-full-backend-2f347d9
```

| Result | Count |
|---|---:|
| Passed | 800 |
| Failed | 0 |
| Skipped | 1 |
| Total | 801 |

The command exited with code `0` after 1 minute 57 seconds.

The skipped test is `Configured_provider_returns_stable_vectors`, which calls
an external embedding provider. No provider call was made. No paid provider
request was made.

The raw console log and TRX report are retained in the ignored directory
`artifacts/nla-final-hardening/sql2019-full-backend-2f347d9/`.

## Corrections made during verification

An earlier SQL-enabled run found that the staging seed guard expected an old
migration count and that two migration-test fixtures wrote current EF entities
after downgrading their databases. The staging guard and domain-contract test
fixture were corrected. A later full run on `fb70b6a` found the same fixture
ordering problem in the photo-evidence migration test. The fixture now seeds
on the latest schema, then downgrades to the requested migration. The full
suite on the tested commit above passed after that correction.

## Limits

This is local execution against a synthetic disposable database. It does not
establish GSD acceptance, held-out NLA quality, production-provider behavior,
or deployment verification. The upcoming GSD 20-query evaluation set and its
answer sheet were not accessed.
