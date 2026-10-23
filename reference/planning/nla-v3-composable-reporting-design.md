# NLA V3 composable reporting design

Date: 19 October 2026.
Status: reporting correctness, interpretation and React workflow hardening is
committed on the feature branch as `e2f90bba35707a7142b3dccb1afb8eeec6143ad4`.

### Historical verification at `e2f90bba` (TEST-110)

The web production build (including TypeScript), lint, changed-file formatting,
7 focused V3 React tests, OpenAPI generation/drift check and 7 negative contract
checks passed. Backend regression and authorization tests were added but could
not be run because this environment has no .NET SDK. The browser scenario was
added but could not launch because Chromium is absent and the official browser
download returned HTTP 403. Native SQL Server 2019 behavior and runtime API
authorization remain unverified; no SQL test connection is configured. No
DeepSeek request was made. First-release acceptance and deployment remain
unverified. Preview state is carried in signed, stateless, user-bound tokens
that expire after ten minutes; the preview store has no in-memory state to
replicate. The cross-instance, tampering, user-binding, and expiry regression
test is present in source but was not executed in TEST-110 because the .NET SDK
was unavailable. This status does not authorize deployment, release, external
provider calls, database changes or new domain measures.

### Quarterly drill-down follow-up at `b0289382` (TEST-111)

Quarter grouping now uses the floored month-to-quarter calculation. Focused
React regressions cover February/Q1 and November/Q4, including category
selection, quarter period, retained filters, and returned source records. The
.NET 10 build and focused V3 regression/authorization tests passed; the focused
React tests, relevant Web CI checks, and narrow-viewport Playwright scenario
also passed. Native SQL Server 2019 integration remains unverified because no
dedicated SQL test connection is configured. First-release acceptance and
deployment remain unverified. No DeepSeek request was made. See
[TEST-111](../evidence/test-runs/TEST-111-pm-analytics-v3-quarterly-drilldown.md)
for exact commands and results.

## Goal and source baseline

Let GSD ask maintenance questions without knowing internal field names or a
fixed sentence template. The model composes a report from an approved catalog;
the backend validates, authorizes and calculates it. Users review the meaning
before execution and can resolve ambiguity through choices.

Implementation base: `9db2c111910a12906bf2cf264dc46631333b0377` on
`deploy/azure-staging`, checked out as
`feature/nla-v3-composable-reporting`. This records the source baseline only;
it does not establish the deployed version or deployment acceptance.

Implemented on the feature branch: the closed plan contract and versioned
catalog; category-specific period expansion and validation; GSD-only catalog,
interpret, preview and execute endpoints; local asset-value resolution; bounded
read-only execution; signed, stateless, user-bound preview tokens that expire
after ten minutes; deterministic question parsing with an optional
Development-only, separately flagged
DeepSeek Flash path; and a V3 report builder with preview, confirmation, typed output,
pagination and the existing V2 reports retained as a fallback. These are source
changes, not deployment or first-release acceptance evidence.

Relevant sources:

- [V2 contracts](../../server/Features/Reports/PmAnalyticsV2Contracts.cs).
- [V2 validator](../../server/Features/Reports/PmAnalyticsV2PlanValidator.cs).
- [V2 query service](../../server/Features/Reports/PmAnalyticsV2QueryService.cs).
- [Rule interpreter](../../server/Features/Reports/PmAnalyticsV2QuestionInterpreter.cs).
- [Experimental model interpreter](../../server/Features/Reports/PmAnalyticsV2ModelPlanInterpreter.cs).
- [V3 contracts](../../server/Features/Reports/PmAnalyticsV3Contracts.cs).
- [V3 catalog](../../server/Features/Reports/PmAnalyticsV3Catalog.cs).
- [V3 validator](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs).
- [V3 query service](../../server/Features/Reports/PmAnalyticsV3QueryService.cs).
- [V3 question interpreter](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs).
- [V3 preview store](../../server/Features/Reports/PmAnalyticsV3PreviewStore.cs).
- [V3 report builder](../../web/src/features/reports/pm-analytics-v3.tsx).
- [Confirmed workflow](confirmed-gsd-workflow.md).

V2 currently permits one measure/category/grouping field, monthly, quarterly
and annual periods, bounded ranges, outcome filters, limited department ranking
and two-cycle comparisons. Its model interpreter is Development-only; the V2
question endpoint uses deterministic grammar. V3 must not silently enable that
experimental interpreter in Production.

