# VUWare for Linux

This directory contains the independent Linux implementation of the VUWare dial library and console application.

## Current milestone

Implemented:

- .NET 10 VU1 protocol and dial-control library.
- Linux USB serial discovery for the VU1 VID/PID (`0403:6015`).
- Explicit `/dev/ttyUSB*` and stable `/dev/serial/by-id/*` connections.
- Dial discovery, position, backlight, easing, and e-paper image transfer.
- PNG, JPEG, and BMP normalization through SkiaSharp.
- Interactive Linux console with `devices` and `doctor` diagnostics.
- CPU load and temperature plus primary AMD GPU load and temperature.
- Live mapping of a Linux measurement to a physical dial.
- Unit tests for protocol and image behavior.

Deferred:

- NVIDIA/NVML, Intel GPU load, multi-GPU selection, and additional sensors.
- Persistent sensor-to-dial configuration and automatic startup monitoring.
- Desktop UI, service mode, and distribution packages.

See [TECH_STACK_EVALUATION.md](TECH_STACK_EVALUATION.md) for the decisions and roadmap.

## Requirements

- Linux on x64 or ARM64.
- .NET 10 SDK for building.
- VU1 Gauge Hub connected over USB.
- Read/write access to the serial device. Most distributions grant this through `dialout` or `uucp` group membership.

## Build and test

From the repository root:

```sh
dotnet restore Linux/VUWare.slnx
dotnet build Linux/VUWare.slnx --configuration Release
dotnet test Linux/VUWare.slnx --configuration Release
```

## Run

```sh
dotnet run --project Linux/VUWare.Console/VUWare.Console.csproj
```

### Run and debug in VS Code

Install the workspace-recommended Microsoft **C# Dev Kit** extension when VS Code prompts. The repository includes shared configurations under `.vscode`:

- Press **F5** and select **Linux: Debug VUWare Console** to build and start the console with the debugger attached.
- Press **Ctrl+F5** from the Run and Debug view to start the selected configuration without stopping at breakpoints.
- Run **Tasks: Run Task → Linux: run console** to run the interactive console in a dedicated terminal.
- Run **Tasks: Run Build Task** to build the Linux solution in Debug configuration.
- Run **Tasks: Run Test Task** to execute the Linux test suite.

The debug configuration uses the integrated terminal so the REPL accepts commands and Ctrl+C correctly. Set breakpoints in either the console or library source before pressing F5.

At the prompt, run:

```text
doctor
devices
sensors
connect
init
dials
```

An explicit stable path may be supplied when auto-detection is unsuitable:

```text
connect /dev/serial/by-id/<device-name>
```

## Linux measurements

The `VUWare.Sensors.Linux` module reads stable Linux kernel interfaces directly. It does not parse the output of `sensors`, `rocm-smi`, or other commands:

- `cpu-load`: aggregate CPU-time deltas from `/proc/stat`.
- `cpu-temp`: AMD `k10temp` hwmon data; `Tdie` is preferred over `Tctl`.
- `gpu-load`: `gpu_busy_percent` from the primary AMD `amdgpu` DRM device.
- `gpu-temp`: AMD hwmon data; `edge` is preferred over `junction`.

The prototype selects the AMD GPU marked `boot_vga`; if none is marked, it uses a deterministic PCI-address order. PCI addresses provide stable identity even when `cardN` and `hwmonN` indices change. Readings are sampled once per second. Missing measurements are reported as unavailable rather than as zero.

List readings and their selected kernel source paths:

```text
sensors
```

Continuously print all readings without connecting to the VU1 hub or sending any dial commands. The optional interval is in seconds and defaults to one second:

```text
watch
watch 0.5
```

Press Ctrl+C to stop watching and return to the prompt.

After connecting and initializing the VU1 hub, drive a dial from a measurement:

```text
monitor <dial-uid> cpu-load
monitor <dial-uid> cpu-temp 20 100
monitor <dial-uid> gpu-load
monitor <dial-uid> gpu-temp 20 110
```

Press Ctrl+C to stop monitoring and return to the prompt. Repeated SIGINT events from some integrated terminals are ignored once monitoring has stopped, so they cannot also close the REPL. Use `exit` to close the console.

On CachyOS, the kernel normally loads `k10temp` and `amdgpu` automatically. Standard hwmon attributes are world-readable. If a value is unavailable, use the source path printed by `sensors` and verify that the corresponding driver and sysfs file exist. The `sensors` command from the optional `lm_sensors` package can be used for comparison, but it is not a runtime dependency.

## Serial permissions

If `doctor` finds the hub but reports insufficient access, inspect the device ownership and add the user to the appropriate serial-device group for the distribution. Sign out and back in after changing group membership. A packaged udev rule is intentionally deferred until policies have been tested on supported distributions.

## Projects

- `VUWare.Lib`: protocol, serial transport/discovery, device state, controller, and image conversion.
- `VUWare.Sensors.Linux`: procfs/sysfs sensor provider for CPU and AMD GPU telemetry.
- `VUWare.Console`: interactive dial-control console.
- `VUWare.Tests`: hardware-independent unit tests.
