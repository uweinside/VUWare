using System;
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
        /// Creates a new empty configuration with default settings.
        /// </summary>
        /// <returns>New DialsConfiguration instance</returns>
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
                    DebugMode = false
                },
                Dials = new()
            };
        }
    }
}
