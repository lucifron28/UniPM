using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterStationInspectionWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DateAccomplished",
                table: "InspectionRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaterCheckUvLight",
                table: "InspectionRecords",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaterReplaceCarbonFilter",
                table: "InspectionRecords",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaterReplaceSedimentFilter",
                table: "InspectionRecords",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateAccomplished",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "WaterCheckUvLight",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "WaterReplaceCarbonFilter",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "WaterReplaceSedimentFilter",
                table: "InspectionRecords");
        }
    }
}