The V3 report-plan version is separate from the existing V3 English evaluation
corpus. Use distinct protocol/dataset identifiers and do not overwrite or retune
historical corpora, scores or evidence.

## First release

Support one subject, `pmExecution`, whose canonical row is one existing eligible
schedule for one asset and PM cycle. Extend verified V2 query primitives instead
of introducing a general SQL agent or another authentication/provider system.

| Capability | Proposed first-release boundary |
|---|---|
| Measures | Up to four requested compatible measures, plus backend provenance counts |
| Categories | Explicit nonempty selection from the four scoped CPMP categories |
| Period | Explicit month, quarter, year or inclusive range up to 12 calendar months |
| Population | Live completion initially; official-history reports remain separate |
| Filters | Flat AND, with typed `in` for multiple values on one field |
| Grouping | Zero, one or two compatible dimensions |
| Result filters | Comparisons against approved calculated measures |
| Ordering | Up to two approved fields plus a deterministic tie-break |
| Ranking | Optional top/bottom selection, maximum 20 groups |
| Results | Aggregate table or paginated matching records |
| Presentation | Typed table; summary for an ungrouped aggregate |
| Language | English evaluation scope; no multilingual quality claim |

Do not add charts, arbitrary expressions, cross-subject joins, nested Boolean
trees, conversational memory, writes or portal integration in this release.
Existing V2 comparisons remain available through V2. Inventory is the next
subject; batches, forms and inspection follow-up come afterward.

## Meaning of the data

These definitions preserve existing V2 behavior. Any discovered disagreement
between these definitions and source must be resolved before implementation.

| Identifier | Definition |
|---|---|
| `scheduledCount` | Count of eligible persisted asset-cycle rows; cancelled schedules excluded |
| `completedCount` | Eligible rows with authoritative inspection completion timestamp |
| `completedOnTimeCount` | Completed rows whose completion is no later than their deadline |
| `completedLateCount` | Completed rows whose completion is after their deadline |
| `nonOperationalCount` | Completed rows whose recorded condition is non-operational |
| `remainingCount` | Incomplete rows in an active cycle, including its deadline instant |
| `missedCount` | Incomplete rows whose deadline has passed |
| `progressPercent` | Completed count divided by eligible scheduled count, multiplied by 100 |
| `onTimeCompliancePercent` | On-time count divided by eligible scheduled count, multiplied by 100; unmeasurable if any relevant selected cycle is active/future |
| `distinctAssetCount` | Unique asset IDs in the stated eligible or matching population, labeled explicitly |

The non-operational count is an inspection finding per asset-cycle, not a
current inventory-condition claim. Do not sum distinct-asset counts across
cycles. Do not label workload counts as personnel performance.

Time basis is scheduled PM cycle for the first release. Deadlines and cycle
boundaries use Asia/Manila. Requests based on inspection dates must be explained
as outside the initial time-basis capability, not silently treated as PM cycles.
Resolve relative dates against Asia/Manila and show the resulting absolute
period in the preview.

Expand calendar periods into valid CPMP cycles separately for each selected
category. Grouping by quarter does not create extra scheduled months. Report
when a requested category has no applicable cycles rather than silently dropping
it. Preserve existing supported-date bounds and explicit legacy-cycle fallback.

Use completion timestamps, not form submission or acknowledgement, for execution
and timeliness. Draft/Submitted inspection rows remain part of the live view.
Official history remains acknowledged-only. Never mix populations without an
explicit, separately defined report subject/population.

Department and building describe current Asset fields. The preview and results
must disclose this for historical periods; no historical-location snapshots are
invented. Missing schedule rows do not become missed inspections or generated
denominators. Distinguish no data from known incomplete coverage; do not claim
complete CPMP coverage without an independently established expected population.

## Catalog and field permissions

Keep a small code-first catalog versioned alongside C# query definitions. Do
not create database-managed metric registration or expose every EF property.

