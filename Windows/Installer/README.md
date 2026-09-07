# VUWare Installer

This directory contains the Inno Setup project for creating a Windows installer for VUWare.App.

## Prerequisites

### For Building the Installer

1. **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Inno Setup 6.2+** - [Download](https://jrsoftware.org/isinfo.php)

### For End Users (Installed Application)

- Windows 10 version 1809 or later (64-bit)
- VU1 Gauge Hub connected via USB
- HWInfo64 with "Shared Memory Support" enabled

## Building the Installer

### Quick Build (Recommended)

Run the PowerShell build script from this directory:

```powershell
.\build-installer.ps1
```

This will:
1. Publish VUWare.App as a self-contained x64 application
2. Compile the Inno Setup script
3. Output the installer to the `Output` folder

### Build Options

```powershell
# Build with custom version
.\build-installer.ps1 -Version "1.2.0"

# Skip dotnet publish (use existing build)
.\build-installer.ps1 -SkipPublish

# Specify custom Inno Setup path
.\build-installer.ps1 -InnoSetupPath "C:\Tools\InnoSetup\ISCC.exe"

# Show detailed output
.\build-installer.ps1 -Verbose
```

### Manual Build

1. **Publish the application:**
   ```powershell
   cd ..
   dotnet publish VUWare.App -c Release -r win-x64 --self-contained
   ```

2. **Compile the installer:**
   - Open `VUWare.iss` in Inno Setup Compiler
   - Press F9 or click Build > Compile
   - Or use command line:
     ```powershell
     & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" VUWare.iss
     ```

## Installer Features

### Installation Options

- **Default installation directory:** `C:\Program Files\VUWare`
- **Start Menu shortcuts:** Created automatically
- **Desktop shortcut:** Optional (user selectable)
- **Elevated autostart:** Optional - creates a scheduled task to start VUWare with admin privileges at Windows logon

### What Gets Installed

```
C:\Program Files\VUWare\
??? VUWare.App.exe           # Main application
??? VUWare.App.dll           # Application library
??? VUWare.Lib.dll           # VU1 controller library
??? VUWare.HWInfo64.dll      # HWInfo64 integration
??? VU1_Icon.ico             # Application icon
??? Config\
?   ??? dials-config.json    # Configuration (preserved on upgrade)
??? [.NET runtime files]     # Self-contained runtime
```

### Uninstallation

- Available through Windows Settings > Apps or Control Panel
- Configuration files in the `Config` folder are preserved (user data)
- Scheduled task for autostart is automatically removed

## Customization

### Changing Application Metadata

Edit the `#define` directives at the top of `VUWare.iss`:

```inno
#define MyAppName "VUWare"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Uwe Baumann"
#define MyAppURL "https://github.com/uweinside/VUWare"
```

### Adding a License Agreement

1. Uncomment the `LicenseFile` line in `VUWare.iss`:
   ```inno
   LicenseFile=..\LICENSE
   ```

2. Ensure you have a LICENSE file in the repository root

### Adding Pre-Installation Information

1. Uncomment the `InfoBeforeFile` line:
   ```inno
   InfoBeforeFile=..\README.md
   ```

### Changing the Application GUID

If you fork this project, generate a new GUID for `AppId`:

```powershell
[guid]::NewGuid().ToString().ToUpper()
```

Replace the GUID in this line:
```inno
AppId={{8A7E5C3D-4B2F-4E1A-9D8C-7F6E5A4B3C2D}
```

## Elevated Autostart

The installer includes an option to start VUWare automatically at Windows logon with elevated (administrator) permissions. This is useful because:

- Some sensor access may require elevated privileges
- Serial port access to VU1 Hub may benefit from elevation

This feature uses Windows Task Scheduler instead of the registry Run key, which allows the application to start with elevated privileges without triggering a UAC prompt each time.

### Manual Task Management

If you need to manage the scheduled task manually:

```powershell
# View the task
schtasks /Query /TN "VUWare Autostart"

# Delete the task
schtasks /Delete /TN "VUWare Autostart" /F

# Enable/disable the task
schtasks /Change /TN "VUWare Autostart" /Enable
schtasks /Change /TN "VUWare Autostart" /Disable
```

## Troubleshooting

### Build Errors

**"Inno Setup Compiler not found"**
- Install Inno Setup from https://jrsoftware.org/isinfo.php
- Or specify the path: `.\build-installer.ps1 -InnoSetupPath "C:\Path\To\ISCC.exe"`

**"Publish output not found"**
- Run the build script without `-SkipPublish`
- Or manually run: `dotnet publish VUWare.App -c Release -r win-x64 --self-contained`

**"dotnet publish failed"**
- Ensure .NET 8.0 SDK is installed
- Check that all project references are resolved

### Installation Issues

**"Windows protected your PC"**
- This is Windows SmartScreen warning for unsigned executables
- Click "More info" then "Run anyway"
- Consider code signing for production releases

**Application doesn't start after installation**
- Check that HWInfo64 is running
- Verify VU1 Hub is connected
- Check Windows Event Viewer for errors

## File Structure

```
Installer/
??? VUWare.iss              # Main Inno Setup script
??? build-installer.ps1     # PowerShell build automation
??? README.md               # This file
??? Output/                 # Generated installers (created by build)
    ??? VUWare-Setup-x.x.x.exe
```

## Code Signing (Optional)

For production releases, consider signing the installer:

1. Obtain a code signing certificate
2. Add SignTool configuration to `VUWare.iss`:
   ```inno
   [Setup]
   SignTool=signtool sign /f "certificate.pfx" /p "password" /t http://timestamp.digicert.com $f
   SignedUninstaller=yes
   ```

## Support

For issues with the installer:
1. Check this README for common solutions
2. Review the Inno Setup documentation: https://jrsoftware.org/ishelp/
3. Report issues at: https://github.com/uweinside/VUWare/issues
