---
id: TEST-091
type: test-run
title: Staging SQL safety metadata null handling
status: executed
recordedAtUtc: 2026-10-17T18:13:37Z
testedCommit: 19627b2e9ea112f842269baeee5aec3bc6bed3e4
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Staging SQL null handling

An independent audit of `b8412f6bab047feaebdc3c463ce24a719b9d9329`
identified a defensive SQL guard defect. `IF value <> expected` does not
reject a NULL value because the comparison evaluates to unknown.

Commit `19627b2e9ea112f842269baeee5aec3bc6bed3e4` applies `ISNULL` with
non-matching defaults to database name, logical server, engine edition,
compatibility and Full-Text availability in the initializer. The same
target/compatibility correction applies to the read-only demo postflight.
Full-Text and postflight engine metadata also use `TRY_CONVERT`.
No backend, domain, credentials, dataset or migration changed.

## Executed focused check

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-staging-sql-null-guards.ps1
```

Result: **22 passed**, exit 0, Windows PowerShell. The script extracts the
actual checked-in SQL predicates and evaluates them as read-only scalar
expressions on local SQL Server `master` using integrated authentication.
Fifteen cases check NULL, incorrect and expected preflight values. Four check
the valid postflight target and each independently missing target component.
Three check missing, incorrect and expected postflight compatibility.
No database or business record is created or changed. SQLCMDINI is cleared
temporarily and restored. No Azure credential or connection is used.

Raw output: `artifacts/staging-sql-null-guards.log`. Reviewed output contains
only the pass count. `git diff --check` passed. The previous 37 backend tests
and ten PowerShell credential checks remain historical results in TEST-090;
they were not rerun for this SQL-only correction.

## Package and live-operation boundary

At this record's commit, a fresh local package is the next preparation step.
Run the existing package command from the clean final deployment head. The
package's ignored `artifacts/azure-staging/<full-source-sha>/provenance.json`
and ZIP checksum identify its actual source and generated output; the final
task report records the command outcome. Do not treat an older local package
or the earlier GitHub package as containing this correction.

Live Azure initialization, the complete initializer against Azure, deployment,
live login, complete backend/web suites and new-head CI are **NOT VERIFIED**
by this record. No Azure database access, seeding, migration, firewall change,
deployment or merge occurred. Do not rerun the 22 completed Azure migrations.
Live initialization still requires separate owner approval with writers stopped.
