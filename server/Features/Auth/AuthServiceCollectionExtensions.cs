using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddUniPmAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        JwtRuntimeConfiguration runtimeConfiguration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
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
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton(runtimeConfiguration);
        services.AddScoped<JwtTokenService>();
        services.AddScoped<DevelopmentUserSeeder>();

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
                        if (user is null || !user.IsActive)
                        {
                            context.Fail("The access token user is no longer active.");
                        }
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthPolicyCatalog.CanManageAssets,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd));
            options.AddPolicy(
                AuthPolicyCatalog.CanManageSchedules,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Supervisor));
            options.AddPolicy(
                AuthPolicyCatalog.CanSubmitInspections,
                policy => policy.RequireRole(AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector));
            options.AddPolicy(
                AuthPolicyCatalog.CanReviewMaintenanceHistory,
                policy => policy.RequireRole(
                    AuthRoleCatalog.Gsd,
                    AuthRoleCatalog.Supervisor,
                    AuthRoleCatalog.DepartmentHead));
        });

        return services;
    }
}
