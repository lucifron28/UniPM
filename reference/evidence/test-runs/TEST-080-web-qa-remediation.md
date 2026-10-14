---
id: TEST-080
type: test-run
title: Backend, web, SQL, and live QA remediation verification
status: executed
recordedAtUtc: 2026-10-14T02:54:35Z
testedCommit: "Web/API: b74ed318dc4249df954dbeaf3c2d428d3d05a07b; focused NLA patch: c642dc2ba8a1ea6642e77d28cf16bfb019735107 plus typed-client change later committed as 3bed0f3cb04d58d835b42186de00d80d552e1ee4; focused seeder tests: base c642dc2ba8a1ea6642e77d28cf16bfb019735107 before query correction later committed as 03f7cf77129712740f7183735eca118c4fb80ffc"
sourceBranch: feature/web-qa-remediation
evidenceLevel: locally-executed
---

# Backend, web, SQL, and live QA remediation verification

## Objective and scope

Verify NLA typed-client resolution and the expanded CPMP demo reset against
focused tests and native SQL Server 2019; run web unit and mock-browser suites;
and exercise the guarded live batch plus selected browser workflows against
an isolated API/database. Model use, embeddings, workers, and external provider
calls were disabled. No production deployment or production data was used.

Exact-head Backend and Web CI passed for code commit
b74ed318dc4249df954dbeaf3c2d428d3d05a07b. This separate documentation record
was prepared after those runs, which tested the code head before evidence
publication.

## Execution identity and environment

- Web, API, live browser runs, and Release build used
  `b74ed318dc4249df954dbeaf3c2d428d3d05a07b`.
- Focused NLA tests passed on base `c642dc2ba8a1ea6642e77d28cf16bfb019735107`
  with the typed-client change later committed as
  `3bed0f3cb04d58d835b42186de00d80d552e1ee4`. The path diff from that commit
  to b74 contains no NLA implementation or test changes; the focused filter
  was not rerun at b74.
- Focused seeder tests passed on the c642 base before the materialized-key
  query correction committed as
  `03f7cf77129712740f7183735eca118c4fb80ffc`. They were not rerun after that
  correction; the native-provider smoke exercised it.
- Native database verification used SQL Server 2019, compatibility level 150,
  and confirmed Full-Text Search. The isolated database was dropped and
  verified absent after QA. The owned API and photo store were also removed;
  raw logs and screenshots remain under ignored `artifacts/`.
- Mock Playwright's default viewport was 1280 by 720. Targeted tests used
  1440 by 900 and 1920 by 1080; responsive tests overrode the viewport.
- Live browser origin was `http://localhost:5178`; the owned API used ports
  5258 and 5262 and an isolated photo-store directory. Explicit process-only
  trusted-origin settings fixed a preflight mismatch. Credentials and
  connection details are omitted.

## Commands

