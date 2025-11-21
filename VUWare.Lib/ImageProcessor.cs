using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace VUWare.Lib
{
    /// <summary>
    /// Processes images for VU1 dial e-paper screens.
    /// Official specs: 200x144 pixels, 1-bit, vertical packing (8 vertical pixels per byte, MSB = top).
    /// Supports PNG/BMP/JPEG. Any source size is scaled to 200x144 (aspect preserved, letterboxed with white).
    /// </summary>
    public class ImageProcessor
    {
        public const int DISPLAY_WIDTH = 200;
        public const int DISPLAY_HEIGHT = 144; // Confirmed panel height
        public const int BYTES_PER_IMAGE = (DISPLAY_WIDTH * DISPLAY_HEIGHT) / 8; // 3600 bytes
        public const int MAX_CHUNK_SIZE = 1000;

        /// <summary>
        /// Split packed image buffer (3600 bytes) into <=1000 byte chunks.
        /// </summary>
        public static List<byte[]> ChunkImageData(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                throw new ArgumentException("Image data cannot be empty", nameof(imageData));
            if (imageData.Length != BYTES_PER_IMAGE)
                throw new ArgumentException($"Image data must be exactly {BYTES_PER_IMAGE} bytes (got {imageData.Length})", nameof(imageData));

            var chunks = new List<byte[]>();
            int offset = 0;
            while (offset < imageData.Length)
            {
                int size = Math.Min(MAX_CHUNK_SIZE, imageData.Length - offset);
                var chunk = new byte[size];
                Array.Copy(imageData, offset, chunk, 0, size);
                chunks.Add(chunk);
                offset += size;
            }
            return chunks;
        }

        /// <summary>
        /// Convert grayscale buffer (width*height) to packed vertical 1-bit. Threshold >127 => bit=1.
        /// Packing order: for each column x, process rows y in groups of 8 (top to bottom), MSB first.
        /// </summary>
        public static byte[] ConvertGrayscaleTo1Bit(byte[] grayscale, int width, int height, int threshold = 127)
        {
            if (grayscale == null || grayscale.Length != width * height)
                throw new ArgumentException("Grayscale buffer size mismatch", nameof(grayscale));
            var packed = new List<byte>(width * ((height + 7) / 8));

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y += 8)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8 && (y + bit) < height; bit++)
                    {
                        int idx = (y + bit) * width + x;
                        if (grayscale[idx] > threshold)
                            b |= (byte)(1 << (7 - bit));
                    }
                    packed.Add(b);
                }
            }
            return packed.ToArray();
        }

        /// <summary>
        /// Load image and produce 3600-byte packed buffer. Scaling:
        ///  - Exact 200x144: used directly.
        ///  - Other: scale proportionally to fit within 200x144, center on white background.
        /// </summary>
        public static byte[] LoadImageFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Image file not found: {filePath}");
            try
            {
                using var src = Image.FromFile(filePath);
                using var normalized = NormalizeToPanel(src);
                using var gray = ConvertToGrayscale(normalized);
                var pixels = ExtractGrayscalePixels(gray);
                var packed = ConvertGrayscaleTo1Bit(pixels, DISPLAY_WIDTH, DISPLAY_HEIGHT);
                if (packed.Length != BYTES_PER_IMAGE)
                    throw new InvalidOperationException($"Packed length {packed.Length} != {BYTES_PER_IMAGE}");
                return packed;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load image '{filePath}': {ex.Message}", ex);
            }
        }

        private static Bitmap NormalizeToPanel(Image source)
        {
            if (source.Width == DISPLAY_WIDTH && source.Height == DISPLAY_HEIGHT)
                return new Bitmap(source); // exact

            var canvas = new Bitmap(DISPLAY_WIDTH, DISPLAY_HEIGHT, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(canvas);
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            float scale = Math.Min((float)DISPLAY_WIDTH / source.Width, (float)DISPLAY_HEIGHT / source.Height);
            int targetW = (int)Math.Round(source.Width * scale);
            int targetH = (int)Math.Round(source.Height * scale);
            int offsetX = (DISPLAY_WIDTH - targetW) / 2;
            int offsetY = (DISPLAY_HEIGHT - targetH) / 2;
            g.DrawImage(source, new Rectangle(offsetX, offsetY, targetW, targetH));
            return canvas;
        }

        private static Bitmap ConvertToGrayscale(Image source)
        {
            if (source is Bitmap bmp && bmp.PixelFormat == PixelFormat.Format8bppIndexed && bmp.Width == DISPLAY_WIDTH && bmp.Height == DISPLAY_HEIGHT)
                return new Bitmap(bmp);

            var gray = new Bitmap(DISPLAY_WIDTH, DISPLAY_HEIGHT, PixelFormat.Format8bppIndexed);
            var pal = gray.Palette;
            for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
            gray.Palette = pal;

            using var temp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(temp)) g.DrawImage(source, 0, 0, source.Width, source.Height);

            var tempData = temp.LockBits(new Rectangle(0, 0, temp.Width, temp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var grayData = gray.LockBits(new Rectangle(0, 0, gray.Width, gray.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                byte[] scan = new byte[tempData.Stride];
                for (int y = 0; y < temp.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(tempData.Scan0, y * tempData.Stride), scan, 0, tempData.Stride);
                    int dstOffset = y * grayData.Stride;
                    for (int x = 0; x < temp.Width; x++)
                    {
                        int idx = x * 4;
                        byte b = scan[idx];
                        byte gCh = scan[idx + 1];
                        byte r = scan[idx + 2];
                        byte grayVal = (byte)((r * 299 + gCh * 587 + b * 114) / 1000);
                        System.Runtime.InteropServices.Marshal.WriteByte(IntPtr.Add(grayData.Scan0, dstOffset + x), grayVal);
                    }
                }
            }
            finally
            {
                temp.UnlockBits(tempData);
                gray.UnlockBits(grayData);
            }
            return gray;
        }

        private static byte[] ExtractGrayscalePixels(Bitmap bitmap)
        {
            if (bitmap.PixelFormat != PixelFormat.Format8bppIndexed || bitmap.Width != DISPLAY_WIDTH || bitmap.Height != DISPLAY_HEIGHT)
                throw new InvalidOperationException("Expected 200x144 8bpp indexed bitmap");

            byte[] output = new byte[DISPLAY_WIDTH * DISPLAY_HEIGHT];
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, data.Stride);
                    for (int x = 0; x < DISPLAY_WIDTH; x++)
                    {
                        byte paletteIndex = row[x];
                        output[y * DISPLAY_WIDTH + x] = bitmap.Palette.Entries[paletteIndex].R;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return output;
        }

        public static byte[] CreateBlankImage()
        {
            var white = new byte[DISPLAY_WIDTH * DISPLAY_HEIGHT];
            for (int i = 0; i < white.Length; i++) white[i] = 255;
            return ConvertGrayscaleTo1Bit(white, DISPLAY_WIDTH, DISPLAY_HEIGHT);
        }

        public static byte[] CreateTestPattern()
        {
            var buf = new byte[DISPLAY_WIDTH * DISPLAY_HEIGHT];
            for (int y = 0; y < DISPLAY_HEIGHT; y++)
            {
                for (int x = 0; x < DISPLAY_WIDTH; x++)
                {
                    buf[y * DISPLAY_WIDTH + x] = ((x + y) / 8) % 2 == 0 ? (byte)240 : (byte)40;
                }
            }
            return ConvertGrayscaleTo1Bit(buf, DISPLAY_WIDTH, DISPLAY_HEIGHT);
        }
    }

    /// <summary>
    /// Queues image updates for asynchronous sending.
    /// </summary>
    public class ImageUpdateQueue
    {
        private readonly Queue<(byte dialIndex, byte[] imageData)> _queue = new();
        private readonly object _lock = new();

        public void QueueImageUpdate(byte dialIndex, byte[] imageData)
        {
            if (imageData == null || imageData.Length != ImageProcessor.BYTES_PER_IMAGE)
                throw new ArgumentException($"Image must be {ImageProcessor.BYTES_PER_IMAGE} bytes", nameof(imageData));
            lock (_lock) _queue.Enqueue((dialIndex, imageData));
        }

        public bool TryGetNextUpdate(out byte dialIndex, out byte[]? imageData)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    var item = _queue.Dequeue();
                    dialIndex = item.dialIndex;
                    imageData = item.imageData;
                    return true;
                }
                dialIndex = 0; imageData = null; return false;
            }
        }

        public int PendingCount { get { lock (_lock) return _queue.Count; } }
        public void Clear() { lock (_lock) _queue.Clear(); }
    }
}
