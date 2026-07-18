using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal sealed class RefreshCookieService(IWebHostEnvironment environment)
{
    public const string CookieName = "unipm_refresh";
    private const string Path = "/api/v1/auth";

    public void Write(HttpResponse response, string token, DateTimeOffset expiresAtUtc)
        => response.Cookies.Append(CookieName, token, CreateOptions(expiresAtUtc));

    public void Clear(HttpResponse response)
        => response.Cookies.Delete(CookieName, CreateOptions(DateTimeOffset.UnixEpoch));

    private CookieOptions CreateOptions(DateTimeOffset expiresAtUtc) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = !environment.IsDevelopment(),
        IsEssential = true,
        Path = Path,
        Expires = expiresAtUtc,
        MaxAge = expiresAtUtc > DateTimeOffset.UtcNow ? expiresAtUtc - DateTimeOffset.UtcNow : TimeSpan.Zero
    };
}