| Field group | Initial fields | Allowed uses |
|---|---|---|
| Context | Department, building, asset category | Filter, group, output, sort |
| Asset identity | Asset ID/code, general location | Record output; bounded identity filters |
| Time | PM cycle; derived calendar quarter/year | Period selection, grouping, output, sort |
| Execution | Deadline, completion timestamp, condition, timeliness | Record output; approved matching filters |
| Workflow | Schedule/form status | Record output; only catalogued matching filters |
| Measures | Named measures above | Aggregate output, compatible sorting/thresholds |

Each entry specifies stable ID, label, type, null semantics, permitted operators,
grain, population, unit, aliases and implementation binding. No model-provided
SQL identifiers or arbitrary join paths. All categories and context values must
resolve locally to authorized canonical values. Do not invent a department when
the user's name has no match. Unknown and ambiguous values receive distinct
responses with bounded choices where appropriate.

Exclude signatures, signatory details, security fields, embeddings, precise
coordinates, photo content and unrestricted narratives from the model catalog.
The four supported categories are fixed; category synonyms are curated aliases,
not evidence of formally approved institutional terminology.

## Proposed report contract

The fields below are conceptual names, not existing endpoint contracts.

```text
ReportPlan
  schemaVersion, catalogVersion
  subject: pmExecution
  mode: aggregate | records
  period: basis pmCycle; explicit calendar scope
  categories: approved category IDs
  baseFilters: department/building/asset identity selections
  matchingFilters: approved outcome/workflow conditions
  measures: approved measure IDs
  groupBy: approved dimensions
  aggregateFilters: measure + typed operator + numeric value
  orderBy: approved result fields + direction
  top: optional bounded ranking
  presentation: table | summary; approved ordered column IDs
```

Aggregate and record modes have different legal combinations. Records mode has
no group thresholds or rankings in the first release. A summary requires an
ungrouped aggregate. Unknown fields, illegal combinations, wrong types and
unsupported operations fail validation. Never silently strip requested clauses.

Separate population filters from matching filters. Department, building,
category and period define the eligible population. Outcome predicates select
matching records and must not shrink a percentage denominator. Initially reject
percentage plans with explicit outcome filters, as V2 does, and offer a count or
record report instead. Broader conditional rates require separately defined
measures, not an implicit change of denominator.

Each requested measure has its own predicate and matching provenance. A report
containing both late and non-operational counts must not imply both measures
share one matching-record set. Drill-down names the measure and retains the
confirmed base scope. Calculation records remain separately available.

Logical aggregate execution order:

```text
Authorization -> canonical rows -> base filters -> per-measure aggregation
-> measurability -> aggregate filters -> sort -> optional top N -> pagination
```

The backend calculates over all eligible rows before returning a page. It
provides typed column metadata, values, units, null reasons, population counts,
as-of time, scope notes and source identifiers. Count groups before/after
thresholds and disclose top-N truncation. Adding/removing a display column must
not change grouping or calculations.

Use underlying integer counts/decimal arithmetic for thresholds. Exactly 100%
means a positive denominator and numerator equal to denominator, not a rounded
display value. A zero denominator is unmeasurable. If a threshold references an
unmeasurable metric, do not silently exclude that group and claim no matches;
return explicit unmeasurable groups/reasons and explain why the requested report
cannot be fully decided. Other measurable columns may still be displayed.

## Interpretation and confirmation flow

```text
Question -> candidate -> structural/semantic/access validation
  ambiguous -> choices -> retained draft -> validate again
  unsupported -> specific explanation and explicit alternatives
  valid -> readable preview -> confirm -> reauthorize/revalidate -> execute
  provider unavailable -> retry/manual reporting option, no invented result
```

Build preview text from the normalized plan and trusted catalog templates, not
an independent model-written explanation. Show subject, population, categories,
absolute period/time basis, measures, grouping, thresholds and output columns.
Expose details progressively so normal reports do not become a technical form.

An unmeasurable result is different from unclear intent. The 2026 annual
compliance request is meaningful but not final before November closes. Explain
that limitation; allow the user to retain the request or explicitly choose
another supported measure/period. Do not silently change its meaning.

Do not offer closed-cycle-to-date compliance as implemented or approved. Its
formula, label and population need separate GSD/domain approval. Preserve other
resolved fields through a clarification, but revalidate dependent thresholds and
units if a measure changes. A percent threshold cannot become a count threshold
without review.

