using Microsoft.EntityFrameworkCore;
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
        
        // Define relationships and indexes (hybrid search foundations)
        modelBuilder.Entity<InspectionRecord>()
            .HasOne(i => i.Schedule)
            .WithMany()
            .HasForeignKey(i => i.ScheduleId)
            .OnDelete(DeleteBehavior.NoAction);
            
        modelBuilder.Entity<InspectionRecord>()
            .HasOne(i => i.Asset)
            .WithMany()
            .HasForeignKey(i => i.AssetId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasKey(document => document.InspectionId);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasOne(document => document.Inspection)
            .WithOne()
            .HasForeignKey<MaintenanceSearchDocument>(document => document.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .Property(document => document.AssetCategory)
            .HasMaxLength(64);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .Property(document => document.ProjectionVersion)
            .HasMaxLength(32);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .Property(document => document.LexiconVersion)
            .HasMaxLength(32);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasIndex(document => new { document.AssetId, document.DateInspected });

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasIndex(document => document.ScheduleId);

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasIndex(document => new { document.AssetCategory, document.DateInspected });

        modelBuilder.Entity<MaintenanceSearchDocument>()
            .HasIndex(document => new { document.IsOperational, document.DateInspected });
    }
}
