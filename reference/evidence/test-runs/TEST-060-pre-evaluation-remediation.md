---
id: TEST-060
type: test-run
title: CPMP scheduling, assignment, registry, and WMS remediation verification
status: executed
recordedAtUtc: 2026-10-09T12:17:40Z
testedCommit: a8f05d75e26993606953e3da97bb6b437ccf9794
sourceBranch: feat/pre-evaluation-remediation
evidenceLevel: locally-executed
---

# CPMP scheduling, assignment, registry, and WMS remediation verification

## Objective

Record focused backend, web, browser, native SQL Server, and Flutter verification for the pre-evaluation remediation. Each result is tied to a retained commit or matching patch where metadata permits; the migration-preservation harness is explicitly marked where its source fingerprint was not retained. The final OpenAPI formatting commit `b181800762c44c1b3233c0302ef48aa249c6bb9d` followed the final workflow run; it changes only JSON whitespace and has the same parsed JSON SHA-256 before and after (`7f9040259f7922aeea58b8a58b44df89916ec892d990aad5d6bca66279b48c44`).

## Execution identity

- Repository base: `444cd7fcca8a48468638c6d46c817e82583107a1`.
- The front-matter `testedCommit` is the exact HEAD for the Flutter focused run: `a8f05d75e26993606953e3da97bb6b437ccf9794`.
- Backend suite, Release build, EF model check, and full Web unit suite: `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`.
- Web lint and typecheck: base `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f` plus the reviewed five-file filter-panel patch matching `132029b1265c5ae28a24b48b04907b94d1ab3947`.
- Web production build: exact HEAD `132029b1265c5ae28a24b48b04907b94d1ab3947`.
- Full mocked Playwright suite: source `132029b1265c5ae28a24b48b04907b94d1ab3947` plus the reviewed E2E fixture patch later committed as `2489d9178d217f2f6514a11870fff2e03a805bcf`.
- Live Playwright role-chain test: source `2489d9178d217f2f6514a11870fff2e03a805bcf` plus the reviewed live-spec patch matching `a8f05d75e26993606953e3da97bb6b437ccf9794`; the run was not recorded as an exact-HEAD checkout at `a8f05d7`.
- Branch: `feat/pre-evaluation-remediation`.

These are separate executions, not one command against a single source revision.

## Environment

The owned local API and web UI used ports 5262 and 5190. Native SQL checks verified `DB_NAME()` against the isolated SQL Server 2019 database `UniPM_PreEval_20261009_5262` before writes. AI integrations were disabled. No write was made to the original demo database/API on port 5254. The Playwright live configuration disabled tracing and did not print request bodies or credentials. Flutter dependency setup used the local cache with `--offline`; tracked package manifests remained unchanged.

## Commands and results

