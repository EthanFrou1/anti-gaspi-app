using Api.Services.Receipts;

namespace Api.Tests.Receipts;

public class ReceiptImageFormatTests
{
    [Fact]
    public void Jpeg_IsRecognisedByItsFirstBytes()
    {
        Assert.True(ReceiptImageFormat.IsJpeg([0xFF, 0xD8, 0xFF, 0xDB, 0x00]));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]              // PNG : refusé (l'app envoie du JPEG)
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })]                          // GIF
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]  // WebP
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })]                                      // « <svg » : texte, pas une photo
    [InlineData(new byte[] { 0xFF, 0xD8 })]                                                  // JPEG tronqué
    [InlineData(new byte[0])]
    public void OtherContent_IsRefused(byte[] data)
    {
        Assert.False(ReceiptImageFormat.IsJpeg(data));
    }
}
