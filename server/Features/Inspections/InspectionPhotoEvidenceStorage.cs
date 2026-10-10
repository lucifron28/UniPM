using System.Buffers.Binary;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Inspections;

public sealed class InspectionPhotoEvidenceOptions
{
    public const string SectionName = "InspectionPhotoEvidence";
    public const int MaximumUploadBytes = 4 * 1024 * 1024;

    public string StorageDirectory { get; set; } = Path.Combine("App_Data", "InspectionPhotos");
}

public interface IInspectionPhotoEvidenceStorage
{
    Task<string> SaveAsync(ReadOnlyMemory<byte> photo, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed class FileSystemInspectionPhotoEvidenceStorage(
    IWebHostEnvironment environment,
    IOptions<InspectionPhotoEvidenceOptions> options) : IInspectionPhotoEvidenceStorage
{
    private readonly string storageDirectory = ResolveStorageDirectory(
        environment,
        options.Value.StorageDirectory);

    public async Task<string> SaveAsync(
        ReadOnlyMemory<byte> photo,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storageDirectory);
        var storageKey = $"{Guid.NewGuid():N}.jpg";
        var path = GetPath(storageKey);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, photo.ToArray(), cancellationToken);
            File.Move(temporaryPath, path);
            return storageKey;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public async Task<byte[]?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = GetPath(storageKey);
        return File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken)
            : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string storageKey)
    {
        if (storageKey.Length != 36
            || !storageKey.EndsWith(".jpg", StringComparison.Ordinal)
            || !Guid.TryParseExact(storageKey.AsSpan(0, 32), "N", out _))
        {
            throw new InvalidOperationException("The inspection photo storage key is invalid.");
        }

        return Path.Combine(storageDirectory, storageKey);
    }

    private static string ResolveStorageDirectory(
        IWebHostEnvironment environment,
        string configuredDirectory)
    {
        var root = Path.GetFullPath(configuredDirectory, environment.ContentRootPath);
        if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            var webRoot = Path.GetFullPath(environment.WebRootPath);
            var relativePath = Path.GetRelativePath(webRoot, root);
            var isOutsideWebRoot = relativePath == ".."
                || relativePath.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal);
            if (!isOutsideWebRoot || Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException(
                    "Inspection photo evidence must be stored outside the public web root.");
            }
        }

        return root;
    }
}

internal static class InspectionPhotoJpeg
{
    private const int MaximumDimension = 4096;
    private const long MaximumPixels = 8_000_000;

