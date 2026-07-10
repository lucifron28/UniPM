using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceSearchDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceSearchDocuments",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Building = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInspected = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsOperational = table.Column<bool>(type: "bit", nullable: false),
                    SourceCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssetUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProjectionVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LexiconVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IssueKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceSearchDocuments");
        }
    }
}
