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
| 8 | Mobile recovery, offline boundaries, release configuration | Urgent storage/account fix completed in TEST-158; release audit remains |
| 9 | Evidence reconciliation and proposed manuscript corrections | Pending |
| 10 | Security review and proposed institutional operations | Pending |
| 11 | Final available regression checks and remaining risks | Pending |

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
