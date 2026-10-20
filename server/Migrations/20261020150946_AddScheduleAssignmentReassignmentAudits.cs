using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleAssignmentReassignmentAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleAssignmentReassignmentAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentRole = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PreviousAssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewAssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleAssignmentReassignmentAudits", x => x.Id);
                    table.CheckConstraint("CK_ScheduleAssignmentReassignmentAudits_AssignmentRole_Allowed", "[AssignmentRole] IN ('Supervisor', 'Inspector')");
                    table.CheckConstraint("CK_ScheduleAssignmentReassignmentAudits_Reason_NotBlank", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.ForeignKey(
                        name: "FK_ScheduleAssignmentReassignmentAudits_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleAssignmentReassignmentAudits_AspNetUsers_NewAssigneeUserId",
                        column: x => x.NewAssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleAssignmentReassignmentAudits_AspNetUsers_PreviousAssigneeUserId",
                        column: x => x.PreviousAssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScheduleAssignmentReassignmentAudits_PreventiveMaintenanceSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "PreventiveMaintenanceSchedules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignmentReassignmentAudits_ChangedByUserId",
                table: "ScheduleAssignmentReassignmentAudits",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignmentReassignmentAudits_NewAssigneeUserId",
                table: "ScheduleAssignmentReassignmentAudits",
                column: "NewAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignmentReassignmentAudits_PreviousAssigneeUserId",
                table: "ScheduleAssignmentReassignmentAudits",
                column: "PreviousAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleAssignmentReassignmentAudits_ScheduleId_ChangedAt",
                table: "ScheduleAssignmentReassignmentAudits",
                columns: new[] { "ScheduleId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleAssignmentReassignmentAudits");
        }
    }
}