Preview binds the normalized plan to authenticated user, catalog/schema version
and expiry. A changed interpretation needs a new preview. Recheck authorization
and catalog compatibility at execution. Provider output cannot authorize itself.
Execution never calls the LLM again to change the confirmed plan. Results are
live at execution time, not a preview-time database snapshot.

Choose the smallest confirmation mechanism after checking current deployment
replicas/restarts and existing cryptographic/session facilities. An opaque
server-side preview or protected stateless envelope are alternatives; neither
requires new persistent business tables by default. Expired or unavailable
previews should request a new preview, never silently reinterpret.

## First acceptance examples

| Question | Expected behavior |
|---|---|
| Which departments had 100% fire-extinguisher compliance in 2025? | Annual on-time compliance grouped by department, positive denominator and exact threshold |
| Show departments below 80% compliance in 2025 | Clarify missing category selection; offer explicit categories/all four |
| Show scheduled, completed and missed fire-extinguisher inspections in 2025 by department | Three measures over one eligible population |
| Show non-operational findings in 2025 by department and PM cycle | Clarify category selection if absent; two dimensions |
| Show 100% compliance for fire extinguishers in 2026 | Preserve annual meaning; explain active/future-cycle measurability |
| Show late and non-operational findings | Clarify period/category and whether separate counts or records meeting both conditions |
| Show fire-extinguisher compliance for inspections completed in 2025 | Explain unsupported completion-date basis; do not reinterpret as scheduled year |
| Give 100% compliance where nothing was scheduled | Explicit unmeasurable result, not 100% |
| Assign all missed inspections to someone | Reject mutation; reporting remains read-only |

An explicit list/category count comparison can include all four categories with
their own applicable cycles. Counts and percentages across categories must show
their eligible denominators so unlike inspection frequencies are visible.

## Safety and operational boundaries

GSD-only reporting initially. Existing authorization remains authoritative for
catalog, interpretation, preview, execution and source drill-down. Protect direct
API requests independently of UI visibility. No new Supervisor access by default.

Send sanitized questions and safe catalog metadata to a provider. Resolve actual
catalog values locally; do not send database rows or personnel narratives for
interpretation. Provider errors, refusal and malformed plans are distinct from
Unsupported intent. Use an explicit configured provider/model and bounded calls;
no silent provider fallback or automatic repair loop. Add feature/cost controls
without altering core PMIS availability.

Construct EF queries from approved bindings. Test provider translation against
disposable native SQL Server; do not infer correctness from InMemory tests.
No model-generated SQL, C#, LINQ strings, HTML, JavaScript or custom formulas.
Canonical projections, existence checks or pre-aggregation must prevent fan-out.
One form's multiple inspections and referral audit history are real cardinality
hazards. Current inspection photo evidence is singular, not a photo collection.

Bound filters, groups, periods, pages, source size and execution time on the
server. Final caps/timeouts require measurement on the small staging tier.
Cancellation and limit violations must return clear failures, not partial
results presented as complete. No migrations, seed edits, live Azure calls or
new provider calls are authorized by this planning task.

## Full delivery roadmap

Phase 0 selected the source base and dedicated feature branch. The first-release
implementation work for Phases 1-4 is present in source; their independent
verification and completion criteria remain pending. Phase 0 deployment
evidence and Phases 5-11 remain pending. This roadmap defines intended work and
completion criteria; it does not authorize provider spending, held-out runs,
database changes, merge or deployment. Sequence is dependency-based, not a dated
delivery promise. Do not expand the first release to include every later phase.

### Phase 0: establish the implementation baseline

- Reconcile main, the deployment branch and the required V2 foundation. Select an
  exact base SHA and a dedicated feature branch without replacing working PMIS
  behavior or losing deployment safeguards.
- Inventory existing queries, provider adapters, UI components, authorization,
  evaluation protocols and evidence. Mark reusable components and known gaps.
- Record the deployed version and rollback path. Separate source readiness from
  deployed acceptance and retain the current reporting route as fallback.

Exit: documented base, scoped change list and explicit acceptance examples. No
unrelated Admin/account-management work is bundled into NLA.

### Phase 1: freeze the first contract and semantic catalog

The versioned plan, code-first measure/dimension/filter catalog, period and
clause validation, typed errors, preview and execution contracts are implemented.
The catalog is exposed to the UI, including allowed filter values and current
asset-value lookup. Independent fixture calculations and SQL-backed contract
verification remain open, so this is not a frozen acceptance contract.

