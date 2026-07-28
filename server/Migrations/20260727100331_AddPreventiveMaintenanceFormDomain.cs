using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPreventiveMaintenanceFormDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreventiveMaintenanceFormId",
                table: "InspectionRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AssetCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Building = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PeriodType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quarter = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Semester = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: true),
                    AcademicYear = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceForms", x => x.Id);
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_AssetCategory_Allowed", "[AssetCategory] IN ('fire-extinguisher', 'fire-alarm', 'emergency-light', 'water-drinking-station')");
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_PeriodType_Allowed", "[PeriodType] IN ('Quarter', 'Semester', 'Annual', 'Custom')");
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_Quarter_Allowed", "[Quarter] IS NULL OR [Quarter] IN ('Q1', 'Q2', 'Q3', 'Q4')");
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_Semester_Allowed", "[Semester] IS NULL OR [Semester] IN ('First', 'Second', 'Summer')");
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_AcademicYear_Format", "[AcademicYear] IS NULL OR [AcademicYear] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_PreventiveMaintenanceForms_Status_Allowed", "[Status] IN ('Draft', 'Submitted', 'Acknowledged')");
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignatoryName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SignatoryPosition = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SignatureData = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: true),
                    SignatureContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SignatureChecksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CapturedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceAcknowledgements", x => x.Id);
                    table.CheckConstraint("CK_PreventiveMaintenanceAcknowledgements_SignatureMetadata", "([SignatureData] IS NULL AND [SignatureContentType] IS NULL AND [SignatureChecksum] IS NULL) OR ([SignatureData] IS NOT NULL AND [SignatureContentType] IS NOT NULL AND [SignatureChecksum] IS NOT NULL)");
                    table.CheckConstraint("CK_PreventiveMaintenanceAcknowledgements_SignatureSize", "[SignatureData] IS NULL OR DATALENGTH([SignatureData]) <= 524288");
                    table.ForeignKey(
                        name: "FK_PreventiveMaintenanceAcknowledgements_PreventiveMaintenanceForms_FormId",
                        column: x => x.FormId,
                        principalTable: "PreventiveMaintenanceForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_PreventiveMaintenanceFormId",
                table: "InspectionRecords",
                column: "PreventiveMaintenanceFormId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceAcknowledgements_FormId",
                table: "PreventiveMaintenanceAcknowledgements",
                column: "FormId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceForms_AssetCategory_Status",
                table: "PreventiveMaintenanceForms",
                columns: new[] { "AssetCategory", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceForms_FileNumber",
                table: "PreventiveMaintenanceForms",
                column: "FileNumber",
                unique: true,
                filter: "[FileNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionRecords_PreventiveMaintenanceForms_PreventiveMaintenanceFormId",
                table: "InspectionRecords",
                column: "PreventiveMaintenanceFormId",
                principalTable: "PreventiveMaintenanceForms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionRecords_PreventiveMaintenanceForms_PreventiveMaintenanceFormId",
                table: "InspectionRecords");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceAcknowledgements");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceForms");

            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_PreventiveMaintenanceFormId",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "PreventiveMaintenanceFormId",
                table: "InspectionRecords");
        }
    }
}
