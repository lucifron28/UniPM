---
id: TEST-090
type: test-run
title: Azure staging demo initialization local safety verification
status: executed
recordedAtUtc: 2026-10-17T18:00:57Z
testedCommit: 71b04db5d14fc146ab72eae6c3e01ff2eb0d6420
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Staging demo initialization preparation

## Authorization and execution boundary

Preparation and local verification only. No live Azure connection, migration,
initialization, firewall change, provisioning, deployment, or merge occurred.
TEST-089's owner-verified empty Azure state was not rechecked or changed.
Live Azure initialization remains separately authorized work.

The original request required independent staging credentials. The owner
subsequently explicitly selected **shared password for all four accounts**.
The implementation retains independent passwords as the default and permits
the shared temporary-demo exception only through an explicit operator switch.
No actual supplied credential was used in these tests, saved, or committed.
Test credentials were generated in memory and discarded.

## Initial source audit

- `--seed-development-users` was Development-only, used one process password
  for five `.local` users by default, and supported skipping the legacy
  Department Head login. Existing passwords were not replaced, but roles,
  display names and active state could be repaired. Account writes could
  partially complete.
- `--seed-demo` did not create users or change passwords. It resolved local
  Inspector/GSD users, called `MigrateAsync` during context setup, then
  removed/recreated the demo-owned records inside an operational-data
  transaction. Repeated local seeding preserved resulting counts, but was
  unsuitable for staging preservation.
- Maintenance commands return before HTTP hosting. Identity requires at least
  12 characters, upper/lowercase, digit and non-alphanumeric characters.
- Existing data contains fictional identities, equipment, locations,
  inspection outcomes and placeholder signatures. No real inspection photos,
  institutional credentials or private personnel input are seeded.

## Credential and initialization design

`UNIPM_STAGING_DEMO_INITIALIZATION=true` selects a guarded profile within the
existing Development maintenance commands. The public host remains Production.
The four accounts are `admin@unipm.test`, `gsd@unipm.test`,
`inspector@unipm.test`, and `supervisor@unipm.test`, with their existing roles.
Five role definitions remain for contract compatibility; there is no Department
Head user. Existing local development behavior remains available.

Default mode requires four `UNIPM_STAGING_<ROLE>_PASSWORD` process variables.
Missing, invalid or duplicate values fail before writes. The account seeder
also validates every credential using the configured Identity validators before
creating any role or user. Explicit `-AllowSharedDemoPassword` selects one
`UNIPM_STAGING_DEMO_PASSWORD` through the child-only shared-mode flag. Mixed
shared/per-role credentials are rejected. Shared mode provides no password
isolation between demo roles and is not an institutional credential policy.

Both staging seed commands verify the exact 22 applied migrations and expected
table contents. They never apply migrations, remove demo records, reset,
reactivate accounts, repair assignments, or change existing passwords. User
initialization requires empty model tables. Demo initialization requires only
the exact four active accounts and their role definitions/assignments, with all
operational tables empty. Repeated and partial initialization is rejected.

The wrapper preserves exact target, current clean source/package provenance,
SQL authentication, encryption/certificate validation, App Service rejection,
and interactive writer-stop confirmation. It adds actual logical-server,
Full-Text availability and exact application-table checks. It runs users before
data, captures child output privately, stops on failure, restores temporary
SQLCMDPASSWORD and clears child environment references. A read-only postflight
checks identities, counts and migration summary. No credential enters arguments
or output. The documented caller restores all credential/connection variables.

The preflight is not a lock. Writers must remain stopped. The two commands are
not a single atomic transaction. A failed account step can leave partial users;
a failed data step can leave completed accounts. Recovery requires diagnosis
and approval, never automatic reseeding or deletion.

## Executed verification

Initial source `c37a28698e4b15ddb2978269dc9218c4ab5bc065` failed compilation
before any tests ran: the new test omitted the namespace for
`CpmpScheduleFrequency`. Commit `71b04db5d14fc146ab72eae6c3e01ff2eb0d6420`
adds only that import. The permitted focused rerun passed.

