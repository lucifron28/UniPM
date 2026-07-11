using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceSearchDocumentEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceSearchDocumentEmbeddings",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    VectorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceSearchDocumentEmbeddings");
        }
    }
}
