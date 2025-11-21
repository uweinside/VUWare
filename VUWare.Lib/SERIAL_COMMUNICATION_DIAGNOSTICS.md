# VUWare Serial Communication Troubleshooting Guide

## Current Issue

The `set` command is timing out (5410ms) when trying to set dial position, while the `dial` query command (which reads information) works fine.

**Symptoms:**
- ? Connection works
- ? Initialization works
- ? Dial discovery works
- ? Dial queries work (reading data)
- ? Dial control fails (writing data) - TIMEOUT

## Root Cause Analysis

### What We Know

1. **Discovery works** - The hub is responding to `RESCAN_BUS` and `GET_DEVICES_MAP` commands
2. **Queries work** - `GET_DEVICE_UID`, `GET_FW_INFO`, etc. return data
3. **Control fails** - `SET_DIAL_PERC_SINGLE` command times out

### Likely Issues

1. **Command Format Problem**
   - The set command might not be formatted correctly
   - The hub might expect a different response format

2. **Response Format Problem**
   - The response from a SET operation might be different than a GET operation
   - Status codes might not be formatted as expected

3. **Hub Firmware Issue**
   - The hardware might not support this command
   - There might be a firmware bug

4. **Timeout Too Short**
   - The 1000ms timeout in `SetDialPercentageAsync` might be insufficient
   - The hub might need more time for SET operations

## What We Changed

### Enhanced SerialPortManager (VUWare.Lib/SerialPortManager.cs)

**Improvements:**
1. Better response parsing that doesn't rely on `\r\n` terminators
2. Calculates expected response length from header instead of waiting for line terminator
3. Added comprehensive debug logging for all serial operations
4. Proper buffer management
5. Better timeout handling with Stopwatch

**Key Changes:**
```csharp
// Old: Waited for "\r\n" terminator
// New: Calculates expected length from protocol header
int dataLength = int.Parse(lengthStr, System.Globalization.NumberStyles.HexNumber);
int expectedLength = 9 + (dataLength * 2);
```

### Enhanced Console Output (VUWare.Console/Program.cs)

**New Diagnostic Information:**
- Shows dial index
- Shows connection status during operation
- Shows last communication time
- Better troubleshooting hints

## How to Test the Fix

### Step 1: Check Debug Output

1. Run the console app in Visual Studio
2. Open View ? Output (or press Ctrl+Alt+O)
3. Select "Debug" in the output pane dropdown
4. Run the commands again
5. Look for `[SerialPort]` debug messages

You should see output like:
```
[SerialPort] Sending command: >03020002039032
[SerialPort] Received response: <0305000000
```

### Step 2: Test Set Command with Diagnostics

```
> set 31003F000650564139323920 50
```

Watch the Output window for:
- Command being sent
- Response being received
- Any errors

### Step 3: Check Response Format

The response to a SET command should be:
```
<CCDDLLLL[DATA]
```

For a successful SET (status code OK):
```
<03050000  (CC=03, DD=05 (StatusCode), LLLL=0000, data=0000 for OK)
```

## Detailed Protocol Information

### Command: SET_DIAL_PERC_SINGLE (0x03)

**Request:**
```
>03 02 0002 03 32
 |  |  |    |  |
 |  |  |    |  ?? Percentage (50 decimal = 0x32 hex)
 |  |  |    ?????? Dial Index
 |  |  ??????????? Data Length (2 bytes)
 |  ?????????????? Data Type (0x02 = SingleValue)
 ????????????????? Command Code (0x03)
```

**Expected Response:**
```
<03 05 0000
 |  |  |
 |  |  ????????????? Data Length (0 bytes = no extra data)
 |  ????????????????  Data Type (0x05 = StatusCode)
 ??????????????????? Echo of Command Code
```

Followed by status code (if DataType = StatusCode):
- `0000` = Success (OK)
- `0001` = Failure
- Other codes = Various errors

## Testing Steps

### Test 1: Manual Serial Communication

If you want to test the serial port directly:

