# ?? SET Command Diagnostic Guide

## Current Situation

The `set` command is failing after 8924ms (timeout at 5000ms + overhead).

**Pattern observed:**
- ? Connection works
- ? Init/Discovery works
- ? Dial queries work (<100ms)
- ? SET commands fail (~8 seconds)

## Hypothesis

The hub is **not responding** to the SET command at all. This could be because:

1. **Hub doesn't support the command**
2. **I2C communication issue** (dial not responding)
3. **Command format is incorrect**
4. **Hub is in an error state**

## Enhanced Diagnostics Added

I've added comprehensive debug logging to help diagnose the issue:

### Files Modified:
1. **VUWare.Lib/DeviceManager.cs** - Added logging to `SetDialPercentageAsync()`
2. **VUWare.Lib/ProtocolHandler.cs** - Added logging to `ParseResponse()`

### What the logging shows:

**In DeviceManager:**
```
[DeviceManager] SET command for dial index 0, percentage 50
[DeviceManager] Built command: >03020002009032
[DeviceManager] SET response received: <030500...
[DeviceManager] Parsed response: Command=0x03, DataType=05, Length=0000
[DeviceManager] SET command succeeded!
```

**In ProtocolHandler:**
```
[ProtocolHandler] Parsed: CC=0x03, DD=05, Length=0, RawData='0000'
[ProtocolHandler] Status code: 0x0000
```

## How to Diagnose

### Step 1: Rebuild and Run
```bash
dotnet build VUWare.sln
dotnet run --project VUWare.Console
```

### Step 2: Open Debug Output
- View ? Output (Ctrl+Alt+O)
- Select "Debug" from dropdown

### Step 3: Test and Watch Debug Output
```
> set 290063000750524834313020 50
```

Watch for these debug messages in the Output window:

**Good scenario (should see):**
```
[SerialPort] Sending command: >03020002009032
[SerialPort] Received response: <030500000000
[DeviceManager] SET command for dial index 0, percentage 50
[DeviceManager] Built command: >03020002009032
[DeviceManager] SET response received: <030500000000
[DeviceManager] Parsed response: Command=0x03, DataType=05, Length=0
[DeviceManager] SET command succeeded!
```

**Bad scenario #1 (no response):**
```
[SerialPort] Sending command: >03020002009032
[SerialPort] Timeout waiting for response after 5000ms
[DeviceManager] Failed to set dial percentage: TimeoutException: ...
```

**Bad scenario #2 (error status):**
```
[SerialPort] Sending command: >03020002009032
[SerialPort] Received response: <030500010000
[ProtocolHandler] Status code: 0x0001
[DeviceManager] SET command returned error status!
```

## Expected Command Format

For dial index 0, percentage 50:

```
Command: >03020002009032
  > = Start
  03 = Command: SET_DIAL_PERC_SINGLE
  02 = DataType: SingleValue
  0002 = DataLength: 2 bytes
  00 = Dial Index (0)
  32 = Percentage (50 decimal = 0x32 hex)
```

## Possible Issues

### Issue #1: Hub Not Responding to SET
If you see `[SerialPort] Timeout waiting for response`:
- Hub might not support SET command
- I2C bus might be stuck
- Dial might be offline
- Hub firmware might be old

**Fix:** Check I2C cables, power cycle hub

### Issue #2: Hub Returns Error Status
If you see status code like `0x0001` (Fail) or `0x0012` (DeviceOffline):
- Command was received but execution failed
- Dial might not be responsive
- I2C communication error

**Possible status codes:**
- `0x0000` = OK (success)
- `0x0001` = Fail (general failure)
- `0x0003` = Timeout
- `0x0012` = DeviceOffline
- `0x0014` = I2CError

### Issue #3: Response Parse Error
If you see `[ProtocolHandler] Failed to parse response`:
- Response format is unexpected
- Response might be corrupted
- Communication error

## Detailed Testing Steps

### Test 1: Check Command Format
```
> dial 290063000750524834313020
```

Note the "Index: 0" - this is what should be in the command.

### Test 2: Try Different Dials
```
> set 870056000650564139323920 50
> set 7B006B000650564139323920 50
> set 31003F000650564139323920 50
```

Do any of them work? Or do all fail?

### Test 3: Try Different Values
```
> set 290063000750524834313020 0
> set 290063000750524834313020 100
> set 290063000750524834313020 25
```

Different values = different hex, might help narrow down issue.

### Test 4: Check Device Status
```
> status
[Shows connection status]

> init
[Re-run discovery]

> dial 290063000750524834313020
[Check if Last Comm time updates]
```

## Debug Output Collection

When testing, **copy the Debug Output messages** and analyze:

**Good message pattern:**
```
[SerialPort] Sending command: ...
[SerialPort] Received response: ...
[DeviceManager] Built command: ...
[DeviceManager] SET response received: ...
[DeviceManager] Parsed response: ...
[DeviceManager] SET command succeeded!
```

**Bad message pattern:**
```
[SerialPort] Sending command: ...
[SerialPort] Timeout waiting for response after 5000ms
```

## What to Check

1. **Is the command being sent?**
   - Look for `[SerialPort] Sending command:`
   - Is the command format correct? (`>03...`)

2. **Is there a response?**
   - Look for `[SerialPort] Received response:`
   - Or `[SerialPort] Timeout`

3. **Is the response valid?**
   - Look for `[ProtocolHandler] Parsed:`
   - Check the status code (should be `0x0000`)

4. **Is the command succeeding?**
   - Look for `[DeviceManager] SET command succeeded!`
   - Or `[DeviceManager] SET command returned error status!`

## Next Steps

1. **Rebuild** with the new debug logging
2. **Run the console app** (restart it)
3. **Test `set` command** and watch Debug Output
4. **Share the debug messages** from the Output window
5. Based on what we see, we can diagnose the real issue

## Possible Root Causes

Based on the timeout pattern, most likely:

1. **Hub doesn't support SET commands** - but you said it worked before
2. **I2C bus issue** - dial disconnected or communication problem
3. **Hub in error state** - needs power cycle
4. **Firmware version mismatch** - old hub firmware doesn't support this command
5. **Multiple dials fighting** - when 4 dials are on bus, one might interfere

## Emergency Troubleshooting

If SET commands don't work at all:

```
> disconnect
> connect COM3     (manually specify port)
> init
> set uid 50
```

If that doesn't work:

```
> disconnect
```

Then power cycle the hub:
1. Unplug USB cable
2. Wait 5 seconds
3. Plug back in
4. In console: `> connect`

## Documentation

- See debug output when you run the test
- Share the exact messages from Output window
- Include which dial you're testing
- Include what error message you see

---

**Status:** ? Enhanced logging added and ready to diagnose

**Next:** Rebuild, test, and share debug output!
