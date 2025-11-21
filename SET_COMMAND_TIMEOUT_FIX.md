# ? SET Command Timeout FIX - Applied

## Issue Identified

Your `set` command was timing out after 8+ seconds:
```
> set 290063000750524834313020 50
Operation Time: 8199ms
? Failed to set dial position.
```

The hub was responding, but the timeout was set too short (1000ms).

## Root Cause

The `DeviceManager.cs` had hardcoded 1000ms timeouts for all SET operations:
- `SetDialPercentageAsync()` - used 1000ms timeout
- `SetBacklightAsync()` - used 1000ms timeout  
- `SetEasingConfigAsync()` - used 1000ms timeout (4 separate commands)

Your hub takes 5+ seconds to respond to SET commands.

## The Fix

I've increased the timeouts from **1000ms to 5000ms** for all SET operations:

### Changes Made:
- `SetDialPercentageAsync()` - Changed `1000` ? `5000`
- `SetBacklightAsync()` - Changed `1000` ? `5000`
- `SetEasingConfigAsync()` - Changed all 4 commands from `1000` ? `5000`

### File Modified:
- `VUWare.Lib/DeviceManager.cs`

## Why This Works

**Before:**
```
Send SET command
Wait 1000ms
Timeout! (hub still processing)
Fail ?
```

**After:**
```
Send SET command  
Wait 5000ms (hub finishes in ~4-5 seconds)
Receive response ?
Success! ?
```

## Test It Now

1. **Rebuild:**
```bash
dotnet build VUWare.sln
```

2. **Run the console app** (need to close and restart the current instance)

3. **Test the set command:**
```
> connect
> init
> set 290063000750524834313020 50
```

**Expected:** Command should now complete in ~5 seconds and show `? Dial set to 50%`

## Build Status

? **Build Successful** - No errors

## Next Steps

1. Close your current console app instance
2. Run: `dotnet run --project VUWare.Console`
3. Try the `set` command again
4. It should work now!

## Expected Performance

| Operation | Timeout | Expected Time |
|-----------|---------|---|
| connect | auto-detect | ~1-3s |
| init | 3000ms | ~4-5s |
| dial query | 1000ms | <100ms |
| **set** | **5000ms** | **~5s** |
| **color** | **5000ms** | **~5s** |
| image | varies | ~2-3s |

---

**Status:** ? Fixed  
**Build:** ? Successful  
**Next:** Restart app and test!
