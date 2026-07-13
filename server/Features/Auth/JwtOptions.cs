namespace UniPM.Api.Features.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int DefaultAccessTokenMinutes = 60;
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = DefaultAccessTokenMinutes;
}
