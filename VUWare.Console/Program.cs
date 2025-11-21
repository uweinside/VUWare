// See https://aka.ms/new-console-template for more information
using VUWare.Lib;
using System;
using System.Threading.Tasks;

class Program
{
    private static VU1Controller? _controller;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════╗");
        Console.WriteLine("║   VUWare Dial Controller Console   ║");
        Console.WriteLine("║      https://vudials.com           ║");
        Console.WriteLine("╚════════════════════════════════════╝");
        Console.WriteLine();

        _controller = new VU1Controller();

        try
        {
            await CommandLoop();
        }
        finally
        {
            _controller?.Dispose();
            Console.WriteLine("\nGoodbye!");
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

                await ProcessCommand(command, parts);
            }
            catch (Exception ex)
            {
                PrintError($"Unexpected error: {ex.Message}");
            }
        }
    }

    private static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine("┌─ Commands ─────────────────────────────────┐");
        Console.WriteLine("│ connect          - Auto-detect and connect │");
        Console.WriteLine("│ connect <port>   - Connect to specific COM │");
        Console.WriteLine("│ disconnect       - Disconnect from hub     │");
        Console.WriteLine("│ init             - Initialize dials        │");
        Console.WriteLine("│ status           - Show connection status  │");
        Console.WriteLine("│ dials            - List all dials          │");
        Console.WriteLine("│ dial <uid>       - Show dial info          │");
        Console.WriteLine("│ set <uid> <pct>  - Set dial position       │");
        Console.WriteLine("│ color <uid> <c>  - Set backlight color     │");
        Console.WriteLine("│ colors           - Show available colors   │");
        Console.WriteLine("│ image <uid> <f>  - Set dial image from BMP │");
        Console.WriteLine("│ help             - Show detailed help      │");
        Console.WriteLine("│ exit             - Exit program            │");
        Console.WriteLine("└────────────────────────────────────────────┘");
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

            case "help":
                CommandHelp();
                break;

            case "exit":
                _isRunning = false;
                break;

            default:
                PrintError($"Unknown command: {command}. Type 'help' for assistance.");
                break;
        }
    }

    private static async Task CommandConnect(string[] args)
    {
        if (_controller == null)
            return;

        if (_controller.IsConnected)
        {
            PrintWarning("Already connected. Disconnect first.");
            return;
        }

        bool success;
        if (args.Length > 1)
        {
            string port = args[1];
            Console.WriteLine($"Connecting to {port}...");
            success = _controller.Connect(port);
        }
        else
        {
            Console.WriteLine("Auto-detecting VU1 hub...");
            success = _controller.AutoDetectAndConnect();
        }

        if (success)
        {
            PrintSuccess($"Connected!");
        }
        else
        {
            PrintError("Connection failed. Check USB connection and try again.");
        }
    }

    private static void CommandDisconnect()
    {
        if (_controller == null)
            return;

        if (!_controller.IsConnected)
        {
            PrintWarning("Not connected.");
            return;
        }

        _controller.Disconnect();
        PrintSuccess("Disconnected.");
    }

    private static async Task CommandInit()
    {
        if (_controller == null)
            return;

        if (!_controller.IsConnected)
        {
            PrintError("Not connected. Use 'connect' first.");
            return;
        }

        if (_controller.IsInitialized)
        {
            PrintWarning("Already initialized.");
            return;
        }

        Console.WriteLine("Initializing and discovering dials...");
        bool success = await _controller.InitializeAsync();

        if (success)
        {
            PrintSuccess($"Initialized! Found {_controller.DialCount} dial(s).");
        }
        else
        {
            PrintError("Initialization failed. Check hub connection and power.");
        }
    }

    private static void CommandStatus()
    {
        if (_controller == null)
            return;

        Console.WriteLine();
        Console.WriteLine("╔═ Connection Status ══════════════════════╗");
        Console.WriteLine($"║ Connected:    {(_controller.IsConnected ? "Yes" : "No "):,-36} │");
        Console.WriteLine($"║ Initialized:  {(_controller.IsInitialized ? "Yes" : "No "):,-36} │");
        Console.WriteLine($"║ Dial Count:   {_controller.DialCount,-36} │");
        Console.WriteLine("╚══════════════════════════════════════════╝");
    }

    private static void CommandListDials()
    {
        if (_controller == null)
            return;

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            return;
        }

        var dials = _controller.GetAllDials();
        if (dials.Count == 0)
        {
            PrintWarning("No dials found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═ Dials ════════════════════════════════════════════════════════╗");
        foreach (var dial in dials.Values)
        {
            Console.WriteLine($"║ {dial.Name,-50} │");
            Console.WriteLine($"║   UID:  {dial.UID,-48} │");
            Console.WriteLine($"║   Pos:  {dial.CurrentValue}% │ Light: RGB({dial.Backlight.Red},{dial.Backlight.Green},{dial.Backlight.Blue})");
            Console.WriteLine("║");
        }
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    }

    private static void CommandShowDial(string[] args)
    {
        if (_controller == null)
            return;

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            return;
        }

        if (args.Length < 2)
        {
            PrintError("Usage: dial <uid>");
            return;
        }

        string uid = args[1];
        var dial = _controller.GetDial(uid);

        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═ Dial Details ═════════════════════════════════════╗");
        Console.WriteLine($"║ Name:              {dial.Name,-30} │");
        Console.WriteLine($"║ UID:               {dial.UID,-30} │");
        Console.WriteLine($"║ Index:             {dial.Index,-30} │");
        Console.WriteLine($"║ Position:          {dial.CurrentValue}%{new string(' ', 27)}│");
        Console.WriteLine($"║ Backlight RGB:     ({dial.Backlight.Red},{dial.Backlight.Green},{dial.Backlight.Blue}){new string(' ', 21)}│");
        Console.WriteLine($"║ White Channel:     {dial.Backlight.White}{new string(' ', 26)}│");
        Console.WriteLine($"║ Firmware Version:  {dial.FirmwareVersion,-30} │");
        Console.WriteLine($"║ Hardware Version:  {dial.HardwareVersion,-30} │");
        Console.WriteLine($"║ Last Comm:         {dial.LastCommunication:yyyy-MM-dd HH:mm:ss,-26} │");
        Console.WriteLine("╚═════════════════════════════════════════════════════╝");
    }

    private static async Task CommandSetDial(string[] args)
    {
        if (_controller == null)
            return;

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: set <uid> <percentage>");
            return;
        }

        string uid = args[1];
        if (!byte.TryParse(args[2], out byte percentage) || percentage > 100)
        {
            PrintError("Invalid percentage. Use 0-100.");
            return;
        }

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            return;
        }

        Console.WriteLine($"Setting {dial.Name} to {percentage}%...");
        bool success = await _controller.SetDialPercentageAsync(uid, percentage);

        if (success)
        {
            PrintSuccess($"Dial set to {percentage}%");
        }
        else
        {
            PrintError("Failed to set dial position.");
        }
    }

    private static async Task CommandSetColor(string[] args)
    {
        if (_controller == null)
            return;

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: color <uid> <color_name>");
            PrintError("Example: color <uid> red");
            return;
        }

        string uid = args[1];
        string colorName = args[2].ToLower();

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            return;
        }

        NamedColor? color = GetColorByName(colorName);
        if (color == null)
        {
            PrintError($"Unknown color: {colorName}. Type 'colors' for available colors.");
            return;
        }

        Console.WriteLine($"Setting {dial.Name} backlight to {color.Name}...");
        bool success = await _controller.SetBacklightColorAsync(uid, color);

        if (success)
        {
            PrintSuccess($"Backlight set to {color.Name}");
        }
        else
        {
            PrintError("Failed to set backlight color.");
        }
    }

    private static void CommandShowColors()
    {
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
    }

    private static async Task CommandSetImage(string[] args)
    {
        if (_controller == null)
            return;

        if (!_controller.IsInitialized)
        {
            PrintError("Not initialized. Use 'init' first.");
            return;
        }

        if (args.Length < 3)
        {
            PrintError("Usage: image <uid> <bitmap_file_path>");
            return;
        }

        string uid = args[1];
        string filePath = args[2];

        var dial = _controller.GetDial(uid);
        if (dial == null)
        {
            PrintError($"Dial '{uid}' not found.");
            return;
        }

        if (!File.Exists(filePath))
        {
            PrintError($"File not found: {filePath}");
            return;
        }

        try
        {
            byte[] imageData = ImageProcessor.LoadImageFile(filePath);
            Console.WriteLine($"Uploading image to {dial.Name}...");
            bool success = await _controller.SetDisplayImageAsync(uid, imageData);

            if (success)
            {
                PrintSuccess("Image uploaded successfully");
            }
            else
            {
                PrintError("Failed to upload image.");
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error loading image: {ex.Message}");
        }
    }

    private static void CommandHelp()
    {
        Console.WriteLine();
        Console.WriteLine("╔═ VUWare Console Help ══════════════════════════════════════╗");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║ GETTING STARTED:                                           ║");
        Console.WriteLine("║ 1. connect              - Auto-detect and connect to hub   ║");
        Console.WriteLine("║ 2. init                 - Discover all connected dials     ║");
        Console.WriteLine("║ 3. dials                - List dials                       ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║ CONTROLLING DIALS:                                         ║");
        Console.WriteLine("║ set <uid> <0-100>       - Set dial position (percentage)   ║");
        Console.WriteLine("║ color <uid> <name>      - Set backlight color              ║");
        Console.WriteLine("║ image <uid> <filepath>  - Load 1-bit BMP image (200x200)   ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║ QUERYING:                                                  ║");
        Console.WriteLine("║ dial <uid>              - Show detailed info for one dial   ║");
        Console.WriteLine("║ dials                   - List all dials with status        ║");
        Console.WriteLine("║ colors                  - Show available backlight colors   ║");
        Console.WriteLine("║ status                  - Show connection status            ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║ CONNECTION:                                                ║");
        Console.WriteLine("║ connect                 - Auto-detect VU1 hub              ║");
        Console.WriteLine("║ connect <port>          - Connect to specific COM port      ║");
        Console.WriteLine("║ disconnect              - Disconnect from hub              ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║ EXAMPLES:                                                  ║");
        Console.WriteLine("║ > connect                                                  ║");
        Console.WriteLine("║ > init                                                     ║");
        Console.WriteLine("║ > dials                                                    ║");
        Console.WriteLine("║ > set 3A4B5C6D7E8F0123 75                                  ║");
        Console.WriteLine("║ > color 3A4B5C6D7E8F0123 red                               ║");
        Console.WriteLine("║ > image 3A4B5C6D7E8F0123 ./test.bmp                         ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
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
