# VUWare.Lib Quick Reference

## Installation

1. Add `System.IO.Ports` NuGet package (included in project file)
2. Reference `VUWare.Lib` project or DLL
3. Add `using VUWare.Lib;` to your code

## Basic Usage Pattern

```csharp
using VUWare.Lib;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        using (var vu1 = new VU1Controller())
        {
            // 1. Connect
            if (!vu1.AutoDetectAndConnect())
            {
                Console.WriteLine("Connection failed");
                return;
            }

            // 2. Initialize
            if (!await vu1.InitializeAsync())
            {
                Console.WriteLine("Initialization failed");
                return;
            }

            // 3. Use
            var dials = vu1.GetAllDials();
            foreach (var dial in dials.Values)
            {
                // Do something with dial...
            }

            // 4. Disconnect (automatic via using statement)
        }
    }
}
```

## Common Tasks

### Get All Dials
```csharp
var dials = vu1.GetAllDials();
foreach (var dial in dials.Values)
{
    Console.WriteLine($"{dial.Name}: {dial.CurrentValue}%");
}
```

### Set Dial Position
```csharp
string dialUID = "3A4B5C6D7E8F0123";
await vu1.SetDialPercentageAsync(dialUID, 75);
```

### Set Backlight Color
```csharp
// Predefined colors
await vu1.SetBacklightColorAsync(dialUID, Colors.Red);
await vu1.SetBacklightColorAsync(dialUID, Colors.Green);

// Custom color
var color = new NamedColor("Purple", 100, 0, 100);
await vu1.SetBacklightColorAsync(dialUID, color);

// Raw RGBW (0-100% each)
await vu1.SetBacklightAsync(dialUID, 100, 50, 0, 0);
```

### Configure Animation (Easing)
```csharp
var easing = new EasingConfig(
    dialStep: 5,              // % change per update
    dialPeriod: 50,           // milliseconds between updates
    backlightStep: 10,        // % change per update
    backlightPeriod: 50       // milliseconds between updates
);
await vu1.SetEasingConfigAsync(dialUID, easing);
```

### Update Display Image
```csharp
// Test pattern
byte[] image = ImageProcessor.CreateTestPattern();
await vu1.SetDisplayImageAsync(dialUID, image);

// Blank (white)
byte[] blank = ImageProcessor.CreateBlankImage();
await vu1.SetDisplayImageAsync(dialUID, blank);

// Load from file (PNG/BMP/JPEG ? 3600-byte packed buffer)
byte[] imageData = ImageProcessor.LoadImageFile("path/to/image.png");
await vu1.SetDisplayImageAsync(dialUID, imageData);
```

### Query Dial Information
```csharp
var dial = vu1.GetDial(dialUID);
if (dial != null)
{
    Console.WriteLine($"Name: {dial.Name}");
    Console.WriteLine($"Index: {dial.Index}");
    Console.WriteLine($"UID: {dial.UID}");
    Console.WriteLine($"FW Version: {dial.FirmwareVersion}");
    Console.WriteLine($"HW Version: {dial.HardwareVersion}");
    Console.WriteLine($"Easing: {dial.Easing}");
}
```

## Color Reference

### Predefined Colors
```csharp
Colors.Off         // (0, 0, 0)
Colors.Red         // (100, 0, 0)
Colors.Green       // (0, 100, 0)
Colors.Blue        // (0, 0, 100)
Colors.White       // (100, 100, 100)
Colors.Yellow      // (100, 100, 0)
Colors.Cyan        // (0, 100, 100)
Colors.Magenta     // (100, 0, 100)
Colors.Orange      // (100, 50, 0)
Colors.Purple      // (100, 0, 100)
Colors.Pink        // (100, 25, 50)
```

### Custom Colors
```csharp
var myColor = new NamedColor("MyColor", red, green, blue, white);
// red, green, blue, white: 0-100 (percent)
```

## Easing Presets

### Fast Animation (Responsive)
```csharp
new EasingConfig(
    dialStep: 5,
    dialPeriod: 50,
    backlightStep: 10,
    backlightPeriod: 50
)
```

### Medium Animation (Balanced)
```csharp
new EasingConfig(
    dialStep: 2,
    dialPeriod: 50,
    backlightStep: 5,
    backlightPeriod: 100
)
```

### Slow Animation (Smooth)
```csharp
new EasingConfig(
    dialStep: 1,
    dialPeriod: 100,
    backlightStep: 2,
    backlightPeriod: 100
)
```

## Connection Methods

### Auto-Detect
```csharp
if (vu1.AutoDetectAndConnect())
{
    // Connected to first available VU1 hub
}
```

### Manual COM Port
```csharp
if (vu1.Connect("COM3"))
{
    // Connected to specific port
}
```

### Disconnect
```csharp
vu1.Disconnect();
// Also called automatically by Dispose()
```

## Properties

### VU1Controller
- `bool IsConnected` - Currently connected to hub
- `bool IsInitialized` - Dials discovered
- `int DialCount` - Number of dials found

### DialState
- `string UID` - Unique identifier (use this for operations)
- `string Name` - Display name
- `byte Index` - Current I2C index
- `byte CurrentValue` - Current position (0-100%)
- `BacklightColor Backlight` - Current color
- `EasingConfig Easing` - Animation settings
- `string FirmwareVersion` - Dial firmware version
- `string HardwareVersion` - Dial hardware version
- `DateTime LastCommunication` - Last contact time

### BacklightColor
- `byte Red` - Red channel (0-100)
- `byte Green` - Green channel (0-100)
- `byte Blue` - Blue channel (0-100)
- `byte White` - White channel (0-100)

