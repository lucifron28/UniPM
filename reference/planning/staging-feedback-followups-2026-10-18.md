# Staging feedback and follow-up work

Recorded 2026-10-18 against branch `feature/staging-workflow-followups`, based
on HEAD `d474cb4725ffce031a7c903cf2201d7afe2427ab` with uncommitted changes.
The deployed application package was built from
`88428443e2a907bb2bc797f8bfa561ef9ffb96e4`.

This is a working follow-up record. Status entries distinguish local source
implementation from source inspection, verification gaps, and live/deployed
acceptance. No live Azure settings or database were changed; local source and
operator-template changes are described in their respective entries.

## 1. NLA requires too much knowledge of the supported schema

**Confirmed usability issue.** The owner found that ordinary questions are hard
to formulate successfully. Staging rejected this reasonable request:

> show me the non-operational water drinking station in 2025 for all the departments

The response only said that the question was outside supported measures or time
scopes. It did not identify which part failed or help the user complete it.

**Source-inspected root cause (2026-10-18).** The V2 interpret endpoint uses a
deterministic grammar. Its existing contract and tests already support annual
periods, non-operational results, and department grouping. The exact wording
failed because `me` and `all` were left as unrecognized tokens, and grouping
accepted `by`, `across`, or `grouped by departments` but not `for all the
departments`. This identifies a wording-coverage gap, not a missing metric or
annual calculation capability. It is source evidence; the deployed image was
not inspected during this fix.

**Local fix.** The grammar now accepts `me` and `all` as connective wording
and maps `for all departments` / `for all the departments` to the existing
Department grouping. The metric schema, authorized route, validation, and
deterministic calculation are unchanged. A focused regression asserts that the
sample produces a 2025 Water Drinking Station NonOperational result grouped by
Department. The focused regression passed 1/1 on the uncommitted working tree.

The current question view has clickable examples, a clear interpreted-scope
summary, and separate matching-source and calculation-source sections. These
improve guidance and auditability without expanding the analytics contract.
Broader language coverage and live deployment acceptance still require
separate evaluation. Preserve deterministic calculations, GSD authorization,
provider privacy boundaries, frozen corpora, and historical experiment results.
Do not add calculation scope through wording changes.

## 2. Make annual CPMP scheduling automatic and present schedules by batch

**Confirmed configuration and usability concern.** The current-year generation
service runs when an eligible asset is registered, and `EnsureYearAsync` groups
missing cycles by Department + category + PM cycle. It considers the CPMP months
from the asset's registration month for new assets. With no GSD-approved
`ScheduleGeneration:EffectiveDate`, it creates cycles for the current and later
months and reports earlier missing cycles for coverage review instead of
backdating them. The hosted recovery worker checks immediately at process start
and then daily while the process remains alive. The previously inspected staging
configuration disabled it with
`PreventiveMaintenanceScheduleGeneration__WorkerEnabled=false`; Azure was not
rechecked in this session. Because the app may scale to zero, the worker is not
a continuous scheduler. The prominent manual Add schedule action still makes
the automatic behavior unclear.

**Owner-confirmed requirement.** On entering a new institutional calendar year,
generate all applicable CPMP cycles for eligible existing assets without an
operator manually creating each schedule. For example, 2027 must include
February, May, August, and November for fire extinguishers and water drinking
stations, and June and December for fire alarms and emergency lights. Future
cycles in that year must already be visible, not created only when each month
arrives. New assets receive eligible upcoming cycles without inventing prior
obligations. Generation must recover after downtime and remain idempotent.

**Implementation/configuration finding (source-inspected 2026-10-18).** The
owner approved automatic current-year CPMP generation. Existing
`EnsureYearAsync` logic includes every CPMP month at the start of the current
year for assets registered before that year. Later in a year it creates only
current and future cycles; earlier missing cycles are reported for coverage
review unless GSD approves an effective date. New assets start at their
registration month. The Container Apps guide, App Service guide, and staging
environment template now set
`PreventiveMaintenanceScheduleGeneration__WorkerEnabled=true`. The hosted
worker checks at process start and daily while the process is running. With
scale-to-zero, it will not run while the app is stopped and waits for the next
request to start the app. No live setting was checked or changed. The worker
only targets the current institutional year; it does not pre-create next
year's schedules in advance.

