# VUWare.Console - Interactive Dial Controller

A professional command-line interface for controlling Streacom VU1 dials via the VU1 Gauge Hub. Features comprehensive logging, real-time status display, and guided automated testing.

## Features

- **Auto-Detection**: Automatically finds and connects to VU1 Gauge Hub via USB
- **Interactive Commands**: 12+ commands for complete dial control
- **Dial Discovery**: Automatic I2C bus scanning and device provisioning
- **Position Control**: Set dial positions 0-100% with smooth animations
- **Backlight Control**: 11 predefined colors (Red, Green, Blue, Yellow, Cyan, Magenta, Orange, Purple, Pink, White, Off)
- **Display Management**: Upload custom 1-bit images to dial e-paper displays
- **Automated Testing**: Single-command dial test suite with auto-setup
- **Comprehensive Logging**: Timestamped operation logs with performance metrics
- **Professional Output**: Color-coded console output with visual progress indicators

## Quick Start

### Prerequisites

- .NET 8.0 or later
- VU1 Gauge Hub connected via USB
- One or more VU1 dials connected to the hub

### Installation & Setup

```bash
# Clone or navigate to the repository
cd C:\Repos\VUWare

# Build the solution
dotnet build VUWare.sln

# Run the console application
dotnet run --project VUWare.Console
```

### Basic Usage

```
??????????????????????????????????????
?   VUWare Dial Controller Console   ?
?      https://vudials.com           ?
??????????????????????????????????????

> connect
? Connected to VU1 Hub!

> init
? Initialized! Found 2 dial(s).

> dials
[Shows all discovered dials with status]

> set 3A4B5C6D7E8F0123 75
? Dial set to 75%

> color 3A4B5C6D7E8F0123 red
? Backlight set to Red

> exit
Goodbye!
```

## Commands Reference

### Connection Management

| Command | Description | Example |
|---------|-------------|---------|
| `connect` | Auto-detect and connect to VU1 Hub | `connect` |
| `connect <port>` | Connect to specific COM port | `connect COM3` |
| `disconnect` | Disconnect from hub | `disconnect` |
| `status` | Show connection and initialization status | `status` |

### Device Discovery

| Command | Description | Example |
|---------|-------------|---------|
| `init` | Discover and initialize all dials | `init` |
| `dials` | List all discovered dials with details | `dials` |
| `dial <uid>` | Get detailed info for specific dial | `dial 3A4B5C6D7E8F0123` |

### Dial Control

| Command | Description | Example |
|---------|-------------|---------|
| `set <uid> <percent>` | Set dial position (0-100%) | `set 3A4B5C6D7E8F0123 50` |
| `color <uid> <name>` | Set backlight color | `color 3A4B5C6D7E8F0123 red` |
| `colors` | Show available backlight colors | `colors` |
| `image <uid> <file>` | Load image to dial display | `image 3A4B5C6D7E8F0123 ./image.bmp` |

### Testing & Information

| Command | Description | Example |
|---------|-------------|---------|
| `test` | Run automated test on all dials* | `test` |
| `help` | Show detailed help information | `help` |
| `exit` | Exit the application | `exit` |

*The `test` command automatically connects and initializes if needed!

## Advanced Usage

### Automated Testing (Single Command)

The `test` command provides a complete automated testing experience:

```
> test
[Auto-connects if needed]
[Auto-initializes if needed]
[Tests each dial with visual feedback]
[Resets each dial to safe defaults]
? Test suite completed successfully!
```

Features:
- ? Automatically handles connection and initialization
- ? Tests dial position control (sets to 50%)
- ? Tests backlight color (sets to Green)
- ? Pauses after each dial for inspection
- ? Resets each dial to 0% and Off
- ? Shows operation timing for performance analysis
- ? Handles errors gracefully

Perfect for:
- Hardware verification after installation
- Maintenance testing
- Troubleshooting dial issues
- Quick health checks

### Manual Workflow

```bash
# Step 1: Connect
> connect
? Connected to VU1 Hub!

# Step 2: Initialize
> init
? Initialized! Found 3 dial(s).
  Dial #1: CPU Temperature (3A4B5C6D7E8F0123)
  Dial #2: GPU Load (4B5C6D7E8F012345)
  Dial #3: Memory Usage (5C6D7E8F01234567)

# Step 3: Get dial details
> dial 3A4B5C6D7E8F0123
Name:        CPU Temperature
Position:    0%
Backlight:   RGB(0, 0, 0)
Last Comm:   2025-01-21 14:32:18

# Step 4: Control dials
> set 3A4B5C6D7E8F0123 75
? Dial set to 75%

> color 3A4B5C6D7E8F0123 red
? Backlight set to Red

# Step 5: Verify changes
> dial 3A4B5C6D7E8F0123
Name:        CPU Temperature
Position:    75%
Backlight:   RGB(100, 0, 0)
Last Comm:   2025-01-21 14:32:20

# Step 6: Clean up
> disconnect
? Disconnected from VU1 Hub.
```

