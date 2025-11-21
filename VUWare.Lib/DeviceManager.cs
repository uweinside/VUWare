using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VUWare.Lib
{
    /// <summary>
    /// Manages VU1 dial devices: discovery, provisioning, state tracking, and commands.
    /// Handles the complexity of I2C addressing, UID-based identification, and index management.
    /// </summary>
    public class DeviceManager : IDisposable
    {
        private readonly SerialPortManager _serialPort;
        private readonly Dictionary<string, DialState> _dialsByUID;
        private readonly Dictionary<byte, string> _indexToUID; // Maps current index to UID
        private readonly object _lockObj = new object();

        // Configuration
        private const int RESCAN_TIMEOUT_MS = 3000;
        private const int PROVISION_ATTEMPT_DELAY_MS = 200;
        private const int PROVISION_ATTEMPTS = 3;

        public DeviceManager(SerialPortManager serialPort)
        {
            _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
            _dialsByUID = new Dictionary<string, DialState>();
            _indexToUID = new Dictionary<byte, string>();
        }

        /// <summary>
        /// Gets a read-only copy of all discovered dials indexed by UID.
        /// </summary>
        public IReadOnlyDictionary<string, DialState> GetAllDials()
        {
            lock (_lockObj)
            {
                return _dialsByUID.ToDictionary(x => x.Key, x => x.Value);
            }
        }

        /// <summary>
        /// Gets a dial by its UID.
        /// </summary>
        public DialState? GetDialByUID(string uid)
        {
            lock (_lockObj)
            {
                if (_dialsByUID.TryGetValue(uid, out var dial))
                {
                    return dial;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets a dial by its current hub index.
        /// Note: Index may change after provisioning.
        /// </summary>
        public DialState? GetDialByIndex(byte index)
        {
            lock (_lockObj)
            {
                if (_indexToUID.TryGetValue(index, out var uid) && _dialsByUID.TryGetValue(uid, out var dial))
                {
                    return dial;
                }
                return null;
            }
        }

        /// <summary>
        /// Performs a complete bus scan and provisions dials.
        /// This should be called during initialization or when dials are disconnected/reconnected.
        /// </summary>
        public async Task<bool> DiscoverDialsAsync()
        {
            try
            {
                if (!_serialPort.IsConnected)
                {
                    throw new InvalidOperationException("Serial port is not connected");
                }

                // Step 1: Rescan the I2C bus
                if (!await RescanBusAsync())
                {
                    return false;
                }

                // Step 2: Provision any unprovisioned dials (multiple attempts)
                for (int attempt = 0; attempt < PROVISION_ATTEMPTS; attempt++)
                {
                    if (!await ProvisionDialsAsync())
                    {
                        // Continue even if provision fails, as dials may already be provisioned
                    }
                    if (attempt < PROVISION_ATTEMPTS - 1)
                    {
                        await Task.Delay(PROVISION_ATTEMPT_DELAY_MS);
                    }
                }

                // Step 3: Get the device map to see which dials are online
                if (!await UpdateDeviceMapAsync())
                {
                    return false;
                }

                // Step 4: Query each online dial for its UID and info
                await QueryDialDetailsAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rescans the I2C bus for connected dials.
        /// </summary>
        private async Task<bool> RescanBusAsync()
        {
            try
            {
                string command = CommandBuilder.RescanBus();
                string response = await SendCommandAsync(command, RESCAN_TIMEOUT_MS);
                
                var message = ProtocolHandler.ParseResponse(response);
                return ProtocolHandler.IsSuccessResponse(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rescan failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Provisions new dials on the I2C bus.
        /// This assigns unique addresses to dials that are still at the default address 0x09.
        /// </summary>
        private async Task<bool> ProvisionDialsAsync()
        {
            try
            {
                string command = CommandBuilder.ProvisionDevice();
                string response = await SendCommandAsync(command, RESCAN_TIMEOUT_MS);
                
                var message = ProtocolHandler.ParseResponse(response);
                return ProtocolHandler.IsSuccessResponse(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Provision failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the internal device map showing which dials are online.
        /// </summary>
        private async Task<bool> UpdateDeviceMapAsync()
        {
            try
            {
                string command = CommandBuilder.GetDevicesMap();
                string response = await SendCommandAsync(command, RESCAN_TIMEOUT_MS);
                
                var message = ProtocolHandler.ParseResponse(response);
                if (!ProtocolHandler.IsSuccessResponse(message))
                {
                    return false;
                }

                // Parse device map: each byte represents one device (0=offline, 1=online)
                byte[] deviceMap = ProtocolHandler.HexStringToBytes(message.RawData);

                lock (_lockObj)
                {
                    // Clear the index-to-UID mapping
                    _indexToUID.Clear();

                    // Mark all existing dials as offline initially
                    foreach (var dial in _dialsByUID.Values)
                    {
                        dial.LastCommunication = DateTime.UtcNow;
                    }

                    // Update based on device map
                    for (int i = 0; i < deviceMap.Length && i < 100; i++)
                    {
                        if (deviceMap[i] == 0x01)
                        {
                            // Device is online at index i
                            // We'll populate the UID mapping in QueryDialDetailsAsync
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Device map update failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Queries details (UID, firmware version, etc.) for each online dial.
        /// </summary>
        private async Task QueryDialDetailsAsync()
        {
            try
            {
                // First get the device map
                string mapCommand = CommandBuilder.GetDevicesMap();
                string mapResponse = await SendCommandAsync(mapCommand, RESCAN_TIMEOUT_MS);
                var mapMessage = ProtocolHandler.ParseResponse(mapResponse);

                if (!ProtocolHandler.IsSuccessResponse(mapMessage))
                {
                    return;
                }

                byte[] deviceMap = ProtocolHandler.HexStringToBytes(mapMessage.RawData);

                // For each online dial, query its UID and metadata
                for (byte i = 0; i < Math.Min(deviceMap.Length, 100); i++)
                {
                    if (deviceMap[i] == 0x01)
                    {
                        // Dial is online at index i
                        await QueryDialDetailsAtIndexAsync(i);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Querying dial details failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Queries details for a specific dial at a given index.
        /// </summary>
        private async Task QueryDialDetailsAtIndexAsync(byte index)
        {
            try
            {
                // Get UID
                string uidCommand = CommandBuilder.GetDeviceUID(index);
                string uidResponse = await SendCommandAsync(uidCommand, 1000);
                var uidMessage = ProtocolHandler.ParseResponse(uidResponse);

                if (!ProtocolHandler.IsSuccessResponse(uidMessage))
                {
                    return;
                }

                string uid = uidMessage.RawData;

                lock (_lockObj)
                {
                    // Create or update dial state
                    if (!_dialsByUID.ContainsKey(uid))
                    {
                        _dialsByUID[uid] = new DialState();
                    }

                    var dial = _dialsByUID[uid];
                    dial.Index = index;
                    dial.UID = uid;
                    if (string.IsNullOrEmpty(dial.Name))
                    {
                        dial.Name = $"Dial_{uid.Substring(0, 8)}";
                    }
                    dial.LastCommunication = DateTime.UtcNow;

                    // Update index mapping
                    _indexToUID[index] = uid;
                }

                // Query additional details asynchronously (don't block on these)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await QueryFirmwareDetailsAsync(index, uid);
                        await QueryEasingConfigAsync(index, uid);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to query firmware details for index {index}: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query dial at index {index}: {ex.Message}");
            }
        }

        /// <summary>
        /// Queries firmware and hardware version details.
        /// </summary>
        private async Task QueryFirmwareDetailsAsync(byte index, string uid)
        {
            try
            {
                // Get firmware version
                string fwCommand = CommandBuilder.GetFirmwareInfo(index);
                string fwResponse = await SendCommandAsync(fwCommand, 1000);
                var fwMessage = ProtocolHandler.ParseResponse(fwResponse);

                if (ProtocolHandler.IsSuccessResponse(fwMessage))
                {
                    string fwVersion = ProtocolHandler.DecodeAsciiString(fwMessage.RawData);
                    lock (_lockObj)
                    {
                        if (_dialsByUID.TryGetValue(uid, out var dial))
                        {
                            dial.FirmwareVersion = fwVersion;
                        }
                    }
                }

                // Get hardware version
                string hwCommand = CommandBuilder.GetHardwareInfo(index);
                string hwResponse = await SendCommandAsync(hwCommand, 1000);
                var hwMessage = ProtocolHandler.ParseResponse(hwResponse);

                if (ProtocolHandler.IsSuccessResponse(hwMessage))
                {
                    string hwVersion = ProtocolHandler.DecodeAsciiString(hwMessage.RawData);
                    lock (_lockObj)
                    {
                        if (_dialsByUID.TryGetValue(uid, out var dial))
                        {
                            dial.HardwareVersion = hwVersion;
                        }
                    }
                }

                // Get build info
                string buildCommand = CommandBuilder.GetBuildInfo(index);
                string buildResponse = await SendCommandAsync(buildCommand, 1000);
                var buildMessage = ProtocolHandler.ParseResponse(buildResponse);

                if (ProtocolHandler.IsSuccessResponse(buildMessage))
                {
                    string buildHash = ProtocolHandler.DecodeAsciiString(buildMessage.RawData);
                    lock (_lockObj)
                    {
                        if (_dialsByUID.TryGetValue(uid, out var dial))
                        {
                            dial.FirmwareBuildHash = buildHash;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firmware query failed for {uid}: {ex.Message}");
            }
        }

        /// <summary>
        /// Queries easing configuration from a dial.
        /// </summary>
        private async Task QueryEasingConfigAsync(byte index, string uid)
        {
            try
            {
                string command = CommandBuilder.GetEasingConfig(index);
                string response = await SendCommandAsync(command, 1000);
                var message = ProtocolHandler.ParseResponse(response);

                if (!ProtocolHandler.IsSuccessResponse(message) || message.BinaryData == null || message.BinaryData.Length < 16)
                {
                    return;
                }

                // Parse 4 x 32-bit values (big-endian)
                uint dialStep = (uint)(
                    (message.BinaryData[0] << 24) |
                    (message.BinaryData[1] << 16) |
                    (message.BinaryData[2] << 8) |
                    message.BinaryData[3]);

                uint dialPeriod = (uint)(
                    (message.BinaryData[4] << 24) |
                    (message.BinaryData[5] << 16) |
                    (message.BinaryData[6] << 8) |
                    message.BinaryData[7]);

                uint backlightStep = (uint)(
                    (message.BinaryData[8] << 24) |
                    (message.BinaryData[9] << 16) |
                    (message.BinaryData[10] << 8) |
                    message.BinaryData[11]);

                uint backlightPeriod = (uint)(
                    (message.BinaryData[12] << 24) |
                    (message.BinaryData[13] << 16) |
                    (message.BinaryData[14] << 8) |
                    message.BinaryData[15]);

                lock (_lockObj)
                {
                    if (_dialsByUID.TryGetValue(uid, out var dial))
                    {
                        dial.Easing = new EasingConfig(dialStep, dialPeriod, backlightStep, backlightPeriod);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Easing config query failed for {uid}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a dial to a specific percentage value.
        /// </summary>
        public async Task<bool> SetDialPercentageAsync(string uid, byte percentage)
        {
            try
            {
                var dial = GetDialByUID(uid);
                if (dial == null)
                {
                    throw new ArgumentException($"Dial with UID '{uid}' not found");
                }

                string command = CommandBuilder.SetDialPercentage(dial.Index, percentage);
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] SET command for dial index {dial.Index}, percentage {percentage}");
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] Built command: {command}");
                
                string response = await SendCommandAsync(command, 5000);
                
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] SET response received: {response}");
                
                var message = ProtocolHandler.ParseResponse(response);
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] Parsed response: Command=0x{message.Command:X2}, DataType={message.DataType}, Length={message.DataLength}");

                if (ProtocolHandler.IsSuccessResponse(message))
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceManager] SET command succeeded!");
                    lock (_lockObj)
                    {
                        dial.CurrentValue = percentage;
                        dial.ValuePending = false;
                        dial.LastCommunication = DateTime.UtcNow;
                    }
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceManager] SET command returned error status!");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] Failed to set dial percentage: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DeviceManager] Exception: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Sets the backlight color for a dial.
        /// </summary>
        public async Task<bool> SetBacklightAsync(string uid, byte red, byte green, byte blue, byte white = 0)
        {
            try
            {
                var dial = GetDialByUID(uid);
                if (dial == null)
                {
                    throw new ArgumentException($"Dial with UID '{uid}' not found");
                }

                string command = CommandBuilder.SetRGBBacklight(dial.Index, red, green, blue, white);
                string response = await SendCommandAsync(command, 5000);  // ? INCREASED FROM 1000ms
                var message = ProtocolHandler.ParseResponse(response);

                if (ProtocolHandler.IsSuccessResponse(message))
                {
                    lock (_lockObj)
                    {
                        dial.Backlight = new BacklightColor(red, green, blue, white);
                        dial.BacklightPending = false;
                        dial.LastCommunication = DateTime.UtcNow;
                    }
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set backlight: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates easing configuration for a dial.
        /// </summary>
        public async Task<bool> SetEasingConfigAsync(string uid, EasingConfig config)
        {
            try
            {
                var dial = GetDialByUID(uid);
                if (dial == null)
                {
                    throw new ArgumentException($"Dial with UID '{uid}' not found");
                }

                bool success = true;

                // Set dial easing step
                string stepCmd = CommandBuilder.SetDialEasingStep(dial.Index, config.DialStep);
                string stepResp = await SendCommandAsync(stepCmd, 5000);  // ? INCREASED FROM 1000ms
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(stepResp)))
                {
                    success = false;
                }

                // Set dial easing period
                string periodCmd = CommandBuilder.SetDialEasingPeriod(dial.Index, config.DialPeriod);
                string periodResp = await SendCommandAsync(periodCmd, 5000);  // ? INCREASED FROM 1000ms
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(periodResp)))
                {
                    success = false;
                }

                // Set backlight easing step
                string blStepCmd = CommandBuilder.SetBacklightEasingStep(dial.Index, config.BacklightStep);
                string blStepResp = await SendCommandAsync(blStepCmd, 5000);  // ? INCREASED FROM 1000ms
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(blStepResp)))
                {
                    success = false;
                }

                // Set backlight easing period
                string blPeriodCmd = CommandBuilder.SetBacklightEasingPeriod(dial.Index, config.BacklightPeriod);
                string blPeriodResp = await SendCommandAsync(blPeriodCmd, 5000);  // ? INCREASED FROM 1000ms
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(blPeriodResp)))
                {
                    success = false;
                }

                if (success)
                {
                    lock (_lockObj)
                    {
                        dial.Easing = config;
                        dial.LastCommunication = DateTime.UtcNow;
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set easing config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends a command and waits for a response.
        /// Thread-safe wrapper around SerialPortManager.SendCommand.
        /// </summary>
        private async Task<string> SendCommandAsync(string command, int timeoutMs)
        {
            return await Task.Run(() => _serialPort.SendCommand(command, timeoutMs));
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
