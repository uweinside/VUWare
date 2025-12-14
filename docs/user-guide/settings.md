# Settings Reference

This guide provides detailed information about all VUWare settings and preferences.

## Accessing Settings

Click the **Settings** button in the main VUWare window to open the configuration interface.

## General Settings

These settings apply to the entire application.

### Enable Polling

**Location**: General Settings section  
**Type**: Checkbox  
**Default**: Enabled

Controls whether VUWare actively monitors sensors and updates dials.

- **Checked**: Monitoring is active, dials update continuously
- **Unchecked**: Monitoring is paused, dials do not update

**Use Cases**:
- Disable temporarily to save system resources
- Pause monitoring while troubleshooting HWInfo64
- Stop updates while configuring dials

!!! info "Status Indicator"
    When polling is disabled, the Status button shows Gray (Idle).

---

### Global Update Interval

**Location**: General Settings section  
**Type**: Numeric input (milliseconds)  
**Default**: 1000ms (1 second)  
**Range**: 100ms to 10000ms

How often VUWare polls HWInfo64 sensors and updates dials.

**Lower values (100-500ms)**:
- Faster, more responsive updates
- Higher CPU usage
- More frequent serial communication
- Use for rapidly changing sensors

**Higher values (2000-5000ms)**:
- Slower, less responsive updates
- Lower CPU usage
- Less serial communication overhead
- Suitable for slow-changing sensors (temperatures)

**Recommended Settings**:

| Sensor Type | Interval | Reason |
|-------------|----------|--------|
| Temperature | 1000-2000ms | Changes slowly |
| Fan Speed | 1000-1500ms | Moderate changes |
| CPU/GPU Usage | 500-1000ms | Fast changes |
| Power | 500-1000ms | Moderate changes |
| Voltage | 250-500ms | Fast changes (OC monitoring) |

!!! tip "Performance vs Responsiveness"
    1000ms (1 second) is ideal for most scenarios. Only reduce for sensors that need immediate updates.

---

## Dial Configuration Settings

Each dial has its own configuration panel with the following settings:

### Display Name

**Type**: Text input  
**Required**: Yes  
**Max Length**: 50 characters

A friendly name that identifies what the dial monitors.

**Examples**:
- "CPU Temperature"
- "GPU Core Load"
- "CPU Fan RPM"
- "System Power"

**Where Used**:
- Dial status tooltips
- Settings window
- Debugging/logs

---

### Sensor Name

**Type**: Text input (selected via sensor browser)  
**Required**: Yes

The exact name of the hardware component from HWInfo64.

**Format**: `[Component Type] [#Index]: [Component Name]`

**Examples**:
- `CPU [#0]: AMD Ryzen 7 9700X: Enhanced`
- `GPU [#0]: NVIDIA GeForce RTX 4080`
- `Motherboard: ASRock X670E Taichi`

!!! warning "Exact Match Required"
    Sensor name must match HWInfo64 exactly, including spaces, brackets, and colons.

---

### Entry Name

**Type**: Text input (selected via sensor browser)  
**Required**: Yes

The specific sensor reading from the selected hardware component.

**Examples**:
- `CPU (Tctl/Tdie)` - CPU temperature
- `GPU Temperature` - Graphics card temperature
- `Total CPU Usage` - Overall CPU utilization
- `GPU Core Load` - Graphics processing load

!!! info "Finding Sensors"
    Use the "Browse Sensors" button to explore all available sensors and readings.

---

### Min Value

**Type**: Numeric input  
**Required**: Yes  
**Must be**: Less than Max Value

The sensor value that represents 0% on the dial.

**Guidelines**:
- For temperature: Use typical idle value (e.g., 20°C)
- For usage: Usually 0%
- For RPM: Usually 0 RPM
- For power: Usually 0W

**Effect on Dial**:
```
If sensor reads at or below Min Value:
  -> Dial shows 0%
```

---

### Max Value

**Type**: Numeric input  
**Required**: Yes  
**Must be**: Greater than Min Value

