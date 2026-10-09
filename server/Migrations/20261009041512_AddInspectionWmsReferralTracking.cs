using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionWmsReferralTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InspectionWmsReferralAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousExternalPmNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    NewExternalPmNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionWmsReferralAudits", x => x.Id);
                    table.CheckConstraint("CK_InspectionWmsReferralAudits_Number_NotBlank", "LEN(LTRIM(RTRIM([NewExternalPmNumber]))) > 0");
                    table.CheckConstraint("CK_InspectionWmsReferralAudits_Revision_Positive", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_InspectionWmsReferralAudits_InspectionRecords_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InspectionWmsReferrals",
                columns: table => new
                {
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalPmNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionWmsReferrals", x => x.InspectionId);
                    table.CheckConstraint("CK_InspectionWmsReferrals_Number_NotBlank", "LEN(LTRIM(RTRIM([ExternalPmNumber]))) > 0");
                    table.CheckConstraint("CK_InspectionWmsReferrals_Revision_Positive", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_InspectionWmsReferrals_InspectionRecords_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionWmsReferralAudits_InspectionId_Revision",
                table: "InspectionWmsReferralAudits",
                columns: new[] { "InspectionId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspectionWmsReferralAudits");

            migrationBuilder.DropTable(
                name: "InspectionWmsReferrals");
        }
    }
}
