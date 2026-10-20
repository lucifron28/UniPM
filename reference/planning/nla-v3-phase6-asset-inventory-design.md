# NLA V3 Phase 6: asset inventory report design

Status: implemented at `fcd6e6b7b1ab71bdc64e3c4520fcb558b7551920` on the
isolated `feature/nla-v3-asset-inventory` branch. Focused local verification
passed; native SQL Server 2019 and institutional acceptance remain unverified.
See [TEST-114](../evidence/test-runs/TEST-114-pm-analytics-v3-asset-inventory.md).
This engineering result does not approve the Phase 5 question labels.

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

Implemented typed plan fields:

```text
ReportPlan
  schemaVersion, catalogVersion
  subject: assetInventory
  mode: aggregate | records
  categories: selected supported asset categories; empty means all four
  baseFilters: department, building, location, registrationStatus
  groupBy: zero, one, or two of category, department, building, location, registrationStatus
  measures: registeredAssetCount
  orderBy: catalogued group or measure fields
  top: optional bounded ranking by registeredAssetCount
  presentation: table | summary; typed columns
```

The backend handles an omitted category selection as all four currently
registered asset categories. `registrationStatus` is optional and accepts only
the persisted `Active`, `Inactive`, or `Retired` catalog codes. The current
asset API only writes `Active`, and the status catalog does not define status
transitions. This implementation exposes the stored codes as filters and groups;
it does not assign lifecycle semantics to them. Other filters use current Asset
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
  a row count is sufficient only while the query has no one-to-many joins. The
  measure has unit `assets`, has no denominator, and returns zero for an empty
  population.
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
The implemented full API regressions cover all three stored statuses, default
all-category selection, department/building/location/status filters, status
grouping, stable record paging, and an Active asset whose inspection history
includes a non-operational finding. The fixture also gives that asset four PM
schedules, four inspection rows, and six referral-audit rows; inventory still
returns one asset. GSD, anonymous, and non-GSD access are covered by the V3
route policy tests. The React test and narrow browser scenario exercise subject
selection, null period, editable grouping, preview, confirmation, provenance,
and results.

Phase 6 regression coverage now also includes tied location-group rankings
with stable ordering, null and blank locations grouped into the documented
null bucket, duplicated values, an empty filtered population, the empty final
record page, and inventory-specific tampered, cross-user, and expired preview
confirmation requests. Filtered record assertions verify registration status
and ensure schedule/inspection IDs remain null. These tests use the full API
preview and execution routes.

Native SQL Server 2019 translation and execution against a disposable test
database remain unverified. The focused integration tests are present and
loopback-only, but the local SQL Server image download was denied by the
container registry and no `UNIPM_SQLSERVER2019_TEST_CONNECTION` was configured.

The highest-value fanout regression seeds one asset with multiple schedules,
inspection records and referral audit rows, then verifies an inventory count
of one and one source row. The query implementation should remain an Asset
query with no joins that can increase its grain.

## Phase 5 status and verification boundary

Phase 5 remains incomplete: the 18 question labels are still pending human
review, the score gate remains closed, and native SQL Server 2019 compatibility
is unverified. The inventory change is isolated as an editable typed subject and
does not alter or approve those PM interpretation expectations. No acceptance
score is claimed. The API contract reuses the existing four categories and
three stored status codes; no status or registry data was changed.

The API regressions use EF InMemory. Do not describe the SQL Server query as
verified until the focused integration test runs against a disposable SQL
Server 2019 database.

## Implemented contract

- `GET /api/v1/analytics/pm/v3/catalog?subject=assetInventory` returns only
  inventory measures, dimensions, base filters, and record columns while
  retaining the shared subject list and GSD authorization.
- `catalogVersion` is 2. Inventory plans set `period` to null; PM plans still
  require their PM period. Empty inventory categories normalize to the four
  supported category IDs.
- `registeredAssetCount` reads `Assets` directly with no schedule, inspection,
  form, acknowledgement, or referral joins. Filters are category, department,
  building, location, and the exact stored registration status code.
- Aggregate and record modes share the existing signed, stateless, user-bound,
  expiring preview and confirmation flow. Record rows have nullable schedule
  and inspection identifiers; inventory rows leave both null.
- The React workflow selects the subject from the catalog, hides PM period and
  matching-record controls for inventory, and shows count provenance, source
  records, empty states, and current registration status separately from
  inspection condition. Natural-language interpretation remains PM-only and
  is never silently mapped to inventory.
- Full end-to-end API regression coverage includes multiple schedules,
  inspections, and referral audit rows without multiplying the asset count;
  status/category groups, building/department/location/status filters, record
  pagination, and invalid subject clauses are covered.

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
