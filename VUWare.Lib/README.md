# VUWare.Lib - VU1 Gauge Hub C# Library

A comprehensive C# library for controlling Streacom VU1 dials via the VU1 Gauge Hub.

## Features

- **Device Discovery**: Auto-detect and provision VU1 dials on the I2C bus
- **Serial Communication**: Type-safe protocol implementation with automatic encoding/decoding
- **Dial Control**: Set dial positions (0-100%) with smooth easing animations
- **Backlight Control**: RGBW backlight control with named colors and easing
- **Display Management**: Update e-paper background images (200x144, 3600 bytes) with automatic chunking
- **State Tracking**: In-memory dial state with UID-based identification
- **Configuration Persistence**: Easing and calibration settings management
- **Async/Await**: Fully asynchronous API for non-blocking operations

## Architecture

### Core Components

1. **SerialPortManager** - USB/Serial communication with the hub
   - Auto-detection of VU1 hub (VID:0x0403, PID:0x6015)
   - Thread-safe send/receive with timeout handling
   - 115200 baud, 8N1 configuration

2. **ProtocolHandler** - Serial protocol implementation
   - Message parsing (>CCDDLLLL[DATA] format)
   - Status code validation
   - Hex string conversion utilities

3. **CommandBuilder** - Type-safe command construction
   - All 30+ device commands pre-built
   - Automatic data encoding and length calculation
   - Input validation

4. **DeviceManager** - Device discovery and state management
   - I2C bus rescanning and device provisioning
   - UID-based dial identification
   - Firmware/hardware version queries
   - Easing configuration management

5. **ImageProcessor** - E-paper display image handling
   - 1-bit binary format conversion (vertical packing 8 pixels/byte)
   - Automatic chunking for serial transmission (1000-byte max)
   - Test pattern generation

6. **VU1Controller** - High-level API
   - Easy-to-use main interface
   - Automatic connection and initialization
   - Periodic update loop for queued commands
   - Predefined named colors

### Class Hierarchy

```
VU1Controller (main entry point)
??? SerialPortManager (USB communication)
    ??? DeviceManager (device management)
        ??? CommandBuilder (command encoding)
            ??? ProtocolHandler (message parsing)
ImageProcessor (image encoding)
ImageUpdateQueue (queued image updates)

DialState (dial information)
??? BacklightColor (RGBW values)
??? EasingConfig (animation settings)
```

## Quick Start

### Basic Example

```csharp
using VUWare.Lib;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var controller = new VU1Controller();

        if (!controller.AutoDetectAndConnect())
        {
            Console.WriteLine("Failed to connect to VU1 hub");
            return;
        }

        if (!await controller.InitializeAsync())
        {
            Console.WriteLine("Failed to initialize");
            return;
        }

        var dials = controller.GetAllDials();
        Console.WriteLine($"Found {dials.Count} dials");

        foreach (var dial in dials.Values)
        {
            await controller.SetDialPercentageAsync(dial.UID, 75);
            await controller.SetBacklightColorAsync(dial.UID, Colors.Red);
            break;
        }

        controller.Disconnect();
    }
}
```

### Display Image Example

```csharp
// Blank (white) 200x144 image
byte[] blankImage = ImageProcessor.CreateBlankImage();

// Test pattern
byte[] testPattern = ImageProcessor.CreateTestPattern();

// Set image on first dial
var dials = controller.GetAllDials();
foreach (var dial in dials.Values)
{
    await controller.SetDisplayImageAsync(dial.UID, testPattern);
    break;
}

// Queue image update
foreach (var dial in dials.Values)
{
    controller.QueueImageUpdate(dial.UID, blankImage);
}
```

## API Reference

### ImageProcessor

#### Constants

- `DISPLAY_WIDTH` = 200
- `DISPLAY_HEIGHT` = 144
- `BYTES_PER_IMAGE` = 3600
- `MAX_CHUNK_SIZE` = 1000

#### Methods

- `byte[] CreateBlankImage()`
- `byte[] CreateTestPattern()`
- `byte[] LoadImageFile(string path)` (PNG/BMP/JPEG ? 1-bit packed 3600 bytes)
- `byte[] ConvertGrayscaleTo1Bit(...)`
- `List<byte[]> ChunkImageData(byte[] data)`

## Key Design Decisions

### Correct Display Geometry

Official product spec: e-paper panel is 200x144 pixels (not 200x200). 1-bit vertical packing yields 3600 bytes per full frame ((200*144)/8). Earlier reverse-engineered notes assumed 5000 bytes; those were based on a generic buffer limit. Only 3600 bytes must be transmitted for a full image.

### Chunked Image Transfer

3600-byte image is sent as 4 chunks (1000 + 1000 + 1000 + 600) with 200ms pauses matching the Python reference implementation. This prevents RX buffer overflow and mirrors firmware pacing expectations.

## Performance

| Operation | Typical Time |
|-----------|--------------|
| Discovery (single dial) | 1-2 s |
| Set dial value | ~50-100 ms |
| Set backlight | ~50-100 ms |
| Image upload (3600 bytes) | 2-3 s |

## Troubleshooting

### Image Not Displaying

- Ensure final buffer length is exactly 3600 bytes
- Verify vertical packing (8 vertical pixels per byte, MSB=top)
- Use `CreateTestPattern()` to validate display integrity
- Confirm dial is initialized and online

### Legacy Documentation Mentions 200x200 / 5000 Bytes

These values are obsolete. The hardware panel is 200x144; send only 3600 bytes. The protocol maximum RX data length (5000) exceeds required size and is a firmware upper bound.

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
