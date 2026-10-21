# GSD validation dataset

This deterministic validation fixture is fictional and intended for separate
local and Azure SQL targets. The PDF forms provide layout guidance only; no
real asset records, inspection entries, personnel, signatures, or equipment
locations were imported. A WMS number is a synthetic external handoff record,
not evidence of a completed repair.

## Expected baseline

| Measure | Expected count |
| --- | ---: |
| Fire extinguishers | 62 |
| Fire alarm systems | 62 |
| Emergency lights | 62 |
| Water drinking stations | 62 |
| Assets | 248 |
| Persisted schedules | 2,232 |
| Completed inspections | 1,932 |
| On-time inspections | 1,449 |
| Late inspections | 483 |
| Operational inspection rows | 1,545 |
| Non-operational inspection rows | 387 |
| Forms | 220 |
| Submitted forms | 110 |
| Acknowledged forms | 110 |
| WMS referral records | 103 |
| WMS referral revision-1 audit rows | 103 |

The 1,932 inspection rows are not all official-history records. Submitted
forms remain outside official history; acknowledged-form rows are eligible.
Each synthetic WMS reference is attached to a qualifying acknowledged finding
and has a matching revision-1 audit row. The fixture records no repair outcome.

The original 200 asset IDs and codes remain stable. The 48 additional assets
add four assets per category to each of General Services Department (`GSD`),
Procurement (`PROC`), and ICTD. Those departments use natural names in the
Department field and compact codes in asset codes, such as `FE-GSD-001`. All
three share the owner-confirmed Library Complex building label with the
Library department. Internal floors, rooms, wings, and equipment placements
are synthetic assumptions, not a verified campus floor plan or inventory.

Every asset is registered on January 1, 2024 so the historical schedules fall
after registration. The fixture includes the valid recurring cycles for
2024–2026: fire extinguishers and water drinking stations in February, May,
August, and November; fire alarm systems and emergency lights in June and
December. It contains 744 schedules for each of 2024 and 2025, all completed.
For 2026, November and December contribute 248 upcoming schedules. Four
13-row batches remain due and unassigned for live workflow validation:

| Category | Department | PM cycle | Example asset |
| --- | --- | --- | --- |
| Fire extinguishers | CCMS | 2026-08 | `FE-CCMS-001` |
| Water drinking stations | Administration | 2026-08 | `WDS-ADMINISTRATION-001` |
| Fire alarm systems | Library | 2026-06 | `FA-LIBRARY-001` |
| Emergency lights | CNAHS | 2026-06 | `EL-CNAHS-001` |

These live batches contain schedules only: no inspection, completed row, or PM
form is pre-created. For a controlled live non-operational scenario, mark
`FE-CCMS-001` non-operational during the session rather than pre-seeding a
finding. Complete the batch through normal assignment, inspection, submission,
acknowledgement, and WMS referral workflows.

## Representative analytics expectations

The following values follow the fixture and existing schedule semantics; no
dashboard or NLA query execution is claimed here:

- Inventory for fire extinguishers: 62 assets.
- Fire extinguisher schedules for CCMS in `2026-08`: 13 eligible and 0
  completed at the initial baseline.
- The four live batches each begin with 13 due, unassigned schedules.
- Official history includes acknowledged-form rows only; submitted-form rows
  remain excluded.

Run the focused seeded-dataset test and later report checks before treating
these expectations as executed evidence.

## Guarded seed commands

Apply the repository's current migrations through the approved operator tooling
before invoking either command. Neither command applies migrations. Run each as
a short-lived `Development` process after validating the target:

```powershell
dotnet run --project server/server.csproj --no-launch-profile -- --seed-gsd-validation-users
dotnet run --project server/server.csproj --no-launch-profile -- --seed-gsd-validation
```

Both commands require `UNIPM_STAGING_DEMO_INITIALIZATION=true`,
`UNIPM_GSD_VALIDATION_SEED_ENABLED=true`, and
`UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION` equal to the configured
`DataSource/Database`. User creation also requires distinct values for
`UNIPM_STAGING_ADMIN_PASSWORD`, `UNIPM_STAGING_GSD_PASSWORD`,
`UNIPM_STAGING_INSPECTOR_PASSWORD`, and
`UNIPM_STAGING_SUPERVISOR_PASSWORD`, unless the existing explicit shared
password mode is selected. Never put passwords in scripts or Git.

The seeders require the exact current compiled migration history, SQL Server
compatibility level 150, local SQL Server or Azure SQL Database, Full-Text
Search, and an empty mapped-data target. The account command creates the five
application roles and four active `.test` role accounts. The dataset command
accepts only those expected accounts and otherwise-empty mapped tables. Azure
SQL also requires explicit encryption and `TrustServerCertificate=False`.
Each seed uses a transaction; a repeated or partially initialized target is
rejected for manual review and is never reset.

The two environments must be seeded independently. Do not point the local
backup at Azure SQL; local and Azure changes do not synchronize. No database
reset or seed execution was performed while preparing this manifest.
