# VU-Server Serial Protocol Documentation

## Overview

This document describes the serial protocol used to communicate with the VU1 Gauge Hub by Streacom. The protocol allows control of multiple VU-style dials with e-paper displays, including setting dial values, controlling RGB backlighting, and updating background images.

## Hardware Connection

- **USB VID**: 0x0403 (1027 decimal)
- **USB PID**: 0x6015 (24597 decimal)
- **Baud Rate**: 115200
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1
- **Timeout**: 2 seconds (configurable)

## E-Paper Display Specs (Confirmed)

| Property | Value |
|----------|-------|
| Resolution | 200 x 144 pixels |
| Bit Depth | 1-bit (black/white) |
| Packed Size | 3600 bytes ((200*144)/8) |
| Packing | Vertical (8 pixels per byte, MSB=top) |
| Threshold | Gray > 127 → bit 1 (light), ≤127 → bit 0 (dark) |
| Transfer Chunk | Up to 1000 bytes per packet |
| Typical Chunk Count | 4 (1000 + 1000 + 1000 + 600) |

Earlier reverse-engineered notes suggested 200x200 (5000 bytes). Firmware RX upper limit (5000) is larger than needed; only 3600 bytes are required for full frame.

## Protocol Format

### Message Structure

All messages follow a fixed format with ASCII-encoded hexadecimal values.

#### Request Format (Host → Hub)
```
>CCDDLLLL[DATA]\r\n
```
- `>` start char
- `CC` command byte (2 hex digits)
- `DD` data type (2 hex digits)
- `LLLL` data length (4 hex digits)
- `[DATA]` hex payload (optional)
- `\r\n` terminator

#### Response Format (Hub → Host)
```
<CCDDLLLL[DATA]\r\n
```
- `<` start char
- Remaining fields mirror request

### Configuration Constants
```
GAUGE_COMM_HEADER_LEN = 9
GAUGE_COMM_MAX_TX_DATA_LEN = 1000
GAUGE_COMM_MAX_RX_DATA_LEN = 5000  // Upper bound, not required size
GAUGE_COMM_START_CHAR = '>'
```

## Image Transfer Sequence (200x144 / 3600B)

1. Clear display (`COMM_CMD_DISPLAY_CLEAR`) – set white background
2. Set cursor origin (`COMM_CMD_DISPLAY_GOTO_XY`, x=0,y=0)
3. Send image data in ≤1000-byte chunks (`COMM_CMD_DISPLAY_IMG_DATA`), 200ms pause between chunks
4. Trigger refresh (`COMM_CMD_DISPLAY_SHOW_IMG`)

Example chunk distribution: 1000 + 1000 + 1000 + 600 bytes.

## Image Data Format

- Each byte represents 8 vertically stacked pixels in a column.
- Columns processed left→right (x=0..199).
- Within each column, pixels processed top→bottom in 8-pixel groups.
- Bit 7 (MSB) = top pixel of group, bit 0 (LSB) = bottom pixel.
- Threshold mapping: gray > 127 → 1 (light), ≤127 → 0 (dark).

## Command Adjustments Note

Any tooling or scripts expecting 5000-byte frames must be updated to validate 3600 bytes instead.

*Remaining protocol documentation unchanged below.*

---

## Data Types

| Code | Name | Description |
|------|------|-------------|
| 0x01 | COMM_DATA_NONE | No data payload |
| 0x02 | COMM_DATA_SINGLE_VALUE | Single value |
| 0x03 | COMM_DATA_MULTIPLE_VALUE | Multiple values |
| 0x04 | COMM_DATA_KEY_VALUE_PAIR | Key-value pairs |
| 0x05 | COMM_DATA_STATUS_CODE | Status code response |

## Status Codes