The primary management unit is **Department + Asset category + PM cycle**.
Building does not split a batch. Replace the asset-by-asset primary listing with
batch rows showing department, category, PM month, month-end deadline,
inspected/scheduled progress, and assigned personnel. Open a batch to inspect
its individual asset schedules and use existing batch assignment controls.
Retain per-asset records internally for eligibility, completion, timeliness,
and submission gating; do not replace the underlying tracking with one shared
completion flag. Keep batch execution progress separate from form lifecycle.

The Schedules registry no longer promotes per-asset **Add schedule** as a
routine action. Its page description now identifies CPMP-generated cycles, and
GSD retains the separate management link for checking or generating missing
current-year cycles. The existing direct creation route and API contract remain
unchanged; they are not part of the default scheduling path. Manual cycle
adjustment controls remain pending GSD authority and audit requirements.

The CPMP manual supplies frequencies and departmental coordination; the exact
software batch key comes from the confirmed UniPM workflow. Do not attribute
that database design directly to the manual.

Manual adjustment/editing remains an exception with a recorded reason and audit
history, not the normal way to establish annual schedules. GSD still needs to
confirm who may adjust cycles and which changes are permitted before those
controls are implemented. An adjustment must not change CPMP frequencies.

Required sequence:

1. Perform a read-only impact audit before enabling the worker in the live
   environment. List the exact missing asset/cycle pairs, existing schedules
   that will be retained, skipped cycles, enrollment deferrals, and cycles
   requiring GSD coverage review.
2. Confirm the scheduling effective date with GSD before historical catch-up.
   Do not invent past obligations or backdate records.
3. Review staging hosting behavior as well as application behavior. The app can
   scale to zero, so an in-process worker is not a continuously running scheduler.
   Verify recovery after startup before choosing the operating procedure.
4. Verify the enabled worker in the deployed environment and confirm repeat
   execution does not duplicate cycles or change existing completed work.
5. Make the Schedules page a batch monitoring and assignment view as described
   above. Review consumers before removing or restricting manual single-asset
   creation; do not retain it as the routine scheduling workflow.
6. Verify automatic year rollover, full-year future-cycle visibility, restart
   recovery, repeat generation without duplicates, new-asset enrollment,
   batch grouping, existing assignment behavior, and unchanged completed work.

Preserve the operative CPMP frequencies:

| Category               | Months                          |
| ---------------------- | ------------------------------- |
| Fire extinguisher      | February, May, August, November |
| Fire alarm             | June, December                  |
| Emergency light        | June, December                  |
| Water drinking station | February, May, August, November |

Month-end is the institutional deadline, not a planned inspection appointment.
Keep assignment, Draft reuse, submission, acknowledgement, and batch behavior
unchanged.

## 3. Manual external WMS PM number entry is hard to discover

**Discoverability concern, not a missing feature.** Current source provides the
entry under Form review, inside an acknowledged form's Corrective-action
findings. Eligible rows show Record WMS PM number and Save WMS PM number.
The inspection detail page displays the number but does not provide this entry.

- Verify this path in the deployed browser while signed in as GSD.
- Confirm the eligible row has completed field work, a form acknowledgement,
  and a non-operational result.
- Make the route to entry and the eligibility explanation easier to discover if
  the browser review confirms confusion. A contextual link from inspection
  detail may be sufficient; avoid duplicating the editor without need.
- Keep the external WMS PM number distinct from UniPM's form file number.
- Preserve revision/audit history and authorization. UniPM records a supplied
  external reference; it does not create or manage WMS/RMRF work.

**Implementation status.** GSD inspection details with corrective follow-up now
link to acknowledged forms, prefiltered by the asset code when that context has
loaded. The existing editor remains in the form's Corrective-action findings;
the link does not bypass form acknowledgement or server-side eligibility. The
Quick search also opens acknowledged forms. Browser verification against an
eligible deployed row remains pending.

## 4. Finish deployment acceptance checks

**Verification gaps, not confirmed application defects.**

