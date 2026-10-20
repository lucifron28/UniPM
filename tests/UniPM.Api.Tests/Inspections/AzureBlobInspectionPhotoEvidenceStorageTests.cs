using System.Collections.Concurrent;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Inspections;

namespace UniPM.Api.Tests;

public sealed class AzureBlobInspectionPhotoEvidenceStorageTests
{
    [Fact]
    public void Azure_blob_provider_requires_a_public_https_account_root()
    {
        var options = new InspectionPhotoEvidenceOptions
        {
            StorageProvider = InspectionPhotoEvidenceOptions.AzureBlobProvider,
            BlobServiceUri = "https://unipmphotos.blob.core.windows.net"
        };

        options.Validate();

        Assert.Equal(
            new Uri("https://unipmphotos.blob.core.windows.net/"),
            options.GetValidatedBlobServiceUri());
    }

    [Theory]
    [InlineData("http://unipmphotos.blob.core.windows.net")]
    [InlineData("https://unipmphotos.blob.core.windows.net/private")]
    [InlineData("https://unipmphotos.blob.core.windows.net/?sig=fake")]
    [InlineData("https://user@unipmphotos.blob.core.windows.net")]
    [InlineData("https://unipmphotos.blob.core.windows.net/#fragment")]
    [InlineData("https://example.com")]
    public void Azure_blob_provider_rejects_invalid_service_uris(string serviceUri)
    {
        var options = new InspectionPhotoEvidenceOptions
        {
            StorageProvider = InspectionPhotoEvidenceOptions.AzureBlobProvider,
            BlobServiceUri = serviceUri
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Unknown_storage_provider_fails_validation()
    {
        var options = new InspectionPhotoEvidenceOptions { StorageProvider = "Blob" };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public async Task Azure_blob_storage_saves_reads_and_deletes_jpeg_evidence()
    {
        var client = new FakeBlobContainerClient();
        var storage = new AzureBlobInspectionPhotoEvidenceStorage(client);
        var photo = new byte[] { 0xff, 0xd8, 0xff, 0xd9 };

        var key = await storage.SaveAsync(photo, CancellationToken.None);

        Assert.Matches("^[0-9a-f]{32}\\.jpg$", key);
        Assert.Equal("image/jpeg", client.ContentTypes[key]);
        Assert.Equal(photo, await storage.ReadAsync(key, CancellationToken.None));

        await storage.DeleteAsync(key, CancellationToken.None);

        Assert.Null(await storage.ReadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_saves_use_independent_blob_names_and_contents()
    {
        var client = new FakeBlobContainerClient();
        var storage = new AzureBlobInspectionPhotoEvidenceStorage(client);
        var photos = Enumerable.Range(1, 24)
            .Select(index => new byte[] { 0xff, 0xd8, (byte)index, 0xff, 0xd9 })
            .ToArray();

        var keys = await Task.WhenAll(photos.Select(photo =>
            storage.SaveAsync(photo, CancellationToken.None)));

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(photos.Length, client.Files.Count);
        for (var index = 0; index < keys.Length; index++)
        {
            Assert.Equal(photos[index], client.Files[keys[index]]);
        }
    }

    [Fact]
    public async Task Azure_failures_are_sanitized_and_uncertain_upload_is_cleaned_up()
    {
        var client = new FakeBlobContainerClient
        {
            UploadFailure = new RequestFailedException(503, "sensitive provider response")
        };
        var storage = new AzureBlobInspectionPhotoEvidenceStorage(client);

        var uploadError = await Assert.ThrowsAsync<IOException>(() =>
            storage.SaveAsync(MinimalJpeg(), CancellationToken.None));

        Assert.Equal("Inspection photo storage operation failed.", uploadError.Message);
        Assert.Null(uploadError.InnerException);
        Assert.Empty(client.Files);

        client.UploadFailure = null;
        var key = await storage.SaveAsync(MinimalJpeg(), CancellationToken.None);
        client.ReadFailure = new RequestFailedException(503, "sensitive provider response");
        client.DeleteFailure = new RequestFailedException(503, "sensitive provider response");

        var readError = await Assert.ThrowsAsync<IOException>(() =>
            storage.ReadAsync(key, CancellationToken.None));
        var deleteError = await Assert.ThrowsAsync<IOException>(() =>
            storage.DeleteAsync(key, CancellationToken.None));

        Assert.Equal("Inspection photo storage operation failed.", readError.Message);
        Assert.Null(readError.InnerException);
        Assert.Equal("Inspection photo storage operation failed.", deleteError.Message);
        Assert.Null(deleteError.InnerException);
    }

    [Fact]
    public async Task Missing_credentials_are_sanitized_and_cancellation_is_preserved()
    {
        var client = new FakeBlobContainerClient
        {
            UploadAuthenticationFailure = new AuthenticationFailedException("sensitive credential detail")
        };
        var storage = new AzureBlobInspectionPhotoEvidenceStorage(client);

        var authenticationError = await Assert.ThrowsAsync<IOException>(() =>
            storage.SaveAsync(MinimalJpeg(), CancellationToken.None));

        Assert.Equal("Inspection photo storage operation failed.", authenticationError.Message);
        Assert.Null(authenticationError.InnerException);

        client.UploadAuthenticationFailure = null;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.SaveAsync(MinimalJpeg(), cancellation.Token));
    }

    [Fact]
    public async Task Invalid_storage_keys_are_rejected_before_blob_access()
    {
        var storage = new AzureBlobInspectionPhotoEvidenceStorage(new FakeBlobContainerClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.ReadAsync("../photo.jpg", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.DeleteAsync("../photo.jpg", CancellationToken.None));
    }

    private static byte[] MinimalJpeg() => [0xff, 0xd8, 0xff, 0xd9];

    private sealed class FakeBlobContainerClient : BlobContainerClient
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, string> ContentTypes { get; } = new(StringComparer.Ordinal);
        public RequestFailedException? UploadFailure { get; set; }
        public AuthenticationFailedException? UploadAuthenticationFailure { get; set; }
        public RequestFailedException? ReadFailure { get; set; }
        public RequestFailedException? DeleteFailure { get; set; }

        public override BlobClient GetBlobClient(string blobName) => new FakeBlobClient(this, blobName);
    }

    private sealed class FakeBlobClient(FakeBlobContainerClient container, string blobName) : BlobClient
    {
        public override async Task<Response<BlobContentInfo>> UploadAsync(
            Stream content,
            BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            container.Files[blobName] = copy.ToArray();
            container.ContentTypes[blobName] = options.HttpHeaders.ContentType!;

            if (container.UploadFailure is { } requestFailure)
            {
                throw requestFailure;
            }

            if (container.UploadAuthenticationFailure is { } authenticationFailure)
            {
                throw authenticationFailure;
            }

            return null!;
        }

        public override async Task<Response> DownloadToAsync(
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (container.ReadFailure is { } readFailure)
            {
                throw readFailure;
            }

            if (!container.Files.TryGetValue(blobName, out var content))
            {
                throw new RequestFailedException(404, "Blob not found.");
            }

            await destination.WriteAsync(content, cancellationToken);
            return null!;
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None,
            BlobRequestConditions? conditions = default,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (container.DeleteFailure is { } deleteFailure)
            {
                throw deleteFailure;
            }

            container.Files.TryRemove(blobName, out _);
            container.ContentTypes.TryRemove(blobName, out _);
            return Task.FromResult<Response<bool>>(null!);
        }
    }
}
