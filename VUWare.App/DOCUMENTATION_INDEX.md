# VUWare.App - Documentation Index

## Quick Navigation

### Getting Started
- **[README.md](README.md)** - Complete system guide with quick start instructions
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Overview of all completed features

### Detailed Documentation
- **[INITIALIZATION.md](INITIALIZATION.md)** - Initialization system architecture and flow
- **[MONITORING.md](MONITORING.md)** - Sensor monitoring system details
- **[SYSTEM_DIAGRAMS.md](SYSTEM_DIAGRAMS.md)** - Visual flowcharts and state diagrams

### Source Code
- **MainWindow.xaml** - UI layout definition
- **MainWindow.xaml.cs** - UI logic and event handlers
- **Services/ConfigManager.cs** - Configuration file I/O
- **Services/AppInitializationService.cs** - Startup initialization engine
- **Services/SensorMonitoringService.cs** - Real-time monitoring engine
- **Models/DialConfiguration.cs** - Configuration data structures

### Configuration
- **Config/dials-config.json** - Application configuration file

---

## Feature Overview

### ? Configuration System
Real-time sensor-to-dial mapping with JSON configuration.

**Key Features:**
- Dial UID and sensor mappings
- Min/Max value ranges for percentage calculation
- Warning and critical thresholds
- Color configurations (normal/warning/critical)
- Per-dial settings and global settings

**Files:** ConfigManager.cs, DialConfiguration.cs, dials-config.json

**See:** README.md ? Configuration File Structure

---

### ? Initialization System
Startup process with status reporting and error handling.

**Key Features:**
- 4-stage initialization (Connect ? Init ? HWInfo ? Ready)
- Non-blocking background thread
- Real-time status updates with color coding
- Comprehensive error messages
- Graceful error recovery

**Files:** AppInitializationService.cs

**See:** INITIALIZATION.md ? Initialization Flow

---

### ? Monitoring System
Continuous sensor polling and automatic dial updates.

**Key Features:**
- Real-time sensor value reading
- Automatic percentage calculation
- Threshold-based color changes
- Efficient update detection
- Background thread operation

**Files:** SensorMonitoringService.cs

**See:** MONITORING.md ? Monitoring Flow

---

### ? User Interface
Status display and real-time monitoring indicators.

**Key Features:**
- Status button with initialization progress
- Four dial buttons with live data
- Color-coded status indicators
- Rich tooltips with sensor details
- Non-blocking UI updates

**Files:** MainWindow.xaml, MainWindow.xaml.cs

**See:** README.md ? UI Elements

---

## Learning Path

### Beginner: Just Want to Use It
1. Read: [README.md](README.md) ? Quick Start section
2. Configure: Edit Config/dials-config.json
3. Run: `dotnet run --project VUWare.App`

### Intermediate: Want to Understand How It Works
1. Read: [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) ? System Architecture
2. Study: [SYSTEM_DIAGRAMS.md](SYSTEM_DIAGRAMS.md) ? Startup Sequence & Monitoring Flow
3. Explore: MainWindow.xaml.cs ? Event handlers

### Advanced: Want to Modify or Extend
1. Study: [INITIALIZATION.md](INITIALIZATION.md) ? Complete initialization flow
2. Study: [MONITORING.md](MONITORING.md) ? Monitoring architecture
3. Review: AppInitializationService.cs ? Multi-stage pattern
4. Review: SensorMonitoringService.cs ? Background thread pattern
5. Review: MainWindow.xaml.cs ? Dispatcher-based UI updates

---

## Troubleshooting Index

### Issue: App won't start
? See: README.md ? Troubleshooting ? App Won't Start

### Issue: Status button stays yellow
? See: README.md ? Troubleshooting ? Status Button Stays Yellow

### Issue: Dials not found
? See: README.md ? Troubleshooting ? Dials Not Found

### Issue: Buttons don't update
? See: README.md ? Troubleshooting ? Buttons Don't Update

### Issue: Colors don't change
? See: README.md ? Troubleshooting ? Colors Don't Change

---

## Architecture Quick Reference

```
UI (MainWindow)
  ?
Config (dials-config.json)
  ?
?? AppInitializationService (startup)
?  ?? Connect VU1 Hub
?  ?? Discover dials
?  ?? Connect HWInfo64
?  ?? Register sensors
?
?? SensorMonitoringService (continuous)
   ?? Poll sensors every 1000ms
   ?? Calculate percentages
   ?? Determine colors
   ?? Update dials

VU1 Hub (serial) ? dials
HWInfo64 (shared memory) ? sensors
```

