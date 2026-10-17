using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Tests;

internal static class CampusSeedAssertions
{
    internal static async Task<string> VerifyAsync(ApplicationDbContext context)
    {
        Assert.Equal(new[] { 2, 5, 8, 11 }, CpmpScheduleFrequency.GetMonths("fire-extinguisher"));
        Assert.Equal(new[] { 6, 12 }, CpmpScheduleFrequency.GetMonths("fire-alarm"));
        Assert.Equal(new[] { 6, 12 }, CpmpScheduleFrequency.GetMonths("emergency-light"));
        Assert.Equal(new[] { 2, 5, 8, 11 }, CpmpScheduleFrequency.GetMonths("water-drinking-station"));
        var assets = await context.Assets.AsNoTracking().OrderBy(asset => asset.AssetCode).ToListAsync();
        var forms = await context.PreventiveMaintenanceForms.AsNoTracking().ToListAsync();
        var inspections = await context.InspectionRecords.AsNoTracking().ToListAsync();
        var schedules = await context.PreventiveMaintenanceSchedules.AsNoTracking().ToListAsync();
        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements.AsNoTracking().ToListAsync();
        Assert.Equal(25, assets.Count);
        Assert.Equal(105, schedules.Count);
        Assert.Equal(77, inspections.Count);
        Assert.Equal(42, forms.Count);
        Assert.Equal(29, acknowledgements.Count);
        Assert.Equal(25, assets.Select(asset => asset.AssetCode).Distinct().Count());
        Assert.Equal(25, assets.Select(asset => asset.QrCodeValue).Distinct().Count());
        Assert.Equal(105, schedules.Select(schedule => (schedule.AssetId, schedule.PmCycle)).Distinct().Count());
        Assert.Equal(forms.Count(form => form.FileNumber != null), forms.Where(form => form.FileNumber != null)
            .Select(form => form.FileNumber).Distinct().Count());

        var buildings = new Dictionary<string, string[]>
        {
            ["CCMS Building"] = ["CCMS"],
            ["University Library Complex"] = ["Library"],
            ["Administration Building"] = ["Office of Student Affairs and Services", "Registrar's Office"],
            ["CNAHS Building"] = ["CNAHS"],
            ["University Gymnasium"] = ["General Services Department"]
        };
        var labs = new[] { "CL-01", "CL-03", "CL-04" };
        foreach (var asset in assets)
        {
            Assert.NotNull(asset.Building);
            Assert.True(buildings.ContainsKey(asset.Building));
            Assert.Contains(asset.Department, buildings[asset.Building]);
            Assert.Matches(@"^(FE|FA|EL|WDS)-(CCMS|LIB|OSAS|REG|CNAHS|GYM)-\d{3}$", asset.AssetCode);
            Assert.StartsWith("UNIPM-", asset.QrCodeValue);
            Assert.Null(asset.VerificationLatitude);
            Assert.Null(asset.VerificationLongitude);
            if (asset.Building == "CCMS Building") Assert.Contains(asset.Location, labs);
        }
        foreach (var inspection in inspections)
        {
            var asset = assets.Single(asset => asset.Id == inspection.AssetId);
            var form = forms.Single(form => form.Id == inspection.PreventiveMaintenanceFormId);
            var schedule = schedules.Single(schedule => schedule.Id == inspection.ScheduleId);
            Assert.Equal(asset.Id, schedule.AssetId);
            Assert.Equal(asset.AssetCategory, form.AssetCategory);
            Assert.Equal(asset.Department, form.Department);
            Assert.Equal(asset.Building, form.Building);
            Assert.Equal(schedule.PmCycle, form.PmCycle);
            Assert.False(string.IsNullOrWhiteSpace(inspection.Remarks));
            if (!inspection.IsOperational) Assert.False(string.IsNullOrWhiteSpace(inspection.ActionsRecommendations));
        }
        foreach (var acknowledgement in acknowledgements)
        {
            Assert.Equal("Acknowledged", forms.Single(form => form.Id == acknowledgement.FormId).Status);
            Assert.Contains(acknowledgement.SignatoryName, new[] { "Dr. Elena Mercado", "Adrian M. Reyes", "Marina L. Santos" });
        }
        var labels = assets.SelectMany(asset => new[] { asset.AssetCode, asset.Building, asset.Department, asset.Location, asset.QrCodeValue })
            .Concat(forms.Select(form => form.FileNumber))
            .Concat(inspections.SelectMany(inspection => new[] { inspection.Remarks, inspection.ActionsRecommendations }))
            .Concat(acknowledgements.SelectMany(ack => new[] { ack.SignatoryName, ack.SignatoryPosition }));
        foreach (var label in labels.Where(label => label != null))
            Assert.DoesNotMatch(@"(?i)\b(demo|fictional|sample|floor|learning resource center)\b", label!);

        // Captured from a23eeaf's fixed GUIDs and original hash-key algorithm before label changes.
        var inventory = assets.Select(asset => $"asset:{asset.Id}")
            .Concat(schedules.Select(schedule => $"schedule:{schedule.Id}"))
            .Concat(forms.Select(form => $"form:{form.Id}"))
            .Concat(inspections.Select(inspection => $"inspection:{inspection.Id}"))
            .Concat(acknowledgements.Select(ack => $"ack:{ack.Id}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", inventory.Order(StringComparer.Ordinal)))));
        Assert.Equal("BD47FD3BD2A79ADEB5C78B5B8E6082D3711727C42AEDEE0D67C0B2D5B3EB2722", fingerprint);

        // Only synthetic inventory labels, no users, credentials, signatures or password hashes.
        return JsonSerializer.Serialize(assets.Select(asset => new
        {
            asset.AssetCode, asset.AssetCategory, asset.Building, asset.Department, asset.Location
        }));
    }
}
