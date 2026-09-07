using System;
using System.Text;

namespace VUWare.Lib
{
    /// <summary>
    /// Builds type-safe protocol commands for the VU1 Gauge Hub.
    /// Handles message encoding in the format: >CCDDLLLL[DATA]
    /// </summary>
    public class CommandBuilder
    {
        // Command codes
        public const byte COMM_CMD_SET_DIAL_RAW_SINGLE = 0x01;
        public const byte COMM_CMD_SET_DIAL_PERC_SINGLE = 0x03;
        public const byte COMM_CMD_SET_DIAL_PERC_MULTIPLE = 0x04;
        public const byte COMM_CMD_SET_DIAL_CALIBRATE_MAX = 0x05;
        public const byte COMM_CMD_SET_DIAL_CALIBRATE_HALF = 0x06;
        public const byte COMM_CMD_GET_DEVICES_MAP = 0x07;
        public const byte COMM_CMD_PROVISION_DEVICE = 0x08;
        public const byte COMM_CMD_RESET_ALL_DEVICES = 0x09;
        public const byte COMM_CMD_DIAL_POWER = 0x0A;
        public const byte COMM_CMD_GET_DEVICE_UID = 0x0B;
        public const byte COMM_CMD_RESCAN_BUS = 0x0C;
        public const byte COMM_CMD_DISPLAY_CLEAR = 0x0D;
        public const byte COMM_CMD_DISPLAY_GOTO_XY = 0x0E;
        public const byte COMM_CMD_DISPLAY_IMG_DATA = 0x0F;
        public const byte COMM_CMD_DISPLAY_SHOW_IMG = 0x10;
        public const byte COMM_CMD_RX_BUFFER_SIZE = 0x11;
        public const byte COMM_CMD_GET_BUILD_INFO = 0x19;
        public const byte COMM_CMD_SET_DIAL_EASING_STEP = 0x14;
        public const byte COMM_CMD_SET_DIAL_EASING_PERIOD = 0x15;
        public const byte COMM_CMD_SET_BACKLIGHT_EASING_STEP = 0x16;
        public const byte COMM_CMD_SET_BACKLIGHT_EASING_PERIOD = 0x17;
        public const byte COMM_CMD_GET_EASING_CONFIG = 0x18;
        public const byte COMM_CMD_GET_FW_INFO = 0x20;
        public const byte COMM_CMD_GET_HW_INFO = 0x21;
        public const byte COMM_CMD_GET_PROTOCOL_INFO = 0x22;
        public const byte COMM_CMD_SET_RGB_BACKLIGHT = 0x13;

        /// <summary>
        /// Builds a complete command string in the format >CCDDLLLL[DATA]
        /// </summary>
        private static string BuildCommand(byte command, DataType dataType, byte[]? data)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('>');
            sb.Append(ProtocolHandler.ByteToHexString(command));
            sb.Append(ProtocolHandler.ByteToHexString((byte)dataType));

            // Calculate data length
            int dataLength = data?.Length ?? 0;
            sb.Append(ProtocolHandler.LengthToHexString(dataLength));

            // Add data if present
            if (data != null && data.Length > 0)
            {
                sb.Append(ProtocolHandler.BytesToHexString(data));
            }

            return sb.ToString();
        }

        /// <summary>
        /// COMM_CMD_RESCAN_BUS (0x0C)
        /// Rescans the I2C bus for connected dials.
        /// </summary>
        public static string RescanBus()
        {
            return BuildCommand(COMM_CMD_RESCAN_BUS, DataType.None, null);
        }

        /// <summary>
        /// COMM_CMD_GET_DEVICES_MAP (0x07)
        /// Returns a bitmap of online devices (up to 100 devices).
        /// </summary>
        public static string GetDevicesMap()
        {
            return BuildCommand(COMM_CMD_GET_DEVICES_MAP, DataType.None, null);
        }

        /// <summary>
        /// COMM_CMD_PROVISION_DEVICE (0x08)
        /// Provisions new devices on the bus (assigns I2C addresses).
        /// </summary>
        public static string ProvisionDevice()
        {
            return BuildCommand(COMM_CMD_PROVISION_DEVICE, DataType.None, null);
        }

        /// <summary>
        /// COMM_CMD_RESET_ALL_DEVICES (0x09)
        /// Resets all devices on the bus.
        /// </summary>
        public static string ResetAllDevices()
        {
            return BuildCommand(COMM_CMD_RESET_ALL_DEVICES, DataType.None, null);
        }

