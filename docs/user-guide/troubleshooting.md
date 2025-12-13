# Troubleshooting

This guide helps you resolve common issues with VUWare.

## Installation Issues

### Windows SmartScreen Warning

**Problem**: "Windows protected your PC" message when running installer

**Cause**: VUWare installer is not code-signed

**Solution**:
1. Click "More info"
2. Click "Run anyway"
3. This is normal for unsigned applications

---

### Installer Won't Run

**Problem**: Installer doesn't start or shows error

**Solutions**:

- Ensure you have administrator privileges
- Right-click installer → "Run as administrator"
- Verify Windows version (requires Windows 10 1809+)
- Check that .NET 8.0 Runtime is installed

**Check .NET Runtime**:
```powershell
dotnet --list-runtimes
```

Look for: `Microsoft.WindowsDesktop.App 8.0.x`

If not found, download from: https://dotnet.microsoft.com/download/dotnet/8.0

---

### Installation Fails with Error

**Problem**: Installation completes with errors

**Solutions**:

- Close any running instances of VUWare
- Temporarily disable antivirus software
- Clear temp folder: `%TEMP%`
- Run installer with verbose logging:
  ```
  VUWare-Setup-x.x.x.exe /LOG="install.log"
  ```

---

## Hardware Detection Issues

### VU1 Hub Not Detected

**Problem**: Status shows "Cannot find VU1 Hub" or stays yellow on "Connecting Dials"

**Diagnostic Steps**:

1. **Check Device Manager**:
    - Open Device Manager
    - Expand "Ports (COM & LPT)"
    - Look for "USB-SERIAL CH340" or similar device
    - Note the COM port (e.g., COM3)

2. **If not listed**:
    - Try a different USB port
    - Use a different USB cable (must be data cable, not charge-only)
    - Power cycle the hub (unplug and replug)

3. **If listed with yellow warning**:
    - Driver is missing or corrupted
    - Download CH340 driver from hub manufacturer
    - Install driver and restart computer

4. **If listed but VUWare can't connect**:
    - Another application may be using the port
    - Close other serial terminal applications
    - Restart VUWare

**Advanced**: Check COM port manually
```powershell
# List all COM ports
mode
```

---

### Dials Not Detected

**Problem**: "0 dials detected" or some dials missing

**Diagnostic Steps**:

1. **Check Power**:
    - Verify each dial's LED is lit
    - If LEDs are off, check power connections
    - Ensure hub is providing power

2. **Check I2C Connections**:
    - Verify I2C cables are firmly seated
    - Check for damaged cables
    - Try different I2C ports on the hub

3. **Power Cycle**:
    - Unplug VU1 Hub from USB
    - Wait 10 seconds
    - Plug hub back in
    - Restart VUWare

4. **Test Individual Dials**:
    - Disconnect all but one dial
    - If detected, reconnect dials one at a time
    - Identify which dial is causing issues

**I2C Address Conflicts**:

If you have multiple dials with the same I2C address:
- Each dial must have a unique I2C address
- Configure addresses using dial manufacturer's tool
- Default addresses: usually 0x38, 0x39, 0x3A, 0x3B

---

## HWInfo64 Integration Issues

### Sensors Not Available

**Problem**: "Cannot connect to HWInfo64" or "No sensors found"

**Diagnostic Steps**:

1. **Verify HWInfo64 is Running**:
    - Check system tray for HWInfo64 icon
    - If not running, launch HWInfo64

2. **Enable Shared Memory**:
    - Open HWInfo64
    - Click Settings (gear icon)
    - Check "Shared Memory Support"
    - Click OK
    - **Important**: Restart HWInfo64

3. **Verify Shared Memory is Active**:
    - Open HWInfo64 Settings
    - Confirm "Shared Memory Support" is checked
    - Check that restart occurred after enabling

4. **Run as Administrator**:
    - Close HWInfo64
    - Right-click HWInfo64 → "Run as administrator"
    - Close VUWare
    - Right-click VUWare → "Run as administrator"

