# NLA V3 future analytical proposals

These are source-informed feasibility notes only. None of the proposed
measures or behaviors is implemented by the final-hardening change. Each
requires a separate business-definition review with the designated GSD owner
before a report contract, schema, query, or UI is changed.

## Schedule coverage: registered assets without expected persisted cycles

- **Current evidence:** V3 separates one current registered `Asset` from
  persisted `PreventiveMaintenanceSchedule` rows. Schedules have an asset,
  category through that asset, PM cycle, period metadata, status, and completion
  timestamp. Existing schedule deferral records and generation policy can
  explain some omissions, but a missing row by itself does not identify an
  expected cycle.
- **Definition needed:** which asset registration states and effective dates
  enter the denominator; category-specific CPMP cycles; pre-generation and
  post-generation windows; cancellations, deferrals, retired/replaced assets,
  and approved exceptions; whether the measure is asset-cycle coverage or
  asset coverage.
- **Provenance and scope:** authoritative current asset register plus the
  approved cycle calendar, schedule-generation eligibility, deferral records,
  and any signed GSD exception. Use current operational authorization and
  scope before grouping by department/building.
- **Schema and SQL:** likely needs a versioned expected-cycle policy or
  materialized expectation snapshot with effective dates and audit trail unless
  the complete expected universe can be derived deterministically. Add SQL
  Server 2019 tests for unscheduled, deferred, cancelled, late-registered,
  duplicate, and out-of-scope assets; validate query cost at production-like
  counts.
- **UI:** explicit eligible/expected population, scheduled/missing counts,
  exception reasons, selected cycle range, and drill-down to each asset and
  policy source. Do not label all absent schedules “missed.”
- **GSD validation:** GSD Head as policy owner, with the schedule-generation
  custodian and asset-registry owner approving effective dates, exceptions, and
  the denominator.

## Repeated non-operational findings across PM cycles

- **Current evidence:** an `InspectionRecord` links to a persisted schedule and
  asset and stores `IsOperational` and `CompletedAt`; the form lifecycle has
  Draft, Submitted, and Acknowledged states. The V3 catalog already defines a
  non-operational count, not recurrence.
- **Definition needed:** whether recurrence means two or more findings ever,
  consecutive eligible cycles, a fixed window, or a GSD-selected threshold;
  whether only acknowledged official history counts; how asset replacement,
  merged records, category changes, and missing cycles affect continuity.
- **Provenance and scope:** acknowledged inspection findings linked to
  authoritative asset-cycle schedules; exclude Draft/Submitted findings from
  official history. Do not infer findings from free-text remarks or WMS
  referral existence.
- **Schema and SQL:** the existing rows may be sufficient if the chosen rule is
  purely derived, but a supporting index or versioned recurrence policy may be
  required. If a durable snapshot is chosen, add a migration and retain source
  inspection IDs. SQL Server 2019 tests should cover distinct cycles, duplicate
  rows, missing cycles, asset identity changes, and acknowledgement gating.
- **UI:** state the recurrence window, cycle count, qualifying findings, and
  source inspection links; distinguish “repeated finding” from a predicted
  failure.
- **GSD validation:** GSD Head and PM/inspection policy owners approve the
  recurrence threshold, official-history boundary, and treatment of gaps.

## Multi-year same-population compliance delta or trend

- **Current evidence:** PM schedule and completion timestamps are retained by
  cycle, but `Asset.Department` and `Asset.Building` represent current values;
  the current report contract does not provide a historical department or
  building snapshot. A changed asset population or reassignment can therefore
  change a historical grouping.
- **Definition needed:** compare category/cycle cohorts, same assets present in
  both years, or the independently eligible population per year; define
  missing/added assets, deferrals, cycle coverage, partial years, zero
  denominators, and whether differences are percentage points or relative
  percent change. V3 does not currently offer a year-over-year delta measure.
- **Provenance and scope:** historical schedules, completion timestamps, and
  a stable, approved population snapshot with time-correct organizational
  assignments. Current asset fields alone are insufficient to assert a
  historical same-population comparison.
- **Schema and SQL:** likely needs effective-dated asset/department snapshots
  or a governed annual population snapshot and migration/backfill policy. SQL
  Server 2019 verification should compare distinct eligible populations,
  denominators, omitted categories/cycles, and computed deltas over multiple
  years.
- **UI:** show both year populations and denominators, the comparison method,
  category-cycle coverage, absolute percentage-point change, and direct source
  records. Do not present an incomparable cohort as a trend.
- **GSD validation:** GSD Head plus the institutional asset/department data
  owner approve the population identity and historical snapshot source.

## Non-operational findings as a percentage

- **Current evidence:** V3 can report `nonOperationalCount` and completed or
  scheduled counts at the same asset-cycle grain. The denominator is not
  currently exposed as a non-operational percentage measure.
- **Definition needed:** choose between completed eligible inspections,
  scheduled eligible cycles, distinct assets inspected, or all registered
  assets; decide acknowledgement rules, partial-cycle behavior, mixed-category
  weighting, and zero-denominator output. These denominators answer different
  questions.
- **Provenance and scope:** numerator and denominator must use the same
  eligible population and cycle scope. A stored finding must come from the
  authorized inspection condition, not from colloquial remarks or corrective
  referral status.
- **Schema and SQL:** no new column is required if GSD approves a derived
  formula over existing authoritative rows; an index may be needed. Add SQL
  Server 2019 tests for zero denominator, partially completed periods,
  multiple categories, grouped percentages, and matching denominators before
  adding a new typed measure.
- **UI:** name the denominator in the label and definition and show numerator,
  denominator, and unmeasurable state beside each percentage.
- **GSD validation:** GSD Head and the official inspection-history owner
  approve the denominator and whether Draft/Submitted rows are excluded.

## Repair/referral turnaround or root-cause analysis

- **Current evidence:** `InspectionWmsReferral` stores an external PM number,
  revision and UniPM audit timestamps. It does not provide authoritative WMS
  acceptance, repair-start, completion, root-cause, or resolution milestones.
  Inspection remarks and recommendations are unstructured and do not establish
  a WMS lifecycle.
- **Definition needed:** referral handoff versus accepted ticket; start/end
  events; pause/reopen behavior; calendar versus business time; source of truth
  for root cause; duplicate tickets; and whether turnaround is elapsed or
  business-hours time.
- **Provenance and scope:** an approved WMS API/export with stable cross-system
  IDs, milestone timestamps, controlled root-cause taxonomy, data freshness,
  and reconciliation. Do not infer completion or cause from the PM number or
  inspection remarks.
- **Schema and SQL:** requires an approved integration contract and likely
  versioned WMS event/status storage plus migrations, idempotent sync, audit,
  and retention rules. Test duplicate/out-of-order events, reopened tickets,
  time zones, missing milestones, and source reconciliation in SQL Server
  2019.
- **UI:** clearly identify external source and last synchronization time,
  separate prepared handoff from actual WMS status, and drill into source
  events. Restrict access according to operational sensitivity.
- **GSD validation:** GSD Head and the WMS product/data owner approve the
  lifecycle semantics, source authority, taxonomy, permissions, and retention.

## Release boundary

The proposals above add no current V3 measure, query, migration, or UI control.
If one is approved, define its data lineage and SQL Server 2019 evidence first,
then version the report catalog and evaluate the independently reviewed
business rule before enabling natural-language interpretation.
