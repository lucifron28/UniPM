# Capstone remediation, October 25, 2026

## Scope and baseline

The repository owner requested the twelve-phase comprehensive remediation plan
and exactly two Luna Max executors. The main chat reviews changes, controls
phase transitions, and reviews commits. Executors may commit their assigned
files under the shared commit lock. No deployment, paid provider calls,
institutional database access, or manuscript edits are authorized by this run.

The integration baseline is `bdc07caeeb37c31ca7b524b7ef45cde5824a783c`
on `main`, the squash merge of the guided-interpretation work. The tip of
`feature/nla-guided-interpretation` is
`6437fa7b1e9585a6d4ab7aa377fe0d99dcae1503`. Their committed trees are
identical. Their divergent commit histories do not indicate missing source
changes. No merge was needed or performed.

The remediation branch is `feature/capstone-remediation`, in a separate
worktree. Three existing uncommitted web/mobile files and the original
checkout's untracked `tmp/` directory are preserved in place. They are outside
this baseline and will not be silently included in the remediation.

## Protected behavior

- Preserve `Draft -> Submitted -> Acknowledged` and one acknowledgement for
  the whole multi-asset form.
- Saving a completed inspection row completes its linked schedule. Submission
  and acknowledgement are separate transitions.
- Only acknowledged rows, plus documented legacy exceptions, enter official
  maintenance history. Execution progress can include completed Draft rows.
- Backend code validates report plans and calculates results. DeepSeek remains
  optional and cannot issue SQL or modify maintenance data.
- Preserve the current server-authoritative compliance timestamp until an
  institutional decision approves another policy.
- Preserve existing migrations and historical evaluation records. Do not
  restore maintenance-history RAG or add predictive maintenance.

## Phase control

| Phase | Work | Status |
|---|---|---|
| 0 | Branch comparison, source audit, baseline commands | Completed; findings below |
| 1 | Corpus test assumptions and React render/ref lint | Completed; local checks passed, skips retained |
| 2 | Authorization and record access | Completed; TEST-155 records scope and local checks |
| 3 | Offline timestamp provenance and synchronization | Completed; TEST-156 records policy, recovery and isolated SQL check |
| 4 | NLA routing, shortcuts, validation, evaluation boundaries | Completed; TEST-157 distinguishes parser, fake provider and native SQL checks |
| 5 | Acknowledgement integrity | Completed; TEST-159 includes concurrent native SQL receipt capture |
| 6 | Reporting semantics and historical grouping | Completed; TEST-160 documents current metadata, stable counts and form headers |
| 7 | Authorized pagination and bounded reporting | Completed; TEST-161 records page scope, both clients and bounded synthetic/SQL measurements |
| 8 | Mobile recovery, offline boundaries, release configuration | Completed available checks; TEST-158/162 record recovery, identity and release boundaries; physical acceptance remains pending |
| 9 | Evidence reconciliation and proposed manuscript corrections | Completed; separate revision proposal and accepted survey recalculation, no manuscript edits |
| 10 | Security review and proposed institutional operations | Completed source review and operating proposal; deployment and institutional decisions remain open |
| 11 | Final available regression checks and remaining risks | Completed available checks; TEST-163 records full backend/web/mobile results and explicit opt-in skips |

Raw execution logs are ignored under `artifacts/remediation/`. Baseline
commands refer to the integration SHA above. Later executions must identify
the actual tested commit, or the base commit and an identified working patch.
Historical passes do not establish a pass on this branch.

## Initial findings