Focused backend filters:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~NaturalLanguageAnalyticsInterpretationTests"
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~DevelopmentDemoSeederTests|FullyQualifiedName~SyntheticMaintenanceSeederTests"
```

Web unit suite and API Release build:

```powershell
npm run test:run
dotnet build server/server.csproj -c Release
```

The full mock Chromium Playwright suite used the retained config, output path,
and portable Node 22.23.3 binary. This is a reproducible equivalent; the exact
outer shell command used for the historical run was not retained:

```powershell
& '.\artifacts\tools\node22\node-v22.23.3-win-x64\node.exe' '.\web\node_modules\@playwright\test\cli.js' test --config '.\artifacts\web-qa-20261014\web\playwright-final.config.mjs' --reporter=list --output '.\artifacts\web-qa-20261014\web\playwright-final-results'
```

The guarded live batch case used the retained config and a process-only demo
password:

```powershell
& '.\artifacts\tools\node22\node-v22.23.3-win-x64\node.exe' '.\web\node_modules\@playwright\test\cli.js' test --config '.\artifacts\web-qa-20261014\web\live\schedule-batch-spec-20261014.config.mjs' --grep 'GSD, Supervisor, and Inspector complete an assigned PM batch through WMS referral'
```

Native SQL smoke applied migrations, seeded synthetic data and development
users, seeded the expanded fixture, reset it twice, and reseeded twice. The
corresponding commands and count/ID snapshots are retained in the SQL logs.
Live UI runs used the ignored harness/config and per-attempt manifests under
`artifacts/web-qa-20261014/web/live/`; their outer launcher command was not
retained. The one-shot photo helper and read-only diagnosis scripts are also
listed under Generated artifacts.

## Results

| Check | Result | Tested identity |
| --- | --- | --- |
| NLA typed-client and interpreter filter | 29 passed, 0 failed; includes real DI resolution and model-disabled rule-based path | c642 base plus typed-client patch; NLA paths unchanged through b74 |
| Demo and synthetic seeder filters | 16 passed, 0 failed; expanded counts 25 assets, 105 schedules, 77 inspections, 42 forms, 29 acknowledgements | c642 base before 03f7 query correction |
| Native SQL reset/reseed | Commands succeeded; repeated snapshots matched. Post-reseed: 46 assets, 139 schedules, 107 inspections, 42 forms, 29 acknowledgements; unrelated sentinel and synthetic baseline preserved | SQL Server 2019, source 03f7 |
| Web unit suite | 219 passed, 0 failed; earlier run 218 passed, 1 failed | b74 |
| Mock Chromium Playwright | 50 passed, 1 guarded live skip, 0 failed | b74 |
| API Release build | Successful retry: 0 warnings, 0 errors; ProductVersion `1.0.0+b74ed318dc4249df954dbeaf3c2d428d3d05a07b` | b74 |
| Guarded live schedule-batch test | 1 passed, 0 failed; synthetic assignment, two-row form submission/acknowledgement and WMS referral | b74 API, owned disposable DB |
| Live browser checks | Requested checks covered cumulatively across follow-up attempts 2, 5, and 6; see accepted coverage below and attempt history for failures/counting caveats. | b74 API and owned Vite |
| Exact-head Backend CI | 463 passed, 45 skipped (508 total) | [run 38488819555](https://github.com/lucifron28/UniPM/actions/runs/38488819555), b74 |
| Exact-head Web CI | 219 unit tests and corresponding 219-test coverage run | [run 38488819578](https://github.com/lucifron28/UniPM/actions/runs/38488819578), b74 |

The native SQL smoke exercised the explicit ID and materialized membership
predicates against SQL Server 2019. The repeated reseed snapshots matched;
their SHA-256 is
`89AB7A8D8D5E824F8B82F4688B14C8EA7BB55EA3C7B852AE16BB91523EC20E4F`.

### Accepted live coverage (cumulative)

| Run | Checks actually exercised |
| --- | --- |
| Attempt 2: 10 passes | Role matrix; asset validation and creation; schedule validation and creation; sidebar geometry and scrolling; pagination; return to primary asset and schedule registries. |
| Attempt 5: quarter and photo pass; WMS assertions completed | Quarter inclusion/exclusion; decoded photo thumbnail and modal; acknowledgement, WMS history/audit assertions, and audit comparison. WMS assertions were not counted as a pass because the harness status field was overwritten by `Acknowledged`; HTTP 200 is inferred from reaching the assertions. |
| Attempt 6: 1 NLA pass | Five eligible records with two late; all-five-source scope; year-only clarification; unsupported question with no query; print action. This checked the print action only, not PDF layout. |

These checks span separate attempts and do not represent one atomic successful
run. Physical-device/mobile behavior, the inherited mobile application,
production, staging, PDF print layout, and real-provider language were not
verified.

## Live UI attempt history

The live UI work used several narrowly scoped runs rather than one clean
monolithic pass. The 14 requested checks are covered cumulatively by attempts
2, 5, and 6; their logs, manifests, and screenshots remain available.

- An initial harness parse error (`await` in a non-async callback) stopped
  before browser launch or mutation. The first corrected retry validated the
  dashboard but stopped on an enrollment-deferrals request whose transport
  error was not retained. API logs contained three GET 499 and two GET 200
  completions without timestamps, so they cannot be associated with that run;
  no authentication or data fault was established.
- Follow-up attempt 1 passed dashboard/count checks, then failed an Assets
  geometry selector because controls are wrapped by labels. Attempt 2 passed
  ten checks and timed out before Quarter Apply because the harness used month
  value `2026-06` while the UI option is `06`. Root reviewed the 32 screenshots;
  no visible layout defect was found.
- Attempt 3's outer result reported a pass, but the Quarter check was
  `NOTVERIFIED` and WMS history timed out. The manifest did not retain WMS
  response status. Attempt 4 exited 1 with zero passed checks; Q2 inclusion
  appeared in its screenshot, but the harness notice matcher was wrong and no
  Q3 request ran. Source review found the UI notice is “The exact PM month was
  cleared because the quarter changed.”
- Attempt 5 captured 18 screenshots and reported 2 passed, 1 failed, 0 not
  verified. Quarter inclusion/exclusion and the photo preview passed; both the
  96 px thumbnail and modal image decoded. WMS form/history assertions and log
  comparison completed, but the check-status collision prevented it from
  counting as a pass. HTTP 200 is inferred from reaching those assertions; no
  raw response body was retained. The captured WMS audit count was 1 and final
  count was also 1. The history button label changed from `View` to `Hide`,
  explaining a stale-label visibility check. NLA/report assertions and
  screenshots completed, then a global diagnostic failed on the deferrals GET
  with `net::ERR_ABORTED`; status and cause were not captured. No provider was
  called.
- Attempt 6 isolated the NLA diagnostic: 1 passed, 0 failed, 0 not verified,
  exit 0, with 14 screenshots. The fresh deferrals request returned 200 with
  zero pending and reviewed items; the dashboard settled and
  `verifiedAbortCount` was 0. No provider was called.
- Root reviewed all 32 attempt-2, 18 attempt-5, and 14 attempt-6 screenshots,
  plus the full WMS panel. No visible layout defect was found. At 1440 px, wide
  tables use intentional horizontal overflow and long placeholders truncate.

The final separately guarded batch test passed 1/1 against the b74 API and did
not upload a photo. A separate one-shot photo API helper exited 1 because it
incorrectly expected `status: Acknowledged` on the acknowledgement response;
that DTO contains `formId` and `acknowledgedAt`. Earlier upload/image checks are
inferred from reaching the assertion and were not logged individually. A
read-only GSD lookup later confirmed the acknowledged form, registry
`hasPhotoEvidence: true`, and protected photo GET 200 with JPEG content and
`private, no-store`. The exact schedule completion timestamp before upload was
not retained. The subsequent live browser thumbnail/modal decode passed.

The live service used process-only CORS settings to match localhost:5178. The
API's earlier 5173 origin was not allowed by the browser; after correction,
preflight returned 204 with the exact allowed origin, and both health routes
returned 200 on ports 5258 and 5262. After review, the owned API, database, and
photo store were removed and verified absent. The Vite process was stopped and
its port verified clear. Logs and screenshots were preserved.

## Generated artifacts

- Web unit and mock browser logs/reports: `artifacts/web-qa-20261014/web/`;
  includes `unit-final.log/.exit`, `playwright-final.log/.exit`, results, and
  contact sheets.
- Backend focused logs/TRX: `artifacts/web-qa-20261014/backend/nla/` and
  `artifacts/web-qa-20261014/backend/seeding/`; prior attempts are retained.
- Native SQL counts, snapshots, build and cleanup logs:
  `artifacts/web-qa-20261014/backend/sql/`, including
  `cleanup-final-20261014.log`.
- Guarded live batch: `artifacts/web-qa-20261014/web/live/` contains
  `schedule-batch-once.log/.exit`, results, and safe fixture manifest.
- Photo workflow/diagnosis: same live directory contains
  `photo-fixture.once.mjs`, its log/exit, `diagnose-photo-fixture.once.mjs`,
  diagnosis log/exit/JSON, and safe `photo-fixture.json` UI handoff manifest.
- Live UI: same live directory contains attempt logs, exit files, manifests,
  and screenshot directories. Final NLA-only evidence is
  `live-ui-run-attempt-6.log/.exit`, `live-ui-manifest-attempt-6.json`, and
  `screenshots-attempt-6/`. Attempt 5 artifacts are named analogously.
- Vite cleanup log: `artifacts/web-qa-20261014/web/live/vite-cleanup-attempt6.log`.

## Limitations

The full local backend suite was not run; the exact-head Backend CI run covers
the full suite. The focused seeder tests predate the 03f7 provider-compatibility
query correction, which was instead exercised by the native SQL smoke. NLA
focused tests predate b74, with no NLA paths changed afterward. Live UI check
coverage is cumulative, not one atomic successful run; WMS completion is
preserved as functional assertions with a harness status-count collision, not
as a passing check. No production deployment, production data, staging
verification, or real-provider behavior was tested. This record does not
include credentials or sensitive configuration.
