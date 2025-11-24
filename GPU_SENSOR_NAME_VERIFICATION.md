# Quick GPU Sensor Name Verification Guide

## For Dials 3 & 4 (GPU Load and GPU Temperature)

Your current configuration uses very long sensor names:
```json
"sensorName": "GPU [#0]: AMD Radeon RX 9070: GIGABYTE Radeon RX 9070 GAMING OC 16G (GV-R9070GAMING OC-16GD)"
```

This is likely the issue. HWInfo64 may report a shorter or different format.

### Step 1: Identify Actual GPU Sensor Name

Run the Console app to list all available sensors:

```bash
dotnet run --project VUWare.Console
> sensors
```

Look for entries starting with **"GPU"** and find your RX 9070.

### Common HWInfo64 GPU Sensor Name Formats

For AMD Radeon cards, HWInfo64 typically reports:
- `GPU [#0]: AMD Radeon RX 9070 GAMING OC 16G`
- `GPU [#0]: GIGABYTE Radeon RX 9070`
- `GPU [#0]: AMD Radeon RX 9070`
- `GPU [#0]: Radeon RX 9070`

**NOT** the full model with part number like:
```
GPU [#0]: AMD Radeon RX 9070: GIGABYTE Radeon RX 9070 GAMING OC 16G (GV-R9070GAMING OC-16GD)
```

### Step 2: Find the Sensor Entries

Under your GPU sensor name, look for entries like:
- **GPU Utilization** or **GPU Load** (for dial 3)
- **GPU Temperature** or **GPU Core Temperature** (for dial 4)

The exact names should be in the Console app output.

### Step 3: Update Configuration

Copy the **exact** sensor name from Console output and update your config:

**Before (Likely Wrong):**
```json
{
  "dialUid": "7B006B000650564139323920",
  "displayName": "GPU Load",
  "sensorName": "GPU [#0]: AMD Radeon RX 9070: GIGABYTE Radeon RX 9070 GAMING OC 16G (GV-R9070GAMING OC-16GD)",
  "entryName": "GPU Utilization",
  ...
}
```

**After (Correct):**
```json
{
  "dialUid": "7B006B000650564139323920",
  "displayName": "GPU Load",
  "sensorName": "GPU [#0]: AMD Radeon RX 9070 GAMING OC 16G",
  "entryName": "GPU Utilization",
  ...
}
```

### Step 4: Verify with Debug Mode

1. Update the sensor names
2. Enable debug mode in config:
   ```json
   "debugMode": true
   ```
3. Run the app
4. Check Debug Output window for messages like:
   ```
   ?? GPU Load
     Sensor: GPU [#0]: AMD Radeon RX 9070 GAMING OC 16G
     Entry: GPU Utilization
     ? Status: MATCHED
   ```

If you see `? Status: NOT FOUND`, the sensor name still doesn't match.

### Troubleshooting Tips

**If "NOT FOUND":**
1. Copy the exact sensor name from Console app output
2. Paste it directly into the config (no abbreviations)
3. Make sure case matches (usually it doesn't matter, but try exactly)
4. Check the entry name is also exact

**Common AMD GPU Entry Names:**
- `GPU Utilization` or `GPU Load`
- `GPU Temperature` or `Core Temperature`
- `Memory Used` or `VRAM Utilization`
- `Core Clock` or `GPU Clock`

**Command to Get All GPU Entries:**
```bash
> sensors
# Look for GPU [#0] section
# Copy exact sensor and entry names
```

### Example Output Format

When you run `> sensors` in Console, you should see:

```
?? HWInfo64 Sensors ???????????????????????????????????????????
? [CPU [#0]: AMD Ryzen 7 9700X]
?   ?? Total CPU Usage
?   ?  Value: 25.50 %
? [GPU [#0]: AMD Radeon RX 9070 GAMING OC 16G]
?   ?? GPU Utilization
?   ?  Value: 45.00 % Min: 0.00 Max: 100.00
?   ?? GPU Temperature
?   ?  Value: 62.50 °C Min: 0.00 Max: 110.00
```

Copy the sensor name exactly as shown: `GPU [#0]: AMD Radeon RX 9070 GAMING OC 16G`

### When You Find the Right Names

Your config should look like:

```json
{
  "dialUid": "7B006B000650564139323920",
  "displayName": "GPU Load",
  "sensorName": "[EXACT FROM CONSOLE]",
  "entryName": "[EXACT FROM CONSOLE]",
  "minValue": 0,
  "maxValue": 100,
  "warningThreshold": 80,
  "criticalThreshold": 95,
  "colorConfig": {
    "normalColor": "Blue",
    "warningColor": "Yellow",
    "criticalColor": "Red"
  },
  "enabled": true,
  "updateIntervalMs": 500
}
```

### Quick Summary

**The Issue:** Sensor names in config don't match exactly what HWInfo64 reports

**The Fix:** 
1. Run Console app: `> sensors`
2. Find your GPU entries
3. Copy exact names
4. Paste into config
5. Restart app with `"debugMode": true`
6. Verify in Debug Output

**Expected Result:**
- Dials 3 & 4 show percentages
- Status changes with GPU load/temperature
- Colors change based on thresholds
