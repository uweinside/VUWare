# VUWare - Streacom VU1 Control Suite

A comprehensive C# toolkit for controlling **Streacom VU1 Dials** via the VU1 Gauge Hub. Features a powerful library for programmatic control and an interactive console application for hands-on management.

**Repository:** [github.com/uweinside/VUWare](https://github.com/uweinside/VUWare)  
**Based on:** Official VU1 code by Saša Karanovi? ([vudials.com](https://vudials.com))

---

## ?? Project Overview

VUWare is structured as a multi-project solution:

| Project | Purpose | Language | Framework |
|---------|---------|----------|-----------|
| **VUWare.Lib** | Core control library | C# | .NET 8 |
| **VUWare.Console** | Interactive CLI application | C# | .NET 8 |
| **VUWare.HWInfo64** | HWInfo64 sensor integration | C# | .NET 8 |

---

## ?? Quick Start

### Prerequisites
- .NET 8.0 or later
- VU1 Gauge Hub connected via USB
- VU1 dials connected to the hub

### Get Started (5 minutes)

```bash
# Clone the repository
git clone https://github.com/uweinside/VUWare.git
cd VUWare

# Build the solution
dotnet build

# Run the console application
dotnet run --project VUWare.Console
```

Once running, use these commands:
```
> connect          # Auto-detect and connect to hub
> init             # Discover all dials
> dials            # List discovered dials
> set <uid> 75     # Set dial position to 75%
> color <uid> red  # Set backlight to red
> help             # Show all commands
```

---

## ?? Project Details

### VUWare.Lib - Control Library

The core library providing everything needed to control the VU1 Hub and dials programmatically.

**Key Features:**
- ? Auto-detection of VU1 hub via USB (VID:0x0403, PID:0x6015)
- ? Device discovery with I2C bus scanning and provisioning
- ? Dial control: Position (0-100%), backlight (RGBW), animations
- ? E-paper display image management (200x144, 3600 bytes)
- ? Full async/await API with comprehensive error handling
- ? State tracking with UID-based dial identification

**Quick Example:**
```csharp
using VUWare.Lib;

var controller = new VU1Controller();
if (!controller.AutoDetectAndConnect())
    return;

if (!await controller.InitializeAsync())
    return;

var dials = controller.GetAllDials();
foreach (var dial in dials.Values)
{
    await controller.SetDialPercentageAsync(dial.UID, 75);
    await controller.SetBacklightColorAsync(dial.UID, Colors.Red);
}
```

**Architecture:**
```
VU1Controller (main entry point)
??? SerialPortManager (USB communication)
??? DeviceManager (device management & discovery)
?   ??? CommandBuilder (command encoding)
??? ImageProcessor (image conversion)
??? ImageUpdateQueue (async image updates)
```

**Learn More:** See [`VUWare.Lib/README.md`](VUWare.Lib/README.md) and [`VUWare.Lib/doc/SERIAL_PROTOCOL.md`](VUWare.Lib/doc/SERIAL_PROTOCOL.md)

---

### VUWare.Console - Interactive Application

A professional command-line interface for complete dial control with comprehensive logging and status monitoring.

**Key Features:**
- ? Auto-detection and connection management
- ? Interactive command-driven interface (12+ commands)
- ? Real-time status display with color-coded output
- ? Automated test suite (single command tests all dials)
- ? Image upload with auto-conversion (PNG/BMP/JPEG ? 200x144 1-bit)
- ? Timestamped operation logs with performance metrics
- ? HWInfo64 sensor monitoring integration

**Supported Commands:**

| Command | Description | Example |
|---------|-------------|---------|
| `connect` | Auto-detect and connect | `connect` |
| `connect <port>` | Connect to specific COM | `connect COM3` |
| `init` | Discover all dials | `init` |
| `status` | Show connection status | `status` |
| `dials` | List all discovered dials | `dials` |
| `dial <uid>` | Show dial details | `dial 290063...` |
| `set <uid> <0-100>` | Set dial position | `set 290063... 75` |
| `color <uid> <name>` | Set backlight color | `color 290063... red` |
| `colors` | List available colors | `colors` |
| `image <uid> <file>` | Upload image | `image 290063... ./image.png` |
| `test` | Auto-test all dials | `test` |
| `sensors` | List HWInfo64 sensors | `sensors` |
| `monitor <u> <s> <e>` | Monitor sensor on dial | `monitor 290063... "CPU Package" "Temperature"` |
| `help` | Show help | `help` |
| `exit` | Exit program | `exit` |

**Example Workflow:**
```
> connect
? Connected to VU1 Hub!
> init
? Initialized! Found 3 dial(s).
> dials
[Shows all discovered dials with UIDs]
> set 290063000750524834313020 50
? Dial set to 50%
> color 290063000750524834313020 cyan
? Backlight set to Cyan
> image 290063000750524834313020 ./etc/image_pack/cpu-temp.png
? Image uploaded successfully
```

**Backlight Colors:** Red, Green, Blue, White, Yellow, Cyan, Magenta, Orange, Purple, Pink, Off

**Learn More:** See [`VUWare.Console/README.md`](VUWare.Console/README.md) and [`VUWare.Console/QUICK_START.md`](VUWare.Console/QUICK_START.md)

---

### VUWare.HWInfo64 - Sensor Integration

A library for reading HWInfo64 sensor data and mapping it to VU1 dials for real-time hardware monitoring displays.

**Key Features:**
- ? Direct access to HWInfo64 shared memory
- ? Structured sensor data with full type safety
- ? Automatic polling with configurable intervals
- ? High-level controller API with event notifications
- ? Threshold-based status indicators (normal/warning/critical)
- ? Auto-scaling of sensor values to dial range

**Supported Sensor Types:**
- Temperature (°C, °F)
- Voltage (V)
- Fan Speed (RPM)
- Current (A)
- Power (W)
- Clock Speed (MHz, GHz)
- Usage (%)
- Other custom sensors

**Quick Example:**
```csharp
using VUWare.HWInfo64;

var reader = new HWiNFOReader();
if (reader.Connect())
{
    var readings = reader.ReadAllSensorReadings();
    foreach (var reading in readings)
    {
        Console.WriteLine($"{reading.SensorName}: {reading.Value} {reading.Unit}");
    }
    reader.Disconnect();
}
```

**Integration with Dial Monitoring:**
```
> sensors
[Lists all HWInfo64 sensors]
> monitor 290063... "CPU Package" "Temperature"
[Dial shows CPU temperature in real-time]
[Press key to exit]
```

**Prerequisites for HWInfo64 Integration:**
1. HWInfo64 running in "Sensors only" mode
2. "Shared Memory Support" enabled in Options
3. .NET 8.0+ runtime

**Learn More:** See [`VUWare.HWInfo64/README.md`](VUWare.HWInfo64/README.md) and [`VUWare.Console/HWINFO64_INTEGRATION.md`](VUWare.Console/HWINFO64_INTEGRATION.md)

---

## ??? Display Image Format

The VU1 dials feature e-paper displays with the following specifications:

| Property | Value |
|----------|-------|
| **Resolution** | 200 × 144 pixels |
| **Color Depth** | 1-bit monochrome (black/white) |
| **Buffer Size** | 3600 bytes (exactly) |
| **Packing** | Vertical: 8 pixels per byte (MSB = top pixel) |
| **Threshold** | Gray > 127 ? white, ? 127 ? black |
| **Input Formats** | PNG, BMP, JPEG (auto-converted) |

**Image Upload:**
```bash
> image <uid> ./path/to/image.png
```

Images are automatically:
1. Loaded from PNG/BMP/JPEG
2. Aspect-fitted to 200×144 (white letterbox if needed)
3. Converted to grayscale
4. Thresholded and vertically packed
5. Transmitted in 4 chunks (1000+1000+1000+600 bytes)

Reference images are included in `etc/image_pack/` and ready to use.

---

## ? Performance Characteristics

| Operation | Typical Time |
|-----------|--------------|
| Auto-detection | 1–3 seconds |
| Discovery (1 dial) | 1–2 seconds |
| Set dial position | ~50–100 ms |
| Set backlight color | ~50–100 ms |
| Image upload (3600 bytes) | 2–3 seconds |
| Sensor poll (HWInfo64) | Configurable (default 500 ms) |

---

## ?? System Architecture

```
?? VUWare.Lib ???????????????????????????????????????
?                                                    ?
?  VU1Controller (Main API)                         ?
?  ??? SerialPortManager                            ?
?  ?   ??? USB communication (VID:0403, PID:6015)   ?
?  ??? DeviceManager                                ?
?  ?   ??? I2C bus scanning                         ?
?  ?   ??? Device provisioning                      ?
?  ?   ??? UID-based identification                 ?
?  ??? ImageProcessor                               ?
?  ?   ??? 1-bit image conversion (200×144)         ?
?  ??? CommandBuilder                               ?
?      ??? 30+ device commands                      ?
?                                                    ?
??????????????????????????????????????????????????????
              ?                           ?
    ???????????????????        ????????????????????
    ? VUWare.Console  ?        ? VUWare.HWInfo64  ?
    ???????????????????        ????????????????????
    ? CLI Application ?        ? Sensor Reader    ?
    ? 12+ Commands    ?        ? Event-Driven     ?
    ? Logging System  ?        ? Auto-Polling     ?
    ???????????????????        ????????????????????
```

---

## ??? Building & Development

### Prerequisites
- .NET 8.0 SDK or later
- Windows, macOS, or Linux
- Git

### Build from Source
```bash
# Clone repository
git clone https://github.com/uweinside/VUWare.git
cd VUWare

# Build all projects
dotnet build

# Run tests (if available)
dotnet test

# Build release binaries
dotnet build --configuration Release
```

### Project Structure
```
VUWare/
??? VUWare.Lib/                  # Core control library
?   ??? VU1Controller.cs         # Main API
?   ??? SerialPortManager.cs     # USB communication
?   ??? DeviceManager.cs         # Device discovery
?   ??? CommandBuilder.cs        # Command encoding
?   ??? ImageProcessor.cs        # Image handling
?   ??? ImageUpdateQueue.cs      # Async queue
?   ??? doc/                      # Documentation
??? VUWare.Console/              # CLI application
?   ??? Program.cs               # Interactive CLI
?   ??? QUICK_START.md           # Getting started
?   ??? HWINFO64_INTEGRATION.md  # Sensor guide
??? VUWare.HWInfo64/             # Sensor integration
?   ??? HWiNFOReader.cs          # Low-level reader
?   ??? HWInfo64Controller.cs    # High-level API
?   ??? SensorModels.cs          # Data structures
?   ??? README.md                # Library docs
??? README.md                     # This file
```

---

## ?? Documentation

| Document | Purpose |
|----------|---------|
| [`VUWare.Lib/README.md`](VUWare.Lib/README.md) | Library architecture and API |
| [`VUWare.Lib/doc/SERIAL_PROTOCOL.md`](VUWare.Lib/doc/SERIAL_PROTOCOL.md) | Serial protocol specification |
| [`VUWare.Lib/doc/QUICK_REFERENCE.md`](VUWare.Lib/doc/QUICK_REFERENCE.md) | Quick reference guide |
| [`VUWare.Console/README.md`](VUWare.Console/README.md) | CLI application manual |
| [`VUWare.Console/QUICK_START.md`](VUWare.Console/QUICK_START.md) | CLI getting started |
| [`VUWare.Console/HWINFO64_INTEGRATION.md`](VUWare.Console/HWINFO64_INTEGRATION.md) | Sensor monitoring guide |
| [`VUWare.HWInfo64/README.md`](VUWare.HWInfo64/README.md) | HWInfo64 library documentation |

---

## ?? Troubleshooting

### Connection Issues
- **Hub not detected:** Verify USB cable connection, check Device Manager for USB device
- **COM port auto-detect fails:** Try `connect COM3` with specific port number
- **Permission denied:** Run as Administrator on Windows

### Dial Control Issues
- **Dial not responding:** Run `init` to rediscover, check I2C cables
- **Position updates slow:** Check USB hub quality, reduce poll interval
- **Backlight not changing:** Verify dial is initialized, check power

### Image Upload Issues
- **Upload fails:** Ensure image converts to exactly 3600 bytes (200×144 1-bit)
- **Image not displaying:** Try a test pattern: `image <uid> ./etc/image_pack/blank.png`
- **Wrong aspect ratio:** Images are auto-fitted with white letterboxing

### HWInfo64 Integration
- **Sensors not found:** Ensure HWInfo64 is running in "Sensors only" mode with Shared Memory Support enabled
- **Wrong sensor names:** Use `sensors` command to list available sensor and entry names
- **Dial not updating:** Verify sensor/entry names match exactly (case-insensitive matching available)

---

## ?? Use Cases

### 1. System Monitoring Dashboard
Use VU1 dials to display real-time CPU temperature, GPU usage, fan speed:
```csharp
// Connect HWInfo64 sensors to dials via the library
var controller = new HWInfo64Controller();
controller.RegisterDialMapping(new DialSensorMapping
{
    Id = "cpu-temp",
    SensorName = "CPU Package",
    EntryName = "Temperature",
    MinValue = 20,
    MaxValue = 100
});
```

### 2. Custom Gauge Display
Create a custom e-paper display image and upload to dials:
```
> image <uid> ./custom-gauge.png
```

### 3. Automated Testing
Run the automated test suite to verify all dials:
```
> test
```

### 4. Programmatic Control
Integrate VUWare.Lib into your own C# applications:
```csharp
var controller = new VU1Controller();
if (controller.AutoDetectAndConnect() && await controller.InitializeAsync())
{
    // Full programmatic control
}
```

---

## ?? Contributing

Contributions are welcome! This project is based on the official VU1 code by Saša Karanovi?.

- **Bug reports:** Open an issue on GitHub
- **Feature requests:** Discuss in GitHub Discussions
- **Pull requests:** Fork, create a feature branch, and submit a PR

---

## ?? License

VUWare is part of an open-source ecosystem for Streacom VU1 dials. See the repository for specific license details.

**Credits:**
- Original VU1 implementation: Saša Karanovi?
- VUWare .NET implementation: Uwe Baumann
- Based on: [Official VU1 code](https://github.com/SasaKaranovic/VU-Server)

---

## ?? Resources

- **VU Dials Official:** [vudials.com](https://vudials.com)
- **GitHub Repository:** [github.com/uweinside/VUWare](https://github.com/uweinside/VUWare)
- **Original VU-Server:** [github.com/SasaKaranovic/VU-Server](https://github.com/SasaKaranovic/VU-Server)
- **HWInfo64:** [hwinfo.com](https://www.hwinfo.com/)

---

## ? FAQ

**Q: Can I use this without a VU1 Hub?**  
A: No, you need the VU1 Gauge Hub connected via USB.

**Q: Do I need to pre-convert images?**  
A: No, the console app handles PNG/BMP/JPEG conversion automatically.

**Q: Can I run multiple dials?**  
A: Yes, up to 100 dials can be connected to a single hub.

**Q: What .NET versions are supported?**  
A: .NET 8.0 or later.

**Q: Is HWInfo64 required?**  
A: Only if you want to use the sensor monitoring feature. The core library works independently.

**Q: Can I build custom applications?**  
A: Yes! VUWare.Lib is a fully documented library designed for integration into your projects.

---

## ?? Support

- **Issues:** Report bugs on [GitHub Issues](https://github.com/uweinside/VUWare/issues)
- **Discussions:** Ask questions on [GitHub Discussions](https://github.com/uweinside/VUWare/discussions)
- **Documentation:** Check project READMEs for detailed guides

---

## ?? Getting Started Now

Ready to get started? Follow these steps:

1. **Clone & Build:**
   ```bash
   git clone https://github.com/uweinside/VUWare.git
   cd VUWare
   dotnet build
   ```

2. **Run the Console App:**
   ```bash
   dotnet run --project VUWare.Console
   ```

3. **Connect & Initialize:**
   ```
   > connect
   > init
   > dials
   ```

4. **Control Your Dials:**
   ```
   > set <uid> 75
   > color <uid> red
   ```

Happy dialing! ???