- Write exact request/response DTO examples and the field compatibility matrix
  for PM execution. Define aggregate versus records mode, multiple measures,
  categories, grouping, thresholds, order and output-column legality.
- Define nulls, measurability, current location, category-specific cycle expansion,
  relative dates, denominators, source scopes and missing-coverage disclosures.
- Specify the confirmation state transitions, preview binding/expiry, error codes
  and rule for invalidating a preview after edits.
- Create hand-reviewed fixture calculations for the 2025 threshold example,
  active-year compliance, mixed-category counts and zero denominators.

Exit: each acceptance example has a normalized plan or explicit clarification,
unsupported or unmeasurable outcome. Calculations are independently specified.
Unresolved future measures are excluded rather than assigned invented formulas.

### Phase 2: deterministic PM execution engine

The read-only executor now calculates the approved PM execution measures over a
shared eligible population, applies matching and aggregate filters, groups on
up to two dimensions, sorts, ranks and paginates. It returns per-measure
provenance, population counts, category-specific cycles, as-of time and scope
notes. Build success does not verify SQL translation or expected results.

- Reuse V2 calculations through approved query primitives and cardinality-safe
  projections. Add compatible measures, two-dimensional grouping, aggregate
  thresholds, stable sorting, ranking and pagination.
- Return typed columns, per-measure provenance, population counts, excluded or
  unmeasurable groups, as-of time and scope notes.
- Enforce authorization and complexity bounds. Add cancellation and clear
  timeout/limit behavior. Keep all queries read-only.
- Verify direct typed requests without an LLM against disposable native SQL
  Server and independently reviewed expected results.

Exit: the closed-year 100% report and all first-release calculation cases work
without provider calls. No fan-out, denominator or pagination discrepancy remains.
Existing V2 contracts and comparisons continue to work.

### Phase 3: report preview and typed presentation

The analytics page now presents the V3 builder, catalog-driven filters, typed
preview, explicit confirmation, result tables and pagination. Existing V2
reporting remains available below the V3 builder. Users can request matching
records as a separate report; metric-linked source drill-down and calculation
source views are not implemented. Keyboard, responsive-screen and browser
acceptance review remains pending.

- Add the plan preview, edit controls, confirmation and execution states using
  existing React components and typed API clients.
- Render table columns from approved typed metadata. Add ungrouped summary and
  metric definitions. Metric-linked matching records and calculation sources
  remain outstanding.
- Handle empty data, partial measurability, stale/expired previews, failed
  execution and cancellation without showing old results as current.
- Keep the existing dashboard flow available. Review keyboard use and desktop
  screenshots at 1280x720, 1440x900 and 1920x1080.

Exit: a user can inspect and confirm a typed report, understand its population,
and inspect evidence without understanding the JSON contract.

### Phase 4: model interpretation and clarification

The default interpreter is deterministic. Recognized question scope is retained
in a draft when the category, period or measure needs clarification; local
catalog suggestions support population-filter choices. The optional DeepSeek
Flash path requires Development mode, `NaturalLanguageAnalytics:V3DeepSeekEnabled`
and `DEEPSEEK_API_KEY`; the feature flag defaults off. No provider was called
during this implementation.

- Use the existing DeepSeek Flash client (`deepseek-flash`) to produce the new
  typed contract from safe catalog definitions and sanitized input. Keep the
  provider fixed, with an explicit feature flag, API-key requirement, budget
  and bounded call count.
- Resolve catalog values locally and validate model output before preview.
  Generate preview wording from the normalized plan.
- Offer bounded clarification choices, preserve resolved fields and revalidate
  dependent measures/thresholds. Separate provider errors from intent rejection.
- Verify that confirmation executes the same meaning without another model call.
  Preserve manual reporting when the model is disabled or unavailable.

Exit: development questions yield correct plans or appropriate clarification and
rejection, with no unauthorized executable output. This is development evidence,
not a held-out accuracy or production-language claim. No question corpus or
provider-quality acceptance has been run for this implementation.

### Phase 5: first-release evaluation and controlled acceptance

**Preparation status (2026-10-19):** A protocol outline and provisional English
development question set have been drafted in
[`reference/evaluation/pm-analytics-v3/`](../evaluation/pm-analytics-v3/README.md).
They are not independently reviewed, frozen, or evaluated. Held-out questions
have not been created or accessed for this phase. The acceptance exit criteria
below remain open.

