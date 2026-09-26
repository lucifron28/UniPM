using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionLocationVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationAttemptId",
                table: "InspectionRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VerificationLatitude",
                table: "Assets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VerificationLongitude",
                table: "Assets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VerificationRadiusMeters",
                table: "Assets",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InspectionLocationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MeasuredLatitude = table.Column<double>(type: "float", nullable: false),
                    MeasuredLongitude = table.Column<double>(type: "float", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "float", nullable: false),
                    ExpectedLatitude = table.Column<double>(type: "float", nullable: true),
                    ExpectedLongitude = table.Column<double>(type: "float", nullable: true),
                    ExpectedRadiusMeters = table.Column<double>(type: "float", nullable: true),
                    DistanceMeters = table.Column<double>(type: "float", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionLocationAttempts", x => x.Id);
                    table.CheckConstraint("CK_InspectionLocationAttempts_Accuracy", "[AccuracyMeters] >= 0");
                    table.CheckConstraint("CK_InspectionLocationAttempts_ExpectedLocation_Complete", "([ExpectedLatitude] IS NULL AND [ExpectedLongitude] IS NULL AND [ExpectedRadiusMeters] IS NULL) OR ([ExpectedLatitude] IS NOT NULL AND [ExpectedLongitude] IS NOT NULL AND [ExpectedRadiusMeters] IS NOT NULL AND [ExpectedLatitude] BETWEEN -90 AND 90 AND [ExpectedLongitude] BETWEEN -180 AND 180 AND [ExpectedRadiusMeters] > 0)");
                    table.CheckConstraint("CK_InspectionLocationAttempts_MeasuredCoordinates", "[MeasuredLatitude] BETWEEN -90 AND 90 AND [MeasuredLongitude] BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_InspectionLocationAttempts_Outcome_Consistent", "([Outcome] = 'NotConfigured' AND [ExpectedLatitude] IS NULL AND [ExpectedLongitude] IS NULL AND [ExpectedRadiusMeters] IS NULL AND [DistanceMeters] IS NULL) OR ([Outcome] IN ('Inside', 'Outside', 'Uncertain') AND [ExpectedLatitude] IS NOT NULL AND [ExpectedLongitude] IS NOT NULL AND [ExpectedRadiusMeters] IS NOT NULL AND [DistanceMeters] IS NOT NULL AND [DistanceMeters] >= 0)");
                    table.ForeignKey(
                        name: "FK_InspectionLocationAttempts_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InspectionLocationAttempts_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InspectionLocationAttempts_PreventiveMaintenanceSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "PreventiveMaintenanceSchedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_LocationAttemptId",
                table: "InspectionRecords",
                column: "LocationAttemptId",
                unique: true,
                filter: "[LocationAttemptId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assets_VerificationLocation_Complete",
                table: "Assets",
                sql: "([VerificationLatitude] IS NULL AND [VerificationLongitude] IS NULL AND [VerificationRadiusMeters] IS NULL) OR ([VerificationLatitude] IS NOT NULL AND [VerificationLongitude] IS NOT NULL AND [VerificationRadiusMeters] IS NOT NULL AND [VerificationLatitude] BETWEEN -90 AND 90 AND [VerificationLongitude] BETWEEN -180 AND 180 AND [VerificationRadiusMeters] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLocationAttempts_ActorUserId_CapturedAt",
                table: "InspectionLocationAttempts",
                columns: new[] { "ActorUserId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLocationAttempts_AssetId",
                table: "InspectionLocationAttempts",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionLocationAttempts_ScheduleId",
                table: "InspectionLocationAttempts",
                column: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionRecords_InspectionLocationAttempts_LocationAttemptId",
                table: "InspectionRecords",
                column: "LocationAttemptId",
                principalTable: "InspectionLocationAttempts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InspectionRecords_InspectionLocationAttempts_LocationAttemptId",
                table: "InspectionRecords");

            migrationBuilder.DropTable(
                name: "InspectionLocationAttempts");

            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_LocationAttemptId",
                table: "InspectionRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_VerificationLocation_Complete",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LocationAttemptId",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "VerificationLatitude",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VerificationLongitude",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VerificationRadiusMeters",
                table: "Assets");
        }
    }
}
