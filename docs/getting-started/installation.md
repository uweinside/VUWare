# Installation

This guide will help you install VUWare on your Windows PC.

## Prerequisites

Before installing VUWare, ensure you have the following:

### Hardware Requirements

!!! info "Required Hardware"
    - **VU1 Gauge Hub** connected via USB
    - **VU1 Analog Dials** (1 to 4 dials) connected to the hub via I2C
    - **Windows PC** running Windows 10 version 1809 or later (64-bit)

### Software Requirements

!!! warning "Important: HWInfo64 Setup"
    VUWare requires HWInfo64 to be installed and configured before first use.

#### HWInfo64 Installation

1. Download HWInfo64 from [https://www.hwinfo.com/](https://www.hwinfo.com/)
2. Install HWInfo64 on your system
3. Launch HWInfo64
4. Click the **Settings** button (gear icon)
5. Check **"Shared Memory Support"**
6. Restart HWInfo64

!!! tip "Keep HWInfo64 Running"
    HWInfo64 must be running whenever you use VUWare. Consider adding it to your Windows startup programs.

#### USB Serial Driver

The VU1 Hub requires a USB serial driver to communicate with your PC.

- **Automatic**: Windows usually installs this driver automatically
- **Manual**: If the hub is not detected, download and install the FTDI VCP drivers

**Download FTDI VCP Drivers**: [https://ftdichip.com/drivers/vcp-drivers/](https://ftdichip.com/drivers/vcp-drivers/)

To verify the driver is installed:

1. Open **Device Manager**
2. Expand **Ports (COM & LPT)**
3. Look for a device labeled similar to "USB Serial Port" or "USB Serial Converter"

If you had to install the driver manually:
1. Restart your computer after installation
2. Reconnect the VU1 Hub
3. Verify the COM port appears in Device Manager

## Installation Methods

### Option 1: Using the Installer (Recommended)

This is the easiest method for most users.

1. **Download the Installer**
    - Go to the [VUWare Releases](https://github.com/uweinside/VUWare/releases) page
    - Download the latest `VUWare-Setup-x.x.x.exe` file

2. **Run the Installer**
    - Double-click the downloaded installer
    - If Windows SmartScreen appears, click "More info" then "Run anyway"

3. **Follow the Installation Wizard**
    - Click **Next** through the welcome screen
    - Choose installation location (default: `C:\Program Files\VUWare`)
    - Select additional options:
        - [x] Create Desktop shortcut (optional)
        - [x] Start VUWare automatically when Windows starts (optional)

4. **Complete Installation**
    - Click **Install** to begin
    - Wait for the installation to complete
    - Optionally launch VUWare immediately

!!! success "Installation Complete"
    VUWare is now installed and ready to use. Find it in your Start Menu under "VUWare".

### Option 2: Building from Source

For developers or advanced users who want to build from source.

!!! note "Prerequisites for Building"
    - .NET 8.0 SDK installed
    - Git (for cloning the repository)
    - Visual Studio 2022 or VS Code (optional, for IDE support)

**Steps:**

1. **Clone the Repository**
    ```bash
    git clone https://github.com/uweinside/VUWare.git
    cd VUWare
    ```

2. **Build the Project**
    ```bash
    dotnet build VUWare.App -c Release
    ```

3. **Run the Application**
    ```bash
    dotnet run --project VUWare.App
    ```

Alternatively, open `VUWare.sln` in Visual Studio and build/run from there.

## Post-Installation

### First Launch Checklist

Before launching VUWare for the first time:

- [x] VU1 Hub is connected via USB and powered on
- [x] VU1 dials are connected to the hub via I2C cables
- [x] HWInfo64 is installed and running
- [x] "Shared Memory Support" is enabled in HWInfo64
- [x] Hub driver is installed (check Device Manager)

### Next Steps

Ready to launch? Proceed to the [First Launch](first-launch.md) guide to set up VUWare.

## Troubleshooting Installation

### Windows SmartScreen Warning

**Problem**: "Windows protected your PC" message appears

**Solution**: This is normal for unsigned applications. Click "More info" then "Run anyway".

### Installer Fails to Start

**Problem**: Installer won't run or shows an error

**Solutions**:
- Ensure you have administrator privileges
- Right-click the installer and select "Run as administrator"
- Verify your Windows version is compatible (Windows 10 1809+)
- Check that .NET 8.0 Runtime is installed

### Device Not Detected

**Problem**: VU1 Hub not appearing in Device Manager

**Solutions**:
- Try a different USB port
- Use a USB data cable (not charge-only)
- Install CH340 driver manually from manufacturer
- Power cycle the hub

### HWInfo64 Not Working

**Problem**: VUWare can't connect to HWInfo64

**Solutions**:
- Verify HWInfo64 is running
- Ensure "Shared Memory Support" is enabled
- Restart HWInfo64 after enabling shared memory
- Run both HWInfo64 and VUWare as administrator

## Uninstallation

To remove VUWare from your system:

1. Open **Settings** > **Apps** > **Apps & features**
2. Find **VUWare** in the list
3. Click **Uninstall**
4. Follow the uninstallation wizard

!!! info "Configuration Preserved"
    Your configuration file (`dials-config.json`) is preserved during uninstallation in case you reinstall later.

## Upgrading

To upgrade to a newer version of VUWare:

1. Download the latest installer
2. Run the new installer (it will detect the existing installation)
3. Follow the upgrade wizard
4. Your configuration will be preserved automatically

!!! tip "Backup Your Config"
    Before upgrading, you can backup your configuration from:
    ```
    C:\Program Files\VUWare\Config\dials-config.json
    ```