        /// <summary>
        /// COMM_CMD_DIAL_POWER (0x0A)
        /// Controls power to all dials. Set powerOn=true to power on, false to power off.
        /// </summary>
        public static string DialPower(bool powerOn)
        {
            byte[] data = { (byte)(powerOn ? 0x01 : 0x00) };
            return BuildCommand(COMM_CMD_DIAL_POWER, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_DEVICE_UID (0x0B)
        /// Gets the unique ID of a specific dial.
        /// </summary>
        public static string GetDeviceUID(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_DEVICE_UID, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_PERC_SINGLE (0x03)
        /// Sets dial position as percentage (0-100).
        /// </summary>
        public static string SetDialPercentage(byte dialIndex, byte percentage)
        {
            if (percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be 0-100");
            }

            byte[] data = { dialIndex, percentage };
            return BuildCommand(COMM_CMD_SET_DIAL_PERC_SINGLE, DataType.KeyValuePair, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_RAW_SINGLE (0x01)
        /// Sets dial position using raw value (0-65535).
        /// </summary>
        public static string SetDialRaw(byte dialIndex, ushort value)
        {
            byte[] data = new byte[3];
            data[0] = dialIndex;
            data[1] = (byte)(value >> 8);  // High byte
            data[2] = (byte)(value & 0xFF); // Low byte

            return BuildCommand(COMM_CMD_SET_DIAL_RAW_SINGLE, DataType.KeyValuePair, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_PERC_MULTIPLE (0x04)
        /// Sets multiple dial positions at once.
        /// </summary>
        public static string SetDialPercentagesMultiple(params (byte index, byte percentage)[] dialValues)
        {
            if (dialValues == null || dialValues.Length == 0)
            {
                throw new ArgumentException("Must provide at least one dial value", nameof(dialValues));
            }

            byte[] data = new byte[dialValues.Length * 2];
            for (int i = 0; i < dialValues.Length; i++)
            {
                if (dialValues[i].percentage > 100)
                {
                    throw new ArgumentOutOfRangeException($"dialValues[{i}].percentage", "Percentage must be 0-100");
                }

                data[i * 2] = dialValues[i].index;
                data[i * 2 + 1] = dialValues[i].percentage;
            }

            return BuildCommand(COMM_CMD_SET_DIAL_PERC_MULTIPLE, DataType.MultipleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_RGB_BACKLIGHT (0x13)
        /// Sets RGBW backlight values for a dial (0-100 percent each).
        /// </summary>
        public static string SetRGBBacklight(byte dialIndex, byte red, byte green, byte blue, byte white = 0)
        {
            byte[] data = { dialIndex, red, green, blue, white };
            return BuildCommand(COMM_CMD_SET_RGB_BACKLIGHT, DataType.MultipleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_EASING_STEP (0x14)
        /// Sets dial easing step size (percentage change per period).
        /// </summary>
        public static string SetDialEasingStep(byte dialIndex, uint step)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(step >> 24);
            data[2] = (byte)(step >> 16);
            data[3] = (byte)(step >> 8);
            data[4] = (byte)(step & 0xFF);

            return BuildCommand(COMM_CMD_SET_DIAL_EASING_STEP, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_EASING_PERIOD (0x15)
        /// Sets dial easing period (milliseconds between steps).
        /// </summary>
        public static string SetDialEasingPeriod(byte dialIndex, uint periodMs)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(periodMs >> 24);
            data[2] = (byte)(periodMs >> 16);
            data[3] = (byte)(periodMs >> 8);
            data[4] = (byte)(periodMs & 0xFF);

            return BuildCommand(COMM_CMD_SET_DIAL_EASING_PERIOD, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_BACKLIGHT_EASING_STEP (0x16)
        /// Sets backlight easing step size (percentage change per period).
        /// </summary>
        public static string SetBacklightEasingStep(byte dialIndex, uint step)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(step >> 24);
            data[2] = (byte)(step >> 16);
            data[3] = (byte)(step >> 8);
            data[4] = (byte)(step & 0xFF);

            return BuildCommand(COMM_CMD_SET_BACKLIGHT_EASING_STEP, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_BACKLIGHT_EASING_PERIOD (0x17)
        /// Sets backlight easing period (milliseconds between steps).
        /// </summary>
        public static string SetBacklightEasingPeriod(byte dialIndex, uint periodMs)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(periodMs >> 24);
            data[2] = (byte)(periodMs >> 16);
            data[3] = (byte)(periodMs >> 8);
            data[4] = (byte)(periodMs & 0xFF);

            return BuildCommand(COMM_CMD_SET_BACKLIGHT_EASING_PERIOD, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_EASING_CONFIG (0x18)
        /// Gets current easing configuration for a dial.
        /// </summary>
        public static string GetEasingConfig(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_EASING_CONFIG, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_DISPLAY_CLEAR (0x0D)
        /// Clears the display. Set color=false for white, true for black.
        /// </summary>
        public static string DisplayClear(byte dialIndex, bool blackBackground = false)
        {
            byte[] data = { dialIndex, (byte)(blackBackground ? 0x01 : 0x00) };
            return BuildCommand(COMM_CMD_DISPLAY_CLEAR, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_DISPLAY_GOTO_XY (0x0E)
        /// Sets cursor position for image data transfer.
        /// </summary>
        public static string DisplayGotoXY(byte dialIndex, ushort x, ushort y)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(x >> 8);
            data[2] = (byte)(x & 0xFF);
            data[3] = (byte)(y >> 8);
            data[4] = (byte)(y & 0xFF);

            return BuildCommand(COMM_CMD_DISPLAY_GOTO_XY, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_DISPLAY_IMG_DATA (0x0F)
        /// Sends image data to the display buffer. Maximum ~1000 bytes per packet.
        /// Image format: 1-bit per pixel, packed vertically (8 pixels per byte).
        /// </summary>
        public static string DisplayImageData(byte dialIndex, byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Image data cannot be empty", nameof(imageData));
            }

            if (imageData.Length > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(imageData), "Image data chunk must be <= 1000 bytes");
            }

            byte[] data = new byte[1 + imageData.Length];
            data[0] = dialIndex;
            Array.Copy(imageData, 0, data, 1, imageData.Length);

            return BuildCommand(COMM_CMD_DISPLAY_IMG_DATA, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_DISPLAY_SHOW_IMG (0x10)
        /// Refreshes display to show buffered image.
        /// </summary>
        public static string DisplayShowImage(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_DISPLAY_SHOW_IMG, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_BUILD_INFO (0x19)
        /// Gets firmware build hash/info.
        /// </summary>
        public static string GetBuildInfo(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_BUILD_INFO, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_FW_INFO (0x20)
        /// Gets firmware version.
        /// </summary>
        public static string GetFirmwareInfo(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_FW_INFO, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_HW_INFO (0x21)
        /// Gets hardware version.
        /// </summary>
        public static string GetHardwareInfo(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_HW_INFO, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_GET_PROTOCOL_INFO (0x22)
        /// Gets protocol version.
        /// </summary>
        public static string GetProtocolInfo(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_GET_PROTOCOL_INFO, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_RX_BUFFER_SIZE (0x11)
        /// Gets the dial's receive buffer size.
        /// </summary>
        public static string GetRxBufferSize(byte dialIndex)
        {
            byte[] data = { dialIndex };
            return BuildCommand(COMM_CMD_RX_BUFFER_SIZE, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_CALIBRATE_MAX (0x05)
        /// Calibrates dial maximum position.
        /// </summary>
        public static string SetDialCalibrateMax(byte dialIndex, uint value)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(value >> 24);
            data[2] = (byte)(value >> 16);
            data[3] = (byte)(value >> 8);
            data[4] = (byte)(value & 0xFF);

            return BuildCommand(COMM_CMD_SET_DIAL_CALIBRATE_MAX, DataType.SingleValue, data);
        }

        /// <summary>
        /// COMM_CMD_SET_DIAL_CALIBRATE_HALF (0x06)
        /// Calibrates dial half position.
        /// </summary>
        public static string SetDialCalibrateHalf(byte dialIndex, uint value)
        {
            byte[] data = new byte[5];
            data[0] = dialIndex;
            data[1] = (byte)(value >> 24);
            data[2] = (byte)(value >> 16);
            data[3] = (byte)(value >> 8);
            data[4] = (byte)(value & 0xFF);

            return BuildCommand(COMM_CMD_SET_DIAL_CALIBRATE_HALF, DataType.SingleValue, data);
        }
    }
}
