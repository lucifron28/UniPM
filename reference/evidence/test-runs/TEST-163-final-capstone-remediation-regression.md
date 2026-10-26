---
id: TEST-163
type: test-run
title: Final available capstone remediation regression
status: executed
recordedAtUtc: 2026-10-25
testedCommit: d6c2e9d1f576754e9635f948bd814f98605d8c5e
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Final available remediation regression

The final test-runner revision is `d6c2e9d1f576754e9635f948bd814f98605d8c5e`.
The backend, application clients and browser fixtures were tested at
`ac5787ce79abd8ecf5d2646ab5a9bd514cdc36f6`. The sole committed difference
between these revisions is Vitest's pool setting in `web/vite.config.ts`.
Closing documentation commits do not change executable source. Local
execution does not establish a remote CI run, deployment or GSD acceptance
of this revision. Earlier phase records remain version-specific.

## Backend environment and commands

.NET SDK 10.0.300/runtime 10.0.8. Native SQL Server 2019 Developer 15.0.2000.5
is the local test host, with Full-Text Search installed. Both SQL test
connection variables point only to local Windows-authenticated master.
Each SQL fixture creates its own GUID database and migrates only that database.
No institutional or default application connection was used.

The test child disables AI, staging initialization and GSD validation seed
opt-ins, and clears real provider credentials. The four embedding smoke
opt-ins are absent. `UNIPM_DEV_SKIP_DEPARTMENT_HEAD_ACCOUNT=false` is explicit
valid test configuration. No application Boolean parser was changed.

Commands run sequentially from the repository root:

```powershell
dotnet build UniPM.slnx --verbosity minimal
dotnet test UniPM.slnx --settings artifacts/remediation/backend/phase11-xunit.runsettings --logger "console;verbosity=minimal" --logger "trx;LogFileName=phase11-final-backend-full.trx" --results-directory artifacts/remediation/backend --verbosity minimal
```

The ignored RunSettings contains one VSTest host and xUnit `MaxParallelThreads`
two with the conservative scheduler. The build passed with zero warnings and
zero errors at 09:37:52.612 to 09:38:50.579 UTC. The full suite then passed
672 cases, failed zero and skipped one, 673 total, exit zero, from
09:39:24.640 to 09:50:28.505 UTC. All times are October 25, 2026.
The sole skip was
`Retrieval.OpenAiCompatibleEmbeddingSmokeTests.Configured_provider_returns_stable_vectors`,
the opt-in external embedding smoke case. No SQL test skipped in this run.
The TRX is `phase11-final-backend-full.trx`; output is `phase11-final-full.log`.

Afterward, a metadata-only query of `master.sys.databases`, bounded to known
test prefixes and their final-run creation window, returned zero remaining
GUID fixture databases. It queried no application table. The staging script
was parsed but never executed against Azure or another deployment target.

## Failed attempts and corrections

| Revision / check | Actual result | Diagnosis / correction |
|---|---|---|
| `bf5df8c`, initial full solution test | 666 passed, six failed, one skipped, 673 total | Two stale staging migration expectations, three obsolete race requests and one invalid empty optional test-process variable. Initial solution build passed. |
| Temporary test-only probe on `bf5df8c` | Diagnostic failure observed HTTP 409 before an update | The initial-assignment endpoint rejects an already assigned batch. The transient assertion patch was restored; its hash was not retained, so this probe is not accepted as exact-commit regression evidence. |
| `fc06ad5`, seven targeted cases | Zero passed, seven failed | New route calls exposed a missing persisted GSD fixture role; staging setup combined incompatible SQL statements; the optional variable was still invalid. |
| `8b20bf9`, same seven cases | Five passed, two failed | Four race cases and the seeder passed. The remaining schema checks ran against populated demo tables, so the nonempty-data guard correctly rejected first. |
| `28a074f`, two staging cases | Two passed, zero failed/skipped | A separate empty GUID database reaches the intended schema/table checks. Exact SqlException assertions remain. |

Commit `32d2216` updates the fail-closed runtime schema guard and the unexecuted
staging script to the existing 23-migration/23-table schema, including schedule
reassignment audits. It adds no migration or auto-update. Applied migration
tests compare exact sequences. PowerShell Parser.ParseFile reported zero
syntax errors; the Azure script was never executed.

Commits `fc06ad5`, `8b20bf9` and `28a074f` correct fixtures. Reassignment calls
use the actual endpoint/DTO. The existing implementation and separate regression
explicitly preserve the inspector when the supervisor changes. The corrected
race checks retain the same SQL lock-wait gates and timeouts, assert inspection
completion under the retained assignment, and inspect the audit. No production
assignment or authorization behavior was loosened.

The earlier targeted backend logs/TRX retain selected case identities and
results but not the actual filter command lines. Those commands cannot be
reconstructed as retained evidence. The final full-suite command above was
retained and includes these cases. The temporary diagnostic probe also lacks
its patch hash, so it is diagnostic context only.

Commit `ac5787c` replaces stale browser array mocks with page/options envelopes
in two existing E2E files. Workflow assertions remain unchanged. These fixture
updates were found by source inspection before the final browser run.

## Web and mobile commands

Node 22.23.4 was selected in each web process without changing the system
installation. Flutter 3.44.5 and Dart 3.12.2 were used for mobile checks.
The following commands ran sequentially in their respective client directories.
Times are UTC on October 25, 2026.

