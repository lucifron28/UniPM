using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AssignScheduleOversight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules",
                column: "AssignedSupervisorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PreventiveMaintenanceSchedules_AspNetUsers_AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules",
                column: "AssignedSupervisorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreventiveMaintenanceSchedules_AspNetUsers_AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "AssignedSupervisorUserId",
                table: "PreventiveMaintenanceSchedules");
        }
    }
}
