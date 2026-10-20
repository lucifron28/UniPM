# Azure SQL Database migration audit

This is a source audit of the 22 EF Core migrations present at commit `0a8ae6a677a92c1c4969eeaa6df41c62ccbfce7d`, from `20260623145921_InitialDomainSchema` through `20261010141251_AddInspectionPhotoEvidence`. It does not certify a deployment. No Azure SQL database was contacted and no migration or preflight query was executed against Azure.

## Subsequent live verification, October 18, 2026

The statement above describes the original audit only. The owner subsequently
approved and executed this unchanged 22-migration chain at source
`c3f9d4a1d8eebb8db607f3d257095d9f8e20111a` against the existing staging
Azure SQL database. Fresh preflight reported `IsFullTextInstalled=1`,
compatibility 150, the expected collation, and an empty target. Migration
returned exit code 0. A separate read-only follow-up verified every expected
migration ID, all 22 empty model tables, enabled/trusted constraints, and the
expected final Full-Text Search state. See
[TEST-089](../evidence/test-runs/TEST-089-azure-sql-staging-migration.md).
The installation guard is therefore verified for that target/source; the
original audit concerns below remain guidance for other targets. No historical
migration or production schema definition was edited to obtain success.

## Findings

The migrations use the EF Core SQL Server provider and SQL Server types, constraints, indexes, and T-SQL. The reviewed chain has no PostgreSQL-specific migration. Most operations are ordinary in-database DDL. Seven migrations contain hand-written SQL; three migrations use Full-Text Search DDL or its installation guard. The full-text index statements are marked `suppressTransaction: true`, matching Microsoft’s requirement that `CREATE FULLTEXT INDEX` run outside a user transaction.

| # | Migration | Main operations | Review flag |
|---:|---|---|---|
| 1 | `20260623145921_InitialDomainSchema` | Assets, schedules, inspection records, keys and indexes | Initial relational schema |
| 2 | `20260710170229_AddMaintenanceSearchDocuments` | Legacy maintenance-search projection and indexes | Later retired by #17 |
| 3 | `20260711001300_EnforceDomainContracts` | Normalize existing values; enforce lengths, allowed values, uniqueness and lookup indexes | **T-SQL/data rewrite**; preflight data before applying to a populated database |
| 4 | `20260711120000_AddMaintenanceFullTextSearch` | Maintenance search full-text catalog and index | **Full-Text Search guard/DDL**; outside-transaction operation |
| 5 | `20260711123548_AddMaintenanceSearchDocumentEmbeddings` | Legacy projection embedding table and index | Later retired by #17 |
| 6 | `20260713001356_AddIdentityAuthentication` | Identity users, roles, claims, logins, tokens and indexes | Identity relational schema |
| 7 | `20260717044115_EnforceOneInspectionPerSchedule` | Reject duplicate schedule inspections, then create unique index | **T-SQL/data-dependent guard** |
| 8 | `20260717153842_AddRefreshSessions` | Refresh-session table and indexes | Identity/auth relational schema |
| 9 | `20260725082333_AddReferenceDocumentFoundation` | Fictional reference-document tables, indexes, and section full-text catalog/index | **Full-Text Search guard/DDL**; outside-transaction operation |
| 10 | `20260727100331_AddPreventiveMaintenanceFormDomain` | Preventive-maintenance forms, links, indexes, and constraints | Relational workflow schema |
| 11 | `20260907150158_AddWaterStationInspectionWorkItems` | Replace legacy water-station inspection columns with work-item fields | Column migration; verify any retained data before applying to a populated database |
| 12 | `20260917145243_SeparatePmExecutionFromAcknowledgement` | Remove acknowledgement-era columns from the earlier PM form shape | **Drops columns**; not a data-preserving rollback |
| 13 | `20260917172750_AddCanonicalPmCycleIdentity` | Add/backfill canonical schedule and form cycles; add format checks and filtered uniqueness | **T-SQL/backfill** and filtered unique index; preflight ambiguous or colliding rows |
| 14 | `20260923160152_AssignScheduleOversight` | Add schedule oversight reference, FK, and index | Relational FK/index |
| 15 | `20260926064226_AddInspectionLocationVerification` | Add inspection location verification metadata/table and indexes | Relational schema; review existing values before enforcement |
| 16 | `20260926165354_AddInspectionLocationQualityMetadata` | Adjust location-quality metadata columns | Column migration |
| 17 | `20260930123820_RetireMaintenanceHistoryRagStorage` | Drop retired maintenance search full-text index/catalog and projection tables | **Destructive Up**; Down recreates empty legacy tables and index, not deleted data |
| 18 | `20261009020518_AddUniqueScheduleAssetCycle` | Reject duplicate asset/cycle schedules, then add unique index | **T-SQL/data-dependent guard** |
| 19 | `20261009041512_AddInspectionWmsReferralTracking` | Corrective-action referral tracking tables and indexes | Relational schema; records preparation/tracking, not external WMS operations |
| 20 | `20261009182058_AddScheduleEnrollmentDeferrals` | Schedule-enrollment deferral table and index | Relational schema |
| 21 | `20261009234631_ReviewScheduleEnrollmentDeferrals` | Review deferrals, adjust indexes, remove superseded columns | **Drops columns**; verify retained data before applying to populated database |
| 22 | `20261010141251_AddInspectionPhotoEvidence` | Inspection photo evidence table and indexes | Relational schema; blob storage is outside this SQL migration |

