# Domain Contract Hardening

Status: implemented on `refactor/domain-contracts`.

This reference records stable code contracts that can be enforced before lexical
FTS. Values remain readable strings in API payloads and SQL Server. Integer-backed
EF enums are intentionally not used.

## Persisted Codes

| Area | Persisted values | Current API-command values |
| --- | --- | --- |
| Asset category | `fire-extinguisher`, `fire-alarm`, `emergency-light`, `water-drinking-station` | all four on asset creation and filtering |
| Asset status | `Active`, `Inactive`, `Retired` | `Active` is the current asset-create default; no status transition command exists |
| Schedule period | `Quarter`, `Semester`, `Annual`, `Custom` | all four are accepted by schedule creation |
| Schedule status | `Due`, `Ongoing`, `Completed`, `Overdue`, `Cancelled` | `Due` on schedule creation and `Completed` when an inspection is recorded |
| Quarter | `Q1`, `Q2`, `Q3`, `Q4` | supplied quarter values on schedule creation |
| Semester | `First`, `Second`, `Summer` | no current write command; fixture/read-side data only |

Synthetic actor role tokens are seed-only fixture vocabulary:
`GSD_FIRE_INSPECTOR`, `ELECTRICAL_ENGINEER`, `PLUMBER`, `PPF_SUPERVISOR`, and
`DEPARTMENT_HEAD`. They are not authorization roles and are not runtime user
contracts.

## Canonicalization And Bounds

- Asset codes are trimmed, line-ending normalized, and stored in invariant
  uppercase with a 64-character maximum.
- QR values are trimmed and stored in invariant uppercase with a 128-character
  maximum. Generated values retain the `UNIPM-...` structure and use an uppercase
  eight-character ID token.
- Coded category/status/period/quarter/semester values accept case and surrounding
  whitespace at API boundaries, then persist and return their catalog spelling.
- Building, department, and location remain nullable free text with a technical
  256-character maximum. No official GSD list is being finalized.
- Inspection remarks and actions/recommendations remain nullable text with the
  existing 2,000-character API limit. `RemarksEmbedding` and
  `DescriptionEmbedding` remain untouched.

## Database Enforcement

The domain-contract migration audits existing rows in this order:

1. maximum lengths;
2. canonicalization of supported code spellings and nullable blank metadata;
3. unsupported-value checks;
4. duplicate checks after canonicalization;
5. column bounds, check constraints, and indexes.

Asset codes have a unique index. QR values have a filtered unique index for
non-null values. Schedule code checks do not encode status transitions or
unconfirmed GSD workflow rules.

## Deferred Boundaries

This hardening does not finalize building/department/location catalogs,
acknowledgement, RMRF, corrective-maintenance handoff, authority over schedule
changes, authentication roles, or any other unresolved GSD workflow.