The sensor value that represents 100% on the dial.

**Guidelines**:
- For temperature: Use maximum safe value (e.g., 95°C)
- For usage: Usually 100%
- For RPM: Use fan's maximum rated speed
- For power: Use component's TDP

**Effect on Dial**:
```
If sensor reads at or above Max Value:
  -> Dial shows 100%
```

---

### Warning Threshold

**Type**: Numeric input  
**Required**: Yes  
**Must be**: Between Min and Max, less than Critical Threshold

The sensor value at which the dial changes to warning color (when using Threshold color mode).

**Guidelines**:
- Set to ~75-80% of maximum safe value
- Should indicate "getting warm" or "high load"
- Below critical but above normal operating range

**Effect on Dial** (in Threshold mode):
```
If sensor value >= Warning Threshold:
  -> Dial color changes to Warning Color
```

---

### Critical Threshold

**Type**: Numeric input  
**Required**: Yes  
**Must be**: Between Warning Threshold and Max Value

The sensor value at which the dial changes to critical color (when using Threshold color mode).

**Guidelines**:
- Set to ~90-95% of maximum safe value
- Should indicate "too hot" or "overloaded"
- Near but below absolute maximum

**Effect on Dial** (in Threshold mode):
```
If sensor value >= Critical Threshold:
  -> Dial color changes to Critical Color
```

---

### Color Mode

**Type**: Dropdown selection  
**Default**: Threshold  
**Options**: Threshold, Static, Off

Controls how the dial backlight color behaves.

**Threshold Mode** (default):
- Color changes based on sensor value and thresholds
- Below warning: Uses Normal Color
- Between warning and critical: Uses Warning Color
- Above critical: Uses Critical Color

**Static Mode**:
- Backlight always shows the same color regardless of sensor value
- Uses the "Static Color" setting
- Thresholds are ignored for color selection
- Useful for aesthetic purposes or to differentiate dials by purpose

**Off Mode**:
- Backlight is turned off
- Dial remains functional but without LED illumination
- Useful to save power or reduce distraction

!!! tip "When to Use Each Mode"
    - **Threshold**: Monitoring temperatures, usage, or any value where visual warnings are helpful
    - **Static**: Decorative purposes, or when you want consistent color per dial type (e.g., CPU=Blue, GPU=Red)
    - **Off**: Minimal distraction, power saving, or when only the needle position matters

---

### Color Configuration

The available color fields depend on the selected Color Mode.

#### Threshold Mode Colors

When Color Mode is set to "Threshold", configure these three colors:

**Normal Color**
- **Type**: Color dropdown  
- **Default**: Cyan
- Used when sensor is below warning threshold
- **Recommended**: Green, Blue, or Cyan

**Warning Color**
- **Type**: Color dropdown  
- **Default**: Yellow  
- Used when sensor is between warning and critical thresholds
- **Recommended**: Orange or Yellow

**Critical Color**
- **Type**: Color dropdown  
- **Default**: Red
- Used when sensor is at or above critical threshold
- **Recommended**: Red or Magenta

#### Static Mode Color

When Color Mode is set to "Static", configure:

**Static Color**
- **Type**: Color dropdown  
- **Default**: Cyan
- Backlight always shows this color regardless of sensor value
- Choose any available color

---

**Available Colors**:

| Color | Hex | Use Case |
|-------|-----|----------|
| White | #FFFFFF | Neutral, high visibility |
| Red | #FF0000 | Critical alerts, static red theme |
| Green | #00FF00 | Normal operation, static green theme |
| Blue | #0000FF | Cool/low temperature, static blue theme |
| Yellow | #FFFF00 | Moderate warning |
| Cyan | #00FFFF | Low usage/cool, default normal |
| Magenta | #FF00FF | Alternative alert |
| Orange | #FF8000 | Standard warning |
| Purple | #8000FF | Custom schemes |
| Pink | #FF0080 | Custom schemes |
| Off | #000000 | No light (when using Off color mode) |

