# Run UniPM Locally

This tutorial takes a new contributor from a clean checkout to a running UniPM
API with fictional development data, then starts the web and mobile clients.
It uses the project's supported local database path: native Windows SQL Server
2019 with Full-Text Search.

## Before You Start

Install or have access to:

- .NET SDK compatible with the solution;
- native Windows SQL Server 2019 with Database Engine Services and Full-Text
  Search;
- Node 22 for the React web client;
- Flutter and the Android toolchain for the mobile client, if mobile work is
  needed;
- PowerShell and Git.

The default database must report major version `15`, Full-Text Search installed,
and compatibility level `150`. Docker is optional development tooling and is not
the target deployment architecture.

## 1. Configure the Local Database

Use process-scoped configuration. Windows Authentication is the preferred local
example, and the values below are examples rather than committed settings:

```powershell
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPMDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<temporary-development-password>"
```

For a named SQL Server instance, use `Server=localhost\INSTANCE_NAME` instead
of `Server=.`. Do not commit passwords, connection strings, or local `.env`
files.

Verify the instance before continuing:

```sql
SELECT
    SERVERPROPERTY('ProductMajorVersion') AS ProductMajorVersion,
    SERVERPROPERTY('IsFullTextInstalled') AS IsFullTextInstalled;

SELECT compatibility_level
FROM sys.databases
WHERE name = N'UniPMDb';
```

Expected development values are major version `15`,
`IsFullTextInstalled = 1`, and compatibility level `150`.

## 2. Restore, Migrate, and Seed Fictional Data

From the repository root:

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet ef database update --project server
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --rebuild-maintenance-search-documents
```

The synthetic data is fictional and intended for development and verification.
The seed creates 20 assets, 34 schedules, and 30 inspections. Development-user
seeding creates the five provisional roles used by the local authentication
scaffold.

Embeddings are disabled by default. Only run the embedding rebuild after a
separately configured provider has been reviewed:

```powershell
dotnet run --project server -- --rebuild-maintenance-embeddings
```

## 3. Start the API

In a new PowerShell window, configure the same process-scoped values and run:

```powershell
dotnet run --project server
```

The web and mobile clients communicate with this API. They do not connect to
SQL Server, an embedding provider, or a summary provider directly.

## 4. Start the Web Client

In another terminal:

```powershell
cd web
npm ci
npm run dev
```

The development web origin is normally `http://localhost:5173`. Keep the exact
origin aligned with the backend CORS configuration. The web client uses the
generated API client and the backend's browser refresh-cookie contract.

## 5. Start the Mobile Client

The Flutter app is Android-first and currently supports memory-only
authentication plus the initial Draft preventive-maintenance form workflow:

```powershell
cd mobile
flutter pub get
flutter run --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/
```

`10.0.2.2` routes an Android emulator to the development machine. A physical
device needs a reachable LAN address or an approved HTTPS development setup.
HTTP is permitted only in debug builds for local development; release builds
must use HTTPS.

The mobile app starts signed out after restart, keeps access tokens in memory,
and does not persist cookies or implement offline synchronization. Offline sync
is deferred and its persistence and synchronization architecture remain
undecided until a separate approved decision.

## Where to Go Next

- [Local stack and projection rebuild](../how-to/run-local-stack.md)
- [System capabilities reference](../reference/system-capabilities.md)
- [Architecture and RAG boundaries](../explanation/architecture-and-rag-boundaries.md)
- [Confirmed GSD workflow](../../planning/confirmed-gsd-workflow.md)
- [Evidence rules](../../evidence/README.md)

This tutorial does not establish IIS production readiness, institutional source
authorization, real-data privacy readiness, or real-provider model quality.
