using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAtUtc);

internal sealed class JwtTokenService(JwtRuntimeConfiguration configuration)
{
    public IssuedAccessToken Issue(ApplicationUser user, IReadOnlyList<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(configuration.AccessTokenLifetime);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(AuthClaimNames.DisplayName, user.DisplayName)
        };
        claims.AddRange(roles
            .OrderBy(role => role, StringComparer.Ordinal)
            .Select(role => new Claim(AuthClaimNames.Role, role)));

        var credentials = new SigningCredentials(
            configuration.SigningKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            configuration.Issuer,
            configuration.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires);
    }
}