| Command | Source | UTC interval | Result |
|---|---|---|---|
| `npm run format:check` | `ac5787c` | 09:38:16–09:38:57 | Exit 0, full Prettier check |
| `npm run lint` | `ac5787c` | 09:39:04–09:41:39 | Exit 0, full ESLint check |
| `npm run build` | `ac5787c` | 09:58:11–09:58:44 | Exit 0, TypeScript check and production build |
| `npm run e2e -- --workers=2` | `ac5787c` | 09:59:36–10:00:29 | Exit 0, 60 passed, one opt-in live case skipped |
| `flutter test --no-pub --concurrency=2` | `ac5787c` | 10:01:07–10:01:59 | Exit 0, 194 passed |
| `flutter build apk --debug --no-pub` | `ac5787c` | 10:02:09–10:04:15 | Exit 0, debug APK produced |

Browser execution used process-only `CI=true`, `UNIPM_E2E_PORT=5197`, an
unused local backend base on port 5199 and no development password. The
owned Vite server ran on the initially free port. Mocked browser cases did
not contact a real backend or authenticate a real account. The one guarded
live workflow skipped; this is not a new live full-stack acceptance run.

`npm run api:check` passed at `bf5df8cf925a1920c8ed15557c0efe799679eb62`.
Subsequent changes did not alter API contracts, the generated client or the
contract checker. Flutter analysis passed at
`8cea97af614bdcb9c2bfd9016261016694a5d5c4`; the mobile tree through the final
revision remains `2aba0b6bf17a58059c45c52971aa1d519dd1b41f`.
These are reused checks by source identity, not commands rerun at the final
revision. The ignored client manifests retain their original command output.

The debug APK has SHA-256
`1b05cf723904e71c8137bb6abc10be3271f44873a38ff68cef518de9a14de7ac`
and size 179,703,021 bytes. It is a debug build, not an approved signed
release. The build warned that `mobile_scanner` uses the Kotlin Gradle Plugin
and needs future compatibility work. No physical Android device was attached.
The release inventory found no local `key.properties`; production signing
and installation were not performed.

### Failed web attempts and isolation correction

At `ac5787c`, `npm run test:run -- --maxWorkers=2` with `vmThreads`
passed 269/270 cases, with one five-second inspection-pagination timeout.
That unchanged case passed in isolation in 563 ms. A full diagnostic
`npm run test:run -- --maxWorkers=1` then passed 223/270 and failed 47 across
four files, with router context and mock-isolation symptoms. All logs remain.

Commit `d6c2e9d` changes only `pool: 'vmThreads'` to `pool: 'forks'`.
The worker count stays two. Assertions, included tests and timeouts are
unchanged. This tests the runner-isolation hypothesis; logs alone do not
establish a single internal cause for every earlier failure.
[Vitest's pool documentation](https://vitest.dev/config/pool), consulted
October 25, documents VM/ESM isolation limitations and the process-based
`forks` alternative.

The isolated original-case command was
`npm run test:run -- --maxWorkers=1 src/features/inspections/inspection-workflow.test.tsx -t 'moves to the second server page without resetting the filters'`.
It passed one selected case and deselected six, 09:56:12–09:56:21 UTC.
The first full attempt ran 09:52:02–09:55:14; the one-worker diagnostic ran
09:56:40–09:57:50. The supported runtime for these attempts was Node 22.23.4.

Raw client outputs are `phase11-web-format-check-final.log`,
`phase11-web-eslint-final.log`, `phase11-web-production-build.log`,
`phase11-web-playwright.log`, `phase11-mobile-full-tests.log` and
`phase11-mobile-debug-apk.log` under `artifacts/remediation/clients/`.
The original unit failures are retained in `phase11-web-unit-full.log` and
`phase11-web-unit-full-workers1.log`, with the isolated pass in
`phase11-web-unit-timeout-focused-rerun.log`.

### Final supported-runtime result

At `d6c2e9d1f576754e9635f948bd814f98605d8c5e`, Node 22.23.4:

```powershell
npm run test:run -- --maxWorkers=2 src/components/layout/app-shell-access.test.tsx src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx src/features/reports/pm-analytics-v3.test.tsx src/features/assets/asset-qr-label.test.tsx src/features/inspections/inspection-workflow.test.tsx
npm run test:run -- --maxWorkers=2
```

The focused five files passed 79/79, zero failed/skipped, exit zero,
10:04:58.682–10:05:22.392 UTC. The full suite passed 270/270 in 31 files,
zero failed/skipped, exit zero, 10:05:29.620–10:06:30.667 UTC.
The only source change was the pool correction. These results support the
runner correction without changing application behavior or weakening tests.

An earlier focused attempt at the same commit passed 79/79 under Node 24.15.0
because the shell selected the wrong runtime. It is retained as diagnostic
output, not accepted as the required Node 22 check. The supported-runtime
focused and full runs above replace that attempt for acceptance.

Raw output is under `artifacts/remediation/backend/` in
`phase11-web-runner-targeted.log`, `phase11-web-runner-targeted-node22.log`
and `phase11-web-runner-full-node22.log`. No backend rerun was necessary
after a web test-pool-only change. The backend subtree remains
`4e3ce64f994f6a2fb02d57cbb7a5fd0e2e8a6d8a`.

## Evidence boundaries

Raw logs, TRX, browser output, APK output and manifests stay ignored under
`artifacts/remediation/` or tool build directories. Public records contain
reviewed aggregates and source identities. No provider credential, raw prompt,
institutional row, private connection string or release key is published.

The accepted survey data and historical live DeepSeek records are separate
evidence. No live DeepSeek call, real-device acceptance, signed release,
institutional restore drill or deployment was performed by this regression.
See the [remaining-risk register](../../planning/capstone-remaining-risks.md).
