---
id: TEST-044
type: test-run
title: CPMP scheduling and compliance verification
status: executed
recordedAtUtc: 2026-09-30T18:36:00Z
testedCommit: 73ed2c18840827779ff85510ed8904c90038b5a8
sourceBranch: fix/cpmp-month-end-deadlines-compliance
evidenceLevel: ci-executed
---

# CPMP scheduling and compliance verification

## Execution identity

The final CI runs tested clean commit
`73ed2c18840827779ff85510ed8904c90038b5a8`. Local execution identities below
are narrower and are not presented as execution of later commits.

| Execution | Commit | Result |
|---|---|---|
| Targeted backend API, reporting and demo tests | `d4b18151bc2228b8e081e7b2eed73964277daf2a` | 110 passed, zero failed |
| Native SQL HTTP scenarios and demo reports | `d4b18151bc2228b8e081e7b2eed73964277daf2a` | Passed |
| Local backend solution build | `d4b18151bc2228b8e081e7b2eed73964277daf2a` | Passed |
| Focused web tests | `ae424403be4dcf7885bd73929184cf6d228a843b` | 60 passed in seven files |
| Focused browser scenarios | `ae424403be4dcf7885bd73929184cf6d228a843b` | Eight passed |
| Backend CI | `73ed2c18840827779ff85510ed8904c90038b5a8` | 384 passed, 41 skipped, zero failed; solution build and tooling checks passed |
| Web CI | `73ed2c18840827779ff85510ed8904c90038b5a8` | 166 unit tests passed; 39 browser tests passed, one skipped |

[Backend CI run](https://github.com/lucifron28/UniPM/actions/runs/36759266147)
and [Web CI run](https://github.com/lucifron28/UniPM/actions/runs/36759266112)
both concluded success.

## Commands and scope

Backend local execution used `dotnet build UniPM.slnx --configuration Release
--no-restore` and a `dotnet test tests/UniPM.Api.Tests --configuration Release`
filter covering schedule queries and assignment, OpenAPI, reference data,
authorization, draft forms, inspection queries/location verification, PM-period
reporting, and Development demo seeding. The filter and TRX are retained in
ignored `artifacts/cpmp/`.

Web local execution used Node 22.23.3, `npm run format:check`, `npm run lint`,
`npm run typecheck`, `npm run api:contract:check`, `npm run api:contract:test`,
`npm run api:check`, focused `npm run test:run`, and Vite production build.
Playwright used an ignored configuration on a separate port and the schedule,
PM dashboard, and acknowledgement-review scenarios. CI executed the committed
workflow's full unit, coverage, production build and browser checks. All seven
negative OpenAPI mutations were rejected. Generated-client drift was absent.
`git diff --check` passed before the final CPMP push.

## Native SQL Server 2019

The native verification used a newly created, disposable database, Windows
integrated authentication and a separate API process. It ran existing
migrations, Development user seeding and demo seeding, then verified readiness
and authenticated login. It exercised creation for all four categories,
rejected one disallowed month per category, and checked canonical due-date
precision for leap February, June, December and November. Legacy arbitrary-day
input was canonicalized to month-end. June, August and November demo dashboard
counts matched their unfinished, late, on-time and not-yet-measurable scenarios.
The owned temporary API was stopped and the disposable database removed.
No existing demo or production records were rewritten. This was an HTTP/SQL
scenario check, not execution of the repository's complete native SQL suite.
The 41 environment-dependent CI skips remain skips.

## Failures and corrections

Initial main CI failures were limited to two unformatted schedule contract files
and a date/quarter mismatch in an authorization fixture. Those were repaired
before domain work. The existing acknowledgement browser URL assertion was
aligned with its original query context.

Local fixture corrections preserved distinct valid CPMP form identities,
category-validation coverage, normalized department expectations, and the
seeder's acknowledgement date. Web corrections supplied a required year,
awaited linked data, and verified asset-row wording in the full dashboard rather
than its summary-only component. A TypeScript optional-year mismatch was fixed
in the validated DTO conversion.

The first native verifier incorrectly parsed a timestamp after PowerShell had
converted JSON dates to native values and discarded fractional seconds during
string conversion. Retaining JSON timestamp strings resolved the verifier; the
SQL month-end value itself was correct.

The first web CI run exposed pagination scroll changes from variable result
heights. The existing schedule test reproduced the defect with enlarged text.
Bounding the desktop result viewport to its intended height and allowing inner
scrolling fixed it. The final complete browser run passed.

## Artifacts and limits

Raw logs, TRX files, browser output and the sanitized native verification summary
remain ignored under `artifacts/cpmp/`. No password, token, connection string,
provider payload or institutional record is included in this evidence.
All data are synthetic. No AI provider was enabled. This record does not claim
GSD validation, IIS deployment, physical-device testing or iOS verification.
Mobile alignment is covered by its subsequent execution record.
