using System.Globalization;
using System.Text;

namespace VUWare.Lib;

public enum DataType : byte
{
    None = 0x01,
    SingleValue = 0x02,
    MultipleValue = 0x03,
    KeyValuePair = 0x04,
    StatusCode = 0x05
}

public enum GaugeStatus : ushort
{
    OK = 0x0000,
    Fail = 0x0001,
    Busy = 0x0002,
    Timeout = 0x0003,
    BadData = 0x0004,
    ProtocolError = 0x0005,
    NoMemory = 0x0006,
    InvalidArgument = 0x0007,
    BadAddress = 0x0008,
    Forbidden = 0x0009,
    AlreadyExists = 0x000B,
    Unsupported = 0x000C,
    NotImplemented = 0x000D,
    MalformedPackage = 0x000E,
    RecursiveCall = 0x0010,
    DataMismatch = 0x0011,
    DeviceOffline = 0x0012,
    ModuleNotInit = 0x0013,
    I2CError = 0x0014,
    USARTError = 0x0015,
    SPIError = 0x0016,
    BTLNoDevice = 0xE001,
    BTLInvalidState = 0xE002,
    BTLInvalidRequest = 0xE003
}

public static class ProtocolHandler
{
    private const int HeaderLength = 9;

    public sealed class Message
    {
        public byte Command { get; init; }
        public DataType DataType { get; init; }
        public int DataLength { get; init; }
        public string RawData { get; init; } = string.Empty;
        public byte[] BinaryData { get; init; } = [];
    }

    public static Message ParseResponse(string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        response = response.TrimEnd('\r', '\n');

        if (response.Length < HeaderLength || response[0] != '<')
            throw new FormatException("A VU1 response must begin with '<' and contain a complete header.");

        try
        {
            var command = byte.Parse(response.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var rawType = byte.Parse(response.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var dataLength = ushort.Parse(response.AsSpan(5, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var rawData = response[HeaderLength..];

            if (!Enum.IsDefined(typeof(DataType), rawType))
                throw new FormatException($"Unknown data type 0x{rawType:X2}.");
            if (rawData.Length != dataLength * 2)
                throw new FormatException($"Header declares {dataLength} bytes but response contains {rawData.Length / 2.0:0.#} bytes.");

            return new Message
            {
                Command = command,
                DataType = (DataType)rawType,
                DataLength = dataLength,
                RawData = rawData,
                BinaryData = HexStringToBytes(rawData)
            };
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            throw new FormatException("The VU1 response header is malformed.", exception);
        }
    }

    public static bool IsSuccessResponse(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.DataType != DataType.StatusCode || GetStatusCode(message) == GaugeStatus.OK;
    }

    public static GaugeStatus GetStatusCode(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.DataType != DataType.StatusCode || message.BinaryData.Length < 2)
            return GaugeStatus.OK;
        return (GaugeStatus)((message.BinaryData[0] << 8) | message.BinaryData[1]);
    }

    public static byte[] HexStringToBytes(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if ((hex.Length & 1) != 0)
            throw new FormatException("Hexadecimal data must contain an even number of characters.");

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return result;
    }

    public static string BytesToHexString(ReadOnlySpan<byte> data) => Convert.ToHexString(data);
    public static string ByteToHexString(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);
    public static string UshortToHexString(ushort value) => value.ToString("X4", CultureInfo.InvariantCulture);
    public static string UintToHexString(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);

    public static string LengthToHexString(int length)
    {
        if (length is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length));
        return length.ToString("X4", CultureInfo.InvariantCulture);
    }

    public static string DecodeAsciiString(string hexString) => Encoding.ASCII.GetString(HexStringToBytes(hexString));
}
