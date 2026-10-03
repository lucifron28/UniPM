using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class RetireMaintenanceHistoryRagStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables
                        ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas
                        ON schemas.schema_id = tables.schema_id
                    INNER JOIN sys.fulltext_catalogs AS catalogs
                        ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                    WHERE schemas.name = N'dbo'
                      AND tables.name = N'MaintenanceSearchDocuments'
                      AND catalogs.name <> N'UniPMMaintenanceRetrieval')
                    THROW 51012, 'Maintenance search document has a full-text index in an unexpected catalog.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.fulltext_catalogs AS catalogs
                        ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                    INNER JOIN sys.tables AS tables
                        ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas
                        ON schemas.schema_id = tables.schema_id
                    WHERE catalogs.name = N'UniPMMaintenanceRetrieval'
                      AND (schemas.name <> N'dbo' OR tables.name <> N'MaintenanceSearchDocuments'))
                    THROW 51016, 'Maintenance retrieval full-text catalog contains an unexpected index.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables
                        ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas
                        ON schemas.schema_id = tables.schema_id
                    INNER JOIN sys.fulltext_catalogs AS catalogs
                        ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                    WHERE schemas.name = N'dbo'
                      AND tables.name = N'MaintenanceSearchDocuments'
                      AND catalogs.name = N'UniPMMaintenanceRetrieval')
                    DROP FULLTEXT INDEX ON [dbo].[MaintenanceSearchDocuments];

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_catalogs
                    WHERE name = N'UniPMMaintenanceRetrieval')
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes AS fullTextIndex
                        INNER JOIN sys.fulltext_catalogs AS catalogs
                            ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                        WHERE catalogs.name = N'UniPMMaintenanceRetrieval')
                        THROW 51013, 'Maintenance retrieval full-text catalog contains an unexpected index.', 1;

                    DROP FULLTEXT CATALOG [UniPMMaintenanceRetrieval];
                END;
                """, suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "MaintenanceSearchDocumentEmbeddings");

            migrationBuilder.DropTable(
                name: "MaintenanceSearchDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceSearchDocuments",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Building = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DateInspected = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsOperational = table.Column<bool>(type: "bit", nullable: false),
                    IssueKeysJson = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    LexiconVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProjectionVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSearchDocuments", x => x.InspectionId);
                    table.ForeignKey(
                        name: "FK_MaintenanceSearchDocuments_InspectionRecords_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSearchDocumentEmbeddings",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModelKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VectorJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSearchDocumentEmbeddings", x => x.InspectionId);
                    table.CheckConstraint("CK_MaintenanceSearchDocumentEmbeddings_Dimensions", "[Dimensions] BETWEEN 1 AND 4096");
                    table.CheckConstraint("CK_MaintenanceSearchDocumentEmbeddings_VectorJson", "ISJSON([VectorJson]) = 1");
                    table.ForeignKey(
                        name: "FK_MaintenanceSearchDocumentEmbeddings_MaintenanceSearchDocuments_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "MaintenanceSearchDocuments",
                        principalColumn: "InspectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSearchDocumentEmbeddings_EmbeddingProfile_SourceHash",
                table: "MaintenanceSearchDocumentEmbeddings",
                columns: new[] { "EmbeddingProfile", "SourceHash" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSearchDocuments_AssetCategory_DateInspected",
                table: "MaintenanceSearchDocuments",
                columns: new[] { "AssetCategory", "DateInspected" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSearchDocuments_AssetId_DateInspected",
                table: "MaintenanceSearchDocuments",
                columns: new[] { "AssetId", "DateInspected" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSearchDocuments_IsOperational_DateInspected",
                table: "MaintenanceSearchDocuments",
                columns: new[] { "IsOperational", "DateInspected" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSearchDocuments_ScheduleId",
                table: "MaintenanceSearchDocuments",
                column: "ScheduleId");

            migrationBuilder.Sql("""
                IF ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1
                    THROW 51014, 'Restoring maintenance retrieval requires SQL Server Full-Text Search to be installed and available.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.fulltext_catalogs
                    WHERE name = N'UniPMMaintenanceRetrieval')
                    CREATE FULLTEXT CATALOG [UniPMMaintenanceRetrieval] WITH ACCENT_SENSITIVITY = OFF;

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables
                        ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas
                        ON schemas.schema_id = tables.schema_id
                    WHERE schemas.name = N'dbo'
                      AND tables.name = N'MaintenanceSearchDocuments')
                    THROW 51015, 'Maintenance search document full-text index already exists with an unexpected migration state.', 1;

                CREATE FULLTEXT INDEX ON [dbo].[MaintenanceSearchDocuments]
                (
                    [SearchText] LANGUAGE 0
                )
                KEY INDEX [PK_MaintenanceSearchDocuments]
                ON [UniPMMaintenanceRetrieval]
                WITH CHANGE_TRACKING = AUTO, STOPLIST = OFF;
                """, suppressTransaction: true);
        }
    }
}
