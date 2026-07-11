using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations;

public partial class AddMaintenanceFullTextSearch : Migration
{
    private const string CatalogName = "UniPMMaintenanceRetrieval";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            IF ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1
                THROW 51010, 'UniPM lexical retrieval requires SQL Server Full-Text Search to be installed and available.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.fulltext_catalogs
                WHERE name = N'{CatalogName}')
                CREATE FULLTEXT CATALOG [{CatalogName}] WITH ACCENT_SENSITIVITY = OFF;

            IF EXISTS (
                SELECT 1
                FROM sys.fulltext_indexes AS fullTextIndex
                INNER JOIN sys.tables AS tables
                    ON tables.object_id = fullTextIndex.object_id
                INNER JOIN sys.schemas AS schemas
                    ON schemas.schema_id = tables.schema_id
                WHERE schemas.name = N'dbo'
                  AND tables.name = N'MaintenanceSearchDocuments')
                THROW 51011, 'UniPM lexical retrieval full-text index already exists with an unexpected migration state.', 1;

            CREATE FULLTEXT INDEX ON [dbo].[MaintenanceSearchDocuments]
            (
                [SearchText] LANGUAGE 1033
            )
            KEY INDEX [PK_MaintenanceSearchDocuments]
            ON [{CatalogName}]
            WITH CHANGE_TRACKING = AUTO;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            IF EXISTS (
                SELECT 1
                FROM sys.fulltext_indexes AS fullTextIndex
                INNER JOIN sys.tables AS tables
                    ON tables.object_id = fullTextIndex.object_id
                INNER JOIN sys.schemas AS schemas
                    ON schemas.schema_id = tables.schema_id
                WHERE schemas.name = N'dbo'
                  AND tables.name = N'MaintenanceSearchDocuments')
                DROP FULLTEXT INDEX ON [dbo].[MaintenanceSearchDocuments];

            IF EXISTS (
                SELECT 1
                FROM sys.fulltext_catalogs
                WHERE name = N'{CatalogName}')
                DROP FULLTEXT CATALOG [{CatalogName}];
            """);
    }
}
