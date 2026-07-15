using System;
using System.Collections.Generic;
using System.Linq;
using NINA.Plugin;
using NINA.Profile;
using NINA.Profile.Interfaces;

namespace NINA.Plugin.DynamicCooling {

    /// <summary>
    /// Central definition of the plugin's persisted settings: the keys, their
    /// defaults, and the fixed 5°C temperature grid. Both the plugin options page
    /// and the Dynamic Cool Camera instruction read/write through a
    /// <see cref="PluginOptionsAccessor"/> built from these. The instruction uses
    /// these values unless a supported per-step override is specified.
    /// Settings are stored per NINA profile.
    /// </summary>
    internal static class DynamicCoolingOptions {

        /// <summary>Must match the [Guid] in AssemblyInfo.cs — namespaces the stored settings.</summary>
        internal const string PluginGuid = "25ac9c96-885e-4733-a437-a5d4863a1c7e";

        internal static PluginOptionsAccessor CreateAccessor(IProfileService profileService) {
            return new PluginOptionsAccessor(profileService, Guid.Parse(PluginGuid));
        }

        // ── General setting keys + defaults ───────────────────────────────────
        internal const string KeySource = "TemperatureSource";
        internal const string KeyMaxDelta = "MaxDelta";
        internal const string KeyTimeout = "CoolingDurationMinutes";

        internal const int DefSource = 0;          // 0 = Weather (or focuser), 1 = Focuser
        internal const double DefMaxDelta = 30.0;  // °C below ambient a typical cooled CMOS TEC sustains
        internal const int DefTimeout = 5;

        /// <summary>The fixed 5°C grid (warmest → coldest): key, temperature, default-enabled.</summary>
        internal static readonly (string Key, double Temp, bool Default)[] Grid = {
            ("Use_p5",   5.0, false),
            ("Use_0",    0.0, true),
            ("Use_m5",  -5.0, true),
            ("Use_m10",-10.0, true),
            ("Use_m15",-15.0, true),
            ("Use_m20",-20.0, true),
            ("Use_m25",-25.0, false),
            ("Use_m30",-30.0, false),
            ("Use_m35",-35.0, false),
            ("Use_m40",-40.0, false),
        };

        internal static bool DefaultFor(string key) {
            foreach (var g in Grid) { if (g.Key == key) { return g.Default; } }
            return false;
        }

        /// <summary>
        /// Enabled temperatures from the grid, distinct + ascending. Empty (nothing
        /// ticked) means "no explicit set" → callers fall back to the legacy 5°C grid.
        /// </summary>
        internal static double[] GetAllowedSet(PluginOptionsAccessor s) {
            var vals = new List<double>();
            foreach (var g in Grid) {
                if (s.GetValueBoolean(g.Key, g.Default)) { vals.Add(g.Temp); }
            }
            return vals.Distinct().OrderBy(v => v).ToArray();
        }
    }
}