| Command                                                                                                                | Tested source                                                                                                 | Result                                                                                                                                                                                          |
| ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj`                                                             | `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`                                                                    | 342 passed, 0 failed, 16 skipped; retained TRX: `artifacts/pre-evaluation/runtime/backend-final.trx`.                                                                                           |
| `dotnet build server/server.csproj --configuration Release`                                                            | `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`                                                                    | Build succeeded, 0 errors and 1 existing CS8602 nullable warning in `PreventiveMaintenanceFormEndpoints.cs`.                                                                                    |
| `dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj` | `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`                                                                    | No model changes pending.                                                                                                                                                                       |
| `npm run test:run`                                                                                                     | `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`                                                                    | 25 files and 205 tests passed.                                                                                                                                                                  |
| `npm run lint`, `npm run typecheck`                                                                                    | `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f` plus patch matching `132029b1265c5ae28a24b48b04907b94d1ab3947`     | Both passed.                                                                                                                                                                                    |
| `npm run build`                                                                                                        | `132029b1265c5ae28a24b48b04907b94d1ab3947`                                                                    | Passed; Vite transformed 2,206 modules.                                                                                                                                                         |
| `npm run e2e -- --config ..\artifacts\pre-evaluation\runtime\playwright-pre-evaluation.config.mjs`                     | `132029b1265c5ae28a24b48b04907b94d1ab3947` plus patch committed as `2489d9178d217f2f6514a11870fff2e03a805bcf` | 44 passed. The initial run on the same base had 33 passed and 11 failures from stale mocks/assertions; fixtures and explicit Apply/Clear expectations were updated, then the full suite passed. |
| `flutter test --no-pub --name 'Inspector sees only own assigned schedules                                              | submits a draft with multiple rows and locks the editor' test/preventive_maintenance_form_draft_test.dart`    | `a8f05d75e26993606953e3da97bb6b437ccf9794`                                                                                                                                                      | 2 focused workflow tests passed. No Flutter source changed. |

The final live Playwright run executed `GSD, Supervisor, and Inspector complete an assigned PM batch through WMS referral` once and passed on the matching patch described above. It used the isolated `playwright-live.config.mjs` against the owned UI/API. The test created a unique synthetic two-asset batch; it verified GSD Supervisor assignment, Supervisor Inspector assignment, Inspector-scoped reads, assignment denials, whole-form submit gating, non-operational and operational rows, acknowledgement, WMS eligibility, initial referral, and preservation of form/inspection/schedule/dashboard state after referral. Retained output: `artifacts/pre-evaluation/runtime/workflow-live-rerun.log`.

## Native SQL Server verification

The migration-preservation harness used the owned SQL Server 2019 database at compatibility level 150, plus a separate disposable scratch database for the duplicate-migration guard. Before/after snapshots showed the original synthetic assets, schedules, inspections, forms, and acknowledgement were preserved. Current-year recovery added nine missing cycles, bringing schedules from 9 to 18; existing schedule records retained their original assignments and metadata. Two concurrent generation requests left the same 18-cycle set with no duplicates. The unique-index migration rejected a duplicate preflight case without deleting or rewriting schedules, and the unique index enforced `(AssetId, PmCycle)`. The retained migration-preservation harness output does not include a Git SHA or source-patch fingerprint, so those snapshot and guard results are observed harness runs, not exact-commit verification. The exact-commit backend TRX at `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f` separately records passing CPMP generation, assignment/inspection race, and WMS concurrency tests.

The WMS migration snapshot harness reported preservation of the existing 9 assets, 18 schedules, 5 inspections, 2 forms, and 1 acknowledgement, with no pre-existing WMS rows. That harness did not retain a source SHA or patch fingerprint. Four SQL Server concurrency tests passed in the exact-commit backend TRX at `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f`:

- `UniPM.Api.Tests.SqlServerInspectionSubmissionIntegrityTests.Concurrent_wms_referral_first_writes_return_one_conflict_and_one_audit_row`
- `UniPM.Api.Tests.SqlServerInspectionSubmissionIntegrityTests.Concurrent_wms_referral_corrections_return_one_conflict_without_extra_audit_history`
- `UniPM.Api.Tests.SqlServerInspectionSubmissionIntegrityTests.Inspection_start_wins_lock_before_supervisor_reassignment_and_assignment_is_rejected`
- `UniPM.Api.Tests.SqlServerInspectionSubmissionIntegrityTests.Supervisor_reassignment_wins_lock_before_inspection_start_and_inspector_is_reauthorized`

The separate read-only EF check is recorded in `artifacts/pre-evaluation/runtime/ef-pending-model-final.log`; the TRX does not record that command. A uniqueness retry was not claimed as exercised.

Raw native harness summaries and snapshots are under `artifacts/pre-evaluation-sql-harness/`; WMS race output is `artifacts/pre-evaluation-sql-harness/phase5-native-races-barrier-2.log`. Earlier sandboxed VSTest launches failed before executing tests because the test-host IPC handshake was unavailable; the successful native run used the host execution environment. No credentials or connection strings are recorded here.

## Browser observations

The following screenshots are retained under the ignored `artifacts/pre-evaluation-browser/` directory:

- `desktop-assets-scrolled.jpg` and `admin-schedules-access-denied.jpg`: sticky desktop sidebar/footer and role-denied route behavior.
- `assets-combined-filter.jpg`, `schedules-combined-filter.jpg`, `inspections-combined-filter.jpg`, and `forms-combined-filter.jpg`: combined filters, explicit Apply/Clear, URL updates, and detail-return preservation.
- `schedule-generation-idempotent.jpg`: GSD manual year generation reports already-existing cycles without duplicating them.
- `gsd-supervisor-assignment.jpg` and `supervisor-inspector-assignment.jpg`: distinct assignment stages and selector ownership.
- `wms-referral-correction-history.jpg` and `wms-filter-return.jpg`: recorded/corrected WMS reference, audit actor/time, status/search filtering, and return URL.
- `mobile-wms-filter-fixed.jpg`, `desktop-wms-filter-fixed.jpg`, `mobile-schedules-wrapped-navigation.jpg`, and `tablet-assets.jpg`: responsive widths, wrapped mobile navigation, and page overflow checks.

At widths 375, 768, and 1440 pixels, the checked pages had no horizontal document overflow. The desktop sign-out control remained visible while the main content scrolled. Observed contrast ratios were 6.02:1 for warning text and 5.93:1 for the referred-status badge. These were local browser checks, not WCAG certification or device testing.

The screenshots are manual browser artifacts captured during the corresponding feature patches. A per-screenshot Git SHA or patch fingerprint was not retained, so they are observational evidence and are not presented as exact-commit runs. The live role-chain run above has separate source-plus-patch provenance.

## Skips and unavailable verification

- Fifteen backend SQL Server 2019/Full-Text tests were skipped because `UNIPM_SQLSERVER2019_TEST_CONNECTION` was unset. Each reported: `Set UNIPM_SQLSERVER2019_TEST_CONNECTION to run the SQL Server 2019 Full-Text compatibility test.` The skipped names were:
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_semantic_candidate_cap_is_false_for_499_eligible_candidates`
  - `UniPM.Api.Tests.SqlServerPmAnalyticsTests.Gsd_analytics_uses_sql_server_2019_metrics_and_enforces_access`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_semantic_candidate_cap_is_true_for_501_eligible_candidates`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Section_deletion_cascades_its_embedding`
  - `UniPM.Api.Tests.SqlServerDomainContractTests.Form_migration_preserves_existing_inspections_and_leaves_form_link_null`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Registration_normalizes_applicability_and_persists_active_and_superseded_revisions`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Fixture_seed_preserves_unrelated_synthetic_records_and_reference_sections_are_full_text_searchable`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_embedding_rebuild_indexes_future_sections_and_refreshes_stale_vectors`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Sql_constraints_reject_invalid_supersession_lifecycle_combinations`
  - `UniPM.Api.Tests.SqlServerDomainContractTests.Sql_Server_2019_creates_the_separate_reference_full_text_catalog`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Sql_constraints_reject_duplicate_identity_sequence_and_invalid_section_embeddings`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_lexical_retrieval_returns_only_active_applicable_source_locatable_sections`
  - `UniPM.Api.Tests.Retrieval.ReferenceDocumentSqlServerTests.Institutional_semantic_candidate_cap_is_false_for_exactly_500_eligible_candidates`
  - `UniPM.Api.Tests.SqlServerDomainContractTests.Sql_Server_2019_with_full_text_search_applies_migrations_and_executes_reference_containstable`
  - `UniPM.Api.Tests.SqlServerDomainContractTests.Preventive_form_status_file_number_and_acknowledgement_constraints_are_enforced`
