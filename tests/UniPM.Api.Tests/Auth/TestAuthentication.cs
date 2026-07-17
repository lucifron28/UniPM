using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Tests;

internal sealed record TestAuthenticationRoles(IReadOnlyList<string> Values);

internal static class TestAuthenticationExtensions
{
    public static AuthenticationBuilder AddTestAuthentication(
        this IServiceCollection services,
        params string[] roles)
    {
        services.AddSingleton(new TestAuthenticationRoles(roles));
        return services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TestAuthenticationRoles roles)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UniPM-Test";
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new("sub", UserId.ToString()),
            new("display_name", "Test User")
        };
        claims.AddRange(roles.Values.Select(role => new Claim("role", role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "display_name", "role"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}
