using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace UniPM.Api.Hosting;

public static class PackagedFrontend
{
    private static readonly string[] ReservedRoutePrefixes =
    [
        "/api",
        "/health",
        "/metrics",
        "/openapi",
        "/swagger",
        "/assets"
    ];

    public static bool IsPackaged(IWebHostEnvironment environment) =>
        !string.IsNullOrWhiteSpace(environment.WebRootPath)
        && File.Exists(Path.Combine(environment.WebRootPath, "index.html"));

    public static void Configure(WebApplication app)
    {
        var webRootPath = app.Environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath)) return;

        var indexPath = Path.Combine(webRootPath, "index.html");
        if (!File.Exists(indexPath)) return;

        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = app.Environment.WebRootFileProvider
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = app.Environment.WebRootFileProvider,
            OnPrepareResponse = context => ApplyCachePolicy(context.Context, context.File)
        });
        app.MapFallback(async context =>
        {
            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var path = context.Request.Path.Value ?? "/";
            if (!ShouldServeFrontend(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers[HeaderNames.CacheControl] = "no-store, no-cache, must-revalidate";
            await context.Response.SendFileAsync(indexPath);
        });
    }

    private static bool ShouldServeFrontend(string path)
    {
        foreach (var prefix in ReservedRoutePrefixes)
        {
            if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => Path.HasExtension(segment));
    }

    private static void ApplyCachePolicy(HttpContext context, IFileInfo file)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers[HeaderNames.CacheControl] = "no-store, no-cache, must-revalidate";
            return;
        }

        context.Response.Headers[HeaderNames.CacheControl] = IsHashedViteAsset(path, file.Name)
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }

    private static bool IsHashedViteAsset(string path, string fileName)
    {
        if (!path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase)) return false;

        var extensionStart = fileName.LastIndexOf('.');
        if (extensionStart < 0 || extensionStart < 9) return false;

        var hashStart = extensionStart - 8;
        if (fileName[hashStart - 1] != '-') return false;

        for (var index = hashStart; index < extensionStart; index++)
        {
            if (!char.IsAsciiLetterOrDigit(fileName[index])
                && fileName[index] is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }
}
