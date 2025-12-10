# VU-Server Implementation Guide

## Overview

This document provides detailed implementation information about the VU1 dial system architecture, including device discovery, addressing mechanisms, data persistence, and the complete lifecycle of dial management.

## System Architecture

### Three-Layer Architecture

```
┌─────────────────────────────────────────────────────┐
│           Application Layer                         │
│         - VU1Controller (VUWare.Lib)                │
│         - Application-specific logic                │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│      Communication Layer (Serial Protocol)          │
│         - SerialPortManager (VUWare.Lib)            │
│         - DeviceManager (VUWare.Lib)                │
│         - ProtocolHandler (VUWare.Lib)              │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│         Hardware Layer (USB/I2C)                     │
│    - Gauge Hub (USB-Serial Bridge)                  │
│    - I2C Bus (up to 100 dial devices)               │
└─────────────────────────────────────────────────────┘
```

### Component Responsibilities

**VU1Controller (VUWare.Lib)**
- High-level API for dial control
- Device discovery and initialization
- State management and caching
- Asynchronous command execution
- Image update queuing

**SerialPortManager (VUWare.Lib)**
- USB device detection (VID:0x0403, PID:0x6015)
- Serial port configuration (115200 8N1)
- Thread-safe send/receive operations
- Line-based protocol implementation
- Timeout and cancellation handling

**DeviceManager (VUWare.Lib)**
- Device provisioning and discovery
- UID tracking and index mapping
- Firmware/hardware version queries
- Easing configuration management

**Gauge Hub (Hardware Device)**
- USB-to-Serial interface (VID:0x0403, PID:0x6015)
- I2C master controller
- Manages I2C bus communication with dials
- Handles device provisioning and addressing
- Protocol translation (Serial ↔ I2C)

**Dial Devices (VU1 Gauges)**
- I2C slave devices
- Contains unique 12-byte UID (factory-programmed)
- E-paper display controller
- Analog dial motor controller
- RGBW backlight controller
- Stores configuration in non-volatile memory

## Device Discovery and Addressing

### I2C Address Architecture

The system uses a two-phase addressing scheme:

#### Phase 1: Factory Default Addresses

**Important:** All VU1 dials leave the factory with the **SAME I2C address** (0x09). This is intentional - it allows multiple identical units on the same bus through dynamic addressing.

When powered on or reset, all dial devices listen on special provisioning addresses:

```
GAUGE_I2C_ADDRESS_GENERAL_CALL = 0x00  # Broadcast address
GAUGE_I2C_ADDRESS_BOOTLOADER = 0x08    # Bootloader mode
GAUGE_I2C_ADDRESS_PROVISIONING = 0x09  # Initial provisioning address (FACTORY DEFAULT)
```

**Why the same address?**
- Standard I2C allows only one device per address
- If all dials had different factory addresses, you'd need 100 different SKUs
- Dynamic addressing allows interchangeable dials
- Physical I2C bus position determines final address assignment

**Key Concept:** Multiple dials share the provisioning address (0x09) simultaneously. The hub uses each dial's unique 12-byte UID to differentiate and assign unique runtime addresses one by one.

#### Phase 2: Dynamic Address Assignment

After provisioning, each dial is assigned a unique runtime address:

```
GAUGE_I2C_ADDRESS_FIRST = 0x0A         # First assignable address (10)
GAUGE_I2C_ADDRESS_LAST = 0x6E          # Last address (10 + 100 = 110)
GAUGE_HUB_MAX_DEVICES = 100            # Maximum supported devices
```

Assigned addresses range from **0x0A to 0x6E** (10 to 110 decimal).

### Complete Discovery Process

#### 1. Initial Bus Scan

```
Command: COMM_CMD_RESCAN_BUS (0x0C)
Purpose: Hub scans I2C bus to detect devices
```

**What happens:**
- Hub sends I2C requests to all possible addresses (0x0A-0x6E)
- Devices that respond are marked as "online"
- Hub builds an internal bitmap of active devices

#### 2. Provisioning New Devices

```
Command: COMM_CMD_PROVISION_DEVICE (0x08)
Purpose: Assign addresses to unprovisioned dials
```

**Why is Provisioning Necessary?**

You might wonder: "Don't I2C devices have fixed, factory-programmed addresses?" 

The answer is YES for most I2C devices, but the VU1 dials use a special approach:

