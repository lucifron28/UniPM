using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

namespace UniPM.Api.Features.ReferenceDocuments;

internal sealed record ReferenceDocumentRegistration(
    Guid Id,
    string SourceType,
    string SourceKey,
    string Title,
    string PublisherAuthority,
    string Revision,
    string LifecycleStatus,
    DateOnly? EffectiveDate,
    Guid? SupersededByDocumentId,
    bool IsSynthetic,
    string? SyntheticFixtureKey,
    IReadOnlyList<ReferenceDocumentApplicabilityRegistration> Applicabilities,
    IReadOnlyList<ReferenceDocumentSectionRegistration> Sections);

internal sealed record ReferenceDocumentApplicabilityRegistration(
    string? AssetCategory,
    string? Manufacturer,
    string? ModelSeries,
    string? EquipmentFamily,
    string? ScopeLabel);

internal sealed record ReferenceDocumentSectionRegistration(
    Guid Id,
    int Sequence,
    string Heading,
    string SourceLocator,
    int? PageStart,
    int? PageEnd,
    string SectionText);

internal sealed class ReferenceDocumentRegistrationService(
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task RegisterOrUpdateAsync(
        ReferenceDocumentRegistration registration,
        CancellationToken cancellationToken = default)
    {
        Validate(registration);
        ReferenceDocumentSourceTypeCatalog.TryNormalize(registration.SourceType, out var sourceType);
        ReferenceDocumentLifecycleCatalog.TryNormalize(registration.LifecycleStatus, out var lifecycleStatus);
        registration = registration with
        {
            SourceType = sourceType,
            LifecycleStatus = lifecycleStatus,
            SourceKey = registration.SourceKey.Trim(),
            Title = registration.Title.Trim(),
            PublisherAuthority = registration.PublisherAuthority.Trim(),
            Revision = registration.Revision.Trim()
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var existing = await context.ReferenceDocuments
            .Include(document => document.Applicabilities)
            .Include(document => document.Sections)
            .SingleOrDefaultAsync(document => document.Id == registration.Id, cancellationToken);

        var conflictingRevision = await context.ReferenceDocuments.AnyAsync(document =>
            document.Id != registration.Id
            && document.SourceType == registration.SourceType
            && document.SourceKey == registration.SourceKey
            && document.Revision == registration.Revision,
            cancellationToken);
        if (conflictingRevision)
        {
            throw new ReferenceDocumentRegistrationException(
                "A reference document with the same source type, source key, and revision already exists.");
        }

        if (registration.SupersededByDocumentId is { } supersededBy
            && !await context.ReferenceDocuments.AnyAsync(document => document.Id == supersededBy, cancellationToken))
        {
            throw new ReferenceDocumentRegistrationException("The superseding reference document does not exist.");
        }

        var now = DateTimeOffset.UtcNow;
        var checksum = ComputeDocumentChecksum(registration);
        if (existing is null)
        {
            existing = new ReferenceDocument { Id = registration.Id, ImportedAt = now };
            context.ReferenceDocuments.Add(existing);
        }
        else if (existing.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Active
                 && !string.Equals(existing.ContentChecksum, checksum, StringComparison.Ordinal)
                 && existing.SourceKey == registration.SourceKey
                 && existing.Revision == registration.Revision)
        {
            throw new ReferenceDocumentRegistrationException(
                "An active reference revision cannot be silently replaced; register a new revision and link supersession explicitly.");
        }

        existing.SourceType = registration.SourceType;
        existing.SourceKey = registration.SourceKey;
        existing.Title = registration.Title;
        existing.PublisherAuthority = registration.PublisherAuthority;
        existing.Revision = registration.Revision;
        existing.LifecycleStatus = registration.LifecycleStatus;
        existing.EffectiveDate = registration.EffectiveDate;
        existing.SupersededByDocumentId = registration.SupersededByDocumentId;
        existing.ContentChecksum = checksum;
        existing.IsSynthetic = registration.IsSynthetic;
        existing.SyntheticFixtureKey = registration.SyntheticFixtureKey;

        var existingApplicabilities = existing.Applicabilities.ToList();
        for (var index = 0; index < registration.Applicabilities.Count; index++)
        {
            var item = registration.Applicabilities[index];
            var applicability = index < existingApplicabilities.Count
                ? existingApplicabilities[index]
                : new ReferenceDocumentApplicability { Id = Guid.NewGuid() };
            if (index >= existingApplicabilities.Count)
            {
                existing.Applicabilities.Add(applicability);
            }

            applicability.AssetCategory = item.AssetCategory;
            applicability.Manufacturer = item.Manufacturer;
            applicability.ModelSeries = item.ModelSeries;
            applicability.EquipmentFamily = item.EquipmentFamily;
            applicability.ScopeLabel = item.ScopeLabel;
        }

        foreach (var obsolete in existingApplicabilities.Skip(registration.Applicabilities.Count))
        {
            context.ReferenceDocumentApplicabilities.Remove(obsolete);
        }

        var sectionsById = existing.Sections.ToDictionary(section => section.Id);
        var registrationIds = registration.Sections.Select(section => section.Id).ToHashSet();
        foreach (var obsolete in existing.Sections.Where(section => !registrationIds.Contains(section.Id)).ToList())
        {
            context.ReferenceDocumentSections.Remove(obsolete);
        }

        foreach (var item in registration.Sections)
        {
            if (!sectionsById.TryGetValue(item.Id, out var section))
            {
                section = new ReferenceDocumentSection { Id = item.Id, CreatedAt = now };
                existing.Sections.Add(section);
            }

            section.Sequence = item.Sequence;
            section.Heading = item.Heading.Trim();
            section.SourceLocator = item.SourceLocator.Trim();
            section.PageStart = item.PageStart;
            section.PageEnd = item.PageEnd;
            section.SectionText = NormalizeText(item.SectionText);
            section.SectionHash = ComputeSectionHash(section.Heading, section.SourceLocator, section.SectionText);
            section.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    internal static string ComputeSectionHash(string heading, string sourceLocator, string sectionText)
        => ComputeHash($"{heading.Trim()}\n{sourceLocator.Trim()}\n{NormalizeText(sectionText)}");

    private static string ComputeDocumentChecksum(ReferenceDocumentRegistration registration)
        => ComputeHash(string.Join(
            "\n",
            registration.SourceType,
            registration.SourceKey.Trim(),
            registration.Revision.Trim(),
            registration.Title.Trim(),
            registration.Sections.OrderBy(section => section.Sequence).Select(section =>
                ComputeSectionHash(section.Heading, section.SourceLocator, section.SectionText))));

    private static string ComputeHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string NormalizeText(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static void Validate(ReferenceDocumentRegistration registration)
    {
        var errors = new List<string>();
        if (registration.Id == Guid.Empty)
        {
            errors.Add("Reference document ID is required.");
        }

        if (!ReferenceDocumentSourceTypeCatalog.TryNormalize(registration.SourceType, out _))
        {
            errors.Add("Reference document source type is unsupported.");
        }

        if (!ReferenceDocumentLifecycleCatalog.TryNormalize(registration.LifecycleStatus, out _))
        {
            errors.Add("Reference document lifecycle status is unsupported.");
        }

        ValidateRequired(registration.SourceKey, "source key", 128, errors);
        ValidateRequired(registration.Title, "title", 512, errors);
        ValidateRequired(registration.PublisherAuthority, "publisher or authority", 256, errors);
        ValidateRequired(registration.Revision, "revision", 128, errors);
        if (registration.Sections.Count == 0)
        {
            errors.Add("At least one reference document section is required.");
        }

        if (registration.Sections.GroupBy(section => section.Sequence).Any(group => group.Count() > 1))
        {
            errors.Add("Reference document section sequence values must be unique per document.");
        }

        foreach (var section in registration.Sections)
        {
            if (section.Id == Guid.Empty || section.Sequence < 0)
            {
                errors.Add("Reference document section IDs and non-negative sequence values are required.");
            }

            ValidateRequired(section.Heading, "section heading", 512, errors);
            ValidateRequired(section.SourceLocator, "section source locator", 512, errors);
            if (string.IsNullOrWhiteSpace(section.SectionText))
            {
                errors.Add("Reference document section text must not be blank.");
            }

            if (section.PageStart is < 1 || section.PageEnd is < 1
                || section.PageEnd is not null && section.PageStart is not null && section.PageEnd < section.PageStart)
            {
                errors.Add("Reference document section page ranges are invalid.");
            }
        }

        foreach (var applicability in registration.Applicabilities)
        {
            if (applicability.AssetCategory is not null
                && !AssetCategoryCatalog.TryNormalize(applicability.AssetCategory, out _))
            {
                errors.Add("Reference document applicability uses an unsupported asset category.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ReferenceDocumentRegistrationException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateRequired(string value, string label, int maximumLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            errors.Add($"Reference document {label} is required and must not exceed {maximumLength} characters.");
        }
    }
}

internal sealed class ReferenceDocumentRegistrationException(string message) : InvalidOperationException(message);
