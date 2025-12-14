# Configuration Guide

This guide explains how to configure VUWare dials to monitor your system sensors.

## Opening the Settings Window

Click the **Settings** button in the main VUWare window to open the configuration interface.

!!! info "Automatic Opening on First Run"
    On first launch, VUWare automatically opens the Settings window after completing hardware initialization. You must configure at least one dial before you can start monitoring.

!!! info "When to Configure"
    You can configure dials:
    - During first-run setup (opens automatically after initialization)
    - Anytime while VUWare is running (click Settings button)
    - After hardware changes (new sensors, different thresholds)

## Settings Window Overview

The settings window contains:

- **General Settings** - Application startup preferences
- **Dial Configuration Panels** - One panel for each detected dial
- **Sensor Selection Dropdowns** - Choose sensors and readings from HWInfo64
- **Action Buttons** - OK, Apply, Cancel

![VUWare Settings Window](../images/settings_window.png)

*The settings window allows you to configure each dial individually*

## General Settings

### Application Preferences

**Minimize at Startup**
- Checkbox to minimize VUWare to system tray on startup
- Default: Enabled (application starts in system tray)
- Requires application restart to take effect

!!! note "Other Settings"
    Additional application settings like polling interval and debug mode can be configured by editing the JSON configuration file. See the [Settings Reference](../user-guide/settings.md#advanced-settings-json-configuration-only) for details.

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

**Sensor Name Dropdown**

Select the hardware component from the first dropdown.

**Examples**:
- `CPU [#0]: AMD Ryzen 7 9700X: Enhanced`
- `GPU [#0]: NVIDIA GeForce RTX 4080`
- `Motherboard: ASRock X670E Taichi`

!!! warning "Exact Match Required"
    The sensor name must match HWInfo64 exactly, including spaces, brackets, and colons.

**Entry Name Dropdown**

After selecting a sensor, choose the specific reading from the second dropdown.

**Examples**:
- `CPU (Tctl/Tdie)` - CPU temperature
- `GPU Temperature` - Graphics card temperature
- `Total CPU Usage` - Overall CPU utilization
- `GPU Core Load` - Graphics processing load

!!! info "Dropdown Contents"
    The Entry Name dropdown is populated based on your Sensor Name selection. Available readings come directly from HWInfo64's shared memory.

**After Selection**:

The configuration panel displays:
- **Sensor Name**: The selected hardware component
- **Entry Name**: The selected sensor reading

!!! tip "Finding the Right Sensor"
    If you're not sure which sensor to use:
    1. Open HWInfo64 sensor window
    2. Find the reading you want to monitor
    3. Note the sensor name (top level) and reading name
    4. Select matching values in VUWare dropdowns

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

Set the values where the dial changes color (when using Threshold color mode).

**Warning Threshold**
- Value at which dial changes to warning color
- Should be below critical but above normal operating range
- Example: 75°C for CPU temperature

**Critical Threshold**
- Value at which dial changes to critical color
- Should be near maximum safe value
- Example: 88°C for CPU temperature

**How Thresholds Work** (in Threshold mode):
```
if (sensor_value >= critical_threshold)
    -> Use Critical Color (typically Red)
else if (sensor_value >= warning_threshold)
    -> Use Warning Color (typically Yellow)
else
    -> Use Normal Color (typically Cyan)
```

!!! example "Example Thresholds"
    **CPU Temperature (°C)**
    - Normal: Below 75°C (Cyan)
    - Warning: 75-88°C (Yellow)
    - Critical: 88°C and above (Red)

### 5. Color Mode

Choose how the dial backlight behaves.

**Color Mode Options**:

**Threshold Mode** (Default)
- Color changes based on sensor value and thresholds
- Below warning: Normal Color
- Between warning and critical: Warning Color
- Above critical: Critical Color
- Use for monitoring temperatures, usage, or any value where visual warnings help

**Static Mode**
- Backlight always shows the same color
- Uses the "Static Color" setting
- Thresholds are ignored for color selection
- Useful for aesthetic purposes or to differentiate dials by purpose (e.g., CPU=Blue, GPU=Red)

**Off Mode**
- Backlight is turned off
- Dial remains functional but without LED illumination
- Useful to save power or reduce distraction

!!! tip "Choosing a Color Mode"
    - **Threshold**: Best for critical monitoring (temperatures, voltages)
    - **Static**: Best for aesthetics or dial identification
    - **Off**: Best when you only care about the needle position

### 6. Color Configuration

Choose colors based on your selected Color Mode.

#### Threshold Mode Colors

When Color Mode is "Threshold", configure these three colors:

**Normal Color**
- Used when sensor is below warning threshold
- Default: Cyan
- Recommended: Green, Blue, or Cyan

**Warning Color**
- Used when sensor is between warning and critical
- Default: Yellow
- Recommended: Orange or Yellow

**Critical Color**
- Used when sensor exceeds critical threshold
- Default: Red
- Recommended: Red or Magenta

#### Static Mode Color

When Color Mode is "Static", configure:

**Static Color**
- Backlight always shows this color
- Default: Cyan
- Choose any available color

**Available Colors**:
- White, Red, Green, Blue, Yellow, Cyan, Magenta, Orange, Purple, Pink, Off

!!! tip "Color Scheme Examples"
    **Threshold Mode:**
    - Traffic Light: Cyan → Yellow → Red
    - Cool to Hot: Blue → Orange → Red
    
    **Static Mode:**
    - CPU Dial: Static Blue
    - GPU Dial: Static Red
    - RAM Dial: Static Green

### 7. Dial Face Image (Optional)

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

### 8. Enable/Disable Dial

Each dial has an **Enabled** checkbox.

- **Checked**: Dial is active and monitoring
- **Unchecked**: Dial is disabled (shows 0%, no updates)

Use this to temporarily disable a dial without losing its configuration.

### 9. Update Interval (Optional)

Override the global update interval for this specific dial.

- Leave at 0 to use global setting (configured in JSON)
- Set a specific value (ms) to override
- Useful for sensors that change at different rates

## Validation

VUWare validates your configuration in real-time.

### Common Validation Errors

**Missing Sensor**
- Error: "Sensor name is required"
- Fix: Select a sensor from the Sensor Name dropdown

**Missing Entry**
- Error: "Entry name is required"
- Fix: Select a reading from the Entry Name dropdown

**Sensor Not Found**
- Error: "Sensor not found in HWInfo64"
- Fix: Ensure the selected sensor still exists in HWInfo64 (hardware changes or HWInfo updates may change sensor names)

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
    While the configuration is stored as JSON, manual editing is not recommended. Always use the Settings interface to make changes when possible. Advanced settings that are not in the UI must be edited in the JSON file - see the [Settings Reference](../user-guide/settings.md#advanced-settings-json-configuration-only).

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

**Color Mode**: `Threshold`

**Colors**:
- Normal Color: `Cyan`
- Warning Color: `Yellow`
- Critical Color: `Red`

**Result**: Dial shows 0% at 20°C (cyan), increases to 73% at 75°C (turns yellow), reaches 99% at 88°C (turns red), and 100% at 95°C (stays red).

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