| ID | Severity/type | Source | Finding and proposed correction | Regression risk |
|---|---|---|---|---|
| RM-001 | Critical, reported test defect | `tests/`, V3 evaluation harness | The reported unreviewed-label test assumes a corpus state that changed after researcher approval. Confirm by execution, then use an explicit unreviewed fixture and retain an approved-corpus acceptance check. | Do not weaken fail-closed evaluation or revise approved labels. |
| RM-002 | Critical, reported lint defect | `web/src/features/reports/pm-analytics-v3.tsx` | Reproduce render-time ref access and correct its state handling without changing clarification behavior. | Resetting question, preview, or clarification state incorrectly. |
| RM-003 | High, confirmed routing behavior | `server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs` | Two exact question mappings bypass normal default interpreter selection. Remove implicit mappings or require explicit template selection with provenance. Preserve the questions as regressions. | Do not count a local mapping as independent provider interpretation or change backend calculations. |
| RM-004 | Medium, documentation inconsistency | `reference/planning/mvp-definition.md`, `PROJECT.md`, evidence index | Some current-looking text still describes NLA as unimplemented or reviewed labels as pending. Reconcile current status with the integrated source and dated evidence, preserving historical records. | Avoid replacing historical limitations with unsupported present-day verification claims. |
| RM-005 | High, evidence reconciliation | `reference/evaluation/manuscript-2026-10-23/` | The collection says SUS item responses are unavailable and BB cases were not executed in that collection. Later supplied survey files and the researcher's confirmation of a final execution report must be recorded as separate sources. | Do not infer a new execution or fabricate case-level dates, versions, tester identities, or captures. |
| RM-006 | High, confirmed baseline failure | `NaturalLanguageAnalyticsQuestionGuard.FindMonths`, interpretation tests | A malformed-output test fails earlier in question assessment because repeated dynamic month regex construction times out. Cache independent abbreviation patterns while retaining their original matching rules and timeout. | Preserve overlapping month detection and fail-closed clarification; do not accept malformed output. |
| RM-007 | Critical, confirmed access defect | `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs` | Authenticated form list/detail reads lack record-level filtering. Apply existing operational roles and ownership/assignment scope before materialization and detail retrieval. | Preserve authorized inspector history and supervisor/GSD work; do not invent final institutional RBAC. |
| RM-008 | Critical, retained legacy exposure | `server/Features/Inspections/` | Some inspection list/detail/history routes retain anonymous reads, including remarks and recommendations. Require operational access and apply appropriate record scope consistently with form reads. | Existing anonymous integration tests must authenticate; preserve intentional acknowledged-only history filtering. |
| RM-009 | High, instruction/code inconsistency | `server/Features/Auth/PreventiveMaintenanceScheduleAuthorization.cs` | Technical Admin receives operational schedule access. Audit recent administration requirements and remove implicit operational privileges unless an explicit approved exception exists. | A technical administrator with an additional operational role must retain that role's access. |
| RM-010 | High, intentional timestamp limitation | Mobile offline drafts, form save, reports | Server receipt determines authoritative completion time. A later synchronization can be late even if work was recorded offline earlier. Preserve this policy pending institutional review, expose its meaning, and test synchronization boundaries. | Do not trust device time for compliance or invent legacy field-work times. |
| RM-011 | Medium, confirmed resource risk | PM form list | The list loads every matching form and related graphs. Add authorized, deterministic server pagination and update consumers together. | Apply visibility before total counts and pagination; avoid silently truncating clients. |
| RM-012 | Medium, verification gap | Mobile release and acceptance | No device is attached. Inspect the placeholder application ID and release controls, then record unavailable physical acceptance separately. | Changing the application ID creates a distinct installed app and does not transfer unsynchronized local drafts. |
| RM-013 | Critical, confirmed offline data-loss defect | `OfflineInspectionRepository.deactivate`, account-scope routing | Logout clears memory in `finally` even when secure persistence fails. Retain the owner's queued work on failure, lock forced exits, block cross-account access and stop in-flight sync before another account can issue subsequent requests. This recovery correction is brought forward from the mobile audit. | Data retention must not expose or transmit one account's work through another account's session. |

## Phase 0 baseline results

The full backend command executed on the unchanged integration source:

```powershell
dotnet test UniPM.slnx --logger "trx;LogFileName=baseline.trx" --results-directory artifacts/remediation/backend
```

Result: 645 total, 593 passed, 2 failed, 50 skipped. The two failures are
RM-001 and RM-006. SQL-dependent checks skipped without an isolated configured
connection. The TRX and command output remain under the ignored artifact path.
This execution does not establish native SQL Server verification.

The baseline web command `npm run lint` failed with exit 1 and one
`react-hooks/refs` error at `pm-analytics-v3.tsx:3259:35`. The callback created
during rendering reaches a ref through `updateClarificationFilterValues`.
This confirms RM-002. Remaining web checks will run after the Phase 1
correction rather than duplicate the full check battery on known failing code.

The client worktree initially lacked dependencies. Lockfile-based dependency
setup is permitted; it is not a code correction. The system Node runtime is
24.15.0 while the project requires Node 22. Flutter is 3.44.5 with Dart 3.12.2.
No Android device is attached. An isolated Node 22.23.4 runtime is available
for subsequent web checks. Flutter analysis is being recorded separately.

Phase 0 permits the focused Phase 1 corrections. Later-phase source changes
remain gated on Phase 1 verification. This audit distinguishes confirmed
failures from the policy limitations above; it does not claim that all
remaining risks have been reproduced.

Backend and client audit findings, baseline counts, corrections, and remaining
risks will be added after their command outputs and diffs are reviewed.

## Phase 1 corrections and verification

