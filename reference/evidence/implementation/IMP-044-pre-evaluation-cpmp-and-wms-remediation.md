---
id: IMP-044
type: implementation
title: CPMP scheduling, two-stage assignment, registry filters, and WMS referrals
status: reviewed
recordedAtUtc: 2026-10-09T12:17:40Z
sourceCommit: b181800762c44c1b3233c0302ef48aa249c6bb9d
sourceBranch: feat/pre-evaluation-remediation
evidenceLevel: source-inspected
---

# CPMP scheduling, two-stage assignment, registry filters, and WMS referrals

## Objective

Complete the approved pre-evaluation remediation in the isolated worktree: improve registry discovery and access, generate missing CPMP schedules idempotently, separate supervisor and Inspector assignment, and record manual WMS references with an audit trail while preserving inspection, form, and schedule history.

## Source identity

The change starts at `444cd7fcca8a48468638c6d46c817e82583107a1` and ends at `b181800762c44c1b3233c0302ef48aa249c6bb9d` on `feat/pre-evaluation-remediation`. The implementation and follow-up commits are:

| Commit                                     | Change                                                                                                                                   |
| ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `5c1b5f0b446a4369c12081c9f2cdd58d77d2a9f4` | Role-aware web navigation, access-denied route state, anchor color-layer correction, sticky desktop shell, and sidebar regression tests. |
| `9921a92ecb1ec91935b155e6cc2c47b8b7d8ee80` | Bounded department, category, cycle, status, date, and keyword filters on the four registry APIs.                                        |
| `e8abf3e1a37d9643d9646bd19e9cec38dd290f54` | Explicit Apply/Clear filters, URL state, retained results, and detail-return context across the registries.                              |
| `5c574dd302c385e3e89b0c6b594aa95abe8ae555` | Automatic CPMP cycle generation, worker recovery, registration generation, and unique `(AssetId, PmCycle)` migration.                    |
| `a9d2216758b43dc6d0ec896d58c2979c4d705ad6` | GSD year-based schedule-generation action and result reporting.                                                                          |
| `4f93f03a04e3cf41ba5c8b81960ddbe1a0cbc696` | GSD supervisor assignment and Supervisor Inspector assignment policies, endpoints, and shared schedule/inspection lock.                  |
| `494915873887185a1083d3ef07ea846f096b2bd5` | Two-stage assignment options and web UI.                                                                                                 |
| `2d0085c3e6b917a773a97873a48f6038a103c714` | Inspection-linked WMS reference and append-only correction audit migration and API.                                                      |
| `f3e66d3e6c819c49b83a7fe19a61d09c92f63957` | WMS status/search filters, GSD editor, and audit-history display.                                                                        |
| `abfa199d52146396e5cf195b507436dfb55fcb14` | Mobile registry filter sizing and success-badge contrast correction.                                                                     |
| `3f655cf106b183c0ab42e4176f288685215c3e85` | Preserve the existing department validation response before taking the assignment lock.                                                  |
| `aa1d04d7a1946b27c816b3029dfbd85adaa43d1f` | Align backend fixtures with generated schedules and the expanded WMS contract.                                                           |
| `132029b1265c5ae28a24b48b04907b94d1ab3947` | Keep applied form filters and query results mounted while draft filters change.                                                          |
| `2489d9178d217f2f6514a11870fff2e03a805bcf` | Align mocked Playwright fixtures with the two-stage and WMS contracts.                                                                   |
| `a8f05d75e26993606953e3da97bb6b437ccf9794` | Add live GSD-to-Supervisor-to-Inspector workflow coverage through WMS referral.                                                          |
| `b181800762c44c1b3233c0302ef48aa249c6bb9d` | Normalize formatting in the OpenAPI JSON without changing its parsed data.                                                               |

## Implementation summary

The four registry endpoints accept bounded combined filters. Web registries keep draft values separate from applied values, encode applied filters and page in the URL, preserve previous results during refetch, and restore filter context on detail return. Form search excludes signatory and signature data. The form lifecycle and official-history visibility rules remain unchanged.

