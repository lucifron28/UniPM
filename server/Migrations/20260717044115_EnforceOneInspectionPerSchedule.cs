using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneInspectionPerSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[InspectionRecords]
                    GROUP BY [ScheduleId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51020, 'Inspection integrity migration stopped: multiple inspection records exist for one schedule.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_ScheduleId",
                table: "InspectionRecords");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_ScheduleId",
                table: "InspectionRecords",
                column: "ScheduleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionRecords_ScheduleId",
                table: "InspectionRecords");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_ScheduleId",
                table: "InspectionRecords",
                column: "ScheduleId");
        }
    }
}
