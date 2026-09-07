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
- Unit tests for protocol and image behavior.

Deferred:

- Linux hardware-sensor providers and automatic sensor-to-dial monitoring.
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
connect
init
dials
```

An explicit stable path may be supplied when auto-detection is unsuitable:

```text
connect /dev/serial/by-id/<device-name>
```

## Serial permissions

If `doctor` finds the hub but reports insufficient access, inspect the device ownership and add the user to the appropriate serial-device group for the distribution. Sign out and back in after changing group membership. A packaged udev rule is intentionally deferred until policies have been tested on supported distributions.

## Projects

- `VUWare.Lib`: protocol, serial transport/discovery, device state, controller, and image conversion.
- `VUWare.Console`: interactive dial-control console.
- `VUWare.Tests`: hardware-independent unit tests.