Asset registration creates eligible cycles from the current institutional month. The hosted worker retries current-year recovery at startup and daily. The GSD action can request a supported current or past year. Existing schedule rows are retained, and generation does not assign personnel. Missed cycles whose deadline has passed are created as Overdue. The unique-index migration rejects duplicate existing `(AssetId, PmCycle)` rows without rewriting schedule data.

Assignment has separate GSD and Supervisor policies and options. GSD selects the responsible Supervisor; the Supervisor selects the Inspector. Changing the Supervisor clears a stale worker assignment. A batch with linked inspection or completion evidence cannot be reassigned. Assignment and inspection creation use the same transaction-owned SQL application lock, then reload and reauthorize the schedule before writing. The public schedule read contract remains intact.

WMS referral tracking is GSD-only and uses one current external PM number per inspection plus append-only revision audit rows. The API requires an acknowledged form, a completed inspection, and a non-operational result. A bounded nonblank external reference can be corrected with an expected revision; concurrent stale edits return a conflict. The referral operation does not change inspection, form, acknowledgement, schedule, or compliance values. Existing form-less official-history rows remain readable under their existing contract.

The desktop shell keeps its identity and sign-out control visible while the main page scrolls. The anchor color reset now lives in Tailwind's base layer, so active navigation links retain their white text utility. Mobile navigation wraps rather than hiding links in a horizontal strip. Interactive control borders and small warning/success badge text meet the checked contrast thresholds. The maroon palette is unchanged.

## Important files and database changes

- `server/Features/Schedules/PreventiveMaintenanceScheduleGenerationService.cs` and `PreventiveMaintenanceScheduleGenerationWorker.cs`: idempotent CPMP generation and recovery.
- `server/Features/Schedules/ScheduleBatchMutationLock.cs`, `SchedulesEndpoints.cs`, and `PreventiveMaintenanceFormEndpoints.cs`: batch identity, authorization, and shared assignment/inspection synchronization.
- `server/Features/Inspections/InspectionsEndpoints.cs`, `InspectionFollowUpStatusCatalog.cs`, and the two `InspectionWmsReferral` models: referral API and audit contract.
- `server/Migrations/20261009020518_AddUniqueScheduleAssetCycle.cs`: unique schedule-cycle guard.
- `server/Migrations/20261009041512_AddInspectionWmsReferralTracking.cs`: referral and audit tables.
- `web/src/features/{assets,inspections,preventive-maintenance-forms,schedules}`: filters, schedule generation/assignment, and WMS UI.
- `web/openapi/unipm-v1.json` and `web/src/api/generated`: API contract and generated clients.

## Tests present

Focused backend tests cover registry filters, generated cycles and eligibility, worker retry and year rollover, schedule assignment authorization, assignment/inspection races, WMS eligibility, audit revisions, stale updates, and source-record preservation. Web tests cover applied filters, return context, the two assignment stages, WMS status/search, and access states. The live Playwright spec exercises the role chain and whole-form workflow against an isolated API/database.

## Verification status and limitations

TEST-060 records execution identities and results as of its run. The CPMP
manual is internally inconsistent: Section 2.4.2's operative frequency table
(PDF page 3) lists Water Drinking Stations in February, May, August, and
November, while Section 2.7's revision history (PDF page 19) describes June
and December. On 2026-10-10, the project owner resolved the implementation
choice in favor of Section 2.4.2. The manual itself has not been corrected.
The existing implementation and category-reference test match that table;
TEST-070 records the focused verification. This is no longer a technical
release blocker. GSD approval of the schedule-generation effective date,
staging acceptance, physical-device testing, and deployment verification
remain pending.

The native SQL Server checks used the disposable `UniPM_PreEval_20261009_5262` database. They do not establish institutional acceptance or deployment verification. Final institutional role policy, external WMS number conventions, real GSD process acceptance, CI, and physical-device behavior remain unverified.

## Related evidence

- [TEST-060: Pre-evaluation remediation verification](../test-runs/TEST-060-pre-evaluation-remediation.md)