- Verify synthetic photo upload, authenticated retrieval, replacement/deletion
  where supported, and persistence across an app restart using Azure Blob
  Storage. Check role restrictions without changing real records.
- Run desktop browser workflow checks against the public deployment, including
  assets, schedules, form review, external PM-number entry, and analytics.
  Local mocked browser QA and a 34-image desktop/mobile screenshot review are
  recorded in TEST-108; they do not replace deployed browser acceptance.
- Verify the mobile client against the staging HTTPS API separately before
  claiming mobile readiness.
- Document cold-start behavior, the schedule-worker decision, temporary SQL
  administrator firewall-rule cleanup, and the possibility of outbound IP pool
  changes. Do not broaden the SQL firewall as a shortcut.
- Record the deployed image/source identity and the executed deployment results
  in a dated evidence record. Preserve older preparation records as history.

Already observed: public website/API health and SQL readiness checks passed.
The operator-supplied authentication report passed for all four test accounts,
including secure refresh cookies, logout invalidation, intended schedule-read
access, Admin schedule denial, and GSD retrieval of 25 assets. Those checks do
not establish complete browser or photo-storage acceptance.

**Screenshot review (2026-10-18).** Reviewed all 28 images in the saved
screenshots directory: 25 manifest entries and three additional QA workflow
captures. They show the deployed build at `be2c514769dba76fa06848ae3eeb22680880866d`,
not the current uncommitted working copy. Treat them as historical visual
evidence, not proof of the current UI or seed data.

The Dashboard remains readable at 1280x720, 1440x900, and 1920x1080. At
1280x720, lower metric cards require normal scrolling. The sidebar stays in the
same place in the captured views, but this browser profile had it collapsed to
icons. The current shell defaults to expanded when no preference is stored and
remembers the user's choice. The asset registry and report detail show wide
tables whose rightmost columns sit outside the initial viewport. Current source
puts them in keyboard-focusable overflow regions, so the captures do not prove
that data is inaccessible. The Assets action is now pinned to the right edge,
and the lower-priority Updated column is hidden below 1440px; TEST-109's
1280x900 browser capture and assertion confirm the action stays visible without
covering Status. Other wide tables retain horizontal scrolling where needed.

The old form review repeats generic "Unsubmitted form" titles for Drafts with
no file number. The current working copy builds each Draft title from its asset
category, department, and PM period. Browser review should confirm those labels
remain readable in the updated form cards.

The old Ask NLA drawer occupies a narrow right panel. Its help and calculation
notes take most of the space, and the result table headers wrap into cramped
columns. The matching and calculation source records remain useful, but the
query guidance and result hierarchy need a cleaner presentation. The current
working copy combines structured and question-based reporting in Dashboard
modes; capture that version in a browser before deciding whether the drawer
issues remain. Do not expand the NLA schema or change its interpretation rules
under this visual follow-up.

**Current-source local visual review (2026-10-18).** The updated view was opened
at the local Vite server with a fictional GSD session and mocked API responses.
No staging account, live API, provider, or database was used. This is an
exploratory view of the uncommitted working tree based on the exact HEAD above,
not formal test evidence. Screenshots are in
`artifacts/web-followup-visual-review-2026-10-18/`:

- `ask-pm-empty-desktop-1440x900.png`
- `ask-pm-result-desktop-1440x900.png`
- `ask-pm-result-compact-1280x720.png`
- `quick-search-gsd-desktop-1440x900.png`
- `gsd-batch-supervisor-assignment-desktop-1440x900.png`

The question composer now shows selectable examples beside the input, rather
than hiding them in a disclosure; selecting an example fills the field without
submitting it. At 1280x720 the page reported no horizontal overflow. The result
view separates interpreted scope, answer, population, and matching source
records. This improves wayfinding and layout; it does not verify that every
example is accepted by the live interpreter or resolve the limited supported
language/schema concern. Quick search is readable, though its capped result
list requires scrolling for lower destinations. The GSD batch screenshot shows
the Supervisor selection/review flow with three fictional schedule rows; it
does not submit an assignment or verify a live handoff. Clarification and
rejection states, legacy URL navigation, Inspector-side assignment, and live
workflow acceptance remain unverified by this exploratory capture. TEST-108
later records a full mocked Playwright run and a review of the current
desktop/mobile screenshot archive. Live workflow acceptance remains pending;
the full mocked suite does not establish deployed authorization or data state.