Commit `ca81bf8a95b3b287f484807cd296e2466766dda2` fixes RM-001 and
addresses the repeated regex construction behind RM-006. The temporary test
fixture copies the reviewed corpus and changes one review decision to pending.
Its manifest stays frozen and hash-valid, so the check isolates the review
gate. The approved corpus and historical labels remain unchanged. A second
case checks that the frozen reviewed corpus can be scored.

The month guard caches independent patterns. Review rejected a union-pattern
approach because non-overlapping regex matches would miss the second month in
`Jan 2026 Feb`. The retained regression checks that this remains ambiguous.
The full backend suite at that commit passed 597 tests, failed zero, and
skipped 50, out of 647 total. Command:

```powershell
dotnet test UniPM.slnx --logger "trx;LogFileName=phase1.trx" --results-directory artifacts/remediation/backend
```

Exit 0; duration 2 minutes 35 seconds. SQL and real-provider verification
remain outside this result.

Commit `87cb905c86f3aa80638cd5fd00d032d9ea4c25f9` removes the generic
clarification filter's render IIFE and adds a preview regression. Formatting
and lint passed at that commit. Type checking found an unchecked array lookup
in Back navigation. Commit
`9b143d12e13864c351a0e2defe54ea8b8b65f122` guards that lookup and waits
for the catalog option in the new test. Type checking and the production build
passed at this later commit. Contract sanity and all seven negative contract
checks passed at documentation-only intermediate commit `0bc5701`.

The full web test attempt returned 246 passed and 10 failed out of 256.
It was a diagnostic run with an uncommitted callback guard, so it is not
accepted as exact-commit regression evidence. End-of-run source/test hashes
are retained in its log. Nine failures concern existing UI tests; their
causes remain unresolved. The new generic-filter test fails because
`makeCatalog()` provides no filter fields. Its focused rerun at `9b143d1`
also failed, with one failed and 18 deselected tests. Waiting cannot populate
an empty fixture. A fixture correction requires adding the Department filter
definition while preserving the preview-plan assertions.

The repository retry limit was reached for this check. The repository owner
then explicitly authorized all remaining permissions for this task and asked
that no more routine permission prompts be sent. This authorizes scoped
correction and verification beyond the default retry limit. The original
plan's prohibitions on deployment, paid calls, and institutional data access
remain in force.

Commit `008f3231fb75973b27d6186a627c46701a344d4c` adds the Department
filter to this test's catalog fixture. Commit
`3e166f405ec3026dbc41ab57b21d1883df227d82` selects the actual picker by
its placeholder. The control has a linked label; its datalist gives it the
native combobox role, so the earlier textbox query was incorrect. No missing
label defect is established. The focused test now passes, with one passed
and 18 deselected cases. The preview-plan assertions remain unchanged. A full
unit run with two workers passed all 256 cases in 31 files at this source.

Playwright at `9b143d1` executed 61 cases: 53 passed, 7 failed, 1 skipped.
Its worktree-owned Vite child started on port 5173; the executor checked its
listener/process identity rather than assuming an existing server belonged
to this source. Raw outputs remain in `artifacts/remediation/clients/`.

The isolated Playwright follow-up at `3e166f4`, with `CI=true`, port 5197
and two workers, passed 60 cases with one skipped. Its owned server identity
and initially free port are recorded. Commit
`bb3f127e714043bf1d8eddfbcd986164d528b420` sets the unit-test worker
limit to two in Vite configuration. The ordinary `npm run test:run` command
then passed all 256 cases in 31 files, with no timeout or assertion changes.
The earlier failed runs remain preserved separately in TEST-153.

Formatting, lint and type checking each passed at `bb3f127`. API contract
sanity and all seven negative checks passed earlier on the unchanged contract;
the production build passed at `9b143d1`, with no application behavior changes
after that build. This establishes local Phase 1 verification, not a remote
GitHub Actions pass or native SQL/device/provider acceptance.

## Phase 2 access decision

The current AGENTS boundary and ADR-006 define Admin as technical
administration. Remove standalone Admin operational grants while retaining
Admin management routes. An account with another operational role retains
that role's ordinary scope.

GSD can read operational records. Inspectors can read their created forms,
historical inspection work, and currently assigned form work. Supervisors can
read the work covered by their existing schedule assignments. A legitimate
member can read the shared multi-row form needed for whole-form receipt;
this does not grant access to unrelated workers' Draft forms. Filter before
loading related records. Wrong roles receive 403 and unreadable detail IDs
receive 404. Apply the same boundary to inspection and photo reads while
retaining acknowledged-only official history.

