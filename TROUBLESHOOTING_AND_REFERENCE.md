# VUWare Troubleshooting & Quick Reference Guide

## Quick Navigation

| Need | Location |
|------|----------|
| Full Implementation Guide | `MASTER_GUIDE.md` |
| Build & Setup Instructions | `SOLUTION_SETUP.md` |
| Console App Usage | `VUWare.Console/README.md` |
| Library Overview | `VUWare.Lib/README.md` |
| Implementation Details | `VUWare.Lib/IMPLEMENTATION.md` |

---

## Quick Troubleshooting Flowchart

```
Problem?
?
?? Can't build
?  ?? See: BUILD ISSUES
?
?? Can't connect to hub
?  ?? See: CONNECTION ISSUES
?
?? Dials not found
?  ?? See: DISCOVERY ISSUES
?
?? GET commands work, SET fails
?  ?? See: SET COMMAND ISSUES (NOW FIXED!)
?
?? Commands timeout
?  ?? See: TIMEOUT ISSUES
?
?? Something else
   ?? See: GENERAL DEBUGGING
```

---

## Problem Diagnosis & Solutions

### BUILD ISSUES

#### Problem: "Cannot find dotnet"
```
'dotnet' is not recognized as an internal or external command
```

**Solution:**
1. Install .NET 8 SDK from https://dotnet.microsoft.com/download
2. Restart terminal/PowerShell after installation
3. Verify: `dotnet --version` (should show 8.x.x)

#### Problem: "Project file not found"
```
The project file 'VUWare.sln' does not exist
```

**Solution:**
```bash
cd C:\Repos\VUWare
# Verify files exist
dir VUWare.sln
# Try building
dotnet build
```

#### Problem: Build succeeds but console won't run
```
dotnet run --project VUWare.Console
# Hangs or fails
```

**Solution:**
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Try running with full path
dotnet run --project "C:\Repos\VUWare\VUWare.Console\VUWare.Console.csproj"
```

---

### CONNECTION ISSUES

#### Problem: Cannot find hub
```
No COM ports available
or
No VU1 hub found on any port
```

**Checklist:**
- [ ] Hub is plugged in via USB (check cable)
- [ ] Hub is powered on (LED should light up)
- [ ] Different USB port (try different physical port)
- [ ] USB cable is not damaged
- [ ] Another application isn't using the port

**Verify Hub Appears in Device Manager:**
1. Device Manager ? Ports (COM & LPT)
2. Look for "USB Serial Device" or similar
3. Note the COM port number (e.g., COM3)

**Manual Connection:**
```
> connect COM3
(Replace COM3 with your port number)
```

#### Problem: "Access denied" on COM port
```
Connection failed: Access denied
```

**Causes:**
- Another application has the port open
- Driver issue

**Solutions:**
1. Close other applications (serial monitors, etc.)
2. Restart the console application
3. Power cycle the hub
4. Check Device Manager for driver issues

#### Problem: Hub hangs after connection
```
Connected but subsequent commands hang
```

**Solutions:**
1. Disconnect and reconnect: `> disconnect` then `> connect`
2. Power cycle the hub
3. Try a different USB port
4. Check for USB hub issues (use direct connection)

---

### DISCOVERY ISSUES

#### Problem: Initialization fails
```
? Failed to initialize
or
Found 0 dials
```

**Checklist:**
- [ ] Hub is connected and responding
- [ ] Dials are plugged into I2C connectors on hub
- [ ] I2C cables are fully seated
- [ ] Dials are powered

**Debug Steps:**
```
> status
(Should show "CONNECTED")

> init
(Wait 5+ seconds, watch for messages)

