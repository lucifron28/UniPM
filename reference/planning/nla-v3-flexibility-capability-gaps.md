---
id: NLA-V3-FLEXIBILITY-CAPABILITY-GAPS
type: capability-inventory
title: NLA V3 operational question capability and gap inventory
status: draft
recordedAtUtc: 2026-11-07T02:05:04Z
evidenceLevel: source-inspected
---

# NLA V3 operational question capability and gaps

## Scope

This inventory describes the current V3 catalog and known follow-up ideas. The
examples are developer-authored and synthetic. They are not GSD interview
questions, GSD-approved terminology, or a validation set. No new subject,
measure, formula, or business rule is implemented by this document.

Class A means V3 can represent the request with its current report contract.
Language handling may still ask for clarification. Class B means the existing
data may be relevant, but a new measure, formula, subject, or population policy
needs approval. Class C needs an authoritative historical or external source
that V3 does not have. Class D is ambiguous, unsafe, or outside the approved
scope and should be clarified or rejected.

## Operational question inventory

| # | Developer-authored question | Class | Available fields or missing source | Approval needed | Research value |
|---:|---|:---:|---|---|---|
| 1 | How many fire-extinguisher PM cycles were scheduled by department in 2026? | A | Schedule cycle/status, category, department | Confirm vocabulary only | Checks scheduled population and department grouping. |
| 2 | Show completed emergency-light inspections by building for 2025. | A | Schedule, inspection completion, category, building | Confirm vocabulary only | Exercises completed count and a second grouping dimension. |
| 3 | How many inspections finished on time versus late by department? | A | `CompletedOnTimeCount`, `CompletedLateCount`, department | Confirm intended comparison wording | Checks distinct completion outcomes. |
| 4 | Which departments had the most missed fire-alarm cycles in 2026? | A | PM-cycle deadline, incomplete schedule, department | Confirm vocabulary only | Checks missed measure and ranking. |
| 5 | Show the five buildings with the lowest on-time compliance. | A | On-time compliance measure, building, `Top` limit | Confirm ranking basis and wording | Checks percent ranking and the 1–20 cap. |
| 6 | Compare scheduled and completed counts by category in 2025. | A | Scheduled and completed measures, category, PM cycle | Confirm comparison wording | Checks multi-measure full-plan preservation. |
| 7 | Show departments with below 80% on-time compliance and at least 10 scheduled cycles. | A | Two catalog measures and aggregate filters | Confirm threshold units | Checks strict/inclusive numeric operators and measure association. |
| 8 | List fire extinguishers with a recorded non-operational inspection finding. | A | Inspection condition and source-record fields | Confirm that `IsOperational` is the intended saved finding | Tests record mode without converting the request to repair status. |
| 9 | How many registered fire extinguishers currently exist? | A | Asset category and registration state | Confirm current inventory population | Tests asset inventory separately from PM execution. |
| 10 | List the scheduled records for a named building and month. | A | Schedule cycle, building, source fields | Confirm month is a PM cycle, not an inspection date | Tests named scope and record listing. |
| 11 | How many cycles were completed within the Inspector assignment period? | A | Initial assignment timestamp, completion timestamp, 30-calendar-day assignment measure | Confirm the assignment policy already recorded in the catalog | Checks Inspector timing stays separate from PM deadlines. |
| 12 | Show records for asset code `FE-DEMO-001` in 2026. | A | Asset code, category, PM cycle, schedule and inspection fields | Confirm code syntax only | Checks codes are not mistaken for threshold operands or years. |
| 13 | How many assets are registered in North Block? | A | Asset registration and building/location fields | Caller must identify building or location when ambiguous | Tests named-place scope and safe clarification. |
| 14 | Show Filipino relative-year scope for nakaiskedyul na pamatay-sunog. | A | Existing categories, schedule fields, fixed Asia/Manila clock | Confirm local vocabulary with GSD before field use | Tests language parsing without changing report semantics. |
| 15 | Which groups have more than five missed inspections? | A | Missed count, grouped dimension, integer aggregate threshold | Ask which dimension if none is named | Distinguishes a grouped aggregate from source records. |
| 16 | How many forms are submitted but not yet acknowledged by the Department Head? | B | Form lifecycle/status and acknowledgement timestamp exist outside the current V3 measures | GSD must define population, role, and whole-form status policy; versioned report contract | Separates form receipt from schedule completion. |
| 17 | Which eligible assets have no expected schedule because they were deferred or cancelled? | B | Assets, schedules, eligibility, deferral/cancellation evidence | GSD must define eligible denominator and treatment of late registration, deferrals, cancellations, and missing rows | Avoids calling missing schedule rows `missedCount`. |
| 18 | Which assets have repeated non-operational findings across PM cycles? | B | Inspection condition, asset ID, cycles | GSD must define repeat window, same finding rule, and official-history population | Could identify recurring work without inventing repair history. |
| 19 | Which departments repeat late completions across cycles? | B | Completion timestamp, PM-cycle deadline, department | Approve repetition window and historical comparison semantics | Tests longitudinal timeliness. |
| 20 | How many schedules are overdue by 1–7, 8–30, and over 30 days? | B | Schedule deadline and current time | GSD must approve aging buckets, timezone, and boundary behavior; do not reuse Inspector's 30-day assignment clock | Measures operational aging without mixing deadline bases. |
| 21 | Which cycles will reach their PM deadline in the next seven days? | B | PM cycle deadline, current date | Approve forecast window, timezone, and treatment of active/future cycles | Could support a due-soon view. |
| 22 | Which water-station findings require filter replacement? | B | Water-station category details and recorded inspection findings may need a new typed measure or filter | GSD must approve field definition and checklist provenance | Could make category-specific findings usable without inferring repairs. |
| 23 | Which inspection records are missing required photo or location evidence? | B | Inspection/photo/location evidence fields | Approve required evidence rules by workflow and role | Supports audit completeness. |
| 24 | Which schedules are awaiting deferral review? | B | Schedule and deferral records | GSD must define review state and permitted actors | Supports schedule governance. |
| 25 | Which Inspectors have the largest current workload? | B | Assignment data exists, but workload denominator and open/closed rules are not a current V3 measure | GSD must define workload, ownership, reassignment, and date window | Could identify assignment imbalance. |
| 26 | What percentage of cycles had a recorded non-operational finding? | B | `IsOperational`, eligible schedule population | Approve numerator, denominator, exclusions, and behavior for empty or active periods | Avoids a misleading denominator or treating an unmeasurable rate as zero. |
| 27 | How did department compliance change year over year? | B | Current schedules and outcomes; historical organizational snapshots may be absent | Approve comparison formula, stable department identity, population rules, and snapshots | Could support trend research if historical scope is sound. |
| 28 | Compare the same departments over several years. | B | PM outcomes and historical department/building identities | Approve time range, identity continuity, and missing-cycle treatment | Tests longitudinal reporting beyond current single-period contract. |
| 29 | Which acknowledged findings remain pending corrective-action handoff? | B | Inspection and handoff preparation state | GSD must approve acknowledgement and handoff lifecycle semantics | Separates recorded findings from later repair completion. |
| 30 | Was the repair for work order WMS-204 completed, and how long did it take? | C | Authoritative WMS work-order status and timestamps are not in UniPM | WMS owner approval, stable cross-system key, access and provenance rules | Connects inspection finding to authoritative repair outcome. |
| 31 | What are the top root causes and repair costs for late inspections? | C | WMS root cause, financial cost, and linked repair records | WMS/Finance approval and verified source linkage | Supports causal and cost analysis only with external evidence. |
| 32 | Which assets are likely to fail next year? | C | Longitudinal labeled outcomes and validated predictive inputs are not available | Separate research approval, data stewardship, validation protocol, and human-use limits | Potential future predictive study; outside the current report contract. |
| 33 | Count missing expected schedules as missed cycles. | D | Absence is not a persisted missed schedule | Do not implement; clarify or reject | Prevents false missed counts. |
| 34 | Treat a Draft or Submitted form as official acknowledged history. | D | Form lifecycle distinguishes Draft, Submitted, and Acknowledged | Do not implement; clarify or reject | Protects official-history semantics. |
| 35 | Treat “needs repair” or “may sira” as a saved non-operational finding. | D | A repair request is not a persisted `IsOperational=false` inspection result | Clarify whether a recorded finding is intended | Prevents inferred findings and repairs. |
| 36 | Join asset inventory and PM execution into one count without a supported report contract. | D | Separate inventory and PM-execution subjects | Reject the cross-subject join | Prevents mixing populations. |
| 37 | Count work as PM-compliant based on its actual inspection date. | D | V3 period uses scheduled PM cycles, not an inspection-date reporting basis | Clarify or reject the unsupported time basis | Prevents deadline and date confusion. |
| 38 | Create an RMRF, approve a repair, or change an asset status from the report answer. | D | No autonomous action path is permitted | Reject; retain human decision and external WMS workflow | Keeps analytics assistive. |
| 39 | Explain the root cause of a repair using an unlinked summary of counts. | D | No source-linked cause evidence | Reject or ask for a source-supported question | Prevents an unsupported causal claim. |
| 40 | Modify the previous report using “make it narrower” without naming the change. | D | No stateful conversational report-edit contract | Clarify the exact supported field; future plan-edit workflow needs approval | Prevents hidden conversation state from changing a report. |

