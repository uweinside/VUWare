# VUWare.Lib - VU1 Gauge Hub C# Library

A comprehensive C# library for controlling Streacom VU1 dials via the VU1 Gauge Hub.

## Features

- **Device Discovery**: Auto-detect and provision VU1 dials on the I2C bus
- **Serial Communication**: Type-safe protocol implementation with automatic encoding/decoding
- **Dial Control**: Set dial positions (0-100%) with smooth easing animations
- **Backlight Control**: RGBW backlight control with named colors and easing
- **Display Management**: Update e-paper background images with automatic chunking
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
   - 1-bit binary format conversion
   - Automatic chunking for serial transmission
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
?   ??? CommandBuilder (command encoding)
?       ??? ProtocolHandler (message parsing)
??? ImageProcessor (image encoding)
??? ImageUpdateQueue (queued image updates)

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

        // Connect to hub
        if (!controller.AutoDetectAndConnect())
        {
            Console.WriteLine("Failed to connect to VU1 hub");
            return;
        }

        // Initialize (discover dials)
        if (!await controller.InitializeAsync())
        {
            Console.WriteLine("Failed to initialize");
            return;
        }

        // Get all dials
        var dials = controller.GetAllDials();
        Console.WriteLine($"Found {dials.Count} dials");

        // Set first dial to 75%
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

### Advanced Example: Multiple Dials

```csharp
// Set multiple dials with different values
var dials = controller.GetAllDials();
int dialIndex = 0;

foreach (var dial in dials.Values)
{
    byte percentage = (byte)((dialIndex + 1) * 25); // 25%, 50%, 75%, 100%
    
    await controller.SetDialPercentageAsync(dial.UID, percentage);
    await controller.SetBacklightColorAsync(dial.UID, Colors.Green);
    
    // Custom easing: slow smooth animation
    var easing = new EasingConfig(
        dialStep: 1,
        dialPeriod: 100,
        backlightStep: 5,
        backlightPeriod: 100
    );
    await controller.SetEasingConfigAsync(dial.UID, easing);
    
    dialIndex++;
}
```

### Display Image Example

```csharp
// Create a blank 200x200 image
byte[] blankImage = ImageProcessor.CreateBlankImage();

// Or create a test pattern
byte[] testPattern = ImageProcessor.CreateTestPattern();

// Set image on first dial
var dials = controller.GetAllDials();
foreach (var dial in dials.Values)
{
    await controller.SetDisplayImageAsync(dial.UID, testPattern);
    break;
}

// Queue image update for later processing
foreach (var dial in dials.Values)
{
    controller.QueueImageUpdate(dial.UID, blankImage);
}
```

## API Reference

### VU1Controller

Main entry point for the library.

#### Methods

- `bool AutoDetectAndConnect()` - Auto-detect and connect to VU1 hub
- `bool Connect(string portName)` - Connect to specific COM port
- `void Disconnect()` - Disconnect from hub
- `async Task<bool> InitializeAsync()` - Discover dials and start updates
- `IReadOnlyDictionary<string, DialState> GetAllDials()` - Get all dials
- `DialState GetDial(string uid)` - Get dial by UID
- `async Task<bool> SetDialPercentageAsync(string uid, byte percentage)` - Set dial position
- `async Task<bool> SetBacklightAsync(string uid, byte r, byte g, byte b, byte w)` - Set color
- `async Task<bool> SetBacklightColorAsync(string uid, NamedColor color)` - Set named color
- `async Task<bool> SetEasingConfigAsync(string uid, EasingConfig config)` - Configure easing
- `async Task<bool> SetDisplayImageAsync(string uid, byte[] imageData)` - Update display
- `void QueueImageUpdate(string uid, byte[] imageData)` - Queue image for later

#### Properties

- `bool IsConnected` - Connection status
- `bool IsInitialized` - Initialization status
- `int DialCount` - Number of discovered dials

### DialState

Represents the current state of a single dial.

#### Properties

- `byte Index` - Hub index (0-99)
- `string UID` - Unique identifier (permanent)
- `string Name` - User-friendly name
- `byte CurrentValue` - Current position (0-100%)
- `BacklightColor Backlight` - Current backlight color
- `EasingConfig Easing` - Animation settings
- `string FirmwareVersion` - Firmware version
- `string HardwareVersion` - Hardware version
- `DateTime LastCommunication` - Last contact timestamp

### EasingConfig

Controls smooth transitions for dial and backlight.

#### Properties

- `uint DialStep` - Percentage change per period (default: 2)
- `uint DialPeriod` - Milliseconds between updates (default: 50)
- `uint BacklightStep` - Percentage change per period (default: 5)
- `uint BacklightPeriod` - Milliseconds between updates (default: 100)

### BacklightColor

RGBW color values (0-100%).

```csharp
var color = new BacklightColor(100, 50, 0); // Orange
```

### ImageProcessor

Static utility class for image handling.

