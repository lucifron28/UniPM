# GSD PMIS demonstration runbook

## Purpose

This runbook prepares the PMIS-only UniPM validation prototype for a GSD
demonstration. It covers the confirmed preventive-maintenance workflow. It does
not demonstrate production deployment, WMS integration, RMRF processing, AI,
offline synchronization, or institutional adoption.

Use fictional records and a dedicated demo database. Do not enter real
personnel, asset, signature, or maintenance data during the rehearsal or
recording.

Maintenance-history RAG is historical and inactive, so this runbook does not
demonstrate it. Natural-language analytics is a planned direction pending
professor/adviser confirmation and is also outside the walkthrough.

## Demonstration environment

The demonstration uses:

- native Windows SQL Server 2019 with Full-Text Search;
- database compatibility level `150`;
- the ASP.NET Core API on the development computer;
- the React web application in a desktop browser;
- the Flutter debug application on an Android device connected to the same
  trusted network.

Create a new database name for each rehearsal, such as
`UniPM_GsdDemo_20260913`. This avoids deleting or modifying an existing
development database.

In a PowerShell terminal at the repository root, set process-only values:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://0.0.0.0:5099"
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPM_GsdDemo_20260913;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"
$env:UNIPM_DEV_USER_PASSWORD = "<temporary-local-password>"
$env:MaintenanceReview__Enabled = "false"
$env:Embeddings__Enabled = "false"
```

Keep the password and connection string out of Git, screenshots, recordings,
shell history captures, and evidence records.

`Encrypt=False` is limited to this local SQL Server 2019 development setup. It
is not a production database transport recommendation.

## Prepare the database

Apply migrations and load the deterministic fictional fixture:

```powershell
dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --seed-synthetic
```

Do not run an embedding rebuild. The PMIS workflow does not need an embedding
or summary provider.

Confirm the database platform:

```sql
SELECT
    SERVERPROPERTY('ProductMajorVersion') AS ProductMajorVersion,
    SERVERPROPERTY('IsFullTextInstalled') AS IsFullTextInstalled;

SELECT compatibility_level
FROM sys.databases
WHERE name = N'UniPM_GsdDemo_20260913';
```

Expected values are `15`, `1`, and `150`.

## Start the applications

Start the API from the configured PowerShell terminal:

```powershell
dotnet run --project server
```

Start the web application from `web/`:

```powershell
npm run dev
```

For a physical Android device, replace `<development-machine-lan-ip>` with an
address reachable from the device:

```powershell
flutter run --dart-define=UNIPM_API_BASE_URL=http://<development-machine-lan-ip>:5099/
```

HTTP is allowed only by the Android debug manifest for local development.
Release builds require HTTPS. If the device cannot reach the API, confirm that
both devices use the same trusted network and that local firewall rules allow
the development API port. Do not weaken the release network policy.

## Preflight check

Before GSD arrives:

1. Open the API readiness endpoint and confirm it reports ready.
2. Open the web login page and sign in with the fictional GSD account.
3. Sign in on the mobile app with the fictional Inspector account.
4. Confirm Assets, Schedules, Official history, and Form review open without an
   error.
5. Confirm the web OpenAPI document contains no maintenance-review operation.
6. Confirm the mobile device can resolve a fictional asset by QR code.
7. Keep one unused pair of same-category schedules for the live form walkthrough.

The Development seeder creates fictional accounts for the documented roles.
All use the process-only password supplied to `UNIPM_DEV_USER_PASSWORD`.

## Demonstration walkthrough

Use two assets from the same department, asset category, and `PmCycle` so one
canonical PM batch and one digital form can contain both rows. Building does
not split the batch.

1. In the web application, show the asset records and their QR identifiers.
2. Show the two Due schedules linked to the selected assets and confirm that
   they share the same canonical batch: department, asset category, and
   `PmCycle`.
3. On mobile, scan or enter the first asset QR identifier.
4. Start a preventive-maintenance Draft and add the first inspection row.
5. Add a second same-category asset row to the same form.
6. Record one row as Operational and one as Non-operational. Give the
   Non-operational row a fictional remark and recommended corrective action.
7. Confirm that each completed inspection row has an
   `InspectionRecord.CompletedAt` timestamp and that its linked schedule is
   already `Completed`. Schedule completion comes from field work, before form
   submission or acknowledgement.
8. Submit the whole form. Point out that the backend assigns one provisional
   UniPM file number to the form while each row keeps its inspection ID.
9. In the web application, open Form review and inspect the Submitted form.
10. Capture a fictional department-head acknowledgement through the authenticated
   session.
11. Confirm that the form becomes Acknowledged while the linked schedules remain
    Completed from the earlier field-work step.
12. Open Official history and confirm the two rows appear only after
    acknowledgement.
13. As GSD, open the corrective-action handoff section. Confirm that it includes
    only the row with a recommended action and contains no signature payload.

Run a short second walkthrough for a water drinking station. Show Date
Accomplished and the carbon-filter, sediment-filter, and UV-light work items.

## File-number explanation

The displayed identifier is a provisional UniPM file number. It identifies one
submitted preventive-maintenance form inside UniPM. It is independent from the
existing GSD Work Management System and is not an RMRF or WMS request number.

If one acknowledged form contains several rows that may lead to separate work
requests, GSD still handles those requests outside UniPM. Do not claim a
one-to-one relationship between the UniPM number and any downstream number.

## Backup recording

Record a short backup video only after the live rehearsal passes. Use the same
fictional walkthrough. Hide passwords, connection strings, terminal history,
machine names, and unrelated browser tabs. Do not commit the recording to the
repository.

If the live device connection fails during the meeting, show the recording and
state that it is a prior controlled development run. Do not describe the video
as live execution.

## Readiness decision

Proceed with the GSD demonstration only when:

- the web and mobile applications use the same current API contract;
- the selected assets and schedules are available;
- the walkthrough schedules share one department, asset category, and `PmCycle`;
- completed inspection rows have `InspectionRecord.CompletedAt` values and
  linked schedules are completed before form acknowledgement;
- submission assigns a provisional UniPM file number;
- acknowledgement records receipt/noting, does not alter execution or compliance
  timestamps, and does not complete schedules;
- acknowledged rows appear in official history;
- Draft and Submitted rows remain absent from official history;
- no AI provider is configured or contacted;
- no real institutional record or signature is present; and
- the complete walkthrough has passed once on the intended physical device.
