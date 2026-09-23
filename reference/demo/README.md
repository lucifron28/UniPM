# UniPM fictional presentation data

This fixture prepares a repeatable local demonstration of the preventive-
maintenance workflow. Every person, asset, finding, recommendation, and
signature in it is fictional. It is available only when the API runs in the
`Development` environment.

## Prepare the demo

Use a dedicated local SQL Server database. Set the password and connection
string only in the current PowerShell process, then run the preparation script
from the repository root:

```powershell
$env:UNIPM_DEV_USER_PASSWORD = "<temporary-local-password>"
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPM_Demo;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"

.\scripts\prepare-demo.ps1
```

The script applies migrations, creates or repairs the Development users,
restores the deterministic demo scenarios, and regenerates the QR files. It is
safe to run again. It does not enable the inactive maintenance-review feature
or contact an AI provider.

The login accounts are:

- `inspector@unipm.local`
- `gsd@unipm.local`

Both use the password supplied through `UNIPM_DEV_USER_PASSWORD`. Do not place
that password in this file, screenshots, or recordings.

`--seed-development-users` does not change the password of an existing
Development account. When reusing a demo database, use the password already
configured for those accounts.

## Demo scenarios

### A. Live mobile workflow

The Inspector's **My PM Tasks** list contains three Due fire-extinguisher
schedules for CCMS and PM cycle `2026-09`. They have no inspections or form at
the start of the demo.

| Asset code | QR payload | Location |
| --- | --- | --- |
| `DEMO-FE-001` | `UNIPM-DEMO-FE-001` | Ground Floor Computer Laboratory |
| `DEMO-FE-002` | `UNIPM-DEMO-FE-002` | Second Floor Faculty Room |
| `DEMO-FE-003` | `UNIPM-DEMO-FE-003` | Third Floor Network Laboratory |

Use these assets for QR scan, code lookup, inspection entry, batch progress,
and whole-form submission.

### B. Submitted acknowledgement review

The Library fire-extinguisher batch for `2026-08` contains three completed
inspections in one Submitted form. Two were completed on time and one was late.
The dashboard therefore shows:

- Scheduled: `3`
- Inspected: `3`
- On time: `2`
- Late: `1`
- On-time compliance: `66.67%`
- Batch state: awaiting acknowledgement

The asset codes are `DEMO-LIB-FE-001`, `DEMO-LIB-FE-002`, and
`DEMO-LIB-FE-003`.

### C. Acknowledged official history

The Student Affairs Office emergency-light batch for `2026-07` contains three
completed inspections in an Acknowledged form. Its fictional acknowledgement
was captured after field work and submission. These three rows are projected
into official history; the Submitted Library rows are not.

The asset codes are `DEMO-EL-001`, `DEMO-EL-002`, and `DEMO-EL-003`.

## QR sheet

Open [qr/index.html](qr/index.html) in a browser at 100% zoom. Each card uses a
large PNG intended for scanning directly from a laptop display. The text under
the code shows the exact persisted payload. Individual PNG files are in the
same directory.

Every demo asset has one unique QR payload. Running `prepare-demo.ps1`
regenerates the committed sheet from the same catalog used by the database
seeder.

## Connect a physical Android phone

The simplest USB setup forwards the phone's port `5254` to the development
computer's port `5254`:

```powershell
adb devices
adb reverse tcp:5254 tcp:5254

dotnet run --project server

Set-Location mobile
flutter run --dart-define=UNIPM_API_BASE_URL=http://127.0.0.1:5254/
```

Keep the phone connected and authorize USB debugging when Android asks. The
HTTP address is for a Flutter debug build and local development only. Release
builds must use HTTPS.

If USB forwarding is unavailable, bind the development API to a trusted LAN
interface and use a reachable address:

```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5254"
dotnet run --project server

Set-Location mobile
flutter run --dart-define=UNIPM_API_BASE_URL=http://<development-machine-lan-ip>:5254/
```

The phone and computer must be on the same trusted network, and the local
firewall must allow the development port. Do not commit the machine's LAN
address.

## Restore the pristine starting state

Live testing changes Scenario A. Restore all three scenarios before the
presentation:

```powershell
dotnet run --project server -- --reset-demo
dotnet run --project server -- --seed-demo
dotnet run --project tools/UniPM.DemoQrGenerator -- --output reference/demo/qr
```

The pristine state has three Due Scenario A schedules and no Scenario A
inspections; one Submitted and unacknowledged Scenario B form; and one
Acknowledged Scenario C form with three official-history documents and no
embeddings.
