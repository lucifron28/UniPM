using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Forms;

public sealed class PreventiveMaintenanceFormDomainTests
{
    private const string TestPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";

    [Theory]
    [InlineData("image/png", false)]
    [InlineData(" IMAGE/PNG ", false)]
    [InlineData("image/jpeg", true)]
    public void Acknowledgement_signature_requires_png_media_type(string contentType, bool hasError)
    {
        var dto = NewAcknowledgementDto(TestPngBase64, contentType);

        var errors = dto.Validate(out _);

        Assert.Equal(hasError, errors.ContainsKey(nameof(dto.SignatureContentType)));
    }

    [Fact]
    public void Acknowledgement_signature_rejects_base64_data_without_png_magic_bytes()
    {
        var dto = NewAcknowledgementDto(Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        var errors = dto.Validate(out _);

        Assert.Contains(nameof(dto.SignatureData), errors.Keys);
        Assert.Contains("PNG", Assert.Single(errors[nameof(dto.SignatureData)]), StringComparison.Ordinal);
    }

    [Fact]
    public void Acknowledgement_signature_enforces_decoded_and_base64_size_boundaries()
    {
        const int decodedLimit = 196_608;
        const int base64Limit = 262_144;
        var png = Convert.FromBase64String(TestPngBase64);
        var atLimitBytes = new byte[decodedLimit];
        png.CopyTo(atLimitBytes, 0);
        var atLimitBase64 = Convert.ToBase64String(atLimitBytes);
        Assert.Equal(base64Limit, atLimitBase64.Length);

        var atLimitDto = NewAcknowledgementDto(atLimitBase64);
        var atLimitErrors = atLimitDto.Validate(out var decodedAtLimit);
        Assert.Empty(atLimitErrors);
        Assert.Equal(decodedLimit, decodedAtLimit.Length);

        var overLimitBytes = new byte[decodedLimit + 1];
        png.CopyTo(overLimitBytes, 0);
        var overLimitBase64 = Convert.ToBase64String(overLimitBytes);
        Assert.True(overLimitBase64.Length > base64Limit);

        var overLimitDto = NewAcknowledgementDto(overLimitBase64);
        var overLimitErrors = overLimitDto.Validate(out _);
        Assert.Contains(nameof(overLimitDto.SignatureData), overLimitErrors.Keys);
        Assert.Contains("base64 characters", Assert.Single(overLimitErrors[nameof(overLimitDto.SignatureData)]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_form_can_contain_multiple_inspection_rows()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"preventive-form-{Guid.NewGuid():N}")
            .Options;
        var formId = Guid.NewGuid();

        await using (var context = new ApplicationDbContext(options))
        {
            var form = NewForm(formId, PreventiveMaintenanceFormStatusCatalog.Draft);
            form.Inspections.Add(NewInspection(formId));
            form.Inspections.Add(NewInspection(formId));
            context.PreventiveMaintenanceForms.Add(form);
            await context.SaveChangesAsync();
        }

        await using var verification = new ApplicationDbContext(options);
        var stored = await verification.PreventiveMaintenanceForms
            .Include(form => form.Inspections)
            .SingleAsync(form => form.Id == formId);

        Assert.Equal(2, stored.Inspections.Count);
        Assert.All(stored.Inspections, inspection => Assert.Equal(formId, inspection.PreventiveMaintenanceFormId));
    }

    [Fact]
    public void Form_status_catalog_contains_only_the_confirmed_values()
    {
        Assert.Equal(
            ["Draft", "Submitted", "Acknowledged"],
            PreventiveMaintenanceFormStatusCatalog.PersistedValues);
        Assert.True(PreventiveMaintenanceFormStatusCatalog.TryNormalize(" submitted ", out var normalized));
        Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Submitted, normalized);
        Assert.False(PreventiveMaintenanceFormStatusCatalog.TryNormalize("Completed", out _));
    }

    private static PreventiveMaintenanceForm NewForm(Guid id, string status, string? fileNumber = null)
        => new()
        {
            Id = id,
            FileNumber = fileNumber,
            AssetCategory = "fire-alarm",
            Building = "Fictional Building",
            Department = "GSD",
            PeriodType = "Semester",
            Semester = "First",
            AcademicYear = "2026-2027",
            Status = status,
            CreatedByUserId = Guid.NewGuid()
        };

    private static InspectionRecord NewInspection(Guid formId)
        => new()
        {
            Id = Guid.NewGuid(),
            PreventiveMaintenanceFormId = formId,
            ScheduleId = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            InspectorUserId = Guid.NewGuid(),
            DateInspected = DateTimeOffset.UtcNow,
            IsOperational = true
        };

    private static AcknowledgePreventiveMaintenanceFormDto NewAcknowledgementDto(
        string signatureData,
        string contentType = "image/png")
        => new()
        {
            SignatoryName = "Fictional Department Head",
            SignatoryPosition = "Department Head",
            SignatureData = signatureData,
            SignatureContentType = contentType
        };
}
