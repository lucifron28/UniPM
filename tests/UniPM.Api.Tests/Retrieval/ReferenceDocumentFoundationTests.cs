using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class ReferenceDocumentFoundationTests
{
    [Fact]
    public async Task Registration_persists_source_provenance_sections_and_hashes_deterministically()
    {
        var databaseName = $"reference-documents-{Guid.NewGuid():N}";
        var factory = new TestContextFactory(databaseName);
        var service = new ReferenceDocumentRegistrationService(factory);
        var registration = CreateRegistration();

        await service.RegisterOrUpdateAsync(registration);
        await service.RegisterOrUpdateAsync(registration);

        await using var context = factory.CreateDbContext();
        var document = await context.ReferenceDocuments
            .Include(item => item.Applicabilities)
            .Include(item => item.Sections)
            .SingleAsync();

        Assert.Equal("Institutional", document.SourceType);
        Assert.Equal("Active", document.LifecycleStatus);
        Assert.True(document.IsSynthetic);
        Assert.Single(document.Applicabilities);
        var section = Assert.Single(document.Sections);
        Assert.Equal("Line one\nLine two", section.SectionText);
        Assert.Equal(
            ReferenceDocumentRegistrationService.ComputeSectionHash(
                "Scope",
                "FIC-001 R1, page 1",
                "Line one\nLine two"),
            section.SectionHash);
    }

    [Fact]
    public async Task Registration_rejects_invalid_category_duplicate_order_and_active_revision_replacement()
    {
        var factory = new TestContextFactory($"reference-documents-{Guid.NewGuid():N}");
        var service = new ReferenceDocumentRegistrationService(factory);
        var registration = CreateRegistration();

        await Assert.ThrowsAsync<ReferenceDocumentRegistrationException>(() => service.RegisterOrUpdateAsync(
            registration with
            {
                Applicabilities = [new ReferenceDocumentApplicabilityRegistration("unknown", null, null, null, null)]
            }));
        await Assert.ThrowsAsync<ReferenceDocumentRegistrationException>(() => service.RegisterOrUpdateAsync(
            registration with { Sections = [registration.Sections[0], registration.Sections[0] with { Id = Guid.NewGuid() }] }));

        await service.RegisterOrUpdateAsync(registration);
        await Assert.ThrowsAsync<ReferenceDocumentRegistrationException>(() => service.RegisterOrUpdateAsync(
            registration with { Title = "Changed fictional title" }));
    }

    [Fact]
    public async Task Embedding_constraints_are_modeled_as_one_to_one_section_data()
    {
        var factory = new TestContextFactory($"reference-documents-{Guid.NewGuid():N}");
        var service = new ReferenceDocumentRegistrationService(factory);
        await service.RegisterOrUpdateAsync(CreateRegistration());

        await using var context = factory.CreateDbContext();
        var section = await context.ReferenceDocumentSections.SingleAsync();
        context.ReferenceDocumentSectionEmbeddings.Add(new ReferenceDocumentSectionEmbedding
        {
            ReferenceDocumentSectionId = section.Id,
            ProviderKey = "test-provider",
            ModelKey = "test-model",
            EmbeddingProfile = "test-provider:test-model:reference-section-v1:2",
            Dimensions = 2,
            VectorJson = "[1,0]",
            SectionHash = section.SectionHash,
            GeneratedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        Assert.Single(await context.ReferenceDocumentSectionEmbeddings.ToListAsync());
    }

    [Fact]
    public async Task Fictional_fixture_is_idempotent_and_reset_is_scoped_to_synthetic_documents()
    {
        var factory = new TestContextFactory($"reference-documents-{Guid.NewGuid():N}");
        var service = new ReferenceDocumentRegistrationService(factory);
        var seeder = new SyntheticReferenceDocumentSeeder(factory, service);

        var first = await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        Assert.Equal(first, second);
        Assert.Equal(3, first.Documents);
        await using (var context = factory.CreateDbContext())
        {
            context.ReferenceDocuments.Add(new ReferenceDocument
            {
                Id = Guid.NewGuid(),
                SourceType = "Institutional",
                SourceKey = "NON-SYNTHETIC",
                Title = "Fictional nonfixture record",
                PublisherAuthority = "Fictional authority",
                Revision = "R1",
                LifecycleStatus = "Archived",
                ImportedAt = DateTimeOffset.UtcNow,
                ContentChecksum = "ABC",
                IsSynthetic = false
            });
            await context.SaveChangesAsync();
        }

        await seeder.ResetAsync();

        await using var verification = factory.CreateDbContext();
        Assert.Single(await verification.ReferenceDocuments.ToListAsync());
        Assert.False((await verification.ReferenceDocuments.SingleAsync()).IsSynthetic);
    }

    private static ReferenceDocumentRegistration CreateRegistration()
        => new(
            Guid.Parse("22000000-0000-0000-0000-000000000001"),
            "Institutional",
            "FIC-001",
            "Fictional Reference Guide",
            "Fictional University Office",
            "R1",
            "Active",
            new DateOnly(2026, 1, 1),
            null,
            true,
            "fictional-reference",
            [new ReferenceDocumentApplicabilityRegistration("fire-extinguisher", null, null, null, "Fictional scope")],
            [new ReferenceDocumentSectionRegistration(
                Guid.Parse("23000000-0000-0000-0000-000000000001"),
                0,
                "Scope",
                "FIC-001 R1, page 1",
                1,
                1,
                "Line one\r\nLine two")]);

    private sealed class TestContextFactory(string databaseName) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
            => new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
