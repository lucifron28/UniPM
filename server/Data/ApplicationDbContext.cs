using Microsoft.EntityFrameworkCore;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules => Set<PreventiveMaintenanceSchedule>();
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();
    public DbSet<MaintenanceSearchDocument> MaintenanceSearchDocuments => Set<MaintenanceSearchDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        var asset = modelBuilder.Entity<Asset>();
        asset.Property(entity => entity.AssetCode)
            .HasMaxLength(AssetCodeValue.MaxLength);
        asset.Property(entity => entity.AssetCategory)
            .HasMaxLength(64);
        asset.Property(entity => entity.Building)
            .HasMaxLength(256);
        asset.Property(entity => entity.Department)
            .HasMaxLength(256);
        asset.Property(entity => entity.Location)
            .HasMaxLength(256);
        asset.Property(entity => entity.QrCodeValue)
            .HasMaxLength(AssetCodeValue.QrCodeMaxLength);
        asset.Property(entity => entity.Status)
            .HasMaxLength(32);
        asset.HasIndex(entity => entity.AssetCode)
            .IsUnique();
        asset.HasIndex(entity => entity.QrCodeValue)
            .IsUnique()
            .HasFilter("[QrCodeValue] IS NOT NULL");
        asset.HasIndex(entity => new { entity.AssetCategory, entity.Status });
        asset.ToTable("Assets", table =>
        {
            table.HasCheckConstraint(
                "CK_Assets_AssetCategory_Allowed",
                $"[AssetCategory] IN ({SqlIn(AssetCategoryCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_Assets_Status_Allowed",
                $"[Status] IN ({SqlIn(AssetStatusCatalog.PersistedValues)})");
        });

        var schedule = modelBuilder.Entity<PreventiveMaintenanceSchedule>();
        schedule.Property(entity => entity.PeriodType)
            .HasMaxLength(32);
        schedule.Property(entity => entity.Status)
            .HasMaxLength(32);
        schedule.Property(entity => entity.Quarter)
            .HasMaxLength(8);
        schedule.Property(entity => entity.Semester)
            .HasMaxLength(16);
        schedule.Property(entity => entity.AcademicYear)
            .HasMaxLength(16);
        schedule.HasIndex(entity => new { entity.AssetId, entity.Status, entity.ScheduleDate });
        schedule.HasIndex(entity => new { entity.Status, entity.ScheduleDate });
        schedule.ToTable("PreventiveMaintenanceSchedules", table =>
        {
            table.HasCheckConstraint(
                "CK_Schedules_PeriodType_Allowed",
                $"[PeriodType] IN ({SqlIn(SchedulePeriodTypeCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_Schedules_Status_Allowed",
                $"[Status] IN ({SqlIn(ScheduleStatusCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_Schedules_Quarter_Allowed",
                $"[Quarter] IS NULL OR [Quarter] IN ({SqlIn(ScheduleQuarterCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_Schedules_Semester_Allowed",
                $"[Semester] IS NULL OR [Semester] IN ({SqlIn(ScheduleSemesterCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_Schedules_AcademicYear_Format",
                "[AcademicYear] IS NULL OR [AcademicYear] LIKE '[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'");
        });

        var inspection = modelBuilder.Entity<InspectionRecord>();
        inspection.Property(entity => entity.Remarks)
            .HasMaxLength(2000);
        inspection.Property(entity => entity.ActionsRecommendations)
            .HasMaxLength(2000);

        var searchDocument = modelBuilder.Entity<MaintenanceSearchDocument>();
        searchDocument.Property(document => document.AssetCode)
            .HasMaxLength(AssetCodeValue.MaxLength);
        searchDocument.Property(document => document.AssetCategory)
            .HasMaxLength(64);
        searchDocument.Property(document => document.Building)
            .HasMaxLength(256);
        searchDocument.Property(document => document.Department)
            .HasMaxLength(256);
        searchDocument.Property(document => document.Location)
            .HasMaxLength(256);
        searchDocument.Property(document => document.IssueKeysJson)
            .HasMaxLength(1024);

        // Define relationships and indexes (hybrid search foundations)
        inspection
            .HasOne(i => i.Schedule)
            .WithMany()
            .HasForeignKey(i => i.ScheduleId)
            .OnDelete(DeleteBehavior.NoAction);
            
        inspection
            .HasOne(i => i.Asset)
            .WithMany()
            .HasForeignKey(i => i.AssetId)
            .OnDelete(DeleteBehavior.NoAction);

        searchDocument
            .HasKey(document => document.InspectionId);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasOne(document => document.Inspection)
            .WithOne()
            .HasForeignKey<MaintenanceSearchDocument>(document => document.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        searchDocument
            .Property(document => document.ProjectionVersion)
            .HasMaxLength(32);

        searchDocument
            .Property(document => document.LexiconVersion)
            .HasMaxLength(32);

        searchDocument
            .HasIndex(document => new { document.AssetId, document.DateInspected });

        searchDocument
            .HasIndex(document => document.ScheduleId);

        searchDocument
            .HasIndex(document => new { document.AssetCategory, document.DateInspected });

        searchDocument
            .HasIndex(document => new { document.IsOperational, document.DateInspected });
    }

    private static string SqlIn(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'"));
    }
}
