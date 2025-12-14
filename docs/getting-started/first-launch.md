# First Launch

This guide walks you through launching VUWare for the first time and setting up your dials.

## Before You Start

Ensure you have completed the [Installation](installation.md) guide and verified:

- [x] VU1 Hub is connected via USB
- [x] VU1 dials are connected to the hub
- [x] HWInfo64 is running with "Shared Memory Support" enabled

## Launching VUWare

Start VUWare from:

- **Start Menu**: Search for "VUWare" and click the app
- **Desktop**: Double-click the VUWare shortcut (if you created one during installation)
- **Program Files**: Navigate to `C:\Program Files\VUWare` and run `VUWare.App.exe`

![VUWare Splash Screen](../images/splash_screen.png)

*VUWare splash screen during initial launch*

## First-Run Initialization

When you launch VUWare for the first time, it automatically performs initialization before you can configure your dials.

!!! info "Automatic Initialization"
    VUWare handles all hardware detection automatically. You'll see status updates as it connects to your equipment.

### Initialization Steps

VUWare performs these steps automatically in the background:

**Step 1: Connecting Dials**

**Status Message**: "Connecting Dials" (Yellow)

VUWare automatically:

- Searches for the VU1 Hub on USB serial ports
- Connects to the hub

**What to expect**:

- This process takes 2-5 seconds
- Status updates as connection proceeds
- Status changes to "Initializing Dials" when hub is found