    public static byte[] StripMetadataAndValidate(ReadOnlySpan<byte> source)
    {
        if (source.Length < 4 || source[0] != 0xff || source[1] != 0xd8)
        {
            throw new InvalidDataException("The upload is not a JPEG image.");
        }

        using var sanitized = new MemoryStream(source.Length);
        sanitized.Write(source[..2]);
        var position = 2;
        var inScan = false;
        var hasFrame = false;
        var hasScan = false;
        var hasEnd = false;

        while (position < source.Length)
        {
            if (inScan)
            {
                var markerPosition = FindMarker(source, position);
                if (markerPosition < 0)
                {
                    throw new InvalidDataException("The JPEG image is incomplete.");
                }

                sanitized.Write(source[position..markerPosition]);
                position = markerPosition;
                inScan = false;
            }

            var markerStart = position;
            if (source[position] != 0xff)
            {
                throw new InvalidDataException("The JPEG image contains an invalid marker.");
            }

            while (position < source.Length && source[position] == 0xff) position++;
            if (position >= source.Length)
            {
                throw new InvalidDataException("The JPEG image is incomplete.");
            }

            var marker = source[position++];
            if (marker == 0xd9)
            {
                sanitized.Write(source[markerStart..position]);
                hasEnd = true;
                break;
            }

            if (marker == 0xd8 || IsRestartMarker(marker))
            {
                throw new InvalidDataException("The JPEG image contains an unexpected marker.");
            }

            if (marker == 0x01)
            {
                sanitized.Write(source[markerStart..position]);
                continue;
            }

            if (position + 2 > source.Length)
            {
                throw new InvalidDataException("The JPEG image is incomplete.");
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(source[position..]);
            if (segmentLength < 2 || position + segmentLength > source.Length)
            {
                throw new InvalidDataException("The JPEG image contains an invalid segment.");
            }

            var segmentEnd = position + segmentLength;
            if (IsStartOfFrame(marker))
            {
                ValidateDimensions(source, position, segmentLength);
                hasFrame = true;
            }

            if (marker == 0xda)
            {
                ValidateScanHeader(source, position, segmentLength);
            }

            if (!IsMetadataMarker(marker))
            {
                sanitized.Write(source[markerStart..segmentEnd]);
            }

            position = segmentEnd;
            if (marker == 0xda)
            {
                if (!hasFrame) throw new InvalidDataException("The JPEG image has no frame header.");
                hasScan = true;
                inScan = true;
            }
            else if (marker == 0xdc && hasScan)
            {
                inScan = true;
            }
        }

        if (!hasFrame || !hasScan || !hasEnd)
        {
            throw new InvalidDataException("The JPEG image is incomplete.");
        }

        return sanitized.ToArray();
    }

    private static int FindMarker(ReadOnlySpan<byte> source, int start)
    {
        for (var index = start; index < source.Length - 1; index++)
        {
            if (source[index] != 0xff) continue;

            var codeIndex = index + 1;
            while (codeIndex < source.Length && source[codeIndex] == 0xff) codeIndex++;
            if (codeIndex >= source.Length) return -1;

            var code = source[codeIndex];
            if (code == 0x00 || code == 0x01 || IsRestartMarker(code))
            {
                index = codeIndex;
                continue;
            }

            return index;
        }

        return -1;
    }

    private static void ValidateDimensions(
        ReadOnlySpan<byte> source,
        int lengthOffset,
        int segmentLength)
    {
        if (segmentLength < 8)
        {
            throw new InvalidDataException("The JPEG image has an invalid frame header.");
        }

        var dataOffset = lengthOffset + 2;
        var height = BinaryPrimitives.ReadUInt16BigEndian(source[(dataOffset + 1)..]);
        var width = BinaryPrimitives.ReadUInt16BigEndian(source[(dataOffset + 3)..]);
        var componentCount = source[dataOffset + 5];
        if (componentCount == 0 || segmentLength != 8 + componentCount * 3)
        {
            throw new InvalidDataException("The JPEG image has an invalid frame header.");
        }

        if (width == 0 || height == 0
            || width > MaximumDimension
            || height > MaximumDimension
            || (long)width * height > MaximumPixels)
        {
            throw new InvalidDataException("The JPEG image dimensions exceed the allowed limit.");
        }
    }

    private static void ValidateScanHeader(
        ReadOnlySpan<byte> source,
        int lengthOffset,
        int segmentLength)
    {
        if (segmentLength < 8)
        {
            throw new InvalidDataException("The JPEG image has an invalid scan header.");
        }

        var componentCount = source[lengthOffset + 2];
        if (componentCount == 0 || segmentLength != 6 + componentCount * 2)
        {
            throw new InvalidDataException("The JPEG image has an invalid scan header.");
        }
    }

    private static bool IsMetadataMarker(byte marker) =>
        marker is >= 0xe0 and <= 0xef || marker == 0xfe;

    private static bool IsRestartMarker(byte marker) => marker is >= 0xd0 and <= 0xd7;

    private static bool IsStartOfFrame(byte marker) => marker switch
    {
        0xc0 or 0xc1 or 0xc2 or 0xc3
            or 0xc5 or 0xc6 or 0xc7
            or 0xc9 or 0xca or 0xcb
            or 0xcd or 0xce or 0xcf => true,
        _ => false
    };
}