```csharp
// Using Windows SerialPort class directly
SerialPort port = new SerialPort("COM3", 115200);
port.Open();
port.WriteLine(">03020002039032");  // Set dial 3 to 50%
string response = port.ReadLine();
Console.WriteLine("Response: " + response);
port.Close();
```

### Test 2: Check Individual Steps

```
> disconnect
> connect
> init
> dials
> dial 31003F000650564139323920   // Should work
> set 31003F000650564139323920 25 // If this fails, issue is in SET command
```

### Test 3: Try Different Values

```
> set 31003F000650564139323920 0   // Minimum
> set 31003F000650564139323920 100 // Maximum
> set 31003F000650564139323920 50  // Mid-range
```

## Possible Solutions

### Solution 1: Increase Timeout

If the issue is a slow response, try increasing the timeout:

In `DeviceManager.cs`, find:
```csharp
string response = await SendCommandAsync(command, 1000);
```

Change to:
```csharp
string response = await SendCommandAsync(command, 5000);  // 5 second timeout
```

### Solution 2: Check Command Format

Verify the command being sent is correct:

1. Add a breakpoint in `CommandBuilder.SetDialPercentage()`
2. Check the returned command string
3. Verify it matches the protocol specification

### Solution 3: Check Hub Status

The hub might be busy or in an error state:

```
> status
```

Look for:
- Connection: ACTIVE
- Initialization: INITIALIZED
- Dial Count: Should show your dials

### Solution 4: Hardware Issue

If none of the above work:

1. Check I2C cable connections
2. Verify dial is powered
3. Try `color` command (also a SET operation):
   ```
   > color 31003F000650564139323920 red
   ```
4. If both SET commands fail, issue is in hub firmware/hardware

## Diagnostic Commands

Run these to gather more information:

```
# Check connection
> status

# Check dial is discovered
> dials

# Check dial details
> dial 31003F000650564139323920

# Try to change color (another SET operation)
> color 31003F000650564139323920 red

# Try to set different positions
> set 31003F000650564139323920 10
> set 31003F000650564139323920 90
```

## Debug Output Interpretation

### Good Signs
```
[SerialPort] Sending command: >03020002039032
[SerialPort] Received response: <0305000000
```
= Command sent and response received

### Bad Signs
```
[SerialPort] Sending command: >03020002039032
[SerialPort] Timeout waiting for response after 1000ms
```
= No response received

```
[SerialPort] Read error: ...
```
= Serial port error

## Next Steps

1. **Rebuild the solution** - This applies the fixes
2. **Run the console app** fresh
3. **Try the `set` command again**
4. **Check Debug Output** for `[SerialPort]` messages
5. **If still failing**, share the Debug Output messages

## Files Modified

1. **VUWare.Lib/SerialPortManager.cs**
   - Enhanced response parsing
   - Better timeout handling
   - Debug logging

2. **VUWare.Console/Program.cs**
   - Enhanced SetDial error reporting
   - Better diagnostic messages

## Implementation Notes

### Response Parsing Logic

**Old Logic:**
- Wait for `\r\n` terminator
- Problem: Might not be sent by hub

**New Logic:**
1. Find start character `<`
2. Read header (9 bytes): `<CCDDLLLL`
3. Parse data length from positions 5-8
4. Calculate expected message length: 9 + (dataLength * 2)
5. Return when message is complete

**Example:**
```
Message: <03050000
Header: <0305 = CC=03, DD=05, LLLL=0000
DataLength = 0x0000 = 0 bytes
ExpectedLength = 9 + (0 * 2) = 9
Return immediately when 9 bytes received
```

## Additional Resources

- **VUWare.Lib/QUICK_REFERENCE.md** - API reference
- **VUWare.Lib/README.md** - Library documentation
- **SerialPortManager.cs** - Serial communication code
- **CommandBuilder.cs** - Command format specifications

## Support

If the issue persists:

1. Check the Debug Output window in Visual Studio
2. Share the `[SerialPort]` debug messages
3. Verify dial firmware version (from `dial <uid>` output)
4. Check I2C cable connections
5. Try power cycling the hub

---

**Last Updated:** 2025-01-21  
**Status:** Diagnostic build with enhanced logging
