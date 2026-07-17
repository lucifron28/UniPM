namespace UniPM.Api.Features.Auth;

public sealed class AuthSessionOptions
{
    public const string SectionName = "AuthSession";
    public const int DefaultRefreshTokenDays = 7;

    public int RefreshTokenDays { get; set; } = DefaultRefreshTokenDays;
    public string WebOrigin { get; set; } = "http://localhost:5173";
}
