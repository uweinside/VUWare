# VUWare.Console - Feature Summary

## Overview

VUWare.Console is a production-ready interactive command-line application for controlling VU dials through the VU1 Gauge Hub. It provides a comprehensive interface with extensive logging, status monitoring, and diagnostic capabilities.

## Key Capabilities

### 1. Device Management

#### Connection
- **Auto-detect**: Automatically finds VU1 Gauge Hub on any available COM port
- **Manual port selection**: Connect to specific COM ports (e.g., COM3)
- **Connection verification**: Confirms successful connection with detailed status
- **Auto-timing**: Measures connection duration for diagnostics

#### Discovery
- **Automatic dial discovery**: Scans all connected dials via I2C
- **Device enumeration**: Lists all discovered dials with metadata
- **Firmware/Hardware versions**: Reports device capabilities
- **Dial assignment**: Automatic I2C index and UID assignment

#### Status Monitoring
- **Real-time status**: Current connection and initialization state
- **Per-dial metrics**: Individual dial position, color, and communication status
- **Connection health**: Last communication timestamp for each dial
- **System state**: Comprehensive overview of entire system

### 2. Dial Control

#### Position Control (0-100%)
- **Precise control**: 1% increments
- **Real-time feedback**: Immediate acknowledgment of position changes
- **Performance metrics**: Operation timing for each change
- **Status verification**: Confirmation of successful change

#### Backlight Control
- **11 predefined colors**: Red, green, blue, white, yellow, cyan, magenta, orange, purple, pink, off
- **RGBW support**: Full color space (0-100% per channel)
- **Real-time color changes**: Immediate visual feedback
- **Color reference**: Built-in color picker with RGB values

#### Display Control
- **E-paper image upload**: 200x200 1-bit monochrome images
- **File validation**: Automatic format and size checking
- **Performance tracking**: Load and upload timing
- **Clear display**: Ability to set blank or test patterns

### 3. Monitoring and Diagnostics

