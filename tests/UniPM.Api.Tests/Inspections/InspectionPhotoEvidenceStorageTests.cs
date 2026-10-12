using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Inspections;

namespace UniPM.Api.Tests;

public sealed class InspectionPhotoEvidenceStorageTests
{
    [Fact]
    public async Task Filesystem_storage_keeps_photos_private_and_supports_read_and_delete()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"unipm-photo-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var storage = new FileSystemInspectionPhotoEvidenceStorage(
                new TestEnvironment(contentRoot, webRoot),
                Options.Create(new InspectionPhotoEvidenceOptions()));
            var photo = new byte[] { 0xff, 0xd8, 0xff, 0xd9 };

            var key = await storage.SaveAsync(photo, CancellationToken.None);
            var storedPath = Path.Combine(contentRoot, "App_Data", "InspectionPhotos", key);
            Assert.True(File.Exists(storedPath));
            var relativePath = Path.GetRelativePath(webRoot, storedPath);
            Assert.True(
                relativePath == ".."
                || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            Assert.Equal(photo, await storage.ReadAsync(key, CancellationToken.None));

            await storage.DeleteAsync(key, CancellationToken.None);

            Assert.False(File.Exists(storedPath));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Filesystem_storage_rejects_a_location_inside_the_public_web_root()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"unipm-photo-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var environment = new TestEnvironment(contentRoot, webRoot);

        Assert.Throws<InvalidOperationException>(() => new FileSystemInspectionPhotoEvidenceStorage(
            environment,
            Options.Create(new InspectionPhotoEvidenceOptions
            {
                StorageDirectory = Path.Combine(webRoot, "private")
            })));
    }

    private sealed class TestEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