The old acknowledgement captures make the empty signature canvas look large
and the explanation dense. The current form detail constrains the canvas to a
25rem maximum width and uses a shorter receipt explanation. TEST-108 includes
current Form review and photo screenshots; its visual review found no blocking
layout issue. The completed QA record and photo views expose
test-only text such as
`QA-WEB-20261017-FOLLOWUP-01`, "QA Synthetic Signatory," and "QA SYNTHETIC
PHOTO." Keep those records out of presentation screenshots and remove them
from any demo database only through its approved cleanup procedure. Several
older asset and inspection captures also use `DEMO-*`, "Fictional" placements,
and unsupported floor descriptions. The current catalog and QR sheet use the
newer asset codes, but the updated seed data still needs a fresh visual review.
The synthetic photo preview itself is centered, contained, and has a visible
close action; no defect was apparent there. The current screenshots use mocked
data, so a fresh visual review of the actual staging seed remains pending.

**Assets table follow-up (TEST-109).** The View details action remains pinned
at the right edge of the desktop table while the Updated column is suppressed
below 1440px to prevent overlap at compact desktop widths. The refreshed
1280x900 screenshot is stored under
`artifacts/web-qa/20261018T-final-followup/supplemental/` and was reviewed after
the Playwright test passed.

The Add schedule view confirms the current per-asset manual workflow, which is
the known mismatch with the requested CPMP-driven annual batch planning and
assignment flow. The owner approved automatic current-year CPMP generation,
and the operator templates now enable the existing worker. Per-asset manual
creation should not be the routine workflow. GSD approval is still required
before past-cycle catch-up or manual cycle adjustments. TEST-108 later records
desktop and mobile captures of the current working copy, including the settled
Schedules navigation state; those mocked screenshots do not verify the
deployed schedule worker.

## 5. Combine Dashboard and PM Analytics

**Owner-approved follow-up plan.** The two reporting destinations overlap and
make users learn separate paths to the same PM results. Keep one Dashboard in
the sidebar with two ways to request a report:

- Keep structured filters as the default entry, using the supported category,
  department, and period controls.
- Offer an adjacent Ask a question option that opens a compact input with
  examples. Avoid an oversized question card above the dashboard.
- Share one results area for the interpreted scope, totals, breakdowns, and
  matching source records. Preserve any distinction between matching records
  and calculation sources.
- Remove PM Analytics as a separate sidebar item and redirect its existing URL
  to the Dashboard, preserving meaningful link context where supported.
- Reuse existing components after inspecting both pages and sketching the
  combined layout. Preserve calculations, authorization, and supported query
  scope; this is a navigation and presentation change.
- Verify both report-entry paths at desktop sizes, including switching modes,
  empty results, clarification, rejection, and existing URL navigation.

Keep NLA language/clarification improvements in section 1 as a separate change.
The Dashboard now provides two report-entry modes: structured filters by
default, and a GSD-only question mode in the same page area. The separate
sidebar destination is removed. Existing `/app/analytics` links redirect to
the question mode, and Quick search offers the same GSD-only destination.
Calculations, authorization, and supported query scope are unchanged. The
current-source local captures and TEST-106 cover the dashboard, switching to
question mode, one mocked valid non-operational query, matching/calculation
source selection, source details, and navigation back to an asset. The result
view was captured at the calculation-source section after internal scrolling.
The full mocked Playwright suite later passed under TEST-108. It exercises the
question mode, a valid result, source-scope switching, source details, and
navigation back to an asset. Empty results, clarification/rejection states,
and legacy URL navigation still need explicit browser acceptance; public
deployment behavior remains unverified.

**Local verification status (2026-10-18):** TEST-108 records the completed
web verification: typecheck passed, all 231 web unit tests passed, and the
full mocked Playwright run passed 57 tests with one opt-in live WMS test
skipped. The sidebar assertions now require the Dashboard mode switch rather
than the removed PM Analytics link. This local run used Node 24, outside the
declared Node 22 range; exact-head CI and deployed behavior remain unverified.

