using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Assets;

public static class AssetsEndpoints
{
    public static IEndpointRouteBuilder MapAssetsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/assets").WithTags("Assets");

        group.MapPost("/", async (CreateAssetDto dto, IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var context = await factory.CreateDbContextAsync();
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = dto.AssetCode,
                AssetCategory = dto.AssetCategory,
                Building = dto.Building,
                Department = dto.Department,
                Location = dto.Location,
                Status = "Active",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            asset.QrCodeValue = $"UNIPM-{asset.AssetCategory.ToUpper().Replace(" ", "")}-{asset.Id.ToString().Substring(0, 8)}";

            context.Assets.Add(asset);
            await context.SaveChangesAsync();

            return Results.Created($"/api/v1/assets/{asset.Id}", asset);
        });

        group.MapGet("/{id}", async (Guid id, IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var context = await factory.CreateDbContextAsync();
            var asset = await context.Assets.FindAsync(id);
            return asset is not null ? Results.Ok(asset) : Results.NotFound();
        });

        return endpoints;
    }
}

public class CreateAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
}