- One optional provider test, `UniPM.Api.Tests.Retrieval.OpenAiCompatibleEmbeddingSmokeTests.Configured_provider_returns_stable_vectors`, was skipped with: `Set the embedding smoke endpoint, model, API key, and a valid UNIPM_EMBEDDING_TEST_DIMENSIONS value to run the optional embedding smoke test.` No real or external AI provider was called.
- `npm run format:check` reported 142 paths on the Windows checkout. A read-only CRLF-normalized comparison with the installed Prettier 3.9.5 showed 141 were line-ending-only; the remaining OpenAPI JSON was normalized in `b181800762c44c1b3233c0302ef48aa249c6bb9d`. The changed OpenAPI file's Prettier check passed. The full format check was not rerun, and Linux CI is not claimed.
- `npm run api:check` was affected by the Windows generated-file line-ending limitation. Independent review of the generated-client diff found no semantic drift and no untracked output at that point; a post-normalization regeneration/check run is not claimed. CI verification remains pending.
- Physical Android/iOS behavior, production deployment, CI, institutional GSD acceptance, and live institutional WMS integration were not tested.

## GSD questions and remaining limits

The CPMP manual's operative frequency table (page 3) lists Water Drinking Stations in February, May, August, and November. Its revision history (page 19) lists June and December. The implementation keeps the operative-table months pending GSD confirmation.

GSD should confirm the governing WDS months, accepted department/location catalog, and whether the displayed “External WMS PM number” needs an institutional format rule. This implementation stores a trimmed, bounded text reference and an audit history; it does not integrate with or monitor the external WMS process. The synthetic workflow and demo accounts do not constitute GSD process acceptance.

## Failures and corrections

An earlier backend run had 335 passed, 7 failed, and 16 skipped. Failures included same-asset/same-cycle fixture collisions, tests that manually inserted schedules after registration had generated them, and a real department-validation ordering regression. The code now preserves the original 400 validation before lock acquisition, and fixtures reuse eligible generated schedules or distinct valid schedule cycles. The corrected full backend suite passed 342 tests. The first full mocked Playwright run is retained separately as 33 passed and 11 failed in `artifacts/pre-evaluation/runtime/web-e2e-mocked-final.log`; stale fixtures and navigation expectations were corrected, then `artifacts/pre-evaluation/runtime/e2e-full-integrated.log` records 44 passed. Test-host launch failures before execution are not counted as test failures.