!!! tip "Color Scheme Examples"
    **Threshold Mode - Traffic Light**:
    - Normal: Green → Warning: Orange → Critical: Red
    
    **Threshold Mode - Cool to Hot**:
    - Normal: Blue → Warning: Orange → Critical: Red
    
    **Threshold Mode - Calm to Alert**:
    - Normal: Cyan → Warning: Yellow → Critical: Magenta
    
    **Static Mode - Dial Purpose Identification**:
    - CPU Dial: Static Blue
    - GPU Dial: Static Red  
    - RAM Dial: Static Green
    - Fan Dial: Static Cyan

---

### Enabled

**Type**: Checkbox  
**Default**: Enabled

Controls whether this specific dial is active.

- **Checked**: Dial monitors sensor and updates
- **Unchecked**: Dial is disabled, shows 0%, no updates

**Use Cases**:
- Temporarily disable a dial without losing configuration
- Test configurations one dial at a time
- Disable unused dials to reduce overhead

---

### Update Interval Override

**Type**: Numeric input (milliseconds)  
**Default**: 0 (use global interval)  
**Range**: 0, or 100-10000ms

Overrides the global update interval for this specific dial.

**When to Use**:
- Set to 0 to use global interval (recommended)
- Set specific value for sensors with different update needs
- Example: Fast-changing voltage sensor at 250ms while temperatures use 1000ms

!!! info "Individual Override"
    If set to a non-zero value, this dial ignores the global update interval setting.

---

## Dial Face Image

### Upload Dial Image

**Location**: Each dial configuration panel  
**Button**: "Upload Dial Image"

Allows you to upload a custom image for the dial face.

**Supported Formats**:
- PNG (recommended, supports transparency)
- JPG/JPEG
- BMP

**Recommended Specifications**:
- Size: 240x240 pixels
- Format: PNG with transparent background
- Colors: High contrast for visibility
- Design: Include scale markings, numbers, etc.

**Upload Process**:

1. Click "Upload Dial Image"
2. Select image file
3. Image is automatically scaled to 240x240
4. Preview appears in settings
5. Click "Upload to Dial" to send to hardware

!!! warning "Upload Time"
    Uploading images takes several seconds. Do not interrupt the process or close the window during upload.

---

## Sensor Browser

### Browsing Sensors

Click "Browse Sensors" to open the sensor browser window.

**Structure**:
- Tree view showing all HWInfo64 sensors
- Grouped by hardware component
- Expandable nodes for each component's readings

