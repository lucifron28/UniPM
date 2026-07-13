using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace UniPM.Api.Features.Auth;

internal sealed class JwtRuntimeConfiguration
{
    private JwtRuntimeConfiguration(
        string issuer,
        string audience,
        byte[] signingKey,
        int accessTokenMinutes,
        bool usesEphemeralDevelopmentKey)
    {
        Issuer = issuer;
        Audience = audience;
        SigningKey = new SymmetricSecurityKey(signingKey);
        AccessTokenLifetime = TimeSpan.FromMinutes(accessTokenMinutes);
        UsesEphemeralDevelopmentKey = usesEphemeralDevelopmentKey;
    }

    public string Issuer { get; }
    public string Audience { get; }
    public SymmetricSecurityKey SigningKey { get; }
    public TimeSpan AccessTokenLifetime { get; }
    public bool UsesEphemeralDevelopmentKey { get; }
    public static TimeSpan ClockSkew { get; } = TimeSpan.FromSeconds(30);

    public static JwtRuntimeConfiguration Create(JwtOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        var issuer = options.Issuer.Trim();
        var audience = options.Audience.Trim();
        var keyBytes = Encoding.UTF8.GetBytes(options.SigningKey);
        var minutes = options.AccessTokenMinutes;
        var configured = issuer.Length > 0
            && audience.Length > 0
            && keyBytes.Length >= JwtOptions.MinimumSigningKeyBytes
            && minutes is >= 1 and <= 1440;

        if (configured)
        {
            return new JwtRuntimeConfiguration(issuer, audience, keyBytes, minutes, false);
        }

        if (!isDevelopment)
        {
            throw new InvalidOperationException(
                "JWT configuration is required for HTTP startup outside Development. "
                + "Configure issuer, audience, a signing key of at least 32 bytes, and an access-token lifetime from 1 to 1440 minutes.");
        }

        return new JwtRuntimeConfiguration(
            "UniPM.Development",
            "UniPM.Development.Clients",
            RandomNumberGenerator.GetBytes(JwtOptions.MinimumSigningKeyBytes),
            JwtOptions.DefaultAccessTokenMinutes,
            true);
    }

    public static JwtRuntimeConfiguration CreateForMaintenanceCommand(JwtOptions options)
    {
        try
        {
            return Create(options, isDevelopment: false);
        }
        catch (InvalidOperationException)
        {
            return new JwtRuntimeConfiguration(
                "UniPM.MaintenanceCommand",
                "UniPM.MaintenanceCommand.Clients",
                RandomNumberGenerator.GetBytes(JwtOptions.MinimumSigningKeyBytes),
                JwtOptions.DefaultAccessTokenMinutes,
                true);
        }
    }

    public TokenValidationParameters CreateValidationParameters()
        => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = ClockSkew,
            NameClaimType = AuthClaimNames.DisplayName,
            RoleClaimType = AuthClaimNames.Role
        };
}
