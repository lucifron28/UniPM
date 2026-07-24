---
id: TEST-021
type: test-run
title: SQL Server 2019 compatibility spike preparation
status: executed
recordedAtUtc: 2026-07-24T10:37:56Z
testedCommit: 136cd502ef86f7ba28f013792efc31b461cfa7ac
sourceBranch: spike/sqlserver-2019-compatibility
evidenceLevel: locally-executed
---

# SQL Server 2019 Compatibility Spike Preparation

## Objective

Prepare an isolated SQL Server 2019 plus Full-Text Search environment and
verify the shared EF compatibility boundary and SQL 2019-safe migration change.

## Execution Identity

The implementation commits tested were `0459957`, `b487e78`, and
`136cd502ef86f7ba28f013792efc31b461cfa7ac`. This record identifies the final
implementation commit before evidence documentation.

## Environment

The default SQL Server 2025 Compose stack and its named volume were not
started, removed, or modified. The attempted 2019 stack uses different
container names, host port `14339`, volume, and network.

No SQL connection string, password, provider credential, endpoint, prompt,
token, or vector is recorded here.

## Commands

```powershell
docker compose -f docker-compose.sqlserver2019.yml config --quiet
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build
docker compose -f docker-compose.sqlserver2019.yml build --progress plain
docker pull mcr.microsoft.com/mssql/server:2019-latest
```

## Results

Compose configuration validation passed. Restore and the Release build passed.
The ordinary solution suite passed with 285 tests and 25 skipped SQL/provider
tests.

The official SQL Server 2019 image pull and the Compose image build both
exceeded the local ten-minute command limit without producing a completed image
or starting a 2019 container. The stalled local Docker client processes were
stopped. No 2019 container, volume, or database was created.

## Test Counts

| Scope | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Ordinary Release solution suite | 285 | 0 | 25 |
| SQL Server 2019-specific suite | 0 | 0 | Not executed |

## SQL Server Verification

Not executed. There was no reachable SQL Server 2019 process, so migrations,
Full-Text Search, seed commands, projection rebuild, authentication concurrency,
lexical retrieval, embedding persistence, app-layer cosine, fusion, and the
benchmark could not be verified against major version 15.

The source review found one proven SQL Server 2022-only migration dependency:
the three-argument `STRING_SPLIT` ordinal form. The implementation replaces it
with an order-preserving XML-node technique and adds SQL integration coverage
for mixed CRLF/CR canonicalization. Source inspection is not a substitute for
the blocked SQL Server 2019 execution.

## AI-Provider Verification

Not executed. No real provider call was attempted. Deterministic semantic and
fused SQL verification remains pending the SQL Server 2019 environment.

## Generated Artifacts

No reviewed SQL Server 2019 artifacts were produced because the image did not
complete. Local Docker build state and command output remain uncommitted.

## Failures And Corrections

The Compose health-check YAML was corrected before container startup and
validated successfully with `docker compose ... config --quiet`.

## Skipped Verification

- SQL Server major-version and compatibility-level probes;
- Full-Text package, catalog, index, and `CONTAINSTABLE` probes;
- fresh migration, seed, Development-user, projection, and embedding commands;
- SQL Server migration, retrieval, benchmark, and refresh-session tests.

## Limitations

Outcome: **FAIL (environment unavailable)**. SQL Server 2019 support is not
claimed. The project must retain SQL Server 2025 as its default until the
isolated 2019 image builds and all required SQL-specific verification passes.