!!! warning "Restart Required"
    HWInfo64 MUST be restarted after enabling Shared Memory Support. Simply closing and reopening is not enough - use File → Exit and relaunch.

---

### Sensor Names Changed

**Problem**: "Sensor not found" after HWInfo64 or driver update

**Cause**: HWInfo64 may change sensor names after updates or hardware changes

**Solution**:

1. Open VUWare Settings
2. For each affected dial:
    - Click "Browse Sensors"
    - Find the sensor (may have slightly different name)
    - Select the correct reading
    - Click OK
3. Click Apply to save

**Prevention**:
- Note your sensor names before updating HWInfo64
- Export configuration as backup before major updates

---

### Specific Sensor Not Showing

**Problem**: A particular sensor is missing from the sensor browser

**Causes & Solutions**:

**Sensor disabled in HWInfo64**:
- Open HWInfo64 sensor window
- Right-click the sensor
- Ensure it's not hidden
- Check "Show in Sensors"

**Polling disabled**:
- Some sensors require polling to be enabled
- HWInfo64 Settings → check polling options

**Hardware not detected**:
- Verify hardware is properly installed
- Check Device Manager for hardware issues
- Update drivers if needed

---

## Monitoring Issues

### Dials Not Updating

**Problem**: Dials show 0% or stuck at one value

**Diagnostic Steps**:

1. **Check Status Button**:
    - If Red: Error occurred (hover for details)
    - If Yellow: Still initializing
    - If Gray: Monitoring not started

2. **Verify HWInfo64**:
    - Ensure HWInfo64 is still running
    - Check sensor is updating in HWInfo64 window

3. **Check Configuration**:
    - Open Settings
    - Verify sensor names are exact match
    - Click "Browse Sensors" to confirm sensor exists

4. **Restart Monitoring**:
    - Click Settings
    - Click Apply (forces reload)
    - Or restart VUWare completely

**Dial Updates But Value is Wrong**:

- Check min/max values in configuration
- Verify threshold values are appropriate
- Compare sensor value in HWInfo64 to dial percentage

---

### Colors Not Changing

**Problem**: Dial updates but stays same color

**Diagnostic Steps**:

1. **Verify Thresholds**:
    - Open Settings
    - Check warning and critical thresholds
    - Ensure sensor value actually reaches thresholds

2. **Check Sensor Range**:
    - Verify min/max values are appropriate
    - Sensor value might be outside configured range

3. **Test Threshold Logic**:
    ```
    Example:
    Min: 20, Max: 95, Warning: 75, Critical: 88
    
    Value 60°C = 53% -> Green (below warning)
    Value 80°C = 80% -> Orange (above warning)
    Value 90°C = 93% -> Red (above critical)
    ```

4. **Confirm Color Settings**:
    - Open Settings → Dial Configuration
    - Verify colors are different for Normal/Warning/Critical
    - Try more distinct colors (e.g., Green/Yellow/Red)

---

### High CPU Usage

**Problem**: VUWare uses excessive CPU

**Causes & Solutions**:

**Polling interval too fast**:
- Open Settings
- Increase "Global Update Interval" to 1000ms or higher
- Lower intervals (100-500ms) use more CPU

**Too many sensors**:
- HWInfo64 polling can be intensive
- Monitor only essential sensors
- Disable unused dials

**Background tasks**:
- Check Task Manager for other processes
- Ensure antivirus isn't scanning VUWare continuously

---

### Application Crashes

**Problem**: VUWare closes unexpectedly

**Diagnostic Steps**:

1. **Check Windows Event Viewer**:
    - Open Event Viewer
    - Navigate to Windows Logs → Application
    - Look for VUWare.App errors
    - Note error details

2. **Run in Debug Mode**:
    - If built from source, run in debug mode
    - Check debug output for errors

3. **Verify Configuration**:
    - Configuration file might be corrupted
    - Rename `dials-config.json` to `.old`
    - Restart VUWare (will recreate default config)
    - Reconfigure dials