**Navigation**:
1. Expand hardware component (e.g., CPU [#0])
2. Browse available readings
3. Select the specific reading you want
4. Click OK to apply

**Search/Filter**: (if implemented)
- Type to filter sensor names
- Helps find specific sensors quickly

---

## Action Buttons

### OK Button

**Function**: Save configuration and close window

**Behavior**:
1. Validates all configuration
2. Saves to `dials-config.json`
3. Reloads monitoring with new settings
4. Closes Settings window

**Disabled When**:
- Validation errors exist
- Fix all errors before clicking OK

---

### Apply Button

**Function**: Save configuration without closing

**Behavior**:
1. Validates all configuration
2. Saves to `dials-config.json`
3. Reloads monitoring with new settings
4. Settings window remains open

**Use Cases**:
- Test configuration changes
- Verify dial behavior before closing
- Iterative configuration adjustments

---

### Cancel Button

**Function**: Discard changes and close

**Behavior**:
1. All changes are discarded
2. Settings window closes
3. Previous configuration remains active

!!! warning "No Confirmation"
    Changes are discarded immediately without confirmation. Use Apply to test changes before committing.

---

## Configuration File

VUWare stores all settings in a JSON file.

**Location**: `C:\Program Files\VUWare\Config\dials-config.json`

**Contents**:
- Application settings (polling, intervals)
- Dial configurations (sensors, thresholds, colors)
- Hardware mappings (dial UIDs)

**Backup**:
```powershell
# Backup current configuration
copy "C:\Program Files\VUWare\Config\dials-config.json" "C:\Backup\dials-config-backup.json"

# Restore configuration
copy "C:\Backup\dials-config-backup.json" "C:\Program Files\VUWare\Config\dials-config.json"
```

!!! danger "Manual Editing"
    Manual editing of the configuration file is not recommended for most users. Always use the Settings interface to make changes when possible. See the Advanced Settings section below for JSON-only settings.

---

## Validation Rules

VUWare validates all settings before saving:

### General Validation

- Update interval must be between 100-10000ms
- All enabled dials must have valid configuration

### Dial Validation

- Display name is required
- Sensor name is required
- Entry name is required
- Min value < Max value
- Warning threshold >= Min value
- Critical threshold >= Warning threshold
- Critical threshold <= Max value
- Colors must be valid color names
- Color mode must be "threshold", "static", or "off"

**Error Display**:
- Red outline around invalid fields
- Error tooltip on hover
- OK/Apply buttons disabled until fixed

---

## Advanced Settings (JSON Configuration Only)

The following settings are available only by manually editing the `dials-config.json` file. These settings should not normally be changed unless you understand their purpose and implications.

!!! danger "Manual Editing Required"
    These settings are NOT available in the Settings UI. You must edit the JSON configuration file manually. Always backup your configuration before making changes.

**Configuration File Location**: `C:\Program Files\VUWare\Config\dials-config.json`

---

### Serial Command Delay

**JSON Field**: `serialCommandDelayMs`  
**Type**: Integer (milliseconds)  
**Default**: 150ms  
**Range**: 50-500ms

Delay between serial commands sent to the VU1 Hub during initialization and dial discovery.

**When to Modify**:
- If you experience communication errors during dial discovery
- Some USB-to-serial chipsets may require longer delays
- Lower values speed up initialization but may cause reliability issues
- Higher values increase initialization time but improve reliability

**Example**:
```json
"appSettings": {
  "serialCommandDelayMs": 200
}
```

!!! warning "Timing Sensitivity"
    The VU1 Hub firmware expects specific timing between commands. Values below 50ms may cause missed responses. Values above 500ms unnecessarily slow down initialization.

---

### Dial Count Override

**JSON Field**: `dialCountOverride`  
**Type**: Integer or null  
**Default**: null (use all detected dials)  
**Range**: 1-4

Forces VUWare to use a specific number of dials, ignoring additional detected dials.

**When to Use**:
- You have 4 dials but only want to use 2 or 3
- Testing configurations with fewer dials
- Temporarily disable a physical dial without unplugging it
- Troubleshooting issues with specific dials

**Example**:
```json
"appSettings": {
  "dialCountOverride": 2
}
```

!!! warning "Physical Dials Ignored"
    If set to 2, only the first 2 configured dials are used. Dials 3 and 4 are ignored even if physically connected and configured.

**To disable override**: Set to `null` (no quotes)
```json
"appSettings": {
  "dialCountOverride": null
}
```

---

### Start Minimized

**JSON Field**: `startMinimized`  
**Type**: Boolean  
**Default**: true

Controls whether VUWare starts minimized to the system tray.

**Values**:
- `true`: Application starts in system tray (default, recommended)
- `false`: Application window is visible on startup

**When to Modify**:
- Set to `false` if you want to see the main window on startup
- Useful during initial setup or troubleshooting
- Set to `true` for normal operation (auto-start with Windows)

**Example**:
```json
"appSettings": {
  "startMinimized": false
}
```

---

### Run Init

**JSON Field**: `runInit`  
**Type**: Boolean  
**Default**: true (for new installations)

Controls whether the Settings window automatically opens after dial discovery.

**Values**:
- `true`: Settings window opens after discovery (first-run behavior)
- `false`: Settings window does not open automatically (normal operation)

**When to Modify**:
- Normally set to `false` automatically after first configuration
- Set to `true` to force Settings window to open on next startup
- Useful after hardware changes or when you need to reconfigure everything

**Example**:
```json
"appSettings": {
  "runInit": true
}
```

!!! info "Automatic Management"
    VUWare automatically sets this to `false` after you configure and save your dials for the first time. You rarely need to modify this manually.

**Use Cases**:
- Hardware replacement: New dials have different UIDs, set to `true` to reconfigure
- Major reconfiguration: Set to `true` to be prompted for settings on next launch
- Reset to factory: Combined with deleting dial configs, forces fresh setup

---

### Auto Connect

**JSON Field**: `autoConnect`  
**Type**: Boolean  
**Default**: true

Controls whether VUWare automatically connects to the VU1 Hub on startup.

**Values**:
- `true`: Automatically connect on startup (default, recommended)
- `false`: Do not connect automatically (manual connection required)

**When to Modify**:
- Set to `false` for manual testing or development
- Useful when running multiple instances or testing tools
- For normal use, should always be `true`

**Example**:
```json
"appSettings": {
  "autoConnect": false
}
```

---

### Log File Path

**JSON Field**: `logFilePath`  
**Type**: String (file path)  
**Default**: "" (empty, no file logging)

Path to a file where VUWare writes debug logs.

**When to Use**:
- Troubleshooting persistent issues
- Reporting bugs with detailed logs
- Monitoring background behavior
- Diagnosing intermittent problems

**Example**:
```json
"appSettings": {
  "logFilePath": "C:\\ProgramData\\VUWare\\debug.log"
}
```

!!! warning "Performance Impact"
    Logging to file can impact performance, especially with high update frequencies. Only enable when needed for troubleshooting.

!!! info "Path Format"
    Use double backslashes `\\` in JSON for Windows paths, or use forward slashes `/` which also work.

**To disable logging**: Set to empty string
```json
"appSettings": {
  "logFilePath": ""
}
```

---

### Debug Mode

**JSON Field**: `debugMode`  
**Type**: Boolean  
**Default**: false

Enables verbose debug output to the console and log file (if configured).

**Values**:
- `true`: Verbose debugging information is output
- `false`: Normal operation, minimal logging

**When to Use**:
- Troubleshooting communication issues
- Reporting bugs (provides detailed diagnostic information)
- Understanding internal behavior

**Example**:
```json
"appSettings": {
  "debugMode": true
}
```

!!! warning "Performance and Privacy Impact"
    Debug mode generates significant log output which can:
    - Slightly reduce performance
    - Create large log files
    - Include detailed system information
    - Only enable when needed for troubleshooting

---

## Best Practices

### Configuration

- Use descriptive display names that clearly identify each sensor
- Set realistic min/max values based on typical sensor ranges
- Set warning thresholds to detect issues early (~75-80% of max)
- Set critical thresholds to indicate serious problems (~90-95% of max)
- Test configurations with Apply before closing Settings
- Use Threshold color mode for monitoring critical sensors
- Use Static color mode for aesthetic or organizational purposes

### Performance

- Use 1000ms update interval for most sensors
- Only lower interval for fast-changing sensors (voltages, rapid fluctuations)
- Disable unused dials to reduce overhead
- Don't monitor more sensors than necessary
- Avoid extremely low update intervals (<250ms) unless required

### Color Selection

- **For Monitoring**: Use high-contrast color schemes (Green → Orange → Red)
- **For Aesthetics**: Choose colors that match your setup theme
- **For Organization**: Use Static mode with different colors per dial purpose
- **Off Mode**: Use when backlight is distracting or unnecessary

### Maintenance

- Backup configuration before major changes
- Document custom configurations for future reference
- Test after HWInfo64 updates (sensor names may change)
- Verify sensor names after hardware changes
- Keep a copy of working configurations for quick recovery

### Advanced Settings (JSON)

- Always backup `dials-config.json` before manual edits
- Use a JSON validator to check syntax before saving
- Restart VUWare after manual configuration changes
- Only modify advanced settings if you understand their purpose
- Return settings to defaults if experiencing issues