The hand-written SQL is concentrated in domain canonicalization/length validation (#3), duplicate guards (#7 and #18), canonical cycle backfill (#13), and the Full-Text Search create/retire/restore paths (#4, #9, #17). Those are the places to inspect first if a target-specific migration error occurs. The duplicate guards intentionally stop rather than pick a row to discard. #3 and #13 can rewrite existing values. Review the exact `Up` operation and expected data state before applying this chain anywhere that already contains application data.

## Azure SQL Database fit and limits

Azure SQL Database supports Full-Text Search, including `CREATE FULLTEXT INDEX`; Microsoft documents SQL Server, Azure SQL Database, and Azure SQL Managed Instance for that statement. Full-Text Search support alone does not prove that this repository’s instance-installation guard works on Azure SQL Database. Two `Up` migrations call `SERVERPROPERTY('IsFullTextInstalled')` and throw unless it converts to `1` (#4 and #9); the rollback path for #17 has the same guard. Microsoft documents this property as reporting whether full-text components are installed on the current SQL Server instance, and documents `NULL` for properties that are invalid or not applicable. The read-only preflight captures the value but cannot establish the guard’s Azure behavior. A `NULL` or `0` means the migration will stop. Treat these guards as an unresolved Azure compatibility check until a reviewed target-specific migration plan is approved; do not edit historical migrations as part of this audit. [Full-Text Search support and syntax](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-fulltext-index-transact-sql?view=sql-server-ver15), [SERVERPROPERTY](https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql)

The project’s SQL Server 2019 validation baseline uses database compatibility level `150`. That is a database property, not something implied by the EF Core SQL Server provider or migration files. Azure SQL Database supports levels `100` through `170`; a newly created Azure SQL Database may default to `170`. The migrations do not set compatibility level. Record the actual target value and agree it with the staging plan before migrations. `STRING_AGG ... WITHIN GROUP` in #3 requires compatibility level 110 or higher for its ordering clause, so level 150 satisfies the specific syntax floor but is not a substitute for testing the full chain. Do not change compatibility level as part of this audit. [View or change compatibility level](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-compatibility-level?view=sql-server-ver17), [STRING_AGG](https://learn.microsoft.com/en-us/sql/t-sql/functions/string-agg-transact-sql?view=fabric)

The local evidence records native SQL Server 2019 with Full-Text Search installed and compatibility level 150. It is not Azure SQL evidence. Azure SQL Database reports `SERVERPROPERTY('EngineEdition') = 5`; the preflight also reports database name, compatibility, collation, migration history, existing user objects, and the current principal’s database permissions. The migrations do not specify a required collation; compare the target output with the local database and application expectations rather than assuming they match. Microsoft’s documented Full-Text Search permission requirements include `ALTER` on the indexed table and `REFERENCES` on its catalog, or a database role such as `db_owner`/`db_ddladmin`; provision least-privilege migration access and verify it before running DDL. [SERVERPROPERTY values](https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql), [Full-text index permissions](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-fulltext-index-transact-sql?view=sql-server-ver15)

## Preflight sequence

1. Confirm the connection is explicitly to the intended Azure SQL logical server and database `unipm-staging-db` on `sql-unipm-staging.database.windows.net`. The query reports database identity and `EngineEdition`; it does not validate a connection string or prove the endpoint name.
2. Before the first migration, require Azure SQL Database (`EngineEdition = 5`), expected database name, an agreed compatibility level and collation, adequate migration permissions, no user objects, and no `dbo.__EFMigrationsHistory` rows/table. Stop on any mismatch; do not treat an existing schema as an empty target.
3. After a separately authorized migration run, rerun the query. Verify the history contains all 22 expected migrations through `20261010141251_AddInspectionPhotoEvidence`, then review schema/data and Full-Text Search behavior before seeding or opening the application.
4. Preserve the query output as sanitized evidence tied to the exact source commit and target environment. Do not include credentials or connection strings.

The companion [`azure-staging-preflight.sql`](../../database/azure-staging-preflight.sql) contains only read-only queries. It does not create, alter, or delete database resources. No Azure migration was run for this audit.
