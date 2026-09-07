namespace VUWare.Lib;

public static class CommandBuilder
{
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
    public const byte COMM_CMD_SET_RGB_BACKLIGHT = 0x13;
    public const byte COMM_CMD_SET_DIAL_EASING_STEP = 0x14;
    public const byte COMM_CMD_SET_DIAL_EASING_PERIOD = 0x15;
    public const byte COMM_CMD_SET_BACKLIGHT_EASING_STEP = 0x16;
    public const byte COMM_CMD_SET_BACKLIGHT_EASING_PERIOD = 0x17;
    public const byte COMM_CMD_GET_EASING_CONFIG = 0x18;
    public const byte COMM_CMD_GET_BUILD_INFO = 0x19;
    public const byte COMM_CMD_GET_FW_INFO = 0x20;
    public const byte COMM_CMD_GET_HW_INFO = 0x21;
    public const byte COMM_CMD_GET_PROTOCOL_INFO = 0x22;

    private static string Build(byte command, DataType type, ReadOnlySpan<byte> data = default) =>
        $">{command:X2}{(byte)type:X2}{data.Length:X4}{ProtocolHandler.BytesToHexString(data)}";

    private static byte[] IndexedUInt32(byte index, uint value) =>
        [index, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    public static string RescanBus() => Build(COMM_CMD_RESCAN_BUS, DataType.None);
    public static string GetDevicesMap() => Build(COMM_CMD_GET_DEVICES_MAP, DataType.None);
    public static string ProvisionDevice() => Build(COMM_CMD_PROVISION_DEVICE, DataType.None);
    public static string ResetAllDevices() => Build(COMM_CMD_RESET_ALL_DEVICES, DataType.None);
    public static string DialPower(bool powerOn) => Build(COMM_CMD_DIAL_POWER, DataType.SingleValue, [(byte)(powerOn ? 1 : 0)]);
    public static string GetDeviceUID(byte dialIndex) => Build(COMM_CMD_GET_DEVICE_UID, DataType.SingleValue, [dialIndex]);

    public static string SetDialPercentage(byte dialIndex, byte percentage)
    {
        if (percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        return Build(COMM_CMD_SET_DIAL_PERC_SINGLE, DataType.KeyValuePair, [dialIndex, percentage]);
    }

    public static string SetDialRaw(byte dialIndex, ushort value) =>
        Build(COMM_CMD_SET_DIAL_RAW_SINGLE, DataType.KeyValuePair, [dialIndex, (byte)(value >> 8), (byte)value]);

    public static string SetDialPercentagesMultiple(params (byte index, byte percentage)[] dialValues)
    {
        ArgumentNullException.ThrowIfNull(dialValues);
        if (dialValues.Length == 0) throw new ArgumentException("At least one dial value is required.", nameof(dialValues));
        var data = new byte[dialValues.Length * 2];
        for (var i = 0; i < dialValues.Length; i++)
        {
            if (dialValues[i].percentage > 100) throw new ArgumentOutOfRangeException(nameof(dialValues));
            data[i * 2] = dialValues[i].index;
            data[i * 2 + 1] = dialValues[i].percentage;
        }
        return Build(COMM_CMD_SET_DIAL_PERC_MULTIPLE, DataType.MultipleValue, data);
    }

    public static string SetRGBBacklight(byte index, byte red, byte green, byte blue, byte white = 0) =>
        Build(COMM_CMD_SET_RGB_BACKLIGHT, DataType.MultipleValue, [index, red, green, blue, white]);
    public static string SetDialEasingStep(byte index, uint value) => Build(COMM_CMD_SET_DIAL_EASING_STEP, DataType.SingleValue, IndexedUInt32(index, value));
    public static string SetDialEasingPeriod(byte index, uint value) => Build(COMM_CMD_SET_DIAL_EASING_PERIOD, DataType.SingleValue, IndexedUInt32(index, value));
    public static string SetBacklightEasingStep(byte index, uint value) => Build(COMM_CMD_SET_BACKLIGHT_EASING_STEP, DataType.SingleValue, IndexedUInt32(index, value));
    public static string SetBacklightEasingPeriod(byte index, uint value) => Build(COMM_CMD_SET_BACKLIGHT_EASING_PERIOD, DataType.SingleValue, IndexedUInt32(index, value));
    public static string GetEasingConfig(byte index) => Build(COMM_CMD_GET_EASING_CONFIG, DataType.SingleValue, [index]);
    public static string DisplayClear(byte index, bool blackBackground = false) => Build(COMM_CMD_DISPLAY_CLEAR, DataType.SingleValue, [index, (byte)(blackBackground ? 1 : 0)]);
    public static string DisplayGotoXY(byte index, ushort x, ushort y) => Build(COMM_CMD_DISPLAY_GOTO_XY, DataType.SingleValue, [index, (byte)(x >> 8), (byte)x, (byte)(y >> 8), (byte)y]);

    public static string DisplayImageData(byte index, byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length is 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(imageData));
        var data = new byte[imageData.Length + 1];
        data[0] = index;
        imageData.CopyTo(data, 1);
        return Build(COMM_CMD_DISPLAY_IMG_DATA, DataType.SingleValue, data);
    }

    public static string DisplayShowImage(byte index) => Build(COMM_CMD_DISPLAY_SHOW_IMG, DataType.SingleValue, [index]);
    public static string GetBuildInfo(byte index) => Build(COMM_CMD_GET_BUILD_INFO, DataType.SingleValue, [index]);
    public static string GetFirmwareInfo(byte index) => Build(COMM_CMD_GET_FW_INFO, DataType.SingleValue, [index]);
    public static string GetHardwareInfo(byte index) => Build(COMM_CMD_GET_HW_INFO, DataType.SingleValue, [index]);
    public static string GetProtocolInfo(byte index) => Build(COMM_CMD_GET_PROTOCOL_INFO, DataType.SingleValue, [index]);
    public static string GetRxBufferSize(byte index) => Build(COMM_CMD_RX_BUFFER_SIZE, DataType.SingleValue, [index]);
    public static string SetDialCalibrateMax(byte index, uint value) => Build(COMM_CMD_SET_DIAL_CALIBRATE_MAX, DataType.SingleValue, IndexedUInt32(index, value));
    public static string SetDialCalibrateHalf(byte index, uint value) => Build(COMM_CMD_SET_DIAL_CALIBRATE_HALF, DataType.SingleValue, IndexedUInt32(index, value));
}
