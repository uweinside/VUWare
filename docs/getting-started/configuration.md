# Configuration Guide

This guide explains how to configure VUWare dials to monitor your system sensors.

## Opening the Settings Window

Click the **Settings** button in the main VUWare window to open the configuration interface.

!!! info "When to Configure"
    You can configure dials:
    - During first-run setup (required)
    - Anytime while VUWare is running (to change settings)
    - After hardware changes (new sensors, different thresholds)

## Settings Window Overview

The settings window contains:

- **General Settings** - Application-wide preferences
- **Dial Configuration Panels** - One panel for each detected dial
- **Browse Sensors** - Button to view all available HWInfo64 sensors
- **Action Buttons** - OK, Apply, Cancel

## General Settings

### Application Preferences

**Enable Polling**
- Checkbox to enable/disable sensor monitoring
- Default: Enabled

**Global Update Interval**
- How often sensors are polled (in milliseconds)
- Default: 1000ms (1 second)
- Range: 100ms to 10000ms
- Lower = more responsive, higher CPU usage
- Higher = less responsive, lower CPU usage

!!! tip "Recommended Interval"
    1000ms (1 second) is ideal for most monitoring scenarios. Only reduce for fast-changing sensors that need immediate updates.

## Configuring a Dial

Each dial has its own configuration panel. Here's how to set one up:

### 1. Display Name

Give your dial a friendly name that describes what it monitors.

**Examples**:
- "CPU Temperature"
- "GPU Load"
- "CPU Fan Speed"
- "GPU Power"

!!! info "Display Names"
    Display names appear in tooltips and help you identify what each dial is monitoring.

### 2. Sensor Selection

This is the most important step - choosing which sensor to monitor.

**Steps**:

1. Click **"Browse Sensors"** button
2. A sensor browser window opens showing all HWInfo64 sensors
3. Browse the tree structure:
    - **Top level**: Hardware components (CPU, GPU, Motherboard, etc.)
    - **Second level**: Specific sensors (Temperature, Usage, Voltage, etc.)
    - **Third level**: Individual readings
4. Select the specific reading you want to monitor
5. Click **OK** to confirm

**Example Tree Structure**:
```
CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  +-- CPU (Tctl/Tdie)              <-- Select this
  +-- CPU Package
  +-- Core 0 (CCD0)
  +-- Core 1 (CCD0)
  ...

GPU [#0]: NVIDIA GeForce RTX 4080
  +-- GPU Temperature              <-- Or this
  +-- GPU Core Load
  +-- GPU Memory Load
  ...
```

!!! warning "Exact Names Required"
    The sensor name and entry name must match exactly as they appear in HWInfo64. If HWInfo64 updates or you change hardware, you may need to reconfigure.

**After Selection**:

The configuration panel fills in:
- **Sensor Name**: The hardware component name
- **Entry Name**: The specific reading name

### 3. Value Range (Min/Max)

Define the range of values that map to 0% and 100% on your dial.

**Min Value**
- The sensor value that represents 0% on the dial
- For temperature: idle or minimum expected value
- For usage: typically 0

**Max Value**
- The sensor value that represents 100% on the dial
- For temperature: maximum safe or expected value
- For usage: typically 100

**Example: CPU Temperature**
```
Min Value: 20 (°C)    -> Dial shows 0%
Max Value: 95 (°C)    -> Dial shows 100%

If sensor reads 57.5°C:
Percentage = (57.5 - 20) / (95 - 20) * 100 = 50%
```

!!! tip "Choosing Min/Max"
    - Run HWInfo64 for a while to see typical min/max values
    - Min should be slightly below your typical idle value
    - Max should be at or slightly below the critical limit
    - Values outside this range are clamped to 0% or 100%

### 4. Thresholds

Set the values where the dial changes color.

**Warning Threshold**
- Value at which dial changes to warning color
- Should be below critical but above normal operating range
- Example: 75°C for CPU temperature

**Critical Threshold**
- Value at which dial changes to critical color
- Should be near maximum safe value
- Example: 88°C for CPU temperature

**How Thresholds Work**:
```
if (sensor_value >= critical_threshold)
    -> Use Critical Color (typically Red)
else if (sensor_value >= warning_threshold)
    -> Use Warning Color (typically Orange)
else
    -> Use Normal Color (typically Green)
```

!!! example "Example Thresholds"
    **CPU Temperature (°C)**
    - Normal: Below 75°C (Green)
    - Warning: 75-88°C (Orange)
    - Critical: 88°C and above (Red)

### 5. Color Configuration

Choose colors for each operating state.

**Normal Color**
- Used when sensor is below warning threshold
- Recommended: Green, Blue, or Cyan

