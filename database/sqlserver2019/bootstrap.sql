IF DB_ID(N'UniPMDb') IS NULL
BEGIN
    PRINT 'Creating empty UniPMDb database for the SQL Server 2019 compatibility spike.';
    CREATE DATABASE [UniPMDb];
END
ELSE
BEGIN
    PRINT 'UniPMDb database already exists for the SQL Server 2019 compatibility spike.';
END;
GO

IF (SELECT compatibility_level FROM sys.databases WHERE name = N'UniPMDb') <> 150
BEGIN
    PRINT 'Setting UniPMDb compatibility level to SQL Server 2019 (150).';
    ALTER DATABASE [UniPMDb] SET COMPATIBILITY_LEVEL = 150;
END;
GO

IF (SELECT compatibility_level FROM sys.databases WHERE name = N'UniPMDb') <> 150
    THROW 51020, 'UniPMDb compatibility level must be 150 for the SQL Server 2019 compatibility spike.', 1;
GO