!!! warning "If Hub Connection Fails"
    - Verify USB cable is properly connected
    - Power cycle the VU1 Hub
    - Try a different USB port
    - See [Troubleshooting](../user-guide/troubleshooting.md#hub-not-found)

**Step 2: Initializing Dials**

**Status Message**: "Initializing Dials" (Yellow)

VUWare:

- Discovers all connected dials via I2C
- Detects each dial's unique ID (UID)
- Reads current dial positions (no changes made on first run)

**What to expect**:

- Discovery takes 2-3 seconds
- Dial needles remain at their current position
- Dial LEDs remain in their current state
- Status changes to "Connecting HWInfo Sensors"

!!! info "First Run vs. Subsequent Runs"
    **First Run (No Saved Configuration)**: VUWare only discovers dials and does not change their position or LED color. The dials remain in their current state.
    
    **Subsequent Runs (With Saved Configuration)**: After you configure and save your settings, VUWare will initialize each configured dial to 0% position with the configured "Normal Color" for that dial (default: Cyan).

!!! warning "If Dials Are Not Detected"
    - Verify all I2C cables are properly connected
    - Power cycle the VU1 Hub
    - Check that dials light up when powered
    - See [Troubleshooting](../user-guide/troubleshooting.md#dials-not-detected)

**Step 3: Connecting to HWInfo64**

**Status Message**: "Connecting HWInfo Sensors" (Yellow)

VUWare:

- Connects to HWInfo64's shared memory
- Reads available sensors
- Prepares sensor list for configuration

**What to expect**:

- Connection is usually instant if HWInfo64 is running
- May retry for up to 5 minutes if HWInfo64 is not yet running
- You'll see retry counter if connection takes time
- Status changes to "Monitoring" when complete

!!! warning "If HWInfo64 Connection Fails"
    - Ensure HWInfo64 is running
    - Verify "Shared Memory Support" is enabled in HWInfo64 settings
    - Restart HWInfo64 if you just enabled shared memory
    - See [Troubleshooting](../user-guide/troubleshooting.md#sensors-not-available)

## Settings Page Opens Automatically

After initialization completes, VUWare automatically opens the Settings window for you to configure your sensors.

![VUWare Settings Window](../images/settings_window.png)

*Settings window opens automatically on first run*

You'll see:

- **General Settings** - Application preferences
- **Dial Configuration Panels** - One for each detected dial
- **Browse Sensors** - Button to view all HWInfo64 sensors

!!! info "Ready to Configure"
    Proceed to the [Configuration Guide](configuration.md) to learn how to set up your sensors.

## Configuring Your Dials

On first run, all dials need configuration. For each dial:

1. **Give it a display name** - E.g., "CPU Temperature"
2. **Browse and select a sensor** - Click "Browse Sensors" to see all available sensors
3. **Set the value range** - Define min/max values for 0% and 100%
4. **Set thresholds** - Define warning and critical values
5. **Choose colors** - Pick colors for normal, warning, and critical states

For detailed configuration instructions, see the [Configuration Guide](configuration.md).

## Completing First-Run Setup

Once you've configured at least one dial:

1. Click **OK** to save your configuration
2. VUWare saves your settings to `dials-config.json`
3. The Settings window closes
4. Monitoring begins automatically
5. Status button turns **Green** (Monitoring)
6. Configured dials start updating with real sensor data

!!! success "Setup Complete"
    VUWare is now configured and monitoring. The Settings window will not open automatically on future launches.

## Main Application Window

After completing configuration, you'll see the main VUWare window:

### Window Layout

**Top Row - Dial Status Buttons (1-4)**

Each button shows:
- Dial number
- Current percentage (e.g., "45%")
- Color indicator (matches dial LED color)

**Bottom Row - Status Button**

Shows current application state:
- **Gray**: Idle/Not configured
- **Yellow**: Initializing or processing
- **Green**: Monitoring active
- **Red**: Error state

### Status Button States

| Color | Meaning | What It Means |
|-------|---------|---------------|
| Gray | Idle | Waiting for configuration or not monitoring |
| Yellow | In Progress | Initializing, connecting, or applying settings |
| Green | Monitoring | Active monitoring - all systems operational |
| Red | Error | Something went wrong - check tooltip for details |

### Dial Button Information

Hover over any dial button to see detailed information:

```
CPU Temperature
Sensor: CPU (Tctl/Tdie)
Value: 62.5 °C
Dial: 66%
Color: Green
Updates: 1234
Last: 14:32:45
```

## What Happens on Subsequent Launches

On future launches, VUWare:

1. Loads your saved configuration
2. Performs the same initialization (connects to hub, discovers dials, connects to HWInfo64)
3. Initializes each configured dial to 0% with its configured Normal Color
4. Starts monitoring immediately with your saved settings
5. Settings window does NOT open automatically

!!! tip "Changing Settings"
    Click the **Settings** button in the main window anytime to modify your configuration.

## Canceling First-Run Setup

!!! danger "Exiting During Initialization"
    If you close VUWare during initialization (before configuring dials), the app will exit. Your next launch will start the initialization process again.

## Common First-Run Issues

### Hub Not Found

**Status stays on**: "Connecting Dials" (Yellow) for more than 30 seconds

**Solutions**:
- Check USB connection
- Verify hub is powered
- Try a different USB port
- Restart VUWare

### No Dials Detected

**Status shows**: "Initializing Dials" completes but shows 0 dials

**Solutions**:
- Verify I2C cables are connected
- Check dial power (LEDs should be on)
- Power cycle the hub
- Re-seat I2C connectors

### HWInfo64 Not Available

**Status shows**: "Connecting HWInfo Sensors" with increasing retry count

**Solutions**:
- Launch HWInfo64
- Enable "Shared Memory Support"
- Restart HWInfo64
- Run VUWare as administrator

### Settings Window Doesn't Open

**Problem**: Initialization completes but Settings window doesn't appear

**Solutions**:
- Check for Settings window minimized or behind other windows
- Click the **Settings** button in the main window manually
- Restart VUWare

## Next Steps

Now that you've completed the first launch and initialization:

1. **[Configure Your Dials](configuration.md)** - Set up sensors and thresholds
2. **[Review Use Cases](../user-guide/use-cases.md)** - See example configurations
3. **[Monitor Your System](../user-guide/settings.md)** - Start watching your sensors!
