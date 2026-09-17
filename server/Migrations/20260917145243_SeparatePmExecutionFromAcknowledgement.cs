using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePmExecutionFromAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FieldWorkCompletedAt",
                table: "PreventiveMaintenanceForms",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "InspectionRecords",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "InspectionRecords",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldWorkCompletedAt",
                table: "PreventiveMaintenanceForms");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "InspectionRecords");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "InspectionRecords");
        }
    }
}
