using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PreventiveMaintenanceFormRegistryQueryEndpointsTests
{
    [Fact]
    public async Task List_forms_combines_status_category_department_cycle_and_metadata_search()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var target = new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            FileNumber = "PM-FILTER-001",
            AssetCategory = "fire-extinguisher",
            Building = "Science",
            Department = "CCMS",
            PmCycle = "2026-02",
            PeriodType = "Quarter",
            Quarter = "Q1",
            Year = 2026,
            Status = PreventiveMaintenanceFormStatusCatalog.Draft,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var other = new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            FileNumber = "PM-FILTER-002",
            AssetCategory = "emergency-light",
            Building = "Science",
            Department = "CCMS",
            PmCycle = "2026-06",
            PeriodType = "Semester",
            Quarter = null,
            Semester = null,
            Year = 2026,
            Status = PreventiveMaintenanceFormStatusCatalog.Draft,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await application.SeedFormsAsync(target, other);

        var response = await client.GetAsync(
            "/api/v1/preventive-maintenance-forms?status=Draft&assetCategory=fire-extinguisher&department=CCMS&pmCycle=2026-02&search=PM-FILTER-001");

        response.EnsureSuccessStatusCode();
        var forms = await response.Content.ReadFromJsonAsync<List<PreventiveMaintenanceFormResponse>>();
        Assert.NotNull(forms);
        Assert.Equal(target.Id, Assert.Single(forms).Id);
    }

    [Fact]
    public async Task List_forms_rejects_invalid_codes_cycles_and_oversized_filters()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var oversized = new string('x', 257);

        var statusResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?status=Returned");
        var categoryResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?assetCategory=hvac");
        var cycleResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?pmCycle=2026-13");
        var searchResponse = await client.GetAsync($"/api/v1/preventive-maintenance-forms?search={oversized}");
        var departmentResponse = await client.GetAsync($"/api/v1/preventive-maintenance-forms?department={oversized}");

        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cycleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, searchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, departmentResponse.StatusCode);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-form-registry-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(AuthRoleCatalog.Gsd);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task SeedFormsAsync(params PreventiveMaintenanceForm[] forms)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.PreventiveMaintenanceForms.AddRange(forms);
            await context.SaveChangesAsync();
        }
    }
}
