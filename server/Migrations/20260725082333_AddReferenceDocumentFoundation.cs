using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceDocumentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PublisherAuthority = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupersededByDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ContentChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsSynthetic = table.Column<bool>(type: "bit", nullable: false),
                    SyntheticFixtureKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDocuments", x => x.Id);
                    table.CheckConstraint("CK_ReferenceDocuments_LifecycleStatus_Allowed", "[LifecycleStatus] IN ('Active', 'Superseded', 'Archived')");
                    table.CheckConstraint("CK_ReferenceDocuments_NoSelfSupersession", "[SupersededByDocumentId] IS NULL OR [SupersededByDocumentId] <> [Id]");
                    table.CheckConstraint("CK_ReferenceDocuments_SourceType_Allowed", "[SourceType] IN ('Institutional', 'Oem')");
                    table.CheckConstraint("CK_ReferenceDocuments_SupersessionLifecycle", "([LifecycleStatus] = 'Superseded' AND [SupersededByDocumentId] IS NOT NULL) OR ([LifecycleStatus] <> 'Superseded' AND [SupersededByDocumentId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReferenceDocuments_ReferenceDocuments_SupersededByDocumentId",
                        column: x => x.SupersededByDocumentId,
                        principalTable: "ReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDocumentApplicabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModelSeries = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EquipmentFamily = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScopeLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDocumentApplicabilities", x => x.Id);
                    table.CheckConstraint("CK_ReferenceDocumentApplicabilities_AssetCategory_Allowed", "[AssetCategory] IS NULL OR [AssetCategory] IN ('fire-extinguisher', 'fire-alarm', 'emergency-light', 'water-drinking-station')");
                    table.ForeignKey(
                        name: "FK_ReferenceDocumentApplicabilities_ReferenceDocuments_ReferenceDocumentId",
                        column: x => x.ReferenceDocumentId,
                        principalTable: "ReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDocumentSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Heading = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SourceLocator = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PageStart = table.Column<int>(type: "int", nullable: true),
                    PageEnd = table.Column<int>(type: "int", nullable: true),
                    SectionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDocumentSections", x => x.Id);
                    table.CheckConstraint("CK_ReferenceDocumentSections_PageEnd", "[PageEnd] IS NULL OR [PageEnd] >= 1");
                    table.CheckConstraint("CK_ReferenceDocumentSections_PageRange", "[PageStart] IS NULL OR [PageEnd] IS NULL OR [PageEnd] >= [PageStart]");
                    table.CheckConstraint("CK_ReferenceDocumentSections_PageStart", "[PageStart] IS NULL OR [PageStart] >= 1");
                    table.CheckConstraint("CK_ReferenceDocumentSections_Sequence", "[Sequence] >= 0");
                    table.ForeignKey(
                        name: "FK_ReferenceDocumentSections_ReferenceDocuments_ReferenceDocumentId",
                        column: x => x.ReferenceDocumentId,
                        principalTable: "ReferenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDocumentSectionEmbeddings",
                columns: table => new
                {
                    ReferenceDocumentSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    VectorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDocumentSectionEmbeddings", x => x.ReferenceDocumentSectionId);
                    table.CheckConstraint("CK_ReferenceDocumentSectionEmbeddings_Dimensions", "[Dimensions] BETWEEN 1 AND 4096");
                    table.CheckConstraint("CK_ReferenceDocumentSectionEmbeddings_VectorJson", "ISJSON([VectorJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ReferenceDocumentSectionEmbeddings_ReferenceDocumentSections_ReferenceDocumentSectionId",
                        column: x => x.ReferenceDocumentSectionId,
                        principalTable: "ReferenceDocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocumentApplicabilities_ReferenceDocumentId_AssetCategory",
                table: "ReferenceDocumentApplicabilities",
                columns: new[] { "ReferenceDocumentId", "AssetCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocuments_LifecycleStatus_SourceType",
                table: "ReferenceDocuments",
                columns: new[] { "LifecycleStatus", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocuments_SourceType_SourceKey_Revision",
                table: "ReferenceDocuments",
                columns: new[] { "SourceType", "SourceKey", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocuments_SupersededByDocumentId",
                table: "ReferenceDocuments",
                column: "SupersededByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocumentSectionEmbeddings_EmbeddingProfile_SectionHash",
                table: "ReferenceDocumentSectionEmbeddings",
                columns: new[] { "EmbeddingProfile", "SectionHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDocumentSections_ReferenceDocumentId_Sequence",
                table: "ReferenceDocumentSections",
                columns: new[] { "ReferenceDocumentId", "Sequence" },
                unique: true);

            migrationBuilder.Sql("""
                IF ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0) <> 1
                    THROW 51020, 'UniPM reference retrieval requires SQL Server Full-Text Search to be installed and available.', 1;

                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'UniPMReferenceRetrieval')
                    CREATE FULLTEXT CATALOG [UniPMReferenceRetrieval] WITH ACCENT_SENSITIVITY = OFF;

                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
                    WHERE schemas.name = N'dbo' AND tables.name = N'ReferenceDocumentSections')
                    THROW 51021, 'UniPM reference retrieval full-text index already exists with an unexpected migration state.', 1;

                CREATE FULLTEXT INDEX ON [dbo].[ReferenceDocumentSections]
                (
                    [Heading] LANGUAGE 0,
                    [SectionText] LANGUAGE 0
                )
                KEY INDEX [PK_ReferenceDocumentSections]
                ON [UniPMReferenceRetrieval]
                WITH CHANGE_TRACKING = AUTO, STOPLIST = OFF;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.fulltext_indexes AS fullTextIndex
                    INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
                    INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
                    WHERE schemas.name = N'dbo' AND tables.name = N'ReferenceDocumentSections')
                    DROP FULLTEXT INDEX ON [dbo].[ReferenceDocumentSections];

                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'UniPMReferenceRetrieval')
                    DROP FULLTEXT CATALOG [UniPMReferenceRetrieval];
                """, suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "ReferenceDocumentApplicabilities");

            migrationBuilder.DropTable(
                name: "ReferenceDocumentSectionEmbeddings");

            migrationBuilder.DropTable(
                name: "ReferenceDocumentSections");

            migrationBuilder.DropTable(
                name: "ReferenceDocuments");
        }
    }
}