### Batch Operations

```bash
# Set multiple dials with different values
> set 3A4B5C6D7E8F0123 25
> set 4B5C6D7E8F012345 50
> set 5C6D7E8F01234567 75

# Set all dials to same color
> color 3A4B5C6D7E8F0123 green
> color 4B5C6D7E8F012345 green
> color 5C6D7E8F01234567 green

# Verify all changes
> dials
```

## Available Colors

The console supports 11 predefined backlight colors:

- **off** - Black (0, 0, 0)
- **red** - Pure Red (100, 0, 0)
- **green** - Pure Green (0, 100, 0)
- **blue** - Pure Blue (0, 0, 100)
- **white** - Pure White (100, 100, 100)
- **yellow** - Yellow (100, 100, 0)
- **cyan** - Cyan (0, 100, 100)
- **magenta** - Magenta (100, 0, 100)
- **orange** - Orange (100, 50, 0)
- **purple** - Purple (100, 0, 100)
- **pink** - Pink (100, 25, 50)

View all colors with: `colors`

## Logging & Output

### Log Levels

The application uses color-coded logging for clear visibility:

| Level | Color | Format |
|-------|-------|--------|
| Info | Cyan | `[HH:MM:SS] ?  Message` |
| Detail | Gray | Message (indented) |
| Error | Red | `[HH:MM:SS] ? Message` |
| Warning | Yellow | `[HH:MM:SS] ? Message` |
| Success | Green | `? Message` |

### Command Tracking

Each command is logged with execution timing:
```
[14:32:15] ?  [Command #1] Executing: connect
[14:32:18] ?  [Command #1] Completed in 3245ms
```

### Status Display

The application provides detailed status information at multiple levels:
- Connection status (connected/disconnected)
- Initialization status (initialized/not initialized)
- Dial count
- Per-dial metrics (position, color, firmware, hardware, last communication)

## File Format Support

### Image Upload

The `image` command accepts binary image files (raw 1-bit format):
- **Size**: Exactly 5000 bytes
- **Dimensions**: 200x200 pixels
- **Format**: 1-bit per pixel (8 pixels per byte), packed vertically
- **Encoding**: Raw binary file

To prepare images:
1. Create 200x200 grayscale image
2. Convert to 1-bit (black & white only)
3. Pack bits vertically (8 pixels = 1 byte)
4. Save as raw binary file

For production use, consider using a third-party library like SixLabors.ImageSharp to convert PNG/BMP/JPEG files.

## Troubleshooting

### "Failed to connect to VU1 hub"

**Possible Causes:**
- Hub not connected via USB
- Hub not powered on
- Wrong USB drivers installed
- Another application using the port

**Solutions:**
1. Verify USB cable is connected
2. Check Device Manager for USB device (VID: 0x0403, PID: 0x6015)
3. Try a different USB port
4. Check for conflicting applications
5. Try manual connection: `connect COM3` (replace with your port)

### "No dials found after initialization"

**Possible Causes:**
- I2C cables not connected
- Dials not powered
- I2C bus issues
- Dial firmware issues

**Solutions:**
1. Check I2C cable connections to hub
2. Verify dial power supplies
3. Power cycle the hub and dials
4. Check last communication timestamp: `dial <uid>`
5. Try reconnecting: `disconnect` then `connect`

### Dial value not updating

**Possible Causes:**
- Dial is offline
- I2C communication failure
- Serial timeout
- Dial firmware issue

**Solutions:**
1. Check if dial appears in `dials` list
2. Check last communication: `dial <uid>`
3. Power cycle the dial
4. Check I2C cable to that specific dial
5. Try `init` to reinitialize

### Command timeout

**Expected Timing:**
- Connection: 1-3 seconds
- Discovery: 4-5 seconds
- Dial control: ~5 seconds per operation
- Image upload: 2-3 seconds

**If timing exceeds expectations:**
1. Check USB cable quality
2. Verify hub power supply
3. Check I2C cable quality
4. Restart the hub and dials
5. Check for other USB devices on same hub

## Performance Characteristics

### Operation Timing

| Operation | Expected Time |
|-----------|---------------|
| Auto-detect | 1-3 seconds |
| Discovery (discovery of 1 dial) | 1-2 seconds |
| Set position | ~1-2 seconds |
| Set color | ~1 second |
| Get dial info | <100ms |
| Test all (3 dials) | ~20 seconds |

