# Capstone remediation, October 25, 2026

## Scope and baseline

The repository owner requested the twelve-phase comprehensive remediation plan
and exactly two Luna Max executors. The main chat reviews changes, controls
phase transitions, and owns commits. No deployment, paid provider calls,
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
| 1 | Corpus test assumptions and React render/ref lint | Ready for correction |
| 2 | Authorization and record access | Pending |
| 3 | Offline timestamp provenance and synchronization | Pending |
| 4 | NLA routing, shortcuts, validation, evaluation boundaries | Pending |
| 5 | Acknowledgement integrity | Pending |
| 6 | Reporting semantics and historical grouping | Pending |
| 7 | Authorized pagination and bounded reporting | Pending |
| 8 | Mobile recovery, offline boundaries, release configuration | Pending |
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

## Phase 1 changes awaiting verification

Commit `ca81bf8a95b3b287f484807cd296e2466766dda2` fixes RM-001 and
addresses the repeated regex construction behind RM-006. The temporary test
fixture copies the reviewed corpus and changes one review decision to pending.
Its manifest stays frozen and hash-valid, so the check isolates the review
gate. The approved corpus and historical labels remain unchanged. A second
case checks that the frozen reviewed corpus can be scored.

The month guard caches independent patterns. Review rejected a union-pattern
approach because non-overlapping regex matches would miss the second month in
`Jan 2026 Feb`. The retained regression checks that this remains ambiguous.
Post-change verification has not yet been recorded here.
