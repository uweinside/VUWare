using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VUWare.Lib
{
    /// <summary>
    /// Enumerates data type codes used in the serial protocol.
    /// </summary>
    public enum DataType : byte
    {
        None = 0x01,
        SingleValue = 0x02,
        MultipleValue = 0x03,
        KeyValuePair = 0x04,
        StatusCode = 0x05
    }

    /// <summary>
    /// Enumerates status codes returned by the hub.
    /// </summary>
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

    /// <summary>
    /// Parses and validates serial protocol responses from the VU1 hub.
    /// </summary>
    public class ProtocolHandler
    {
        private const int HEADER_LENGTH = 9; // "<CCDDLLLL"
        private const int MIN_RESPONSE_LENGTH = HEADER_LENGTH;

        /// <summary>
        /// Represents a parsed protocol message.
        /// </summary>
        public class Message
        {
            public byte Command { get; set; }
            public DataType DataType { get; set; }
            public int DataLength { get; set; }
            public string RawData { get; set; } = string.Empty;
            public byte[]? BinaryData { get; set; }

            public override string ToString()
            {
                return $"Command: 0x{Command:X2}, Type: {DataType}, Length: {DataLength}";
            }
        }

        /// <summary>
        /// Parses a protocol response string.
        /// Response format: <CCDDLLLL[DATA]
        /// </summary>
        public static Message ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response) || response.Length < MIN_RESPONSE_LENGTH)
            {
                throw new ArgumentException("Invalid response format");
            }

            try
            {
                // Validate start character
                if (response[0] != '<')
                {
                    throw new InvalidOperationException("Response does not start with '<'");
                }

                // Parse header
                byte command = byte.Parse(response.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                byte dataType = byte.Parse(response.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                int dataLength = int.Parse(response.Substring(5, 4), System.Globalization.NumberStyles.HexNumber);

                // Extract data
                string rawData = response.Length > HEADER_LENGTH ? response.Substring(HEADER_LENGTH) : string.Empty;

                var message = new Message
                {
                    Command = command,
                    DataType = (DataType)dataType,
                    DataLength = dataLength,
                    RawData = rawData
                };

                // If data type is status code, parse it
                if (message.DataType == DataType.StatusCode && rawData.Length >= 4)
                {
                    message.BinaryData = HexStringToBytes(rawData.Substring(0, 4));
                }
                else if (rawData.Length > 0)
                {
                    message.BinaryData = HexStringToBytes(rawData);
                }

                return message;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse response: {response}", ex);
            }
        }

        /// <summary>
        /// Validates that a response indicates success.
        /// </summary>
        public static bool IsSuccessResponse(Message message)
        {
            if (message.DataType != DataType.StatusCode)
            {
                return true; // Non-status responses are typically successful
            }

            if (message.BinaryData == null || message.BinaryData.Length < 2)
            {
                return false;
            }

            // Status codes are big-endian 16-bit values
            ushort statusCode = (ushort)((message.BinaryData[0] << 8) | message.BinaryData[1]);
            return statusCode == (ushort)GaugeStatus.OK;
        }

        /// <summary>
        /// Gets the status code from a response message.
        /// </summary>
        public static GaugeStatus GetStatusCode(Message message)
        {
            if (message.DataType != DataType.StatusCode || message.BinaryData == null || message.BinaryData.Length < 2)
            {
                return GaugeStatus.OK;
            }

            ushort statusCode = (ushort)((message.BinaryData[0] << 8) | message.BinaryData[1]);
            return (GaugeStatus)statusCode;
        }

        /// <summary>
        /// Converts a hex string to a byte array.
        /// </summary>
        public static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            {
                return Array.Empty<byte>();
            }

            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return result;
        }

        /// <summary>
        /// Converts a byte array to a hex string.
        /// </summary>
        public static string BytesToHexString(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts a single byte to a 2-digit hex string.
        /// </summary>
        public static string ByteToHexString(byte value)
        {
            return value.ToString("X2");
        }

        /// <summary>
        /// Converts a 16-bit value to big-endian hex string (4 characters).
        /// </summary>
        public static string UshortToHexString(ushort value)
        {
            return value.ToString("X4");
        }

        /// <summary>
        /// Converts a 32-bit value to big-endian hex string (8 characters).
        /// </summary>
        public static string UintToHexString(uint value)
        {
            return value.ToString("X8");
        }

        /// <summary>
        /// Converts a data length to a 4-digit hex string.
        /// </summary>
        public static string LengthToHexString(int length)
        {
            if (length < 0 || length > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 0 and 65535");
            }
            return length.ToString("X4");
        }

        /// <summary>
        /// Decodes hex-encoded ASCII string (e.g., firmware version).
        /// </summary>
        public static string DecodeAsciiString(string hexString)
        {
            try
            {
                byte[] bytes = HexStringToBytes(hexString);
                return Encoding.ASCII.GetString(bytes);
            }
            catch
            {
                return hexString;
            }
        }
    }
}
