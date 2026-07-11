using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class MaintenanceSearchDocumentProjectorTests
{
    [Fact]
    public void Build_creates_the_exact_deterministic_search_text()
    {
        var assetUpdatedAt = AtManila(2025, 5, 1, 8);
        var inspection = CreateInspection(
            remarks: "mahina ang pressure",
            actionsRecommendations: "Schedule a refill and follow-up check.",
            dateInspected: AtManila(2025, 5, 15, 8));
        inspection.CreatedAt = AtManila(2025, 5, 15, 8, 1);
        inspection.UpdatedAt = AtManila(2025, 5, 15, 8, 2);
        var asset = CreateAsset(assetUpdatedAt);

        var document = CreateProjector().Build(inspection, asset);

        Assert.Equal("1.0.0", document.ProjectionVersion);
        Assert.Equal("1.0.0", document.LexiconVersion);
        Assert.Equal(assetUpdatedAt, document.AssetUpdatedAt);
        Assert.Equal("[\"low_pressure\"]", document.IssueKeysJson);
        Assert.Equal(
            string.Join('\n',
                "asset-code: FE-001",
                "asset-category: fire-extinguisher",
                "building: Main Building",
                "department: Administration",
                "location: Ground Floor",
                "date-inspected: 2025-05-15T08:00:00.0000000+08:00",
                "operational-status: not operational",
                "issue-keys: low_pressure",
                "remarks: mahina ang pressure",
                "actions-recommendations: Schedule a refill and follow-up check."),
            document.SearchText);
    }

    [Fact]
    public void Build_normalizes_line_endings_and_single_line_metadata()
    {
        var asset = CreateAsset();
        asset.AssetCode = "FE\r\n001";
        asset.Building = "Main\r\nBuilding";
        asset.Department = "GSD\rServices";
        asset.Location = "East\r\nHallway";
        var inspection = CreateInspection(
            remarks: "mahina\r\nang pressure\rfor refill",
            actionsRecommendations: "Line one\r\nLine two\rLine three");

        var document = CreateProjector().Build(inspection, asset);

        Assert.Equal(
            string.Join('\n',
                "asset-code: FE 001",
                "asset-category: fire-extinguisher",
                "building: Main Building",
                "department: GSD Services",
                "location: East Hallway",
                "date-inspected: 2025-05-15T08:00:00.0000000+08:00",
                "operational-status: not operational",
                "issue-keys: low_pressure",
                "remarks: mahina\nang pressure\nfor refill",
                "actions-recommendations: Line one\nLine two\nLine three"),
            document.SearchText);
    }

    [Fact]
    public void Build_derives_issue_keys_from_remarks_and_keeps_recommendations_raw()
    {
        var inspection = CreateInspection(
            remarks: "hindi nagrerespond ang smoke detector",
            actionsRecommendations: "Check the alarm panel and detector connection.");

        var asset = CreateAsset();
        asset.AssetCategory = "fire-alarm";
        var document = CreateProjector().Build(inspection, asset);

        Assert.Equal(
            "[\"smoke_detector_issue\",\"device_not_responding\"]",
            document.IssueKeysJson);
        Assert.Contains(
            "actions-recommendations: Check the alarm panel and detector connection.",
            document.SearchText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_does_not_create_a_false_positive_from_recommendations_only()
    {
        var inspection = CreateInspection(
            remarks: "Operational",
            actionsRecommendations: "Review the low pressure procedure next quarter.");

        var document = CreateProjector().Build(inspection, CreateAsset());

        Assert.Equal("[]", document.IssueKeysJson);
        Assert.Contains("low pressure procedure", document.SearchText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_respects_asset_category_boundaries()
    {
        var asset = CreateAsset();
        asset.AssetCategory = "water-drinking-station";

        var document = CreateProjector().Build(
            CreateInspection(remarks: "low pressure"),
            asset);

        Assert.Equal("[]", document.IssueKeysJson);
    }

    [Fact]
    public void Build_with_empty_findings_produces_stable_metadata_only_text()
    {
        var asset = CreateAsset();
        var nullDocument = CreateProjector().Build(
            CreateInspection(remarks: null, actionsRecommendations: null),
            asset);
        var emptyDocument = CreateProjector().Build(
            CreateInspection(remarks: string.Empty, actionsRecommendations: string.Empty),
            asset);

        Assert.Equal("[]", nullDocument.IssueKeysJson);
        Assert.Equal(nullDocument.SearchText, emptyDocument.SearchText);
        Assert.Equal(
            string.Join('\n',
                "asset-code: FE-001",
                "asset-category: fire-extinguisher",
                "building: Main Building",
                "department: Administration",
                "location: Ground Floor",
                "date-inspected: 2025-05-15T08:00:00.0000000+08:00",
                "operational-status: not operational",
                "issue-keys: ",
                "remarks: ",
                "actions-recommendations: "),
            nullDocument.SearchText);
    }

    [Fact]
    public async Task Rebuild_updates_stale_asset_metadata_and_asset_timestamp()
    {
        var factory = new TestContextFactory();
        var asset = CreateAsset(AtManila(2025, 5, 1, 8));
        var inspection = CreateInspection();

        await AddSourceRecordsAsync(factory, asset, inspection);
        var projector = CreateProjector(factory);
        Assert.Equal(new MaintenanceSearchDocumentRebuildResult(1, 0, 0, 1), await projector.RebuildAsync());

        var updatedAt = AtManila(2025, 6, 1, 9);
        await using (var context = factory.CreateDbContext())
        {
            var storedAsset = await context.Assets.SingleAsync(candidate => candidate.Id == asset.Id);
            storedAsset.Building = "Annex Building";
            storedAsset.Location = "Second Floor Hallway";
            storedAsset.UpdatedAt = updatedAt;
            await context.SaveChangesAsync();
        }

        Assert.Equal(new MaintenanceSearchDocumentRebuildResult(0, 1, 0, 1), await projector.RebuildAsync());

        await using var verificationContext = factory.CreateDbContext();
        var document = await verificationContext.MaintenanceSearchDocuments.SingleAsync();
        Assert.Equal("Annex Building", document.Building);
        Assert.Equal("Second Floor Hallway", document.Location);
        Assert.Equal(updatedAt, document.AssetUpdatedAt);
        Assert.Contains("building: Annex Building", document.SearchText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rebuild_updates_edited_inspection_content_and_versions()
    {
        var factory = new TestContextFactory();
        var asset = CreateAsset();
        var inspection = CreateInspection();

        await AddSourceRecordsAsync(factory, asset, inspection);
        var projector = CreateProjector(factory);
        await projector.RebuildAsync();

        var updatedAt = AtManila(2025, 6, 2, 9);
        await using (var context = factory.CreateDbContext())
        {
            var storedInspection = await context.InspectionRecords.SingleAsync(candidate => candidate.Id == inspection.Id);
            storedInspection.Remarks = "expired unit";
            storedInspection.UpdatedAt = updatedAt;

            var storedDocument = await context.MaintenanceSearchDocuments.SingleAsync();
            storedDocument.ProjectionVersion = "stale-projection";
            storedDocument.LexiconVersion = "stale-lexicon";
            await context.SaveChangesAsync();
        }

        Assert.Equal(new MaintenanceSearchDocumentRebuildResult(0, 1, 0, 1), await projector.RebuildAsync());

        await using var verificationContext = factory.CreateDbContext();
        var document = await verificationContext.MaintenanceSearchDocuments.SingleAsync();
        Assert.Equal("[\"expired_unit\"]", document.IssueKeysJson);
        Assert.Contains("remarks: expired unit", document.SearchText, StringComparison.Ordinal);
        Assert.Equal(updatedAt, document.SourceUpdatedAt);
        Assert.Equal(MaintenanceSearchDocumentProjector.ProjectionVersion, document.ProjectionVersion);
        Assert.Equal(MaintenanceSearchDocumentProjector.LexiconVersion, document.LexiconVersion);
    }

    [Fact]
    public async Task Rebuild_is_idempotent_and_does_not_duplicate_documents()
    {
        var factory = new TestContextFactory();
        await AddSourceRecordsAsync(factory, CreateAsset(), CreateInspection());
        var projector = CreateProjector(factory);

        Assert.Equal(new MaintenanceSearchDocumentRebuildResult(1, 0, 0, 1), await projector.RebuildAsync());
        Assert.Equal(new MaintenanceSearchDocumentRebuildResult(0, 0, 0, 1), await projector.RebuildAsync());

        await using var context = factory.CreateDbContext();
        Assert.Equal(1, await context.MaintenanceSearchDocuments.CountAsync());
    }

    [Fact]
    public void Build_excludes_evaluation_labels_and_private_source_fields()
    {
        var document = CreateProjector().Build(
            CreateInspection(
                remarks: "Pressure is normal.",
                actionsRecommendations: "No corrective action required."),
            CreateAsset());

        Assert.DoesNotContain("expectedIssueKeys", document.SearchText, StringComparison.Ordinal);
        Assert.DoesNotContain("scenarioTags", document.SearchText, StringComparison.Ordinal);
        Assert.DoesNotContain("isColdStartAsset", document.SearchText, StringComparison.Ordinal);
        Assert.DoesNotContain("RmrfNumber", document.SearchText, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIPM-FIRE-EXTINGUISHER", document.SearchText, StringComparison.Ordinal);
        Assert.Null(typeof(MaintenanceSearchDocument).GetProperty(nameof(InspectionRecord.InspectorUserId)));
        Assert.Null(typeof(MaintenanceSearchDocument).GetProperty(nameof(InspectionRecord.RemarksEmbedding)));
    }

    private static MaintenanceSearchDocumentProjector CreateProjector(TestContextFactory? factory = null)
    {
        var root = FindRepositoryRoot();
        var lexiconPath = Path.Combine(
            root,
            "server",
            "Features",
            "Retrieval",
            "Resources",
            MaintenanceIssueLexiconOptions.LexiconFileName);
        var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = lexiconPath
        });

        return new MaintenanceSearchDocumentProjector(
            factory ?? new TestContextFactory(),
            new MaintenanceIssueNormalizer(loader));
    }

    private static async Task AddSourceRecordsAsync(
        TestContextFactory factory,
        Asset asset,
        InspectionRecord inspection)
    {
        await using var context = factory.CreateDbContext();
        context.Assets.Add(asset);
        context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
        {
            Id = inspection.ScheduleId,
            AssetId = asset.Id,
            ScheduleDate = inspection.DateInspected,
            PeriodType = "Quarter",
            Status = "Completed",
            CompletedAt = inspection.DateInspected
        });
        context.InspectionRecords.Add(inspection);
        await context.SaveChangesAsync();
    }

    private static Asset CreateAsset(DateTimeOffset? updatedAt = null)
    {
        var createdAt = AtManila(2025, 5, 1, 8);
        return new Asset
        {
            Id = Guid.Parse("f6420fec-4fb8-5c78-8299-12767e924b1a"),
            AssetCode = "FE-001",
            AssetCategory = "fire-extinguisher",
            Building = "Main Building",
            Department = "Administration",
            Location = "Ground Floor",
            QrCodeValue = "UNIPM-FIRE-EXTINGUISHER-F6420FEC",
            Status = "Active",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt ?? createdAt
        };
    }

    private static InspectionRecord CreateInspection(
        string? remarks = "mahina ang pressure",
        string? actionsRecommendations = "Schedule a refill.",
        DateTimeOffset? dateInspected = null)
    {
        var inspectedAt = dateInspected ?? AtManila(2025, 5, 15, 8);
        return new InspectionRecord
        {
            Id = Guid.Parse("267babd6-4e6d-5082-843a-6db5d9d3e5ae"),
            ScheduleId = Guid.Parse("e0665856-e579-5eab-9453-610e81631115"),
            AssetId = Guid.Parse("f6420fec-4fb8-5c78-8299-12767e924b1a"),
            InspectorUserId = Guid.NewGuid(),
            DateInspected = inspectedAt,
            IsOperational = false,
            Remarks = remarks,
            ActionsRecommendations = actionsRecommendations,
            CreatedAt = inspectedAt,
            UpdatedAt = inspectedAt
        };
    }

    private static DateTimeOffset AtManila(int year, int month, int day, int hour, int minute = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root for projection tests.");
    }

    private sealed class TestContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-search-document-{Guid.NewGuid():N}")
            .Options;

        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