**Engineering follow-up (2026-10-19):** A V3-only corpus validator and
deterministic diagnostic/scoring gate, pending-review labels, and synthetic
calculation fixtures have been added on the Phase 5 work branch. All 18
provisional labels remain pending, so no question accuracy score or acceptance
claim is available. Diagnostics surfaced two material candidate-label
differences (the `or` filter and “not late” questions); both need reviewer
adjudication. SQL Server 2019 and GSD task acceptance remain open.

- Create a separately identified protocol and reviewed question set. Separate
  V2/V3 shared tasks from newly supported tasks and include unsafe, ambiguous,
  unmeasurable, missing-data and provider-failure cases.
- Review development results, cost and latency. Fix in scope, then freeze source,
  binaries/build identity, catalog, provider/model, prompt, corpus hash and split.
- Obtain separate authorization before held-out evaluation. Preserve historical
  evaluation sets and provenance limitations. Never tune against held-out cases.
- Run the authorized SQL, role/security, regression and browser acceptance checks.
  Collect GSD task success, clarification burden and correction of wrong previews.
- Prepare a release record listing exact results, skipped checks and limitations.

Exit: first-release acceptance is supported by recorded evidence, not assumed
because the feature handles more question types. Any unsafe executable result
prevents describing the provider as deployment-qualified. Actual release remains
a separate authorization step.

### Phase 6: asset inventory reporting

**Engineering status (2026-10-20):** The bounded inventory subject is
implemented on the isolated `feature/nla-v3-asset-inventory` branch at code
commit `b2c40f2a39c0e1b326a612cbdf09f77ea6555459`. The follow-up backend, Web,
API-contract, and browser verification is recorded in
[TEST-117](../evidence/test-runs/TEST-117-nla-v3-phase6-final-verification.md).
An exact-head backend and evaluation follow-up at `a9331b2b2cdd04cbf2ecf27d3b08c85dbb6639f0`
is recorded in
[TEST-118](../evidence/test-runs/TEST-118-nla-v3-phase6-exact-head-follow-up.md);
it confirms the current code remains unchanged and records the 18 pending
labels without scoring them. The earlier
[TEST-114](../evidence/test-runs/TEST-114-pm-analytics-v3-asset-inventory.md)
remains the initial implementation evidence. Native SQL Server 2019 is still
unverified. Phase 5 remains open: all 18 provisional interpretation labels
still need independent human review, and no score or GSD acceptance claim is
available.

**Offline/mobile integration verification (2026-10-20):** The asset inventory
subject is included in the isolated `feature/offline-nla-v3-integration`
candidate with the offline inspection and mobile acknowledgement/home UX
histories. The backend and Web integration corrections and local regression
results are recorded in
[TEST-122](../evidence/test-runs/TEST-122-offline-nla-integration-candidate-verification.md).
Native SQL Server 2019 remains unverified because the disposable image registry
request was denied, and GSD acceptance remains open. Backend CI #370, Web CI
#283, and Flutter CI #50 passed on integration source commit `a93e71b`; see
[TEST-123](../evidence/test-runs/TEST-123-offline-nla-integration-ci-verification.md).
This does not implement Phase 7 or close Phase 5 review gates.

The subject design, including grain, counts, filters, tests and dependencies,
is in
[`nla-v3-phase6-asset-inventory-design.md`](nla-v3-phase6-asset-inventory-design.md).

- Add a subject whose row is one registered asset. Support category, department,
  building, general location and registration-status filters and counts.
- Answer questions such as active assets per building and category. Do not
  require a PM period for an inventory question.
- Separate registration status from latest recorded inspection condition. Any
  latest-condition view must define its timestamp, ties and missing inspections.

Exit: inventory counts agree with authorized registry results and never count
one asset once per historical schedule. Extend protocol coverage independently.

### Phase 7: batches and assignment coverage

**Status:** planning only. The implementation requirements and open semantic
decisions are in
[`nla-v3-phase7-batch-assignment-design.md`](nla-v3-phase7-batch-assignment-design.md).
Resolve those decisions before adding the subject; Phase 7 is not part of the
current implementation branch.

