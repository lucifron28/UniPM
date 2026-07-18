using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerRefreshSessionTests
{
    private const string PreviousMigration = "20260717044115_EnforceOneInspectionPerSchedule";
    private const string Email = "sql-refresh@unipm.local";
    private const string Password = "SqlRefreshPassword123!";

    [SqlServerFact]
    public async Task Refresh_session_migration_applies_forward_and_enforces_hash_and_rowversion_contracts()
    {
        await using var database = await SqlServerRefreshDatabase.CreateAsync(RequireSqlServerConnection());
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
            await context.Database.MigrateAsync();
            var user = NewUser();
            context.Users.Add(user);
            context.RefreshSessions.Add(NewSession(user.Id, "A".PadLeft(64, 'A')));
            await context.SaveChangesAsync();
        }

        await using (var duplicateContext = database.CreateContext())
        {
            var userId = await duplicateContext.Users.Select(user => user.Id).SingleAsync();
            duplicateContext.RefreshSessions.Add(NewSession(userId, "A".PadLeft(64, 'A')));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstSession = await first.RefreshSessions.SingleAsync();
        var secondSession = await second.RefreshSessions.SingleAsync();
        firstSession.RevocationReason = "First";
        await first.SaveChangesAsync();
        secondSession.RevocationReason = "Second";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_refresh_replay_and_logout_leave_no_active_refresh_token_and_no_server_error()
    {
        await using var database = await SqlServerRefreshDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlRefreshApplicationFactory(database.ConnectionString);
        await application.SeedUserAsync();
        using var loginClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = ExtractRefreshCookie(login);

        using var refreshClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var logoutClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        refreshClient.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");
        logoutClient.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");
        var results = await Task.WhenAll(
            refreshClient.PostAsync("/api/v1/auth/refresh", null),
            logoutClient.PostAsync("/api/v1/auth/logout", null));

        Assert.All(results, response => Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode));
        Assert.True(results[0].StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
        Assert.Equal(HttpStatusCode.NoContent, results[1].StatusCode);
        Assert.Equal(0, await application.ActiveSessionCountAsync());
    }

    [SqlServerFact]
    public async Task Simultaneous_refresh_requests_never_leave_two_active_replacements()
    {
        await using var database = await SqlServerRefreshDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlRefreshApplicationFactory(database.ConnectionString);
        await application.SeedUserAsync();
        using var loginClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        var token = ExtractRefreshCookie(login);
        using var first = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var second = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        first.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");
        second.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");

        var responses = await Task.WhenAll(
            first.PostAsync("/api/v1/auth/refresh", null),
            second.PostAsync("/api/v1/auth/refresh", null));

        Assert.All(responses, response => Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode));
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized }));
        Assert.InRange(await application.ActiveSessionCountAsync(), 0, 1);
    }

    [SqlServerFact]
    public async Task Failed_rotation_releases_its_transaction_before_revoking_the_family()
    {
        await using var database = await SqlServerRefreshDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlRefreshApplicationFactory(database.ConnectionString, failFirstRotation: true);
        await application.SeedUserAsync();
        using var loginClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        var token = ExtractRefreshCookie(login);
        using var refreshClient = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        refreshClient.DefaultRequestHeaders.Add("Cookie", $"{RefreshCookieService.CookieName}={token}");

        var response = await refreshClient.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await application.ActiveSessionCountAsync());
    }

    private static string RequireSqlServerConnection()
        => Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")!;

    private static ApplicationUser NewUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = $"sql-{Guid.NewGuid():N}@unipm.local",
        NormalizedUserName = $"SQL-{Guid.NewGuid():N}@UNIPM.LOCAL",
        Email = $"sql-{Guid.NewGuid():N}@unipm.local",
        NormalizedEmail = $"SQL-{Guid.NewGuid():N}@UNIPM.LOCAL",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        DisplayName = "SQL Refresh User",
        IsActive = true
    };

    private static RefreshSession NewSession(Guid userId, string hash) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = hash,
        TokenFamilyId = Guid.NewGuid(),
        SecurityStampHash = "B".PadLeft(64, 'B'),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
    };

    private static string ExtractRefreshCookie(HttpResponseMessage response)
        => response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith($"{RefreshCookieService.CookieName}=", StringComparison.Ordinal))
            .Split(';', 2)[0].Split('=', 2)[1];

    private sealed class SqlRefreshApplicationFactory(string connectionString, bool failFirstRotation = false)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "UniPM.SqlRefresh",
                ["Jwt:Audience"] = "UniPM.SqlRefresh.Clients",
                ["Jwt:SigningKey"] = "UniPM-SqlRefresh-Signing-Key-At-Least-32-Bytes!",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["AuthSession:RefreshTokenDays"] = "7",
                ["AuthSession:WebOrigin"] = "http://localhost:5173"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                {
                    options.UseSqlServer(connectionString);
                    if (failFirstRotation)
                    {
                        options.AddInterceptors(new FailFirstRotationSaveInterceptor());
                    }
                });
            });
        }

        public async Task SeedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = Email, Email = Email, EmailConfirmed = true,
                DisplayName = "SQL Refresh User", IsActive = true, LockoutEnabled = true
            };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);
        }

        public async Task<int> ActiveSessionCountAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.RefreshSessions.CountAsync(session => session.RevokedAtUtc == null);
        }
    }

    private sealed class FailFirstRotationSaveInterceptor : SaveChangesInterceptor
    {
        private int hasFailed;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowForRotation(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowForRotation(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ThrowForRotation(DbContext? context)
        {
            if (context is null
                || !context.ChangeTracker.Entries<RefreshSession>()
                    .Any(entry => entry.Entity.RevocationReason == "Rotated")
                || Interlocked.Exchange(ref hasFailed, 1) != 0)
            {
                return;
            }

            throw new DbUpdateConcurrencyException("Forced refresh rotation failure.");
        }
    }

    private sealed class SqlServerRefreshDatabase : IAsyncDisposable
    {
        private readonly string databaseName;
        private SqlServerRefreshDatabase(string connectionString, string databaseName) { ConnectionString = connectionString; this.databaseName = databaseName; }
        public string ConnectionString { get; }
        public static async Task<SqlServerRefreshDatabase> CreateAsync(string baseConnectionString)
        {
            var databaseName = $"UniPMRefreshSessions_{Guid.NewGuid():N}";
            var database = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
            var master = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
            return new SqlServerRefreshDatabase(database.ConnectionString, databaseName);
        }
        public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);
        public async ValueTask DisposeAsync()
        {
            var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
