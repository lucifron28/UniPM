using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueScheduleAssetCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (" +
                "    SELECT 1 FROM [PreventiveMaintenanceSchedules] " +
                "    GROUP BY [AssetId], [PmCycle] HAVING COUNT_BIG(*) > 1" +
                ") " +
                "BEGIN; " +
                "    THROW 51000, 'Cannot add UX_Schedules_AssetId_PmCycle because duplicate asset/cycle schedules exist. Review and resolve collisions manually before retrying.', 1; " +
                "END;");

            migrationBuilder.CreateIndex(
                name: "UX_Schedules_AssetId_PmCycle",
                table: "PreventiveMaintenanceSchedules",
                columns: new[] { "AssetId", "PmCycle" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Schedules_AssetId_PmCycle",
                table: "PreventiveMaintenanceSchedules");
        }
    }
}
