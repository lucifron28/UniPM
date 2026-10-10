using UniPM.Api.Features.Inspections;

namespace UniPM.Api.Tests;

public sealed class InspectionPhotoJpegTests
{
    [Fact]
    public void Strips_application_metadata_and_preserves_image_segments()
    {
        byte[] source =
        [
            0xff, 0xd8,
            0xff, 0xe1, 0x00, 0x06, 0x45, 0x58, 0x49, 0x46,
            0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
            0x11,
            0xff, 0xd9
        ];

        var sanitized = InspectionPhotoJpeg.StripMetadataAndValidate(source);

        Assert.False(ContainsSequence(sanitized, [0xff, 0xe1]));
        Assert.True(ContainsSequence(sanitized, [0xff, 0xc0]));
        Assert.True(ContainsSequence(sanitized, [0xff, 0xda]));
        Assert.Equal((byte)0xd9, sanitized[^1]);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new byte[] { 0xff, 0xd8, 0xff, 0xd9 })]
    public void Rejects_non_jpeg_or_incomplete_jpeg(byte[] source)
    {
        Assert.Throws<InvalidDataException>(
            () => InspectionPhotoJpeg.StripMetadataAndValidate(source));
    }

    [Theory]
    [InlineData(4097, 1)]
    [InlineData(3000, 3000)]
    public void Rejects_images_over_dimension_or_pixel_limits(int width, int height)
    {
        byte[] source =
        [
            0xff, 0xd8,
            0xff, 0xc0, 0x00, 0x0b, 0x08,
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width,
            0x01, 0x01, 0x11, 0x00,
            0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
            0x11,
            0xff, 0xd9
        ];

        Assert.Throws<InvalidDataException>(
            () => InspectionPhotoJpeg.StripMetadataAndValidate(source));
    }

    private static bool ContainsSequence(byte[] source, byte[] value) =>
        source.AsSpan().IndexOf(value) >= 0;
}