| Code | Name | Description |
|------|------|-------------|
| 0x0000 | GAUGE_STATUS_OK | Success |
| 0x0001 | GAUGE_STATUS_FAIL | General failure |
| 0x0002 | GAUGE_STATUS_BUSY | Device busy |
| 0x0003 | GAUGE_STATUS_TIMEOUT | Operation timeout |
| 0x0004 | GAUGE_STATUS_BAD_DATA | Invalid data |
| 0x0005 | GAUGE_STATUS_PROTOCOL_ERROR | Protocol error |
| 0x0006 | GAUGE_STATUS_NO_MEMORY | Out of memory |
| 0x0007 | GAUGE_STATUS_INVALID_ARGUMENT | Invalid argument |
| 0x0008 | GAUGE_STATUS_BAD_ADDRESS | Bad address |
| 0x0009 | GAUGE_STATUS_FORBIDDEN | Operation forbidden |
| 0x000B | GAUGE_STATUS_ALREADY_EXISTS | Already exists |
| 0x000C | GAUGE_STATUS_UNSUPPORTED | Unsupported operation |
| 0x000D | GAUGE_STATUS_NOT_IMPLEMENTED | Not implemented |
| 0x000E | GAUGE_STATUS_MALFORMED_PACKAGE | Malformed package |
| 0x0010 | GAUGE_STATUS_RECURSIVE_CALL | Recursive call detected |
| 0x0011 | GAUGE_STATUS_DATA_MISMATCH | Data mismatch |
| 0x0012 | GAUGE_STATUS_DEVICE_OFFLINE | Device offline |
| 0x0013 | GAUGE_STATUS_MODULE_NOT_INIT | Module not initialized |
| 0x0014 | GAUGE_STATUS_I2C_ERROR | I2C communication error |
| 0x0015 | GAUGE_STATUS_USART_ERROR | USART error |
| 0x0016 | GAUGE_STATUS_SPI_ERROR | SPI error |
| 0xE001 | GAUGE_STATUS_BTL_NO_DEVICE | Bootloader: No device |
| 0xE002 | GAUGE_STATUS_BTL_INVALID_STATE | Bootloader: Invalid state |
| 0xE003 | GAUGE_STATUS_BTL_INVALID_REQUEST | Bootloader: Invalid request |

## Commands

### Bus Management Commands

#### 0x0C - COMM_CMD_RESCAN_BUS
Rescans the I2C bus for connected dials.

**Request:**
```
>0C0100000000
```
- Command: 0x0C
- Data Type: 0x01 (NONE)
- Length: 0x0000

**Response:**
```
<0C05000200000
```
- Status: 0x0000 (OK)

---

#### 0x07 - COMM_CMD_GET_DEVICES_MAP
Returns a bitmap of online devices (up to 100 devices).

**Request:**
```
>070100000000
```

**Response:**
```
<0702LLLL[BITMAP]
```
- Data contains one byte per device index
- 0x01 = device online
- 0x00 = device offline
- Example: `010100` means devices 0 and 2 are online

---

#### 0x08 - COMM_CMD_PROVISION_DEVICE
Provisions new devices on the bus (assigns I2C addresses).

**Request:**
```
>080100000000
```

**Response:**
```
<0805000200000
```
- Status: 0x0000 (OK)

---

#### 0x09 - COMM_CMD_RESET_ALL_DEVICES
Resets all devices on the bus.

**Request:**
```
>090100000000
```

**Response:**
```
<0905000200000
```

---

#### 0x0A - COMM_CMD_DIAL_POWER
Controls power to all dials.

**Request:**
```
>0A020001[VALUE]
```
- Data: 0x01 = power on, 0x00 = power off
- Example: `>0A02000101` (power on)

**Response:**
```
<0A05000200000
```

---

### Device Information Commands

#### 0x0B - COMM_CMD_GET_DEVICE_UID
Gets the unique ID of a specific dial.

**Request:**
```
>0B020001[INDEX]
```
- INDEX: Dial index (0-99)
- Example: `>0B02000100` (get UID of dial 0)