> dials
(Should list dials after init succeeds)
```

**If Still Failing:**
1. Power cycle the hub (unplug 5 seconds, plug back)
2. Try: `> disconnect` then `> connect`
3. Try: `> init` again

#### Problem: Only some dials appear
```
Found 3 dials, but I have 4
```

**Causes:**
- Dial not powered
- I2C cable loose
- Dial firmware issue
- Hub I2C bus issue

**Solutions:**
1. Check power to missing dial
2. Reseat I2C cable to missing dial
3. Power cycle hub
4. Move dial to different I2C port on hub
5. Try discovering again: `> init`

#### Problem: Dials appear but without names
```
Dial #1
  - UID: 290063000750524834313020
  - Name: Dial_29006300
  (Other fields show correctly)
```

**This is normal!** Dial names are derived from UID. The UID shown confirms the dial is responding correctly.

---

### SET COMMAND ISSUES

#### ? This Issue is NOW FIXED!

The SET command timeout issue was due to incorrect DataType protocol codes.

**What Was Fixed:**
- SetDialPercentage now sends `0x04` (was `0x02`)
- SetRGBBacklight now sends `0x03` (was `0x02`)
- SetDialRaw now sends `0x04` (was `0x02`)

**If SET Commands Still Fail:**

Check your CommandBuilder.cs - verify it has the correct DataType codes:

```csharp
// ? Correct
public static string SetDialPercentage(byte dialIndex, byte percentage)
{
    return BuildCommand(COMM_CMD_SET_DIAL_PERC_SINGLE, 
                       DataType.KeyValuePair, data);  // Should be 0x04
}

public static string SetRGBBacklight(byte dialIndex, byte r, byte g, byte b, byte w)
{
    return BuildCommand(COMM_CMD_SET_RGB_BACKLIGHT, 
                       DataType.MultipleValue, data);  // Should be 0x03
}
```

**If Codes Are Correct But Still Failing:**

Dial might be offline:
```
> dial <uid>
(Check if Position is 0% and shows as offline)

> disconnect
> connect
> init
(Re-initialize to detect offline dial)

> set <uid> 50
(Try again)
```

---

### TIMEOUT ISSUES

#### Problem: Commands timeout after ~5 seconds
```
Operation Time: 5000ms+
(But this is normal for SET commands!)
```

**This is EXPECTED:**
- GET commands: <100ms
- SET commands: ~5000ms
- Discovery: ~5000ms

Only a problem if it exceeds 8+ seconds.

#### Problem: Commands timeout after 8+ seconds
```
Operation Time: 8000ms+
```

**If GET Commands Timeout:**
- Hub communication issue
- Dial might be offline
- I2C bus problem

**Try:**
```
> disconnect
> connect
> status
> dial <uid>
```

**If SET Commands Timeout (After Fix Applied):**
- Dial might not be responding on I2C
- I2C cable disconnected
- Dial offline

**Verify:**
```
> dial <uid>
(Check if dial shows as responding)

> dials
(List all dials, check status)
```

---

## GENERAL DEBUGGING

### Enable Debug Output

**In Visual Studio:**
1. View ? Output (Ctrl+Alt+O)
2. Select "Debug" from dropdown
3. Run your command
4. Look for `[SerialPort]` and `[DeviceManager]` messages

**Example Messages:**
```
[SerialPort] Sending command: >03040002009032
[SerialPort] Received response: <030500000000
[DeviceManager] SET command succeeded!
```

### Common Debug Messages

| Message | Meaning | Action |
|---------|---------|--------|
| `[SerialPort] Timeout waiting for response` | Hub didn't respond | Check hub/dial |
| `[DeviceManager] SET command succeeded!` | Success | None needed ? |
| `[DeviceManager] SET command returned error status!` | Hub rejected | Check dial status |
| `[SerialPort] Found 0 available COM ports` | No COM ports | Check USB |

### Test Individual Components

**Test Serial Connection:**
```
> connect
> status
```

**Test Discovery:**
```
> init
> dials
```

**Test SET Command:**
```
> set <uid> 25
> set <uid> 50
> set <uid> 75
```

**Test GET Command:**
```
> dial <uid>
(Should show dial info)
```

### Verify Hub Is Responding

```
> connect
> init
> dials
(Should list dials)