Phase 2 source work and its targeted verification are complete, as recorded
in TEST-155. Later-phase audit notes do not establish
completed corrections or executed acceptance.

## Correction ledger

The initial findings above preserve the audit's starting state. This ledger
records the corrections and their remaining limits. Paths are repository-relative.

| Issue | Root cause and changed files | Correction and executed result | Remaining limit |
|---|---|---|---|
| RM-001, Critical | `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3EvaluationHarnessTests.cs` assumed the frozen labels remained unreviewed. | An explicit copied pending-label fixture tests fail-closed behavior; a separate case accepts the approved corpus. Full backend Phase 1 passed 597, skipped 50. TEST-153. | Development corpus agreement is not independent accuracy. |
| RM-002, Critical | `web/src/features/reports/pm-analytics-v3.tsx` reached ref-backed clarification state during render. | Removed the render IIFE, guarded Back navigation and corrected the regression fixture. Web unit 256 passed; browser 60 passed, one skipped. TEST-154. | Local checks do not establish remote CI or GSD acceptance. |
| RM-003, High | `PmAnalyticsV3QuestionInterpreter.cs` selected two exact reviewed questions before normal routing. | Removed implicit runtime mappings, retained the questions as regressions and added a bounded generic phrase rule. TEST-157: backend 69 passed, UI 19 passed, parser 18/18 and 69/69. | Fake-provider tests are not a new live DeepSeek evaluation. |
| RM-004, Medium | Current-looking planning summaries retained historical PMIS-only and SQL-verification claims. | README, PROJECT, planning headers and evidence index now distinguish dated baselines from this remediation. | Historical records are preserved; deployment and current-build physical acceptance remain pending. |
| RM-005, High | The repository collection preceded the supplied item-level survey CSVs and later BB execution attestation. | Separate reconciliation proposal recalculates accepted survey totals and records the 32-case attestation. No raw responses or manuscript files were changed. | The particular BB case-level attachment still needs identification. Do not populate missing case metadata from automated tests. |
| RM-006, High | `NaturalLanguageAnalyticsQuestionGuard.cs` repeatedly constructed month regexes and timed out before the intended malformed-output assertion. | Cached independent bounded patterns, preserving overlapping month detection. Full backend Phase 1 passed. TEST-153. | Regex timeouts remain a safety boundary. |
| RM-007, Critical | `PreventiveMaintenanceFormEndpoints.cs` authenticated readers without applying form ownership/assignment scope. | Shared `OperationalInspectionReadScope.cs` applies GSD/Inspector/Supervisor scope before materialization, count and page; unreadable details return 404. TEST-155: 188 passed, one skipped, plus 28 passed. | Legitimate members can see the shared whole form. Final institutional RBAC remains provisional. |
| RM-008, Critical | Inspection/history/photo read paths retained anonymous or insufficiently scoped access. | Inspection endpoints and photo reads now require operational access and the shared scope. Existing authenticated workflow assertions remain. TEST-155. | Official-history eligibility remains acknowledged-only plus documented legacy exceptions. |
| RM-009, High | `PreventiveMaintenanceScheduleAuthorization.cs`, role policies and web guards treated technical Admin as an operational role. | Removed standalone Admin operational grants; retained explicit technical management tools and combined-role access. Web role 27 and form 21 passed. TEST-155. | Technical Admin tools remain privileged and mutable; institutional restrictions are a separate decision. |
| RM-010, High | Mobile entered/device dates could be confused with backend completion time. | Form save/idempotency logic, mobile labels and report documentation identify first server-accepted save as the compliance clock. TEST-156: backend 63, isolated SQL one, plus focused client checks passed. | Delayed offline sync can count as late. No trusted physical-work timestamp was invented. |
| RM-011, Medium | Form list loaded complete graphs; web filtered/sliced downloaded forms locally. | Backend page/options contracts, generated web client/registry and mobile page draining now preserve caller scope and all filters. TEST-161 records paging, SQL and bounded scale checks. | Offset paging is not a concurrent snapshot. The API/client upgrade must be coordinated. |
| RM-012, Medium | Android retained `com.example.mobile`; no device was attached. | Android namespace/application identity and Kotlin path use the selected UniPM identity. TEST-162 records release controls and available checks. | Signed distribution, old-queue migration and physical acceptance are not established. |
| RM-013, Critical | `offline_inspection_repository.dart` cleared memory in a logout `finally` even when persistence failed. | Retained failed writes, separated authorized session/cache owner, locked forced exits and blocked cross-account sync. Router/session handling and regression tests changed. TEST-158: repository 24 and session 10 passed; analyzer clean. | Injected failures are not actual device storage exhaustion. Bytes never durably stored cannot survive physical process loss. |
| RM-014, Medium | `StagingDemoInitialization.cs` and `scripts/initialize-azure-staging-demo.ps1` still expected 22 migrations while the source contained 23. | Updated exact schema manifests and fail-closed guards, without adding migrations or auto-updating a database. Native SQL full regression includes the corrected initializer cases. TEST-163. | The staging script was syntax-checked only, never executed against a deployment target. |
| RM-015, Medium | Four SQL race fixtures used initial assignment for reassignment, and two browser files mocked the retired form-array contract. Staging fixture setup and persisted roles also prevented intended checks. | Corrected endpoint/DTO, roles, empty-database setup and page/options mocks. Preserved inspector-retention policy, race gates and workflow assertions. Full backend and browser runs passed. TEST-163. | These are engineering fixtures, not new GSD acceptance. Earlier failures remain recorded. |
| RM-016, Medium | `web/vite.config.ts` used VM-based workers. Full-suite attempts showed a timeout and router/mock-context failures. | Changed only the Vitest pool to process-based `forks`, retaining two workers, assertions and timeouts. Node 22 focused 79/79 and full 270/270 passed. TEST-163. | The results support the runner correction, not a claim that every earlier failure had an independently proven internal cause. |

