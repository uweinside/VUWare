using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace VUWare.Lib
{
    /// <summary>
    /// Processes images for display on VU1 dial e-paper screens.
    /// Converts images to 1-bit black/white format and handles chunking for transmission.
    /// Supports PNG, BMP, and JPEG images (200x144 pixels).
    /// </summary>
    public class ImageProcessor
    {
        // Display specifications
        public const int DISPLAY_WIDTH = 200;
        public const int DISPLAY_HEIGHT = 144;
        public const int BYTES_PER_IMAGE = (DISPLAY_WIDTH * DISPLAY_HEIGHT) / 8; // 3600 bytes
        public const int MAX_CHUNK_SIZE = 1000;

        // Supported image dimensions
        public const int EXPECTED_WIDTH = 200;
        public const int EXPECTED_HEIGHT = 144;

        /// <summary>
        /// Encodes raw image data (as 1-bit pixels) into chunks for transmission.
        /// Each byte represents 8 vertical pixels (MSB = top, LSB = bottom).
        /// </summary>
        public static List<byte[]> ChunkImageData(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Image data cannot be empty", nameof(imageData));
            }

            if (imageData.Length > BYTES_PER_IMAGE)
            {
                throw new ArgumentOutOfRangeException(nameof(imageData),
                    $"Image data exceeds maximum size of {BYTES_PER_IMAGE} bytes");
            }

            var chunks = new List<byte[]>();
            int bytesProcessed = 0;

            while (bytesProcessed < imageData.Length)
            {
                int chunkSize = Math.Min(MAX_CHUNK_SIZE, imageData.Length - bytesProcessed);
                byte[] chunk = new byte[chunkSize];
                Array.Copy(imageData, bytesProcessed, chunk, 0, chunkSize);
                chunks.Add(chunk);
                bytesProcessed += chunkSize;
            }

            return chunks;
        }

        /// <summary>
        /// Converts a grayscale pixel array to 1-bit packed format.
        /// Each byte represents 8 vertical pixels, packed MSB first.
        /// </summary>
        public static byte[] ConvertGrayscaleTo1Bit(byte[] grayscaleData, int width, int height, int threshold = 127)
        {
            if (grayscaleData == null || grayscaleData.Length != width * height)
            {
                throw new ArgumentException("Grayscale data size mismatch", nameof(grayscaleData));
            }

            List<byte> packedData = new List<byte>();

            // Process column by column (vertical packing)
            for (int x = 0; x < width; x++)
            {
                // Process each vertical strip of 8 pixels
                for (int y = 0; y < height; y += 8)
                {
                    byte packedByte = 0;

                    // Pack 8 pixels into one byte (MSB = top pixel)
                    for (int bit = 0; bit < 8 && y + bit < height; bit++)
                    {
                        int pixelIndex = (y + bit) * width + x;
                        byte grayValue = grayscaleData[pixelIndex];

                        // Threshold: >127 = white (0), <=127 = black (1)
                        bool isBlack = grayValue <= threshold;
                        if (isBlack)
                        {
                            packedByte |= (byte)(1 << (7 - bit)); // MSB first
                        }
                    }

                    packedData.Add(packedByte);
                }
            }

            return packedData.ToArray();
        }

        /// <summary>
        /// Converts a bitmap file to 1-bit e-paper format.
        /// Supports PNG, BMP, and JPEG images.
        /// Images are automatically converted to grayscale and resized to 200x144 if needed.
        /// </summary>
        public static byte[] LoadImageFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Image file not found: {filePath}");
            }

            try
            {
                // Load image
                using (var originalImage = Image.FromFile(filePath))
                {
                    // Convert to grayscale bitmap if needed
                    var grayscaleBitmap = ConvertToGrayscale(originalImage);
                    
                    // Resize to expected dimensions if needed
                    if (grayscaleBitmap.Width != EXPECTED_WIDTH || grayscaleBitmap.Height != EXPECTED_HEIGHT)
                    {
                        var resized = ResizeImage(grayscaleBitmap, EXPECTED_WIDTH, EXPECTED_HEIGHT);
                        grayscaleBitmap.Dispose();
                        grayscaleBitmap = resized;
                    }

                    // Extract grayscale pixel data
                    byte[] grayscaleData = ExtractGrayscalePixels(grayscaleBitmap);
                    
                    // Convert to 1-bit format
                    byte[] binarized = ConvertGrayscaleTo1Bit(grayscaleData, EXPECTED_WIDTH, EXPECTED_HEIGHT);
                    
                    grayscaleBitmap.Dispose();
                    
                    return binarized;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load image file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts an image to grayscale.
        /// </summary>
        private static Bitmap ConvertToGrayscale(Image source)
        {
            var grayscaleBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);
            
            if (source is Bitmap srcBitmap && srcBitmap.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                // If already 8-bit indexed (grayscale), just copy
                var srcData = srcBitmap.LockBits(
                    new Rectangle(0, 0, srcBitmap.Width, srcBitmap.Height),
                    ImageLockMode.ReadOnly,
                    srcBitmap.PixelFormat);

                var destData = grayscaleBitmap.LockBits(
                    new Rectangle(0, 0, grayscaleBitmap.Width, grayscaleBitmap.Height),
                    ImageLockMode.WriteOnly,
                    grayscaleBitmap.PixelFormat);

                byte[] buffer = new byte[srcData.Stride * srcData.Height];
                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, buffer, 0, buffer.Length);
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, destData.Scan0, buffer.Length);

                srcBitmap.UnlockBits(srcData);
                grayscaleBitmap.UnlockBits(destData);
            }
            else
            {
                // Convert color image to grayscale
                using (var tempBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                using (var graphics = Graphics.FromImage(tempBitmap))
                {
                    graphics.DrawImage(source, 0, 0);
                    
                    // Extract grayscale values using luminosity formula
                    byte[] pixelData = new byte[source.Width * source.Height];
                    byte[] scanline = new byte[tempBitmap.Width * 4]; // 32-bit = 4 bytes per pixel
                    
                    var bitmapData = tempBitmap.LockBits(
                        new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);

                    try
                    {
                        for (int y = 0; y < tempBitmap.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                System.IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride),
                                scanline, 0, bitmapData.Stride);

                            for (int x = 0; x < tempBitmap.Width; x++)
                            {
                                int idx = x * 4;
                                byte b = scanline[idx];
                                byte g = scanline[idx + 1];
                                byte r = scanline[idx + 2];
                                // Luminosity formula: 0.299*R + 0.587*G + 0.114*B
                                byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                                pixelData[y * source.Width + x] = gray;
                            }
                        }
                    }
                    finally
                    {
                        tempBitmap.UnlockBits(bitmapData);
                    }
                    
                    // Create proper grayscale palette (0-255)
                    var palette = grayscaleBitmap.Palette;
                    for (int i = 0; i < 256; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    grayscaleBitmap.Palette = palette;
                    
                    // Copy pixel data to bitmap
                    var destData = grayscaleBitmap.LockBits(
                        new Rectangle(0, 0, grayscaleBitmap.Width, grayscaleBitmap.Height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format8bppIndexed);

                    try
                    {
                        for (int y = 0; y < grayscaleBitmap.Height; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(
                                pixelData, y * grayscaleBitmap.Width,
                                System.IntPtr.Add(destData.Scan0, y * destData.Stride),
                                grayscaleBitmap.Width);
                        }
                    }
                    finally
                    {
                        grayscaleBitmap.UnlockBits(destData);
                    }
                }
            }

            return grayscaleBitmap;
        }

        /// <summary>
        /// Resizes an image to the specified dimensions using high-quality interpolation.
        /// </summary>
        private static Bitmap ResizeImage(Image source, int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            return bitmap;
        }

        /// <summary>
        /// Extracts grayscale pixel values from a bitmap.
        /// </summary>
        private static byte[] ExtractGrayscalePixels(Bitmap bitmap)
        {
            byte[] pixelData = new byte[bitmap.Width * bitmap.Height];
            
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            try
            {
                if (bitmapData.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    // For 8-bit indexed, copy directly
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            System.IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride),
                            pixelData,
                            y * bitmap.Width,
                            bitmap.Width);
                    }
                }
                else
                {
                    // For other formats, convert each pixel
                    byte[] scanline = new byte[bitmapData.Stride];
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            System.IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride),
                            scanline, 0, bitmapData.Stride);

                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            byte gray;
                            if (bitmapData.PixelFormat == PixelFormat.Format32bppArgb)
                            {
                                int idx = x * 4;
                                byte b = scanline[idx];
                                byte g = scanline[idx + 1];
                                byte r = scanline[idx + 2];
                                // Luminosity: 0.299*R + 0.587*G + 0.114*B
                                gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                            }
                            else
                            {
                                // Default: just use first byte
                                gray = scanline[x];
                            }
                            pixelData[y * bitmap.Width + x] = gray;
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return pixelData;
        }
    }

    /// <summary>
    /// Manages queued image updates for efficient transmission.
    /// </summary>
    public class ImageUpdateQueue
    {
        private readonly Queue<(byte dialIndex, byte[] imageData)> _queue;
        private readonly object _lockObj = new object();

        public ImageUpdateQueue()
        {
            _queue = new Queue<(byte, byte[])>();
        }

        /// <summary>
        /// Queues an image update for a dial.
        /// </summary>
        public void QueueImageUpdate(byte dialIndex, byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Image data cannot be empty", nameof(imageData));
            }

            lock (_lockObj)
            {
                _queue.Enqueue((dialIndex, imageData));
            }
        }

        /// <summary>
        /// Gets the next pending image update, if any.
        /// </summary>
        public bool TryGetNextUpdate(out byte dialIndex, out byte[]? imageData)
        {
            lock (_lockObj)
            {
                if (_queue.Count > 0)
                {
                    (dialIndex, imageData) = _queue.Dequeue();
                    return true;
                }

                dialIndex = 0;
                imageData = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the number of pending updates.
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (_lockObj)
                {
                    return _queue.Count;
                }
            }
        }

        /// <summary>
        /// Clears all pending updates.
        /// </summary>
        public void Clear()
        {
            lock (_lockObj)
            {
                _queue.Clear();
            }
        }
    }
}
