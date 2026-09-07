using SkiaSharp;

namespace VUWare.Lib;

public static class ImageProcessor
{
    public const int DISPLAY_WIDTH = 200;
    public const int DISPLAY_HEIGHT = 144;
    public const int BYTES_PER_IMAGE = DISPLAY_WIDTH * DISPLAY_HEIGHT / 8;
    public const int MAX_CHUNK_SIZE = 1000;

    public static byte[] LoadImageFile(string filePath, int threshold = 127)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Image file not found.", filePath);
        using var source = SKBitmap.Decode(filePath) ?? throw new InvalidDataException("The file is not a supported image.");
        using var normalized = new SKBitmap(DISPLAY_WIDTH, DISPLAY_HEIGHT, SKColorType.Gray8, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(normalized);
        canvas.Clear(SKColors.White);

        var scale = Math.Min((float)DISPLAY_WIDTH / source.Width, (float)DISPLAY_HEIGHT / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var destination = SKRect.Create((DISPLAY_WIDTH - width) / 2f, (DISPLAY_HEIGHT - height) / 2f, width, height);
        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawBitmap(source, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
        canvas.Flush();

        var grayscale = new byte[DISPLAY_WIDTH * DISPLAY_HEIGHT];
        for (var y = 0; y < DISPLAY_HEIGHT; y++)
        for (var x = 0; x < DISPLAY_WIDTH; x++)
        {
            var color = normalized.GetPixel(x, y);
            grayscale[y * DISPLAY_WIDTH + x] = color.Red;
        }
        return ConvertGrayscaleTo1Bit(grayscale, DISPLAY_WIDTH, DISPLAY_HEIGHT, threshold);
    }

    public static byte[] ConvertGrayscaleTo1Bit(byte[] grayscale, int width, int height, int threshold = 127)
    {
        ArgumentNullException.ThrowIfNull(grayscale);
        if (width <= 0 || height <= 0 || grayscale.Length != checked(width * height))
            throw new ArgumentException("The grayscale buffer dimensions are invalid.", nameof(grayscale));

        var output = new byte[width * ((height + 7) / 8)];
        var offset = 0;
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y += 8)
        {
            byte packed = 0;
            for (var bit = 0; bit < 8 && y + bit < height; bit++)
                if (grayscale[(y + bit) * width + x] > threshold) packed |= (byte)(1 << (7 - bit));
            output[offset++] = packed;
        }
        return output;
    }

    public static IReadOnlyList<byte[]> ChunkImageData(byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length != BYTES_PER_IMAGE)
            throw new ArgumentException($"Image data must contain exactly {BYTES_PER_IMAGE} bytes.", nameof(imageData));
        return imageData.Chunk(MAX_CHUNK_SIZE).Select(chunk => chunk.ToArray()).ToArray();
    }

    public static byte[] CreateBlankImage() => Enumerable.Repeat((byte)0xFF, BYTES_PER_IMAGE).ToArray();

    public static byte[] CreateTestPattern()
    {
        var pixels = new byte[DISPLAY_WIDTH * DISPLAY_HEIGHT];
        for (var y = 0; y < DISPLAY_HEIGHT; y++)
        for (var x = 0; x < DISPLAY_WIDTH; x++)
            pixels[y * DISPLAY_WIDTH + x] = (byte)(((x + y) / 8 % 2 == 0) ? 240 : 40);
        return ConvertGrayscaleTo1Bit(pixels, DISPLAY_WIDTH, DISPLAY_HEIGHT);
    }
}
