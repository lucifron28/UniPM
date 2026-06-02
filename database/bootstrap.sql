IF DB_ID(N'UniPMDb') IS NULL
BEGIN
    PRINT 'Creating empty UniPMDb database.';
    CREATE DATABASE [UniPMDb];
END
ELSE
BEGIN
    PRINT 'UniPMDb database already exists.';
END;
GO
