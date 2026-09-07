using System.Globalization;
using System.Runtime.InteropServices;
using VUWare.Lib;
using VUWare.Sensors.Linux;

var cancellation = new CancellationTokenSource();
CancellationTokenSource? activeOperation = null;
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    var operation = Volatile.Read(ref activeOperation);
    if (operation is not null)
        operation.Cancel();
    else
        Console.WriteLine("\nNo operation is running. Use 'exit' to close VUWare Console.");
};

using var controller = new VU1Controller();
using var sensorProvider = new LinuxSensorProvider();
sensorProvider.Connect();
var discovery = new LinuxSerialDeviceDiscovery();
PrintBanner();
PrintHelp();

while (!cancellation.IsCancellationRequested)
{
    Console.Write("\nvuware> ");
    var line = Console.ReadLine();
    if (line is null) break;
    var arguments = ParseArguments(line);
    if (arguments.Count == 0) continue;

    try
    {
        var command = arguments[0].ToLowerInvariant();
        switch (command)
        {
            case "connect":
                var connected = arguments.Count > 1 ? controller.Connect(arguments[1]) : controller.AutoDetectAndConnect();
                WriteResult(connected, connected ? $"Connected to {controller.PortName}. Run 'init' next." : "Connection failed. Run 'doctor' for diagnostics.");
                break;
            case "disconnect":
                controller.Disconnect();
                WriteSuccess("Disconnected.");
                break;
            case "init":
                RequireConnected();
                WriteResult(await controller.InitializeAsync(cancellation.Token), $"Initialization complete; {controller.DialCount} dial(s) discovered.");
                break;
            case "status":
                Console.WriteLine($"Connected:   {controller.IsConnected}\nDevice:      {controller.PortName ?? "-"}\nInitialized: {controller.IsInitialized}\nDials:       {controller.DialCount}");
                break;
            case "devices":
                PrintDevices(discovery.FindDevices());
                break;
            case "doctor":
                PrintDoctor(discovery.FindDevices());
                break;
            case "sensors":
                await PrintSensorsAsync();
                break;
            case "watch" or "watch-sensors":
                await WatchSensorsAsync(arguments);
                break;
            case "monitor":
                RequireArguments(arguments, 3, "monitor <uid> <cpu-load|cpu-temp|gpu-load|gpu-temp> [min] [max]");
                await MonitorAsync(arguments);
                break;
            case "dials":
                RequireInitialized();
                foreach (var dial in controller.GetAllDials().Values)
                    Console.WriteLine($"{dial.UID}  {dial.Name,-18} index={dial.Index,2} position={dial.CurrentValue,3}% RGBW=({dial.Backlight.Red},{dial.Backlight.Green},{dial.Backlight.Blue},{dial.Backlight.White})");
                break;
            case "dial":
                RequireArguments(arguments, 2, "dial <uid>");
                RequireInitialized();
                var detail = RequireDial(arguments[1]);
                Console.WriteLine($"Name: {detail.Name}\nUID: {detail.UID}\nIndex: {detail.Index}\nPosition: {detail.CurrentValue}%\nBacklight: {detail.Backlight}\nLast communication: {detail.LastCommunication:O}");
                break;
            case "set":
                RequireArguments(arguments, 3, "set <uid> <0-100>");
                RequireInitialized();
                if (!byte.TryParse(arguments[2], out var percentage) || percentage > 100) throw new ArgumentException("Percentage must be between 0 and 100.");
                WriteResult(await controller.SetDialPercentageAsync(arguments[1], percentage, cancellation.Token), $"Dial set to {percentage}%.");
                break;
            case "color":
                RequireArguments(arguments, 3, "color <uid> <name>");
                RequireInitialized();
                var color = Colors.All.FirstOrDefault(item => item.Name.Equals(arguments[2], StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException("Unknown color. Run 'colors'.");
                WriteResult(await controller.SetBacklightColorAsync(arguments[1], color, cancellation.Token), $"Backlight set to {color.Name}.");
                break;
            case "colors":
                Console.WriteLine(string.Join(", ", Colors.All.Select(item => item.Name.ToLowerInvariant())));
                break;
            case "image":
                RequireArguments(arguments, 3, "image <uid> <path>");
                RequireInitialized();
                var image = ImageProcessor.LoadImageFile(arguments[2]);
                WriteResult(await controller.SetDisplayImageAsync(arguments[1], image, cancellation.Token), "Image uploaded.");
                break;
            case "test":
                RequireInitialized();
                foreach (var dial in controller.GetAllDials().Values)
                {
                    Console.WriteLine($"Testing {dial.Name}...");
                    await controller.SetDialPercentageAsync(dial.UID, 50, cancellation.Token);
                    await controller.SetBacklightColorAsync(dial.UID, Colors.Green, cancellation.Token);
                }
                WriteSuccess("Test state applied. Use 'set' and 'color off' to reset individual dials.");
                break;
            case "help":
                PrintHelp();
                break;
            case "exit" or "quit":
                cancellation.Cancel();
                break;
            default:
                WriteError($"Unknown command '{command}'. Run 'help'.");
                break;
        }
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    catch (Exception exception) { WriteError(exception.Message); }
}

static List<string> ParseArguments(string input)
{
    var result = new List<string>();
    var current = new System.Text.StringBuilder();
    var quoted = false;
    foreach (var character in input)
    {
        if (character == '"') { quoted = !quoted; continue; }
        if (char.IsWhiteSpace(character) && !quoted)
        {
            if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
            continue;
        }
        current.Append(character);
    }
    if (quoted) throw new ArgumentException("Unterminated quote.");
    if (current.Length > 0) result.Add(current.ToString());
    return result;
}

void RequireConnected()
{
    if (!controller.IsConnected) throw new InvalidOperationException("Not connected. Run 'connect' first.");
}

void RequireInitialized()
{
    if (!controller.IsInitialized) throw new InvalidOperationException("Not initialized. Run 'connect' and 'init' first.");
}

DialState RequireDial(string uid) => controller.GetDial(uid) ?? throw new KeyNotFoundException($"Dial '{uid}' was not found.");

async Task PrintSensorsAsync()
{
    if (!sensorProvider.IsConnected && !sensorProvider.Connect())
    {
        WriteError("Linux sensor provider could not start.");
        return;
    }

    await sensorProvider.WaitForFirstSampleAsync(TimeSpan.FromSeconds(3), cancellation.Token);
    foreach (var key in LinuxMetricKeys.All)
    {
        var reading = sensorProvider.GetMetric(key);
        if (reading is LinuxSensorReading linuxReading)
        {
            Console.WriteLine($"{key,-10} {reading.Value,6:0.0} {reading.Unit,-2}  min={reading.ValueMin,6:0.0} max={reading.ValueMax,6:0.0} avg={reading.ValueAvg,6:0.0}");
            Console.WriteLine($"             source: {linuxReading.Source}");
        }
        else if (sensorProvider.Diagnostics.TryGetValue(key, out var diagnostic))
        {
            Console.WriteLine($"{key,-10} unavailable  {diagnostic.Message}");
            if (diagnostic.Source is not null)
                Console.WriteLine($"             source: {diagnostic.Source}");
        }
    }
}

async Task WatchSensorsAsync(IReadOnlyList<string> arguments)
{
    var intervalSeconds = arguments.Count > 1 ? ParseNumber(arguments[1], "interval") : 1d;
    if (intervalSeconds is < 0.1 or > 60)
        throw new ArgumentOutOfRangeException(nameof(arguments), "Watch interval must be between 0.1 and 60 seconds.");
    if (!sensorProvider.IsConnected && !sensorProvider.Connect())
        throw new InvalidOperationException("Linux sensor provider could not start.");

    await sensorProvider.WaitForFirstSampleAsync(TimeSpan.FromSeconds(3), cancellation.Token);
    using var watchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
    Volatile.Write(ref activeOperation, watchCancellation);
    Console.WriteLine($"Watching all sensor readings every {intervalSeconds:0.##} second(s). No dial commands will be sent. Press Ctrl+C to stop.");
    try
    {
        while (!watchCancellation.IsCancellationRequested)
        {
            Console.WriteLine($"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
            foreach (var key in LinuxMetricKeys.All)
            {
                var reading = sensorProvider.GetMetric(key);
                if (reading is not null)
                    Console.WriteLine($"  {key,-10} {reading.Value,6:0.0} {reading.Unit,-2}  min={reading.ValueMin,6:0.0} max={reading.ValueMax,6:0.0} avg={reading.ValueAvg,6:0.0}");
                else if (sensorProvider.Diagnostics.TryGetValue(key, out var diagnostic))
                    Console.WriteLine($"  {key,-10} unavailable  {diagnostic.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), watchCancellation.Token);
        }
    }
    catch (OperationCanceledException) when (watchCancellation.IsCancellationRequested) { }
    finally
    {
        Volatile.Write(ref activeOperation, null);
        Console.WriteLine("\nSensor watch stopped. Returning to the VUWare prompt.");
    }
}

async Task MonitorAsync(IReadOnlyList<string> arguments)
{
    RequireInitialized();
    var dial = RequireDial(arguments[1]);
    var key = arguments[2].ToLowerInvariant();
    if (!LinuxMetricKeys.All.Contains(key, StringComparer.OrdinalIgnoreCase))
        throw new ArgumentException($"Unknown metric '{key}'. Run 'sensors' to list metrics.");

    var minimum = arguments.Count > 3 ? ParseNumber(arguments[3], "minimum") : 0d;
    var maximum = arguments.Count > 4 ? ParseNumber(arguments[4], "maximum") : 100d;
    if (maximum <= minimum)
        throw new ArgumentException("Maximum must be greater than minimum.");
    if (!sensorProvider.IsConnected && !sensorProvider.Connect())
        throw new InvalidOperationException("Linux sensor provider could not start.");

    await sensorProvider.WaitForFirstSampleAsync(TimeSpan.FromSeconds(3), cancellation.Token);
    using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
    Volatile.Write(ref activeOperation, monitorCancellation);
    Console.WriteLine($"Monitoring {key} on {dial.Name} ({dial.UID}) over {minimum:0.##}..{maximum:0.##}. Press Ctrl+C to stop.");
    byte? lastPercentage = null;
    try
    {
        while (!monitorCancellation.IsCancellationRequested)
        {
            var reading = sensorProvider.GetMetric(key);
            if (reading is null || DateTime.UtcNow - reading.LastUpdate > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException($"Metric '{key}' is unavailable or stale.");

            var percentage = (byte)Math.Round(Math.Clamp(
                100d * (reading.Value - minimum) / (maximum - minimum), 0d, 100d));
            if (percentage != lastPercentage)
            {
                if (!await controller.SetDialPercentageAsync(dial.UID, percentage, monitorCancellation.Token))
                    throw new IOException("Dial update failed.");
                lastPercentage = percentage;
            }

            Console.Write($"\r{key}: {reading.Value,6:0.0} {reading.Unit} -> {percentage,3}%   ");
            await Task.Delay(TimeSpan.FromSeconds(1), monitorCancellation.Token);
        }
    }
    catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested) { }
    finally
    {
        Volatile.Write(ref activeOperation, null);
        Console.WriteLine(cancellation.IsCancellationRequested
            ? "\nMonitoring stopped because the console is shutting down."
            : "\nMonitoring stopped. Returning to the VUWare prompt.");
    }
}

static double ParseNumber(string value, string name)
{
    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
        throw new ArgumentException($"Invalid {name} value '{value}'. Use invariant decimal notation.");
    return result;
}

static void RequireArguments(IReadOnlyList<string> arguments, int count, string usage)
{
    if (arguments.Count < count) throw new ArgumentException($"Usage: {usage}");
}

static void PrintDevices(IReadOnlyList<SerialDeviceInfo> devices)
{
    if (devices.Count == 0) { WriteError("No VU1 Gauge Hub matching USB 0403:6015 was found."); return; }
    foreach (var device in devices)
        Console.WriteLine($"{device.PreferredPath}\n  kernel path: {device.DevicePath}\n  VID:PID: {device.VendorId}:{device.ProductId}\n  access: {(device.IsAccessible ? "read/write" : "insufficient")}");
}

static void PrintDoctor(IReadOnlyList<SerialDeviceInfo> devices)
{
    Console.WriteLine($"OS:           {RuntimeInformation.OSDescription}");
    Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
    Console.WriteLine($"Runtime:      {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"User:         {Environment.UserName}");
    Console.WriteLine($"VU1 devices:  {devices.Count}");
    PrintDevices(devices);
    if (devices.Any(device => !device.IsAccessible))
        Console.WriteLine("Permission hint: add the user to the distribution's serial-device group (commonly 'dialout' or 'uucp'), sign in again, then retry.");
}

static void PrintBanner() => Console.WriteLine("VUWare Console for Linux (.NET 10)\nVU1 Gauge Hub dial controller");

static void PrintHelp()
{
    Console.WriteLine("""
Commands:
  devices                    List VU1 serial-device candidates
  doctor                     Diagnose runtime, discovery, and permissions
    sensors                    Show one CPU and AMD GPU sensor snapshot
    watch [seconds]            Continuously show all sensors without using dials
    monitor <uid> <metric>     Drive a dial from a sensor; Ctrl+C stops monitoring
                                                            Metrics: cpu-load, cpu-temp, gpu-load, gpu-temp
                                                            Optional: monitor <uid> <metric> <min> <max>
  connect [device]           Auto-detect or use /dev/ttyUSB* or /dev/serial/by-id/*
  disconnect                 Disconnect from the hub
  init                       Discover connected dials
  status                     Show connection state
  dials                      List discovered dials
  dial <uid>                 Show one dial
  set <uid> <0-100>          Set dial position
  color <uid> <name>         Set a named backlight color
  colors                     List named colors
  image <uid> <path>         Upload PNG, JPEG, or BMP (scaled to 200x144)
  test                       Apply a visible test state to all dials
  help                       Show this help
    exit                       Exit (Ctrl+C only stops an active operation)
""");
}

static void WriteResult(bool success, string successMessage)
{
    if (success) WriteSuccess(successMessage); else WriteError("The operation failed.");
}

static void WriteSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ {message}");
    Console.ResetColor();
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ {message}");
    Console.ResetColor();
}
