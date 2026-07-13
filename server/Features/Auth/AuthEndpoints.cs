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
            .WithName("Login");
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService tokenService)
    {
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
        return Results.Ok(new LoginResponse(
            issued.Value,
            issued.ExpiresAtUtc,
            ToUserResponse(user, roles)));
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