**Response:**
```
<0B02000C[UID_HEX]
```
- UID is 12 bytes (24 hex characters)
- Example: `<0B02000C313233343536373839414243` (UID: "123456789ABC")

---

#### 0x19 - COMM_CMD_GET_BUILD_INFO
Gets firmware build hash/info.

**Request:**
```
>19020001[INDEX]
```

**Response:**
```
<1902LLLL[BUILD_INFO_HEX]
```
- Build info as hex-encoded ASCII string

---

#### 0x20 - COMM_CMD_GET_FW_INFO
Gets firmware version.

**Request:**
```
>20020001[INDEX]
```

**Response:**
```
<2002LLLL[FW_VERSION_HEX]
```
- Firmware version as hex-encoded ASCII string

---

#### 0x21 - COMM_CMD_GET_HW_INFO
Gets hardware version.

**Request:**
```
>21020001[INDEX]
```

**Response:**
```
<2102LLLL[HW_VERSION_HEX]
```
- Hardware version as hex-encoded ASCII string

---

#### 0x22 - COMM_CMD_GET_PROTOCOL_INFO
Gets protocol version.

**Request:**
```
>22020001[INDEX]
```

**Response:**
```
<2202LLLL[PROTOCOL_VERSION_HEX]
```
- Protocol version as hex-encoded ASCII string (e.g., "v1")

---

### Dial Control Commands

#### 0x01 - COMM_CMD_SET_DIAL_RAW_SINGLE
Sets dial position using raw value (0-65535).

**Request:**
```
>0104LLLL[INDEX][VALUE_H][VALUE_L]
```
- INDEX: Dial index (1 byte)
- VALUE: 16-bit value, big-endian (2 bytes)
- Example: `>010400030027FF` (dial 0, value 10239)

**Response:**
```
<0105000200000
```

---

#### 0x03 - COMM_CMD_SET_DIAL_PERC_SINGLE
Sets dial position as percentage (0-100).

**Request:**
```
>0304LLLL[INDEX][PERCENT]
```
- INDEX: Dial index (1 byte)
- PERCENT: 0-100 (1 byte)
- Example: `>0304000200324B` (dial 0, 75%)

**Response:**
```
<0305000200000
```

---

#### 0x04 - COMM_CMD_SET_DIAL_PERC_MULTIPLE
Sets multiple dial positions at once.

**Request:**
```
>0404LLLL[INDEX0][PERC0][INDEX1][PERC1]...
```
- Pairs of INDEX and PERCENT values
- Example: `>04040004003200015A` (dial 0→50%, dial 1→90%)

**Response:**
```
<0405000200000
```

---

#### 0x05 - COMM_CMD_SET_DIAL_CALIBRATE_MAX
Calibrates dial maximum position.

**Request:**
```
>0504LLLL[INDEX][VALUE_32BIT]
```
- INDEX: Dial index (1 byte)
- VALUE: 32-bit calibration value, big-endian (4 bytes)
- Example: `>050400050000010000` (dial 0, value 65536)

**Response:**
```
<0505000200000
```

---

#### 0x06 - COMM_CMD_SET_DIAL_CALIBRATE_HALF
Calibrates dial half position.

**Request:**
```
>0604LLLL[INDEX][VALUE_32BIT]
```
- Same format as CALIBRATE_MAX

---

### Backlight Control Commands

#### 0x13 - COMM_CMD_SET_RGB_BACKLIGHT
Sets RGBW backlight values for a dial.

**Request:**
```
>1303LLLL[INDEX][R][G][B][W]
```
- INDEX: Dial index (1 byte)
- R, G, B, W: 0-100 percent (4 bytes)
- Example: `>13030005006464640000` (dial 0, R=100, G=100, B=100, W=0)

**Response:**
```
<1305000200000
```

---

### Easing Configuration Commands

Easing controls how smoothly the dial needle and backlight transition to new values.

#### 0x14 - COMM_CMD_SET_DIAL_EASING_STEP
Sets dial easing step size (percentage change per period).