4. **Reinstall**:
    - Uninstall VUWare
    - Backup configuration file
    - Reinstall latest version
    - Restore configuration

---

## Permission Issues

### Access Denied Errors

**Problem**: "Access denied" or permission errors

**Solutions**:

1. **Run as Administrator**:
    - Right-click VUWare shortcut
    - Select "Run as administrator"

2. **Set Permanent Admin Rights**:
    - Right-click VUWare.App.exe
    - Properties → Compatibility
    - Check "Run this program as an administrator"
    - Click OK

3. **Check Configuration File Permissions**:
    - Navigate to: `C:\Program Files\VUWare\Config`
    - Right-click `dials-config.json` → Properties
    - Security → ensure your user has Write permission

---

## Configuration Issues

### Configuration Not Saving

**Problem**: Changes lost after closing Settings

**Causes & Solutions**:

**Validation errors**:
- Check for error messages in Settings window
- Fix all validation errors before saving
- OK/Apply buttons are disabled when errors exist

**File permission issues**:
- See [Access Denied Errors](#access-denied-errors) above

**Antivirus blocking**:
- Temporarily disable antivirus
- Add VUWare folder to antivirus exclusions
- Retry saving configuration

---

### Configuration File Corrupted

**Problem**: "Invalid configuration" error on startup

**Solution - Reset Configuration**:

1. Navigate to: `C:\Program Files\VUWare\Config`
2. Rename `dials-config.json` to `dials-config.old`
3. Restart VUWare
4. VUWare creates new default configuration
5. Reconfigure dials

**Recovery from Backup**:

If you have a backup:
1. Copy backup to `C:\Program Files\VUWare\Config\dials-config.json`
2. Restart VUWare

---

## Performance Issues

### Slow Updates

**Problem**: Dials update slowly or lag behind sensor values

**Solutions**:

**Increase polling frequency**:
- Open Settings
- Decrease "Global Update Interval" (e.g., 500ms)
- Note: Lower values use more CPU

**Serial communication delay**:
- Serial updates take 100-200ms per dial
- This is normal for the hardware
- Cannot be significantly improved

**HWInfo64 performance**:
- HWInfo64 polling settings affect sensor update rate
- HWInfo64 Settings → adjust polling intervals

---

### Memory Leaks

**Problem**: VUWare memory usage grows over time

**Diagnostic**:

1. Monitor VUWare in Task Manager
2. If memory grows continuously without stopping:
    - This may be a bug
    - Report issue with details

**Temporary Fix**:
- Restart VUWare periodically
- Use Task Scheduler to auto-restart daily

---

## Reporting Issues

When reporting bugs or asking for help, please include:

### Required Information

- **VUWare version**: Check About dialog or installer version
- **Windows version**: Run `winver` to check
- **HWInfo64 version**: Check HWInfo64 About dialog
- **Hardware details**:
    - VU1 Hub model
    - Number of dials connected
    - PC hardware (CPU, GPU, motherboard)

### Reproduction Steps

1. Describe exactly what you were doing
2. Include configuration details if relevant
3. Note error messages (exact text)
4. Specify when the issue occurs (startup, monitoring, configuration, etc.)

### Logs and Files

If possible, attach:
- Configuration file: `C:\Program Files\VUWare\Config\dials-config.json`
- Windows Event Viewer logs (Application)
- Screenshots of error messages

### Where to Report

- GitHub Issues: https://github.com/uweinside/VUWare/issues
- Include all information above
- Search existing issues first to avoid duplicates

---

## Additional Resources

- **[Installation Guide](../getting-started/installation.md)** - Reinstallation steps
- **[Configuration Guide](../getting-started/configuration.md)** - Reconfiguration help
- **[Use Cases](use-cases.md)** - Example configurations
- **[GitHub Issues](https://github.com/uweinside/VUWare/issues)** - Known issues and bug reports