---

## Configuration Quick Reference

Find your dial UID with Console app:
```bash
> dials
[Shows all connected dials with UIDs]
```

Find your sensor names with Console app:
```bash
> sensors
[Shows all available sensors and entries]
```

Edit Config/dials-config.json:
```json
{
  "dialUid": "290063000750524834313020",
  "sensorName": "CPU [#0]: AMD Ryzen 7 9700X: Enhanced",
  "entryName": "CPU (Tctl/Tdie)",
  "minValue": 20,
  "maxValue": 95,
  "warningThreshold": 75,
  "criticalThreshold": 88
}
```

---

## Feature Checklist

- ? Configuration loading and validation
- ? Multi-stage async initialization
- ? Status display with color coding
- ? HWInfo64 sensor polling
- ? Automatic percentage calculation
- ? Threshold-based color changes
- ? Real-time dial updates
- ? Tooltips with sensor information
- ? Background thread processing
- ? Error handling and recovery
- ? Thread-safe UI updates
- ? Comprehensive documentation

---

## Development Guide

### Adding a New Feature

1. **Configuration Changes:**
   - Update DialConfiguration.cs models
   - Update dials-config.json schema
   - Update validation logic

2. **Initialization Changes:**
   - Modify AppInitializationService.cs
   - Add status enum if needed
   - Update MainWindow event handlers

3. **Monitoring Changes:**
   - Modify SensorMonitoringService.cs
   - Update DialSensorUpdate class
   - Update MainWindow UI handlers

4. **UI Changes:**
   - Update MainWindow.xaml layout
   - Update MainWindow.xaml.cs logic
   - Ensure Dispatcher.Invoke() for thread safety

### Testing Changes

1. Build: `dotnet build`
2. Run: `dotnet run --project VUWare.App`
3. Check Status button progression
4. Verify buttons update with sensor values
5. Check tooltips for correct information

---

## Performance Tips

- **Slower Updates?** Increase `globalUpdateIntervalMs` in config
- **Faster Updates?** Decrease it (minimum 500ms recommended)
- **Reduce CPU?** Use larger update intervals
- **Reduce Serial Traffic?** Monitoring already optimizes (only updates on change)

---

## Next Enhancement Ideas

1. **Data Logging** - Save historical sensor data
2. **Graphs** - Real-time data visualization
3. **Alarms** - Alert notifications on thresholds
4. **Config UI** - Visual configuration editor
5. **More Dials** - Support 8+ dials
6. **Recording** - Session playback capability
7. **Profiles** - Multiple configuration presets
8. **Auto-Detection** - Automatic sensor discovery

---

## Support Resources

- **Console App:** Use VUWare.Console for low-level testing
- **HWInfo64:** Required for sensor monitoring
- **Documentation:** See README.md and MONITORING.md
- **Troubleshooting:** See README.md ? Troubleshooting section
- **GitHub:** https://github.com/uweinside/VUWare

---

## Files Summary

| File | Purpose | Type |
|------|---------|------|
| MainWindow.xaml | UI layout | XAML |
| MainWindow.xaml.cs | UI logic | C# |
| AppInitializationService.cs | Startup | C# |
| SensorMonitoringService.cs | Monitoring | C# |
| ConfigManager.cs | Config I/O | C# |
| DialConfiguration.cs | Config models | C# |
| dials-config.json | Configuration | JSON |
| README.md | User guide | Markdown |
| INITIALIZATION.md | Init docs | Markdown |
| MONITORING.md | Monitor docs | Markdown |
| SYSTEM_DIAGRAMS.md | Flowcharts | Markdown |
| IMPLEMENTATION_SUMMARY.md | Overview | Markdown |
| DOCUMENTATION_INDEX.md | This file | Markdown |

---

## Quick Links

- **Startup Sequence:** SYSTEM_DIAGRAMS.md ? Application Startup Sequence
- **Monitoring Loop:** SYSTEM_DIAGRAMS.md ? Sensor Value to Dial Mapping
- **Error Handling:** SYSTEM_DIAGRAMS.md ? Error Handling Flow
- **Button States:** SYSTEM_DIAGRAMS.md ? Button State Machine
- **Configuration:** README.md ? Configuration File Structure
- **Thresholds:** MONITORING.md ? Threshold-Based Colors
- **Threading:** MONITORING.md ? Thread Safety

---

## Status: ? Complete and Production Ready

All features implemented and documented. Ready for use and extension.

Last Updated: 2024
Version: 1.0
