using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceDocuments;

namespace UniPM.Api.Data.Seeding;

internal sealed class SyntheticReferenceDocumentSeeder(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ReferenceDocumentRegistrationService registrationService)
{
    public const string DatasetFileName = "synthetic-reference-documents-v1.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<SyntheticReferenceDocumentSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var dataset = await LoadAsync(cancellationToken);
        ValidateDataset(dataset);

        foreach (var document in dataset.Documents.OrderByDescending(document => document.LifecycleStatus == "Active"))
        {
            await registrationService.RegisterOrUpdateAsync(
                new ReferenceDocumentRegistration(
                    document.Id,
                    document.SourceType,
                    document.SourceKey,
                    document.Title,
                    document.PublisherAuthority,
                    document.Revision,
                    document.LifecycleStatus,
                    document.EffectiveDate,
                    document.SupersededByDocumentId,
                    true,
                    document.SeedKey,
                    document.Applicabilities.Select(item => new ReferenceDocumentApplicabilityRegistration(
                        item.AssetCategory,
                        item.Manufacturer,
                        item.ModelSeries,
                        item.EquipmentFamily,
                        item.ScopeLabel)).ToArray(),
                    document.Sections.Select(section => new ReferenceDocumentSectionRegistration(
                        section.Id,
                        section.Sequence,
                        section.Heading,
                        section.SourceLocator,
                        section.PageStart,
                        section.PageEnd,
                        section.SectionText)).ToArray()),
                cancellationToken);
        }

        return new SyntheticReferenceDocumentSeedResult(
            dataset.Documents.Count,
            dataset.Documents.Sum(document => document.Sections.Count));
    }

    public async Task<SyntheticReferenceDocumentResetResult> ResetAsync(CancellationToken cancellationToken = default)
    {
        var dataset = await LoadAsync(cancellationToken);
        ValidateDataset(dataset);
        var fixtureKeysById = dataset.Documents.ToDictionary(document => document.Id, document => document.SeedKey);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context.ReferenceDocuments
            .Where(document => document.IsSynthetic && fixtureKeysById.Keys.Contains(document.Id))
            .Include(document => document.Sections)
            .Include(document => document.Applicabilities)
            .ToListAsync(cancellationToken);
        var documents = candidates
            .Where(document => fixtureKeysById.TryGetValue(document.Id, out var fixtureKey)
                && string.Equals(document.SyntheticFixtureKey, fixtureKey, StringComparison.Ordinal))
            .ToList();
        var sections = documents.SelectMany(document => document.Sections).ToList();
        var embeddings = await context.ReferenceDocumentSectionEmbeddings
            .Where(embedding => sections.Select(section => section.Id).Contains(embedding.ReferenceDocumentSectionId))
            .ToListAsync(cancellationToken);

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        context.ReferenceDocumentSectionEmbeddings.RemoveRange(embeddings);
        context.ReferenceDocumentSections.RemoveRange(sections);
        context.ReferenceDocumentApplicabilities.RemoveRange(documents.SelectMany(document => document.Applicabilities));
        context.ReferenceDocuments.RemoveRange(documents);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new SyntheticReferenceDocumentResetResult(documents.Count, sections.Count);
    }

    private static void ValidateDataset(SyntheticReferenceDocumentDataset dataset)
    {
        if (!string.Equals(dataset.DatasetVersion, "1.1.0", StringComparison.Ordinal)
            || dataset.Documents.Count == 0
            || dataset.Documents.Any(document => document.Id == Guid.Empty
                || !document.Title.StartsWith("Fictional", StringComparison.Ordinal)
                || document.Sections.Count == 0)
            || dataset.Documents.Select(document => document.Id).Distinct().Count() != dataset.Documents.Count
            || dataset.Documents.Select(document => document.SeedKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dataset.Documents.Count)
        {
            throw new SyntheticReferenceDocumentFixtureException("The synthetic reference-document fixture is invalid.");
        }
    }

    private static async Task<SyntheticReferenceDocumentDataset> LoadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Seeding", "Resources", DatasetFileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SyntheticReferenceDocumentDataset>(stream, SerializerOptions, cancellationToken)
            ?? throw new SyntheticReferenceDocumentFixtureException("The synthetic reference-document fixture is empty.");
    }
}

public sealed record SyntheticReferenceDocumentSeedResult(int Documents, int Sections);
public sealed record SyntheticReferenceDocumentResetResult(int DocumentsRemoved, int SectionsRemoved);
public sealed class SyntheticReferenceDocumentFixtureException(string message) : InvalidOperationException(message);
