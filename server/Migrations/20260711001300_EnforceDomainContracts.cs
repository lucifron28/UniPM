using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniPM.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDomainContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the migration deterministic for existing SQL Server data:
            // length audit -> canonicalization -> supported-value audit -> duplicate audit.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM dbo.Assets
                    WHERE DATALENGTH(AssetCode) / 2 > 64
                       OR DATALENGTH(AssetCategory) / 2 > 64
                       OR DATALENGTH(Status) / 2 > 32
                       OR DATALENGTH(Building) / 2 > 256
                       OR DATALENGTH(Department) / 2 > 256
                       OR DATALENGTH(Location) / 2 > 256
                       OR DATALENGTH(QrCodeValue) / 2 > 128)
                    THROW 51000, 'Domain-contract migration stopped: an asset value exceeds its maximum length.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.PreventiveMaintenanceSchedules
                    WHERE DATALENGTH(PeriodType) / 2 > 32
                       OR DATALENGTH(Status) / 2 > 32
                       OR DATALENGTH(Quarter) / 2 > 8
                       OR DATALENGTH(Semester) / 2 > 16
                       OR DATALENGTH(AcademicYear) / 2 > 16)
                    THROW 51000, 'Domain-contract migration stopped: a schedule value exceeds its maximum length.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.InspectionRecords
                    WHERE DATALENGTH(Remarks) / 2 > 2000
                       OR DATALENGTH(ActionsRecommendations) / 2 > 2000)
                    THROW 51000, 'Domain-contract migration stopped: an inspection text value exceeds its maximum length.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.MaintenanceSearchDocuments
                    WHERE DATALENGTH(AssetCode) / 2 > 64
                       OR DATALENGTH(AssetCategory) / 2 > 64
                       OR DATALENGTH(Building) / 2 > 256
                       OR DATALENGTH(Department) / 2 > 256
                       OR DATALENGTH(Location) / 2 > 256
                       OR DATALENGTH(IssueKeysJson) / 2 > 1024)
                    THROW 51000, 'Domain-contract migration stopped: a search-document value exceeds its maximum length.', 1;

                -- SQL Server 2019 has no STRING_SPLIT ordinal parameter. The
                -- preceding length audit bounds identifiers at 128 characters, so
                -- this tally set preserves normalized line order without XQuery.
                ;WITH Digits(Value) AS
                (
                    SELECT Value FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS valuesTable(Value)
                ),
                Tally AS
                (
                    SELECT TOP (256)
                        CONVERT(int, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) AS Number
                    FROM Digits AS hundreds
                    CROSS JOIN Digits AS tens
                    CROSS JOIN Digits AS ones
                )
                UPDATE assets
                SET AssetCode = UPPER(COALESCE(assetCodeCanonical.CanonicalValue, N'')),
                    AssetCategory = LOWER(LTRIM(RTRIM(AssetCategory))),
                    Status = CASE UPPER(LTRIM(RTRIM(Status)))
                        WHEN 'ACTIVE' THEN 'Active'
                        WHEN 'INACTIVE' THEN 'Inactive'
                        WHEN 'RETIRED' THEN 'Retired'
                        ELSE LTRIM(RTRIM(Status))
                    END,
                    Building = NULLIF(LTRIM(RTRIM(Building)), ''),
                    Department = NULLIF(LTRIM(RTRIM(Department)), ''),
                    Location = NULLIF(LTRIM(RTRIM(Location)), ''),
                    QrCodeValue = CASE
                        WHEN qrCodeCanonical.CanonicalValue IS NULL THEN NULL
                        ELSE UPPER(qrCodeCanonical.CanonicalValue)
                    END
                FROM dbo.Assets AS assets
                CROSS APPLY
                (
                    VALUES (REPLACE(REPLACE(COALESCE(assets.AssetCode, N''), NCHAR(13) + NCHAR(10), NCHAR(10)), NCHAR(13), NCHAR(10)))
                ) AS assetCodeSource(NormalizedValue)
                CROSS APPLY
                (
                    SELECT STRING_AGG(CONVERT(nvarchar(max), TRIM(fragments.Fragment)), N' ')
                        WITHIN GROUP (ORDER BY fragments.Ordinal) AS CanonicalValue
                    FROM
                    (
                        SELECT tally.Number AS Ordinal,
                            SUBSTRING(assetCodeSource.NormalizedValue, tally.Number + 1,
                                CHARINDEX(NCHAR(10), assetCodeSource.NormalizedValue + NCHAR(10), tally.Number + 1) - tally.Number - 1) AS Fragment
                        FROM Tally AS tally
                        WHERE tally.Number <= LEN(assetCodeSource.NormalizedValue)
                          AND (tally.Number = 0 OR SUBSTRING(assetCodeSource.NormalizedValue, tally.Number, 1) = NCHAR(10))
                    ) AS fragments
                    WHERE TRIM(fragments.Fragment) <> N''
                ) AS assetCodeCanonical
                CROSS APPLY
                (
                    VALUES (REPLACE(REPLACE(COALESCE(assets.QrCodeValue, N''), NCHAR(13) + NCHAR(10), NCHAR(10)), NCHAR(13), NCHAR(10)))
                ) AS qrCodeSource(NormalizedValue)
                CROSS APPLY
                (
                    SELECT STRING_AGG(CONVERT(nvarchar(max), TRIM(fragments.Fragment)), N' ')
                        WITHIN GROUP (ORDER BY fragments.Ordinal) AS CanonicalValue
                    FROM
                    (
                        SELECT tally.Number AS Ordinal,
                            SUBSTRING(qrCodeSource.NormalizedValue, tally.Number + 1,
                                CHARINDEX(NCHAR(10), qrCodeSource.NormalizedValue + NCHAR(10), tally.Number + 1) - tally.Number - 1) AS Fragment
                        FROM Tally AS tally
                        WHERE tally.Number <= LEN(qrCodeSource.NormalizedValue)
                          AND (tally.Number = 0 OR SUBSTRING(qrCodeSource.NormalizedValue, tally.Number, 1) = NCHAR(10))
                    ) AS fragments
                    WHERE TRIM(fragments.Fragment) <> N''
                ) AS qrCodeCanonical;

                UPDATE dbo.PreventiveMaintenanceSchedules
                SET PeriodType = CASE UPPER(LTRIM(RTRIM(PeriodType)))
                        WHEN 'QUARTER' THEN 'Quarter'
                        WHEN 'SEMESTER' THEN 'Semester'
                        WHEN 'ANNUAL' THEN 'Annual'
                        WHEN 'CUSTOM' THEN 'Custom'
                        ELSE LTRIM(RTRIM(PeriodType))
                    END,
                    Status = CASE UPPER(LTRIM(RTRIM(Status)))
                        WHEN 'DUE' THEN 'Due'
                        WHEN 'ONGOING' THEN 'Ongoing'
                        WHEN 'COMPLETED' THEN 'Completed'
                        WHEN 'OVERDUE' THEN 'Overdue'
                        WHEN 'CANCELLED' THEN 'Cancelled'
                        ELSE LTRIM(RTRIM(Status))
                    END,
                    Quarter = CASE
                        WHEN Quarter IS NULL OR LTRIM(RTRIM(Quarter)) = '' THEN NULL
                        ELSE UPPER(LTRIM(RTRIM(Quarter)))
                    END,
                    Semester = CASE
                        WHEN Semester IS NULL OR LTRIM(RTRIM(Semester)) = '' THEN NULL
                        WHEN UPPER(LTRIM(RTRIM(Semester))) = 'FIRST' THEN 'First'
                        WHEN UPPER(LTRIM(RTRIM(Semester))) = 'SECOND' THEN 'Second'
                        WHEN UPPER(LTRIM(RTRIM(Semester))) = 'SUMMER' THEN 'Summer'
                        ELSE LTRIM(RTRIM(Semester))
                    END,
                    AcademicYear = NULLIF(LTRIM(RTRIM(AcademicYear)), '');

                ;WITH Digits(Value) AS
                (
                    SELECT Value FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS valuesTable(Value)
                ),
                Tally AS
                (
                    SELECT TOP (256)
                        CONVERT(int, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) AS Number
                    FROM Digits AS hundreds
                    CROSS JOIN Digits AS tens
                    CROSS JOIN Digits AS ones
                )
                UPDATE documents
                SET AssetCode = UPPER(COALESCE(assetCodeCanonical.CanonicalValue, N'')),
                    AssetCategory = LOWER(LTRIM(RTRIM(AssetCategory))),
                    Building = NULLIF(LTRIM(RTRIM(Building)), ''),
                    Department = NULLIF(LTRIM(RTRIM(Department)), ''),
                    Location = NULLIF(LTRIM(RTRIM(Location)), '')
                FROM dbo.MaintenanceSearchDocuments AS documents
                CROSS APPLY
                (
                    VALUES (REPLACE(REPLACE(COALESCE(documents.AssetCode, N''), NCHAR(13) + NCHAR(10), NCHAR(10)), NCHAR(13), NCHAR(10)))
                ) AS assetCodeSource(NormalizedValue)
                CROSS APPLY
                (
                    SELECT STRING_AGG(CONVERT(nvarchar(max), TRIM(fragments.Fragment)), N' ')
                        WITHIN GROUP (ORDER BY fragments.Ordinal) AS CanonicalValue
                    FROM
                    (
                        SELECT tally.Number AS Ordinal,
                            SUBSTRING(assetCodeSource.NormalizedValue, tally.Number + 1,
                                CHARINDEX(NCHAR(10), assetCodeSource.NormalizedValue + NCHAR(10), tally.Number + 1) - tally.Number - 1) AS Fragment
                        FROM Tally AS tally
                        WHERE tally.Number <= LEN(assetCodeSource.NormalizedValue)
                          AND (tally.Number = 0 OR SUBSTRING(assetCodeSource.NormalizedValue, tally.Number, 1) = NCHAR(10))
                    ) AS fragments
                    WHERE TRIM(fragments.Fragment) <> N''
                ) AS assetCodeCanonical;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.Assets
                    WHERE NULLIF(AssetCode, '') IS NULL
                       OR AssetCategory NOT IN ('fire-extinguisher', 'fire-alarm', 'emergency-light', 'water-drinking-station')
                       OR Status NOT IN ('Active', 'Inactive', 'Retired'))
                    THROW 51000, 'Domain-contract migration stopped: an asset contains an unsupported code.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.PreventiveMaintenanceSchedules
                    WHERE PeriodType NOT IN ('Quarter', 'Semester', 'Annual', 'Custom')
                       OR Status NOT IN ('Due', 'Ongoing', 'Completed', 'Overdue', 'Cancelled')
                       OR (Quarter IS NOT NULL AND Quarter NOT IN ('Q1', 'Q2', 'Q3', 'Q4'))
                       OR (Semester IS NOT NULL AND Semester NOT IN ('First', 'Second', 'Summer'))
                       OR (AcademicYear IS NOT NULL AND AcademicYear NOT LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'))
                    THROW 51000, 'Domain-contract migration stopped: a schedule contains an unsupported code.', 1;

                IF EXISTS (
                    SELECT AssetCode
                    FROM dbo.Assets
                    GROUP BY AssetCode
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Domain-contract migration stopped: canonical asset codes are duplicated.', 1;

                IF EXISTS (
                    SELECT QrCodeValue
                    FROM dbo.Assets
                    WHERE QrCodeValue IS NOT NULL
                    GROUP BY QrCodeValue
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Domain-contract migration stopped: canonical QR values are duplicated.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssetId",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Semester",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Quarter",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PeriodType",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AcademicYear",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IssueKeysJson",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssetCode",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InspectionRecords",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionsRecommendations",
                table: "InspectionRecords",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Assets",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "QrCodeValue",
                table: "Assets",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Assets",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "Assets",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Assets",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssetCode",
                table: "Assets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AssetCategory",
                table: "Assets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssetId_Status_ScheduleDate",
                table: "PreventiveMaintenanceSchedules",
                columns: new[] { "AssetId", "Status", "ScheduleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSchedules_Status_ScheduleDate",
                table: "PreventiveMaintenanceSchedules",
                columns: new[] { "Status", "ScheduleDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_AcademicYear_Format",
                table: "PreventiveMaintenanceSchedules",
                sql: "[AcademicYear] IS NULL OR [AcademicYear] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_PeriodType_Allowed",
                table: "PreventiveMaintenanceSchedules",
                sql: "[PeriodType] IN ('Quarter', 'Semester', 'Annual', 'Custom')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_Quarter_Allowed",
                table: "PreventiveMaintenanceSchedules",
                sql: "[Quarter] IS NULL OR [Quarter] IN ('Q1', 'Q2', 'Q3', 'Q4')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_Semester_Allowed",
                table: "PreventiveMaintenanceSchedules",
                sql: "[Semester] IS NULL OR [Semester] IN ('First', 'Second', 'Summer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedules_Status_Allowed",
                table: "PreventiveMaintenanceSchedules",
                sql: "[Status] IN ('Due', 'Ongoing', 'Completed', 'Overdue', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCategory_Status",
                table: "Assets",
                columns: new[] { "AssetCategory", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCode",
                table: "Assets",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_QrCodeValue",
                table: "Assets",
                column: "QrCodeValue",
                unique: true,
                filter: "[QrCodeValue] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assets_AssetCategory_Allowed",
                table: "Assets",
                sql: "[AssetCategory] IN ('fire-extinguisher', 'fire-alarm', 'emergency-light', 'water-drinking-station')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assets_Status_Allowed",
                table: "Assets",
                sql: "[Status] IN ('Active', 'Inactive', 'Retired')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssetId_Status_ScheduleDate",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_PreventiveMaintenanceSchedules_Status_ScheduleDate",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_AcademicYear_Format",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_PeriodType_Allowed",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_Quarter_Allowed",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_Semester_Allowed",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedules_Status_Allowed",
                table: "PreventiveMaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetCategory_Status",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetCode",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_QrCodeValue",
                table: "Assets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_AssetCategory_Allowed",
                table: "Assets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_Status_Allowed",
                table: "Assets");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Semester",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Quarter",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PeriodType",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "AcademicYear",
                table: "PreventiveMaintenanceSchedules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IssueKeysJson",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssetCode",
                table: "MaintenanceSearchDocuments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InspectionRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActionsRecommendations",
                table: "InspectionRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "QrCodeValue",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Department",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Building",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssetCode",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "AssetCategory",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceSchedules_AssetId",
                table: "PreventiveMaintenanceSchedules",
                column: "AssetId");
        }
    }
}
