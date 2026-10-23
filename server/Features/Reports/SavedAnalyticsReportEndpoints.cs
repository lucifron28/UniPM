using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Reports;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavedAnalyticsReportRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] PmAnalyticsV3ReportPlan? Plan,
    string PeriodMode = "Fixed", Guid? Revision = null);

public sealed record SavedAnalyticsReportResponse(
    Guid Id, string Name, PmAnalyticsV3ReportPlan Plan, string PeriodMode,
    Guid Revision, DateTimeOffset UpdatedAtUtc);

internal static class SavedAnalyticsReportEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] PeriodModes = ["Fixed", "ThisMonth", "LastMonth", "ThisYear", "LastYear"];

    internal static void MapSavedAnalyticsReports(this RouteGroupBuilder analytics)
    {
        // Inherits the authenticated GSD policy from the existing analytics group.
        var group = analytics.MapGroup("/pm/v3/saved-reports")
            .Produces<ProblemDetails>(401).Produces<ProblemDetails>(403);

        group.MapGet("", async (HttpContext http, IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var owner)) return Results.Unauthorized();
            await using var db = await factory.CreateDbContextAsync(ct);
            var rows = await db.Set<SavedAnalyticsReport>().AsNoTracking()
                .Where(row => row.OwnerUserId == owner).OrderByDescending(row => row.UpdatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(rows.Select(ToResponse).ToArray());
        }).WithName("ListSavedAnalyticsReports")
            .Produces<SavedAnalyticsReportResponse[]>();

        group.MapPost("", async (SavedAnalyticsReportRequest request, HttpContext http,
            IDbContextFactory<ApplicationDbContext> factory, PmAnalyticsV3QueryService service,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!TryOwner(http, out var owner)) return Results.Unauthorized();
            var resolution = await Validate(request, service, clock, ct);
            if (resolution.Plan is null) return ApiErrors.Validation(resolution.Errors);
            await using var db = await factory.CreateDbContextAsync(ct);
            if (await db.Set<SavedAnalyticsReport>().CountAsync(row => row.OwnerUserId == owner, ct) >= 100)
                return Results.Problem(statusCode: 409, title: "You can save up to 100 reports. Delete one before saving another.");
            var row = new SavedAnalyticsReport { Id = Guid.NewGuid(), OwnerUserId = owner };
            Apply(row, request, resolution.Plan.Plan, clock);
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/analytics/pm/v3/saved-reports/{row.Id}", ToResponse(row));
        }).WithName("CreateSavedAnalyticsReport")
            .Produces<SavedAnalyticsReportResponse>(201).Produces<ValidationProblemDetails>(400)
            .Produces<ProblemDetails>(409);

        group.MapPut("/{id:guid}", async (Guid id, SavedAnalyticsReportRequest request, HttpContext http,
            IDbContextFactory<ApplicationDbContext> factory, PmAnalyticsV3QueryService service,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!TryOwner(http, out var owner)) return Results.Unauthorized();
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.Set<SavedAnalyticsReport>().SingleOrDefaultAsync(
                row => row.Id == id && row.OwnerUserId == owner, ct);
            if (row is null) return Results.NotFound();
            if (request.Revision != row.Revision) return Changed();
            var resolution = await Validate(request, service, clock, ct);
            if (resolution.Plan is null) return ApiErrors.Validation(resolution.Errors);
            Apply(row, request, resolution.Plan.Plan, clock);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Changed(); }
            return Results.Ok(ToResponse(row));
        }).WithName("UpdateSavedAnalyticsReport")
            .Produces<SavedAnalyticsReportResponse>().Produces<ValidationProblemDetails>(400)
            .Produces(404).Produces<ProblemDetails>(409);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http,
            IDbContextFactory<ApplicationDbContext> factory, CancellationToken ct) =>
        {
            if (!TryOwner(http, out var owner)) return Results.Unauthorized();
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.Set<SavedAnalyticsReport>().SingleOrDefaultAsync(
                row => row.Id == id && row.OwnerUserId == owner, ct);
            if (row is null) return Results.NotFound();
            db.Remove(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Changed(); }
            return Results.NoContent();
        }).WithName("DeleteSavedAnalyticsReport").Produces(204).Produces(404).Produces<ProblemDetails>(409);

        group.MapPost("/{id:guid}/preview", async (Guid id, HttpContext http,
            IDbContextFactory<ApplicationDbContext> factory, PmAnalyticsV3QueryService service,
            PmAnalyticsV3PreviewStore previews, TimeProvider clock, CancellationToken ct) =>
        {
            if (!TryOwner(http, out var owner)) return Results.Unauthorized();
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.Set<SavedAnalyticsReport>().AsNoTracking().SingleOrDefaultAsync(
                row => row.Id == id && row.OwnerUserId == owner, ct);
            if (row is null) return Results.NotFound();
            var saved = ToResponse(row);
            var resolution = await Validate(new(saved.Name, saved.Plan, saved.PeriodMode), service, clock, ct);
            if (resolution.Plan is null) return ApiErrors.Validation(resolution.Errors);
            var userId = UserId(http)!;
            if (!previews.TryCreate(userId, resolution.Plan, out var previewId, out var expiresAt))
                return Results.Problem(statusCode: 503, title: "A report preview could not be created. Try again.");
            return Results.Ok(new PmAnalyticsV3PreviewResponse(previewId, expiresAt, resolution.Plan.Plan,
                resolution.Plan.CyclesByCategory, PmAnalyticsV3QueryService.CreatePreviewSummary(resolution.Plan),
                PmAnalyticsV3QueryService.ScopeNotesFor(resolution.Plan.Plan.Subject)));
        }).WithName("PreviewSavedAnalyticsReport").Produces<PmAnalyticsV3PreviewResponse>()
            .Produces<ValidationProblemDetails>(400).Produces(404).Produces<ProblemDetails>(503);
    }

    private static async Task<PmAnalyticsV3PlanResolution> Validate(SavedAnalyticsReportRequest request,
        PmAnalyticsV3QueryService service, TimeProvider clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 || request.Name.Any(char.IsControl))
            errors["name"] = ["Enter a report name of 1 to 120 characters without control characters."];
        if (!PeriodModes.Contains(request.PeriodMode))
            errors["periodMode"] = ["Select a supported saved period mode."];
        if (request.PeriodMode != "Fixed" && request.Plan?.Subject != PmAnalyticsV3Catalog.SubjectPmExecution)
            errors["periodMode"] = ["Relative periods are available only for PM execution reports."];
        if (errors.Count > 0) return new(null, errors, []);
        var plan = request.Plan;
        if (plan is not null && request.PeriodMode != "Fixed")
        {
            // Same UTC+08:00 calendar used by deterministic PM cycle analytics.
            var local = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8));
            var date = request.PeriodMode == "LastMonth" ? local.AddMonths(-1) : local;
            var month = request.PeriodMode is "ThisMonth" or "LastMonth";
            plan = plan with { Period = new("pmCycle", month ? PmAnalyticsV3PeriodKind.Month : PmAnalyticsV3PeriodKind.Year,
                request.PeriodMode == "LastYear" ? date.Year - 1 : date.Year, month ? date.Month : null) };
        }
        return await service.NormalizeAndResolveAsync(plan, ct);
    }

    private static void Apply(SavedAnalyticsReport row, SavedAnalyticsReportRequest request,
        PmAnalyticsV3ReportPlan normalized, TimeProvider clock)
    {
        row.Name = request.Name.Trim();
        row.PeriodMode = request.PeriodMode;
        row.PlanJson = JsonSerializer.Serialize(normalized, JsonOptions);
        row.Revision = Guid.NewGuid();
        row.UpdatedAtUtc = clock.GetUtcNow();
    }

    private static SavedAnalyticsReportResponse ToResponse(SavedAnalyticsReport row) => new(
        row.Id, row.Name, JsonSerializer.Deserialize<PmAnalyticsV3ReportPlan>(row.PlanJson, JsonOptions)!,
        row.PeriodMode, row.Revision, row.UpdatedAtUtc);

    private static string? UserId(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
    private static bool TryOwner(HttpContext http, out Guid owner) => Guid.TryParse(UserId(http), out owner);
    private static IResult Changed() => Results.Problem(statusCode: 409,
        title: "This saved report changed. Reload My reports before editing it again.");
}