**Warning Color**
- Used when sensor is between warning and critical
- Recommended: Orange or Yellow

**Critical Color**
- Used when sensor exceeds critical threshold
- Recommended: Red or Magenta

**Available Colors**:
- White
- Red
- Green
- Blue
- Yellow
- Cyan
- Magenta
- Orange
- Purple
- Pink

!!! tip "Color Combinations"
    Popular color schemes:
    - **Traffic Light**: Green -> Orange -> Red
    - **Cool to Hot**: Blue -> Orange -> Red
    - **Calm to Alert**: Cyan -> Yellow -> Magenta

### 6. Dial Face Image (Optional)

Upload a custom image for the dial face.

**Steps**:
1. Click **"Upload Dial Image"** button
2. Select an image file (PNG, JPG, BMP)
3. Image is automatically scaled to 240x240 pixels
4. Preview shows the uploaded image
5. Click **"Upload to Dial"** to send to hardware

**Recommended Image Specs**:
- Format: PNG (for transparency support)
- Size: 240x240 pixels
- Colors: High contrast for visibility
- Design: Include scale markings if desired

!!! warning "Image Upload Time"
    Uploading images to the dial takes several seconds. Don't interrupt the process.

### 7. Enable/Disable Dial

Each dial has an **Enabled** checkbox.

- **Checked**: Dial is active and monitoring
- **Unchecked**: Dial is disabled (shows 0%, no updates)

Use this to temporarily disable a dial without losing its configuration.

### 8. Update Interval (Optional)

Override the global update interval for this specific dial.

- Leave at 0 to use global setting
- Set a specific value (ms) to override
- Useful for sensors that change at different rates

## Validation

VUWare validates your configuration in real-time.

### Common Validation Errors

**Missing Sensor**
- Error: "Sensor name is required"
- Fix: Click "Browse Sensors" and select a sensor

**Invalid Range**
- Error: "Max value must be greater than min value"
- Fix: Ensure Max > Min

**Invalid Thresholds**
- Error: "Critical threshold must be >= warning threshold"
- Fix: Ensure Critical >= Warning

**Out of Range Thresholds**
- Error: "Thresholds must be between min and max values"
- Fix: Set thresholds within the min/max range

!!! info "Button States"
    The **OK** and **Apply** buttons are disabled when there are validation errors. Fix all errors before saving.

## Saving Your Configuration

### Apply Button

- Saves configuration to disk
- Reloads monitoring without closing window
- Use this to test changes without closing settings

### OK Button

- Saves configuration to disk
- Reloads monitoring
- Closes settings window

### Cancel Button

- Discards all changes
- Closes settings window
- Configuration remains unchanged

!!! warning "Unsaved Changes"
    If you close the window without clicking OK or Apply, your changes are lost.

## Configuration File Location

VUWare stores configuration in:
```
C:\Program Files\VUWare\Config\dials-config.json
```

!!! danger "Manual Editing Not Recommended"
    While the configuration is stored as JSON, manual editing is not recommended. Always use the Settings interface to make changes.

## Example Configuration

Here's a complete example for monitoring CPU temperature:

**Display Name**: `CPU Temperature`

**Sensor Selection**:
- Sensor Name: `CPU [#0]: AMD Ryzen 7 9700X: Enhanced`
- Entry Name: `CPU (Tctl/Tdie)`

**Value Range**:
- Min Value: `20` (°C at idle)
- Max Value: `95` (°C maximum safe)

**Thresholds**:
- Warning Threshold: `75` (starts getting warm)
- Critical Threshold: `88` (too hot)

**Colors**:
- Normal Color: `Green`
- Warning Color: `Orange`
- Critical Color: `Red`

**Result**: Dial shows 0% at 20°C (green), increases to 73% at 75°C (turns orange), reaches 99% at 88°C (turns red), and 100% at 95°C (stays red).

## Testing Your Configuration

After configuring a dial:

1. Click **Apply** to save
2. Watch the dial in the main window
3. Monitor the sensor value in HWInfo64
4. Verify the dial percentage matches expected value
5. Trigger warning/critical thresholds to test colors

!!! tip "Testing Thresholds"
    Run a CPU/GPU stress test to verify your warning and critical thresholds trigger at the right values.

## Reconfiguring Dials

To change a dial's configuration:

1. Click **Settings** button
2. Modify the desired dial panel
3. Click **Apply** to test or **OK** to save and close
4. Changes take effect immediately

## Next Steps

Once your dials are configured:

- **[Common Use Cases](../user-guide/use-cases.md)** - See example configurations
- **[Settings Guide](../user-guide/settings.md)** - Learn about advanced settings
- **[Troubleshooting](../user-guide/troubleshooting.md)** - Fix common issues