#### Command Logging
- **Sequential tracking**: Every command numbered (#1, #2, #3...)
- **Execution details**: What was executed, with full arguments
- **Timestamps**: HH:mm:ss format for every operation
- **Performance metrics**: Millisecond-level operation timing

#### Status Information
- **Connection state**: ACTIVE/INACTIVE
- **Initialization state**: INITIALIZED/NOT INITIALIZED
- **Dial inventory**: Complete list of discovered devices
- **Hardware details**: Firmware and hardware versions
- **Communication health**: Last communication timestamp

#### Error Reporting
- **Detailed error messages**: What went wrong and why
- **Troubleshooting guidance**: Specific steps to resolve issues
- **Context information**: File paths, device UIDs, values
- **Error stacks**: Exception details for severe failures

### 4. User Experience

#### Interactive Prompt
- **Command menu**: Quick reference at each prompt
- **Helpful prompts**: Next recommended steps after operations
- **Color-coded feedback**: Visual distinction of success/warning/error
- **Clear formatting**: Box drawing characters for readability

#### Comprehensive Help
- **Command reference**: Full list of all commands
- **Usage examples**: Common workflows documented
- **Color reference**: All available colors with RGB values
- **Troubleshooting**: Solutions for common problems

#### Status Displays
- **Connection status**: Overview of current connection
- **Dial listing**: All discovered dials in readable format
- **Dial details**: Complete information for a single dial
- **Performance summary**: Operation timing breakdown

## Technical Features

### Performance Metrics

Every operation includes timing information:
- **Discovery time**: How long to find all dials (typically 2-3 seconds)
- **Operation time**: How long to execute command (typically 50-1000ms)
- **Load time**: File loading duration (for images)
- **Upload time**: Data transmission duration
- **Total time**: Sum of all operation components

### State Management

The application tracks:
- Connection state (connected/disconnected)
- Initialization state (initialized/not initialized)
- Dial count (0-N dials)
- Per-dial state (position, color, timestamps)
- Command counter (sequentially numbered)

### Async Operations

All network operations are fully asynchronous:
- Non-blocking command execution
- Responsive UI during long operations
- Proper cancellation handling
- Clean async/await patterns

### Error Handling

Comprehensive error handling includes:
- Connection error detection and reporting
- Validation of dial UIDs
- File existence checking
- Image format validation
- Size validation (images must be exactly 5000 bytes)
- Exception catching with detailed logging

## Command Reference

### Connection Commands
```
connect           - Auto-detect and connect to VU1 Hub
connect <port>    - Connect to specific COM port (e.g., connect COM3)
disconnect        - Disconnect from VU1 Hub
status            - Display connection status and dial inventory
```

### Dial Inquiry Commands
```
dials             - List all discovered dials
dial <uid>        - Show detailed information for one dial
colors            - Show available backlight colors
help              - Display comprehensive help information
```

### Dial Control Commands
```
set <uid> <0-100> - Set dial position to percentage
color <uid> <col>  - Set backlight color (red, green, blue, etc.)
image <uid> <file> - Upload 1-bit BMP image to dial display
```

### System Commands
```
init              - Initialize and discover all dials
exit              - Exit the application
```

## Required Hardware

- VU1 Gauge Hub (https://vudials.com)
- One or more VU dials
- USB connection from computer to hub
- I2C cables connecting hub to dials

## System Requirements

- .NET 8.0 or later
- Windows with USB drivers installed
- At least one COM port for USB connection
- 20MB free disk space

## Output Examples

### Connection Success
```
? Connected to VU1 Hub!
  • Connection Status: ACTIVE
  • Initialization Status: NOT INITIALIZED
  • Next Step: Run 'init' to discover dials
```

### Initialization Success
```
? Initialized! Found 2 dial(s).
  Dial #1:
    - Name: CPU Temperature
    - UID: 3A4B5C6D7E8F0123
    - FW: 1.2.3
  Dial #2:
    - Name: GPU Load
    - UID: 4B5C6D7E8F012345
    - FW: 1.2.3
```

### Dial Control Success
```
? Dial set to 75%
  • Dial: CPU Temperature (3A4B5C6D7E8F0123)
  • Target Position: 75%
  • Operation Time: 1200ms
  • Status: SUCCESS
```

### Image Upload Success
```
? Image uploaded successfully
  • Dial: CPU Temperature (3A4B5C6D7E8F0123)
  • Image Size: 5000 bytes
  • Load Time: 45ms
  • Upload Time: 2150ms
  • Total Time: 2195ms
  • Status: SUCCESS
```

## Logging Output

Every command includes comprehensive logging:

```
[14:32:15] ?  [Command #1] Executing: connect
[14:32:15] ?  Starting auto-detection of VU1 hub
? Connected to VU1 Hub!
[14:32:18] ?  ? Successfully connected to VU1 Hub
  • Connection Status: ACTIVE
  • Initialization Status: NOT INITIALIZED
  • Next Step: Run 'init' to discover dials
[14:32:18] ?  [Command #1] Completed in 3245ms
```

## Architecture

The console app leverages the VUWare.Lib library:

```
Program.cs (Interactive Console)
    ?
VU1Controller (Main API)
    ?? SerialPortManager (USB Communication)
    ?? DeviceManager (Dial Discovery)
    ?? ProtocolHandler (Command/Response Parsing)
    ?? CommandBuilder (Protocol Command Construction)
    ?? ImageProcessor (Image Handling)
    ?? DialState (Device State Management)
```

## Performance Characteristics

- **Connection Detection**: 2-3 seconds (includes auto-detect retries)
- **Dial Discovery**: 4-5 seconds (per dial)
- **Position Change**: 50-150ms
- **Color Change**: 50-150ms
- **Image Upload**: 2-3 seconds (for 5KB image)
- **Command Response**: 100-500ms (typical)

## Safety Features

- **State validation**: Commands check required initialization state
- **UID validation**: All dial UIDs are verified before operations
- **Range checking**: Position values validated (0-100)
- **File validation**: Image files checked for size and existence
- **Error recovery**: Failed operations don't crash the application
- **Clean shutdown**: Proper resource cleanup on exit

## Future Enhancements

Potential additions:
- Batch operations (set multiple dials)
- Macro recording and playback
- Configuration file support
- Statistics and performance logging
- USB device detection notifications
- Multiple concurrent dial updates
- Animation and easing configuration
- Real-time dial monitoring mode

## Support and Documentation

- **README.md**: Usage guide and examples
- **LOGGING_ENHANCEMENTS.md**: Detailed logging feature documentation
- **QUICK_REFERENCE.md**: VUWare.Lib API reference
- **IMPLEMENTATION.md**: Architecture details
- **https://vudials.com**: Hardware information

## License

See LICENSE file in repository root.
