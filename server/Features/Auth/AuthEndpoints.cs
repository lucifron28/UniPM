using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using UniPM.Api.Features;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<LoginResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<AuthUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService tokenService,
        RefreshSessionService refreshSessions,
        RefreshCookieService cookieService,
        TrustedWebOriginValidator originValidator,
        HttpContext httpContext)
    {
        if (!originValidator.IsTrusted(httpContext.Request))
        {
            return ApiErrors.Forbidden("The request origin is not allowed.");
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return ApiErrors.Validation(validationErrors);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return ApiErrors.Unauthorized("Invalid email or password.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return ApiErrors.Unauthorized("Invalid email or password.");
        }

        var roles = (await userManager.GetRolesAsync(user))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        var issued = tokenService.Issue(user, roles);
        var refresh = await refreshSessions.IssueAsync(user, httpContext.RequestAborted);
        cookieService.Write(httpContext.Response, refresh.Token, refresh.ExpiresAtUtc);
        SetNoStore(httpContext.Response);
        return Results.Ok(new LoginResponse(
            issued.Value,
            issued.ExpiresAtUtc,
            ToUserResponse(user, roles)));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        RefreshSessionService refreshSessions,
        RefreshCookieService cookieService,
        TrustedWebOriginValidator originValidator)
    {
        if (!originValidator.IsTrusted(httpContext.Request))
        {
            return ApiErrors.Forbidden("The request origin is not allowed.");
        }

        var result = await refreshSessions.RotateAsync(
            httpContext.Request.Cookies[RefreshCookieService.CookieName],
            httpContext.RequestAborted);
        if (!result.Succeeded)
        {
            cookieService.Clear(httpContext.Response);
            SetNoStore(httpContext.Response);
            return ApiErrors.Unauthorized("The session could not be refreshed.");
        }

        var roles = (await userManager.GetRolesAsync(result.User!))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        var issued = tokenService.Issue(result.User!, roles);
        cookieService.Write(httpContext.Response, result.Replacement!.Token, result.Replacement.ExpiresAtUtc);
        SetNoStore(httpContext.Response);
        return Results.Ok(new LoginResponse(
            issued.Value,
            issued.ExpiresAtUtc,
            ToUserResponse(result.User!, roles)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        RefreshSessionService refreshSessions,
        RefreshCookieService cookieService,
        TrustedWebOriginValidator originValidator)
    {
        if (!originValidator.IsTrusted(httpContext.Request))
        {
            return ApiErrors.Forbidden("The request origin is not allowed.");
        }

        await refreshSessions.RevokeFamilyForTokenAsync(
            httpContext.Request.Cookies[RefreshCookieService.CookieName],
            httpContext.RequestAborted);
        cookieService.Clear(httpContext.Response);
        SetNoStore(httpContext.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return ApiErrors.Unauthorized("The authenticated user is unavailable.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("The authenticated user is unavailable.");
        }

        var roles = (await userManager.GetRolesAsync(user))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        return Results.Ok(ToUserResponse(user, roles));
    }

    private static AuthUserResponse ToUserResponse(
        ApplicationUser user,
        IReadOnlyList<string> roles)
        => new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles);

    private static void SetNoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Email))
        {
            errors[nameof(Email)] = ["Email is required."];
        }
        else if (Email.Length > 256)
        {
            errors[nameof(Email)] = ["Email must not exceed 256 characters."];
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            errors[nameof(Password)] = ["Password is required."];
        }
        else if (Password.Length > 256)
        {
            errors[nameof(Password)] = ["Password must not exceed 256 characters."];
        }

        return errors;
    }
}

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthUserResponse User);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
