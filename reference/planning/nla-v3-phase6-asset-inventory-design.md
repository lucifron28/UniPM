# NLA V3 Phase 6: asset inventory report design

Status: proposed contract for review. Phase 6 implementation has not started.

## Subject and authorization

Add one typed V3 subject, `assetInventory`, using the existing catalog,
validation, GSD authorization, query, signed preview, confirmation, and React
plan/result patterns. The canonical row is exactly one registered row from the
authorized `Assets` table. No PM period is required. A question about PM
execution stays on `pmExecution`; the backend must not choose a subject from a
model supplied table or join path.

The endpoint remains GSD authorized, consistent with existing V3 reporting and
the asset registry summary. No wider role or per-user row scope is implied.
Read current `Assets` fields through the existing EF context. Do not join
`PreventiveMaintenanceSchedules`, `InspectionRecords`, forms, acknowledgements,
or referral audit history in the inventory query.

## Contract boundary

Proposed typed plan fields:

```text
ReportPlan
  schemaVersion, catalogVersion
  subject: assetInventory
  mode: aggregate | records
  categories: optional nonempty selection of supported asset categories
  baseFilters: department, building, location, registrationStatus
  groupBy: zero, one, or two of category, department, building, location, registrationStatus
  measures: registeredAssetCount and, if needed, separately named status counts
  orderBy: catalogued group or measure fields
  top: optional bounded ranking by registeredAssetCount
  presentation: table | summary; typed columns
```

The backend handles an omitted category selection as all four currently
registered asset categories. `registrationStatus` is optional and accepts only
the persisted `Active`, `Inactive`, or `Retired` catalog codes. The current
asset API only writes `Active`, and the status catalog does not define status
transitions; before implementation, confirm what existing inactive and retired
rows mean and whether GSDs may filter on each. Other filters use current Asset
values and existing bounded exact-match catalog resolution.
Department, building, and general location are free-text current-state fields;
they are not historical snapshots. Empty-value behavior must be explicit
(`IsNull` where supported), not an inferred label.

Keep filters flat AND. A filter selects the eligible asset population before
grouping and counts. Do not infer status from an inspection, schedule, QR
verification, or acknowledgement. `Active` is a registration status; an
inspection condition such as `NonOperational` is a separate recorded finding
and is outside this inventory measure.

## Calculations and presentation

- `registeredAssetCount` is the count of unique authorized Asset IDs after
  category and base filters. Because the subject query is one row per asset,
  `COUNT(*)` is sufficient only while the query has no one-to-many joins.
- `activeAssetCount`, `inactiveAssetCount`, and `retiredAssetCount` may be
  exposed as explicitly named status counts. If a status filter is applied,
  each count is calculated within the filtered population; do not report counts
  for excluded statuses as though they shared that denominator.
- Grouped rows partition the eligible Asset population. A two-dimension group
  must never duplicate an asset across values. Top/bottom selection occurs
  before any requested display sort, with stable category/value tie-breaks.
- No inventory percentage is introduced in this milestone. Empty populations
  return count zero; they do not produce a null rate or a PM coverage claim.
- Record mode returns one row per Asset with supported identity and current
  registration context. It does not expose QR secrets, coordinates, embeddings,
  or arbitrary descriptions.

If users ask for current operational condition, clarify that registration
status does not answer it. A future condition metric needs a separate design
that chooses a source inspection, as-of timestamp, ties, and missing-inspection
semantics. Do not add that metric to Phase 6 by joining inspection rows.

## Focused test contract

Use synthetic fixtures with explicit Asset IDs, statuses, and current context.
Include:

1. Counts by category, building, and department, with authorized filters.
2. All three registration statuses, including mixed status groups and exact
   status filters.
3. Assets with no PM schedules, many PM schedules, many inspection/form rows,
   and referral audit rows. Each must still count once and return one record.
4. Null and duplicate current location values, with the documented null group
   and stable ordering.
5. Empty inventory scope and page boundaries.
6. A deliberately non-operational inspection on an Active asset, proving the
   inventory status remains Active and the operational finding is not used as
   registration status.
7. GSD access, anonymous 401, non-GSD 403, invalid category/status rejection,
   signed preview expiry and cross-user rejection.

The highest-value fanout regression seeds one asset with multiple schedules,
inspection records and referral audit rows, then verifies an inventory count
of one and one source row. The query implementation should remain an Asset
query with no joins that can increase its grain.

## Dependencies and phase gate

Before coding, complete Phase 5 review and verification: settle provisional
question labels, review calculation fixture expectations, run backend and web
checks, and execute SQL Server 2019 scenarios on a disposable local database.
No inventory code should be started while those semantic or compatibility
questions remain open. Confirm the four asset categories and status definitions
against the existing catalogs; do not add statuses or alter registry data.

## Phases 7–8 planning

### Phase 7: batch and assignment reporting

Use an explicit batch identity from department, category, and PM cycle;
building must not split a batch. Document incomplete/missing department and
assignment values, unassigned versus assigned status, and how partially
completed batches are counted. Establish whether assignments describe current
or historical responsibility before exposing them. Never infer old assignments
from present state, expose unrestricted employee data, score staff, or write
assignments. Tests should compare report batch eligibility with the existing
operational batch workflow and prove assignment joins do not multiply batches.

### Phase 8: forms and follow-up reporting

Report at one row per form with Draft, Submitted, and Acknowledged status.
Choose a separate explicit event timestamp and fixed as-of time for each ageing
definition; form creation, submission, and acknowledgement age are different
questions. Acknowledgement is not inspection completion. For non-operational
follow-up, define whether the subject is one inspection or one referral, and
deduplicate the one-to-many referral audit table before counts. Report only
recorded external PM references; external repair completion and turnaround
remain unavailable until an authoritative lifecycle source exists.
Signatures and signatory details are never interpreter fields.
