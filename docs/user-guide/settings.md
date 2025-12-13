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

### Debug Mode

**Location**: General Settings section  
**Type**: Checkbox  
**Default**: Disabled

Enables detailed debug logging for troubleshooting.

- **Checked**: Verbose logging to console/file
- **Unchecked**: Normal operation

!!! warning "Performance Impact"
    Debug mode generates significant log output and may slightly reduce performance. Only enable when troubleshooting issues.

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

The sensor value at which the dial changes to warning color.

**Guidelines**:
- Set to ~75-80% of maximum safe value
- Should indicate "getting warm" or "high load"
- Below critical but above normal operating range

**Effect on Dial**:
```
If sensor value >= Warning Threshold:
  -> Dial color changes to Warning Color
```

---

### Critical Threshold

**Type**: Numeric input  
**Required**: Yes  
**Must be**: Between Warning Threshold and Max Value

The sensor value at which the dial changes to critical color.

**Guidelines**:
- Set to ~90-95% of maximum safe value
- Should indicate "too hot" or "overloaded"
- Near but below absolute maximum

**Effect on Dial**:
```
If sensor value >= Critical Threshold:
  -> Dial color changes to Critical Color
```

---

### Color Configuration

Each dial can use three different colors based on sensor value.

#### Normal Color

**Type**: Color dropdown  
**Default**: Green

Used when sensor value is below Warning Threshold.

**Recommended Colors**:
- Green: General purpose, "all good"
- Blue: Cooling-related sensors
- Cyan: Low-usage sensors

---

#### Warning Color

**Type**: Color dropdown  
**Default**: Orange

Used when sensor value is between Warning and Critical Thresholds.

**Recommended Colors**:
- Orange: Standard warning
- Yellow: Moderate alert
- Magenta: Custom warning scheme

---

#### Critical Color

**Type**: Color dropdown  
**Default**: Red

Used when sensor value is at or above Critical Threshold.

**Recommended Colors**:
- Red: Standard critical alert
- Magenta: High visibility
- Purple: Custom critical scheme

---

**Available Colors**:

| Color | Hex | Use Case |
|-------|-----|----------|
| White | #FFFFFF | Neutral, high visibility |
| Red | #FF0000 | Critical alerts |
| Green | #00FF00 | Normal operation |
| Blue | #0000FF | Cool/low temperature |
| Yellow | #FFFF00 | Moderate warning |
| Cyan | #00FFFF | Low usage/cool |
| Magenta | #FF00FF | Alternative alert |
| Orange | #FF8000 | Standard warning |
| Purple | #8000FF | Custom schemes |
| Pink | #FF0080 | Custom schemes |

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
    Manual editing of the configuration file is not recommended. Always use the Settings interface to make changes.

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

**Error Display**:
- Red outline around invalid fields
- Error tooltip on hover
- OK/Apply buttons disabled until fixed

---

## Best Practices

### Configuration

- Use descriptive display names
- Set realistic min/max values based on typical sensor ranges
- Set warning thresholds to detect issues early
- Set critical thresholds to indicate serious problems
- Test configurations with Apply before closing

### Performance

- Use 1000ms update interval for most sensors
- Only lower interval for fast-changing sensors
- Disable unused dials to reduce overhead
- Don't monitor more sensors than necessary

### Maintenance

- Backup configuration before major changes
- Document custom configurations
- Test after HWInfo64 updates
- Verify sensor names after hardware changes
