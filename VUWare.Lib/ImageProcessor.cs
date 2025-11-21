using System;
using System.Collections.Generic;
using System.IO;

namespace VUWare.Lib
{
    /// <summary>
    /// Processes images for display on VU1 dial e-paper screens.
    /// Converts images to 1-bit black/white format and handles chunking for transmission.
    /// </summary>
    public class ImageProcessor
    {
        // Display specifications
        public const int DISPLAY_WIDTH = 200;
        public const int DISPLAY_HEIGHT = 200;
        public const int BYTES_PER_IMAGE = (DISPLAY_WIDTH * DISPLAY_HEIGHT) / 8; // 5000 bytes
        public const int MAX_CHUNK_SIZE = 1000;

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
        /// Note: This is a placeholder. Real implementation would use System.Drawing or similar.
        /// For now, assumes input is already grayscale pixel data.
        /// </summary>
        public static byte[] LoadImageFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Image file not found: {filePath}");
            }

            // This is a simplified implementation
            // A full implementation would:
            // 1. Load PNG/BMP/JPEG using System.Drawing or third-party library
            // 2. Resize to 200x200 if needed
            // 3. Convert to grayscale
            // 4. Convert to 1-bit format

            // For now, just read raw binary file (assumed to be 1-bit pixel data)
            byte[] data = File.ReadAllBytes(filePath);

            if (data.Length != BYTES_PER_IMAGE)
            {
                throw new InvalidOperationException(
                    $"Image file size mismatch. Expected {BYTES_PER_IMAGE} bytes, got {data.Length}");
            }

            return data;
        }

        /// <summary>
        /// Creates a blank (white) image.
        /// </summary>
        public static byte[] CreateBlankImage()
        {
            return new byte[BYTES_PER_IMAGE]; // All zeros = white pixels
        }

        /// <summary>
        /// Creates a test pattern (checkerboard).
        /// </summary>
        public static byte[] CreateTestPattern()
        {
            byte[] image = new byte[BYTES_PER_IMAGE];
            int bytesPerRow = DISPLAY_WIDTH / 8;

            for (int y = 0; y < DISPLAY_HEIGHT; y++)
            {
                for (int x = 0; x < bytesPerRow; x++)
                {
                    if ((x + (y / 8)) % 2 == 0)
                    {
                        // Checkerboard pattern
                        image[y * bytesPerRow + x] = 0xAA;
                    }
                }
            }

            return image;
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
