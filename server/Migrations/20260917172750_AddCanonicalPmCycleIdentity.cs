using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalPmCycleIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PmCycle",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PmCycle",
                table: "PreventiveMaintenanceForms",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [PreventiveMaintenanceSchedules] " +
                "SET [PmCycle] = LEFT(CONVERT(varchar(34), [ScheduleDate], 126), 7)");

            migrationBuilder.AlterColumn<string>(
                name: "PmCycle",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldNullable: true);

            migrationBuilder.Sql(
                "WITH [FormCycles] AS (" +
                "    SELECT form.[Id], MIN(schedule.[PmCycle]) AS [PmCycle], " +
                "        UPPER(LTRIM(RTRIM(form.[Department]))) AS [DepartmentKey], " +
                "        UPPER(LTRIM(RTRIM(form.[AssetCategory]))) AS [AssetCategoryKey] " +
                "    FROM [PreventiveMaintenanceForms] AS form " +
                "    INNER JOIN [InspectionRecords] AS inspection " +
                "        ON inspection.[PreventiveMaintenanceFormId] = form.[Id] " +
                "    INNER JOIN [PreventiveMaintenanceSchedules] AS schedule " +
                "        ON schedule.[Id] = inspection.[ScheduleId] " +
                "    WHERE form.[Department] IS NOT NULL " +
                "    GROUP BY form.[Id], UPPER(LTRIM(RTRIM(form.[Department]))), " +
                "        UPPER(LTRIM(RTRIM(form.[AssetCategory]))) " +
                "    HAVING COUNT(DISTINCT schedule.[PmCycle]) = 1" +
                "), [SafeFormCycles] AS (" +
                "    SELECT candidate.[Id], candidate.[PmCycle] " +
                "    FROM [FormCycles] AS candidate " +
                "    WHERE NOT EXISTS (" +
                "        SELECT 1 FROM [FormCycles] AS other " +
                "        WHERE other.[Id] <> candidate.[Id] " +
                "            AND other.[DepartmentKey] = candidate.[DepartmentKey] " +
                "            AND other.[AssetCategoryKey] = candidate.[AssetCategoryKey] " +
                "            AND other.[PmCycle] = candidate.[PmCycle]" +
                "    )" +
                ") " +
                "UPDATE form SET [PmCycle] = safe.[PmCycle] " +
                "FROM [PreventiveMaintenanceForms] AS form " +
                "INNER JOIN [SafeFormCycles] AS safe ON safe.[Id] = form.[Id]");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceForms_Department_AssetCategory_PmCycle",
                table: "PreventiveMaintenanceForms",
                columns: new[] { "Department", "AssetCategory", "PmCycle" },
                unique: true,
                filter: "[Department] IS NOT NULL AND [PmCycle] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_PmCycle_Format",
                table: "PreventiveMaintenanceSchedules",
                sql: "LEN([PmCycle]) = 7 AND [PmCycle] LIKE '[0-9][0-9][0-9][0-9]-[0-1][0-9]' AND RIGHT([PmCycle], 2) BETWEEN '01' AND '12'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreventiveMaintenanceForms_PmCycle_Format",
                table: "PreventiveMaintenanceForms",
                sql: "[PmCycle] IS NULL OR (LEN([PmCycle]) = 7 AND [PmCycle] LIKE '[0-9][0-9][0-9][0-9]-[0-1][0-9]' AND RIGHT([PmCycle], 2) BETWEEN '01' AND '12')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_PmCycle_Format",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceForms_Department_AssetCategory_PmCycle",
                table: "PreventiveMaintenanceForms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PreventiveMaintenanceForms_PmCycle_Format",
                table: "PreventiveMaintenanceForms");

            migrationBuilder.DropColumn(
                name: "PmCycle",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "PmCycle",
                table: "PreventiveMaintenanceForms");
        }
    }
}
