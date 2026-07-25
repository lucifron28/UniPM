using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules => Set<PreventiveMaintenanceSchedule>();
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();
    public DbSet<MaintenanceSearchDocument> MaintenanceSearchDocuments => Set<MaintenanceSearchDocument>();
    public DbSet<MaintenanceSearchDocumentEmbedding> MaintenanceSearchDocumentEmbeddings => Set<MaintenanceSearchDocumentEmbedding>();
    public DbSet<ReferenceDocument> ReferenceDocuments => Set<ReferenceDocument>();
    public DbSet<ReferenceDocumentApplicability> ReferenceDocumentApplicabilities => Set<ReferenceDocumentApplicability>();
    public DbSet<ReferenceDocumentSection> ReferenceDocumentSections => Set<ReferenceDocumentSection>();
    public DbSet<ReferenceDocumentSectionEmbedding> ReferenceDocumentSectionEmbeddings => Set<ReferenceDocumentSectionEmbedding>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var user = modelBuilder.Entity<ApplicationUser>();
        user.Property(entity => entity.DisplayName)
            .HasMaxLength(160);
        user.Property(entity => entity.IsActive)
            .HasDefaultValue(true);

        var refreshSession = modelBuilder.Entity<RefreshSession>();
        refreshSession.ToTable("RefreshSessions");
        refreshSession.Property(entity => entity.TokenHash).HasMaxLength(64);
        refreshSession.Property(entity => entity.SecurityStampHash).HasMaxLength(64);
        refreshSession.Property(entity => entity.RevocationReason).HasMaxLength(64);
        refreshSession.Property(entity => entity.RowVersion).IsRowVersion();
        refreshSession.HasIndex(entity => entity.TokenHash).IsUnique();
        refreshSession.HasIndex(entity => new { entity.UserId, entity.TokenFamilyId });
        refreshSession.HasIndex(entity => new { entity.TokenFamilyId, entity.ExpiresAtUtc });
        refreshSession.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        refreshSession.HasOne(entity => entity.ReplacedBySession)
            .WithMany()
            .HasForeignKey(entity => entity.ReplacedBySessionId)
            .OnDelete(DeleteBehavior.NoAction);
        
        var asset = modelBuilder.Entity<Asset>();
        asset.Property(entity => entity.AssetCode)
            .HasMaxLength(AssetCodeValue.MaxLength);
        asset.Property(entity => entity.AssetCategory)
            .HasMaxLength(64);
        asset.Property(entity => entity.Building)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
        asset.Property(entity => entity.Department)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
        asset.Property(entity => entity.Location)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
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
        inspection.HasIndex(entity => entity.ScheduleId)
            .IsUnique();

        var searchDocument = modelBuilder.Entity<MaintenanceSearchDocument>();
        searchDocument.Property(document => document.AssetCode)
            .HasMaxLength(AssetCodeValue.MaxLength);
        searchDocument.Property(document => document.AssetCategory)
            .HasMaxLength(64);
        searchDocument.Property(document => document.Building)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
        searchDocument.Property(document => document.Department)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
        searchDocument.Property(document => document.Location)
            .HasMaxLength(AssetCodeValue.MetadataMaxLength);
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

        var searchDocumentEmbedding = modelBuilder.Entity<MaintenanceSearchDocumentEmbedding>();
        searchDocumentEmbedding
            .HasKey(embedding => embedding.InspectionId);
        searchDocumentEmbedding
            .Property(embedding => embedding.ProviderKey)
            .HasMaxLength(64);
        searchDocumentEmbedding
            .Property(embedding => embedding.ModelKey)
            .HasMaxLength(256);
        searchDocumentEmbedding
            .Property(embedding => embedding.EmbeddingProfile)
            .HasMaxLength(512);
        searchDocumentEmbedding
            .Property(embedding => embedding.Dimensions)
            .IsRequired();
        searchDocumentEmbedding
            .Property(embedding => embedding.VectorJson)
            .HasColumnType("nvarchar(max)");
        searchDocumentEmbedding
            .Property(embedding => embedding.SourceHash)
            .HasMaxLength(64);
        searchDocumentEmbedding
            .HasIndex(embedding => new { embedding.EmbeddingProfile, embedding.SourceHash });
        searchDocumentEmbedding
            .HasOne(embedding => embedding.SearchDocument)
            .WithOne(document => document.Embedding)
            .HasForeignKey<MaintenanceSearchDocumentEmbedding>(embedding => embedding.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);
        searchDocumentEmbedding.ToTable("MaintenanceSearchDocumentEmbeddings", table =>
        {
            table.HasCheckConstraint(
                "CK_MaintenanceSearchDocumentEmbeddings_Dimensions",
                "[Dimensions] BETWEEN 1 AND 4096");
            table.HasCheckConstraint(
                "CK_MaintenanceSearchDocumentEmbeddings_VectorJson",
                "ISJSON([VectorJson]) = 1");
        });

        var referenceDocument = modelBuilder.Entity<ReferenceDocument>();
        referenceDocument.Property(document => document.SourceType).HasMaxLength(32);
        referenceDocument.Property(document => document.SourceKey).HasMaxLength(128);
        referenceDocument.Property(document => document.Title).HasMaxLength(512);
        referenceDocument.Property(document => document.PublisherAuthority).HasMaxLength(256);
        referenceDocument.Property(document => document.Revision).HasMaxLength(128);
        referenceDocument.Property(document => document.LifecycleStatus).HasMaxLength(32);
        referenceDocument.Property(document => document.ContentChecksum).HasMaxLength(64);
        referenceDocument.Property(document => document.SyntheticFixtureKey).HasMaxLength(128);
        referenceDocument.HasIndex(document => new { document.SourceType, document.SourceKey, document.Revision }).IsUnique();
        referenceDocument.HasIndex(document => new { document.LifecycleStatus, document.SourceType });
        referenceDocument.HasOne(document => document.SupersededByDocument)
            .WithMany()
            .HasForeignKey(document => document.SupersededByDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        referenceDocument.ToTable("ReferenceDocuments", table =>
        {
            table.HasCheckConstraint(
                "CK_ReferenceDocuments_SourceType_Allowed",
                $"[SourceType] IN ({SqlIn(ReferenceDocumentSourceTypeCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_ReferenceDocuments_LifecycleStatus_Allowed",
                $"[LifecycleStatus] IN ({SqlIn(ReferenceDocumentLifecycleCatalog.PersistedValues)})");
            table.HasCheckConstraint(
                "CK_ReferenceDocuments_SupersessionLifecycle",
                "([LifecycleStatus] = 'Superseded' AND [SupersededByDocumentId] IS NOT NULL) OR ([LifecycleStatus] <> 'Superseded' AND [SupersededByDocumentId] IS NULL)");
            table.HasCheckConstraint(
                "CK_ReferenceDocuments_NoSelfSupersession",
                "[SupersededByDocumentId] IS NULL OR [SupersededByDocumentId] <> [Id]");
        });

        var applicability = modelBuilder.Entity<ReferenceDocumentApplicability>();
        applicability.Property(item => item.AssetCategory).HasMaxLength(64);
        applicability.Property(item => item.Manufacturer).HasMaxLength(128);
        applicability.Property(item => item.ModelSeries).HasMaxLength(128);
        applicability.Property(item => item.EquipmentFamily).HasMaxLength(128);
        applicability.Property(item => item.ScopeLabel).HasMaxLength(256);
        applicability.HasIndex(item => new { item.ReferenceDocumentId, item.AssetCategory });
        applicability.HasOne(item => item.ReferenceDocument)
            .WithMany(document => document.Applicabilities)
            .HasForeignKey(item => item.ReferenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        applicability.ToTable("ReferenceDocumentApplicabilities", table => table.HasCheckConstraint(
            "CK_ReferenceDocumentApplicabilities_AssetCategory_Allowed",
            $"[AssetCategory] IS NULL OR [AssetCategory] IN ({SqlIn(AssetCategoryCatalog.PersistedValues)})"));

        var referenceSection = modelBuilder.Entity<ReferenceDocumentSection>();
        referenceSection.Property(section => section.Heading).HasMaxLength(512);
        referenceSection.Property(section => section.SourceLocator).HasMaxLength(512);
        referenceSection.Property(section => section.SectionText).HasColumnType("nvarchar(max)");
        referenceSection.Property(section => section.SectionHash).HasMaxLength(64);
        referenceSection.HasIndex(section => new { section.ReferenceDocumentId, section.Sequence }).IsUnique();
        referenceSection.HasOne(section => section.ReferenceDocument)
            .WithMany(document => document.Sections)
            .HasForeignKey(section => section.ReferenceDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        referenceSection.ToTable("ReferenceDocumentSections", table =>
        {
            table.HasCheckConstraint("CK_ReferenceDocumentSections_Sequence", "[Sequence] >= 0");
            table.HasCheckConstraint("CK_ReferenceDocumentSections_PageStart", "[PageStart] IS NULL OR [PageStart] >= 1");
            table.HasCheckConstraint("CK_ReferenceDocumentSections_PageEnd", "[PageEnd] IS NULL OR [PageEnd] >= 1");
            table.HasCheckConstraint(
                "CK_ReferenceDocumentSections_PageRange",
                "[PageStart] IS NULL OR [PageEnd] IS NULL OR [PageEnd] >= [PageStart]");
        });

        var referenceEmbedding = modelBuilder.Entity<ReferenceDocumentSectionEmbedding>();
        referenceEmbedding.HasKey(embedding => embedding.ReferenceDocumentSectionId);
        referenceEmbedding.Property(embedding => embedding.ProviderKey).HasMaxLength(64);
        referenceEmbedding.Property(embedding => embedding.ModelKey).HasMaxLength(256);
        referenceEmbedding.Property(embedding => embedding.EmbeddingProfile).HasMaxLength(512);
        referenceEmbedding.Property(embedding => embedding.VectorJson).HasColumnType("nvarchar(max)");
        referenceEmbedding.Property(embedding => embedding.SectionHash).HasMaxLength(64);
        referenceEmbedding.HasIndex(embedding => new { embedding.EmbeddingProfile, embedding.SectionHash });
        referenceEmbedding.HasOne(embedding => embedding.ReferenceDocumentSection)
            .WithOne(section => section.Embedding)
            .HasForeignKey<ReferenceDocumentSectionEmbedding>(embedding => embedding.ReferenceDocumentSectionId)
            .OnDelete(DeleteBehavior.Cascade);
        referenceEmbedding.ToTable("ReferenceDocumentSectionEmbeddings", table =>
        {
            table.HasCheckConstraint(
                "CK_ReferenceDocumentSectionEmbeddings_Dimensions",
                "[Dimensions] BETWEEN 1 AND 4096");
            table.HasCheckConstraint(
                "CK_ReferenceDocumentSectionEmbeddings_VectorJson",
                "ISJSON([VectorJson]) = 1");
        });
    }

    private static string SqlIn(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'"));
    }
}
