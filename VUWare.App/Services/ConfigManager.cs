using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VUWare.App.Models;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages loading and saving dial configuration from/to JSON files.
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initializes a new ConfigManager with the specified configuration file path.
        /// </summary>
        /// <param name="configPath">Full path to the JSON configuration file</param>
        public ConfigManager(string configPath)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        }

        /// <summary>
        /// Loads the dial configuration from the JSON file.
        /// </summary>
        /// <returns>DialsConfiguration object, or null if file doesn't exist</returns>
        /// <exception cref="JsonException">Thrown if JSON is malformed</exception>
        /// <exception cref="IOException">Thrown if file cannot be read</exception>
        public DialsConfiguration? Load()
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<DialsConfiguration>(json, JsonOptions);
                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse configuration file at '{_configPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously loads the dial configuration from the JSON file.
        /// </summary>
        /// <returns>DialsConfiguration object, or null if file doesn't exist</returns>
        public async Task<DialsConfiguration?> LoadAsync()
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<DialsConfiguration>(json, JsonOptions);
                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse configuration file at '{_configPath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves the dial configuration to the JSON file.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="config">Configuration to save</param>
        /// <exception cref="IOException">Thrown if file cannot be written</exception>
        public void Save(DialsConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string directory = Path.GetDirectoryName(_configPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }

        /// <summary>
        /// Asynchronously saves the dial configuration to the JSON file.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="config">Configuration to save</param>
        public async Task SaveAsync(DialsConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string directory = Path.GetDirectoryName(_configPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configPath, json);
        }

        /// <summary>
        /// Gets the path to the default configuration file.
        /// Checks AppData first, then falls back to local Config directory.
        /// </summary>
        /// <returns>Path to dials-config.json</returns>
        public static string GetDefaultConfigPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataConfig = Path.Combine(appDataPath, "VUWare", "dials-config.json");
            
            // If file exists in AppData, use it; otherwise use local Config directory
            if (File.Exists(appDataConfig))
            {
                return appDataConfig;
            }

            // Fallback to local Config directory (for development/bundled configs)
            string localConfig = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                "dials-config.json");
            
            return localConfig;
        }

        /// <summary>
        /// Tries to load config from the default location, with fallback logic.
        /// Checks AppData first, then local Config directory.
        /// </summary>
        /// <returns>Loaded configuration or null if not found</returns>
        public static DialsConfiguration? LoadDefault()
        {
            var manager = new ConfigManager(GetDefaultConfigPath());
            return manager.Load();
        }

        /// <summary>
        /// Asynchronously tries to load config from the default location, with fallback logic.
        /// </summary>
        /// <returns>Loaded configuration or null if not found</returns>
        public static async Task<DialsConfiguration?> LoadDefaultAsync()
        {
            var manager = new ConfigManager(GetDefaultConfigPath());
            return await manager.LoadAsync();
        }

        /// <summary>
        /// Creates a new configuration with default settings and 4 placeholder dials.
        /// The runInit flag is set to true so the settings window is shown after first discovery.
        /// </summary>
        /// <returns>New DialsConfiguration instance with placeholder dials</returns>
        public static DialsConfiguration CreateDefault()
        {
            return new DialsConfiguration
            {
                Version = "1.0",
                AppSettings = new AppSettings
                {
                    AutoConnect = true,
                    EnablePolling = true,
                    GlobalUpdateIntervalMs = 1000,
                    LogFilePath = "",
                    DebugMode = false,
                    SerialCommandDelayMs = 150,
                    DialCountOverride = null,
                    StartMinimized = false,
                    RunInit = true  // Show settings window after first discovery
                },
                Dials = new List<DialConfig>
                {
                    CreatePlaceholderDial(1, "PLACEHOLDER_DIAL_1"),
                    CreatePlaceholderDial(2, "PLACEHOLDER_DIAL_2"),
                    CreatePlaceholderDial(3, "PLACEHOLDER_DIAL_3"),
                    CreatePlaceholderDial(4, "PLACEHOLDER_DIAL_4")
                }
            };
        }

        /// <summary>
        /// Creates a placeholder dial configuration for initial setup.
        /// </summary>
        private static DialConfig CreatePlaceholderDial(int dialNumber, string placeholderUid)
        {
            return new DialConfig
            {
                DialUid = placeholderUid,
                DisplayName = $"Dial {dialNumber}",
                SensorName = "",
                EntryName = "",
                MinValue = 0,
                MaxValue = 100,
                WarningThreshold = 80,
                CriticalThreshold = 95,
                ColorConfig = new DialColorConfig
                {
                    ColorMode = "off",
                    StaticColor = "Off",
                    NormalColor = "Cyan",
                    WarningColor = "Yellow",
                    CriticalColor = "Red"
                },
                Enabled = true,
                UpdateIntervalMs = 1000,
                DisplayFormat = "percentage",
                DisplayUnit = ""
            };
        }

        /// <summary>
        /// Checks if a configuration file exists at any of the default locations.
        /// </summary>
        /// <returns>True if config file exists, false otherwise</returns>
        public static bool ConfigFileExists()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataConfig = Path.Combine(appDataPath, "VUWare", "dials-config.json");
            
            if (File.Exists(appDataConfig))
                return true;

            string localConfig = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                "dials-config.json");
            
            return File.Exists(localConfig);
        }

        /// <summary>
        /// Creates and saves a default configuration file if one doesn't exist.
        /// </summary>
        /// <returns>True if a new config was created, false if it already existed</returns>
        public static bool EnsureDefaultConfigExists()
        {
            if (ConfigFileExists())
            {
                return false;
            }

            // Create config in AppData location
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataConfig = Path.Combine(appDataPath, "VUWare", "dials-config.json");
            
            var config = CreateDefault();
            var manager = new ConfigManager(appDataConfig);
            manager.Save(config);
            
            System.Diagnostics.Debug.WriteLine($"[ConfigManager] Created default configuration at: {appDataConfig}");
            return true;
        }
    }
}
