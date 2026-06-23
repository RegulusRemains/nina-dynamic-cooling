using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NINA.Plugin.DynamicCooling {

    /// <summary>
    /// Helpers for snapping a computed cooling target onto a user-defined set of
    /// dark-library temperatures (e.g. "0, -10, -15, -20"), so the camera only
    /// ever stabilizes at a temperature the user already has matching darks for.
    ///
    /// The set need NOT be evenly spaced — "0,-10,-15,-20" is a valid mix of 10°C
    /// and 5°C gaps. When the set is empty/blank, every helper falls back to the
    /// legacy uniform 5°C grid so existing sequences behave exactly as before.
    /// </summary>
    internal static class TemperatureSteps {

        /// <summary>Legacy uniform step used when no explicit set is configured.</summary>
        internal const double LegacyStep = 5.0;

        /// <summary>
        /// Parse a list like "0,-10,-15,-20" (commas, semicolons, or whitespace
        /// separated) into a distinct, ascending array. Invalid tokens are skipped.
        /// Returns an empty array when nothing valid is present.
        /// </summary>
        internal static double[] Parse(string spec) {
            if (string.IsNullOrWhiteSpace(spec)) {
                return Array.Empty<double>();
            }
            var vals = new List<double>();
            var tokens = spec.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var tok in tokens) {
                if (double.TryParse(tok.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) {
                    vals.Add(v);
                }
            }
            return vals.Distinct().OrderBy(v => v).ToArray();
        }

        /// <summary>
        /// Snap toward the warm/achievable side: return the COLDEST allowed value
        /// that is still ≥ raw (i.e. the TEC can reach it). If raw is colder than
        /// every allowed value, clamp to the coldest available. If raw is warmer
        /// than all, return the warmest. Empty set → legacy Ceiling to 5°C.
        /// </summary>
        internal static double SnapAchievable(double raw, double[] allowed) {
            if (allowed == null || allowed.Length == 0) {
                return Math.Ceiling(raw / LegacyStep) * LegacyStep;
            }
            // allowed is ascending: [0] = coldest, [^1] = warmest.
            double? coldestReachable = null;
            foreach (var v in allowed) {
                if (v >= raw) { coldestReachable = v; break; }  // first (coldest) that is reachable
            }
            return coldestReachable ?? allowed[allowed.Length - 1];  // none reachable → warmest available
        }

        /// <summary>
        /// Return the next allowed value strictly warmer than <paramref name="current"/>,
        /// or <paramref name="current"/> itself if none exists. Empty set → current + 5°C.
        /// Used for the warmer-retry fallback when the TEC can't hold the first target.
        /// </summary>
        internal static double NextWarmer(double current, double[] allowed) {
            if (allowed == null || allowed.Length == 0) {
                return current + LegacyStep;
            }
            foreach (var v in allowed) {           // ascending — first one above current is the next step warmer
                if (v > current) { return v; }
            }
            return current;
        }

        /// <summary>
        /// Return the allowed value nearest to <paramref name="temp"/> (the step the
        /// camera is presently stabilized at). Ties resolve to the warmer value.
        /// Empty set → legacy Round to nearest 5°C.
        /// </summary>
        internal static double SnapNearest(double temp, double[] allowed) {
            if (allowed == null || allowed.Length == 0) {
                return Math.Round(temp / LegacyStep) * LegacyStep;
            }
            double best = allowed[0];
            double bestDist = Math.Abs(allowed[0] - temp);
            foreach (var v in allowed) {
                double d = Math.Abs(v - temp);
                if (d < bestDist || (d == bestDist && v > best)) {
                    best = v;
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>Human-readable summary of the configured set, for logging/ToString.</summary>
        internal static string Describe(double[] allowed) {
            if (allowed == null || allowed.Length == 0) {
                return "5°C grid";
            }
            return "[" + string.Join(",", allowed.Select(v => v.ToString("F0", CultureInfo.InvariantCulture))) + "]°C";
        }
    }
}
