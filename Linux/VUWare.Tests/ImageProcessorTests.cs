using VUWare.Lib;

namespace VUWare.Tests;

public sealed class ImageProcessorTests
{
    [Fact]
    public void ConvertGrayscaleTo1Bit_PacksColumnsVerticallyWithTopPixelInMostSignificantBit()
    {
        byte[] pixels =
        [
            255, 0,
            0, 255,
            255, 0,
            0, 255,
            255, 0,
            0, 255,
            255, 0,
            0, 255
        ];

        Assert.Equal(new byte[] { 0xAA, 0x55 }, ImageProcessor.ConvertGrayscaleTo1Bit(pixels, 2, 8));
    }

    [Fact]
    public void ChunkImageData_CreatesExpectedChunks()
    {
        var chunks = ImageProcessor.ChunkImageData(new byte[ImageProcessor.BYTES_PER_IMAGE]);
        Assert.Equal([1000, 1000, 1000, 600], chunks.Select(chunk => chunk.Length));
    }

    [Fact]
    public void BlankImage_IsWhiteAndCorrectSize()
    {
        var image = ImageProcessor.CreateBlankImage();
        Assert.Equal(ImageProcessor.BYTES_PER_IMAGE, image.Length);
        Assert.All(image, value => Assert.Equal(0xFF, value));
    }

    [Fact]
    public void ChunkImageData_RejectsIncorrectSize() =>
        Assert.Throws<ArgumentException>(() => ImageProcessor.ChunkImageData(new byte[10]));
}
