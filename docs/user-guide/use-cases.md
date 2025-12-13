# Common Use Cases

This guide provides real-world configuration examples for monitoring common system sensors with VUWare.

## CPU Temperature Monitoring

Monitor your CPU temperature to ensure it stays within safe operating limits.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | CPU Temperature | |
| **Sensor** | CPU [#0]: [Your CPU Model] | Find in HWInfo64 |
| **Entry** | CPU (Tctl/Tdie) | Or "CPU Package" |
| **Min Value** | 20°C | Typical idle temperature |
| **Max Value** | 95°C | Maximum safe temperature |
| **Warning Threshold** | 75°C | Starts to get warm |
| **Critical Threshold** | 88°C | Too hot - check cooling |
| **Normal Color** | Green | Operating normally |
| **Warning Color** | Orange | Getting warm |
| **Critical Color** | Red | Overheating risk |

### Interpretation

- **0-20%** (20-35°C): Idle, excellent cooling
- **20-40%** (35-50°C): Light load, normal
- **40-73%** (50-75°C): Moderate load, normal
- **73-99%** (75-88°C): Heavy load, warm (orange)
- **99-100%** (88-95°C): Very heavy load, hot (red)

!!! tip "Adjusting for Your CPU"
    - Check your CPU's TJMax (maximum junction temperature)
    - Set max value to TJMax or slightly below
    - AMD: typically 95°C
    - Intel: typically 100°C

---

## GPU Temperature

Monitor graphics card temperature during gaming or rendering.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | GPU Temperature | |
| **Sensor** | GPU [#0]: [Your GPU Model] | |
| **Entry** | GPU Temperature | |
| **Min Value** | 25°C | Idle temperature |
| **Max Value** | 85°C | Maximum safe |
| **Warning Threshold** | 75°C | Getting warm |
| **Critical Threshold** | 80°C | Too hot |
| **Normal Color** | Blue | Cool |
| **Warning Color** | Orange | Warm |
| **Critical Color** | Red | Hot |

### Interpretation

- **0-29%** (25-43°C): Idle or light load
- **29-59%** (43-68°C): Gaming, normal
- **59-88%** (68-80°C): Heavy rendering (orange)
- **88-100%** (80-85°C): Maximum load (red)

---

## CPU Usage Percentage

Monitor overall CPU utilization.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | CPU Usage | |
| **Sensor** | CPU [#0]: [Your CPU Model] | |
| **Entry** | Total CPU Usage | Or "CPU Core #0 Usage" |
| **Min Value** | 0% | Idle |
| **Max Value** | 100% | Full utilization |
| **Warning Threshold** | 80% | High load |
| **Critical Threshold** | 95% | Maxed out |
| **Normal Color** | Green | Available capacity |
| **Warning Color** | Yellow | High usage |
| **Critical Color** | Red | Bottlenecked |

### Interpretation

- **0-80%**: Normal operation (green)
- **80-95%**: High CPU load, may affect performance (yellow)
- **95-100%**: CPU bottleneck, system may slow down (red)

---

## GPU Usage Percentage

Track graphics card utilization during gaming or rendering.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | GPU Load | |
| **Sensor** | GPU [#0]: [Your GPU Model] | |
| **Entry** | GPU Core Load | Or "GPU Usage" |
| **Min Value** | 0% | Idle |
| **Max Value** | 100% | Full load |
| **Warning Threshold** | 85% | Heavy load |
| **Critical Threshold** | 98% | Maxed out |
| **Normal Color** | Cyan | Headroom available |
| **Warning Color** | Orange | Working hard |
| **Critical Color** | Red | Bottlenecked |

### Interpretation

- **0-85%**: Normal, GPU has headroom (cyan)
- **85-98%**: GPU working at capacity (orange)
- **98-100%**: Fully utilized, may be bottleneck (red)

---

## Fan Speed (RPM)

Monitor case or CPU fan speed to ensure adequate cooling.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | CPU Fan Speed | |
| **Sensor** | Motherboard: [Your Motherboard] | |
| **Entry** | CPU Fan | Or "Fan #1" |
| **Min Value** | 0 RPM | Fan off or minimum |
| **Max Value** | 3000 RPM | Your fan's max speed |
| **Warning Threshold** | 2400 RPM | Running fast |
| **Critical Threshold** | 2800 RPM | Near maximum |
| **Normal Color** | Green | Normal speed |
| **Warning Color** | Orange | High speed |
| **Critical Color** | Red | Very high speed |

!!! info "Determining Max RPM"
    Check your fan's specifications or observe the maximum RPM in HWInfo64 during stress testing.

### Interpretation

- **0-80%** (0-2400 RPM): Normal operation (green)
- **80-93%** (2400-2800 RPM): System under load (orange)
- **93-100%** (2800-3000 RPM): Maximum cooling engaged (red)

---

## CPU Power Consumption

Monitor processor power draw (requires supported hardware).

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | CPU Power | |
| **Sensor** | CPU [#0]: [Your CPU Model] | |
| **Entry** | CPU Package Power | Or "CPU Core Power" |
| **Min Value** | 0W | Idle |
| **Max Value** | 200W | Your CPU's TDP or max |
| **Warning Threshold** | 150W | High consumption |
| **Critical Threshold** | 180W | Near TDP limit |
| **Normal Color** | Green | Efficient |
| **Warning Color** | Yellow | Power-hungry task |
| **Critical Color** | Red | Maximum draw |

!!! tip "Set Max to Your CPU's TDP"
    Check your CPU specifications for TDP (Thermal Design Power) and use that as the max value.

### Interpretation

- **0-75%** (0-150W): Normal operation
- **75-90%** (150-180W): Heavy workload (yellow)
- **90-100%** (180-200W): Maximum power draw (red)

---

## GPU Power Consumption

Track graphics card power usage during gaming or rendering.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | GPU Power | |
| **Sensor** | GPU [#0]: [Your GPU Model] | |
| **Entry** | GPU Power | |
| **Min Value** | 0W | Idle |
| **Max Value** | 320W | Your GPU's TDP |
| **Warning Threshold** | 280W | High power |
| **Critical Threshold** | 300W | Near limit |
| **Normal Color** | Green | Efficient |
| **Warning Color** | Orange | Power-hungry |
| **Critical Color** | Red | Maximum |

---

## Memory Usage (RAM)

Monitor system memory utilization.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | RAM Usage | |
| **Sensor** | System: Memory | |
| **Entry** | Memory Usage | Or "Physical Memory Used %" |
| **Min Value** | 0% | Nothing loaded |
| **Max Value** | 100% | All RAM used |
| **Warning Threshold** | 85% | High usage |
| **Critical Threshold** | 95% | Nearly full |
| **Normal Color** | Green | Available |
| **Warning Color** | Yellow | Getting full |
| **Critical Color** | Red | Almost full |

### Interpretation

- **0-85%**: Plenty of RAM available (green)
- **85-95%**: High usage, may start swapping (yellow)
- **95-100%**: Memory constrained (red)

---

## Network Speed (Download)

Monitor network download speed (if supported by motherboard).

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | Network Down | |
| **Sensor** | Network: [Your Network Adapter] | |
| **Entry** | Download Speed | In MB/s |
| **Min Value** | 0 MB/s | Idle |
| **Max Value** | 125 MB/s | 1 Gigabit max (adjust) |
| **Warning Threshold** | 100 MB/s | High usage |
| **Critical Threshold** | 120 MB/s | Saturated |
| **Normal Color** | Cyan | Available bandwidth |
| **Warning Color** | Yellow | Busy |
| **Critical Color** | Red | Maxed out |

!!! info "Adjust for Your Connection"
    - 1 Gigabit = ~125 MB/s
    - 100 Megabit = ~12.5 MB/s
    - 2.5 Gigabit = ~312.5 MB/s

---

## Storage Temperature (SSD/NVMe)

Monitor drive temperature to prevent thermal throttling.

### Configuration

| Setting | Value | Notes |
|---------|-------|-------|
| **Display Name** | SSD Temperature | |
| **Sensor** | Drive: [Your Drive Model] | |
| **Entry** | Drive Temperature | |
| **Min Value** | 25°C | Idle |
| **Max Value** | 80°C | Thermal throttle limit |
| **Warning Threshold** | 65°C | Getting warm |
| **Critical Threshold** | 75°C | Hot |
| **Normal Color** | Green | Cool |
| **Warning Color** | Orange | Warm |
| **Critical Color** | Red | Throttling risk |

---

## Multi-Dial Setup Examples

### Gaming PC Setup

| Dial | Monitor | Purpose |
|------|---------|---------|
| **Dial 1** | CPU Temperature | Ensure CPU stays cool |
| **Dial 2** | GPU Temperature | Prevent GPU overheating |
| **Dial 3** | CPU Usage % | Check for CPU bottleneck |
| **Dial 4** | GPU Usage % | Check for GPU bottleneck |

### Workstation Setup

| Dial | Monitor | Purpose |
|------|---------|---------|
| **Dial 1** | CPU Power | Track power efficiency |
| **Dial 2** | GPU Power | Monitor render workload |
| **Dial 3** | RAM Usage | Prevent memory swapping |
| **Dial 4** | SSD Temperature | Avoid thermal throttling |

### Overclocker Setup

| Dial | Monitor | Purpose |
|------|---------|---------|
| **Dial 1** | CPU Temperature | Monitor OC stability |
| **Dial 2** | CPU Package Power | Track power limits |
| **Dial 3** | CPU Core Voltage | Watch voltage levels |
| **Dial 4** | CPU Fan Speed | Ensure cooling is adequate |

### Quiet PC Setup

| Dial | Monitor | Purpose |
|------|---------|---------|
| **Dial 1** | CPU Temperature | Keep temps low for quiet fans |
| **Dial 2** | GPU Temperature | Prevent fan ramp-up |
| **Dial 3** | CPU Fan Speed | Monitor noise levels |
| **Dial 4** | Case Fan Speed | Ensure adequate airflow |

---

## Tips for Choosing Sensors

### Temperature Sensors
- Use °C (Celsius) for consistency
- Set min to typical idle value
- Set max to thermal limit
- Warning at 80% of max is usually good

### Usage/Load Sensors
- Min is almost always 0%
- Max is almost always 100%
- Warning at 80-85% is typical
- Critical at 95%+ for bottleneck detection

### RPM Sensors
- Check fan specifications for max RPM
- Warning at 80% of max RPM
- Critical at 90-95% of max RPM
- Low RPM might indicate fan failure

### Power Sensors
- Use TDP (Thermal Design Power) as max
- Warning at 75-80% of TDP
- Critical at 90-95% of TDP
- Helps identify power-limited scenarios

### Voltage Sensors
- Requires careful calibration
- Check CPU/GPU specifications
- Set tight thresholds for OC safety
- Monitor for voltage droop under load
