using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Forms;

public sealed class PreventiveMaintenanceFormDomainTests
{
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
}
