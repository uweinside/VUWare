# ?? QUICK START - After Fix

## What Was Fixed

SET commands were sending wrong DataType codes (`0x02` instead of `0x04`/`0x03`), causing the hub to ignore them.

**Example:**
```
Before: >03 02 0002 0032  (Wrong - hub ignores)
After:  >03 04 0002 0032  (Correct - hub executes)
```

## How to Test

### Step 1: Rebuild
```bash
dotnet build VUWare.sln
```
Expected: `Build successful`

### Step 2: Run Console
```bash
dotnet run --project VUWare.Console
```

### Step 3: Test Commands
```
> connect
Auto-detecting VU1 hub...
? Connected to VU1 Hub!

> init
Initializing and discovering dials...
? Initialized! Found 4 dial(s).

> set 290063000750524834313020 50
Setting Dial_29006300 to 50%...
? Dial set to 50%          ? THIS SHOULD NOW WORK!
Operation Time: ~5000ms

> color 290063000750524834313020 red
Setting Dial_29006300 backlight to Red...
? Backlight set to Red     ? THIS SHOULD NOW WORK!
Operation Time: ~5000ms

> dial 290063000750524834313020
[Shows updated dial status with new position]
```

## Expected Timeline

| Operation | Time | Status |
|-----------|------|--------|
| connect | 1-3s | ? |
| init | 4-5s | ? |
| dial query | <1s | ? |
| set | ~5s | ? FIXED |
| color | ~5s | ? FIXED |

## What Changed

### File: CommandBuilder.cs

| Method | From | To |
|--------|------|-----|
| SetDialPercentage | `0x02` | `0x04` |
| SetDialRaw | `0x02` | `0x04` |
| SetDialPercentagesMultiple | `0x02` | `0x03` |
| SetRGBBacklight | `0x02` | `0x03` |

That's it! Just 4 DataType code corrections.

## Why This Works

The hub firmware expects:
- **0x04** = Key-Value pairs (dial ID ? value)
- **0x03** = Multiple values (R, G, B, W colors)
- **0x02** = Single values (queries only)

C# was using `0x02` for SET commands. Hub ignored them. Now using correct codes.

## If It Still Doesn't Work

1. **Check Debug Output:**
   - View ? Output ? Debug dropdown
   - Look for `[SerialPort]` messages
   - Should see: `[SerialPort] Received response: <03050000...`

2. **Check Hub Hardware:**
   - Is it powered?
   - Are I2C cables connected?
   - Try power cycling

3. **Check Dial Index:**
   - Run `> dials`
   - Use the correct index from output

## Build Status

? Build successful  
? Ready to test  
? All fixes applied

## Next: Test It!

The fix is in place. Now rebuild and test the SET commands. They should work!

```bash
dotnet build
dotnet run --project VUWare.Console
# Then try: > set <uid> 50
```

Good luck! ??

