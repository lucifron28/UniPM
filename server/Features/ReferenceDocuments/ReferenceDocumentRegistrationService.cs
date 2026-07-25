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
        registration = Normalize(registration);

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

        var now = DateTimeOffset.UtcNow;
        var checksum = ComputeDocumentChecksum(registration);
        if (existing is null)
        {
            await ValidateSupersessionAsync(context, null, registration, cancellationToken);
            existing = new ReferenceDocument { Id = registration.Id, ImportedAt = now };
            context.ReferenceDocuments.Add(existing);
        }
        else
        {
            if (!string.Equals(existing.SourceType, registration.SourceType, StringComparison.Ordinal)
                || !string.Equals(existing.SourceKey, registration.SourceKey, StringComparison.Ordinal)
                || !string.Equals(existing.Revision, registration.Revision, StringComparison.Ordinal))
            {
                throw new ReferenceDocumentRegistrationException(
                    "Reference document source type, source key, and revision are immutable for an existing document ID.");
            }

            if (!string.Equals(existing.ContentChecksum, checksum, StringComparison.Ordinal)
                || existing.IsSynthetic != registration.IsSynthetic
                || !string.Equals(existing.SyntheticFixtureKey, registration.SyntheticFixtureKey, StringComparison.Ordinal))
            {
                throw new ReferenceDocumentRegistrationException(
                    "Reference document material and provenance are immutable; register a new document ID and revision for changes.");
            }

            var isSuperseding = existing.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Active
                && registration.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Superseded;
            if (!string.Equals(existing.LifecycleStatus, registration.LifecycleStatus, StringComparison.Ordinal)
                && !isSuperseding)
            {
                throw new ReferenceDocumentRegistrationException(
                    "Only an explicit Active-to-Superseded lifecycle transition is permitted for an existing reference document.");
            }

            if (!isSuperseding
                && existing.SupersededByDocumentId != registration.SupersededByDocumentId)
            {
                throw new ReferenceDocumentRegistrationException(
                    "Supersession links are immutable except during an Active-to-Superseded transition.");
            }

            await ValidateSupersessionAsync(context, existing, registration, cancellationToken);
            if (!isSuperseding)
            {
                return;
            }
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

        if (existing.Applicabilities.Count == 0 && existing.Sections.Count == 0)
        {
            foreach (var item in registration.Applicabilities)
            {
                existing.Applicabilities.Add(new ReferenceDocumentApplicability
                {
                    Id = Guid.NewGuid(),
                    AssetCategory = item.AssetCategory,
                    Manufacturer = item.Manufacturer,
                    ModelSeries = item.ModelSeries,
                    EquipmentFamily = item.EquipmentFamily,
                    ScopeLabel = item.ScopeLabel
                });
            }

            foreach (var item in registration.Sections)
            {
                existing.Sections.Add(new ReferenceDocumentSection
                {
                    Id = item.Id,
                    Sequence = item.Sequence,
                    Heading = item.Heading,
                    SourceLocator = item.SourceLocator,
                    PageStart = item.PageStart,
                    PageEnd = item.PageEnd,
                    SectionText = item.SectionText,
                    SectionHash = ComputeSectionHash(item),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    internal static string ComputeSectionHash(ReferenceDocumentSectionRegistration section)
        => ComputeHash($"{section.Sequence}\n{section.Heading}\n{section.SourceLocator}\n{section.PageStart}\n{section.PageEnd}\n{section.SectionText}");

    private static string ComputeDocumentChecksum(ReferenceDocumentRegistration registration)
    {
        var parts = new List<string>
        {
            registration.SourceType,
            registration.SourceKey,
            registration.Revision,
            registration.Title,
            registration.PublisherAuthority,
            registration.EffectiveDate?.ToString("O") ?? string.Empty
        };
        parts.AddRange(registration.Applicabilities.Select(applicability => string.Join(
            "|",
            applicability.AssetCategory,
            applicability.Manufacturer,
            applicability.ModelSeries,
            applicability.EquipmentFamily,
            applicability.ScopeLabel)));
        parts.AddRange(registration.Sections.OrderBy(section => section.Sequence).Select(ComputeSectionHash));
        return ComputeHash(string.Join("\n", parts));
    }

    private static string ComputeHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task ValidateSupersessionAsync(
        ApplicationDbContext context,
        ReferenceDocument? existing,
        ReferenceDocumentRegistration registration,
        CancellationToken cancellationToken)
    {
        if (registration.SupersededByDocumentId is not { } supersededByDocumentId)
        {
            if (registration.LifecycleStatus == ReferenceDocumentLifecycleCatalog.Superseded)
            {
                throw new ReferenceDocumentRegistrationException(
                    "A superseded reference document must identify its active superseding revision.");
            }

            return;
        }

        if (supersededByDocumentId == registration.Id)
        {
            throw new ReferenceDocumentRegistrationException("A reference document cannot supersede itself.");
        }

        var supersedingDocument = await context.ReferenceDocuments.SingleOrDefaultAsync(
            document => document.Id == supersededByDocumentId,
            cancellationToken);
        if (supersedingDocument is null
            || supersedingDocument.LifecycleStatus != ReferenceDocumentLifecycleCatalog.Active
            || !string.Equals(supersedingDocument.SourceType, registration.SourceType, StringComparison.Ordinal)
            || !string.Equals(supersedingDocument.SourceKey, registration.SourceKey, StringComparison.Ordinal))
        {
            throw new ReferenceDocumentRegistrationException(
                "A supersession link must target an active revision of the same reference source.");
        }

        if (registration.LifecycleStatus != ReferenceDocumentLifecycleCatalog.Superseded)
        {
            throw new ReferenceDocumentRegistrationException(
                "Only a superseded reference document may carry a supersession link.");
        }
    }

    private static ReferenceDocumentRegistration Normalize(ReferenceDocumentRegistration registration)
    {
        ReferenceDocumentSourceTypeCatalog.TryNormalize(registration.SourceType, out var sourceType);
        ReferenceDocumentLifecycleCatalog.TryNormalize(registration.LifecycleStatus, out var lifecycleStatus);
        return registration with
        {
            SourceType = sourceType,
            LifecycleStatus = lifecycleStatus,
            SourceKey = registration.SourceKey.Trim(),
            Title = registration.Title.Trim(),
            PublisherAuthority = registration.PublisherAuthority.Trim(),
            Revision = registration.Revision.Trim(),
            Applicabilities = registration.Applicabilities.Select(NormalizeApplicability).ToArray(),
            Sections = registration.Sections.Select(section => section with
            {
                Heading = section.Heading.Trim(),
                SourceLocator = section.SourceLocator.Trim(),
                SectionText = NormalizeText(section.SectionText)
            }).ToArray()
        };
    }

    private static ReferenceDocumentApplicabilityRegistration NormalizeApplicability(
        ReferenceDocumentApplicabilityRegistration applicability)
    {
        var assetCategory = applicability.AssetCategory is null
            ? null
            : AssetCategoryCatalog.TryNormalize(applicability.AssetCategory, out var normalizedCategory)
                ? normalizedCategory
                : applicability.AssetCategory.Trim();
        return applicability with
        {
            AssetCategory = assetCategory,
            Manufacturer = TrimToNull(applicability.Manufacturer),
            ModelSeries = TrimToNull(applicability.ModelSeries),
            EquipmentFamily = TrimToNull(applicability.EquipmentFamily),
            ScopeLabel = TrimToNull(applicability.ScopeLabel)
        };
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        if (registration.Applicabilities.Count == 0)
        {
            errors.Add("At least one reference document applicability row is required.");
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

            if (AllBlank(applicability))
            {
                errors.Add("Reference document applicability must contain at least one applicability value.");
            }

            ValidateOptionalLength(applicability.Manufacturer, "manufacturer", 128, errors);
            ValidateOptionalLength(applicability.ModelSeries, "model or series", 128, errors);
            ValidateOptionalLength(applicability.EquipmentFamily, "equipment family", 128, errors);
            ValidateOptionalLength(applicability.ScopeLabel, "scope label", 256, errors);

            if (string.Equals(registration.SourceType.Trim(), ReferenceDocumentSourceTypeCatalog.Oem, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(applicability.Manufacturer)
                && string.IsNullOrWhiteSpace(applicability.ModelSeries)
                && string.IsNullOrWhiteSpace(applicability.EquipmentFamily)
                && string.IsNullOrWhiteSpace(applicability.ScopeLabel))
            {
                errors.Add("OEM reference applicability requires manufacturer, model or series, equipment family, or scope metadata.");
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

    private static bool AllBlank(ReferenceDocumentApplicabilityRegistration applicability)
        => string.IsNullOrWhiteSpace(applicability.AssetCategory)
            && string.IsNullOrWhiteSpace(applicability.Manufacturer)
            && string.IsNullOrWhiteSpace(applicability.ModelSeries)
            && string.IsNullOrWhiteSpace(applicability.EquipmentFamily)
            && string.IsNullOrWhiteSpace(applicability.ScopeLabel);

    private static void ValidateOptionalLength(string? value, string label, int maximumLength, List<string> errors)
    {
        if (value?.Trim().Length > maximumLength)
        {
            errors.Add($"Reference document applicability {label} must not exceed {maximumLength} characters.");
        }
    }
}

internal sealed class ReferenceDocumentRegistrationException(string message) : InvalidOperationException(message);
