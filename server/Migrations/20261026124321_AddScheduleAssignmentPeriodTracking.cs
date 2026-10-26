using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleAssignmentPeriodTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "NewAssigneeUserId",
                table: "ScheduleAssignmentReassignmentAudits",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "ScheduleAssignmentReassignmentAudits",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Reassignment");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstInspectorAssignedAt",
                table: "PreventiveMaintenanceSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstSupervisorAssignedAt",
                table: "PreventiveMaintenanceSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduleAssignmentReassignmentAudits_EventType_Allowed",
                table: "ScheduleAssignmentReassignmentAudits",
                sql: "[EventType] IN ('InitialAssignment', 'Reassignment', 'Unassignment')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [ScheduleAssignmentReassignmentAudits] WHERE [EventType] <> N'Reassignment' OR [NewAssigneeUserId] IS NULL) BEGIN THROW 51000, 'Cannot downgrade after initial-assignment or unassignment events have been recorded.', 1; END;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduleAssignmentReassignmentAudits_EventType_Allowed",
                table: "ScheduleAssignmentReassignmentAudits");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "ScheduleAssignmentReassignmentAudits");

            migrationBuilder.DropColumn(
                name: "FirstInspectorAssignedAt",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "FirstSupervisorAssignedAt",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "NewAssigneeUserId",
                table: "ScheduleAssignmentReassignmentAudits",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