### EasingConfig
- `uint DialStep` - Dial animation step (%)
- `uint DialPeriod` - Dial update period (ms)
- `uint BacklightStep` - Backlight animation step (%)
- `uint BacklightPeriod` - Backlight update period (ms)

## Error Handling

### Check Return Values
```csharp
bool success = await vu1.SetDialPercentageAsync(uid, 50);
if (!success)
{
    // Handle failure - check IsConnected, dial existence, etc.
}
```

### Null Checks
```csharp
var dial = vu1.GetDial(uid);
if (dial == null)
{
    // Dial not found
}
```

### Exception Handling
```csharp
try
{
    await vu1.InitializeAsync();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Performance Notes

- **Discovery**: 2-3 seconds (includes 3 provision attempts)
- **Dial Update**: 50-100ms per dial
- **Image Upload**: 2-3 seconds (for 5KB image)
- **Update Loop**: 500ms interval

## Timeouts

- Default communication timeout: 2000ms
- Rescan timeout: 3000ms
- Query timeout: 1000ms
- Image transfer: 2000-3000ms

## Important Notes

1. **UID vs Index**: Always use UID for operations, not Index
2. **Connection**: Call `AutoDetectAndConnect()` or `Connect(port)` first
3. **Initialization**: Call `InitializeAsync()` before using dials
4. **Disposal**: Use `using` statement or call `Dispose()` manually
5. **Async**: All network operations are async - use `await`
6. **Power Cycles**: No automatic detection - manually re-initialize
7. **Thread Safety**: Library is thread-safe - safe to use from multiple threads

## Protocol Notes

### Line-Based Communication

The VU1 hub uses a **line-based protocol** where each command and response is a complete line terminated with `\r\n`. This design:

- Simplifies implementation (use standard `ReadLine()`)
- Improves reliability (complete messages, no partial data)
- Enables clean asynchronous I/O patterns

**C# Implementation:**

The VUWare.Lib library implements line-based reading in `SerialPortManager.cs`:

```csharp
// VUWare.Lib/SerialPortManager.cs - ReadResponseAsync method
private async Task<string> ReadResponseAsync(SerialPort serialPort, 
                                             int timeoutMs, 
                                             CancellationToken cancellationToken)
{
    // Read LINE-BY-LINE - blocks until \n or timeout
    while (!cancellationToken.IsCancellationRequested)
    {
        if (serialPort.BytesToRead == 0)
        {
            await Task.Delay(10, cancellationToken);
            continue;
        }
        
        string line = serialPort.ReadLine().Trim();  // Complete line
        
        if (!string.IsNullOrEmpty(line) && line.StartsWith("<"))
        {
            return line;  // Found response
        }
    }
}
```

**Key Implementation Details:**

- **Port Configuration** (`SerialPortManager.cs`):
  ```csharp
  _serialPort = new SerialPort(portName) {
      BaudRate = 115200,
      NewLine = "\r\n"  // Match hub line terminator
  };
  ```

- **Command Sending** (`SerialPortManager.SendCommandAsync`):
  ```csharp
  byte[] commandBytes = Encoding.ASCII.GetBytes(command + "\r\n");
  await serialPort.BaseStream.WriteAsync(commandBytes, ...);
  ```

- **Response Reading**: Uses `SerialPort.ReadLine()` which blocks until `\n` received or timeout occurs

See `SerialPortManager.cs`, `CommandBuilder.cs`, and `ProtocolHandler.cs` for complete implementation details.

**Reference Documentation:**
- `SERIAL_PROTOCOL.md` - Complete protocol specification
- `IMPLEMENTATION_GUIDE.md` - Architecture and design patterns
- `SerialPortManager.cs` - Serial communication implementation
- `ProtocolHandler.cs` - Message parsing and validation

## Troubleshooting

### "Not connected"
- Call `AutoDetectAndConnect()` or `Connect(port)` first

### "Dial not found"
- Call `InitializeAsync()` first
- Verify dial UID is correct
- Check dial is powered and connected

### "No dials found"
- Check USB connection
- Verify VU1 hub appears in Device Manager
- Check I2C connections from hub to dials
- Power cycle hub and dials

### "Command failed"
- Check `IsConnected` is true
- Verify dial UID exists in `GetAllDials()`
- Check dial firmware version is compatible
- Check serial port permissions

### "Image won't upload"
- Verify image data is exactly 3600 bytes
- Check 1-bit format (8 pixels per byte, vertical packing)
- Verify `IsConnected` and dial exists
- Check USB connection stability
- Ensure image is 200x144 pixels after conversion

## API Reference

### ImageProcessor

#### Constants

- `DISPLAY_WIDTH` = 200
- `DISPLAY_HEIGHT` = 144
- `BYTES_PER_IMAGE` = 3600
- `MAX_CHUNK_SIZE` = 1000

#### Methods

- `byte[] CreateBlankImage()` - Returns 3600-byte white image
- `byte[] CreateTestPattern()` - Returns 3600-byte test pattern
- `byte[] LoadImageFile(string path)` - PNG/BMP/JPEG ? 3600-byte packed buffer (auto-scaled to 200x144)
- `byte[] ConvertGrayscaleTo1Bit(byte[] grayscale, int width, int height, int threshold = 127)` - Convert grayscale to packed 1-bit
- `List<byte[]> ChunkImageData(byte[] data)` - Split 3600-byte buffer into ?1000-byte chunks

### VU1Controller

## Examples Location

Full working examples available in:
- `IMPLEMENTATION.md` - Architecture examples
- `README.md` - API reference examples

## More Information

- `README.md` - Complete documentation
- `IMPLEMENTATION.md` - Architecture and design
- `SERIAL_PROTOCOL.md` - Low-level protocol
- `IMPLEMENTATION_GUIDE.md` - Device specifications
