// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;

namespace VUWare.App.Services
{
    /// <summary>
    /// Flags indicating which types of configuration changes occurred.
    /// Used to determine which components need to be updated during configuration reload.
    /// </summary>
    [Flags]
    public enum ConfigChangeType
    {
        /// <summary>No changes detected</summary>
        None = 0,

        /// <summary>App-level settings changed (may require restart)</summary>
        AppSettings = 1,

        /// <summary>Individual dial settings changed (thresholds, colors, etc.)</summary>
        DialSettings = 2,

        /// <summary>Sensor mappings changed (sensor/entry names)</summary>
        SensorMappings = 4,

        /// <summary>Update intervals changed (global or per-dial)</summary>
        UpdateIntervals = 8,

        /// <summary>All changes</summary>
        All = 15
    }
}
