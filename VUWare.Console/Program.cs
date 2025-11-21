// See https://aka.ms/new-console-template for more information
using VUWare.Lib;
using VUWare.HWInfo64;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

class Program
{
    private static VU1Controller? _controller;
    private static bool _isRunning = true;
    private static Stopwatch? _commandTimer;
    private static int _commandCount = 0;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═════════════════════════════════════════════════╗");
        Console.WriteLine("║   VUWare Streacom VU1 Dial Controller Console   ║");
        Console.WriteLine("║      (c) 2025 Uwe Baumann                       ║");
        Console.WriteLine("║      https://github.com/uweinside/VUWare        ║");
        Console.WriteLine("║                                                 ║");
        Console.WriteLine("║      Based on the official VU1 code written by  ║");
        Console.WriteLine("║      Saša Karanović                             ║");
        Console.WriteLine("║      https://vudials.com                        ║");
        Console.WriteLine("║      https://github.com/SasaKaranovic/VU-Server ║");
        Console.WriteLine("╚═════════════════════════════════════════════════╝");
        Console.WriteLine();
        LogInfo("Initializing VUWare Console Application");
        LogInfo($"Build Time: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        LogInfo($"Target Framework: .NET 8.0");
        Console.WriteLine();

        _controller = new VU1Controller();

        try
        {
            await CommandLoop();
        }
        finally
        {
            LogInfo("Shutting down VUWare Console");
            _controller?.Dispose();
            Console.WriteLine();
            PrintSuccess("Goodbye!");
        }
    }

    private static async Task CommandLoop()
    {
        while (_isRunning)
        {
            try
            {
                PrintMenu();
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string command = parts[0].ToLower();

                _commandCount++;
                _commandTimer = Stopwatch.StartNew();

                LogInfo($"[Command #{_commandCount}] Executing: {command} {(parts.Length > 1 ? string.Join(" ", parts[1..]) : "")}");

                await ProcessCommand(command, parts);

                _commandTimer.Stop();
                LogInfo($"[Command #{_commandCount}] Completed in {_commandTimer.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                PrintError($"Unexpected error: {ex.Message}");
                LogError($"Exception: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine("┌─ Commands ──────────────────────────────────┐");
        Console.WriteLine("│ connect          - Auto-detect and connect  │");
        Console.WriteLine("│ connect <port>   - Connect to specific COM  │");
        Console.WriteLine("│ disconnect       - Disconnect from hub      │");
        Console.WriteLine("│ init             - Initialize dials         │");
        Console.WriteLine("│ status           - Show connection status   │");
        Console.WriteLine("│ dials            - List all dials           │");
        Console.WriteLine("│ dial <uid>       - Show dial info           │");
        Console.WriteLine("│ set <uid> <pct>  - Set dial position        │");
        Console.WriteLine("│ color <uid> <c>  - Set backlight color      │");
        Console.WriteLine("│ colors           - Show available colors    │");
        Console.WriteLine("│ image <uid> <f>  - Set dial image           │");
        Console.WriteLine("│                    (PNG/BMP/JPEG,200x144px) │");
        Console.WriteLine("│ sensors          - List HWInfo64 sensors    │");
        Console.WriteLine("│ monitor <uid> <s>- Monitor sensor on dial   │");
        Console.WriteLine("│ test             - Test all dials           │");
        Console.WriteLine("│ help             - Show detailed help       │");
        Console.WriteLine("│ exit             - Exit program             │");
        Console.WriteLine("└─────────────────────────────────────────────┘");
        Console.Write("> ");
    }

    private static async Task ProcessCommand(string command, string[] args)
    {
        switch (command)
        {
            case "connect":
                await CommandConnect(args);
                break;

            case "disconnect":
                CommandDisconnect();
                break;

            case "init":
                await CommandInit();
                break;

            case "status":
                CommandStatus();
                break;

            case "dials":
                CommandListDials();
                break;

            case "dial":
                CommandShowDial(args);
                break;

            case "set":
                await CommandSetDial(args);
                break;

            case "color":
                await CommandSetColor(args);
                break;

            case "colors":
                CommandShowColors();
                break;

            case "image":
                await CommandSetImage(args);
                break;

            case "test":
                await CommandTestDials();
                break;

            case "help":
                CommandHelp();
                break;

            case "exit":
                _isRunning = false;
                LogInfo("Exit command received - shutting down");
                break;

            case "sensors":
                CommandListSensors();
                break;

            case "monitor":
                await CommandMonitorSensors(args);
                break;

            default:
                PrintError($"Unknown command: {command}. Type 'help' for assistance.");
                LogWarning($"Unknown command attempted: {command}");
                break;
        }
    }

    private static async Task CommandConnect(string[] args)
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (_controller.IsConnected)
        {
            PrintWarning("Already connected. Disconnect first.");
            LogWarning("Connection attempt made while already connected");
            return;
        }

        bool success;
        if (args.Length > 1)
        {
            string port = args[1];
            LogInfo($"Connecting to specific COM port: {port}");
            Console.WriteLine($"Connecting to {port}...");
            success = _controller.Connect(port);
            LogInfo($"Connection to {port} result: {success}");
        }
        else
        {
            LogInfo("Starting auto-detection of VU1 hub");
            Console.WriteLine("Auto-detecting VU1 hub...");
            var timer = Stopwatch.StartNew();
            success = _controller.AutoDetectAndConnect();
            timer.Stop();
            LogInfo($"Auto-detection completed in {timer.ElapsedMilliseconds}ms, Result: {success}");
        }

        if (success)
        {
            PrintSuccess($"Connected to VU1 Hub!");
            LogInfo("✓ Successfully connected to VU1 Hub");
            Console.WriteLine();
            LogDetail("Connection Details:");
            LogDetail($"  • Connection Status: ACTIVE");
            LogDetail($"  • Initialization Status: {(_controller.IsInitialized ? "INITIALIZED" : "NOT INITIALIZED")}");
            LogDetail($"  • Next Step: Run 'init' to discover dials");
        }
        else
        {
            PrintError("Connection failed. Check USB connection and try again.");
            LogError("✗ Failed to connect to VU1 Hub");
            LogDetail("Troubleshooting steps:");
            LogDetail("  1. Verify VU1 Gauge Hub is connected via USB");
            LogDetail("  2. Check Device Manager for USB device");
            LogDetail("  3. Try specifying COM port directly: connect COM3");
            LogDetail("  4. Ensure proper USB drivers are installed");
        }
    }

    private static void CommandDisconnect()
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsConnected)
        {
            PrintWarning("Not connected.");
            LogWarning("Disconnect attempted while not connected");
            return;
        }

        LogInfo("Disconnecting from VU1 Hub");
        _controller.Disconnect();
        PrintSuccess("Disconnected from VU1 Hub.");
        LogInfo("✓ Successfully disconnected");
        LogDetail("Connection Details:");
        LogDetail($"  • Connection Status: INACTIVE");
        LogDetail($"  • Initialization Status: NOT INITIALIZED");
    }

    private static async Task CommandInit()
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsConnected)
        {
            PrintError("Not connected. Use 'connect' first.");
            LogError("Init attempted without connection");
            return;
        }

