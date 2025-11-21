# VUWare.Console

An interactive command-line application for testing and controlling VU dials (https://vudials.com) using the VUWare.Lib library.

## Features

- **Auto-detect and connect** to VU1 Gauge Hub via USB
- **Discover and list** all connected dials
- **Control dial position** (0-100%)
- **Set backlight colors** (predefined or custom RGB)
- **Upload e-paper display images** from BMP files
- **View detailed dial information** (firmware, hardware version, etc.)
- **Extensive logging and status information** for all operations
- **Interactive command-line interface** with helpful prompts
- **Performance metrics** - tracks operation timing for diagnostics
- **Comprehensive error reporting** with troubleshooting guidance

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

## Logging and Status Information

The console application includes comprehensive logging for all operations:

### Command Execution Logging
Every command displays:
- **Command number** - Sequential tracking of commands executed
- **Command name and arguments** - Exactly what was executed
- **Execution time** - Elapsed milliseconds for the operation
- **Timestamp** - When the command was processed

Example output:
```
[14:32:15] ?  [Command #1] Executing: connect
[14:32:18] ?  [Command #1] Completed in 3245ms
```

### Connection Status Logging
When connecting, you'll see:
- Connection status (success/failure)
- Discovery details (auto-detect vs manual port)
- Next recommended steps
- Troubleshooting guidance on failure

Example:
```
[14:32:15] ?  Starting auto-detection of VU1 hub
? Connected to VU1 Hub!
[14:32:18] ?  ? Successfully connected to VU1 Hub
  • Connection Status: ACTIVE
  • Initialization Status: NOT INITIALIZED
  • Next Step: Run 'init' to discover dials
```

### Initialization Logging
When discovering dials:
- Discovery process duration
- Total dials found
- Individual dial details (name, UID, firmware, hardware versions)
- Detailed summary of discovered hardware

Example:
```
[14:32:20] ?  Starting dial discovery process
Initializing and discovering dials...
[14:32:24] ?  ? Initialization successful, discovered 2 dial(s) in 4125ms
  Dial #1:
    - Name: CPU Temperature
    - UID: 3A4B5C6D7E8F0123
    - Index: 0
    - FW: 1.2.3
    - HW: 1.0
  Dial #2:
    - Name: GPU Load
    - UID: 4B5C6D7E8F012345
    - Index: 1
    - FW: 1.2.3
    - HW: 1.0
```

### Operation Logging
For dial control operations (set position, color, image), you see:
- Target dial and operation details
- Performance metrics (operation time)
- Success/failure status
- Troubleshooting tips on failure

Example:
```
[14:32:30] ?  Setting dial 'CPU Temperature' to 75%
Setting CPU Temperature to 75%...
? Dial set to 75%
[14:32:31] ?  ? Successfully set 'CPU Temperature' to 75% in 1200ms
  • Dial: CPU Temperature (3A4B5C6D7E8F0123)
  • Target Position: 75%
  • Operation Time: 1200ms
  • Status: SUCCESS
```

### Status Display Logging
The `status` command displays:
- Connection state (ACTIVE/INACTIVE)
- Initialization state (INITIALIZED/NOT INITIALIZED)
- Count of connected dials
- Summary of each dial's current state
- Last communication time

Example:
```
[14:33:00] ?  Displaying connection status
? Connected:           YES                                            ?
? Initialized:         YES                                            ?
? Dial Count:          2                                              ?
  • Connection Status: ACTIVE
  • Initialization Status: INITIALIZED
  • Connected Dials: 2
  Dial Summary:
    • CPU Temperature:
      - Position: 75%
      - Backlight: RGB(100, 0, 0)
      - Last Comm: 2024-01-15 14:32:31
    • GPU Load:
      - Position: 45%
      - Backlight: RGB(0, 100, 0)
      - Last Comm: 2024-01-15 14:32:28
```

### Log Levels

Different types of messages are color-coded:

| Symbol | Color | Meaning |
|--------|-------|---------|
| ? | Cyan | Information - command execution and major operations |
| ? | Green | Success - operation completed successfully |
| ? | Red | Error - operation failed |
| ? | Yellow | Warning - command had issues but didn't fail |
| (gray text) | Gray | Detail - supplementary information |

## Examples

### Example 1: Complete Workflow with Logging

```
> connect
[14:32:15] ?  Starting auto-detection of VU1 hub
Auto-detecting VU1 hub...
? Connected to VU1 Hub!
[14:32:18] ?  ? Successfully connected to VU1 Hub
  • Connection Status: ACTIVE
  • Initialization Status: NOT INITIALIZED
  • Next Step: Run 'init' to discover dials

> init
[14:32:20] ?  Starting dial discovery process
Initializing and discovering dials...
[14:32:24] ?  ? Initialization successful, discovered 2 dial(s) in 4125ms
? Initialized! Found 2 dial(s).
  • Discovery Time: 4125ms
  • Total Dials: 2

> set 3A4B5C6D7E8F0123 75
[14:32:30] ?  Setting dial 'CPU Temp' to 75%
Setting CPU Temp to 75%...
? Dial set to 75%
[14:32:31] ?  ? Successfully set 'CPU Temp' to 75% in 1200ms
  • Dial: CPU Temp (3A4B5C6D7E8F0123)
  • Target Position: 75%
  • Operation Time: 1200ms
  • Status: SUCCESS

> color 3A4B5C6D7E8F0123 red
[14:32:35] ?  Setting 'CPU Temp' backlight to Red
Setting CPU Temp backlight to Red...
? Backlight set to Red
[14:32:36] ?  ? Successfully set 'CPU Temp' backlight to Red in 850ms
  • Dial: CPU Temp (3A4B5C6D7E8F0123)
  • Color: Red
  • RGB Values: (100%, 0%, 0%)
  • White Value: 0%
  • Operation Time: 850ms
  • Status: SUCCESS
```

### Example 2: Set Dial Position with Diagnostics

```
> set 3A4B5C6D7E8F0123 50
[14:35:00] ?  Setting dial 'CPU Temp' to 50%
Setting CPU Temp to 50%...
? Dial set to 50%
[14:35:01] ?  ? Successfully set 'CPU Temp' to 50% in 945ms
  • Dial: CPU Temp (3A4B5C6D7E8F0123)
  • Target Position: 50%
  • Operation Time: 945ms
  • Status: SUCCESS
```

### Example 3: Upload Display Image with Details

```
> image 3A4B5C6D7E8F0123 ./icons/gauge.bmp
[14:36:10] ?  Loading image from: ./icons/gauge.bmp
  Image File Details:
  • Path: C:\Projects\VUWare\icons\gauge.bmp
  • Size: 5000 bytes
  • Modified: 2024-01-15 10:30:45
[14:36:10] ?  Image loaded successfully (5000 bytes) in 45ms
Uploading image to CPU Temp...
? Image uploaded successfully
[14:36:12] ?  ? Image successfully uploaded to 'CPU Temp' in 2150ms
  • Dial: CPU Temp (3A4B5C6D7E8F0123)
  • Image Size: 5000 bytes
  • Expected Size: 5000 bytes
  • Load Time: 45ms
  • Upload Time: 2150ms
  • Total Time: 2195ms
  • Status: SUCCESS
```

## Troubleshooting with Logs

The application provides context-specific troubleshooting information:

### Connection Issues
If connection fails, you'll see:
```
? Connection failed. Check USB connection and try again.
[14:32:18] ?  ? Failed to connect to VU1 Hub
  Troubleshooting steps:
  1. Verify VU1 Gauge Hub is connected via USB
  2. Check Device Manager for USB device
  3. Try specifying COM port directly: connect COM3
  4. Ensure proper USB drivers are installed
```

### Initialization Issues
If discovery fails, you'll see:
```
? Initialization failed. Check hub connection and power.
[14:32:24] ?  ? Initialization failed
  Troubleshooting:
  1. Check USB cable connection to VU1 Hub
  2. Verify hub has power and is responding
  3. Check I2C connections from hub to dials
  4. Ensure dials are powered
  5. Try power cycling the hub and dials
```

## Architecture

The console application uses the following components:

- **VU1Controller**: Main API for device communication
- **DeviceManager**: Dial discovery and management
- **ProtocolHandler**: Low-level command/response parsing
- **SerialPortManager**: USB communication
- **Logging System**: Comprehensive operation tracking with timestamps

All interaction goes through `VU1Controller`, which provides a clean async API.

## Performance Monitoring

Each command execution includes performance metrics:
- **Execution time**: Total time for the command
- **Operation time**: Actual communication time (for individual operations)
- **Load time**: Image file reading (for image uploads)
- **Upload time**: Image transmission (for image uploads)

Use these metrics to:
- Monitor system responsiveness
- Identify slow operations
- Diagnose communication issues

## Error Handling

The console application includes comprehensive error checking:

- **Connection errors**: Guides you to connect first
- **Initialization errors**: Reminds you to run init
- **Invalid parameters**: Shows usage information
- **Command failures**: Reports why the operation failed
- **Detailed context**: Logs include relevant error details and stack traces

## Related Documentation

- `QUICK_REFERENCE.md` - VUWare.Lib quick reference
- `README.md` - VUWare.Lib API documentation
- `IMPLEMENTATION.md` - Architecture and design details

## License

See LICENSE file in repository root.

## Links

- VU Dials: https://vudials.com
- Repository: https://github.com/uweinside/VUWare
