using Microsoft.EntityFrameworkCore;
using UniPM.Api.Models;

namespace UniPM.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules => Set<PreventiveMaintenanceSchedule>();
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();

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
    }
}
