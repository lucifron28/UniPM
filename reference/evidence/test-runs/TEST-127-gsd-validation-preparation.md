---
id: TEST-127
type: test-run
title: GSD validation dataset and LAN fallback preparation
status: executed
recordedAtUtc: 2026-10-20T22:25:46Z
testedCommit: 6ea6dc1134a093ab1b927b4eab70a4b4579b258e
sourceBranch: main
evidenceLevel: locally-executed
---

# GSD validation dataset and LAN fallback preparation

Verification uses the base commit above plus this task's working patch.
Existing unrelated Web and NLA working-tree changes were not committed as part
of this preparation. No existing SQL database, Azure resource, firewall, or
running application was changed by the preparation commands.

The tested seed patch is committed as
`eb7628e07a92ff36f67532b2c85dc7290a344aee`; the tested mobile patch is committed as
`eda77105dd3382df20ace27f010d4189a29682bb`. Checks ran before those commits,
with the unrelated working-tree changes present. They are not a clean-tree or
exact-head CI claim.

## Dataset and source review

The separate validation fixture contains 200 assets across four categories,
500 schedules, 248 historical inspections, 20 forms, and 10 acknowledgements.
Four complete 13-asset batches remain unassigned and uninspected. The manifest
records the deterministic distribution, expected counts, and live exercise.
Old demo fixtures and their existing test expectations remain unchanged.

The reviewer inspected the first page of each supplied scanned form:
FE_3rd_2026.pdf, emergency-lights-2nd-sem-25_26.pdf,
water-drinking-station-3rd-2026.pdf, and fire-alarm-2nd-sem-25_26.pdf.
Only their general equipment/location/condition/date/findings pattern informed
the implementation. This was not an inventory import or a review of every page.
No real inspection entries, signatories, signatures, or equipment assignments
were copied. The water-station sheet's row-level acknowledgements do not change
UniPM's confirmed whole-form lifecycle.

Code review covered stable UUIDs and QR values, valid CPMP cycles, complete
live-batch scope, fictional category-specific findings, synthetic signature
checksums, and WMS referral timestamps after acknowledgement. New guarded seed
commands require explicit target confirmation, exact current migrations, empty
mapped tables, and the expected role-account state. They do not migrate or
reset. Azure connections additionally require certificate-validated encryption.

## Focused execution

The dedicated backend fixture passed with exit 0: 2 passed, 0 failed, 0 skipped.
The initial command compiled the Release API and test assembly after two
focused compile corrections. VSTest then failed its sandbox startup handshake;
an extended sandbox attempt was cancelled without test results. The same
focused filter passed outside the sandbox using those built assemblies:

```powershell
$env:VSTEST_CONNECTION_TIMEOUT = '240' # Temporary process setting, restored after the run.
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~GsdValidation -m:1 -nodeReuse:false
```

The build-producing invocation used the same arguments without `--no-build`.
All attempts remain under `artifacts/gsd-validation-focused-runs/`; attempt 05
records the passing result. The InMemory checks exercised role-account
bootstrap and configured passwords, distribution/counts, unique UUID/QR/cycle
values, signature checksums, referral/audit timing, four untouched live batches,
repeat rejection, and partial-target rejection. InMemory does not enforce SQL
constraints or relational transactions. No real SQL seed execution is claimed.

Windows PowerShell 5.1.26100.9711 parsed all five LAN files and passed the pure
RFC1918 and local-SQL target checks. The legacy `UniPMDb`, wrong database,
mismatched confirmation, and Azure targets were rejected. The command was:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/lan/validate-powershell5.ps1
```

This process-only execution-policy flag did not change Windows policy. The
ignored check script and its reviewed output are retained under `artifacts/lan/`.
No network selection, SQL connection, process start, or firewall operation ran.

One mobile storage test passed with exit 0:

```powershell
cd mobile
flutter test test/offline_inspection_repository_test.dart --plain-name "encrypted snapshots are isolated by backend and user"
```

The secure-storage plugin was mocked. The test verifies endpoint and user
isolation, endpoint normalization, preservation of the other backend's queue,
and no automatic import or deletion of old unscoped keys. It makes no backend
or provider calls. Output: `artifacts/gsd-validation/mobile-backend-scope.log`.
Formatting of the three changed mobile files completed; the Dart process then
reported a sandbox telemetry-file permission error. The subsequent focused
Flutter check ran outside the sandbox and passed.

## Operator handoff and limits

See [dataset manifest](../../guides/gsd-validation-dataset.md) and
[operator runbook](../../guides/gsd-validation-preparation.md).
The LAN helpers were not used to start services, connect to SQL, change firewall
rules, or stop processes during preparation. Readiness across physical devices
and Windows Firewall remains an operator check.

Native SQL Server constraints, transactions, migration history, Azure engine
checks, and the actual browser/mobile acceptance workflow remain unexecuted.
No full backend, Web, Flutter, or Playwright suite ran. No AI provider call,
database reset, migration, deployment, or Azure operation ran.

Before initialization, the operator must approve the fresh local/Azure target,
backup and rollback plan, migration window, credentials, seed execution, and any
later Azure cutover. Resolve old mobile queues in the original app/backend before
upgrading; unscoped old queues cannot be assigned an origin safely. New queues
are isolated by backend and user. LAN and Azure records never synchronize.
