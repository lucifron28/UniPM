-- Read-only verification after a separately approved initialization. No credentials or hashes returned.
SET NOCOUNT ON;
IF ISNULL(DB_NAME(), N'') <> N'unipm-staging-db'
   OR ISNULL(CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), N'') <> N'sql-unipm-staging'
   OR ISNULL(TRY_CONVERT(int, SERVERPROPERTY('EngineEdition')), 0) <> 5
    THROW 51100, N'Unexpected Azure staging target.', 1;
IF ISNULL((SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()), 0) <> 150
    THROW 51101, N'Unexpected compatibility level.', 1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 22
   OR NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261010141251_AddInspectionPhotoEvidence')
    THROW 51102, N'Unexpected migration history. Compare all IDs with the migration postflight.', 1;
IF (SELECT COUNT(*) FROM dbo.AspNetUsers) <> 4
   OR (SELECT COUNT(*) FROM dbo.AspNetUserRoles) <> 4
   OR EXISTS (
       SELECT expected.Email, expected.RoleName FROM (VALUES
           (N'admin@unipm.test', N'Admin'), (N'gsd@unipm.test', N'GSD'),
           (N'inspector@unipm.test', N'Inspector'), (N'supervisor@unipm.test', N'Supervisor')
       ) AS expected(Email, RoleName)
       EXCEPT SELECT u.Email, r.Name FROM dbo.AspNetUsers u
       JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
       JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId WHERE u.IsActive = 1)
    THROW 51103, N'Unexpected demonstration accounts or roles.', 1;
IF (SELECT COUNT(*) FROM dbo.Assets) <> 25
   OR (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceSchedules) <> 105
   OR (SELECT COUNT(*) FROM dbo.InspectionRecords) <> 77
   OR (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceForms) <> 42
   OR (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceAcknowledgements) <> 29
   OR EXISTS (SELECT 1 FROM dbo.InspectionPhotoEvidence)
    THROW 51104, N'Unexpected fictional demonstration record counts.', 1;
SELECT N'UNIPM_STAGING_DEMO_VERIFIED' AS [Result],
    (SELECT COUNT(*) FROM dbo.AspNetUsers) AS [Users],
    (SELECT COUNT(*) FROM dbo.Assets) AS [Assets],
    (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceSchedules) AS [Schedules],
    (SELECT COUNT(*) FROM dbo.InspectionRecords) AS [Inspections],
    (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceForms) AS [Forms],
    (SELECT COUNT(*) FROM dbo.PreventiveMaintenanceAcknowledgements) AS [Acknowledgements];