### Limits

- **Maximum Dials**: 100 on one I2C bus
- **Image Size**: Exactly 5000 bytes
- **Image Chunk Size**: 1000 bytes (automatic chunking)
- **Position Range**: 0-100%
- **Color Channels**: 0-100% each (RGBW)

## System Requirements

- **Framework**: .NET 8.0 or later
- **OS**: Windows with USB support
- **Memory**: <100 MB
- **USB Port**: Available USB 2.0 or higher
- **VU1 Hub**: Required for operation

## Architecture

The console application is built on the **VUWare.Lib** library, which provides:

- `VU1Controller` - High-level API for dial control
- `SerialPortManager` - USB/Serial communication
- `DeviceManager` - Device discovery and management
- `CommandBuilder` - Protocol command generation
- `ProtocolHandler` - Message parsing
- `ImageProcessor` - Image encoding and chunking

For detailed library documentation, see VUWare.Lib/README.md

## Command Workflow Examples

### Example 1: Quick Hardware Test
```
> test
[Fully automated - no setup needed!]
```

### Example 2: Monitor CPU Temperature
```
> connect
> init
> set 3A4B5C6D7E8F0123 75    # Set to 75% (example CPU temp)
> color 3A4B5C6D7E8F0123 orange
> dial 3A4B5C6D7E8F0123     # Verify changes
```

### Example 3: Visual Indicator Setup
```
> connect
> init
> set 3A4B5C6D7E8F0123 0     # Normal - Green
> color 3A4B5C6D7E8F0123 green
> set 3A4B5C6D7E8F0123 50    # Warning - Yellow
> color 3A4B5C6D7E8F0123 yellow
> set 3A4B5C6D7E8F0123 100   # Critical - Red
> color 3A4B5C6D7E8F0123 red
```

## Tips for Success

1. **Always start with `connect` and `init`** (unless using `test`)
2. **Use `dials` to find UIDs** before controlling dials
3. **Check `status` if something seems wrong**
4. **Use `help` for detailed command information**
5. **Try `test` for quick hardware verification**
6. **Monitor timing** - helps identify I2C issues
7. **Keep I2C cables short** - reduces noise and timing issues

## Build & Development

### Building from Source

```bash
# Restore dependencies
dotnet restore VUWare.sln

# Build solution
dotnet build VUWare.sln

# Build release version
dotnet build -c Release VUWare.sln

# Run console app
dotnet run --project VUWare.Console
```

### Project Structure

```
VUWare.Console/
??? Program.cs              # Main application with all commands
??? VUWare.Console.csproj   # Project configuration
??? README.md              # This file

VUWare.Lib/
??? VU1Controller.cs       # High-level API
??? DeviceManager.cs       # Device management
??? SerialPortManager.cs   # Serial communication
??? CommandBuilder.cs      # Command generation
??? ProtocolHandler.cs     # Protocol parsing
??? DialState.cs           # Dial information
??? ImageProcessor.cs      # Image handling
??? [more classes...]
```

## Logging to Output Window

In Visual Studio, enable logging output:

1. Open View ? Output (Ctrl+Alt+O)
2. Select "Debug" from the dropdown
3. Run the application
4. Logs appear in the Output window
5. Look for `[SerialPort]` and `[DeviceManager]` messages

## FAQ

**Q: How do I find the UID of my dial?**
A: Connect and initialize, then run `dials` to see all UIDs listed.

**Q: Can I control multiple dials simultaneously?**
A: Run commands sequentially. For true parallelism, use the VUWare.Lib library directly.

**Q: What if my dial doesn't respond?**
A: Check I2C cables, power supply, and run `dial <uid>` to check last communication time.

**Q: How do I upload an image to a dial?**
A: Use `image <uid> <filepath>` with a 5000-byte raw binary 1-bit image file.

**Q: Can I customize colors?**
A: The console app uses predefined colors. For custom RGBA values, use the VUWare.Lib library directly.

**Q: What's the difference between `set` and `color` commands?**
A: `set` controls dial position (0-100%), `color` controls backlight color (RGBW).

## License

[Your License Here]

## Support & Contributions

- **GitHub**: https://github.com/uweinside/VUWare
- **Issues**: Report bugs via GitHub Issues
- **Discussions**: Use GitHub Discussions for questions

## Related Documentation

- **VUWare.Lib/README.md** - Library API reference
- **VUWare.Lib/IMPLEMENTATION.md** - Architecture details
- **VUWare.Lib/QUICK_REFERENCE.md** - Developer quick reference

---

**Application Version**: 1.0  
**Framework**: .NET 8.0  
**Last Updated**: 2025-01-21  
**Status**: ? Production Ready

