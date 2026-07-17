using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class AuthEndpointsTests
{
    private const string Email = "gsd-test@unipm.local";
    private const string Password = "TestOnlyPassword123!";
    private const string Issuer = "UniPM.Tests";
    private const string Audience = "UniPM.Tests.Clients";
    private const string SigningKey = "UniPM-Test-Only-Signing-Key-At-Least-32-Bytes!";

    [Fact]
    public async Task Valid_login_returns_expected_claims_expiration_and_safe_user_contract()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await LoginAsync(client, Email, Password);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
        Assert.NotNull(payload);
        Assert.Equal(Email, payload.User.Email);
        Assert.Equal("GSD Test User", payload.User.DisplayName);
        Assert.Equal([AuthRoleCatalog.Gsd], payload.User.Roles);
        Assert.InRange(
            payload.ExpiresAtUtc,
            DateTimeOffset.UtcNow.AddMinutes(14),
            DateTimeOffset.UtcNow.AddMinutes(16));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        Assert.Equal(Issuer, token.Issuer);
        Assert.Contains(Audience, token.Audiences);
        Assert.NotNull(token.Claims.SingleOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub));
        Assert.NotNull(token.Claims.SingleOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti));
        Assert.Equal(Email, token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("GSD Test User", token.Claims.Single(claim => claim.Type == "display_name").Value);
        Assert.Equal(AuthRoleCatalog.Gsd, token.Claims.Single(claim => claim.Type == "role").Value);
        Assert.DoesNotContain("passwordHash", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("wrong-password@unipm.local", "WrongPassword123!")]
    [InlineData("unknown@unipm.local", "WrongPassword123!")]
    public async Task Invalid_credentials_return_the_same_generic_unauthorized_contract(
        string email,
        string password)
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(
            "wrong-password@unipm.local",
            Password,
            true,
            false,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await LoginAsync(client, email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Authentication failed", problem.Title);
        Assert.Equal("Invalid email or password.", problem.Detail);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Inactive_or_locked_accounts_receive_the_generic_unauthorized_contract(
        bool isActive,
        bool isLockedOut)
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(
            Email,
            Password,
            isActive,
            isLockedOut,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await LoginAsync(client, Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem?.Detail);
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_current_user_and_roles()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(
            Email,
            Password,
            true,
            false,
            AuthRoleCatalog.Gsd,
            AuthRoleCatalog.Supervisor);
        using var client = application.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var login = await (await LoginAsync(client, Email, Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(user);
        Assert.Equal(Email, user.Email);
        Assert.Equal([AuthRoleCatalog.Gsd, AuthRoleCatalog.Supervisor], user.Roles);
    }

    [Fact]
    public async Task Jwt_role_claim_authorizes_the_matching_operational_policy()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, Email, Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = "JWT-GSD-001",
            assetCategory = "fire-extinguisher",
            building = "Test Building",
            department = "GSD",
            location = "Test Room"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(InvalidTokenKind.Issuer)]
    [InlineData(InvalidTokenKind.Audience)]
    [InlineData(InvalidTokenKind.Signature)]
    [InlineData(InvalidTokenKind.Expired)]
    public async Task Invalid_tokens_are_rejected(InvalidTokenKind kind)
    {
        await using var application = new AuthApplicationFactory();
        var userId = await application.SeedUserAsync(
            Email,
            Password,
            true,
            false,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var token = CreateToken(userId, kind);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_is_rejected_after_user_is_deactivated()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, Email, Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        await application.SetActiveAsync(Email, false);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_sets_an_opaque_http_only_refresh_cookie_and_persists_only_its_hash()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await LoginAsync(client, Email, Password);

        response.EnsureSuccessStatusCode();
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("no-cache", response.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);
        var token = ExtractRefreshCookie(response);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var session = Assert.Single(await application.GetRefreshSessionsAsync());
        Assert.NotEqual(token, session.TokenHash);
        Assert.Equal(64, session.TokenHash.Length);
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_replay_revokes_only_its_family()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var firstLogin = await LoginAsync(client, Email, Password);
        var firstToken = ExtractRefreshCookie(firstLogin);
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={firstToken}");

        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null);

        refresh.EnsureSuccessStatusCode();
        var replacementToken = ExtractRefreshCookie(refresh);
        Assert.NotEqual(firstToken, replacementToken);
        var sessions = await application.GetRefreshSessionsAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, session => session.RevokedAtUtc is null);
        Assert.Equal(sessions[0].ExpiresAtUtc, sessions[1].ExpiresAtUtc);

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={firstToken}");
        var replay = await client.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("The session could not be refreshed.", (await replay.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
        Assert.All(await application.GetRefreshSessionsAsync(), session => Assert.NotNull(session.RevokedAtUtc));
    }

    [Fact]
    public async Task Logout_revokes_its_family_and_is_idempotent()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(client, Email, Password);
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={ExtractRefreshCookie(login)}");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.All(await application.GetRefreshSessionsAsync(), session => Assert.NotNull(session.RevokedAtUtc));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Refresh_rejections_are_generic_and_clear_the_cookie(string? token)
    {
        await using var application = new AuthApplicationFactory();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");
        }

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("The session could not be refreshed.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
        Assert.Contains("expires=", response.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Security_stamp_change_rejects_refresh_with_the_generic_contract()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(client, Email, Password);
        await application.UpdateSecurityStampAsync(Email);
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={ExtractRefreshCookie(login)}");

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("The session could not be refreshed.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
    }

    [Fact]
    public async Task Logout_of_one_login_family_leaves_a_second_login_family_refreshable()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var first = ExtractRefreshCookie(await LoginAsync(client, Email, Password));
        var second = ExtractRefreshCookie(await LoginAsync(client, Email, Password));
        Assert.Equal(2, (await application.GetRefreshSessionsAsync()).Select(item => item.TokenFamilyId).Distinct().Count());

        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={first}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={second}");

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
    }

    [Theory]
    [InlineData("http://*")]
    [InlineData("https://*.example.edu")]
    public void Wildcard_web_origins_are_rejected_at_startup_validation(string origin)
    {
        Assert.Throws<InvalidOperationException>(() => TrustedWebOriginValidator.Normalize(origin));
    }

    [Fact]
    public async Task Browser_origin_is_exact_and_credentialed()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var preflight = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal("http://localhost:5173", preflight.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("true", preflight.Headers.GetValues("Access-Control-Allow-Credentials").Single(), StringComparison.OrdinalIgnoreCase);

        using var untrusted = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = Email, password = Password })
        };
        untrusted.Headers.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(untrusted)).StatusCode);

        using var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        malformed.Headers.Add("Origin", "not-an-origin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(malformed)).StatusCode);
    }

    [Fact]
    public async Task Inspector_can_submit_an_inspection_for_their_authenticated_identity()
    {
        await using var application = new AuthApplicationFactory();
        var inspectorId = await application.SeedUserAsync(
            "inspector@unipm.local",
            Password,
            true,
            false,
            AuthRoleCatalog.Inspector);
        var scheduleId = await application.SeedScheduleAsync();
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, "inspector@unipm.local", Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", InspectionRequest(
            scheduleId,
            inspectorId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inspection = await response.Content.ReadFromJsonAsync<InspectionResponse>();
        Assert.NotNull(inspection);
        Assert.Equal(inspectorId, inspection.InspectorUserId);
    }

    [Fact]
    public async Task Inspector_cannot_submit_an_inspection_for_another_user()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(
            "inspector@unipm.local",
            Password,
            true,
            false,
            AuthRoleCatalog.Inspector);
        var scheduleId = await application.SeedScheduleAsync();
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, "inspector@unipm.local", Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", InspectionRequest(
            scheduleId,
            Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gsd_submission_rejects_nonexistent_or_inactive_inspector_users(bool seedInactiveUser)
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        var inspectorUserId = seedInactiveUser
            ? await application.SeedUserAsync(
                "inactive-inspector@unipm.local",
                Password,
                false,
                false,
                AuthRoleCatalog.Inspector)
            : Guid.NewGuid();
        var scheduleId = await application.SeedScheduleAsync();
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, Email, Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", InspectionRequest(
            scheduleId,
            inspectorUserId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(RecordInspectionDto.InspectorUserId), problem.Errors.Keys);
    }

    [Fact]
    public async Task Gsd_can_provisionally_submit_an_inspection_on_behalf_of_an_active_user()
    {
        await using var application = new AuthApplicationFactory();
        await application.SeedUserAsync(Email, Password, true, false, AuthRoleCatalog.Gsd);
        var inspectorUserId = await application.SeedUserAsync(
            "field-personnel@unipm.local",
            Password,
            true,
            false,
            AuthRoleCatalog.Inspector);
        var scheduleId = await application.SeedScheduleAsync();
        using var client = application.CreateClient();
        var login = await (await LoginAsync(client, Email, Password))
            .Content.ReadFromJsonAsync<LoginPayload>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", InspectionRequest(
            scheduleId,
            inspectorUserId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inspection = await response.Content.ReadFromJsonAsync<InspectionResponse>();
        Assert.NotNull(inspection);
        Assert.Equal(inspectorUserId, inspection.InspectorUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short-key")]
    public void Production_http_startup_rejects_missing_or_weak_signing_configuration(string? key)
    {
        using var application = new ProductionConfigurationFactory(key);

        var exception = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("JWT configuration is required", exception.ToString(), StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
        => client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{RefreshCookieService.CookieName}=", StringComparison.Ordinal));
        return header.Split(';', 2)[0].Split('=', 2)[1];
    }

    private static object InspectionRequest(Guid scheduleId, Guid inspectorUserId) => new
    {
        scheduleId,
        inspectorUserId,
        dateInspected = DateTimeOffset.UtcNow,
        isOperational = true,
        remarks = "Operational test inspection"
    };

    private static string CreateToken(Guid userId, InvalidTokenKind kind)
    {
        var now = DateTimeOffset.UtcNow;
        var key = kind == InvalidTokenKind.Signature
            ? "Different-Test-Only-Signing-Key-At-Least-32-Bytes!"
            : SigningKey;
        var token = new JwtSecurityToken(
            kind == InvalidTokenKind.Issuer ? "WrongIssuer" : Issuer,
            kind == InvalidTokenKind.Audience ? "WrongAudience" : Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(JwtRegisteredClaimNames.Email, Email),
                new Claim("display_name", "GSD Test User"),
                new Claim("role", AuthRoleCatalog.Gsd)
            ],
            notBefore: kind == InvalidTokenKind.Expired
                ? now.AddMinutes(-10).UtcDateTime
                : now.UtcDateTime,
            expires: kind == InvalidTokenKind.Expired
                ? now.AddMinutes(-5).UtcDateTime
                : now.AddMinutes(15).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal sealed class AuthApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-auth-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["AuthSession:RefreshTokenDays"] = "7",
                    ["AuthSession:WebOrigin"] = "http://localhost:5173",
                    ["UNIPM_DEV_USER_PASSWORD"] = Password
                }));
            builder.ConfigureServices(services =>
            {
                var runtime = JwtRuntimeConfiguration.Create(new JwtOptions
                {
                    Issuer = Issuer,
                    Audience = Audience,
                    SigningKey = SigningKey,
                    AccessTokenMinutes = 15
                }, isDevelopment: false);
                services.RemoveAll<JwtRuntimeConfiguration>();
                services.AddSingleton(runtime);
                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options => options.TokenValidationParameters = runtime.CreateValidationParameters());
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task<Guid> SeedUserAsync(
            string email,
            string password,
            bool isActive,
            bool isLockedOut,
            params string[] roles)
        {
            await using var scope = Services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
                }
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "GSD Test User",
                IsActive = isActive,
                LockoutEnabled = true,
                LockoutEnd = isLockedOut ? DateTimeOffset.UtcNow.AddMinutes(15) : null
            };
            Assert.True((await userManager.CreateAsync(user, password)).Succeeded);
            if (roles.Length > 0)
            {
                Assert.True((await userManager.AddToRolesAsync(user, roles)).Succeeded);
            }

            return user.Id;
        }

        public async Task SetActiveAsync(string email, bool isActive)
        {
            await using var scope = Services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            user.IsActive = isActive;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        public async Task<IReadOnlyList<RefreshSession>> GetRefreshSessionsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.RefreshSessions.OrderBy(session => session.CreatedAtUtc).ToListAsync();
        }

        public async Task UpdateSecurityStampAsync(string email)
        {
            await using var scope = Services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            Assert.True((await userManager.UpdateSecurityStampAsync(user)).Succeeded);
        }

        public async Task<Guid> SeedScheduleAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"AUTH-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                AssetCategory = "fire-extinguisher",
                Building = "Test Building",
                Department = "GSD",
                Location = "Test Room",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = now,
                PeriodType = "Quarter",
                Status = "Due",
                Quarter = "Q1",
                Year = 2026,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();
            return schedule.Id;
        }
    }

    private sealed class ProductionConfigurationFactory(string? signingKey)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = signingKey,
                    ["Jwt:AccessTokenMinutes"] = "60"
                }));
        }
    }

    public enum InvalidTokenKind
    {
        Issuer,
        Audience,
        Signature,
        Expired
    }

    private sealed record LoginPayload(
        string AccessToken,
        DateTimeOffset ExpiresAtUtc,
        UserPayload User);

    private sealed record UserPayload(
        Guid Id,
        string Email,
        string DisplayName,
        IReadOnlyList<string> Roles);
}
