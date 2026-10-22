---
id: TEST-141
type: test-run
title: Controlled backend numerical and source-selection verification
status: executed
recordedAtUtc: 2026-10-23
testedCommit: 89cdc38820e828bf66bab1fa5822236a73f1548c
sourceBranch: feature/nla-guided-interpretation
evidenceLevel: locally-executed
---

# Controlled backend numerical and source-selection verification

## Source, scope and method

After the TEST-140 environment failure, the user approved one isolated retry.
The temporary capture tool built against existing reference-project binaries
using `BuildProjectReferences=false`, leaving the running local API untouched.
It invoked `PmAnalyticsV3RegressionTests.ExecuteFixtureAsync` by reflection.
That existing helper seeds disposable EF InMemory contexts, resolves each
typed plan and calls the real `PmAnalyticsV3QueryService.ExecuteAsync`.
It does not contact a provider or external database and does not run xUnit.

Ten requests were prepared across seven existing fixtures, with fixed clocks
and a total of 21 input rows. The zero-eligible fixture has no rows. No existing
test or backend application source was edited. Existing Web/mobile working
changes were not included in this backend-only execution claim.

Before the attempt, the commit above and hashes of the query-service source,
existing fixture helper, source fixtures, request definitions and capture
program were retained in ignored
`artifacts/manuscript-numerical-2026-10-23/baseline.json`. The final capture
identity includes binary and comparison-script hashes in `completed-identity.json`.

## Commands actually executed

From the repository root:

```powershell
dotnet run --project artifacts/manuscript-numerical-2026-10-23/runner/NumericalCapture.csproj -p:BuildProjectReferences=false -- reference/evaluation/pm-analytics-v3/synthetic-calculation-fixtures.json artifacts/manuscript-numerical-2026-10-23/requests.json artifacts/manuscript-numerical-2026-10-23/backend-responses.json
```

Exit 0. All ten actual responses were captured. Output and errors were piped
through Tee-Object to `backend-capture-isolated.log` in the same ignored folder.

The separate Python reference calculation was invoked using the bundled
`C:\Users\Ron\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`:

```powershell
python artifacts/manuscript-numerical-2026-10-23/compare.py
```

The first comparison exited 1 with 74/75 matches: the reference checker compared
the expected Decimal 100 formatted as 100.00 with the observed integer 100 as
different types. Both values were numerically 100. One focused checker-only
correction normalized numeric representations. The same command was rerun
once against the unchanged captured backend responses and exited 0 with
75/75 matches. Both comparison logs are retained. No backend recapture or
calculation change was needed.

## Reference calculation

Python recomputed expected values directly from fixture asset rows, without
reading their existing `expected` blocks. It counted rows and completions,
compared completion times with month-end deadlines in UTC+8, calculated
progress/compliance, applied the 60% group threshold, selected quarter/source
records and checked page membership. Counts, numerators, denominators, flags
and source lists were compared exactly. Percentages were compared at two
decimal places using decimal arithmetic and positive half-up rounding.

The reference implementation was prepared by the same development agent;
it is not external independent human review. No claim of independent adviser
or GSD numerical approval is made.

## Results

- Controlled report scenarios executed: 10.
- Declared comparison fields matched after the checker correction: 75/75.
- Scenario-level result: all ten match their declared expectations.
- Native SQL Server, live Azure, browser, provider and physical-device checks: not run.

See the [manuscript-ready table](../../evaluation/manuscript-2026-10-23/manuscript-nla-numerical-verification.md)
for scopes, formulas and actual results. The reviewed supporting files in that
folder contain source input rows, every field comparison, and selected
synthetic backend observations with random UUIDs omitted. Raw full responses,
requests, runner source, reference script and logs remain in ignored artifacts.

## Limits

This validates the query-service path on fixed synthetic InMemory data, not
SQL translation, native SQL Server 2019, deployment data, NLA language accuracy
or the complete signed-preview/browser workflow. Source lists are checked for
the declared scenarios, not all possible scopes. The Q4 selection fixture has
future stored completion data relative to its fixed reporting instant; this
tests stored record selection, not future-date entry validation. Null results
for open cycles and empty populations are expected successful outcomes.

The manuscript's earlier numerical values must not be treated as these
executions. Use the new NUM-01 through NUM-10 identities, dataset and reporting
dates. These scenarios also do not automatically close BB-01 through BB-32.
