using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReviewScheduleEnrollmentDeferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ScheduleEnrollmentDeferrals",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "ScheduleEnrollmentDeferrals",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "ScheduleEnrollmentDeferrals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEnrollmentDeferrals_ReviewedAt_DeferredAt",
                table: "ScheduleEnrollmentDeferrals",
                columns: new[] { "ReviewedAt", "DeferredAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduleEnrollmentDeferrals_ReviewState",
                table: "ScheduleEnrollmentDeferrals",
                sql: "([ReviewedAt] IS NULL AND [ReviewedByUserId] IS NULL) OR ([ReviewedAt] IS NOT NULL AND [ReviewedByUserId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduleEnrollmentDeferrals_ReviewedAt_DeferredAt",
                table: "ScheduleEnrollmentDeferrals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduleEnrollmentDeferrals_ReviewState",
                table: "ScheduleEnrollmentDeferrals");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ScheduleEnrollmentDeferrals");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ScheduleEnrollmentDeferrals");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ScheduleEnrollmentDeferrals");
        }
    }
}