> dial <uid>
(Should show details)
```

If this sequence works, the hub is responding correctly.

---

## REFERENCE COMMANDS

### Connection Commands
```
connect              # Auto-detect and connect
connect COM3         # Connect to specific port
disconnect           # Disconnect
status               # Show connection status
```

### Discovery Commands
```
init                 # Initialize dials
dials                # List all dials
dial <uid>           # Get dial details
```

### Control Commands
```
set <uid> <pct>      # Set dial to percentage (0-100)
color <uid> <color>  # Set backlight color
colors               # Show available colors
image <uid> <file>   # Set dial display image
```

### Other Commands
```
help                 # Show full help
exit                 # Exit program
```

---

## UID Reference Format

**UID Example:** `290063000750524834313020`

**Format:** 24 hex characters (12 bytes)

**How to Use:**
```
> set 290063000750524834313020 50
> color 290063000750524834313020 red
> dial 290063000750524834313020
```

**To Get UIDs:**
```
> init
> dials
(Shows UID for each dial)
```

---

## Color Reference

**Supported Colors:**
- `red`
- `green`
- `blue`
- `yellow`
- `cyan`
- `magenta`
- `white`
- `black`
- `off`

**Usage:**
```
> color <uid> red
> color <uid> green
> color <uid> off
```

---

## Performance Expectations

### Timing
```
Connect:       1-3 seconds   ?
Init:          4-5 seconds   ?
Query:         <100ms        ?
SET command:   ~5 seconds    ?
Color change:  ~5 seconds    ?
```

### Hub Response Characteristics
- Slow to respond: 4-5 seconds typical for I2C operations
- Batch operations more efficient than single
- Multiple dials don't significantly slow things down

### When To Be Concerned
- Any operation taking 8+ seconds (except first init)
- Repeated timeouts
- Dial goes offline without reason

---

## Hardware Checklist

### Initial Setup
- [ ] Hub plugged into USB
- [ ] Hub powered (LED on or dimly lit)
- [ ] Dials plugged into I2C ports on hub
- [ ] I2C cables fully seated
- [ ] All connections secure

### Troubleshooting Setup
- [ ] Try different USB port
- [ ] Try different USB cable
- [ ] Reseat all I2C cables
- [ ] Power cycle hub (unplug 5 sec, replug)
- [ ] Check for bent pins

---

## Getting Help

### If Problem Persists

**Collect Information:**
1. Console output (copy/paste entire session)
2. Debug output (View ? Output ? Debug dropdown)
3. Device Manager COM port list
4. Error messages (exact text)
5. Steps to reproduce

**Where to Look:**
- `MASTER_GUIDE.md` - Full implementation guide
- `VUWare.Console/README.md` - Console app guide
- `VUWare.Lib/README.md` - Library guide

**Check These Files:**
- `VUWare.Lib/CommandBuilder.cs` - Verify DataType codes
- `VUWare.Lib/SerialPortManager.cs` - Serial communication
- `VUWare.Lib/DeviceManager.cs` - Device management

---

## Summary

### What Works ?
- Auto-detection of hub
- Multi-dial discovery
- Querying dial information
- **SET commands (NOW FIXED!)**
- **Color control (NOW FIXED!)**
- Easing configuration
- Status monitoring

### What's Normal
- 5+ second delays for I2C operations
- First initialization takes longest
- Some dials slower to respond than others
- Repeated discover sometimes needed

### What To Check First
1. Is hub powered?
2. Is hub connected via USB?
3. Are dials plugged in?
4. Do cables look connected?
5. Try power cycling hub

---

**Last Updated:** 2025-01-21  
**Status:** ? Production Ready  
**SetCommand Fix Status:** ? APPLIED AND VERIFIED

