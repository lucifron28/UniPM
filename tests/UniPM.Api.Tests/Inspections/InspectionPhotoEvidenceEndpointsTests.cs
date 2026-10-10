using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class InspectionPhotoEvidenceEndpointsTests
{
    [Fact]
    public async Task Inspector_can_save_read_replace_and_remove_optional_draft_photo()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();
        var scenario = await application.CreateDraftAsync();
        var firstJpeg = JpegWithAppMetadata();
        var formUrl = $"/api/v1/preventive-maintenance-forms/{scenario.FormId}";
        var formWithoutPhoto = await client.GetFromJsonAsync<PreventiveMaintenanceFormResponse>(formUrl);
        Assert.NotNull(formWithoutPhoto);
        Assert.False(Assert.Single(formWithoutPhoto.Inspections).HasPhotoEvidence);

        using var firstContent = new ByteArrayContent(firstJpeg);
        firstContent.Headers.ContentType = new("image/jpeg");
        var upload = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            firstContent);

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
        var firstEvidence = await application.ReadEvidenceAsync(scenario.InspectionId);
        Assert.NotNull(firstEvidence);
        Assert.Equal(1, firstEvidence.Revision);
        Assert.Equal(firstEvidence.LengthBytes, application.Storage.Files[firstEvidence.StorageKey].Length);
        Assert.False(ContainsSequence(application.Storage.Files[firstEvidence.StorageKey], [0xff, 0xe1]));
        var formWithPhoto = await client.GetFromJsonAsync<PreventiveMaintenanceFormResponse>(formUrl);
        Assert.NotNull(formWithPhoto);
        Assert.True(Assert.Single(formWithPhoto.Inspections).HasPhotoEvidence);

        var read = await client.GetAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);
        Assert.Contains("private", read.Headers.CacheControl?.ToString());
        Assert.Contains("no-store", read.Headers.CacheControl?.ToString());
        Assert.Equal(application.Storage.Files[firstEvidence.StorageKey], await read.Content.ReadAsByteArrayAsync());

        using var replacementContent = new ByteArrayContent(MinimalJpeg());
        replacementContent.Headers.ContentType = new("image/jpeg");
        var replace = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            replacementContent);
        Assert.Equal(HttpStatusCode.NoContent, replace.StatusCode);
        var replacement = await application.ReadEvidenceAsync(scenario.InspectionId);
        Assert.NotNull(replacement);
        Assert.Equal(2, replacement.Revision);
        Assert.Single(application.Storage.Files);
        Assert.DoesNotContain(firstEvidence.StorageKey, application.Storage.Files.Keys);

        var delete = await client.DeleteAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Null(await application.ReadEvidenceAsync(scenario.InspectionId));
        Assert.Empty(application.Storage.Files);
    }

    [Fact]
    public async Task Photo_upload_rejects_wrong_content_type_invalid_jpeg_and_oversized_body()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();
        var scenario = await application.CreateDraftAsync();

        using var wrongType = new ByteArrayContent(MinimalJpeg());
        wrongType.Headers.ContentType = new("application/octet-stream");
        var unsupported = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            wrongType);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

        using var invalid = new ByteArrayContent([1, 2, 3, 4]);
        invalid.Headers.ContentType = new("image/jpeg");
        var invalidImage = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidImage.StatusCode);

        using var oversized = new ByteArrayContent(new byte[InspectionPhotoEvidenceOptions.MaximumUploadBytes + 1]);
        oversized.Headers.ContentType = new("image/jpeg");
        var tooLarge = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            oversized);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Empty(application.Storage.Files);
    }

    [Fact]
    public async Task Photo_upload_is_inspector_only_and_draft_only()
    {
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var gsdClient = gsdApplication.CreateClient();
        var gsdScenario = await gsdApplication.CreateDraftAsync();
        using var gsdContent = new ByteArrayContent(MinimalJpeg());
        gsdContent.Headers.ContentType = new("image/jpeg");
        var gsdUpload = await gsdClient.PutAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/photo",
            gsdContent);
        Assert.Equal(HttpStatusCode.Forbidden, gsdUpload.StatusCode);
        await gsdApplication.AddPhotoAsync(gsdScenario.InspectionId);
        var draftPhoto = await gsdClient.GetAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.NotFound, draftPhoto.StatusCode);
        await gsdApplication.SetFormStatusAsync(
            gsdScenario.FormId,
            PreventiveMaintenanceFormStatusCatalog.Acknowledged);
        var acknowledgedPhoto = await gsdClient.GetAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.OK, acknowledgedPhoto.StatusCode);

        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var inspectorClient = inspectorApplication.CreateClient();
        var submittedScenario = await inspectorApplication.CreateDraftAsync(
            PreventiveMaintenanceFormStatusCatalog.Submitted);
        using var inspectorContent = new ByteArrayContent(MinimalJpeg());
        inspectorContent.Headers.ContentType = new("image/jpeg");
        var submittedUpload = await inspectorClient.PutAsync(
            $"/api/v1/inspections/{submittedScenario.InspectionId}/photo",
            inspectorContent);
        Assert.Equal(HttpStatusCode.Conflict, submittedUpload.StatusCode);
        var submittedDelete = await inspectorClient.DeleteAsync(
            $"/api/v1/inspections/{submittedScenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.Conflict, submittedDelete.StatusCode);
    }

    [Fact]
    public async Task Inspector_cannot_read_or_change_another_inspectors_photo()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();
        var scenario = await application.CreateDraftAsync();
        await application.AddPhotoAsync(scenario.InspectionId);
        await application.ChangeInspectionOwnerAsync(scenario.InspectionId, Guid.NewGuid());

        var read = await client.GetAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");
        using var content = new ByteArrayContent(MinimalJpeg());
        content.Headers.ContentType = new("image/jpeg");
        var upload = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            content);
        var delete = await client.DeleteAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.NotNull(await application.ReadEvidenceAsync(scenario.InspectionId));
    }

    [Fact]
    public async Task Photo_endpoints_require_authentication()
    {
        await using var application = new TestApplicationFactory(authenticate: false);
        using var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/inspections/{Guid.NewGuid()}/photo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static bool ContainsSequence(byte[] source, byte[] value) =>
        source.AsSpan().IndexOf(value) >= 0;

    private static byte[] MinimalJpeg() =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
        0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
        0x11,
        0xff, 0xd9
    ];

    private static byte[] JpegWithAppMetadata() =>
    [
        0xff, 0xd8,
        0xff, 0xe1, 0x00, 0x06, 0x45, 0x58, 0x49, 0x46,
        .. MinimalJpeg()[2..]
    ];

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-photo-{Guid.NewGuid():N}";
        private readonly string[] roles;
        private readonly bool authenticate;

        public TestApplicationFactory(params string[] roles)
            : this(authenticate: true, roles)
        {
        }

        public TestApplicationFactory(bool authenticate, params string[] roles)
        {
            this.authenticate = authenticate;
            this.roles = roles;
        }

        public MemoryPhotoStorage Storage { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                if (authenticate) services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IInspectionPhotoEvidenceStorage>();
                services.AddSingleton<IInspectionPhotoEvidenceStorage>(Storage);
            });
        }

        public async Task<PhotoScenario> CreateDraftAsync(
            string formStatus = PreventiveMaintenanceFormStatusCatalog.Draft)
        {
            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "photo-test@unipm.local",
                NormalizedUserName = "PHOTO-TEST@UNIPM.LOCAL",
                Email = "photo-test@unipm.local",
                NormalizedEmail = "PHOTO-TEST@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "Photo Test User",
                IsActive = true
            };
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"PHOTO-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                AssetCategory = "fire-extinguisher",
                Department = "GSD",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(8)),
                PmCycle = "2026-08",
                PeriodType = "Quarter",
                Quarter = "Q3",
                Year = 2026,
                Status = ScheduleStatusCatalog.Due,
                CreatedAt = now,
                UpdatedAt = now
            };
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = asset.AssetCategory,
                Department = "GSD",
                PmCycle = schedule.PmCycle,
                PeriodType = schedule.PeriodType,
                Quarter = schedule.Quarter,
                Year = schedule.Year,
                Status = formStatus,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                AssetId = asset.Id,
                PreventiveMaintenanceFormId = form.Id,
                InspectorUserId = user.Id,
                DateInspected = now,
                IsOperational = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.Users.Add(user);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.PreventiveMaintenanceForms.Add(form);
            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();
            return new PhotoScenario(inspection.Id, schedule.Id, form.Id);
        }

        public async Task<InspectionPhotoEvidence?> ReadEvidenceAsync(Guid inspectionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.InspectionPhotoEvidence.AsNoTracking()
                .SingleOrDefaultAsync(row => row.InspectionId == inspectionId);
        }

        public async Task AddPhotoAsync(Guid inspectionId)
        {
            var photo = MinimalJpeg();
            var key = await Storage.SaveAsync(photo, CancellationToken.None);
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.InspectionPhotoEvidence.Add(new InspectionPhotoEvidence
            {
                InspectionId = inspectionId,
                StorageKey = key,
                LengthBytes = photo.Length,
                UploadedAt = DateTimeOffset.UtcNow,
                Revision = 1
            });
            await context.SaveChangesAsync();
        }

        public async Task ChangeInspectionOwnerAsync(Guid inspectionId, Guid ownerId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var inspection = await context.InspectionRecords
                .SingleAsync(candidate => candidate.Id == inspectionId);
            inspection.InspectorUserId = ownerId;
            await context.SaveChangesAsync();
        }

        public async Task SetFormStatusAsync(Guid formId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var form = await context.PreventiveMaintenanceForms.SingleAsync(row => row.Id == formId);
            form.Status = status;
            await context.SaveChangesAsync();
        }
    }

    private sealed class MemoryPhotoStorage : IInspectionPhotoEvidenceStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public Task<string> SaveAsync(ReadOnlyMemory<byte> photo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = $"{Guid.NewGuid():N}.jpg";
            Files[key] = photo.ToArray();
            return Task.FromResult(key);
        }

        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Files.GetValueOrDefault(storageKey));
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed record PhotoScenario(Guid InspectionId, Guid ScheduleId, Guid FormId);
}
