using System.Runtime.InteropServices;
using VUWare.Lib;

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var controller = new VU1Controller();
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
  exit                       Exit
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
