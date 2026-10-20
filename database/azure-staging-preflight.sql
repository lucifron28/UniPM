-- Read-only checks for the pre-provisioned Azure SQL staging database.
-- Connect explicitly to sql-unipm-staging.database.windows.net / unipm-staging-db.
-- This script contains no CREATE, ALTER, DROP, INSERT, UPDATE, or DELETE statements.
SET NOCOUNT ON;

SELECT
    DB_NAME() AS CurrentDatabase,
    N'unipm-staging-db' AS ExpectedDatabase,
    CASE WHEN DB_NAME() = N'unipm-staging-db' THEN N'MATCH' ELSE N'MISMATCH' END AS DatabaseNameCheck,
    TRY_CONVERT(int, SERVERPROPERTY('EngineEdition')) AS EngineEdition,
    CASE WHEN TRY_CONVERT(int, SERVERPROPERTY('EngineEdition')) = 5 THEN N'Azure SQL Database'
         ELSE N'Not Azure SQL Database (expected EngineEdition 5)' END AS EngineCheck,
    SERVERPROPERTY('ServerName') AS ReportedServerName,
    SERVERPROPERTY('IsFullTextInstalled') AS IsFullTextInstalled,
    DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS DatabaseCollation,
    db.compatibility_level AS CompatibilityLevel,
    USER_NAME() AS CurrentDatabasePrincipal
FROM sys.databases AS db
WHERE db.database_id = DB_ID();

SELECT
    IS_ROLEMEMBER(N'db_owner') AS IsDbOwner,
    IS_ROLEMEMBER(N'db_ddladmin') AS IsDbDdlAdmin,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL') AS HasDatabaseControl,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER') AS HasDatabaseAlter,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE TABLE') AS CanCreateTable,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CREATE FULLTEXT CATALOG') AS CanCreateFullTextCatalog,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER ANY FULLTEXT CATALOG') AS CanAlterAnyFullTextCatalog;

SELECT
    COUNT_BIG(*) AS UserObjectCountExcludingEfHistory,
    SUM(CASE WHEN obj.type = 'U' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS UserTableCountExcludingEfHistory
FROM sys.objects AS obj
WHERE obj.is_ms_shipped = 0
  AND NOT (obj.schema_id = SCHEMA_ID(N'dbo') AND obj.name = N'__EFMigrationsHistory');

SELECT
    schema_name(schema_id) AS SchemaName,
    name AS ObjectName,
    type_desc AS ObjectType
FROM sys.objects
WHERE is_ms_shipped = 0
  AND NOT (schema_id = SCHEMA_ID(N'dbo') AND name = N'__EFMigrationsHistory')
ORDER BY schema_name(schema_id), name;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT
        N'Absent' AS MigrationHistoryState,
        CONVERT(bigint, 0) AS AppliedMigrationCount,
        CONVERT(nvarchar(150), NULL) AS LatestMigrationId,
        N'20261010141251_AddInspectionPhotoEvidence' AS ExpectedLatestMigrationId;
END
ELSE
BEGIN
    SELECT
        N'Present' AS MigrationHistoryState,
        COUNT_BIG(*) AS AppliedMigrationCount,
        MAX(MigrationId) AS LatestMigrationId,
        N'20261010141251_AddInspectionPhotoEvidence' AS ExpectedLatestMigrationId
    FROM dbo.__EFMigrationsHistory;

    SELECT MigrationId, ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;
END;

SELECT name AS FullTextCatalogName
FROM sys.fulltext_catalogs
ORDER BY name;