        if (_controller.IsInitialized)
        {
            PrintWarning("Already initialized.");
            LogWarning("Init attempted when already initialized");
            LogDetail("Current Dial Discovery:");
            LogDetail($"  • Total Dials Found: {_controller.DialCount}");
            if (_controller.DialCount > 0)
            {
                var dials = _controller.GetAllDials();
                foreach (var dial in dials.Values)
                {
                    LogDetail($"  • {dial.Name} (UID: {dial.UID})");
                }
            }
            return;
        }

        LogInfo("Starting dial discovery process");
        Console.WriteLine("Initializing and discovering dials...");
        var discoveryTimer = Stopwatch.StartNew();
        bool success = await _controller.InitializeAsync();
        discoveryTimer.Stop();

        if (success)
        {
            LogInfo($"✓ Initialization successful, discovered {_controller.DialCount} dial(s) in {discoveryTimer.ElapsedMilliseconds}ms");
            PrintSuccess($"Initialized! Found {_controller.DialCount} dial(s).");
            Console.WriteLine();
            LogDetail("Dial Discovery Details:");
            LogDetail($"  • Discovery Time: {discoveryTimer.ElapsedMilliseconds}ms");
            LogDetail($"  • Total Dials: {_controller.DialCount}");

            var dials = _controller.GetAllDials();
            if (dials.Count > 0)
            {
                Console.WriteLine();
                int dialNum = 1;
                foreach (var dial in dials.Values)
                {
                    LogDetail($"  Dial #{dialNum}:");
                    LogDetail($"    - Name: {dial.Name}");
                    LogDetail($"    - UID: {dial.UID}");
                    LogDetail($"    - Index: {dial.Index}");
                    LogDetail($"    - FW: {dial.FirmwareVersion}");
                    LogDetail($"    - HW: {dial.HardwareVersion}");
                    dialNum++;
                }
            }
            LogDetail($"  • Next Step: Use 'dials' to list, or 'dial <uid>' for details");
        }
        else
        {
            LogError("✗ Initialization failed");
            PrintError("Initialization failed. Check hub connection and power.");
            LogDetail("Troubleshooting:");
            LogDetail($"  • Discovery Time: {discoveryTimer.ElapsedMilliseconds}ms");
            LogDetail("  1. Check USB cable connection to VU1 Hub");
            LogDetail("  2. Verify hub has power and is responding");
            LogDetail("  3. Check I2C connections from hub to dials");
            LogDetail("  4. Ensure dials are powered");
            LogDetail("  5. Try power cycling the hub and dials");
        }
    }

    private static void CommandStatus()
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        LogInfo("Displaying connection status");
        Console.WriteLine();
        Console.WriteLine("╔═ Connection Status ════════════════════════════════════════╗");
        Console.WriteLine($"║ Connected:           {(_controller.IsConnected ? "YES" : "NO"),-48} │");
        Console.WriteLine($"║ Initialized:         {(_controller.IsInitialized ? "YES" : "NO"),-48} │");
        Console.WriteLine($"║ Dial Count:          {_controller.DialCount,-48} │");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

        Console.WriteLine();
        LogDetail("Detailed Status Information:");
        LogDetail($"  • Connection Status: {(_controller.IsConnected ? "ACTIVE" : "INACTIVE")}");
        LogDetail($"  • Initialization Status: {(_controller.IsInitialized ? "INITIALIZED" : "NOT INITIALIZED")}");
        LogDetail($"  • Connected Dials: {_controller.DialCount}");

        if (_controller.IsInitialized && _controller.DialCount > 0)
        {
            var dials = _controller.GetAllDials();
            Console.WriteLine();
            LogDetail("Dial Summary:");
            foreach (var dial in dials.Values)
            {
                LogDetail($"  • {dial.Name}:");
                LogDetail($"    - Position: {dial.CurrentValue}%");
                LogDetail($"    - Backlight: RGB({dial.Backlight.Red}, {dial.Backlight.Green}, {dial.Backlight.Blue})");
                LogDetail($"    - Last Comm: {dial.LastCommunication:yyyy-MM-dd HH:mm:ss}");
            }
        }
    }

    private static void CommandListDials()
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            LogError("List dials attempted without initialization");
            return;
        }

        var dials = _controller.GetAllDials();
        if (dials.Count == 0)
        {
            PrintWarning("No dials found.");
            LogWarning("List dials showed no discovered dials");
            return;
        }

        LogInfo($"Displaying {dials.Count} discovered dial(s)");
        Console.WriteLine();
        Console.WriteLine("╔═ Dials ════════════════════════════════════════════════════════╗");
        int dialNum = 1;
        foreach (var dial in dials.Values)
        {
            Console.WriteLine($"║ [{dialNum}] {dial.Name,-45} │");
            Console.WriteLine($"║   UID:  {dial.UID,-48} │");
            Console.WriteLine($"║   Pos:  {dial.CurrentValue,3}% │ Light: RGB({dial.Backlight.Red:3},{dial.Backlight.Green:3},{dial.Backlight.Blue:3})");
            Console.WriteLine("║");
            dialNum++;
        }
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        Console.WriteLine();
        LogDetail("Dial Inventory:");
        foreach (var dial in dials.Values)
        {
            LogDetail($"  • {dial.Name} [{dial.UID}]");
            LogDetail($"    - Position: {dial.CurrentValue}%");
            LogDetail($"    - Color: RGB({dial.Backlight.Red}, {dial.Backlight.Green}, {dial.Backlight.Blue})");
            LogDetail($"    - Firmware: {dial.FirmwareVersion}");
            LogDetail($"    - Hardware: {dial.HardwareVersion}");
        }
    }

    private static void CommandShowDial(string[] args)
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            LogError("Show dial attempted without initialization");
            return;
        }

        if (args.Length < 2)
        {
            PrintError("Usage: dial <uid>");
            LogWarning("Show dial command missing UID argument");
            return;
        }

        string uid = args[1];
        LogInfo($"Querying dial information for UID: {uid}");
        var dial = _controller.GetDial(uid);

        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            LogError($"Dial not found with UID: {uid}");
            LogDetail("Available dials:");
            var allDials = _controller.GetAllDials();
            foreach (var d in allDials.Values)
            {
                LogDetail($"  • {d.Name} ({d.UID})");
            }
            return;
        }

        LogInfo($"✓ Found dial: {dial.Name}");
        Console.WriteLine();
        Console.WriteLine( "╔═ Dial Details ═════════════════════════════════════╗");
        Console.WriteLine($"║ Name:              {dial.Name,-30} │");
        Console.WriteLine($"║ UID:               {dial.UID,-30} │");
        Console.WriteLine($"║ Index:             {dial.Index,-30} │");
        Console.WriteLine($"║ Position:          {dial.CurrentValue}%{new string(' ', 27)}│");
        Console.WriteLine($"║ Backlight RGB:     ({dial.Backlight.Red,3},{dial.Backlight.Green,3},{dial.Backlight.Blue,3}){new string(' ', 18)}│");
        Console.WriteLine($"║ White Channel:     {dial.Backlight.White,-30} │");
        Console.WriteLine($"║ Firmware Version:  {dial.FirmwareVersion,-30} │");
        Console.WriteLine($"║ Hardware Version:  {dial.HardwareVersion,-30} │");
        Console.WriteLine($"║ Last Comm:         {dial.LastCommunication:yyyy-MM-dd HH:mm:ss,-26} │");
        Console.WriteLine( "╚═════════════════════════════════════════════════════╝");

        Console.WriteLine();
        LogDetail("Detailed Dial Information:");
        LogDetail($"  • Display Name: {dial.Name}");
        LogDetail($"  • Unique ID: {dial.UID}");
        LogDetail($"  • I2C Index: {dial.Index}");
        LogDetail($"  • Current Position: {dial.CurrentValue}%");
        LogDetail($"  • Backlight Settings:");
        LogDetail($"    - Red: {dial.Backlight.Red}%");
        LogDetail($"    - Green: {dial.Backlight.Green}%");
        LogDetail($"    - Blue: {dial.Backlight.Blue}%");
        LogDetail($"    - White: {dial.Backlight.White}%");
        LogDetail($"  • Firmware Version: {dial.FirmwareVersion}");
        LogDetail($"  • Hardware Version: {dial.HardwareVersion}");
        LogDetail($"  • Last Communication: {dial.LastCommunication:yyyy-MM-dd HH:mm:ss}");
        if (dial.Easing != null)
        {
            LogDetail($"  • Easing Configuration:");
            LogDetail($"    - Dial Step: {dial.Easing.DialStep}%");
            LogDetail($"    - Dial Period: {dial.Easing.DialPeriod}ms");
            LogDetail($"    - Backlight Step: {dial.Easing.BacklightStep}%");
            LogDetail($"    - Backlight Period: {dial.Easing.BacklightPeriod}ms");
        }
    }

    private static async Task CommandSetDial(string[] args)
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            LogError("Set dial attempted without initialization");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: set <uid> <percentage>");
            LogWarning("Set dial command missing required arguments");
            return;
        }

        string uid = args[1];
        if (!byte.TryParse(args[2], out byte percentage) || percentage > 100)
        {
            PrintError("Invalid percentage. Use 0-100.");
            LogError($"Invalid percentage value: {args[2]}");
            return;
        }

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            LogError($"Dial not found with UID: {uid}");
            return;
        }

        LogInfo($"Setting dial '{dial.Name}' to {percentage}%");
        Console.WriteLine($"Setting {dial.Name} to {percentage}%...");
        var timer = Stopwatch.StartNew();
        bool success = await _controller.SetDialPercentageAsync(uid, percentage);
        timer.Stop();

        if (success)
        {
            PrintSuccess($"Dial set to {percentage}%");
            LogInfo($"✓ Successfully set '{dial.Name}' to {percentage}% in {timer.ElapsedMilliseconds}ms");
            Console.WriteLine();
            LogDetail("Operation Details:");
            LogDetail($"  • Dial: {dial.Name} ({uid})");
            LogDetail($"  • Target Position: {percentage}%");
            LogDetail($"  • Previous Position: {dial.CurrentValue}%");
            LogDetail($"  • Operation Time: {timer.ElapsedMilliseconds}ms");
            LogDetail($"  • Status: SUCCESS");
        }
        else
        {
            PrintError("Failed to set dial position.");
            LogError($"✗ Failed to set '{dial.Name}' to {percentage}%");
            LogDetail("Troubleshooting:");
            LogDetail($"  • Operation Time: {timer.ElapsedMilliseconds}ms");
            LogDetail($"  • Dial Index: {dial.Index}");
            LogDetail($"  • Connection Status: {(_controller.IsConnected ? "CONNECTED" : "NOT CONNECTED")}");
            LogDetail($"  • Last Communication: {dial.LastCommunication:yyyy-MM-dd HH:mm:ss}");
            LogDetail("Steps to resolve:");
            LogDetail("  1. Verify dial is connected to hub (check I2C cable)");
            LogDetail("  2. Check if dial is powered");
            LogDetail("  3. Try querying dial info: dial " + uid);
            LogDetail("  4. Check Debug Output window for serial communication details");
            LogDetail("  5. If timeout (>5000ms), hub may not be responding - try reconnecting");
        }
    }

    private static async Task CommandSetColor(string[] args)
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            LogError("Set color attempted without initialization");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: color <uid> <color_name>");
            PrintError("Example: color <uid> red");
            LogWarning("Set color command missing required arguments");
            return;
        }

        string uid = args[1];
        string colorName = args[2].ToLower();

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            LogError($"Dial not found with UID: {uid}");
            return;
        }

        NamedColor? color = GetColorByName(colorName);
        if (color == null)
        {
            PrintError($"Unknown color: {colorName}. Type 'colors' for available colors.");
            LogWarning($"Unknown color name: {colorName}");
            return;
        }

        LogInfo($"Setting '{dial.Name}' backlight to {color.Name}");
        Console.WriteLine($"Setting {dial.Name} backlight to {color.Name}...");
        var timer = Stopwatch.StartNew();
        bool success = await _controller.SetBacklightColorAsync(uid, color);
        timer.Stop();

        if (success)
        {
            PrintSuccess($"Backlight set to {color.Name}");
            LogInfo($"✓ Successfully set '{dial.Name}' backlight to {color.Name} in {timer.ElapsedMilliseconds}ms");
            Console.WriteLine();
            LogDetail("Operation Details:");
            LogDetail($"  • Dial: {dial.Name} ({uid})");
            LogDetail($"  • Color: {color.Name}");
            LogDetail($"  • RGB Values: ({color.Red}%, {color.Green}%, {color.Blue}%)");
            LogDetail($"  • White Value: {color.White}%");
            LogDetail($"  • Operation Time: {timer.ElapsedMilliseconds}ms");
            LogDetail($"  • Status: SUCCESS");
        }
        else
        {
            PrintError("Failed to set backlight color.");
            LogError($"✗ Failed to set '{dial.Name}' backlight to {color.Name}");
            LogDetail("Troubleshooting:");
            LogDetail($"  • Operation Time: {timer.ElapsedMilliseconds}ms");
            LogDetail("  1. Verify dial is connected to hub");
            LogDetail("  2. Check I2C cable connections");
            LogDetail("  3. Ensure dial is powered");
            LogDetail("  4. Try again with 'color' command");
        }
    }

    private static void CommandShowColors()
    {
        LogInfo("Displaying available colors");
        Console.WriteLine();
        Console.WriteLine("╔═ Available Colors ═════════════════════════════╗");
        Console.WriteLine("│ off      - Off (0, 0, 0)                       │");
        Console.WriteLine("│ red      - Red (100, 0, 0)                     │");
        Console.WriteLine("│ green    - Green (0, 100, 0)                   │");
        Console.WriteLine("│ blue     - Blue (0, 0, 100)                    │");
        Console.WriteLine("│ white    - White (100, 100, 100)               │");
        Console.WriteLine("│ yellow   - Yellow (100, 100, 0)                │");
        Console.WriteLine("│ cyan     - Cyan (0, 100, 100)                  │");
        Console.WriteLine("│ magenta  - Magenta (100, 0, 100)               │");
        Console.WriteLine("│ orange   - Orange (100, 50, 0)                 │");
        Console.WriteLine("│ purple   - Purple (100, 0, 100)                │");
        Console.WriteLine("│ pink     - Pink (100, 25, 50)                  │");
        Console.WriteLine("╚═════════════════════════════════════════════════╝");

        Console.WriteLine();
        LogDetail("Color Reference Chart:");
        LogDetail("  • off     → RGB(0, 0, 0)       - Black");
        LogDetail("  • red     → RGB(100, 0, 0)     - Pure Red");
        LogDetail("  • green   → RGB(0, 100, 0)     - Pure Green");
        LogDetail("  • blue    → RGB(0, 0, 100)     - Pure Blue");
        LogDetail("  • white   → RGB(100, 100, 100) - Pure White");
        LogDetail("  • yellow  → RGB(100, 100, 0)   - Red + Green");
        LogDetail("  • cyan    → RGB(0, 100, 100)   - Green + Blue");
        LogDetail("  • magenta → RGB(100, 0, 100)   - Red + Blue");
        LogDetail("  • orange  → RGB(100, 50, 0)    - Red + Half Green");
        LogDetail("  • purple  → RGB(100, 0, 100)   - Red + Blue");
        LogDetail("  • pink    → RGB(100, 25, 50)   - Red + Quarter Green + Half Blue");
    }

    private static async Task CommandSetImage(string[] args)
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            LogError("Set image attempted without initialization");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: image <uid> <bitmap_file_path>");
            LogWarning("Set image command missing required arguments");
            return;
        }

        string uid = args[1];
        string filePath = args[2];

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            LogError($"Dial not found with UID: {uid}");
            return;
        }

        if (!File.Exists(filePath))
        {
            PrintError($"File not found: {filePath}");
            LogError($"Image file not found: {filePath}");
            return;
        }

        try
        {
            LogInfo($"Loading image from: {filePath}");
            var fileInfo = new FileInfo(filePath);
            LogDetail($"Image File Details:");
            LogDetail($"  • Path: {Path.GetFullPath(filePath)}");
            LogDetail($"  • Size: {fileInfo.Length} bytes");
            LogDetail($"  • Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

            var loadTimer = Stopwatch.StartNew();
            byte[] imageData = ImageProcessor.LoadImageFile(filePath);
            loadTimer.Stop();

            LogInfo($"Image loaded successfully ({imageData.Length} bytes) in {loadTimer.ElapsedMilliseconds}ms");
            Console.WriteLine($"Uploading image to {dial.Name}...");

            var uploadTimer = Stopwatch.StartNew();
            bool success = await _controller.SetDisplayImageAsync(uid, imageData);
            uploadTimer.Stop();

            if (success)
            {
                PrintSuccess("Image uploaded successfully");
                LogInfo($"✓ Image successfully uploaded to '{dial.Name}' in {uploadTimer.ElapsedMilliseconds}ms");
                Console.WriteLine();
                LogDetail("Upload Details:");
                LogDetail($"  • Dial: {dial.Name} ({uid})");
                LogDetail($"  • Image Size: {imageData.Length} bytes (expected {ImageProcessor.BYTES_PER_IMAGE})");
                LogDetail($"  • Dimensions: 200x144 px (1-bit vertical packed)");
                LogDetail($"  • Load Time: {loadTimer.ElapsedMilliseconds}ms");
                LogDetail($"  • Upload Time: {uploadTimer.ElapsedMilliseconds}ms");
                LogDetail($"  • Total Time: {loadTimer.ElapsedMilliseconds + uploadTimer.ElapsedMilliseconds}ms");
                LogDetail($"  • Status: SUCCESS");
            }
            else
            {
                PrintError("Failed to upload image.");
                LogError("✗ Failed to upload image to dial");
                LogDetail("Upload Details:");
                LogDetail($"  • Dial: {dial.Name} ({uid})");
                LogDetail($"  • Image Size: {imageData.Length} bytes (expected {ImageProcessor.BYTES_PER_IMAGE})");
                LogDetail("Troubleshooting:");
                LogDetail("  1. Ensure source image converts/scales to 200x144 pixels");
                LogDetail("  2. Confirm resulting packed size is 3600 bytes");
                LogDetail("  3. Verify hub connection and dial index is valid");
                LogDetail("  4. Try a blank image: 'image <uid> ./etc/image_pack/blank.png'");
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error loading image: {ex.Message}");
            LogError($"✗ Exception while loading image: {ex.GetType().Name}");
            LogDetail($"Error Details:");
            LogDetail($"  • Message: {ex.Message}");
            LogDetail($"  • File Path: {filePath}");
            if (ex.InnerException != null)
            {
                LogDetail($"  • Inner Exception: {ex.InnerException.Message}");
            }
        }
    }

    private static async Task CommandTestDials()
    {
        if (_controller == null)
        {
            PrintError("Controller not initialized");
            return;
        }

        // Auto-connect if not connected
        if (!_controller.IsConnected)
        {
            LogInfo("Auto-connecting to VU1 Hub...");
            Console.WriteLine("Auto-connecting to VU1 Hub...");
            bool connectSuccess = _controller.AutoDetectAndConnect();
            
            if (!connectSuccess)
            {
                PrintError("Failed to auto-connect to VU1 Hub");
                LogError("Auto-connect failed in test command");
                return;
            }
            
            PrintSuccess("Connected to VU1 Hub!");
            LogInfo("✓ Auto-connected successfully");
        }

        // Auto-initialize if not initialized
        if (!_controller.IsInitialized)
        {
            LogInfo("Auto-initializing dials...");
            Console.WriteLine("Auto-initializing dials...");
            bool initSuccess = await _controller.InitializeAsync();
            
            if (!initSuccess)
            {
                PrintError("Failed to initialize dials");
                LogError("Auto-init failed in test command");
                return;
            }
            
            PrintSuccess($"Initialized! Found {_controller.DialCount} dial(s).");
            LogInfo($"✓ Auto-initialized successfully - found {_controller.DialCount} dial(s)");
        }

        var dials = _controller.GetAllDials();
        if (dials.Count == 0)
        {
            PrintWarning("No dials found. Test cannot be performed.");
            LogWarning("Test command executed with no dials discovered");
            return;
        }

        LogInfo("Starting automated dial test suite");
        Console.WriteLine();
        Console.WriteLine( "╔══════════════════════════════════╗");
        Console.WriteLine( "║   VUWare Dial Test Suite         ║");
        Console.WriteLine($"║   Testing {dials.Count} dial(s)  ║");
        Console.WriteLine( "╚══════════════════════════════════╝");
        Console.WriteLine();

        int dialNumber = 1;
        foreach (var dial in dials.Values)
        {
            Console.WriteLine();
            Console.WriteLine($"┌─ Dial {dialNumber}/{dials.Count}: {dial.Name} ─────────────┐");
            Console.WriteLine($"│ UID: {dial.UID}");
            Console.WriteLine("│");

            try
            {
                // Set to test position
                LogInfo($"Testing dial '{dial.Name}' (UID: {dial.UID})");
                Console.WriteLine($"│ Setting position to 50%...");
                
                var setTimer = Stopwatch.StartNew();
                bool setSuccess = await _controller.SetDialPercentageAsync(dial.UID, 50);
                setTimer.Stop();

                if (setSuccess)
                {
                    Console.WriteLine($"│ ✓ Position set in {setTimer.ElapsedMilliseconds}ms");
                    LogInfo($"✓ Successfully set dial position to 50% in {setTimer.ElapsedMilliseconds}ms");
                }
                else
                {
                    Console.WriteLine($"│ ✗ Failed to set position");
                    LogError($"✗ Failed to set dial position");
                }

                // Set to test color (green)
                LogInfo($"Setting dial '{dial.Name}' backlight to Green");
                Console.WriteLine($"│ Setting backlight to Green...");
                
                var colorTimer = Stopwatch.StartNew();
                bool colorSuccess = await _controller.SetBacklightColorAsync(dial.UID, Colors.Green);
                colorTimer.Stop();

                if (colorSuccess)
                {
                    Console.WriteLine($"│ ✓ Backlight set in {colorTimer.ElapsedMilliseconds}ms");
                    LogInfo($"✓ Successfully set backlight to Green in {colorTimer.ElapsedMilliseconds}ms");
                }
                else
                {
                    Console.WriteLine($"│ ✗ Failed to set backlight");
                    LogError($"✗ Failed to set backlight color");
                }

                Console.WriteLine("│");
                Console.WriteLine("│ Press any key to continue...");
                Console.ReadKey(true);

                // Reset to default
                Console.WriteLine("│ Resetting position to 0% and backlight to Off...");
                
                await _controller.SetDialPercentageAsync(dial.UID, 0);
                await _controller.SetBacklightColorAsync(dial.UID, Colors.Off);

                LogInfo($"✓ Reset dial '{dial.Name}' to defaults");
                Console.WriteLine($"│ ✓ Dial reset to defaults");
                Console.WriteLine("└────────────────────────────────┘");

                PrintSuccess($"Dial {dial.Name} test completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"│ ✗ Error: {ex.Message}");
                Console.WriteLine("└────────────────────────────────┘");
                PrintError($"Error testing {dial.Name}: {ex.Message}");
                LogError($"Exception during testing of dial '{dial.Name}': {ex.GetType().Name}: {ex.Message}");
            }

            dialNumber++;
        }

        Console.WriteLine();
        PrintSuccess("Dial test suite completed successfully!");
        LogInfo($"✓ Test suite completed - tested {dials.Count} dial(s)");
        LogDetail("All dials have been reset to default state (position: 0%, backlight: off)");
    }

    private static void CommandHelp()
    {
        LogInfo("Displaying help information");
        Console.WriteLine();
        Console.WriteLine("╔═ VUWare Console Help ════════════════════════════════════════════╗");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ GETTING STARTED:                                                 ║");
        Console.WriteLine("║ 1. connect              - Auto-detect and connect to hub         ║");
        Console.WriteLine("║ 2. init                 - Discover all connected dials           ║");
        Console.WriteLine("║ 3. dials                - List dials                             ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ CONTROLLING DIALS:                                               ║");
        Console.WriteLine("║ set <uid> <0-100>       - Set dial position (percentage)         ║");
        Console.WriteLine("║ color <uid> <name>      - Set backlight color                    ║");
        Console.WriteLine("║ image <uid> <filepath>  - Load PNG/BMP/JPG (auto->200x144/3600B) ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ DISPLAY IMAGE FORMAT:                                            ║");
        Console.WriteLine("║ • Resolution: 200x144 px                                         ║");
        Console.WriteLine("║ • Color Depth: 1-bit (vertical pack, 8 pixels/byte)              ║");
        Console.WriteLine("║ • Total Bytes: 3600                                              ║");
        Console.WriteLine("║ • Bit: 1 = light (>127), 0 = dark (<=127)                        ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ TESTING:                                                         ║");
        Console.WriteLine("║ test                    - Run automated test on all dials        ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ QUERYING:                                                        ║");
        Console.WriteLine("║ dial <uid>              - Show detailed info for one dial        ║");
        Console.WriteLine("║ dials                   - List all dials with status             ║");
        Console.WriteLine("║ colors                  - Show available backlight colors        ║");
        Console.WriteLine("║ status                  - Show connection status                 ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ CONNECTION:                                                      ║");
        Console.WriteLine("║ connect                 - Auto-detect VU1 hub                    ║");
        Console.WriteLine("║ connect <port>          - Connect to specific COM port           ║");
        Console.WriteLine("║ disconnect              - Disconnect from hub                    ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ HWINFO64 INTEGRATION:                                             ║");
        Console.WriteLine("║ sensors                 - List all available HWInfo64 sensors    ║");
        Console.WriteLine("║ monitor <uid> <s:e>     - Monitor sensor on dial (press key exit)║");
        Console.WriteLine("║                          <s:e> format: \"SensorName:EntryName\"   ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("║ EXAMPLES:                                                        ║");
        Console.WriteLine("║ > connect                                                        ║");
        Console.WriteLine("║ > init                                                           ║");
        Console.WriteLine("║ > sensors                                                        ║");
        Console.WriteLine("║ > monitor ABC123 \"CPU Package:Temperature\"                      ║");
        Console.WriteLine("║ > dials                                                          ║");
        Console.WriteLine("║ > set 3A4B... 75                                                 ║");
        Console.WriteLine("║ > color 3A4B... red                                              ║");
        Console.WriteLine("║                                                                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
    }

    private static NamedColor? GetColorByName(string name)
    {
        return name switch
        {
            "off" => Colors.Off,
            "red" => Colors.Red,
            "green" => Colors.Green,
            "blue" => Colors.Blue,
            "white" => Colors.White,
            "yellow" => Colors.Yellow,
            "cyan" => Colors.Cyan,
            "magenta" => Colors.Magenta,
            "orange" => Colors.Orange,
            "purple" => Colors.Purple,
            "pink" => Colors.Pink,
            _ => null
        };
    }

    private static void CommandListSensors()
    {
        LogInfo("Listing all HWInfo64 sensors");
        Console.WriteLine();
        Console.WriteLine("╔═ HWInfo64 Sensors ════════════════════════════════════════════╗");

        var reader = new HWiNFOReader();
        if (!reader.Connect())
        {
            PrintWarning("HWInfo64 not available or Shared Memory Support not enabled");
            LogWarning("Failed to connect to HWInfo64 shared memory");
            Console.WriteLine("║ Make sure:                                                 │");
            Console.WriteLine("║ 1. HWInfo64 is running                                     │");
            Console.WriteLine("║ 2. Running in 'Sensors only' mode                          │");
            Console.WriteLine("║ 3. 'Shared Memory Support' is enabled in Options           │");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            return;
        }

        try
        {
            var readings = reader.ReadAllSensorReadings();

            if (readings == null || readings.Count == 0)
            {
                PrintWarning("No sensors found.");
                LogWarning("HWInfo64 reported no sensor readings");
                Console.WriteLine("║ No sensor readings available in shared memory             │");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
                return;
            }

            // Group by sensor name
            var grouped = readings.GroupBy(r => r.SensorName).OrderBy(g => g.Key);
            int count = 0;

            foreach (var sensorGroup in grouped)
            {
                Console.WriteLine($"║ [{sensorGroup.Key}]");
                foreach (var reading in sensorGroup.OrderBy(r => r.EntryName))
                {
                    Console.WriteLine($"║   ├─ {reading.EntryName,-40}");
                    Console.WriteLine($"║   │  Value: {reading.Value:F2} {reading.Unit,-10} Min: {reading.ValueMin:F2} Max: {reading.ValueMax:F2}");
                    count++;
                }
                Console.WriteLine("║");
            }

            Console.WriteLine($"║ Total: {count} sensor reading(s)                           │");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            LogInfo($"✓ Listed {count} sensor readings from {grouped.Count()} sensor(s)");
        }
        finally
        {
            reader.Disconnect();
        }
    }

    private static async Task CommandMonitorSensors(string[] args)
    {
        if (_controller == null || !_controller.IsInitialized)
        {
            PrintError("VU1 not initialized. Use 'connect' and 'init' first.");
            LogError("Monitor command attempted without initialized VU1");
            return;
        }

        if (args.Length < 2)
        {
            PrintError("Usage: monitor <dial_uid> <sensor_name:entry_name> [poll_interval_ms]");
            PrintError("Example: monitor ABC123 \"CPU Package:Temperature\" 500");
            LogWarning("Monitor command missing required arguments");
            return;
        }

        string dialUID = args[1];
        if (args.Length < 3)
        {
            PrintError("Usage: monitor <dial_uid> <sensor_name:entry_name> [poll_interval_ms]");
            LogWarning("Monitor command missing sensor name");
            return;
        }

        var dial = _controller.GetDial(dialUID);
        if (dial == null)
        {
            PrintError($"Dial '{dialUID}' not found.");
            LogError($"Monitor dial not found with UID: {dialUID}");
            return;
        }

        string[] sensorParts = args[2].Split(':');
        if (sensorParts.Length != 2)
        {
            PrintError("Usage: monitor <dial_uid> <sensor_name:entry_name>");
            PrintError("Example: monitor ABC123 \"CPU Package:Temperature\"");
            LogWarning("Invalid sensor specification format");
            return;
        }

        string sensorName = sensorParts[0];
        string entryName = sensorParts[1];
        int pollInterval = 500; // Default 500ms

        if (args.Length > 3 && !int.TryParse(args[3], out pollInterval))
        {
            PrintError("Invalid poll interval. Using default 500ms");
            pollInterval = 500;
        }

        LogInfo($"Starting sensor monitoring on dial '{dial.Name}'");
        LogInfo($"Sensor: {sensorName} > {entryName}");
        LogInfo($"Poll interval: {pollInterval}ms");

        Console.WriteLine();
        Console.WriteLine("╔═ Sensor Monitor ═══════════════════════════════════════════════╗");
        Console.WriteLine($"║ Dial: {dial.Name,-55}│");
        Console.WriteLine($"║ Sensor: {sensorName,-51}│");
        Console.WriteLine($"║ Entry: {entryName,-52}│");
        Console.WriteLine($"║ Poll Interval: {pollInterval}ms{new string(' ', 43)}│");
        Console.WriteLine("║                                                               ║");
        Console.WriteLine("║ Press any key to stop monitoring...                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        var reader = new HWiNFOReader();

        if (!reader.Connect())
        {
            PrintError("Failed to connect to HWInfo64. Check if it's running.");
            LogError("Cannot connect to HWInfo64 shared memory for monitoring");
            return;
        }

        try
        {
            // Start a task to wait for key press
            var keyPressTask = Task.Run(() => Console.ReadKey(true), cts.Token);

            int updateCount = 0;
            double lastValue = 0;
            bool foundSensor = false;

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Check if key was pressed
                    if (keyPressTask.IsCompleted)
                    {
                        break;
                    }

                    var readings = reader.ReadAllSensorReadings();
                    var matching = readings.FirstOrDefault(r =>
                        r.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase) &&
                        r.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase));

                    if (matching != null)
                    {
                        foundSensor = true;
                        double value = matching.Value;
                        string unit = matching.Unit;

                        // Map sensor value to dial percentage (0-100)
                        // Simple linear mapping - adjust min/max as needed for your sensors
                        byte percentage = 0;

                        // Auto-detect reasonable ranges based on sensor type
                        if (matching.Type == SensorType.Temperature)
                        {
                            // Temperature: assume 0-100°C range
                            percentage = (byte)Math.Clamp((value / 100.0) * 100, 0, 100);
                        }
                        else if (matching.Type == SensorType.Usage)
                        {
                            // Usage: already percentage
                            percentage = (byte)Math.Clamp(value, 0, 100);
                        }
                        else if (matching.Type == SensorType.Voltage)
                        {
                            // Voltage: assume 0-5V range
                            percentage = (byte)Math.Clamp((value / 5.0) * 100, 0, 100);
                        }
                        else if (matching.Type == SensorType.Fan)
                        {
                            // Fan: assume 0-5000 RPM range
                            percentage = (byte)Math.Clamp((value / 5000.0) * 100, 0, 100);
                        }
                        else if (matching.Type == SensorType.Power)
                        {
                            // Power: assume 0-500W range
                            percentage = (byte)Math.Clamp((value / 500.0) * 100, 0, 100);
                        }
                        else
                        {
                            // Default: use min/max from sensor data
                            if (matching.ValueMax > matching.ValueMin)
                            {
                                double range = matching.ValueMax - matching.ValueMin;
                                percentage = (byte)Math.Clamp(((value - matching.ValueMin) / range) * 100, 0, 100);
                            }
                        }

                        // Update dial
                        try
                        {
                            await _controller.SetDialPercentageAsync(dialUID, percentage);
                            updateCount++;

                            // Log significant changes
                            if (Math.Abs(value - lastValue) > 0.5)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Value: {value:F1} {unit,-6} │ Dial: {percentage:3}% │ Updates: {updateCount}");
                                lastValue = value;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"Failed to update dial: {ex.Message}");
                            break;
                        }
                    }
                    else if (!foundSensor && updateCount == 0)
                    {
                        PrintWarning($"Sensor not found: {sensorName} > {entryName}");
                        LogWarning($"Sensor not found in HWInfo64");
                        LogDetail("Available sensors:");
                        var allReadings = reader.ReadAllSensorReadings();
                        var availableSensors = allReadings.GroupBy(r => r.SensorName).Take(5);
                        foreach (var sensor in availableSensors)
                        {
                            LogDetail($"  • {sensor.Key}");
                            foreach (var entry in sensor.Take(3))
                            {
                                LogDetail($"    - {entry.EntryName}");
                            }
                        }
                        break;
                    }

                    await Task.Delay(pollInterval, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"Error during monitoring: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            Console.WriteLine();
            LogInfo($"✓ Monitoring completed - {updateCount} updates sent to dial");
            PrintSuccess($"Monitoring ended. Sent {updateCount} updates to dial.");

            // Reset dial to 0
            try
            {
                await _controller.SetDialPercentageAsync(dialUID, 0);
                await _controller.SetBacklightAsync(dialUID, 0, 100, 0); // Green
            }
            catch { }
        }
        finally
        {
            reader.Disconnect();
            cts.Cancel();
        }
    }

    // Logging Methods
    private static void LogInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ  {message}");
        Console.ResetColor();
    }

    private static void LogDetail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ {message}");
        Console.ResetColor();
    }

    private static void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ {message}");
        Console.ResetColor();
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ {message}");
        Console.ResetColor();
    }

    private static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }
}
