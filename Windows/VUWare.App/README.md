# VUWare.App - VU1 Gauge Hub Monitor

VUWare.App is a Windows desktop application that displays real-time system monitoring data from HWInfo64 on VU1 Gauge Hub analog dials. Monitor CPU temperature, GPU load, fan speeds, and any other sensor with beautiful analog gauges that change color based on your configured thresholds.

## Features

### Real-Time Hardware Monitoring
- Display any HWInfo64 sensor on physical VU1 analog dials
- Monitor up to 4 sensors simultaneously (one per dial)
- Automatic color changes based on configurable warning and critical thresholds
- Live updates with configurable polling intervals (default: 1 second)

### Easy Configuration
- Graphical settings interface for configuring dials
- Browse and select from all available HWInfo64 sensors
- Set custom minimum/maximum ranges for each dial
- Configure warning and critical threshold values
- Choose colors for normal, warning, and critical states
- Upload custom dial face images for personalization

### First-Run Setup Wizard
- Automatic detection of connected VU1 Gauge Hub
- Discovery of all connected dials via I2C
- Guided configuration for first-time users
- HWInfo64 sensor browser for easy sensor selection

### Status Monitoring
- Visual status indicators for system state
- Real-time dial percentage display with color coding
- Detailed tooltips showing sensor values and update information
- Error reporting and diagnostics

## Prerequisites

### Hardware Requirements
- **VU1 Gauge Hub** - Connected via USB
- **VU1 Analog Dials** - 1 to 4 dials connected to the hub via I2C
- **Windows PC** - Windows 10 version 1809 or later (64-bit)

### Software Requirements
- **HWInfo64** - Must be installed and running
  - Download from: https://www.hwinfo.com/
  - Enable "Shared Memory Support" in HWInfo64 settings:
    1. Open HWInfo64
    2. Click Settings (gear icon)
    3. Check "Shared Memory Support"
    4. Restart HWInfo64

- **USB Serial Driver** - Required for VU1 Hub communication
  - Usually installed automatically by Windows
  - If not detected, install the CH340 driver from your hub manufacturer

## Installation

### Option 1: Using the Installer (Recommended)
1. Download the latest `VUWare-Setup-x.x.x.exe` from the releases page
2. Run the installer
3. Follow the installation wizard
4. Optionally enable "Start VUWare automatically when Windows starts"
5. Launch VUWare from the Start Menu or Desktop shortcut

### Option 2: Building from Source
1. Clone the repository
2. Ensure .NET 8.0 SDK is installed
3. Build and run:
   ```
   dotnet run --project VUWare.App
   ```

## Quick Start Guide

### First Launch

When you first launch VUWare.App, the setup wizard will guide you through:

1. **Dial Detection** - The app automatically discovers your connected VU1 dials
2. **HWInfo64 Connection** - Connects to HWInfo64's shared memory
3. **Sensor Configuration** - Configure each dial with your preferred sensors

### Configuring a Dial

For each dial, you need to configure:

1. **Sensor Selection**
   - Click "Browse Sensors" to see all available HWInfo64 sensors
   - Select the sensor and reading you want to monitor
   - Example: "CPU [#0]: AMD Ryzen 7 9700X" > "CPU (Tctl/Tdie)"

2. **Value Range**
   - **Min Value**: The sensor value that represents 0% on the dial
   - **Max Value**: The sensor value that represents 100% on the dial
   - Example: CPU temp from 20°C (0%) to 95°C (100%)

3. **Thresholds**
   - **Warning Threshold**: Value at which the dial turns warning color (e.g., Orange at 75°C)
   - **Critical Threshold**: Value at which the dial turns critical color (e.g., Red at 88°C)

4. **Colors**
   - Choose colors for Normal, Warning, and Critical states
   - Available colors: White, Red, Green, Blue, Yellow, Cyan, Magenta, Orange, Purple, Pink

5. **Dial Face (Optional)**
   - Upload a custom dial face image (240x240 PNG recommended)
   - Or use the default VU1 dial face

### Running the Application

Once configured, VUWare.App runs in the background:

- **Status Button** (bottom) - Shows current state:
  - Gray: Idle
  - Yellow: Initializing
  - Green: Monitoring active
  - Red: Error state

- **Dial Buttons** (1-4) - Show each dial's current status:
  - Displays current percentage
  - Color-coded based on thresholds
  - Hover for detailed sensor information

### Accessing Settings

Click the **Settings** button to:
- Modify dial configurations
- Change sensor mappings
- Adjust thresholds and colors
- Upload new dial face images
- Change polling intervals

## Common Use Cases

### CPU Temperature Monitoring
- **Min Value**: 20°C (idle temperature)
- **Max Value**: 95°C (maximum safe temperature)
- **Warning**: 75°C (Orange)
- **Critical**: 88°C (Red)

### GPU Usage
- **Min Value**: 0% (idle)
- **Max Value**: 100% (full load)
- **Warning**: 80% (Orange)
- **Critical**: 95% (Red)

### Fan Speed (RPM)
- **Min Value**: 0 RPM
- **Max Value**: 3000 RPM (your fan's max speed)
- **Warning**: 2400 RPM (Orange - high speed)
- **Critical**: 2800 RPM (Red - very high)

### CPU/GPU Power Consumption
- **Min Value**: 0W
- **Max Value**: 200W (TDP limit)
- **Warning**: 150W (Orange)
- **Critical**: 180W (Red)

## Troubleshooting

### Application Won't Start
- Verify VU1 Hub is powered and connected via USB
- Check that the hub appears in Device Manager under Ports (COM & LPT)
- Ensure .NET 8.0 Runtime is installed

### Dials Not Detected
- Power cycle the VU1 Hub
- Check I2C cable connections between hub and dials
- Verify each dial powers on (LED indicators)

### Sensors Not Available
- Ensure HWInfo64 is running
- Enable "Shared Memory Support" in HWInfo64 settings
- Restart HWInfo64 after enabling shared memory

### Dials Not Updating
- Check HWInfo64 is still running
- Verify sensor names match exactly (sensor names may change after hardware/driver updates)
- Look at the Status button for error messages
- Open Settings and click "Browse Sensors" to verify current sensor names

### Colors Not Changing
- Verify threshold values are correct
- Ensure sensor values are actually reaching the thresholds
- Check that min/max values are appropriate for your sensor range

### Permission Errors
- Run VUWare.App as Administrator if needed
- Some sensor access may require elevated privileges

## Configuration File

VUWare.App stores its configuration in:
```
C:\Program Files\VUWare\Config\dials-config.json
```

This file is preserved during updates and contains:
- Dial UID mappings
- Sensor selections and mappings
- Threshold values and colors
- Display preferences
- Polling intervals

**Note**: Manual editing is not recommended. Use the Settings interface to make changes.

## Support

### Getting Help
- Check this README for common solutions
- Review the troubleshooting section above
- Report issues at: https://github.com/uweinside/VUWare/issues

### Reporting Issues
When reporting issues, please include:
- VUWare version number
- Windows version
- HWInfo64 version
- Description of the problem
- Any error messages from the Status button
- Whether the issue occurs during initialization or monitoring

## License

VUWare.App is licensed under the MIT License. See LICENSE file for details.

Copyright (c) 2025 Uwe Baumann