## 6. Make quarter filters depend on asset category

**Owner-requested follow-up.** Disable quarters with no applicable CPMP cycle
when a category is selected:

| Category               | Available calendar quarters | Scheduled months                |
| ---------------------- | --------------------------- | ------------------------------- |
| Fire extinguisher      | Q1, Q2, Q3, Q4              | February, May, August, November |
| Water drinking station | Q1, Q2, Q3, Q4              | February, May, August, November |
| Fire alarm             | Q2, Q4                      | June, December                  |
| Emergency light        | Q2, Q4                      | June, December                  |

- Keep all quarters available when no category is selected, and retain the
  All quarters option.
- Explain disabled choices accessibly, for example that the category has no
  scheduled CPMP cycle in that quarter.
- If changing category makes the selected quarter inapplicable, clear that
  selection and explain the change.
- Keep calendar-quarter numbering. June is Q2 and December is Q4, even for
  categories with only two cycles per year.
- Apply the rule consistently to filters that select scheduled PM cycles.
  Do not apply it to actual inspection-date filters: late work can occur in a
  different quarter from its scheduled cycle.
- Reuse canonical frequency mappings and inspect existing backend validation
  before editing. Preserve server validation and URL/filter consistency without
  changing CPMP frequencies or maintenance records.
- Verify category switching, All quarters, disabled-choice explanations, and
  invalid combinations arriving through existing links.

**Implementation status:** Schedule and inspection registry filters now derive
available quarters from each category's existing scheduled months, disable
inapplicable choices with an accessible explanation, clear stale draft
selections, and replace unsupported category/quarter combinations in the URL.
The CPMP mappings and server-side validation rules were not changed. The full
mocked Playwright suite passed with the quarter-filter scenarios included, and
the desktop/mobile captures were reviewed.

## 7. Show assigned personnel and support supervisor autocomplete in Form review

**Owner-requested follow-up.** Help GSD identify responsibility without opening
each schedule.

- Show the assigned supervisor and assigned inspectors on form entries and the
  form detail view, using the role term Inspectors consistently.
- Add a supervisor filter where users type part of a name and select an
  autosuggestion from existing supervisor accounts. Match by account identity,
  not free-text name alone, and distinguish duplicate names using appropriate
  existing non-sensitive account information.
- Include All supervisors and Unassigned choices. Filtering must never change
  assignments.
- Keep assigned personnel separate from the inspector who actually performed
  each inspection. Reassignment can make these different people.
- Inspect current form/schedule contracts and historical assignment behavior
  before implementation. Do not present current assignments as proven historical
  responsibility. Use Unassigned or Not recorded as appropriate; if a batch has
  multiple supervisors, represent that accurately instead of choosing one.
- Preserve authorization, form lifecycle, assignment semantics, and existing
  filters. Keep name suggestions bounded/searchable rather than loading an
  unbounded account list.
- Verify partial-name suggestions, duplicate names, selected-account filtering,
  clearing, Unassigned, and consistency between form entries and details.

**Implementation status.** Form list and detail responses now include the
current schedule Inspector and Supervisor account IDs and display names. Form
entries show current assignments separately from the person recorded as having
performed the inspection. The GSD registry can search supervisor names, choose
an account identity, and filter by that account or Unassigned. Duplicate names
include a short account-ID suffix. The supervisor and inspector batch
assignment screens already use the same searchable picker and three-step
selection, review, and confirmation flow.

The picker uses one type-to-suggest field in Form review and both batch
assignment flows. Assignment-option endpoints now filter active accounts by
role and display-name search in SQL, return at most ten matches, and include a
currently selected eligible account when it falls outside those matches. The
assignment queue returns the current assigned names with each batch, so its
cards no longer load complete role-option lists. TEST-100 records the earlier
UI-only cap; TEST-101 records this server-side follow-up and its focused API
verification. TEST-102 records a successful supported Node 22 production
build. TEST-107 records the focused Form review test and a race fix: resolving
supervisor suggestions no longer clears text the user is typing. A fresh
Form review browser screenshot, full suites, CI, and deployed acceptance remain
pending.