#### Methods

- `byte[] CreateBlankImage()` - Create white 200x200 image
- `byte[] CreateTestPattern()` - Create checkerboard pattern
- `byte[] LoadImageFile(string path)` - Load image from file
- `byte[] ConvertGrayscaleTo1Bit(byte[] data, int w, int h, int threshold)` - Convert grayscale
- `List<byte[]> ChunkImageData(byte[] data)` - Split into transmission chunks

#### Constants

- `DISPLAY_WIDTH` = 200
- `DISPLAY_HEIGHT` = 200
- `BYTES_PER_IMAGE` = 5000
- `MAX_CHUNK_SIZE` = 1000

### NamedColor and Colors

Individual color instances (NamedColor class):

```csharp
var color = new NamedColor("Custom", 100, 50, 0); // Orange
```

Predefined colors in the Colors static class:

```csharp
await controller.SetBacklightColorAsync(uid, Colors.Red);
await controller.SetBacklightColorAsync(uid, Colors.Green);
await controller.SetBacklightColorAsync(uid, Colors.Blue);
```

#### Available Colors

- `Colors.Off` - Black (0, 0, 0)
- `Colors.Red` - (100, 0, 0)
- `Colors.Green` - (0, 100, 0)
- `Colors.Blue` - (0, 0, 100)
- `Colors.White` - (100, 100, 100)
- `Colors.Yellow` - (100, 100, 0)
- `Colors.Cyan` - (0, 100, 100)
- `Colors.Magenta` - (100, 0, 100)
- `Colors.Orange` - (100, 50, 0)
- `Colors.Purple` - (100, 0, 100)
- `Colors.Pink` - (100, 25, 50)

## Key Design Decisions

### UID-Based Identification

Dials are identified by their unique 12-byte UID (factory-programmed) rather than I2C address index. This ensures:

- Dial identity persists across power cycles
- Metadata follows the physical dial regardless of position
- Automatic recovery if dials are rearranged

### Asynchronous API

All network operations use async/await to prevent blocking:

```csharp
// Non-blocking
await controller.SetDialPercentageAsync(uid, 75);

// Event loop continues to run
```

### Automatic Provisioning

Device discovery handles the I2C provisioning complexity:

1. All dials start at default address 0x09
2. Hub assigns unique addresses via UID-based targeting
3. Each dial maps its UID to assigned address
4. Server maintains UID?Index mapping

### Chunked Image Transfer

Large image data (5000 bytes) is split into 1000-byte chunks:

- Prevents USB/serial buffer overflows
- Allows progress tracking
- Automatic 200ms delay between chunks

## Limitations and Known Issues

### Power Cycle Detection

The library does NOT automatically detect dial power cycles. If dials lose power during operation:

- Call `await controller.InitializeAsync()` again to re-provision
- Or disconnect/reconnect manually

Recommended: Implement a heartbeat mechanism in your application to detect disconnections.

### Image Loading

`ImageProcessor.LoadImageFile()` is a placeholder that only reads raw binary files. For production:

1. Use System.Drawing or a third-party library (SixLabors.ImageSharp)
2. Load PNG/BMP/JPEG files
3. Resize to 200x200
4. Convert to grayscale
5. Convert to 1-bit format

### Index vs. UID

Remember:

- **Index** (0-99): Hub's internal I2C address offset, can change
- **UID**: Permanent identifier, never changes, use this for persistence

## Implementation Notes

### Thread Safety

The library is thread-safe:

- All shared state protected by locks
- Serial port operations are serialized
- Device manager maintains consistent state

### Error Handling

Exceptions are logged to Debug output but not thrown for network errors:

- Connection failures return `false`
- Use `IsConnected` and `IsInitialized` properties
- Check return values of async operations

### Performance

- Discovery takes ~2-3 seconds (3 provision attempts × 200ms)
- Dial value update: ~50-100ms
- Image update: ~2-3 seconds (for 5000-byte image at 1000-byte chunks)
- Periodic update loop runs every 500ms

## Troubleshooting

### "Failed to connect to VU1 hub"

- Check USB connection
- Verify VID:0x0403, PID:0x6015 in Device Manager
- Try manual `Connect("COM3")` with specific port

### "No dials found after initialization"

- Check I2C connections from hub to dials
- Verify dial power supply
- Try `RescanBus()` and `ProvisionDevice()` manually

### Dial value not updating

- Verify dial's `LastCommunication` is recent
- Check backlight status (may indicate I2C issue)
- Rescan/reprovision the bus

### Image not displaying

- Verify image data size: 5000 bytes exactly
- Check 1-bit format (8 vertical pixels per byte)
- Try test pattern first: `CreateTestPattern()`

## References

- IMPLEMENTATION_GUIDE.md - Detailed architecture and device lifecycle
- SERIAL_PROTOCOL.md - Low-level protocol specification
- VU1 Gauge Hub hardware documentation

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