## Explicit gaps and safe boundaries

The inventory leaves these distinctions intact:

- Missing expected schedules are not `missedCount`. Deferrals, cancellations,
  eligibility and late registration need an approved denominator.
- Submitted forms are not acknowledged official history. A completed schedule
  timestamp does not establish Department Head receipt.
- A recorded non-operational finding does not prove WMS repair completion or
  handoff status.
- Repeated findings and repeated late completions need a time window and an
  official-history population policy.
- PM-cycle aging and nearing-deadline windows use the PM deadline. They must not
  reuse the Inspector assignment's separate 30-calendar-day measure.
- Filter replacement, photo/location evidence, deferral review, Inspector
  workload and provenance by role require explicit definitions.
- Non-operational percentages require an approved numerator and denominator.
  Empty populations and active/future cycles must remain unmeasurable where the
  formula says they are unmeasurable.
- Year-over-year deltas and department/building snapshots need stable historical
  identities. Current dimensions do not prove a historical snapshot.
- WMS repair state, costs, root cause, and turnaround need authoritative,
  linked external records. Prediction needs a separate approved study.
- “Modify the previous report” requires a bounded plan-edit workflow. It cannot
  be implemented as unconstrained conversation memory.

## Runtime and cost review

`PmAnalyticsV3QueryService` loads at most 50,001 candidate schedule rows and
rejects reports above the 50,000-row execution limit. It then resolves eligible
cycles and performs grouping, filtering, ordering, and paging in application
memory. The code exposes a 100-row page size and a 200-group chart limit. These
are source-inspected bounds, not load-test results. No 50,000-row latency or
memory measurement was run for this follow-up.