```text
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore
  --filter "FullyQualifiedName~StagingDemoInitializationTests|FullyQualifiedName~DevelopmentUserSeederTests|FullyQualifiedName~DevelopmentDemoSeederTests|FullyQualifiedName~AuthEndpointsTests"
  --logger "trx;LogFileName=staging-initialization.trx"
  --results-directory artifacts/staging-initialization/71b04db
  -m:1 /nodeReuse:false
```

Result: **37 passed, 0 failed, 0 skipped**, exit 0, reported duration 29 seconds.
This comprises 25 authentication cases, 3 legacy user-seeding cases, 6 legacy
demo-seeding cases, and 3 new staging cases. Two new cases execute native SQL.

The SQL fixtures reject remote/cloud targets and create GUID-named disposable
databases on the local native SQL Server only. They verified major version 15,
Full-Text installed, and compatibility 150. Each initially applies only 21
migrations and proves account initialization rejects that schema without
advancing it. Fixture setup then applies migration 22 locally and checks:

- invalid credentials leave roles/users empty;
- four active users receive the expected role;
- each independent password verifies only its own user, while explicit shared
  mode supports the four users with the one generated test credential;
- users-only restart and partial operational data are rejected without deletion;
- local test-only removal of the inserted partial fixture restores the intended
  users-only phase, after which demo initialization succeeds;
- 25 assets, 105 schedules, 77 inspection rows, 42 forms, 29 acknowledgements;
- all schedule category/month pairs follow the unchanged CPMP catalog;
- SQL foreign keys/check constraints accept the complete fictional dataset;
- no inspection photos, no duplicate seeding, reset rejected;
- original password hashes and the exact 22 migration IDs remain unchanged.

Both databases were dropped in fixture cleanup. A separate local, read-only
master query confirmed **0 remaining `UniPM_StagingSeedTest_` databases**.
The application's existing database was not used.

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-staging-initialization-guards.ps1
```

Windows PowerShell passed syntax and **10 local credential/safety checks**,
exit 0. It extracts only the credential function, exercises generated process
credentials, validates wrong-target rejection, source safety flags and command
order, and restores the prior environment. No SQL/seed process is invoked.
The guide's operator PowerShell block also passed parser validation.
`git diff --check` passed.

Reviewed raw output remains ignored:

- `artifacts/staging-initialization-test.log`, initial compile failure;
- `artifacts/staging-initialization-rerun.log`;
- `artifacts/staging-initialization/71b04db/staging-initialization.trx`.

Build warnings remain: existing Pkcs 9.0.4 vulnerability advisories and an
existing nullable warning. EF1002 flags the new metadata-table count query:
its table identifier comes exclusively from the EF model, and closing brackets
are escaped before identifier quoting. It accepts no operator identifier or
user input. No database/model/migration or authentication contract changed.

## Procedure and limitations

The [deployment guide](../../guides/azure-staging-deployment.md#fictional-demo-initialization-separate-approval-required)
contains the Windows hidden-prompt procedure, shared-mode exception, current-IP
hotspot access, environment cleanup, postflight, and partial-failure stop rule.
The App Service template explicitly excludes seeding credentials and flags.

No large dataset addition was needed. Existing synthetic cases demonstrate
pending and completed work, Draft/Submitted/Acknowledged, late/on-time results
and all four PM categories. Seeded data is not evidence of photo upload,
physical-device geolocation or institutional acceptance.

**NOT VERIFIED:** full wrapper success against Azure, actual Azure seeding,
an exact-head release package containing these changes, App Service hosting,
live account login, Blob permissions/photo persistence, or deployment. The SQL
postflight's Azure-specific guards were source-inspected, not run against live
Azure. Full backend/web/Flutter suites were not requested or rerun. Push/CI
status is reported separately after publication.

Next authorized operator work must first package the clean final commit and
obtain explicit approval for live demo initialization with all writers stopped.
Do not rerun the already completed Azure migrations. Status remains unmerged
and undeployed; the live database remains unseeded by this task.
