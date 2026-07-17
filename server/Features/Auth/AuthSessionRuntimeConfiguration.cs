namespace UniPM.Api.Features.Auth;

internal sealed class AuthSessionRuntimeConfiguration
{
    private AuthSessionRuntimeConfiguration(TimeSpan refreshTokenLifetime, string webOrigin)
    {
        RefreshTokenLifetime = refreshTokenLifetime;
        WebOrigin = webOrigin;
    }

    public TimeSpan RefreshTokenLifetime { get; }
    public string WebOrigin { get; }

    public static AuthSessionRuntimeConfiguration Create(AuthSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RefreshTokenDays is < 1 or > 30)
        {
            throw new InvalidOperationException("AuthSession refresh-token lifetime must be between 1 and 30 days.");
        }

        return new AuthSessionRuntimeConfiguration(
            TimeSpan.FromDays(options.RefreshTokenDays),
            TrustedWebOriginValidator.Normalize(options.WebOrigin));
    }
}
