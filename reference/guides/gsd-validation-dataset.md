# GSD validation dataset

This is a deterministic, fictional validation fixture for preparing separate
local and Azure SQL targets. The PDF forms are guidance only; no real assets,
inspection entries, people, personnel signatures, or locations were imported.
Acknowledgement names and the small PNG signatures are synthetic. A WMS
reference means a fictional external handoff record, not a completed repair.

## Expected baseline

| Measure | Expected count |
| --- | ---: |
| Fire extinguishers | 50 |
| Fire alarm systems | 50 |
| Emergency lights | 50 |
| Water drinking stations | 50 |
| Assets | 200 |
| Persisted schedules | 500 |
| Completed inspections | 248 |
| On-time inspections | 186 |
| Late inspections | 62 |
| Operational inspection rows | 198 |
| Non-operational inspection rows | 50 |
| Forms | 20 |
| Submitted forms | 10 |
| Acknowledged forms | 10 |
| Acknowledged non-operational findings awaiting WMS referral | 12 |
| Acknowledged non-operational findings with WMS references | 15 |
| WMS referral revision-1 audit rows | 15 |

Submitted forms remain outside official history; the 248 inspection rows are
not all official-history records. The 15 fictional WMS PM numbers are attached
only to qualifying findings on acknowledged forms. Each referral has a
revision-1 audit row, and the fixture records no repair outcome.

Each category contains 50 assets distributed across CCMS, Administration,
Library, and CNAHS. Codes use category and department labels, for example
`FE-CCMS-001`. Identifiers are stable across separate targets and QR values
are unique. Building names follow the existing campus fixture vocabulary;
floor, wing, corridor, room, and equipment placements are synthetic assumptions,
not a verified MSEUF floor plan or inventory. Persisted cycles follow the existing CPMP frequencies. The 200
November/December schedules are upcoming; four additional 13-row batches are
left due and unassigned for the live workflow:

| Category | Department | PM cycle | Example asset |
| --- | --- | --- | --- |
| Fire extinguishers | CCMS | 2026-08 | `FE-CCMS-001` |
| Water drinking stations | Administration | 2026-08 | `WDS-ADMINISTRATION-001` |
| Fire alarm systems | Library | 2026-06 | `FA-LIBRARY-001` |
| Emergency lights | CNAHS | 2026-06 | `EL-CNAHS-001` |

These batches contain persisted schedules only: no inspection, completed row,
or PM form is pre-created for them. For a controlled live non-operational
scenario, mark `FE-CCMS-001` non-operational during the session rather than
pre-seeding a finding. Complete the batch through normal assignment, inspection,
submission, acknowledgement, and WMS referral workflows.

## Representative analytics expectations

The following values are derived from the fixture and existing schedule
semantics; no dashboard or NLA query execution is claimed here:

- Inventory for fire extinguishers: 50 assets.
- Fire extinguisher schedules for CCMS in `2026-08`: 13 eligible and 0
  completed at the initial baseline.
- The four live batches each begin with 13 due, unassigned schedules.
- Official history can include acknowledged-form rows only; submitted-form
  rows must remain excluded.

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
SQL also requires explicit encryption and
`TrustServerCertificate=False`. Each seed uses a transaction; a repeated or
partially initialized target is rejected for manual review and is never reset.

The two environments must be seeded independently. Do not point the local
backup at Azure SQL; local and Azure changes do not synchronize. No database
reset or seed execution was performed while preparing this manifest.