- Add a batch subject keyed by department, category and PM cycle. Building does
  not split a batch. Distinguish batch counts from asset-cycle counts.
- Support unassigned Supervisor/Inspector batches, pending assigned work and
  bounded workload summaries. Define how partially completed or inconsistent
  batches are represented.
- Show current versus historically recorded assignment meaning explicitly.
  Missing assignment history cannot establish past personnel responsibility.
- Keep local personnel resolution and existing role permissions; this phase does
  not grant the model access to unrestricted staff information or assignment writes.

Exit: batch counts and eligibility match the operational assignment workflow.
Reports cannot silently assign work or present workload as employee performance.

### Phase 8: forms and inspection follow-up

The bounded requirements and dependencies are listed in the Phase 6 design's
[Phase 8 planning notes](nla-v3-phase6-asset-inventory-design.md#phase-8-forms-and-follow-up-reporting).

- Add form reports at one row per form, with Draft, Submitted and Acknowledged
  states. Define ageing from an explicit event timestamp and a fixed as-of time.
- Report awaiting acknowledgement and incomplete form workflows without treating
  acknowledgement as inspection completion.
- Add follow-up reports for non-operational inspections with or without a recorded
  external PM number. Deduplicate referral audits correctly.
- Keep external WMS repair processing, completion and turnaround metrics outside
  scope unless authoritative lifecycle data and approved definitions become available.

Exit: form counts are unaffected by their number of inspection rows. Follow-up
reports describe recorded handoff/reference information only. Signatures and
signatory details remain excluded from remote interpretation.

### Phase 9: richer analysis and visualization

- Add new-engine comparisons for approved periods and grouped changes. Reconcile
  populations, expose count differences and distinguish percentage-point changes
  from relative percentage changes.
- Add ordered time-series buckets, recurrence counts and top/bottom comparisons.
  Recurrence describes recorded findings, not failure causes or repair outcomes.
- Add bounded bar/line chart mappings using an existing suitable library if
  available. Validate field types, units, nulls and temporal axes. Tables remain
  the accessible alternative and numerical source of truth.
- Saved reports / My reports: implemented on `feature/nla-saved-reports` on
  October 23, 2026. Users can explicitly save reviewed report definitions,
  open new previews, edit, rename or delete them. Reopening uses backend plan
  validation without another AI interpretation call. TEST-148 records a
  targeted InMemory backend check; TEST-149 records TypeScript checking.
  TEST-150 records a passing Chromium workflow with controlled API responses.
  The migration remains unapplied and combined browser/native SQL verification
  is pending. See [usage and setup](../guides/how-to/use-saved-reports.md).
  - Save the report settings, not a frozen result set. Each Run or Refresh
    calculates fresh results from currently authorized records.
  - Support clearly labeled fixed periods, such as 2025, and relative periods,
    such as this month or last year. Resolve relative periods on each run and
    display the actual scope before confirmation.
  - Recheck ownership/access, current permissions, catalog/schema compatibility
    and plan validity before execution. Preserve preview and confirmation.
  - Do not automatically learn questions, add parser rules or approve reusable
    interpretations. Persist no unsafe prompts, provider payloads or secrets.
  - Automatic refresh while open, scheduled execution and shared saved reports
    remain separate scope decisions. This follow-up does not block the current
    clarification correction or change existing evaluation results.

Exit: each new analysis has independent expectations and labels that disclose
population changes. Charts match the returned table and execute no generated code.

### Phase 10: optional domain and institutional extensions

These are separate decisions, not prerequisites for the first release:

- GSD-approved compliance across closed cycles to date, including its distinct
  name, eligibility population and treatment of incomplete coverage.
- Completion-date reports distinct from scheduled-cycle reports.
- Acknowledged-history reporting subjects with approved publication semantics.
- Historical location/assignment snapshots if the university needs such analysis;
  current fields cannot be relabeled as historical facts.
- Additional languages after human label review and separately reported evaluation.
- Supervisor access only after approved record-level permissions and privacy review.

Exit: each adopted extension has an explicit domain decision, data sufficiency,
privacy boundary, migration assessment if necessary, and its own verification.
Do not promise unsupported historical facts or future institutional integration.

### Phase 11: release, manuscript and maintenance

- Release only an explicitly scoped, verified subset. Check exact-head required
  CI, configuration, provider limits, secrets, migration needs and rollback before
  requesting or acting on release authorization.
- Deploy to controlled staging first when authorized. Verify live authentication,
  report correctness, source drill-down, provider failure behavior and configured
  limits. Local tests do not establish Azure acceptance.
- Retain the existing dashboard/manual path and a way to disable model
  interpretation without disabling PMIS. Record rollback and operator procedures.
- Reconcile title/objectives/scope/methodology/architecture/evaluation in the
  actual current manuscript workspace. Report completed scope and measured results
  separately from roadmap items. Preserve AI-use and synthetic-data disclosures.
- After feedback, update catalog versions and regression examples. Requalify
  provider/model/prompt changes rather than assuming prior results transfer.
- Retire V2 routes only through a separately reviewed compatibility decision after
  client migration and acceptance; never as an incidental V3 cleanup.

Exit: released capabilities, evidence, operator procedure and manuscript agree.
Unreleased subjects and institutional integrations remain clearly marked pending.

### Release cuts and dependencies

| Release cut | Included phases/capabilities | What it does not imply |
|---|---|---|
| Core V3 | Phases 0-5, PM execution tables and question-to-plan flow | Inventory, assignments, forms and charts are not required |
| Operational reporting expansion | Phases 6-8, each independently scoped | No arbitrary cross-subject joins or WMS lifecycle monitoring |
| Advanced reporting | Phase 9 after relevant subjects are stable | No forecasting, diagnosis, personnel scoring or generated formulas |
| Institutional extensions | Individually approved items from Phase 10 | No automatic language/RBAC/domain expansion |

Phase 11 release/evidence/manuscript practices apply to every release cut, not
only the end of the roadmap. Any added capability returns through semantic
definition, deterministic verification, interpretation evaluation and acceptance.

Use atomic commits for catalog/contracts, execution, security/provider work,
frontend, tests and evidence where they form separate logical changes. Do not
batch unrelated Admin work or historical evidence rewrites into these phases.

## Verification design

Planned only. Implementation tasks must explicitly authorize test additions and
the verification budget under AGENTS.md.

- Fixed-clock fixture oracle covers closed/open/future cycles, zero schedules,
  late deadlines, missing coverage, legacy fallback and category-specific months.
- Hand-reviewed count/rate expectations cover multi-measure, mixed categories,
  two dimensions, exact unrounded thresholds, nulls and current-location limits.
- Native SQL cases cover translation, real one-to-many relationships, stable
  pagination and unchanged denominators after matching/aggregate filters.
- Authorization/preview cases cover tampering, other users, expiry, role changes,
  changed catalog, canceled preview and direct execution attempts.
- Provider cases cover wrong supported fields as well as malformed JSON,
  injection, refusals, timeout and quota failure. Valid JSON can still mean the
  wrong report.
- UI checks cover keyboard/choice flow, disclosure of assumptions, dynamic
  columns, per-measure evidence, empty/unmeasurable/error states and desktop
  screenshots at 1280x720, 1440x900 and 1920x1080.
- Evaluate shared V2/V3 tasks separately from newly supported tasks. Report plan
  accuracy, unsafe executable output, calculation/presentation correctness,
  clarification turns, user correction of wrong previews, task success, latency
  and cost. Do not score a correctly rejected unsupported question as an error.
- Freeze source, catalog, provider/model, prompt, corpus hash and protocol before
  approved held-out execution. Keep historical corpora and provenance caveats.

## Decisions still to finalize

- Confirmation mechanism and expiry under the actual hosting topology.
- Exact typed filter/result DTOs and field compatibility matrix.
- Provider/model and budget after development evaluation; no model qualification
  follows from this design.
- Measured query caps, timeouts and acceptable latency.
- Independent label/fixture reviewers and evaluation protocol identifiers.
- Any future compliance-to-date definition requires domain approval. It does
  not block the initial release preserving existing compliance semantics.

## Manuscript and next work item

Describe a planned semantic-catalog-grounded interpretation layer with validated
structured plans and deterministic reporting. Do not call it an implemented RAG
system, novel algorithm or evaluated language capability. Confirm the current
manuscript revision in its separate workspace before editing it.

Next bounded implementation-planning deliverable: exact catalog/DTO examples,
field compatibility matrix and independent expected calculations for the first
closed-year threshold report. No production implementation is claimed here.