These values describe current schedule assignments, not historical assignments
at inspection time. Focused web verification passed. The local GSD batch
selection/review view was captured with fictional mocked records; assignment
submission, the Inspector-side handoff, and deployed-app acceptance remain
pending. The full web unit and Playwright suites passed locally, and current
Form review desktop/mobile screenshots were reviewed. A focused browser test
also confirmed the searchable Supervisor batch flow; no assignment was
submitted.

## 8. Separate planning and assignment from schedule history

**Owner-agreed direction.** Keep one Schedules sidebar destination with two
internal views, not an additional History sidebar tab:

- **Planning & assignment:** default to the current institutional year and show
  upcoming batches, assignment status, progress, and eligible actions.
- **History:** browse past PM cycles by year, category, period, and department.
  Historical cycles can still contain unfinished overdue work; do not classify
  history solely by Completed status or hide outstanding obligations when a
  period passes. Show recorded assignments only where supported by evidence.

The guided assignment sequence is **Year -> Asset category -> PM month ->
Department -> Assign supervisor**. Default the year, offer only CPMP-valid
months for the category, then show matching department batches. Asset category
means the equipment type, not an individual asset. Calendar quarter is an
optional shortcut, not another mandatory selection. The month determines its
quarter; apply section 6 consistently.

Provide a direct **Needs supervisor** view that immediately lists eligible
unassigned batches without requiring users to complete those filters first.
Filters remain available to narrow the list. Due and Unassigned are different:
eligible future batches must be assignable before their deadline, subject to
existing assignment rules.

Put a visible **Assign supervisor** action on eligible batch rows. Use a
searchable supervisor picker and confirm department, category, PM cycle, and
affected asset count before saving. Display the assigned name after success
and explain unavailable actions. Preserve restrictions once work has started;
do not weaken backend eligibility to simplify the screen.

**Implementation status.** GSD and Supervisor users now land on Planning &
assignment for the current Manila calendar year. GSD can see all batches and
filter to those needing a Supervisor; Supervisors see their assigned batches
with a filter for those needing an Inspector. Schedule history is a separate
view. Opening a batch's schedule list applies the exact department, category,
year, and PM-cycle filters to the paginated per-asset registry. Schedule
generation and performance review at larger asset counts remain pending.

**Scale audit (2026-10-18).** The assignment queue previously called the
unpaginated `GET /api/v1/schedules` list with year/category/department filters,
then grouped all returned schedule rows in the browser. At 1,000 assets, an
all-four-cycle mix could mean roughly 4,000 rows for a year.

**Local implementation status (2026-10-18; TEST-098).** The queue now calls
`GET /api/v1/schedules/assignment-batches`. The API returns one compact summary
per Department + asset category + PM cycle, including schedule totals,
assignment states, progress counts, a representative schedule, and the lock
state used by the existing assignment rules. GSD receives all filtered
batches. A Supervisor receives complete batches containing at least one row
assigned to that Supervisor, so mixed ownership remains visible and cannot be
mistaken for a consistent assignment. Inspection evidence is included in the
lock decision. The browser no longer downloads or groups every full schedule
DTO, and assignment confirmation reuses the returned batch count.

The API still reads a lightweight projected row for each matching schedule and
groups those rows in application memory. No 1,000+ asset performance run has
been made; this reduces network transfer and browser work but does not prove
end-to-end responsiveness at that scale. Keep the paginated per-asset registry
for reviewing assets within a batch. Schedule generation policy remains
separately gated by GSD's approved effective date and adjustment authority.
History, planning, batch deep links, and mobile filter captures were reviewed;
these mocked local screenshots do not verify live assignment handoffs or
large-directory performance.

## 9. Make the Supervisor workflow task-oriented

**Owner-agreed follow-up.** Audit current role screens and policies first, then
make the existing responsibilities clear:

- GSD assigns a Supervisor to a batch. The assigned Supervisor assigns an
  Inspector. Preserve the actual permission model and batch assignment behavior.
- Give Supervisors a clear **My assigned batches** starting view with department,
  category, cycle, deadline, assigned inspector, and inspected/scheduled progress.
- Make batches needing an inspector easy to find and prioritize within that
  view, without hiding overdue or already assigned work.
