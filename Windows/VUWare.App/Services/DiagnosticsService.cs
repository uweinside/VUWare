using System;
using System.Collections.Generic;
using System.Linq;
using VUWare.HWInfo64;
using VUWare.Lib.Sensors;

namespace VUWare.App.Services
{
    /// <summary>
    /// Diagnostic service for debugging sensor monitoring issues.
    /// Provides detailed information about sensor matching and polling status.
    /// </summary>
    public class DiagnosticsService
    {
        private readonly ISensorProvider _sensorProvider;
        private readonly HWInfo64Controller? _hwInfoController;

        /// <summary>
        /// Creates a DiagnosticsService using ISensorProvider abstraction.
        /// </summary>
        public DiagnosticsService(ISensorProvider sensorProvider)
        {
            _sensorProvider = sensorProvider ?? throw new ArgumentNullException(nameof(sensorProvider));
        }

        /// <summary>
        /// Creates a DiagnosticsService with HWInfo64Controller for backward compatibility.
        /// This constructor supports dial mapping diagnostics specific to HWInfo64.
        /// </summary>
        public DiagnosticsService(HWInfo64Controller hwInfoController)
        {
            _hwInfoController = hwInfoController ?? throw new ArgumentNullException(nameof(hwInfoController));
            _sensorProvider = hwInfoController.SensorProvider;
        }

        /// <summary>
        /// Gets detailed diagnostic information about sensor provider connection and sensors.
        /// </summary>
        public string GetDiagnosticsReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine($"=== {_sensorProvider.ProviderName} Diagnostics Report ===");
            report.AppendLine();

            // Connection status
            report.AppendLine($"Provider: {_sensorProvider.ProviderName}");
            report.AppendLine($"Connected: {_sensorProvider.IsConnected}");
            
            // HWInfo64-specific properties
            if (_hwInfoController != null)
            {
                report.AppendLine($"Initialized: {_hwInfoController.IsInitialized}");
                report.AppendLine($"Poll Interval: {_hwInfoController.PollIntervalMs}ms");
            }
            report.AppendLine();

            if (!_sensorProvider.IsConnected)
            {
                report.AppendLine($"? {_sensorProvider.ProviderName} is not connected!");
                report.AppendLine("Please ensure:");
                report.AppendLine("  1. The sensor software is running");
                report.AppendLine("  2. Shared memory/API access is enabled");
                return report.ToString();
            }

            // Available sensors
            var sensors = _sensorProvider.GetAllReadings();
            report.AppendLine($"Available Sensors: {sensors.Count}");
            report.AppendLine();

            if (sensors.Count == 0)
            {
                report.AppendLine("? No sensors found!");
                return report.ToString();
            }

            // Group by sensor name
            var grouped = sensors.GroupBy(s => s.SensorName).OrderBy(g => g.Key);
            foreach (var sensorGroup in grouped)
            {
                report.AppendLine($"?? {sensorGroup.Key}");
                foreach (var reading in sensorGroup.OrderBy(r => r.EntryName))
                {
                    report.AppendLine($"  ?? {reading.EntryName}");
                    report.AppendLine($"  ?  Value: {reading.Value:F2} {reading.Unit}");
                    report.AppendLine($"  ?  Range: [{reading.ValueMin:F2}, {reading.ValueMax:F2}]");
                }
                report.AppendLine();
            }

            // Registered mappings (HWInfo64-specific)
            if (_hwInfoController != null)
            {
                var mappings = _hwInfoController.GetAllMappings();
                report.AppendLine($"Registered Mappings: {mappings.Count}");
                report.AppendLine();

                if (mappings.Count == 0)
                {
                    report.AppendLine("? No sensor mappings registered!");
                    return report.ToString();
                }

                foreach (var mapping in mappings.Values)
                {
                    report.AppendLine($"?? {mapping.DisplayName}");
                    report.AppendLine($"  ID: {mapping.Id}");
                    report.AppendLine($"  Sensor: {mapping.SensorName}");
                    report.AppendLine($"  Entry: {mapping.EntryName}");

                    // Try to find matching sensor
                    var matchingSensor = sensors.FirstOrDefault(s =>
                        s.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                        s.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                    if (matchingSensor != null)
                    {
                        report.AppendLine($"  ? Status: MATCHED");
                        report.AppendLine($"  Value: {matchingSensor.Value:F2} {matchingSensor.Unit}");
                        var percentage = mapping.GetPercentage(matchingSensor.Value);
                        report.AppendLine($"  Percentage: {percentage}%");
                    }
                    else
                    {
                        report.AppendLine($"  ? Status: NOT FOUND");
                        
                        // Try to find partial matches
                        var partialMatches = sensors.Where(s =>
                            s.SensorName.Contains(mapping.SensorName, StringComparison.OrdinalIgnoreCase) ||
                            mapping.SensorName.Contains(s.SensorName, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (partialMatches.Count > 0)
                        {
                            report.AppendLine($"  ?? Possible matches (sensor name contains):");
                            foreach (var partial in partialMatches.Take(3))
                            {
                                report.AppendLine($"    - {partial.SensorName} > {partial.EntryName}");
                            }
                        }

                        // Try to find entry matches
                        var entryMatches = sensors.Where(s =>
                            s.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (entryMatches.Count > 0)
                        {
                            report.AppendLine($"  ?? Found entries with matching name:");
                            foreach (var entry in entryMatches.Take(3))
                            {
                                report.AppendLine($"    - {entry.SensorName} > {entry.EntryName}");
                            }
                        }
                    }

                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Validates that all configured sensors can be found.
        /// </summary>
        public List<string> ValidateSensorMappings()
        {
            var issues = new List<string>();

            if (!_sensorProvider.IsConnected)
            {
                issues.Add($"{_sensorProvider.ProviderName} is not connected");
                return issues;
            }

            var sensors = _sensorProvider.GetAllReadings();
            if (sensors.Count == 0)
            {
                issues.Add($"No sensors found in {_sensorProvider.ProviderName}");
                return issues;
            }

            // HWInfo64-specific mapping validation
            if (_hwInfoController != null)
            {
                var mappings = _hwInfoController.GetAllMappings();
                if (mappings.Count == 0)
                {
                    issues.Add("No sensor mappings registered");
                    return issues;
                }

                foreach (var mapping in mappings.Values)
                {
                    var found = sensors.FirstOrDefault(s =>
                        s.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                        s.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                    if (found == null)
                    {
                        issues.Add($"Sensor not found: '{mapping.SensorName}' > '{mapping.EntryName}'");
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// Gets a simple status summary for display.
        /// </summary>
        public string GetStatusSummary()
        {
            if (!_sensorProvider.IsConnected)
                return $"? {_sensorProvider.ProviderName} not connected";

            var sensors = _sensorProvider.GetAllReadings();
            if (sensors.Count == 0)
                return "? No sensors available";

            // HWInfo64-specific mapping status
            if (_hwInfoController != null)
            {
                var mappings = _hwInfoController.GetAllMappings();
                var validMappings = 0;

                foreach (var mapping in mappings.Values)
                {
                    var found = sensors.FirstOrDefault(s =>
                        s.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                        s.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                    if (found != null)
                        validMappings++;
                }

                return $"? {validMappings}/{mappings.Count} sensors matched";
            }

            return $"? {sensors.Count} sensors available";
        }
    }
}
