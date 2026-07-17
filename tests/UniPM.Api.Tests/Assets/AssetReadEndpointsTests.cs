using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Tests;

public sealed class AssetReadEndpointsTests
{
    [Fact]
    public async Task List_assets_returns_assets_matching_filters()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        await CreateAssetAsync(client, " fe-001 ", " FIRE-EXTINGUISHER ", "Main", "CCMS", "Lobby");
        await CreateAssetAsync(client, "EL-001", "emergency-light", "Main", "CCMS", "Stairs");

        var response = await client.GetAsync(
            "/api/v1/assets?assetCategory=fire-extinguisher&building=Main&department=CCMS");

        response.EnsureSuccessStatusCode();
        var assets = await response.Content.ReadFromJsonAsync<List<AssetResponse>>();

        Assert.NotNull(assets);
        var asset = Assert.Single(assets);
        Assert.Equal("FE-001", asset.AssetCode);
        Assert.Equal("fire-extinguisher", asset.AssetCategory);
        Assert.Equal("Main", asset.Building);
        Assert.Equal("CCMS", asset.Department);
    }

    [Fact]
    public async Task List_assets_rejects_unsupported_code_filters()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var categoryResponse = await client.GetAsync("/api/v1/assets?assetCategory=hvac");
        var statusResponse = await client.GetAsync("/api/v1/assets?status=Paused");

        Assert.Equal(HttpStatusCode.BadRequest, categoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
    }

    [Fact]
    public async Task Create_asset_returns_conflict_for_a_duplicate_canonical_code()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        await CreateAssetAsync(client, "FE-101", "fire-extinguisher", "Main", "GSD", "Lobby");
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = " fe-101 ",
            assetCategory = "fire-extinguisher"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_asset_by_id_returns_asset_response()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var created = await CreateAssetAsync(client, "FA-001", "fire-alarm", "Annex", "GSD", "Hallway");

        var response = await client.GetAsync($"/api/v1/assets/{created.Id}");

        response.EnsureSuccessStatusCode();
        var asset = await response.Content.ReadFromJsonAsync<AssetResponse>();

        Assert.NotNull(asset);
        Assert.Equal(created.Id, asset.Id);
        Assert.Equal("FA-001", asset.AssetCode);
        Assert.Equal("Annex", asset.Building);
    }

    [Fact]
    public async Task Get_asset_by_qr_returns_asset_response()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var created = await CreateAssetAsync(
            client,
            "WDS-001",
            "water-drinking-station",
            "Main",
            "GSD",
            "Cafeteria");

        Assert.Equal(
            $"UNIPM-WATER-DRINKING-STATION-{created.Id.ToString()[..8].ToUpperInvariant()}",
            created.QrCodeValue);

        var response = await client.GetAsync($"/api/v1/assets/by-qr/{created.QrCodeValue}");

        response.EnsureSuccessStatusCode();
        var asset = await response.Content.ReadFromJsonAsync<AssetResponse>();

        Assert.NotNull(asset);
        Assert.Equal(created.Id, asset.Id);
        Assert.Equal(created.QrCodeValue, asset.QrCodeValue);
    }

    [Fact]
    public async Task Get_asset_by_qr_returns_not_found_for_unknown_qr()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/assets/by-qr/UNKNOWN-QR");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<AssetResponse> CreateAssetAsync(
        HttpClient client,
        string assetCode,
        string assetCategory,
        string building,
        string department,
        string location)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory,
            building,
            department,
            location
        });

        response.EnsureSuccessStatusCode();

        var asset = await response.Content.ReadFromJsonAsync<AssetResponse>();
        Assert.NotNull(asset);
        return asset;
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-assets-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(AuthRoleCatalog.Gsd);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private sealed record AssetResponse(
        Guid Id,
        string AssetCode,
        string AssetCategory,
        string? Building,
        string? Department,
        string? Location,
        string? QrCodeValue,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