- Provide a visible **Assign inspector** action with a searchable eligible-name
  picker and confirmation of the batch and affected asset count.
- Batch details show outstanding/completed assets, findings, and links to
  inspections and forms, within the role's permitted visibility.
- Explain restricted actions, including assignments locked by work already
  started. Preserve inspection behavior, form lifecycle, and assignment rules.

Verify both sides of the handoff, GSD to Supervisor and Supervisor to Inspector,
including empty queues, assignment confirmation, and updated names/progress.

**Owner-confirmed consistency requirement.** Supervisor assignment and Inspector
assignment must use the same UI and sequence: open the relevant batch queue,
select a batch, choose an eligible person through a searchable name picker,
review the department/category/PM cycle and affected asset count, then confirm.
Use consistent button placement, selection feedback, confirmation, success, and
error states. Reuse the assignment presentation where practical; only role
labels, eligible people, and existing permission/eligibility rules should differ.
GSD uses Needs supervisor; Supervisors use their assigned batches needing an
inspector. Neither flow should require opening an individual asset schedule or
filtering by Due before assignment.

**Implementation status.** Both assignment paths now use the shared three-step
selection, review, and confirmation form with role-specific eligible people and
the existing permission rules. GSD assigns Supervisors from the Needs
Supervisor queue; Supervisors assign Inspectors from their assigned batches.
The review shows batch scope and the server-reported schedule count. A local
GSD selection/review screenshot uses fictional mocked records and did not submit
an assignment. The Inspector-side handoff, successful assignment submission,
and assignment error states remain pending for live acceptance.

The current local picker presents one type-to-suggest field for both roles.
The batch summary and Inspector picker now use the same user-facing role term,
"Inspector," instead of mixing it with "Skilled worker." The focused web
workflow run passed 2/2 targeted GSD-to-Supervisor and Supervisor-to-Inspector
assignment tests (19 unrelated tests skipped). This verifies the shared form's
role-specific controls, batch-size review, and mocked assignment responses. It
does not verify live API handoff or deployed acceptance. Current desktop
planning, mobile schedule, and role-specific assignment-review screenshots were
visually reviewed. TEST-103 records the focused workflow run.

## 10. Add role-aware quick navigation

**Owner-agreed follow-up.** Provide **Quick search** in the sidebar and a
**Ctrl+K** shortcut that opens a compact navigation search. Use predefined
destinations and task aliases; no AI or new search infrastructure is needed.

Example searches and destinations:

| Search               | Destination                                                |
| -------------------- | ---------------------------------------------------------- |
| Assign supervisor    | GSD Schedules, Needs supervisor view                       |
| Assign inspector     | Supervisor's assigned batches needing an inspector         |
| Find inspection      | Inspection registry                                        |
| Record WMS PM number | Relevant form-review entry point with eligibility guidance |

Show only destinations/actions available to the signed-in role. Selecting a
result navigates; it never performs assignment or writes records. Do not
pretend a generic shortcut identifies a specific eligible form automatically.
Include keyboard selection, Escape dismissal, focus handling, and a useful
empty-results message. Existing routes must still enforce authorization.

Quick search was added after the batch and role workflows were in place. The
schedule page has a role-gated Assignments view. GSD sees batches that need a
Supervisor; Supervisors see their assigned batches and can prioritize those
needing an Inspector. Both queues use the same batch selection and searchable
assignment form. Mixed assignments are identified before a batch is changed.
Changing a Supervisor warns that Inspector assignments are cleared. The count
comes from the grouped assignment-batch summary and is available with the queue
response.

**Implementation status.** The sidebar now has Quick search and a Ctrl+K
shortcut. Search results are predefined, filtered by the signed-in role, and
navigate to existing pages or assignment/form queues without writing records.
Arrow keys, Enter, Escape, focus restoration, and an empty-results message are
implemented. The active keyboard result scrolls into view. Selecting a
destination from the mobile sidebar closes the drawer. Assignment shortcuts
open the role-specific Needs supervisor or Needs inspector filter, and the
filter is preserved in the URL. A final keyboard regression fix resets the
active destination whenever the query changes, so Enter cannot target an item
outside the filtered results. The focused Playwright regression passed and
captured the centered GSD Quick search dialog at 1440x900. The modal is compact,
shows the matching task clearly, and retains keyboard instructions. Lower
results use internal scrolling. The tests use a fictional session and mocked
APIs; they do not verify deployed authorization or destination data.