**Request:**
```
>1402LLLL[INDEX][STEP_32BIT]
```
- INDEX: Dial index (1 byte)
- STEP: 32-bit value, big-endian (4 bytes)
- Example: `>140200050000000005` (dial 0, 5% step)

**Response:**
```
<1405000200000
```

---

#### 0x15 - COMM_CMD_SET_DIAL_EASING_PERIOD
Sets dial easing period (milliseconds between steps).

**Request:**
```
>1502LLLL[INDEX][PERIOD_32BIT]
```
- INDEX: Dial index (1 byte)
- PERIOD: 32-bit milliseconds, big-endian (4 bytes)
- Example: `>150200050000000064` (dial 0, 100ms period)

**Response:**
```
<1505000200000
```

---

#### 0x16 - COMM_CMD_SET_BACKLIGHT_EASING_STEP
Sets backlight easing step size.

**Request:**
```
>1602LLLL[INDEX][STEP_32BIT]
```
- Same format as dial easing step

---

#### 0x17 - COMM_CMD_SET_BACKLIGHT_EASING_PERIOD
Sets backlight easing period.

**Request:**
```
>1702LLLL[INDEX][PERIOD_32BIT]
```
- Same format as dial easing period

---

#### 0x18 - COMM_CMD_GET_EASING_CONFIG
Gets current easing configuration for a dial.

**Request:**
```
>18020001[INDEX]
```

**Response:**
```
<18020010[DIAL_STEP_32][DIAL_PERIOD_32][BL_STEP_32][BL_PERIOD_32]
```
- Returns 16 bytes total (4x 32-bit values):
  - Bytes 0-3: Dial step (big-endian)
  - Bytes 4-7: Dial period (big-endian)
  - Bytes 8-11: Backlight step (big-endian)
  - Bytes 12-15: Backlight period (big-endian)

---

### E-Paper Display Commands

The dials have e-paper displays (likely 200x200 pixels) for showing background scales and graphics.

#### 0x0D - COMM_CMD_DISPLAY_CLEAR
Clears the display.

**Request:**
```
>0D02LLLL[INDEX][COLOR]
```
- INDEX: Dial index (1 byte)
- COLOR: 0x00 = white, 0x01 = black (1 byte)
- Example: `>0D020002000` (clear dial 0 to white)

**Response:**
```
<0D05000200000
```

---

#### 0x0E - COMM_CMD_DISPLAY_GOTO_XY
Sets cursor position for image data transfer.

**Request:**
```
>0E02LLLL[INDEX][X_H][X_L][Y_H][Y_L]
```
- INDEX: Dial index (1 byte)
- X: 16-bit X position, big-endian (2 bytes)
- Y: 16-bit Y position, big-endian (2 bytes)
- Example: `>0E02000500000000` (dial 0, position 0,0)

**Response:**
```
<0E05000200000
```

---

#### 0x0F - COMM_CMD_DISPLAY_IMG_DATA
Sends image data to the display buffer.

**Request:**
```
>0F02LLLL[INDEX][IMAGE_DATA...]
```
- INDEX: Dial index (1 byte)
- IMAGE_DATA: Binary image data (hex-encoded)
- Maximum ~1000 bytes per packet
- Image format: 1-bit per pixel, packed vertically (8 pixels per byte)
- Send data in chunks if larger than max packet size

**Image Data Format:**
- Each byte represents 8 vertical pixels
- MSB is top pixel, LSB is bottom pixel
- 1 = black pixel, 0 = white pixel
- Data is sent column by column (vertical strips)

**Response:**
```
<0F05000200000
```

---

#### 0x10 - COMM_CMD_DISPLAY_SHOW_IMG
Refreshes display to show buffered image.

**Request:**
```
>10020001[INDEX]
```
- INDEX: Dial index (1 byte)
- Example: `>1002000100` (show image on dial 0)

**Response:**
```
<1005000200000
```

---

#### 0x11 - COMM_CMD_RX_BUFFER_SIZE
Gets the dial's receive buffer size.