## Other reviewed corrections

TEST-159 records signature format/size, one acknowledgement per Submitted
form, separate capture operator and signatory, and a concurrent SQL race.
Backend 46 passed, mobile 11 and web 21 passed. The drawn signature is a
receipt captured through an authenticated operator, not cryptographic signing.

TEST-160 records current asset metadata explicitly in reports and inspection
rows. The transfer regression passed four backend cases and focused web/mobile
presentation cases. Counts, completion clocks, official-history eligibility
and recorded form headers stay stable when current asset grouping changes.
No historical snapshot or new transfer workflow was introduced.

## Evidence and operational deliverables

- [Evidence reconciliation and proposed manuscript corrections](capstone-evidence-reconciliation-2026-10-25.md).
- [Institutional operating and security proposal](capstone-institutional-operations.md).
- [Remaining-risk register and defense boundaries](capstone-remaining-risks.md).
- [Commit list](capstone-remediation-commits.md).
- [Engineering execution records](../evidence/INDEX.md), TEST-152 onward.

The evidence proposal preserves accepted SUS/ISO responses, the February 19
interview date and Engr. Ariel Nicolas's surname. It does not rewrite the
submission files, claim a new live-provider score, or add the saved-query
feature to the study. Six appendix attachment placeholders remain.

The operating proposal covers IIS/.NET/SQL prerequisites, account roles,
TLS and secrets, photo/database consistency, backup/restore, coordinated
API/APK updates, support ownership and cost-estimation inputs. Deployment,
institutional policy approval and real-data recovery were not performed.

The NLA roadmap's Phases 7–8 remain planning. They are separate from the
numbered remediation phases in this report.

## Final verification and defense readiness

TEST-163 records the final locally executed regression. Backend build passed
with zero warnings/errors; the full suite passed 672 and skipped the single
opt-in external embedding smoke case, 673 total. Native SQL fixtures used
fresh GUID databases on the local SQL Server 2019 host. No SQL test skipped.
Web unit tests passed 270/270. Browser tests passed 60 with one opt-in live
backend workflow skipped. Flutter passed 194/194 and produced a debug APK.
Formatting, lint, production build and API generation checks passed at the
source identities documented in that record; Flutter analysis is reused on
the identical mobile subtree. These are local results, not a remote CI run.

The final code/test-runner revision is
`d6c2e9d1f576754e9635f948bd814f98605d8c5e`. Backend, client application
and browser checks ran at `ac5787c`; the only later source change is the
Vitest pool setting, which received its own focused and full unit checks.
Closing documentation does not change executable source. The branch remains
local and unmerged. The commit list identifies each logical correction.

Confirmed defects are corrected within the available scope. Intentionally
retained limitations include server-receipt compliance time, current asset
grouping, Development-gated provider routing and receipt capture through an
authenticated operator. Those choices require accurate explanation in the
study; they are not evidence of verified physical inspection or signatory
identity. The separate manuscript proposal records the necessary wording
changes, historical provider evidence and accepted survey totals.

Physical-device acceptance, release signing, independent evaluation,
institutional hosting/restore approval and a new live-provider experiment
remain outside this execution. Six appendix attachments and the identified
historical BB report still require the documented follow-up. The original
checkout's uncommitted work was preserved and was not part of this tested
revision. Do not claim production readiness or a perfect standalone model
score from these engineering checks.
