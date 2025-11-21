using System;

namespace VUWare.Lib
{
    /// <summary>
    /// Represents the state of a single VU1 dial.
    /// </summary>
    public class DialState
    {
        /// <summary>
        /// Gets or sets the hub index (0-99) for this dial.
        /// Note: Index may change after provisioning/power cycles.
        /// Use UID as the permanent identifier.
        /// </summary>
        public byte Index { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier (12-byte UID).
        /// This is permanent and never changes.
        /// Use this as the primary key for identifying dials.
        /// </summary>
        public string UID { get; set; }

        /// <summary>
        /// Gets or sets the user-friendly name for this dial.
        /// Example: "CPU Usage", "GPU Usage", "RAM Usage"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the current dial position (0-100%).
        /// </summary>
        public byte CurrentValue { get; set; }

        /// <summary>
        /// Gets or sets whether a value update is pending.
        /// </summary>
        public bool ValuePending { get; set; }

        /// <summary>
        /// Gets or sets the backlight color state (RGBW, 0-100 each).
        /// </summary>
        public BacklightColor Backlight { get; set; }

        /// <summary>
        /// Gets or sets whether a backlight update is pending.
        /// </summary>
        public bool BacklightPending { get; set; }

        /// <summary>
        /// Gets or sets the current background image filename.
        /// </summary>
        public string ImageFile { get; set; }

        /// <summary>
        /// Gets or sets whether an image update is pending.
        /// </summary>
        public bool ImagePending { get; set; }

        /// <summary>
        /// Gets or sets the easing configuration for smooth transitions.
        /// </summary>
        public EasingConfig Easing { get; set; }

        /// <summary>
        /// Gets or sets the firmware version.
        /// </summary>
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// Gets or sets the hardware version.
        /// </summary>
        public string HardwareVersion { get; set; }

        /// <summary>
        /// Gets or sets the firmware build hash.
        /// </summary>
        public string FirmwareBuildHash { get; set; }

        /// <summary>
        /// Gets or sets the protocol version.
        /// </summary>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the generation (e.g., "VU1").
        /// </summary>
        public string Generation { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of last successful communication.
        /// </summary>
        public DateTime LastCommunication { get; set; }

        public DialState()
        {
            Index = 0;
            UID = string.Empty;
            Name = string.Empty;
            CurrentValue = 0;
            ValuePending = false;
            Backlight = new BacklightColor();
            BacklightPending = false;
            ImageFile = string.Empty;
            ImagePending = false;
            Easing = new EasingConfig();
            FirmwareVersion = "?";
            HardwareVersion = "?";
            FirmwareBuildHash = "?";
            ProtocolVersion = "v1";
            Generation = "VU1";
            LastCommunication = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"DialState(Index={Index}, UID={UID}, Name={Name}, Value={CurrentValue}%, FW={FirmwareVersion})";
        }
    }

    /// <summary>
    /// Represents RGBW backlight color values (0-100% each).
    /// </summary>
    public class BacklightColor
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte White { get; set; }

        public BacklightColor()
        {
            Red = 0;
            Green = 0;
            Blue = 0;
            White = 0;
        }

        public BacklightColor(byte red, byte green, byte blue, byte white = 0)
        {
            Red = red;
            Green = green;
            Blue = blue;
            White = white;
        }

        public override string ToString()
        {
            return $"RGBW({Red}, {Green}, {Blue}, {White})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is BacklightColor other)
            {
                return Red == other.Red && Green == other.Green && 
                       Blue == other.Blue && White == other.White;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Red, Green, Blue, White);
        }
    }

    /// <summary>
    /// Represents easing configuration for smooth dial and backlight transitions.
    /// </summary>
    public class EasingConfig
    {
        /// <summary>
        /// Gets or sets the dial step size (percentage per period).
        /// Default: 2
        /// </summary>
        public uint DialStep { get; set; }

        /// <summary>
        /// Gets or sets the dial update period in milliseconds.
        /// Default: 50
        /// </summary>
        public uint DialPeriod { get; set; }

        /// <summary>
        /// Gets or sets the backlight step size (percentage per period).
        /// Default: 5
        /// </summary>
        public uint BacklightStep { get; set; }

        /// <summary>
        /// Gets or sets the backlight update period in milliseconds.
        /// Default: 100
        /// </summary>
        public uint BacklightPeriod { get; set; }

        public EasingConfig()
        {
            DialStep = 2;
            DialPeriod = 50;
            BacklightStep = 5;
            BacklightPeriod = 100;
        }

        public EasingConfig(uint dialStep, uint dialPeriod, uint backlightStep, uint backlightPeriod)
        {
            DialStep = dialStep;
            DialPeriod = dialPeriod;
            BacklightStep = backlightStep;
            BacklightPeriod = backlightPeriod;
        }

        public override string ToString()
        {
            return $"Easing(DialStep={DialStep}%/{DialPeriod}ms, BacklightStep={BacklightStep}%/{BacklightPeriod}ms)";
        }

        public override bool Equals(object? obj)
        {
            if (obj is EasingConfig other)
            {
                return DialStep == other.DialStep && DialPeriod == other.DialPeriod &&
                       BacklightStep == other.BacklightStep && BacklightPeriod == other.BacklightPeriod;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DialStep, DialPeriod, BacklightStep, BacklightPeriod);
        }
    }
}
