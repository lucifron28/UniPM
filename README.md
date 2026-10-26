# UniPM

UniPM is a web and mobile preventive-maintenance system for a university
General Services Department. It connects asset records, maintenance schedules,
field inspections, form acknowledgement, and reporting in one workflow.

The web application supports administrative work, scheduling, monitoring and
record review. The Android mobile application supports asset scanning,
inspection checklists and form submission in the field.

## Features

- Asset registry with QR-based lookup and category-specific inspection checklists.
- Preventive-maintenance schedules, supervisor and inspector assignments, and progress monitoring.
- Multi-asset inspection forms with whole-form submission and acknowledgement.
- Encrypted offline Draft storage, queued synchronization and conflict review on mobile.
- Inspection photos, acknowledged maintenance history and corrective-action handoff preparation.
- Dashboards and natural-language reports for PM execution and registered asset inventory.
- Role-based access for GSD personnel, supervisors, inspectors and technical administrators.

UniPM covers fire extinguishers, fire alarm systems, emergency lights and
water drinking stations. Corrective-action handoffs support manual processing
in the GSD's existing Work Management System.

## Maintenance workflow

A preventive-maintenance form contains inspection rows for one or more assets.
Forms follow this lifecycle:

```text
Draft -> Submitted -> Acknowledged
```

Inspectors record each asset's checklist and findings. The first inspection
save accepted by the server completes its linked schedule. Submission sends
the whole form for acknowledgement, and one signature records receipt of the
form. Acknowledged rows become part of official maintenance history.

Mobile users can edit cached Draft work offline and synchronize it when
connected. Submission, photo upload and acknowledgement require a connection.
Timeliness uses the server-recorded completion time, so delayed synchronization
can affect whether work is classified as on time.

Natural-language analytics converts a question into a structured report plan.
Users review its scope and confirm execution; the backend validates the plan
and calculates the results. The deterministic parser works without an external
AI service. Optional DeepSeek interpretation is available when explicitly
enabled in Development. Core maintenance workflows do not require AI.

## Technology

| Component | Stack                                                               |
| --------- | ------------------------------------------------------------------- |
| API       | ASP.NET Core Web API, Entity Framework Core, .NET 10                |
| Database  | SQL Server 2019 or later, compatibility level 150, Full-Text Search |
| Web       | React, TypeScript, Vite                                             |
| Mobile    | Flutter, Android                                                    |
| Testing   | xUnit, Vitest, Playwright, Flutter tests                            |

The backend controls database access, authentication, report validation and
calculations. Both clients communicate through its API.

## Repository structure

```text
server/       ASP.NET Core API and database migrations
web/          React web application
mobile/       Flutter mobile application
database/     Database setup scripts
tests/        Backend tests
scripts/      Local launchers and development utilities
reference/    Guides, architecture documents and research records
.github/      CI workflows
```

## Run locally

The following setup uses Windows and PowerShell. Install:

- .NET 10 SDK, as specified in `global.json`.
- SQL Server 2019 or later with Database Engine Services and Full-Text Search.
- Node.js 22 and npm.
- Flutter and Android tooling for the mobile application, with an emulator or connected phone.

### Set up the database

From the repository root, configure a local development database and a
password for the synthetic development accounts:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=UniPMDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<choose-a-development-password>"

dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
```

Use a password with at least 12 characters, including uppercase and lowercase
letters, a number and a symbol. Replace the SQL Server instance name if needed.
The database must use compatibility level `150`.

The seed commands create fictional maintenance records and local accounts such
as `gsd@unipm.local`, `supervisor@unipm.local` and `inspector@unipm.local`.
Sign in with the password you configured. Seeding is an explicit Development
operation and does not run when the API starts normally.

Keep credentials in process environment variables or the ignored root `.env`.
The launcher can read database and JWT settings from `.env`; see
[.env.example](.env.example) for the available settings. Replace its example
values and keep configured files out of Git.

### Start the API and web application

Install the web dependencies once:

```powershell
npm --prefix web ci
```

Open two PowerShell windows at the repository root. In the first, ensure the
database connection is configured and start the API:

```powershell
.\scripts\start-server.ps1
```

In the second, start the web application:

```powershell
.\scripts\start-web.ps1
```

Open [http://localhost:5173](http://localhost:5173). The API listens on
`http://localhost:5254`, with health checks at `/health/live` and
`/health/ready`. Stop either process with Ctrl+C.

### Start the mobile application

Install Flutter dependencies, then launch with a connected Android device or
running emulator:

```powershell
Push-Location mobile
flutter pub get
Pop-Location
.\scripts\start-mobile.ps1
```

The launcher selects the local API address for the emulator or uses ADB port
forwarding for a connected phone. See [mobile/README.md](mobile/README.md) for
custom API addresses, device selection and release signing. Release builds
require an HTTPS API address.

## Development checks

Run checks for the component being changed:

```powershell
# Backend
dotnet build UniPM.slnx
dotnet test UniPM.slnx

# Web
Push-Location web
npm run lint
npm run test:run
npm run build
Pop-Location

# Mobile
Push-Location mobile
flutter analyze
flutter test
Pop-Location
```

SQL Server integration tests and live browser workflows require their explicit
fixture configuration. Ordinary test runs skip unavailable opt-in checks.

## Documentation

- [Web application setup and API client](web/README.md)
- [Mobile setup and offline workflow](mobile/README.md)
- [Maintenance workflow](reference/planning/confirmed-gsd-workflow.md)
- [API contract](web/openapi/unipm-v1.json)
- [Engineering test and evaluation records](reference/evidence/INDEX.md)
- [Institutional hosting and maintenance proposal](reference/planning/capstone-institutional-operations.md)

## License

UniPM is proprietary source-available software. The public repository supports
academic review, demonstration and portfolio viewing. Use, redistribution,
modification or institutional operation requires permission under
[LICENSE.md](LICENSE.md).