**Request:**
```
>11020001[INDEX]
```

**Response:**
```
<1102LLLL[BUFFER_SIZE_32BIT]
```
- Returns 32-bit buffer size in bytes

---

### Debug Commands

#### 0xF3 - COMM_CMD_DEBUG_I2C_SCAN
Scans I2C bus for debugging.

**Request:**
```
>F30100000000
```

**Response:**
```
<F302LLLL[SCAN_RESULTS]
```

---

## Complete Workflow Examples

### Example 1: Discover and Set Dial Value

```
1. Rescan bus
   TX: >0C0100000000
   RX: <0C05000200000

2. Get device map
   TX: >070100000000
   RX: <0702000401010000
   (Dials 0 and 1 are online)

3. Get UID of dial 0
   TX: >0B02000100
   RX: <0B02000C414243444546474849303132
   (UID: "ABCDEFGHI012")

4. Set dial 0 to 50%
   TX: >0304000200032
   RX: <0305000200000

5. Set backlight to red
   TX: >1303000500640000
   RX: <1305000200000
   (R=100, G=0, B=0, W=0)
```

### Example 2: Update Display Image

```
1. Clear display to white
   TX: >0D02000200000
   RX: <0D05000200000

2. Set cursor to 0,0
   TX: >0E02000500000000
   RX: <0E05000200000

3. Send image data (first chunk of 1000 bytes)
   TX: >0F0203E90000[1000_BYTES_HEX]
   RX: <0F05000200000

4. Send image data (second chunk)
   TX: >0F0203E90000[1000_BYTES_HEX]
   RX: <0F05000200000

5. Show image on display
   TX: >1002000100
   RX: <1005000200000
```

### Example 3: Configure Easing

```
1. Set dial easing: 5% per 50ms
   TX: >140200050000000005
   RX: <1405000200000
   
   TX: >150200050000000032
   RX: <1505000200000

2. Set backlight easing: 10% per 100ms
   TX: >16020005000000000A
   RX: <1605000200000
   
   TX: >170200050000000064
   RX: <1705000200000

3. Read back configuration
   TX: >18020001000
   RX: <180200100000000500000032000000A00000064
   (Returns: dial_step=5, dial_period=50, bl_step=10, bl_period=100)
```

## Implementation Notes

### C# Implementation Considerations

1. **Serial Port Configuration:**
   - Use `SerialPort` class from `System.IO.Ports`
   - Set ReadTimeout and WriteTimeout to 2000ms
   - Always append `\r\n` to transmitted messages

2. **Message Encoding:**
   - All data is hex-encoded ASCII
   - Use `BitConverter` and `ToString("X2")` for byte-to-hex conversion
   - Parse hex strings with `Convert.ToByte(str, 16)`

3. **Thread Safety:**
   - Implement locking around serial port operations
   - Only one command should be in-flight at a time

4. **Response Handling:**
   - Read until `<` character appears or timeout
   - Parse response header to determine data type
   - If data type is 0x05, check status code
   - Otherwise, extract data payload

5. **Image Data:**
   - Convert images to 1-bit grayscale
   - Pack pixels vertically (8 pixels per byte)
   - Send in 1000-byte chunks with 200ms delay between chunks
   - Call DISPLAY_SHOW_IMG after all chunks sent

6. **Error Handling:**
   - Check response status codes
   - Implement retry logic for timeout errors
   - Validate response matches expected command

## Protocol Version

- **Serial Protocol Version**: v1
- **Gauge Communication Protocol**: Compatible with VU1 by Streacom

## Additional Resources

- The hub acts as an I2C master, controlling up to 100 dial devices
- Each dial has a unique 12-byte UID
- Dials are addressed by index (0-99) after provisioning
- First available I2C address: 0x0A
- Maximum packet size consideration: ~1000 bytes for image data

---

*This documentation was reverse-engineered from the VU-Server Python implementation.*
