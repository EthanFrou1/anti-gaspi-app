using Api.Services.Receipts;
using Api.Tests.Infrastructure;

namespace Api.Tests.Receipts;

public class JpegMetadataStripperTests
{
    private static readonly byte[] Photo = TestJpeg.PhotoWithMetadata;

    [Fact]
    public void TheFixture_ReallyContainsMetadata()
    {
        // Garde-fou : sinon, les tests ci-dessous ne prouveraient rien.
        Assert.True(TestJpeg.Contains(Photo, "Exif"));
        Assert.True(TestJpeg.Contains(Photo, "iPhone 15"));
        Assert.True(TestJpeg.Contains(Photo, "Commentaire secret"));
        Assert.True(TestJpeg.Contains(Photo, "TRAILING-SECRET"));
    }

    [Fact]
    public void Exif_Gps_Comment_AndDataAfterTheImage_AreRemoved()
    {
        var stripped = JpegMetadataStripper.Strip(Photo)!;

        Assert.False(TestJpeg.Contains(stripped, "Exif"));
        Assert.False(TestJpeg.Contains(stripped, "Apple"));
        Assert.False(TestJpeg.Contains(stripped, "iPhone"));
        Assert.False(TestJpeg.Contains(stripped, "Commentaire"));
        Assert.False(TestJpeg.Contains(stripped, "TRAILING"));
        Assert.Equal([0xFF, 0xD8], stripped[..2]);
        Assert.Equal([0xFF, 0xD9], stripped[^2..]);
    }

    [Fact]
    public void ThePixels_AreUntouched()
    {
        var stripped = JpegMetadataStripper.Strip(Photo)!;

        // Tout ce qui décrit l'image (tables, dimensions, données compressées) est identique :
        // du premier segment de quantification (FF DB) jusqu'à la fin de l'image.
        Assert.Equal(ImageData(Photo), ImageData(stripped));
    }

    [Fact]
    public void Stripping_IsIdempotent()
    {
        var once = JpegMetadataStripper.Strip(Photo)!;

        Assert.Equal(once, JpegMetadataStripper.Strip(once));
    }

    [Fact]
    public void ByteStuffing_AndRestartMarkers_InTheCompressedData_AreKept()
    {
        byte[] jpeg =
        [
            0xFF, 0xD8,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
            0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56,                     // bourrage FF 00, marqueur RST0
            0xFF, 0xD9,
        ];

        Assert.Equal(jpeg, JpegMetadataStripper.Strip(jpeg));
    }

    [Fact]
    public void AnInvalidStructure_IsRefused()
    {
        Assert.Null(JpegMetadataStripper.Strip([0x89, 0x50, 0x4E, 0x47]));             // PNG
        Assert.Null(JpegMetadataStripper.Strip(Photo[..(Photo.Length / 2)]));          // coupé : pas de fin d'image
        Assert.Null(JpegMetadataStripper.Strip([0xFF, 0xD8, 0xFF, 0xE1, 0x7F, 0xFF])); // segment plus long que le fichier
        Assert.Null(JpegMetadataStripper.Strip([0xFF, 0xD8, 0x00, 0x00]));             // pas de marqueur
    }

    private static byte[] ImageData(byte[] jpeg)
    {
        var start = jpeg.AsSpan().IndexOf([(byte)0xFF, (byte)0xDB]);
        var end = jpeg.AsSpan().IndexOf([(byte)0xFF, (byte)0xD9]) + 2;
        return jpeg[start..end];
    }
}
