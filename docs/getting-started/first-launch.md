# First Launch

This guide walks you through launching VUWare for the first time and completing the initial setup wizard.

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

## First-Run Setup Wizard

When you launch VUWare for the first time, the setup wizard automatically starts.

### Welcome Screen

The welcome message explains what the wizard will do:

1. Detect your VU1 Gauge Hub
2. Discover connected dials
3. Connect to HWInfo64
4. Guide you through dial configuration

!!! tip "Initial Setup Required"
    VUWare requires initial configuration to function properly. The wizard makes this process quick and easy.

### Step 1: Dial Detection

**Status Message**: "Connecting Dials" (Yellow)

VUWare automatically:

- Searches for the VU1 Hub on USB serial ports
- Connects to the hub
- Discovers all connected dials via I2C

**What to expect**:

- This process takes 5-10 seconds
- Each dial's unique ID (UID) is detected
- Status changes to "Initializing Dials" when complete

!!! warning "If Dials Are Not Detected"
    - Verify all I2C cables are properly connected
    - Power cycle the VU1 Hub
    - Check that dials light up when powered
    - See [Troubleshooting](../user-guide/troubleshooting.md#dials-not-detected)

### Step 2: Dial Initialization

**Status Message**: "Initializing Dials" (Yellow)

VUWare:

- Sets each dial to 0% position
- Applies default color (Green)
- Verifies communication with each dial

**What to expect**:

- Dial needles move to 0% position
- Dial LEDs change to green
- Each dial responds within 1-2 seconds

### Step 3: HWInfo64 Connection

**Status Message**: "Connecting HWInfo Sensors" (Yellow)

VUWare:

- Connects to HWInfo64's shared memory
- Reads available sensors
- Prepares sensor list for configuration

!!! warning "If HWInfo64 Connection Fails"
    - Ensure HWInfo64 is running
    - Verify "Shared Memory Support" is enabled in HWInfo64 settings
    - Restart HWInfo64 if you just enabled shared memory
    - See [Troubleshooting](../user-guide/troubleshooting.md#sensors-not-available)

### Step 4: Dial Configuration

The setup wizard displays configuration panels for each detected dial.

For each dial, you'll see:

- **Dial Number** (e.g., "Dial #1")
- **Unique ID** (the dial's hardware UID)
- Configuration fields (initially empty)

!!! info "Ready to Configure"
    The wizard is now ready for you to configure each dial. Proceed to the [Configuration Guide](configuration.md) to learn how to set up your sensors.

## Main Application Window

After the wizard completes initialization, you'll see the main VUWare window:

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

## Configuring Your First Dial

During first run, all dials need configuration. Click the **Settings** button to open the configuration window.

!!! info "Next Steps"
    Continue to the [Configuration Guide](configuration.md) to set up your sensors and start monitoring.

## Completing First-Run Setup

Once you've configured at least one dial and clicked **OK**:

1. VUWare saves your configuration to `dials-config.json`
2. Monitoring begins automatically
3. Status button turns **Green** (Monitoring)
4. Configured dials start updating with real sensor data

!!! success "Setup Complete"
    VUWare is now configured and monitoring. The setup wizard won't appear again on future launches.

## What Happens on Subsequent Launches

On future launches, VUWare:

1. Loads your saved configuration
2. Connects to the VU1 Hub
3. Connects to HWInfo64
4. Starts monitoring immediately
5. No wizard required!

## Canceling First-Run Setup

!!! danger "Exiting During Setup"
    If you close the setup wizard before completing configuration, VUWare will exit completely. You'll need to restart the application to try again.

## Common First-Run Issues

### Hub Not Found

**Status stays on**: "Connecting Dials" (Yellow) for more than 30 seconds

**Solutions**:
- Check USB connection
- Verify hub is powered
- Try a different USB port
- Restart VUWare

### No Dials Detected

**Status shows**: "0 dials detected" or error message

**Solutions**:
- Verify I2C cables are connected
- Check dial power (LEDs should be on)
- Power cycle the hub
- Re-seat I2C connectors

### HWInfo64 Not Available

**Status shows**: Error connecting to HWInfo64

**Solutions**:
- Launch HWInfo64
- Enable "Shared Memory Support"
- Restart HWInfo64
- Run VUWare as administrator

### Configuration Window Won't Open

**Problem**: Settings button doesn't respond

**Solutions**:
- Wait for initialization to complete (status must be Yellow or Green)
- Check for error messages in status button tooltip
- Restart VUWare

## Next Steps

Now that you've completed the first launch and initialization:

1. **[Configure Your Dials](configuration.md)** - Set up sensors and thresholds
2. **[Review Use Cases](../user-guide/use-cases.md)** - See example configurations
3. **[Monitor Your System](../user-guide/settings.md)** - Start watching your sensors!