The host can use SQL Server Query Store and representative SQL Server 2019
tests for future query-plan and scale work. Neither was part of this
parser/guard change. No query cache, data warehouse, database index, or SQL
aggregation rewrite was added.

The DeepSeek API client has a 60-second timeout and up to two retries for a
logical request. Its run ledger allows up to 60 logical calls, but the V3
interpreter creates that ledger per scoped interpreter instance. It is not an
application-wide, per-user, or concurrent-request budget. No global provider
rate limiter or per-user concurrency control was verified. These limits remain
a documented operational gap; this change adds no provider traffic or
app-wide behavior.

## Manuscript-ready summary

| State | Defensible statement |
|---|---|
| Implemented | V3 retains its closed typed catalog, validator, role scope, period policy, formulas, limits, and deterministic executor. Numeric threshold parsing now consumes complete tokens, validates grouping and range, and clarifies malformed, negative, fractional-integer, and overflow operands. A bounded fake-model guard rule preserves only explicit catalog-mappable paraphrases; compound or uncertain measure intent clarifies. |
| Experimentally evaluated | Developer-authored synthetic regression tests exercised the deterministic endpoint and mocked DeepSeek proposal path. The fixed existing V3 developer suite and new regression expansion results are recorded separately. No live provider, GSD question, or institutional record was used. |
| Proposed | New report subjects and formulas for schedule coverage gaps, acknowledgement backlog, recurrence, aging, due-soon work, findings percentages, WMS repair, workload, historical snapshots, costs, root cause, and prediction remain proposals. GSD approval and versioned contracts are required where listed. |
| Threats to validity | The examples and fake model outputs are developer-authored, not GSD-approved or held out. No live DeepSeek call, user study, native SQL Server 2019 calculation run, production-like 50,000-row benchmark, or field acceptance was performed. Synthetic parser agreement does not estimate real-user accuracy. |

## Recommendation

Do not freeze this branch for independent GSD evaluation automatically. It is a
candidate for a newly approved pre-GSD freeze only after the owner reviews the
new evidence, confirms the bounded paraphrase policy and numeric behavior, and
accepts the remaining SQL, provider-budget, and GSD-labeling limits. This
document itself does not create a freeze or authorize GSD exposure.
