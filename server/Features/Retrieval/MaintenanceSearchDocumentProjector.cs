using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

public sealed class MaintenanceSearchDocumentProjector(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    MaintenanceIssueNormalizer issueNormalizer)
{
    public const string ProjectionVersion = "1.0.0";
    public const string LexiconVersion = MaintenanceIssueLexiconOptions.SupportedLexiconVersion;

    public MaintenanceSearchDocument Build(InspectionRecord inspection, Asset asset)
    {
        if (inspection.AssetId != asset.Id)
        {
            throw new InvalidOperationException("The inspection and asset IDs must match before projection.");
        }

        var assetCode = AssetCodeValue.Normalize(asset.AssetCode);
        if (!AssetCategoryCatalog.TryNormalize(asset.AssetCategory, out var assetCategory))
        {
            throw new InvalidOperationException(
                $"Asset '{asset.Id}' cannot be projected because its category is unsupported.");
        }
        var building = NormalizeSingleLineMetadata(asset.Building);
        var department = NormalizeSingleLineMetadata(asset.Department);
        var location = NormalizeSingleLineMetadata(asset.Location);
        var remarks = NormalizeSearchText(inspection.Remarks);
        var actionsRecommendations = NormalizeSearchText(inspection.ActionsRecommendations);
        var matches = issueNormalizer.Normalize(remarks, assetCategory);
        var issueKeys = matches.Select(match => match.IssueKey).ToArray();

        return new MaintenanceSearchDocument
        {
            InspectionId = inspection.Id,
            AssetId = asset.Id,
            ScheduleId = inspection.ScheduleId,
            AssetCode = assetCode,
            AssetCategory = assetCategory,
            Building = building,
            Department = department,
            Location = location,
            DateInspected = inspection.DateInspected,
            IsOperational = inspection.IsOperational,
            SourceCreatedAt = inspection.CreatedAt,
            SourceUpdatedAt = inspection.UpdatedAt,
            AssetUpdatedAt = asset.UpdatedAt,
            ProjectionVersion = ProjectionVersion,
            LexiconVersion = LexiconVersion,
            IssueKeysJson = JsonSerializer.Serialize(issueKeys),
            SearchText = BuildSearchText(
                inspection,
                assetCode,
                assetCategory,
                building,
                department,
                location,
                issueKeys,
                remarks,
                actionsRecommendations)
        };
    }

