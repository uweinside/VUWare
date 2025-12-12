using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace VUWare.App.Models
{
    /// <summary>
    /// Represents color settings for a dial with optional threshold-based color changes.
    /// </summary>
    public class DialColorConfig
    {
        private string _colorMode = "threshold";

        /// <summary>Color mode: "threshold" (uses thresholds), "static" (fixed color), or "off" (no color/hidden)</summary>
        [JsonPropertyName("colorMode")]
        public string ColorMode
        {
            get => _colorMode;
            set => _colorMode = value?.ToLowerInvariant() ?? "threshold";
        }

        /// <summary>Static color when colorMode is "static" (e.g., "Green", "Cyan", "Blue")</summary>
        [JsonPropertyName("staticColor")]
        public string StaticColor { get; set; } = "Cyan";

        /// <summary>Normal operating color (e.g., "Green", "Cyan", "Blue")</summary>
        [JsonPropertyName("normalColor")]
        public string NormalColor { get; set; } = "Cyan";

        /// <summary>Color when warning threshold is reached (e.g., "Yellow")</summary>
        [JsonPropertyName("warningColor")]
        public string WarningColor { get; set; } = "Yellow";

        /// <summary>Color when critical threshold is reached (e.g., "Red")</summary>
        [JsonPropertyName("criticalColor")]
        public string CriticalColor { get; set; } = "Red";

        /// <summary>Determines the appropriate color based on sensor value and thresholds.</summary>
        /// <param name="sensorValue">Current sensor value</param>
        /// <param name="criticalThreshold">Critical threshold value (optional)</param>
        /// <param name="warningThreshold">Warning threshold value (optional)</param>
        /// <returns>Color name to display</returns>
        public string GetColorForValue(double sensorValue, double? criticalThreshold, double? warningThreshold)
        {
            if (criticalThreshold.HasValue && sensorValue >= criticalThreshold.Value)
                return CriticalColor;

            if (warningThreshold.HasValue && sensorValue >= warningThreshold.Value)
                return WarningColor;

            return NormalColor;
        }

        /// <summary>Gets the color based on the configured color mode.</summary>
        public string GetColor(double sensorValue, double? criticalThreshold, double? warningThreshold)
        {
            return ColorMode switch
            {
                "static" => StaticColor,
                "off" => string.Empty,
                "threshold" => GetColorForValue(sensorValue, criticalThreshold, warningThreshold),
                _ => NormalColor
            };
        }
    }

    /// <summary>
    /// Represents the configuration for a single VU1 dial paired with a HWInfo64 sensor.
    /// </summary>
    public class DialConfig
    {
        private string _displayFormat = "percentage";

        /// <summary>Unique identifier of the VU1 dial (from device discovery)</summary>
        [JsonPropertyName("dialUid")]
        public string DialUid { get; set; } = string.Empty;

        /// <summary>Friendly display name for this dial (e.g., "CPU Temp", "GPU Load")</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>HWInfo64 sensor name (e.g., "CPU Package", "GPU 1")</summary>
        [JsonPropertyName("sensorName")]
        public string SensorName { get; set; } = string.Empty;

        /// <summary>HWInfo64 sensor ID (for disambiguation when multiple sensors share the same name)</summary>
        [JsonPropertyName("sensorId")]
        public uint SensorId { get; set; } = 0;

        /// <summary>HWInfo64 sensor instance (for disambiguation when multiple sensors share the same name)</summary>
        [JsonPropertyName("sensorInstance")]
        public uint SensorInstance { get; set; } = 0;

        /// <summary>HWInfo64 entry/reading name (e.g., "Temperature", "Load")</summary>
        [JsonPropertyName("entryName")]
        public string EntryName { get; set; } = string.Empty;

        /// <summary>HWInfo64 entry ID (for disambiguation when multiple entries share the same name)</summary>
        [JsonPropertyName("entryId")]
        public uint EntryId { get; set; } = 0;

        /// <summary>Minimum value to map to 0% on the dial</summary>
        [JsonPropertyName("minValue")]
        public double MinValue { get; set; }

        /// <summary>Maximum value to map to 100% on the dial</summary>
        [JsonPropertyName("maxValue")]
        public double MaxValue { get; set; }

        /// <summary>Optional warning threshold value</summary>
        [JsonPropertyName("warningThreshold")]
        public double? WarningThreshold { get; set; }

        /// <summary>Optional critical threshold value</summary>
        [JsonPropertyName("criticalThreshold")]
        public double? CriticalThreshold { get; set; }

        /// <summary>Color configuration for this dial</summary>
        [JsonPropertyName("colorConfig")]
        public DialColorConfig ColorConfig { get; set; } = new();

        /// <summary>Enable/disable this dial mapping</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Update frequency in milliseconds (default 1000ms)</summary>
        [JsonPropertyName("updateIntervalMs")]
        public int UpdateIntervalMs { get; set; } = 1000;

        /// <summary>Display format: "percentage" for %, "value" for actual sensor value (default: "percentage")</summary>
        [JsonPropertyName("displayFormat")]
        public string DisplayFormat
        {
            get => _displayFormat;
            set => _displayFormat = value?.ToLowerInvariant() ?? "percentage";
        }

        /// <summary>Unit suffix for value display (e.g., "°C", "MHz") - only used when displayFormat is "value"</summary>
        [JsonPropertyName("displayUnit")]
        public string DisplayUnit { get; set; } = string.Empty;

        /// <summary>Number of decimal places to display (0 = integer like "3400", 1 = one decimal like "34.2", default: 1)</summary>
        [JsonPropertyName("decimalPlaces")]
        public int DecimalPlaces { get; set; } = 1;

        /// <summary>Gets the appropriate color based on the current sensor value.</summary>
        public string GetColorForValue(double sensorValue)
        {
            return ColorConfig.GetColor(sensorValue, CriticalThreshold, WarningThreshold);
        }
    }

    /// <summary>
    /// Root configuration file structure containing all dial mappings and app settings.
    /// </summary>
    public class DialsConfiguration
    {
        /// <summary>Configuration version for migration purposes</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>General application settings</summary>
        [JsonPropertyName("appSettings")]
        public AppSettings AppSettings { get; set; } = new();

        /// <summary>List of up to 4 dial configurations</summary>
        [JsonPropertyName("dials")]
        public List<DialConfig> Dials { get; set; } = new();

        /// <summary>Gets the effective number of dials to use (respects dialCountOverride if set)</summary>
        public int GetEffectiveDialCount()
        {
            if (AppSettings.DialCountOverride.HasValue)
            {
                return Math.Min(AppSettings.DialCountOverride.Value, Dials.Count);
            }
            return Dials.Count;
        }

        /// <summary>Gets the active dials based on effective dial count</summary>
        public List<DialConfig> GetActiveDials()
        {
            int effectiveCount = GetEffectiveDialCount();
            return Dials.Take(effectiveCount).ToList();
        }

        /// <summary>Validates that the configuration is well-formed.</summary>
        public bool Validate(out List<string> errors)
        {
            errors = new();

            if (Dials.Count > 4)
            {
                errors.Add($"Maximum 4 dials supported, found {Dials.Count}");
            }

            // Validate dialCountOverride
            if (AppSettings.DialCountOverride.HasValue)
            {
                int overrideCount = AppSettings.DialCountOverride.Value;
                if (overrideCount < 1 || overrideCount > 4)
                {
                    errors.Add($"dialCountOverride must be between 1 and 4, found {overrideCount}");
                }
                
                if (overrideCount > Dials.Count)
                {
                    errors.Add($"dialCountOverride ({overrideCount}) exceeds number of configured dials ({Dials.Count})");
                }
            }

            // Valid color names (from VU1Controller backlight colors)
            var validColors = new[] { "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", "Orange", "Purple", "Pink", "White", "Off" };

            foreach (var dial in Dials)
            {
                if (string.IsNullOrWhiteSpace(dial.DialUid))
                    errors.Add($"Dial missing DialUid");

                if (string.IsNullOrWhiteSpace(dial.DisplayName))
                    errors.Add($"Dial '{dial.DialUid}' missing DisplayName");

                if (string.IsNullOrWhiteSpace(dial.SensorName))
                    errors.Add($"Dial '{dial.DisplayName}' missing SensorName");

                if (string.IsNullOrWhiteSpace(dial.EntryName))
                    errors.Add($"Dial '{dial.DisplayName}' missing EntryName");

                if (dial.MaxValue <= dial.MinValue)
                    errors.Add($"Dial '{dial.DisplayName}': MaxValue must be > MinValue");

                if (dial.WarningThreshold.HasValue && dial.CriticalThreshold.HasValue &&
                    dial.CriticalThreshold.Value <= dial.WarningThreshold.Value)
                    errors.Add($"Dial '{dial.DisplayName}': CriticalThreshold must be > WarningThreshold");

                if (dial.UpdateIntervalMs < 100)
                    errors.Add($"Dial '{dial.DisplayName}': UpdateIntervalMs must be >= 100");

                // Validate colors
                if (!validColors.Contains(dial.ColorConfig.NormalColor, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Dial '{dial.DisplayName}': Invalid NormalColor '{dial.ColorConfig.NormalColor}'");

                if (!validColors.Contains(dial.ColorConfig.WarningColor, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Dial '{dial.DisplayName}': Invalid WarningColor '{dial.ColorConfig.WarningColor}'");

                if (!validColors.Contains(dial.ColorConfig.CriticalColor, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Dial '{dial.DisplayName}': Invalid CriticalColor '{dial.ColorConfig.CriticalColor}'");
            }

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// General application settings for the WPF app.
    /// </summary>
    public class AppSettings
    {
        /// <summary>Auto-connect to VU1 Hub on startup</summary>
        [JsonPropertyName("autoConnect")]
        public bool AutoConnect { get; set; } = true;

        /// <summary>Enable/disable HWInfo64 polling</summary>
        [JsonPropertyName("enablePolling")]
        public bool EnablePolling { get; set; } = true;

        /// <summary>Global polling interval in milliseconds</summary>
        [JsonPropertyName("globalUpdateIntervalMs")]
        public int GlobalUpdateIntervalMs { get; set; } = 1000;

        /// <summary>Log file path (empty = no logging)</summary>
        [JsonPropertyName("logFilePath")]
        public string LogFilePath { get; set; } = string.Empty;

        /// <summary>Enable debug logging</summary>
        [JsonPropertyName("debugMode")]
        public bool DebugMode { get; set; } = false;

        /// <summary>Delay in milliseconds between firmware detail queries during initialization (default: 50ms)</summary>
        [JsonPropertyName("serialCommandDelayMs")]
        public int SerialCommandDelayMs { get; set; } = 50;

        /// <summary>Start the application minimized to system tray</summary>
        [JsonPropertyName("startMinimized")]
        public bool StartMinimized { get; set; } = true;

        /// <summary>Override the number of active dials (null = use all detected dials, 1-4 = limit to specified count)</summary>
        [JsonPropertyName("dialCountOverride")]
        public int? DialCountOverride { get; set; } = null;
    }
}
