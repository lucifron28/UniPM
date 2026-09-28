using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionLocationQualityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_Accuracy",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_Outcome_Consistent",
                table: "InspectionLocationAttempts");

            migrationBuilder.AlterColumn<double>(
                name: "AccuracyMeters",
                table: "InspectionLocationAttempts",
                type: "float",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AddColumn<string>(
                name: "AccuracyMode",
                table: "InspectionLocationAttempts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "AcquisitionDurationMs",
                table: "InspectionLocationAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DevicePositionTimestamp",
                table: "InspectionLocationAttempts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAccuracy",
                table: "InspectionLocationAttempts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMocked",
                table: "InspectionLocationAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_Accuracy",
                table: "InspectionLocationAttempts",
                sql: "([AccuracyMeters] IS NULL OR [AccuracyMeters] >= 0) AND (([HasAccuracy] = 1 AND [AccuracyMeters] IS NOT NULL) OR ([HasAccuracy] = 0 AND [AccuracyMeters] IS NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_AccuracyMode",
                table: "InspectionLocationAttempts",
                sql: "[AccuracyMode] IN ('Precise', 'Reduced', 'Unknown')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_AcquisitionDuration",
                table: "InspectionLocationAttempts",
                sql: "[AcquisitionDurationMs] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_Outcome_Consistent",
                table: "InspectionLocationAttempts",
                sql: "([Outcome] = 'NotConfigured' AND [ExpectedLatitude] IS NULL AND [ExpectedLongitude] IS NULL AND [ExpectedRadiusMeters] IS NULL AND [DistanceMeters] IS NULL) OR ([Outcome] = 'Uncertain' AND [HasAccuracy] = 0 AND [ExpectedLatitude] IS NOT NULL AND [ExpectedLongitude] IS NOT NULL AND [ExpectedRadiusMeters] IS NOT NULL AND [DistanceMeters] IS NOT NULL AND [DistanceMeters] >= 0) OR ([Outcome] IN ('Inside', 'Outside', 'Uncertain') AND [HasAccuracy] = 1 AND [ExpectedLatitude] IS NOT NULL AND [ExpectedLongitude] IS NOT NULL AND [ExpectedRadiusMeters] IS NOT NULL AND [DistanceMeters] IS NOT NULL AND [DistanceMeters] >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_Accuracy",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_AccuracyMode",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_AcquisitionDuration",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionLocationAttempts_Outcome_Consistent",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropColumn(
                name: "AccuracyMode",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropColumn(
                name: "AcquisitionDurationMs",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropColumn(
                name: "DevicePositionTimestamp",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropColumn(
                name: "HasAccuracy",
                table: "InspectionLocationAttempts");

            migrationBuilder.DropColumn(
                name: "IsMocked",
                table: "InspectionLocationAttempts");

            migrationBuilder.AlterColumn<double>(
                name: "AccuracyMeters",
                table: "InspectionLocationAttempts",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_Accuracy",
                table: "InspectionLocationAttempts",
                sql: "[AccuracyMeters] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InspectionLocationAttempts_Outcome_Consistent",
                table: "InspectionLocationAttempts",
                sql: "([Outcome] = 'NotConfigured' AND [ExpectedLatitude] IS NULL AND [ExpectedLongitude] IS NULL AND [ExpectedRadiusMeters] IS NULL AND [DistanceMeters] IS NULL) OR ([Outcome] IN ('Inside', 'Outside', 'Uncertain') AND [ExpectedLatitude] IS NOT NULL AND [ExpectedLongitude] IS NOT NULL AND [ExpectedRadiusMeters] IS NOT NULL AND [DistanceMeters] IS NOT NULL AND [DistanceMeters] >= 0)");
        }
    }
}