    public async Task<MaintenanceSearchDocumentRebuildResult> RebuildAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);

        var result = await RebuildWithinContextAsync(context, inspectionIds: null, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public async Task<MaintenanceSearchDocumentRebuildResult> RebuildAsync(
        ApplicationDbContext context,
        IReadOnlySet<Guid> inspectionIds,
        CancellationToken cancellationToken = default)
    {
        var result = await RebuildWithinContextAsync(context, inspectionIds, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<MaintenanceSearchDocumentRebuildResult> RebuildWithinContextAsync(
        ApplicationDbContext context,
        IReadOnlySet<Guid>? inspectionIds,
        CancellationToken cancellationToken)
    {
        var inspectionsQuery = context.InspectionRecords
            .AsNoTracking()
            .Include(inspection => inspection.Asset)
            .AsQueryable();

        if (inspectionIds is not null)
        {
            inspectionsQuery = inspectionsQuery.Where(inspection => inspectionIds.Contains(inspection.Id));
        }

        var inspections = await inspectionsQuery.ToListAsync(cancellationToken);
        var sourceDocuments = inspections
            .Select(inspection =>
            {
                if (inspection.Asset is null)
                {
                    throw new InvalidOperationException(
                        $"Inspection '{inspection.Id}' cannot be projected because its asset is missing.");
                }

                return Build(inspection, inspection.Asset);
            })
            .ToDictionary(document => document.InspectionId);

        var documentsQuery = context.MaintenanceSearchDocuments.AsQueryable();
        if (inspectionIds is not null)
        {
            documentsQuery = documentsQuery.Where(document => inspectionIds.Contains(document.InspectionId));
        }

        var existingDocuments = await documentsQuery.ToDictionaryAsync(
            document => document.InspectionId,
            cancellationToken);

        var created = 0;
        var updated = 0;

        foreach (var sourceDocument in sourceDocuments.Values)
        {
            if (!existingDocuments.TryGetValue(sourceDocument.InspectionId, out var existingDocument))
            {
                context.MaintenanceSearchDocuments.Add(sourceDocument);
                created++;
                continue;
            }

            if (ApplyIfChanged(sourceDocument, existingDocument))
            {
                updated++;
            }
        }

        var removed = 0;
        foreach (var existingDocument in existingDocuments.Values)
        {
            if (sourceDocuments.ContainsKey(existingDocument.InspectionId))
            {
                continue;
            }

            context.MaintenanceSearchDocuments.Remove(existingDocument);
            removed++;
        }

        return new MaintenanceSearchDocumentRebuildResult(
            created,
            updated,
            removed,
            sourceDocuments.Count);
    }

    private static bool ApplyIfChanged(
        MaintenanceSearchDocument source,
        MaintenanceSearchDocument target)
    {
        if (AreEqual(source, target))
        {
            return false;
        }

        target.AssetId = source.AssetId;
        target.ScheduleId = source.ScheduleId;
        target.AssetCode = source.AssetCode;
        target.AssetCategory = source.AssetCategory;
        target.Building = source.Building;
        target.Department = source.Department;
        target.Location = source.Location;
        target.DateInspected = source.DateInspected;
        target.IsOperational = source.IsOperational;
        target.SourceCreatedAt = source.SourceCreatedAt;
        target.SourceUpdatedAt = source.SourceUpdatedAt;
        target.AssetUpdatedAt = source.AssetUpdatedAt;
        target.ProjectionVersion = source.ProjectionVersion;
        target.LexiconVersion = source.LexiconVersion;
        target.IssueKeysJson = source.IssueKeysJson;
        target.SearchText = source.SearchText;
        return true;
    }

    private static bool AreEqual(
        MaintenanceSearchDocument left,
        MaintenanceSearchDocument right)
    {
        return left.AssetId == right.AssetId
            && left.ScheduleId == right.ScheduleId
            && left.AssetCode == right.AssetCode
            && left.AssetCategory == right.AssetCategory
            && left.Building == right.Building
            && left.Department == right.Department
            && left.Location == right.Location
            && left.DateInspected == right.DateInspected
            && left.IsOperational == right.IsOperational
            && left.SourceCreatedAt == right.SourceCreatedAt
            && left.SourceUpdatedAt == right.SourceUpdatedAt
            && left.AssetUpdatedAt == right.AssetUpdatedAt
            && left.ProjectionVersion == right.ProjectionVersion
            && left.LexiconVersion == right.LexiconVersion
            && left.IssueKeysJson == right.IssueKeysJson
            && left.SearchText == right.SearchText;
    }

    private static string BuildSearchText(
        InspectionRecord inspection,
        string assetCode,
        string assetCategory,
        string building,
        string department,
        string location,
        IReadOnlyList<string> issueKeys,
        string remarks,
        string actionsRecommendations)
    {
        return string.Join('\n',
            $"asset-code: {assetCode}",
            $"asset-category: {assetCategory}",
            $"building: {building}",
            $"department: {department}",
            $"location: {location}",
            $"date-inspected: {inspection.DateInspected:O}",
            $"operational-status: {(inspection.IsOperational ? "operational" : "not operational")}",
            $"issue-keys: {string.Join(", ", issueKeys)}",
            $"remarks: {remarks}",
            $"actions-recommendations: {actionsRecommendations}");
    }

    private static string NormalizeSearchText(string? value)
    {
        return NormalizeLineEndings(value).Trim();
    }

    private static string NormalizeSingleLineMetadata(string? value)
    {
        var normalized = NormalizeLineEndings(value).Trim();
        return string.Join(
            ' ',
            normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeLineEndings(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }
}

public sealed record MaintenanceSearchDocumentRebuildResult(
    int Created,
    int Updated,
    int Removed,
    int Total);