This change does not replace the per-asset schedule registry with batch
monitoring, alter schedule generation, or resolve GSD authority for schedule
adjustments. A batch's **View schedules in batch** action now opens the
server-paginated schedule registry filtered to the exact department, category,
year, and PM cycle, rather than opening one representative asset schedule.
TEST-108's mocked local browser run and screenshot review passed; live schedule
acceptance and performance at larger asset counts remain pending.

## Suggested order

1. Obtain GSD's effective-date decision before historical schedule catch-up and
   confirm who may adjust cycles before exposing manual adjustment controls.
2. Complete the deployed WMS-entry check and synthetic Azure Blob photo
   persistence checks. Keep API-assisted field entry distinct from browser-only
   acceptance.
3. Run the read-only live schedule-impact audit before enabling or relying on
   automatic generation in staging; confirm recovery and idempotence there.
4. Measure the assignment queue against a substantially larger synthetic data
   set before making a 1,000+ asset responsiveness claim.
5. Keep broader NLA schema and language expansion parked until the separate
   post-validation direction is approved. This UI work does not widen the
   grammar or establish live analytics quality.

## Current workspace verification

Final local verification ran on 2026-10-18 against the working tree on branch
`feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. TEST-108 records the exact Web
source-manifest digest:

`4339a6e166940ea8ed0f469dd11ff0740c0cba7cb14f9557a7616f4e5fc05c6c`

The package declares Node `>=22 <23`; the final run used Node 24.15.0 and npm
11.12.1. Results are valid local observations, not supported-runtime or
exact-head CI verification.

- `npm run format:check`, `npm run lint`, and `npm run typecheck` passed.
- `npm run test:run` passed **231 tests across 29 files**. Vitest emitted its
  existing jsdom `window.scrollTo()` not-implemented warnings.
- `npm run e2e -- --reporter=line` passed **57 tests; 1 opt-in live WMS test
  skipped** because `UNIPM_DEV_USER_PASSWORD` was not configured. The tests use
  mocked API responses.
- The final Quick search selection-reset and schedule deep-link/screenshot
  regression run passed **2/2**. Changing the Quick search input resets the
  selected result; Enter navigates to the filtered Supervisor assignment queue.
  The planning screenshot now shows the settled active Schedules indicator.
- `npm run build` passed; typecheck ran and Vite transformed 2,224 modules.
  This build used Node 24, outside the declared engine range.
- `npm run api:contract:check` passed earlier in this worktree. `npm run
  api:check` regenerated the client, then its generated-file comparison reported
  modified/untracked generated files in this uncommitted worktree; a clean
  generated-client comparison is not established.
- The final screenshot archive contains **34 images** (15 desktop/landscape,
  19 mobile/portrait), with contact sheets, a full-resolution gallery, a SHA-256
  manifest, and visual review under ignored
  `artifacts/web-qa/20261018T-final-followup/`. Screenshots use fictional
  sessions and mocked API responses. The visual review found no blocking layout
  defect. TEST-109 supersedes the archive's initial Assets action-column note:
  the action is pinned and confirmed visible at 1280x900; its supplemental
  screenshot is alongside the archive. QR print sizing remains undecided
  pending a label-stock specification. The TanStack Query badge is a
  development-only overlay.
- `git diff --check` passed after the evidence and planning updates; the
  generated route tree may produce a line-ending conversion notice.

Remaining acceptance work is external or broader than this UI change: supported
Node 22 full verification, exact-head GitHub CI, full backend suite, deployed
assignment and WMS workflows, Azure Blob persistence, live schedule-worker
behavior, a 1,000+ asset performance run, physical-device verification, and
GSD/staging acceptance. GSD's scheduling effective date and adjustment
authority remain unresolved. No Azure database/resource was accessed, and no
deployment or merge was performed. The implementation was committed locally
after the verification described above; branch push and exact-head CI remain
unverified in this record.