- **All VU1 dials have the SAME factory I2C address (0x09)**
- This is intentional and necessary for several reasons:
  - Standard I2C allows only **one device per address** on a bus
  - If each dial had a unique factory address, you'd need **100 different product SKUs** to support 100 dials
  - Manufacturing would require programming each unit differently during production
  - Inventory management would be complex (can't use any dial in any position)
  - Replacement/repair would require specific SKUs for specific positions

- **Each dial also has a unique 12-byte UID** (factory-programmed in flash/EEPROM)
- The UID never changes and survives power cycles
- The hub uses the UID to target specific dials during provisioning

**How Multiple Dials at 0x09 Works:**

When multiple dials share address 0x09, I2C bus arbitration ensures only one responds successfully:
- I2C is a multi-master bus with collision detection
- When multiple devices try to respond, the first one physically on the bus "wins"
- The hub reads that dial's UID and assigns it a unique runtime address
- That dial reconfigures to the new address and stops listening on 0x09
- The process repeats for the next dial still at 0x09

This is similar to how some sensor chips (like certain accelerometers) handle address conflicts with hardware address pins, but VU1 uses software addressing via UIDs instead.

**Provisioning Algorithm (Hub firmware side):**

```
1. Hub broadcasts on GENERAL_CALL address (0x00)
   - All unprovisioned dials respond (all still at 0x09)

2. For each unprovisioned dial:
   a. Hub sends GET_UID command to PROVISIONING address (0x09)
   b. Multiple dials may be at 0x09, but I2C bus arbitration means
      only the first one physically on the bus successfully responds
   c. That dial returns its unique 12-byte UID
   d. Hub assigns next available address (starting from 0x0A)
   e. Hub sends SET_ADDRESS command with:
      - Target UID (to identify specific dial)
      - New I2C address
   f. ONLY the dial with matching UID adopts the new address
   g. That dial reconfigures its I2C peripheral to the new address
   h. That dial stops listening on PROVISIONING address (0x09)
   i. Dial stores new address in RAM (volatile - lost on power cycle)

3. Repeat step 2 until no more devices respond on 0x09
   - Each iteration removes one dial from 0x09
   - Eventually only newly added/reset dials remain at 0x09
```

**Important Notes:**
- Provisioning must be called multiple times (typically 3 attempts)
- 200ms delay recommended between attempts
- VUWare.Lib implementation calls provision 3 times for reliability

**C# Implementation (VUWare.Lib/DeviceManager.cs):**
```csharp
public async Task<bool> DiscoverAndProvisionAsync()
{
    const int NUM_ATTEMPTS = 3;
    const int DELAY_MS = 200;
    
    for (int attempt = 0; attempt < NUM_ATTEMPTS; attempt++)
    {
        await ProvisionDevicesAsync();
        await Task.Delay(DELAY_MS);
    }
    
    return await ReloadDialsAsync(rescan: true);
}
```

#### 3. Retrieving Device Map

```
Command: COMM_CMD_GET_DEVICES_MAP (0x07)
Returns: Bitmap of online devices (up to 100 bytes)
```

**Response Format:**
- One byte per device index (0-99)
- `0x01` = device online at that index
- `0x00` = no device at that index

**Example:**
```
Response: 010100010000...
Meaning:
  Index 0: Online (0x01)
  Index 1: Offline (0x00)
  Index 2: Online (0x01)
  Index 3: Offline (0x00)
  Index 4: Online (0x01)
  ...etc
```

#### 4. Reading Device UIDs

After getting the device map, the server queries each online dial:

```
For each online index:
    Command: COMM_CMD_GET_DEVICE_UID (0x0B)
    Data: [dial_index]
    Response: 12-byte UID (hex-encoded as 24 characters)
```

**C# Implementation (VUWare.Lib/DeviceManager.cs):**
```csharp
public async Task<Dictionary<string, DialState>> DiscoverDialsAsync(bool rescan = false)
{
    if (rescan)
    {
        await BusRescanAsync();
        string response = await SendCommandAsync(CommandBuilder.GetDevicesMap());
        
        // Parse device map (2 hex chars per device)
        var onlineDials = new List<byte>();
        for (int i = 0; i < response.Length; i += 2)
        {
            byte status = Convert.ToByte(response.Substring(i, 2), 16);
            if (status == 1)
            {
                onlineDials.Add((byte)(i / 2));
            }
        }
        
        // Read UID for each online dial
        foreach (byte dialIndex in onlineDials)
        {
            string deviceUID = await GetDeviceUIDAsync(dialIndex);
            _dials[deviceUID] = new DialState
            {
                Index = dialIndex,
                UID = deviceUID,
                // ...additional fields
            };
        }
    }
    
    return _dials;
}
```

### Index vs. UID Addressing

**Two addressing schemes coexist:**

1. **Index-Based (Hub ↔ Dial Communication)**
   - Hub uses index (0-99) to address dials
   - Index corresponds to assigned I2C address offset
   - Index may change if device is re-provisioned
   - Fast, efficient for I2C communication

2. **UID-Based (Server ↔ Application)**
   - Server uses 12-byte UID to identify dials
   - UID is permanent (factory-programmed)
   - UID remains consistent across power cycles
   - Allows tracking dial identity regardless of physical location

**Why UID is Critical for Persistence:**

The UID is the key to maintaining dial metadata across power cycles:

```
Before Power Cycle:
  Dial at Index 2, UID "ABC123...", Name "CPU Usage", Easing 5/100ms

Power Cycle Occurs:
  - All dials reset to I2C address 0x09
  - Server must rediscover devices
  
After Re-provisioning:
  - Dial might now be at Index 5 (different physical position)
  - Server reads UID: "ABC123..."
  - Server looks up UID in database
  - Finds: Name "CPU Usage", Easing 5/100ms
  - Reapplies configuration to dial at new index 5
  
Result: User sees same dial with same settings, even though index changed
```

The database uses `dial_uid` as the primary lookup key (with UNIQUE constraint), ensuring:
- Dial identity persists regardless of I2C address changes
- User-defined metadata (name, settings) follows the physical dial
- System handles dial rearrangement automatically

**Translation Example:**
```
User Request: "Set dial with UID '3A4B5C6D7E8F' to 75%"

Server Process:
1. Look up UID in internal dial list
2. Find associated index (e.g., index=5)
3. Send command: SET_DIAL_PERC_SINGLE with index=5, value=75
4. Hub translates index to I2C address (0x0A + 5 = 0x0F)
5. Hub sends I2C command to address 0x0F
```

## Serial Communication Architecture

### Line-Based Protocol Design

**The VU1 hub implements a line-based communication protocol.** This is a crucial architectural decision that simplifies implementation and improves reliability.

**Protocol Characteristics:**
- Commands and responses are complete ASCII lines
- Each line is terminated with `\r\n` (CRLF)
- Commands start with `>`, responses start with `<`
- No character-by-character parsing required
- Standard `ReadLine()` methods can be used

**Why Line-Based?**
1. **Simplicity** - Standard library support for line reading
2. **Reliability** - Complete messages, no partial data
3. **Debugging** - Easy to capture and inspect in serial monitors
4. **Atomic** - Each line is a complete transaction
5. **Buffering** - OS/hardware handles buffering automatically

### Python Implementation

The VU-Server Python implementation (located in `legacy/src/serial_driver.py`) demonstrates the line-based approach:

```python
def handle_serial_read(self):
    """Reads one complete line until \\n"""
    try:
        response = self.port.readline()  # ← LINE-BASED READING
        ret = response.decode("utf-8").strip()
        return ret
    except _serial.SerialTimeoutException:
        return None

def read_until_response(self, timeout=5):
    """Reads lines until response starting with '<' is found"""
    rx_lines = []
    while time.time() <= timeout_timestamp:
        line = self.handle_serial_read()  # ← LINE-BY-LINE
        if line and line.startswith('<'):
            break
        rx_lines.append(line)
    return rx_lines
```

### C# Implementation

The VUWare.Lib C# implementation (in `SerialPortManager.cs`) uses the same pattern:

```csharp
private async Task<string> ReadResponseAsync(SerialPort serialPort, 
                                             int timeoutMs, 
                                             CancellationToken cancellationToken)
{
    // Read LINE-BY-LINE like the Python implementation
    // The VU1 hub sends complete responses as lines terminated with \\r\\n
    while (!cancellationToken.IsCancellationRequested)
    {
        if (serialPort.BytesToRead == 0)
        {
            await Task.Delay(10, cancellationToken);
            continue;
        }
        
        // Read one line (blocks until \\n or timeout)
        string line = serialPort.ReadLine().Trim();  // ← LINE-BASED READING
        
        if (!string.IsNullOrEmpty(line) && line.StartsWith("<"))
        {
            return line;
        }
    }
}
```

Both implementations use `readline()` / `ReadLine()` to receive complete protocol messages as lines, matching the hub's line-based protocol design.

## Data Persistence and State Management

### Server-Side Persistence (SQLite Database)

**Database Schema:**

```sql
CREATE TABLE dials (
    dial_id INTEGER PRIMARY KEY AUTOINCREMENT,
    dial_uid TEXT NOT NULL UNIQUE,
    dial_name TEXT DEFAULT 'Not Set',
    dial_gen TEXT DEFAULT 'VU1',
    dial_build_hash TEXT DEFAULT '?',
    dial_fw_version TEXT DEFAULT '?',
    dial_hw_version TEXT DEFAULT '?',
    dial_protocol_version TEXT DEFAULT 'V1',
    easing_dial_step INTEGER DEFAULT 2,
    easing_dial_period INTEGER DEFAULT 50,
    easing_backlight_step INTEGER DEFAULT 5,
    easing_backlight_period INTEGER DEFAULT 100
);
```

**What is Stored:**

| Data | Persistence | Source | Purpose |
|------|-------------|--------|---------|
| **dial_uid** | Server DB | Read from dial hardware | Unique identifier |
| **dial_name** | Server DB | User-defined | Friendly name ("CPU", "GPU", etc.) |
| **dial_gen** | Server DB | Server config | Generation identifier |
| **dial_build_hash** | Server DB | Read from dial hardware | Firmware build hash |
| **dial_fw_version** | Server DB | Read from dial hardware | Firmware version |
| **dial_hw_version** | Server DB | Read from dial hardware | Hardware revision |
| **dial_protocol_version** | Server DB | Read from dial hardware | Protocol version |
| **easing_dial_step** | Server DB + Dial NV Memory | User config | Dial movement smoothing |
| **easing_dial_period** | Server DB + Dial NV Memory | User config | Dial update timing |
| **easing_backlight_step** | Server DB + Dial NV Memory | User config | Backlight smoothing |
| **easing_backlight_period** | Server DB + Dial NV Memory | User config | Backlight timing |

### Dial-Side Persistence (Non-Volatile Memory)

**What Dials Remember:**

1. **Easing Configuration**
   - Dial step size
   - Dial period
   - Backlight step size
   - Backlight period
   - Stored in dial's EEPROM/Flash

2. **Calibration Data**
   - Maximum position (full scale)
   - Half position (midpoint)
   - Stored in dial's EEPROM/Flash

**What Dials DON'T Remember:**

1. **I2C Address** - Stored in RAM only, resets to 0x09 on power cycle
   - This is intentional - allows flexible reconfiguration
   - All dials revert to factory default address (0x09)
   - Requires re-provisioning on each startup
2. **Current Position** - Resets to 0
3. **Backlight Color** - Resets to off
4. **Display Image** - E-paper retains last image (passive retention)

### Startup Sequence

**Application Initialization Process:**

```
1. Create VU1Controller instance
   - Initialize SerialPortManager
   - Initialize DeviceManager
   - Set up command queue

2. Connect to Gauge Hub
   - Auto-detect: Scan for VID:0x0403, PID:0x6015
   - Manual: Use specified COM port
   - Timeout: 2 seconds

3. Discover dials (initial provisioning)
   - Call RescanBus
   - Call ProvisionDevice (3 times with 200ms delay)
   - Call GetDevicesMap
   - For each online dial:
     - Read UID from hardware
     - Create DialState object
     - Map UID to current runtime index
     - Query firmware/hardware versions
     - Query easing configuration

4. Initialize dial state
   - Set all dials to 0%
   - Apply default easing configuration
   - Clear display buffers

5. Ready for operation
   - Application can now control dials
   - Use UID-based API methods
```

**C# Code Flow (VUWare.Lib/VU1Controller.cs):**
```csharp
public class VU1Controller : IDisposable
{
    private readonly SerialPortManager _serialPort;
    private readonly DeviceManager _deviceManager;
    
    public async Task<bool> InitializeAsync()
    {
        // Discover devices with provisioning
        var dials = await _deviceManager.DiscoverAndProvisionAsync();
        
        if (dials.Count == 0)
        {
            return false;
        }
        
        // Query device information
        foreach (var dial in dials.Values)
        {
            dial.FirmwareVersion = await _deviceManager.GetFirmwareVersionAsync(dial.Index);
            dial.HardwareVersion = await _deviceManager.GetHardwareVersionAsync(dial.Index);
            dial.Easing = await _deviceManager.GetEasingConfigAsync(dial.Index);
        }
        
        // Initialize to safe state
        foreach (var dial in dials.Values)
        {
            await SetDialPercentageAsync(dial.UID, 0);
        }
        
        _isInitialized = true;
        return true;
    }
}
```

**Device Discovery:**
```csharp
public async Task<Dictionary<string, DialState>> DiscoverAndProvisionAsync()
{
    // Rescan I2C bus
    await RescanBusAsync();
    
    // Provision devices (3 attempts)
    for (int i = 0; i < 3; i++)
    {
        await ProvisionDevicesAsync();
        await Task.Delay(200);
    }
    
    // Get device map and read UIDs
    return await DiscoverDialsAsync(rescan: true);
}
```

**State Management:**
```csharp
// VUWare.Lib/DialState.cs
public class DialState
{
    public byte Index { get; set; }              // Hub index (changes with provisioning)
    public string UID { get; set; }              // Permanent unique ID (PRIMARY KEY)
    public string Name { get; set; }             // User-friendly name
    public byte CurrentValue { get; set; }       // Current percentage (0-100)
    public BacklightColor Backlight { get; set; } // Current RGBW values
    public EasingConfig Easing { get; set; }     // Animation settings
    public string FirmwareVersion { get; set; }  // Firmware version
    public string HardwareVersion { get; set; }  // Hardware version
    public DateTime LastCommunication { get; set; } // Last contact time
}

public class BacklightColor
{
    public byte Red { get; set; }    // 0-100
    public byte Green { get; set; }  // 0-100
    public byte Blue { get; set; }   // 0-100
    public byte White { get; set; }  // 0-100
}

public class EasingConfig
{
    public uint DialStep { get; set; }         // % change per update
    public uint DialPeriod { get; set; }       // ms between updates
    public uint BacklightStep { get; set; }    // % change per update  
    public uint BacklightPeriod { get; set; }  // ms between updates
}

// Access by UID (permanent identifier)
public DialState? GetDial(string uid)
{
    return _dials.TryGetValue(uid, out var dial) ? dial : null;
}

// Get all dials
public IReadOnlyDictionary<string, DialState> GetAllDials()
{
    return _dials;
}
```

**Note:** The UID is used as the dictionary key, ensuring metadata follows the physical dial regardless of its current index/position.

### Complete Power Cycle Example

```
1. System powered on
   - All dials assert reset, listen on 0x09

2. Host (PC/Server) starts
   - Initializes VU1Controller
   - Scans for devices (VID:0x0403, PID:0x6015)
   - Connects to Gauge Hub

3. Device discovery process:
   - VU1Controller.InitializeAsync called
   - Scans I2C bus (COMM_CMD_RESCAN_BUS)
   - Receives bitmap of online devices (COMM_CMD_GET_DEVICES_MAP)
   - Detected devices: 3 (indexes 0, 1, 2)

4. Provisioning new devices:
   - VU1Controller discovers 3 online devices (indexes 0-2)
   - Provisions each device:
     - Sends GET_UID to 0x09
     - Receives unique UIDs:
       - Dial 1: "ABC123ABC123"
       - Dial 2: "DEF456DEF456"
       - Dial 3: "789ABC789ABC"
     - Assigns dynamic I2C addresses:
       - Dial 1: 0x0A
       - Dial 2: 0x0B
       - Dial 3: 0x0C
     - Sends SET_ADDRESS commands to each dial
       - Targeting by UID
       - New addresses (10, 11, 12)

5. Retrieving device map:
   - Host requests device map (COMM_CMD_GET_DEVICES_MAP)
   - Receives bitmap: 00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000011
   - Indicates 3 online devices (at indexes 0, 1, 2)

6. Reading device UIDs:
   - Host reads UID for each online dial (COMM_CMD_GET_DEVICE_UID)
   - For each index 0-2:
     - Sends request to get UID
     - Receives UID from dial
   - Confirms UIDs:
     - Dial 1: "ABC123ABC123"
     - Dial 2: "DEF456DEF456"
     - Dial 3: "789ABC789ABC"

7. Initializing dial state:
   - Sets all dials to 0%
   - Applies default easing configuration
   - Clears display buffers

8. Ready for operation
   - Host sends commands to control dials
   - E.g., SetDialPercentageAsync("ABC123ABC123", 75)
   - VU1Controller translates UID to index, sends I2C command to dial
```

### Image Format and Conversion

**Display Specifications:**
- E-paper display: 200x144 pixels (confirmed)
- 1-bit color depth (black/white)
- Vertical byte packing (8 pixels per byte, MSB=top)
- Total packed size: 3600 bytes ((200×144)/8)

**Image Encoding Process (VUWare.Lib/ImageProcessor.cs):**

```csharp
public static byte[] LoadImageFile(string filePath)
{
    // 1. Load image
    using var img = Image.Load<Rgba32>(filePath);
    
    // 2. Resize to display dimensions if needed
    if (img.Width != DISPLAY_WIDTH || img.Height != DISPLAY_HEIGHT)
    {
        img.Mutate(x => x.Resize(DISPLAY_WIDTH, DISPLAY_HEIGHT));
    }
    
    // 3. Convert to grayscale
    byte[] grayscale = ConvertToGrayscale(img);
    
    // 4. Pack vertically: 8 pixels per byte
    return ConvertGrayscaleTo1Bit(grayscale, DISPLAY_WIDTH, DISPLAY_HEIGHT);
}

public static byte[] ConvertGrayscaleTo1Bit(byte[] grayscale, int width, int height, int threshold = 127)
{
    byte[] packed = new byte[(width * height) / 8];
    int byteIndex = 0;
    
    // Process column by column (vertical packing)
    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y += 8)
        {
            byte packedByte = 0;
            
            // Pack 8 vertical pixels into one byte (MSB = top pixel)
            for (int bit = 0; bit < 8; bit++)
            {
                int pixelY = y + bit;
                if (pixelY < height)
                {
                    int pixelIndex = pixelY * width + x;
                    bool isLight = grayscale[pixelIndex] > threshold;
                    
                    if (isLight)
                    {
                        packedByte |= (byte)(1 << (7 - bit)); // MSB = top pixel
                    }
                }
            }
            
            packed[byteIndex++] = packedByte;
        }
    }
    
    return packed;
}
```

**Bit Packing Example:**
````````

This is the description of what the code block changes:
Update Default Values section to reference C# constants

This is the code block that represents the suggested code change:

````````markdown
### Default Values

```csharp
// VUWare.Lib default easing configuration
public static class EasingDefaults
{
    public const uint DialStep = 2;          // 2% per update
    public const uint DialPeriod = 50;       // 50ms between updates
    public const uint BacklightStep = 5;     // 5% per update
    public const uint BacklightPeriod = 100; // 100ms between updates
}

// Example usage
var defaultEasing = new EasingConfig
{
    DialStep = 2,
    DialPeriod = 50,
    BacklightStep = 5,
    BacklightPeriod = 100
};
```

### Configuration Persistence

**Storage Options:**
1. **Application-Managed** - Application stores dial metadata (UID → Name, Easing, etc.)
2. **Dial EEPROM** - Dials store easing configuration in non-volatile memory

**VUWare.Lib Pattern:**

```csharp
// Application stores mapping of UID to user preferences
public class DialConfiguration
{
    public string UID { get; set; }
    public string Name { get; set; }
    public EasingConfig Easing { get; set; }
}

// Load configuration on startup
public async Task RestoreDialConfigurationAsync(DialConfiguration config)
{
    var dial = GetDial(config.UID);
    if (dial != null)
    {
        dial.Name = config.Name;
        
        // Send easing to dial hardware
        await SetEasingConfigAsync(config.UID, config.Easing);
    }
}

// SetEasingConfigAsync sends four commands to the dial
public async Task<bool> SetEasingConfigAsync(string uid, EasingConfig config)
{
    var dial = GetDial(uid);
    if (dial == null) return false;
    
    await SendCommandAsync(CommandBuilder.SetDialEasingStep(dial.Index, config.DialStep));
    await SendCommandAsync(CommandBuilder.SetDialEasingPeriod(dial.Index, config.DialPeriod));
    await SendCommandAsync(CommandBuilder.SetBacklightEasingStep(dial.Index, config.BacklightStep));
    await SendCommandAsync(CommandBuilder.SetBacklightEasingPeriod(dial.Index, config.BacklightPeriod));
    
    dial.Easing = config;
    return true;
}
```

**Synchronization Flow:**
```
Application Loads Configuration
    ↓
Call SetEasingConfigAsync(UID, config)
    ↓
VU1Controller sends commands to dial:
    - SET_DIAL_EASING_STEP
    - SET_DIAL_EASING_PERIOD
    - SET_BACKLIGHT_EASING_STEP
    - SET_BACKLIGHT_EASING_PERIOD
    ↓
Dial stores in EEPROM
    ↓
Dial uses for all future transitions
```

**On Application Restart:**
```
Application Starts
    ↓
VU1Controller.InitializeAsync() discovers dials
    ↓
Application loads saved configurations (UID-based)
    ↓
Application calls SetEasingConfigAsync for each dial
    ↓
Dials receive and store configuration
    ↓
Ready for operation
```

## Error Handling and Edge Cases

### Serial Communication Errors

**Timeout Handling:**
- Read timeout: 2 seconds (configurable)
- Write timeout: 2 seconds (configurable)
- On timeout: Throw TimeoutException
- Async operations support CancellationToken

**Response Validation (VUWare.Lib/ProtocolHandler.cs):**
```csharp
public static Message ParseResponse(string response)
{
    if (string.IsNullOrEmpty(response) || response.Length < 9)
    {
        throw new ArgumentException("Invalid response format");
    }
    
    // Validate start character
    if (response[0] != '<')
    {
        throw new InvalidOperationException("Response does not start with '<'");
    }
    
    // Parse header: <CCDDLLLL[DATA]
    byte command = byte.Parse(response.Substring(1, 2), NumberStyles.HexNumber);
    byte dataType = byte.Parse(response.Substring(3, 2), NumberStyles.HexNumber);
    int dataLength = int.Parse(response.Substring(5, 4), NumberStyles.HexNumber);
    
    string rawData = response.Length > 9 ? response.Substring(9) : string.Empty;
    
    var message = new Message
    {
        Command = command,
        DataType = (DataType)dataType,
        DataLength = dataLength,
        RawData = rawData
    };
    
    // Parse binary data if present
    if (message.DataType == DataType.StatusCode && rawData.Length >= 4)
    {
        message.BinaryData = HexStringToBytes(rawData.Substring(0, 4));
    }
    else if (rawData.Length > 0)
    {
        message.BinaryData = HexStringToBytes(rawData);
    }
    
    return message;
}

public static bool IsSuccessResponse(Message message)
{
    if (message.DataType != DataType.StatusCode)
    {
        return true; // Non-status responses are typically successful
    }
    
    if (message.BinaryData == null || message.BinaryData.Length < 2)
    {
        return false;
    }
    
    // Status codes are big-endian 16-bit values
    ushort statusCode = (ushort)((message.BinaryData[0] << 8) | message.BinaryData[1]);
    return statusCode == (ushort)GaugeStatus.OK;
}
```

### Device Offline/Disconnect

**Current Workaround:**

Users must manually:
1. Restart the server application
2. Or call the `/api/v0/dial/provision` endpoint
3. This forces re-discovery and re-provisioning

**Code Location:**
```python
# server.py, lines 549-553
# Only provisions at startup
if len(self.dial_handler.dials) <= 1:
    logger.info("No additional dials found. Searching the bus for new ones...")
    self.dial_handler.provision_dials(num_attempts=3)

# No periodic re-provisioning or verification in periodic_dial_update()
```

**C# Implementation Recommendation:**

For a robust C# library, VUWare.Lib can implement:

1. **Heartbeat/Keep-Alive**: Periodically verify dial communication
2. **UID Verification**: Periodically confirm index→UID mapping
3. **Automatic Recovery**: Re-provision on detection of communication loss
4. **Device Count Monitoring**: Track expected vs. actual device count
5. **Error Pattern Detection**: Trigger recovery on repeated failures

**Example C# Architecture:**
```csharp
// VUWare.Lib - Dial health monitoring
public class DialHealthMonitor
{
    private readonly VU1Controller _controller;
    private readonly Timer _verificationTimer;
    private readonly Dictionary<string, int> _failureCounts = new();
    
    public DialHealthMonitor(VU1Controller controller)
    {
        _controller = controller;
        _verificationTimer = new Timer(PeriodicVerification, null, 
                                       TimeSpan.FromMinutes(1), 
                                       TimeSpan.FromMinutes(1));
    }
    
    private async void PeriodicVerification(object? state)
    {
        foreach (var dial in _controller.GetAllDials().Values)
        {
            try
            {
                // Verify UID still matches index
                string actualUID = await _controller._deviceManager.GetDeviceUIDAsync(dial.Index);
                
                if (actualUID != dial.UID)
                {
                    // Dial has reset or been replaced!
                    await TriggerRecoveryAsync();
                    break;
                }
                
                // Reset failure count on success
                _failureCounts[dial.UID] = 0;
            }
            catch (TimeoutException)
            {
                // Track failures
                _failureCounts.TryGetValue(dial.UID, out int count);
                _failureCounts[dial.UID] = count + 1;
                
                if (_failureCounts[dial.UID] >= 3)
                {
                    // Multiple consecutive failures - trigger recovery
                    await TriggerRecoveryAsync();
                    break;
                }
            }
        }
    }
    
    private async Task TriggerRecoveryAsync()
    {
        Debug.WriteLine("Dial configuration mismatch detected, re-provisioning...");
        
        // Re-initialize entire system
        await _controller.InitializeAsync();
        
        // Application should restore user configurations after recovery
        OnRecoveryComplete?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? OnRecoveryComplete;
}

// Usage in application
var healthMonitor = new DialHealthMonitor(vu1Controller);
healthMonitor.OnRecoveryComplete += (s, e) => {
    // Restore user dial configurations
    RestoreDialConfigurations();
};
```

### Provisioning Failures

### Known Implementation Issue: Stale Data After Power Cycle

**Symptom:**
After a power cycle, multiple dials may display the same value if not properly cleared.

**Root Cause:**
Device cache not cleared during re-discovery.

**Prevention in VUWare.Lib:**

The library prevents this by clearing the device cache during re-discovery:

```csharp
// VUWare.Lib/DeviceManager.cs
public async Task<Dictionary<string, DialState>> DiscoverDialsAsync(bool rescan = false)
{
    if (rescan)
    {
        // CRITICAL: Clear stale data before rescanning
        _dials.Clear();
        
        await BusRescanAsync();
        
        // Get fresh device map from hardware
        string response = await SendCommandAsync(CommandBuilder.GetDevicesMap());
        
        // Parse device map - only add devices reported as online
        var onlineDials = new List<byte>();
        for (int i = 0; i < response.Length; i += 2)
        {
            byte status = Convert.ToByte(response.Substring(i, 2), 16);
            if (status == 1)  // Only add if actually online
            {
                onlineDials.Add((byte)(i / 2));
            }
        }
        
        // Build fresh dial list from hardware state only
        foreach (byte dialIndex in onlineDials)
        {
            string deviceUID = await GetDeviceUIDAsync(dialIndex);
            _dials[deviceUID] = new DialState
            {
                Index = dialIndex,
                UID = deviceUID,
                Name = $"Dial {dialIndex}",
                // Fresh state only
            };
        }
    }
    
    return _dials;
}
```

**Key Implementation Points:**

1. **Always clear cache on rescan** - `_dials.Clear()` when `rescan=true`
2. **Only add confirmed devices** - Check GET_DEVICES_MAP response
3. **Use UID as primary key** - Dictionary<string UID, DialState>
4. **Never assume previous state** - Build from hardware truth each time

**Application Layer Best Practice:**

```csharp
// After power cycle or connection issues
public async Task ReconnectAndRestoreAsync()
{
    // 1. Re-initialize (clears cache, rescans, provisions)
    await vu1Controller.InitializeAsync();
    
    // 2. Restore user configurations from app storage
    var savedConfigs = LoadSavedDialConfigurations();
    
    foreach (var config in savedConfigs)
    {
        var dial = vu1Controller.GetDial(config.UID);
        if (dial != null)
        {
            // Dial still exists, restore configuration
            dial.Name = config.Name;
            await vu1Controller.SetEasingConfigAsync(config.UID, config.Easing);
        }
    }
}
```

### Database Corruption

**Note:** VUWare.Lib does not include database functionality. Applications using VUWare.Lib are responsible for:
- Storing dial configurations (UID → Name, Easing, etc.)
- Persisting user preferences
- Implementing backup/recovery strategies

**Recommended Application Pattern:**

```csharp
// Application-side persistence using JSON files, SQLite, etc.
public class DialConfigurationStore
{
    private readonly string _configPath = "dial_configs.json";
    
    public void SaveConfiguration(string uid, DialConfiguration config)
    {
        var configs = LoadAllConfigurations();
        configs[uid] = config;
        
        string json = JsonSerializer.Serialize(configs);
        File.WriteAllText(_configPath, json);
    }
    
    public Dictionary<string, DialConfiguration> LoadAllConfigurations()
    {
        if (!File.Exists(_configPath))
        {
            return new Dictionary<string, DialConfiguration>();
        }
        
        string json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<Dictionary<string, DialConfiguration>>(json) 
               ?? new Dictionary<string, DialConfiguration>();
    }
}
```

## API Key and Access Control

**Note:** VUWare.Lib is a direct hardware control library and does not implement authentication or access control. These features would be implemented at the application layer (e.g., in a REST API server or desktop application).

**If Building a Multi-User Server:**

Applications that expose dial control via network API should implement authentication. Example architecture:

### Authentication System

**Three-Level Hierarchy:**

1. **Master Key** (Level 99)
   - Full system access
   - Can create/delete other keys
   - Can manage all dials

2. **Admin Keys** (Level 10-98)
   - Can manage assigned dials
   - Cannot create new keys

3. **Standard Keys** (Level 1-9)
   - Limited read/write access
   - Access to specific dials only

### Example Database Schema
