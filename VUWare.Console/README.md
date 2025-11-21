# VUWare.Console

An interactive command-line application for testing and controlling VU dials (https://vudials.com) using the VUWare.Lib library.

## Features

- **Auto-detect and connect** to VU1 Gauge Hub via USB
- **Discover and list** all connected dials
- **Control dial position** (0-100%)
- **Set backlight colors** (predefined or custom RGB)
- **Upload e-paper display images** from BMP files
- **View detailed dial information** (firmware, hardware version, etc.)
- **Interactive command-line interface** with helpful prompts

## Getting Started

### Prerequisites

- .NET 8.0 or later
- VU1 Gauge Hub connected via USB
- One or more VU dials connected to the hub

### Build

```bash
dotnet build VUWare.Console
```

### Run

```bash
dotnet run --project VUWare.Console
```

## Usage

### Basic Workflow

1. **Connect** to the hub:
   ```
   > connect
   ```
   Or connect to a specific COM port:
   ```
   > connect COM3
   ```

2. **Initialize** and discover dials:
   ```
   > init
   ```

3. **List** all discovered dials:
   ```
   > dials
   ```

4. **Control** a dial (use the UID from the dials list):
   ```
   > set <uid> 75
   > color <uid> red
   > image <uid> path/to/image.bmp
   ```

### Available Commands

| Command | Arguments | Description |
|---------|-----------|-------------|
| `connect` | `[port]` | Auto-detect and connect to hub, or connect to specific COM port |
| `disconnect` | | Disconnect from hub |
| `init` | | Initialize and discover all dials |
| `status` | | Show connection and initialization status |
| `dials` | | List all discovered dials with current position and backlight |
| `dial` | `<uid>` | Show detailed information for a specific dial |
| `set` | `<uid> <0-100>` | Set dial position to a percentage |
| `color` | `<uid> <color_name>` | Set backlight color (see color options below) |
| `colors` | | Show all available backlight colors |
| `image` | `<uid> <filepath>` | Upload a 1-bit BMP image (200x200) to dial display |
| `help` | | Show detailed help information |
| `exit` | | Exit the application |

### Color Options

The following predefined colors are available:

- `off` - Black (0, 0, 0)
- `red` - Red (100, 0, 0)
- `green` - Green (0, 100, 0)
- `blue` - Blue (0, 0, 100)
- `white` - White (100, 100, 100)
- `yellow` - Yellow (100, 100, 0)
- `cyan` - Cyan (0, 100, 100)
- `magenta` - Magenta (100, 0, 100)
- `orange` - Orange (100, 50, 0)
- `purple` - Purple (100, 0, 100)
- `pink` - Pink (100, 25, 50)

### Image Upload

Images must be:
- **Format**: 1-bit monochrome BMP (black and white only)
- **Size**: 200x200 pixels
- **File Size**: Exactly 5000 bytes (packed as 8 pixels per byte)

The application will automatically validate and convert the image to the required format.

## Examples

### Example 1: Set Dial Position

```
> connect
Connected!
> init
Initialized! Found 2 dial(s).
> dials
Dial: CPU Temp
  UID: 3A4B5C6D7E8F0123
  Position: 25%

> set 3A4B5C6D7E8F0123 75
? Dial set to 75%
```

### Example 2: Set Backlight Color

```
> color 3A4B5C6D7E8F0123 red
? Backlight set to Red
```

### Example 3: Upload Display Image

First, prepare a 200x200 pixel 1-bit BMP image, then:

```
> image 3A4B5C6D7E8F0123 ./my_icon.bmp
? Image uploaded successfully
```

## Troubleshooting

### "Connection failed"
- Ensure VU1 hub is connected via USB
- Check Device Manager to confirm USB connection
- Try `connect COM3` (replace 3 with your actual COM port number)

### "Dial not found"
- Run `init` first to discover dials
- Verify dial UID is correct (from `dials` command)
- Ensure dial is powered and connected to hub

### "No dials found"
- Check USB connection from hub to dials
- Verify I2C cables are properly seated
- Power cycle the dials and hub

### "Image won't upload"
- Verify image is exactly 200x200 pixels
- Ensure image is 1-bit format (black and white only)
- Check file is not corrupted (should be 5000 bytes)

## Architecture

The console application uses the following components:

- **VU1Controller**: Main API for device communication
- **DeviceManager**: Dial discovery and management
- **ProtocolHandler**: Low-level command/response parsing
- **SerialPortManager**: USB communication

All interaction goes through `VU1Controller`, which provides a clean async API.

## Error Handling

The console application includes comprehensive error checking:

- **Connection errors**: Guides you to connect first
- **Initialization errors**: Reminds you to run init
- **Invalid parameters**: Shows usage information
- **Command failures**: Reports why the operation failed

## Performance Notes

- **Discovery**: 2-3 seconds (includes multiple discovery attempts)
- **Dial update**: 50-100ms per dial
- **Image upload**: 2-3 seconds
- **Command response**: 100-500ms depending on command

## Related Documentation

- `QUICK_REFERENCE.md` - VUWare.Lib quick reference
- `README.md` - VUWare.Lib API documentation
- `IMPLEMENTATION.md` - Architecture and design details

## License

See LICENSE file in repository root.

## Links

- VU Dials: https://vudials.com
- Repository: https://github.com/uweinside/VUWare
