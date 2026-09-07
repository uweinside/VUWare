namespace VUWare.Lib;

public sealed class DialState
{
    public byte Index { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte CurrentValue { get; set; }
    public bool ValuePending { get; set; }
    public BacklightColor Backlight { get; set; } = new();
    public bool BacklightPending { get; set; }
    public string ImageFile { get; set; } = string.Empty;
    public bool ImagePending { get; set; }
    public EasingConfig Easing { get; set; } = new();
    public string FirmwareVersion { get; set; } = "?";
    public string HardwareVersion { get; set; } = "?";
    public string FirmwareBuildHash { get; set; } = "?";
    public string ProtocolVersion { get; set; } = "v1";
    public string Generation { get; set; } = "VU1";
    public DateTime LastCommunication { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"{Name} ({UID}) at index {Index}: {CurrentValue}%";
}

public sealed record BacklightColor(byte Red = 0, byte Green = 0, byte Blue = 0, byte White = 0);
public sealed record EasingConfig(uint DialStep = 2, uint DialPeriod = 50, uint BacklightStep = 5, uint BacklightPeriod = 100);

public sealed record NamedColor(string Name, byte Red, byte Green, byte Blue, byte White = 0);

public static class Colors
{
    public static readonly NamedColor Off = new("Off", 0, 0, 0);
    public static readonly NamedColor Red = new("Red", 100, 0, 0);
    public static readonly NamedColor Green = new("Green", 0, 100, 0);
    public static readonly NamedColor Blue = new("Blue", 0, 0, 100);
    public static readonly NamedColor White = new("White", 100, 100, 100);
    public static readonly NamedColor Yellow = new("Yellow", 100, 100, 0);
    public static readonly NamedColor Cyan = new("Cyan", 0, 100, 100);
    public static readonly NamedColor Magenta = new("Magenta", 100, 0, 100);
    public static readonly NamedColor Orange = new("Orange", 100, 50, 0);
    public static readonly NamedColor Purple = new("Purple", 100, 0, 100);
    public static readonly NamedColor Pink = new("Pink", 100, 25, 50);

    public static IReadOnlyList<NamedColor> All { get; } =
        [Off, Red, Green, Blue, White, Yellow, Cyan, Magenta, Orange, Purple, Pink];
}
