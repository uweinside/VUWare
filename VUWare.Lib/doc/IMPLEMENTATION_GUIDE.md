# VU-Server Implementation Guide

## Overview

This document provides detailed implementation information about the VU1 dial system architecture, including device discovery, addressing mechanisms, data persistence, and the complete lifecycle of dial management.

## System Architecture

### Three-Layer Architecture

```
┌─────────────────────────────────────────────────────┐
│           Application Layer (REST API)              │
│         - Server Handler (server.py)                │
│         - Dial Handler (server_dial_handler.py)     │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│      Communication Layer (Serial Protocol)          │
│         - Dial Driver (dial_driver.py)              │
│         - Serial Driver (serial_driver.py)          │
└─────────────────────────────────────────────────────┘
                        ↕
┌─────────────────────────────────────────────────────┐
│         Hardware Layer (USB/I2C)                     │
│    - Gauge Hub (USB-Serial Bridge)                  │
│    - I2C Bus (up to 100 dial devices)               │
└─────────────────────────────────────────────────────┘
```

### Component Responsibilities

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
- Server implementation calls provision 3 times for reliability

**Server Implementation:**
```python
def provision_dials(self, num_attempts=3):
    for _ in range(num_attempts):
        self.dial_driver.provision_dials()
        sleep(0.2)
    self._reload_dials(True)
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

**Server Implementation:**
```python
def get_dial_list(self, rescan=False):
    if rescan:
        self.bus_rescan()
        resp = self._sendCommand(COMM_CMD_GET_DEVICES_MAP, COMM_DATA_NONE)
        resp = textwrap.wrap(resp, 2)  # Split into 2-char chunks
        
        onlineDials = []
        for key, elem in enumerate(resp):
            if int(elem, 16) == 1:
                onlineDials.append(key)
        
        for dialIndex in onlineDials:
            deviceUID = self.dial_get_uid(dialIndex)
            self.dials[dialIndex] = {
                'index': str(dialIndex),
                'uid': deviceUID,
                # ...additional fields
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
        if line and line.startswith('<'):  # Response found!
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

**Server Initialization Process:**

```
1. Load server configuration from config.yaml
   - Hostname, port, timeouts
   - Hardware COM port (optional)
   - Master API key

2. Initialize SQLite database
   - Create tables if missing
   - Load existing dial records

3. Connect to Gauge Hub
   - Auto-detect: Scan for VID:0x0403, PID:0x6015
   - Manual: Use port from config.yaml
   - Timeout: 2 seconds

4. Discover dials (initial provisioning if needed)
   - Call RESCAN_BUS
   - Call PROVISION_DEVICE (3 times)
   - Call GET_DEVICES_MAP
   - For each online dial:
     - Read UID from hardware
     - Look up UID in SQLite database
     - If UID exists: Load stored metadata (name, easing, etc.)
     - If UID is new: Create default database entry
     - Map UID to current runtime index
     - Read firmware/hardware versions
     - Read easing configuration

5. Apply stored configuration to dials
   - Send easing settings from database to each dial
   - Set all dials to 0%
   - Clear/restore background images

6. Start REST API server
   - Listen on configured port
   - Begin periodic update loop (handles queued commands)
```

**Code Flow:**
```python
# From server.py and server_dial_handler.py

def __init__(self, dial_driver, server_config):
    self.dial_driver = dial_driver
    self.server_config = server_config
    
    # Load timeout config
    cfg = self.server_config.get_server_config()
    self.communication_timeout = cfg.get('communication_timeout', 3)
    
    # Discover devices and restore metadata
    self._reload_dials(rescan=True)
    
    # Apply stored configuration (from database)
    self._send_db_config_to_dials()
    
    # Initialize to safe state
    self.dial_driver.set_all_dials_to(0)

def _reload_dials(self, rescan=False):
    """Discover dials and restore their metadata from database"""
    # Get list from hardware (UID for each discovered dial)
    dials = self.dial_driver.get_dial_list(rescan)
    
    # For each discovered dial
    for dial in dials:
        # Look up UID in database (or create new entry)
        dial_info = self.server_config.append_dial_info_from_db([dial])
        
        # Now dial has metadata: name, easing settings, etc.
        # This metadata is keyed by UID, not index
        self.dials[dial['uid']] = dial
```

**Database Lookup During Discovery:**
```python
def append_dial_info_from_db(self, dial_list):
    """Merge hardware dial list with database metadata"""
    for key, dial in enumerate(dial_list):
        # Fetch or create database entry using UID
        dial_info = self.database.fetch_dial_info_or_create_default(dial['uid'])
        
        # Restore metadata from database
        dial_list[key]['dial_name'] = dial_info['dial_name']  # e.g., "CPU Usage"
        dial_list[key]['easing']['dial_step'] = dial_info['easing_dial_step']
        dial_list[key]['easing']['dial_period'] = dial_info['easing_dial_period']
        # ...etc
        
        # Store in memory with UID as key (not index!)
        self.dials[dial['uid']] = dial
    
    return dial_list
```

### Runtime State Management

**Server maintains in-memory state for each dial:**

```python
self.dials[dial_uid] = {
    'index': '0',                    # Hub index (changes with provisioning)
    'uid': '3A4B5C6D7E8F',          # Permanent unique ID (DATABASE KEY)
    'dial_name': 'CPU Usage',        # User-friendly name (FROM DATABASE)
    'value': 0,                      # Current percentage (0-100)
    'backlight': {                   # Current RGBW values (0-100 each)
        'red': 0,
        'green': 0,
        'blue': 0,
        'white': 0
    },
    'image_file': 'img_blank',       # Current background image filename
    'update_deadline': time(),       # Last communication timestamp
    'value_changed': False,          # Pending value update flag
    'backlight_changed': False,      # Pending backlight update flag
    'image_changed': False,          # Pending image update flag
    'easing': {                      # Animation settings (FROM DATABASE)
        'dial_step': 2,
        'dial_period': 50,
        'backlight_step': 5,
        'backlight_period': 100
    },
    'fw_hash': 'abc123...',          # Firmware build hash
    'fw_version': 'v1.2.3',          # Firmware version
    'hw_version': 'v1.0',            # Hardware version
    'protocol_version': 'v1'         # Protocol version
}
```

**Note:** The key `dial_uid` is used to index this dictionary, ensuring metadata follows the physical dial regardless of its current index/position.

### Complete Power Cycle Example

Here's a detailed walkthrough showing how dial metadata persists across power cycles:

**Initial Setup (First Boot):**
```
1. User connects 3 dials to hub
2. Server starts, runs provisioning
3. Dials discovered:
   - Index 0, UID "ABC123", assigned name "CPU Usage"
   - Index 1, UID "DEF456", assigned name "GPU Usage"  
   - Index 2, UID "789GHI", assigned name "RAM Usage"

4. Database entries created:
   dial_uid="ABC123", dial_name="CPU Usage", easing_dial_step=5
   dial_uid="DEF456", dial_name="GPU Usage", easing_dial_step=3
   dial_uid="789GHI", dial_name="RAM Usage", easing_dial_step=2

5. User configures via REST API:
   - "CPU Usage" dial shows 75%, red backlight
   - "GPU Usage" dial shows 50%, green backlight
   - "RAM Usage" dial shows 30%, blue backlight
```

**Power Cycle Occurs:**
```
1. Power lost, all dials reset:
   - All I2C addresses reset to 0x09
   - Dial positions reset to 0
   - Backlight colors reset to off
   - UIDs remain: "ABC123", "DEF456", "789GHI"
   - Database unchanged on server

2. User physically rearranges dials on I2C bus
   (moved dials around on the desk)
```

**System Restart (After Power Cycle):**
```
1. Server starts, connects to hub
2. Provisioning runs (3 times with 200ms delays)
3. Dials may get DIFFERENT indices due to rearrangement:
   - Index 0, UID "789GHI" (was index 2 before!)
   - Index 1, UID "ABC123" (was index 0 before!)
   - Index 2, UID "DEF456" (was index 1 before!)

4. For each discovered dial, server looks up UID in database:
   
   Dial at Index 0:
   - Read UID: "789GHI"
   - Database lookup: dial_name="RAM Usage", easing_dial_step=2
   - Restore metadata to this dial at index 0
   
   Dial at Index 1:
   - Read UID: "ABC123"
   - Database lookup: dial_name="CPU Usage", easing_dial_step=5
   - Restore metadata to this dial at index 1
   
   Dial at Index 2:
   - Read UID: "DEF456"
   - Database lookup: dial_name="GPU Usage", easing_dial_step=3
   - Restore metadata to this dial at index 2

5. Server sends configuration to dials:
   - Dial "789GHI" at index 0: Apply easing_dial_step=2
   - Dial "ABC123" at index 1: Apply easing_dial_step=5
   - Dial "DEF456" at index 2: Apply easing_dial_step=3

6. Server resets all positions to 0% (safe state)

7. Application resumes:
   - App sends: "Set CPU Usage to 75%"
   - Server looks up UID "ABC123" → finds index 1
   - Server sends command to index 1
   - Correct dial responds!
```

**Key Takeaways:**
- Physical dial position/index can change
- UID is the permanent identifier
- Database uses UID as primary key
- Metadata automatically follows the physical dial
- Applications use dial names/UIDs, never indices
- Index-to-UID mapping happens transparently in server

### Periodic Update Loop

The server uses a periodic callback to process queued updates:

**Update Priority:**
1. Dial values (position updates)
2. Backlight colors
3. Display images (large data transfers)

**Implementation:**
```python
def periodic_dial_update(self):
    """Called every ~100ms by tornado.ioloop.PeriodicCallback"""
    
    # Update dial positions
    for _, dial in self.dials.items():
        if dial['value_changed']:
            self.dial_driver.dial_single_set_percent(
                dial['index'], 
                dial['value']
            )
            dial['value_changed'] = False
            dial['update_deadline'] = time() + self.communication_timeout
    
    # Update backlights
    for _, dial in self.dials.items():
        if dial['backlight_changed']:
            self.dial_driver.dial_set_backlight(
                dial['index'],
                dial['backlight']['red'],
                dial['backlight']['green'],
                dial['backlight']['blue'],
                dial['backlight']['white']
            )
            dial['backlight_changed'] = False
            dial['update_deadline'] = time() + self.communication_timeout
    
    # Update images (if no other updates pending)
    for _, dial in self.dials.items():
        if dial['image_changed']:
            self.dial_driver.update_display(
                device=dial['index'],
                imageFile=dial['image_file']
            )
            dial['image_changed'] = False
```

**Why Queued Updates?**
- Serial communication is slow (115200 baud)
- Image transfers can take 10+ seconds
- Prevents blocking REST API responses
- Allows batching of rapid value changes

## Image Storage and Management

### Server-Side Image Storage

**Location:** `upload/` directory (relative to server root)

**Filename Convention:**
- Format: `img_[UID]`
- No file extension
- Example: `img_3A4B5C6D7E8F`

**Special Files:**
- `img_blank` - Default blank/empty background

**Persistence:**
- Images stored as files on server filesystem
- Filenames mapped to dial UIDs
- Server sends image to dial on:
  - Startup (restore last image)
  - User upload (via REST API)
  - Explicit refresh request

### Image Format and Conversion

**Display Specifications:**
- E-paper display: 200x144 pixels (confirmed)
- 1-bit color depth (black/white)
- Vertical byte packing (8 pixels per byte, MSB=top)
- Total packed size: 3600 bytes ((200×144)/8)

**Image Encoding Process:**

```python
def img_to_binary(self, img_filepath):
    """Convert image file to dial-compatible binary format"""
    
    # 1. Load image and convert to grayscale
    img = Image.open(img_filepath)
    img = img.convert("L")  # Luminance (grayscale)
    
    # 2. Transpose to column-major order
    imgData = np.asarray(img)
    imgData = imgData.T.tolist()
    
    # 3. Pack vertically: 8 pixels per byte
    buff = []
    for column in imgData:
        # Threshold: >127 = white (0), ≤127 = black (1)
        bits = [1 if pixel > 127 else 0 for pixel in column]
        
        # Pack into bytes (MSB = top pixel)
        bytes = [
            int("".join(map(str, bits[i:i+8])), 2)
            for i in range(0, len(bits), 8)
        ]
        buff.extend(bytes)
    
    return buff
```

**Bit Packing Example:**
```
Pixels (top to bottom): [255, 200, 100, 50, 0, 30, 180, 220]
Threshold at 127:       [1,   1,   0,   0,  0, 0,  1,   1]
Binary string:          "11000011"
Byte value:             0xC3 (195 decimal)
```

### Image Transfer Protocol

**Transfer Sequence:**

```
1. Clear display
   Command: DISPLAY_CLEAR
   Data: [dial_index, color_flag]
   
2. Set cursor to origin
   Command: DISPLAY_GOTO_XY
   Data: [dial_index, 0x00, 0x00, 0x00, 0x00]
   
3. Send image data in chunks
   Command: DISPLAY_IMG_DATA (repeated)
   Data: [dial_index, chunk_data...]
   Chunk size: 1000 bytes maximum
   Delay: 200ms between chunks
   
4. Trigger refresh
   Command: DISPLAY_SHOW_IMG
   Data: [dial_index]
```

**Performance Considerations:**
- 200×144 display = 28,800 pixels
- 1 bit per pixel = 3600 bytes total
- At 1000 bytes per chunk = 4 chunks (1000 + 1000 + 1000 + 600)
- At 200ms per chunk = ~0.8 seconds transfer time
- E-paper refresh = 1-2 seconds additional

**Total time to update display: ~2-3 seconds**

## Easing and Animation

### Concept

Easing controls how smoothly dials transition between values. Without easing, dials would instantly jump to new positions.

**Two Independent Easing Systems:**
1. **Dial Easing** - Needle/pointer movement
2. **Backlight Easing** - RGBW color transitions

### Easing Parameters

**Step Size** (`easing_step`)
- Percentage change per update period
- Example: `step=5` means move 5% per period
- Larger = faster transitions
- Range: 1-100

**Period** (`easing_period`)
- Milliseconds between updates
- Example: `period=50` means update every 50ms
- Smaller = smoother transitions
- Range: typically 10-1000ms

### Example Calculations

**Scenario:** Dial at 0%, commanded to 100%

**Configuration 1: Fast (step=10, period=50ms)**
```
Time 0ms:    0%
Time 50ms:   10%
Time 100ms:  20%
Time 150ms:  30%
...
Time 500ms:  100% (complete in 0.5 seconds)
```

**Configuration 2: Slow (step=2, period=100ms)**
```
Time 0ms:    0%
Time 100ms:  2%
Time 200ms:  4%
Time 300ms:  6%
...
Time 5000ms: 100% (complete in 5 seconds)
```

### Default Values

```python
# Server defaults (from database schema)
easing_dial_step = 2            # 2% per update
easing_dial_period = 50         # 50ms between updates
easing_backlight_step = 5       # 5% per update
easing_backlight_period = 100   # 100ms between updates
```

### Configuration Persistence

**Dual Storage:**
1. **Server Database** - Source of truth, restored on dial discovery
2. **Dial EEPROM** - Local copy, survives server restart

**Synchronization Flow:**
```
User Changes Easing via API
    ↓
Server Updates Database
    ↓
Server Sends Commands to Dial:
    - SET_DIAL_EASING_STEP
    - SET_DIAL_EASING_PERIOD
    - SET_BACKLIGHT_EASING_STEP
    - SET_BACKLIGHT_EASING_PERIOD
    ↓
Dial Stores in EEPROM
    ↓
Dial Uses for All Future Transitions
```

**On Server Restart:**
```
Server Starts
    ↓
Discovers Dials (via UID)
    ↓
Loads Easing Config from Database
    ↓
Sends Config to Each Dial
    ↓
Dial Overwrites EEPROM
    ↓
Ready for Operation
```

## Error Handling and Edge Cases

### Serial Communication Errors

**Timeout Handling:**
- Read timeout: 2 seconds (configurable)
- Write timeout: 2 seconds (configurable)
- On timeout: Log error, return failure
- No automatic retry at driver level

**Response Validation:**
```python
def _parseResponse(self, response):
    """Parse hub response and check for errors"""
    for line in response:
        if line.startswith('<'):
            cmd = line[1:3]
            dataType = line[3:5]
            dataLen = line[5:9]
            data = line[9:]
            
            # Check if response is a status code
            if dataType == COMM_DATA_STATUS_CODE:
                status = int(data, 16)
                if status == GAUGE_STATUS_OK:
                    return True
                else:
                    logger.error(f"Error code: {status}")
                    return False
            
            return data
    
    return False
```

### Device Offline/Disconnect

**Detection:**
- Device stops responding to commands
- Status code: `GAUGE_STATUS_DEVICE_OFFLINE` (0x0012)

**Recovery:**
1. Call `RESCAN_BUS`
2. Call `GET_DEVICES_MAP`
3. Compare with previous device map
4. Log disconnected devices
5. Remove from active dial list

### Power Cycle Detection

**Critical Finding: The current implementation does NOT detect power cycles automatically.**

The server has no mechanism to detect when dials have lost power and reset. Here's what happens:

**Server Lifecycle:**
```
Server Start
    ↓
Connect to Hub (USB serial port)
    ↓
Discover Dials (RESCAN + PROVISION + GET_DEVICES_MAP)
    ↓
Build dial list in memory
    ↓
Run periodic update loop (every ~1 second)
    ↓
Server runs continuously until stopped
```

**What Doesn't Happen:**
- No periodic re-scanning of the I2C bus
- No automatic re-provisioning
- No detection of address reset (dials reverting to 0x09)
- No validation that dial at index X still has expected UID

**Power Cycle Scenarios:**

**Scenario 1: Dials lose power while server running**
```
1. Server running, dials at indices 0, 1, 2
2. User accidentally unplugs dial power
3. All dials reset to address 0x09
4. Server continues sending commands to indices 0, 1, 2
5. Commands fail (no devices at those addresses)
6. Server logs "device offline" errors
7. Values stop updating
8. NO AUTOMATIC RECOVERY - manual intervention required
```

**Scenario 2: Server restart (dials kept powered)**
```
1. Dials remain powered at addresses 0x0A, 0x0B, 0x0C
2. Server restarts
3. Server runs provisioning (assumes all dials at 0x09)
4. Provisioning attempts fail or assign conflicting addresses
5. May result in communication errors
6. User must power-cycle dials to reset to 0x09
```

**Scenario 3: Everything restarts together**
```
1. Both server and dials power cycle
2. Server starts, runs discovery
3. All dials at 0x09 (correct state)
4. Provisioning succeeds
5. System works correctly
```

**Why This Matters:**

The server assumes:
- Initial discovery is sufficient
- Index-to-UID mapping remains valid forever
- Dials never reset during server operation

But reality:
- USB power can glitch
- Users may disconnect/reconnect devices
- Hardware issues can cause device resets
- Dials may be on separate power supply

**Detection Mechanisms (Not Implemented):**

The server COULD detect power cycles by:

1. **Periodic UID Verification**
   ```python
   # Every N seconds, verify each dial's UID
   def verify_dial_identity(dialIndex, expectedUID):
       actualUID = dial_get_uid(dialIndex)
       if actualUID != expectedUID:
           # Dial has changed or reset!
           return False
       return True
   ```

2. **Communication Failure Pattern**
   ```python
   # If multiple sequential commands fail with DEVICE_OFFLINE
   if consecutive_failures > threshold:
       # Trigger automatic re-discovery
       rescan_and_reprovision()
   ```

3. **Periodic Re-provisioning**
   ```python
   # Every N minutes, do a full bus rescan
   def periodic_discovery():
       devices_map = get_devices_map()
       if len(devices_map) != len(expected_devices):
           # Device count changed, re-provision
           provision_dials()
   ```

4. **Hub Status Query**
   ```
   # Check if hub has noticed address conflicts or errors
   # (Would require hub firmware support)
   ```

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

For a robust C# library, implement:

1. **Heartbeat/Keep-Alive**: Periodically verify dial communication
2. **UID Verification**: Periodically confirm index→UID mapping
3. **Automatic Recovery**: Re-provision on detection of communication loss
4. **Device Count Monitoring**: Track expected vs. actual device count
5. **Error Pattern Detection**: Trigger recovery on repeated failures

Example architecture:
```csharp
class DialMonitor {
    Timer verificationTimer;
    Dictionary<int, DialInfo> expectedDevices;
    
    void PeriodicVerification() {
        foreach (var dial in expectedDevices) {
            if (!VerifyDialUID(dial.Index, dial.UID)) {
                // Dial has reset or been replaced
                TriggerRecovery();
                break;
            }
        }
    }
    
    void TriggerRecovery() {
        Log.Warning("Dial configuration mismatch detected, re-provisioning...");
        RescanBus();
        ProvisionDevices();
        RestoreConfiguration();
    }
}
```

### Provisioning Failures

**Common Causes:**
- Too many dials on bus (>100)
- I2C address conflicts
- Power supply issues
- Cable/connection problems

**Mitigation:**
- Multiple provision attempts (3x with 200ms delay)
- Full bus reset between attempts
- User notification if no dials found

### Known Implementation Issue: Stale Data After Power Cycle

**Symptom:**
After a power cycle, multiple dials may display the same value. You must add devices one-by-one, re-discovering after each addition.

**Root Cause:**
Inconsistent dictionary key usage between layers:

```python
# dial_driver.py uses INDEX as key:
self.dials[dialIndex] = {...}  # Key: 0, 1, 2...

# server_dial_handler.py uses UID as key:
self.dials[dial['uid']] = dial  # Key: "ABC123...", "DEF456..."
```

**What Happens:**

```
1. Before power cycle:
   dial_driver.dials = {
       0: {uid: "ABC123", ...},
       1: {uid: "DEF456", ...},
       2: {uid: "GHI789", ...}
   }

2. Power cycle occurs, all dials reset to address 0x09

3. Provisioning starts, but only partially completes:
   - First dial provisioned → index 0, UID "ABC123"
   - dial_driver.dials[0] updated with new data
   - BUT indices 1 and 2 still contain STALE DATA

4. get_dial_list() iterates self.dials.items():
   - Returns entries for indices 0, 1, 2
   - Only index 0 is actually online!
   - Indices 1 and 2 are ghosts from previous session

5. Server tries to communicate with non-existent dials:
   - Commands to indices 1, 2 fail or hit wrong devices
   - Results in duplicate values on wrong dials
```

**Why One-By-One Works:**
- Forces `rescan=True` after each dial addition
- Each rescan queries actual hardware state (GET_DEVICES_MAP)
- But doesn't clear the stale entries in dial_driver.dials
- Works by accident because latest rescan data overwrites stale data

**Proper Fix:**
The `get_dial_list()` method should:
1. Clear `self.dials = {}` when `rescan=True`
2. Only populate with dials actually reported by hardware
3. Never return stale index-based entries

```python
def get_dial_list(self, rescan=False):
    if rescan:
        self.dials = {}  # CLEAR STALE DATA
        self.bus_rescan()
        resp = self._sendCommand(self.commands.COMM_CMD_GET_DEVICES_MAP, ...)
        
        # Only add dials actually reported by hardware
        onlineDials = []
        for key, elem in enumerate(resp):
            if int(elem, 16) == 1:
                onlineDials.append(key)
        
        # Build fresh dial list
        for dialIndex in onlineDials:
            deviceUID = self.dial_get_uid(dialIndex)
            self.dials[dialIndex] = {...}
```

**C# Implementation Note:**
When implementing your C# library, ensure:
- Clear device cache on discovery/rescan
- Only track devices confirmed by GET_DEVICES_MAP
- Use UID as primary key throughout all layers
- Maintain index-to-UID mapping separately if needed

### Database Corruption

**Detection:**
- SQLite errors on query
- Missing required tables

**Recovery:**
```python
def __init__(self, database_file='vudials.db', init_if_missing=False):
    """Initialize database, create schema if missing"""
    if not os.path.exists(self.database_file) and not init_if_missing:
        raise SystemError("Database file does not exist!")
    
    self.connection = sqlite3.connect(self.database_file)
    
    if init_if_missing:
        self._init_database()  # Create tables if missing
```

## API Key and Access Control

### Authentication System

**Three-Level Hierarchy:**

1. **Master Key** (Level 99)
   - Defined in `config.yaml`
   - Full system access
   - Can create/delete other keys
   - Can manage all dials

2. **Admin Keys** (Level 10-98)
   - Created by master key
   - Can manage assigned dials
   - Cannot create new keys

3. **Standard Keys** (Level 1-9)
   - Limited read/write access
   - Access to specific dials only

### Database Schema

```sql
CREATE TABLE api_keys (
    key_id INTEGER UNIQUE PRIMARY KEY AUTOINCREMENT,
    key_name TEXT,
    key_uid TEXT NOT NULL UNIQUE,
    key_level INTEGER
);

CREATE TABLE dial_access (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    dial_uid TEXT NOT NULL,
    key_id INTEGER NOT NULL
);
```

### Access Control Flow

```
REST API Request
    ↓
Extract API Key from Request
    ↓
Validate Key Exists in Database
    ↓
Check Key Level:
    - Level 99: Allow all operations
    - Level <99: Check dial_access table
    ↓
Verify Key Has Access to Requested Dial UID
    ↓
Process Request or Return 401 Unauthorized
```

## Implementation Checklist for C# Library

### Core Components

- [ ] **SerialPortManager**
  - USB device detection (VID:0x0403, PID:0x6015)
  - Serial port configuration (115200 8N1)
  - Thread-safe send/receive
  - Timeout handling

- [ ] **ProtocolHandler**
  - Message encoding (hex ASCII format)
  - Message decoding
  - Response parsing
  - Status code validation

- [ ] **DeviceManager**
  - Device discovery (rescan, provision, get map)
  - UID tracking
  - Index-to-UID mapping
  - State synchronization

- [ ] **CommandBuilder**
  - Type-safe command construction
  - Data type handling
  - Length calculation
  - Checksum (if needed)

- [ ] **ImageProcessor**
  - Image loading (PNG, BMP, JPEG)
  - Grayscale conversion
  - Bit packing (vertical 8-pixel bytes)
  - Chunking (1000-byte segments)

### Advanced Features

- [ ] **StateManager**
  - In-memory dial state
  - Persistence (file or database)
  - Change tracking

- [ ] **EasingController**
  - Configuration management
  - Validation (ranges, types)

- [ ] **CommandQueue**
  - Asynchronous command execution
  - Priority handling (values → backlight → images)
  - Batch operations

- [ ] **ErrorHandler**
  - Retry logic
  - Connection recovery
  - Logging

### Testing Considerations

1. **Mock Serial Port** - Test without hardware
2. **Device Simulator** - Simulate hub responses
3. **Stress Testing** - Rapid commands, large images
4. **Edge Cases** - Disconnection, invalid data, timeouts

## Firmware Update Process (Bootloader)

The system supports firmware updates via bootloader commands (0xF0-0xF8 range):

**Bootloader Commands:**
- `DG_HUB_TO_DEV_BTL_JUMP_TO_BOOTLOADER` (0xF0)
- `DG_HUB_TO_DEV_BTL_BOOTLOADER_INFO` (0xF1)
- `DG_HUB_TO_DEV_BTL_GET_CRC` (0xF2)
- `DG_HUB_TO_DEV_BTL_ERASE_APP` (0xF3)
- `DG_HUB_TO_DEV_FWUP_PACKAGE` (0xF4)
- `DG_HUB_TO_DEV_FWUP_FINISHED` (0xF5)
- `DG_HUB_TO_DEV_BTL_EXIT` (0xF6)
- `DG_HUB_TO_DEV_BTL_RESTART_UPLOAD` (0xF7)
- `DG_HUB_TO_DEV_BTL_READ_STATUS_CODE` (0xF8)

**Update Sequence:**
1. Enter bootloader mode
2. Erase application flash
3. Send firmware packages (chunked)
4. Verify CRC
5. Mark upload finished
6. Exit bootloader (reboot)

**Safety Keys:**
```
GAUGE_I2C_JUMP_TO_BTL_KEY1 = 0x53
GAUGE_I2C_JUMP_TO_BTL_KEY2 = 0x4B
```

These keys must be sent to prevent accidental bootloader entry.

## Additional Notes

### Power Management

- `COMM_CMD_DIAL_POWER` (0x0A) controls power to all dials
- Useful for power saving or emergency stop
- Power off preserves dial EEPROM data

### Debug Commands

- `COMM_CMD_DEBUG_I2C_SCAN` (0xF3) - Scan I2C bus
- Returns list of responding addresses
- Useful for diagnosing connection issues

### Configuration Reset

- `COMM_CMD_RESET_CFG` (0x12) - Reset dial to factory defaults
- Clears stored easing, calibration
- Does not affect UID or firmware

### Keep-Alive (Deprecated)

The codebase shows remnants of a keep-alive mechanism:
- `DG_HUB_TO_DEV_KEEP_ALIVE` (0x19)
- `DG_HUB_TO_DEV_SERVER_ABSENT` (0x1A)
- `DG_HUB_TO_DEV_SERVER_PRESENT` (0x1B)

**Currently disabled** - Communication timeout is used instead.

## Summary

The VU1 system uses a sophisticated multi-layer architecture:

1. **Hardware Layer**: I2C bus with dynamic addressing
2. **Protocol Layer**: ASCII-hex serial protocol
3. **State Layer**: Server-side persistence + dial EEPROM
4. **API Layer**: REST interface with key-based access

Key implementation insights:

- **All dials share factory address 0x09** - Enables interchangeable units
- **Provisioning is stateless** - I2C addresses stored in RAM, reset on power cycle
- **UIDs are permanent** - Factory-programmed in flash, never change
- **UID-based addressing** - Hub uses UID to target specific dial during provisioning
- **Dual persistence** - Server DB for discovery, dial EEPROM for config
- **Queued updates** - Prevents blocking on slow operations
- **Easing is transparent** - Dials handle animation autonomously

When implementing a C# library, focus on:
1. Robust serial communication
2. Reliable device discovery/provisioning
3. Efficient image encoding
4. Clean state management
5. Comprehensive error handling

---

*This implementation guide complements the SERIAL_PROTOCOL.md document.*
