using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddUniPmAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        JwtRuntimeConfiguration runtimeConfiguration,
        AuthSessionRuntimeConfiguration sessionRuntimeConfiguration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthSessionOptions>(configuration.GetSection(AuthSessionOptions.SectionName));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddDefaultTokenProviders()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton(runtimeConfiguration);
        services.AddSingleton(sessionRuntimeConfiguration);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<JwtTokenService>();
        services.AddScoped<RefreshTokenGenerator>();
        services.AddScoped<RefreshSessionService>();
        services.AddScoped<RefreshCookieService>();
        services.AddScoped<TrustedWebOriginValidator>();
        services.AddScoped<DevelopmentUserSeeder>();
        services.AddSingleton<IAuthorizationHandler, PreventiveMaintenanceScheduleAuthorizationHandler>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = runtimeConfiguration.CreateValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        if (!Guid.TryParse(subject, out var userId))
                        {
                            context.Fail("The access token subject is invalid.");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();
                        var user = await userManager.FindByIdAsync(userId.ToString());
                        var tokenStampHash = context.Principal?
                            .FindFirst(AuthClaimNames.SecurityStampHash)?.Value;
                        var currentStampHash = user is null
                            ? null
                            : JwtTokenService.HashSecurityStamp(user.SecurityStamp);
                        if (user is null
                            || !user.IsActive
                            || tokenStampHash is null
                            || currentStampHash is null
                            || !CryptographicOperations.FixedTimeEquals(
                                Encoding.ASCII.GetBytes(tokenStampHash),
                                Encoding.ASCII.GetBytes(currentStampHash)))
                        {
                            context.Fail("The access token user session is no longer valid.");
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthPolicyCatalog.CanManageAdminManagement,
                policy => policy.RequireRole(AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanManageAssets,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanManageSchedules,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Supervisor, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanGenerateSchedules,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanReadSchedules,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(
                        AuthRoleCatalog.Gsd,
                        AuthRoleCatalog.Inspector,
                        AuthRoleCatalog.Supervisor,
                        AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanAssignScheduleSupervisors,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanAssignScheduleWorkers,
                policy => policy.RequireRole(AuthRoleCatalog.Supervisor, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanManagePreventiveMaintenanceForms,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Admin));
            options.AddPolicy(
                AuthPolicyCatalog.CanInspectPreventiveMaintenanceSchedule,
                policy =>
                {
                    policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Admin);
                    policy.AddRequirements(PreventiveMaintenanceScheduleAccessRequirement.Instance);
                });
            options.AddPolicy(
                AuthPolicyCatalog.CanAccessCorrectiveMaintenanceHandoff,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd));
            options.AddPolicy(
                AuthPolicyCatalog.CanManageWmsReferral,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd));
        });

        return services;
    }
}
