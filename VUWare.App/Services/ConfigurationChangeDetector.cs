// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using VUWare.App.Models;

namespace VUWare.App.Services
{
    /// <summary>
    /// Detects and categorizes configuration changes between two DialsConfiguration instances.
    /// Used to determine minimal set of operations needed to apply configuration updates.
    /// </summary>
    public static class ConfigurationChangeDetector
    {
        /// <summary>
        /// Compares two configurations and returns flags indicating what changed.
        /// </summary>
        public static ConfigChangeType DetectChanges(
            DialsConfiguration oldConfig,
            DialsConfiguration newConfig)
        {
            if (oldConfig == null || newConfig == null)
            {
                return ConfigChangeType.All;
            }

            var changes = ConfigChangeType.None;

            // Check app settings changes
            if (!AppSettingsEqual(oldConfig.AppSettings, newConfig.AppSettings))
            {
                changes |= ConfigChangeType.AppSettings;
            }

            // Check global update interval
            if (oldConfig.AppSettings.GlobalUpdateIntervalMs != newConfig.AppSettings.GlobalUpdateIntervalMs)
            {
                changes |= ConfigChangeType.UpdateIntervals;
            }

            // Check each dial configuration
            foreach (var newDial in newConfig.Dials)
            {
                var oldDial = oldConfig.Dials.FirstOrDefault(d => d.DialUid == newDial.DialUid);

                if (oldDial == null)
                {
                    // New dial added
                    changes |= ConfigChangeType.SensorMappings;
                    changes |= ConfigChangeType.DialSettings;
                    continue;
                }

                // Check sensor mapping changes
                if (oldDial.SensorName != newDial.SensorName ||
                    oldDial.SensorId != newDial.SensorId ||
                    oldDial.SensorInstance != newDial.SensorInstance ||
                    oldDial.EntryName != newDial.EntryName ||
                    oldDial.EntryId != newDial.EntryId)
                {
                    changes |= ConfigChangeType.SensorMappings;
                }

                // Check dial settings (thresholds, colors, format, etc.)
                if (DialSettingsChanged(oldDial, newDial))
                {
                    changes |= ConfigChangeType.DialSettings;
                }

                // Check update interval
                if (oldDial.UpdateIntervalMs != newDial.UpdateIntervalMs)
                {
                    changes |= ConfigChangeType.UpdateIntervals;
                }
            }

            // Check for removed dials
            foreach (var oldDial in oldConfig.Dials)
            {
                if (!newConfig.Dials.Any(d => d.DialUid == oldDial.DialUid))
                {
                    changes |= ConfigChangeType.SensorMappings;
                }
            }

            return changes;
        }

        /// <summary>
        /// Checks if app settings are equal (excluding fields that don't require runtime changes).
        /// </summary>
        private static bool AppSettingsEqual(AppSettings old, AppSettings newSettings)
        {
            // Compare only runtime-relevant settings
            // Exclude: AutoConnect (startup only), StartMinimized (startup only)
            return old.EnablePolling == newSettings.EnablePolling &&
                   old.DebugMode == newSettings.DebugMode &&
                   old.SerialCommandDelayMs == newSettings.SerialCommandDelayMs &&
                   old.LogFilePath == newSettings.LogFilePath;
        }

        /// <summary>
        /// Checks if dial settings changed (excluding sensor mappings and intervals).
        /// </summary>
        private static bool DialSettingsChanged(DialConfig oldDial, DialConfig newDial)
        {
            // Check basic settings
            if (oldDial.DisplayName != newDial.DisplayName ||
                oldDial.MinValue != newDial.MinValue ||
                oldDial.MaxValue != newDial.MaxValue ||
                oldDial.WarningThreshold != newDial.WarningThreshold ||
                oldDial.CriticalThreshold != newDial.CriticalThreshold ||
                oldDial.DisplayFormat != newDial.DisplayFormat ||
                oldDial.DisplayUnit != newDial.DisplayUnit ||
                oldDial.DecimalPlaces != newDial.DecimalPlaces ||
                oldDial.Enabled != newDial.Enabled)
            {
                return true;
            }

            // Check color configuration
            var oldColor = oldDial.ColorConfig;
            var newColor = newDial.ColorConfig;

            if (oldColor.ColorMode != newColor.ColorMode ||
                oldColor.StaticColor != newColor.StaticColor ||
                oldColor.NormalColor != newColor.NormalColor ||
                oldColor.WarningColor != newColor.WarningColor ||
                oldColor.CriticalColor != newColor.CriticalColor)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a human-readable description of the changes.
        /// </summary>
        public static string GetChangeDescription(ConfigChangeType changes)
        {
            if (changes == ConfigChangeType.None)
            {
                return "No changes detected";
            }

            var parts = new System.Collections.Generic.List<string>();

            if (changes.HasFlag(ConfigChangeType.AppSettings))
            {
                parts.Add("application settings");
            }

            if (changes.HasFlag(ConfigChangeType.SensorMappings))
            {
                parts.Add("sensor mappings");
            }

            if (changes.HasFlag(ConfigChangeType.DialSettings))
            {
                parts.Add("dial settings");
            }

            if (changes.HasFlag(ConfigChangeType.UpdateIntervals))
            {
                parts.Add("update intervals");
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Determines if the changes require a full application restart.
        /// </summary>
        public static bool RequiresRestart(ConfigChangeType changes)
        {
            // Only specific app settings require restart
            // Most changes can be applied dynamically
            return changes.HasFlag(ConfigChangeType.AppSettings);
        }
    }
}
