using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleEnrollmentDeferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleEnrollmentDeferrals",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PmCycle = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    DepartmentAtDeferral = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AssetCategoryAtDeferral = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeferredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextEligiblePmCycle = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEnrollmentDeferrals", x => new { x.AssetId, x.PmCycle });
                    table.CheckConstraint("CK_ScheduleEnrollmentDeferrals_NextEligiblePmCycle_Format", "LEN([NextEligiblePmCycle]) = 7 AND [NextEligiblePmCycle] LIKE '[0-9][0-9][0-9][0-9]-[0-1][0-9]' AND RIGHT([NextEligiblePmCycle], 2) BETWEEN '01' AND '12'");
                    table.CheckConstraint("CK_ScheduleEnrollmentDeferrals_PmCycle_Format", "LEN([PmCycle]) = 7 AND [PmCycle] LIKE '[0-9][0-9][0-9][0-9]-[0-1][0-9]' AND RIGHT([PmCycle], 2) BETWEEN '01' AND '12'");
                    table.CheckConstraint("CK_ScheduleEnrollmentDeferrals_ReasonCode_Allowed", "[ReasonCode] IN ('BatchAssigned', 'WorkInProgress', 'CycleCompleted', 'CycleCancelled', 'InspectionStarted', 'FormSubmitted', 'FormAcknowledged')");
                    table.ForeignKey(
                        name: "FK_ScheduleEnrollmentDeferrals_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEnrollmentDeferrals_DeferredAt_AssetId",
                table: "ScheduleEnrollmentDeferrals",
                columns: new[] { "DeferredAt", "AssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleEnrollmentDeferrals");
        }
    }
}
