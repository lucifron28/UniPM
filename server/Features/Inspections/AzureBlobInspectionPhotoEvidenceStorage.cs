using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Inspections;

public sealed class AzureBlobInspectionPhotoEvidenceStorage : IInspectionPhotoEvidenceStorage
{
    private const string GenericStorageFailure = "Inspection photo storage operation failed.";
    private readonly BlobContainerClient container;

    public AzureBlobInspectionPhotoEvidenceStorage(IOptions<InspectionPhotoEvidenceOptions> options)
        : this(CreateContainerClient(options.Value))
    {
    }

    public AzureBlobInspectionPhotoEvidenceStorage(BlobContainerClient container)
    {
        this.container = container;
    }

    public async Task<string> SaveAsync(
        ReadOnlyMemory<byte> photo,
        CancellationToken cancellationToken)
    {
        var storageKey = $"{Guid.NewGuid():N}.jpg";
        var blob = container.GetBlobClient(storageKey);
        await using var content = new MemoryStream(photo.ToArray(), writable: false);

        try
        {
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
            }, cancellationToken);
            return storageKey;
        }
        catch (RequestFailedException)
        {
            await TryDeleteAfterFailedUploadAsync(blob);
            throw new IOException(GenericStorageFailure);
        }
        catch (AuthenticationFailedException)
        {
            await TryDeleteAfterFailedUploadAsync(blob);
            throw new IOException(GenericStorageFailure);
        }
        catch (AggregateException)
        {
            await TryDeleteAfterFailedUploadAsync(blob);
            throw new IOException(GenericStorageFailure);
        }
        catch (OperationCanceledException)
        {
            await TryDeleteAfterFailedUploadAsync(blob);
            throw;
        }
    }

    public async Task<byte[]?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        InspectionPhotoEvidenceStorageKeys.Validate(storageKey);
        using var contents = new MemoryStream();
        try
        {
            await container.GetBlobClient(storageKey).DownloadToAsync(contents, cancellationToken);
            return contents.ToArray();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException)
        {
            throw new IOException(GenericStorageFailure);
        }
        catch (AuthenticationFailedException)
        {
            throw new IOException(GenericStorageFailure);
        }
        catch (AggregateException)
        {
            throw new IOException(GenericStorageFailure);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        InspectionPhotoEvidenceStorageKeys.Validate(storageKey);
        try
        {
            await container.GetBlobClient(storageKey).DeleteIfExistsAsync(
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException)
        {
            throw new IOException(GenericStorageFailure);
        }
        catch (AuthenticationFailedException)
        {
            throw new IOException(GenericStorageFailure);
        }
        catch (AggregateException)
        {
            throw new IOException(GenericStorageFailure);
        }
    }

    private static BlobContainerClient CreateContainerClient(InspectionPhotoEvidenceOptions options)
    {
        options.Validate();
        if (options.StorageProvider != InspectionPhotoEvidenceOptions.AzureBlobProvider)
        {
            throw new InvalidOperationException(
                "Azure Blob photo storage requires StorageProvider to be AzureBlob.");
        }

        var client = new BlobServiceClient(
            options.GetValidatedBlobServiceUri(),
            new DefaultAzureCredential());
        return client.GetBlobContainerClient(InspectionPhotoEvidenceOptions.AzureBlobContainerName);
    }

    private static async Task TryDeleteAfterFailedUploadAsync(BlobClient blob)
    {
        try
        {
            await blob.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
        catch (Exception exception) when (
            exception is RequestFailedException
                or AuthenticationFailedException
                or AggregateException
                or OperationCanceledException)
        {
        }
    }
}
