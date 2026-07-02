using NUnit.Framework;
using System;
using System.Reflection;

namespace NINA.Plugin.DynamicCooling.Tests {

    /// <summary>
    /// Tests for <c>TemperatureSteps</c> — the dark-library snapping logic every
    /// Dynamic Cool Camera decision goes through. The class is internal, so the
    /// tests reach it by reflection off the plugin assembly (anchored on the
    /// public <see cref="DynamicCoolingPlugin"/> type).
    /// </summary>
    [TestFixture]
    public class TemperatureStepsTests {

        private static readonly Type Steps =
            typeof(DynamicCoolingPlugin).Assembly.GetType("NINA.Plugin.DynamicCooling.TemperatureSteps", throwOnError: true);

        private static T Call<T>(string method, params object[] args) {
            var mi = Steps.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(mi, Is.Not.Null, $"TemperatureSteps.{method} not found");
            return (T)mi.Invoke(null, args);
        }

        // Ascending = [0] coldest … [^1] warmest, as GetAllowedSet produces.
        private static readonly double[] Grid = { -20.0, -15.0, -10.0, -5.0, 0.0 };
        private static readonly double[] Uneven = { -20.0, -15.0, -10.0, 0.0 };

        // ── Parse ──────────────────────────────────────────────────────────────
        [Test]
        public void Parse_MixedSeparators_SortedDistinctAscending() {
            var result = Call<double[]>("Parse", "0, -10;-15 -20\t-10");
            Assert.That(result, Is.EqualTo(new[] { -20.0, -15.0, -10.0, 0.0 }));
        }

        [Test]
        public void Parse_InvalidTokensSkipped_BlankGivesEmpty() {
            Assert.That(Call<double[]>("Parse", "abc,-5,x"), Is.EqualTo(new[] { -5.0 }));
            Assert.That(Call<double[]>("Parse", "   "), Is.Empty);
            Assert.That(Call<double[]>("Parse", (string)null), Is.Empty);
        }

        // ── SnapAchievable ─────────────────────────────────────────────────────
        [Test]
        public void SnapAchievable_PicksColdestReachableValue() {
            // TEC can reach -12 → coldest enabled temp that is ≥ -12 is -10.
            Assert.That(Call<double>("SnapAchievable", -12.0, Grid), Is.EqualTo(-10.0));
            // Uneven grid: -12 skips the missing -5/-10... -10 present here too.
            Assert.That(Call<double>("SnapAchievable", -14.0, Uneven), Is.EqualTo(-10.0));
        }

        [Test]
        public void SnapAchievable_RawColderThanAll_ReturnsColdestEnabled() {
            Assert.That(Call<double>("SnapAchievable", -45.0, Grid), Is.EqualTo(-20.0));
        }

        [Test]
        public void SnapAchievable_RawWarmerThanAll_ReturnsWarmestEnabled() {
            Assert.That(Call<double>("SnapAchievable", 3.0, Grid), Is.EqualTo(0.0));
        }

        [Test]
        public void SnapAchievable_EmptySet_FallsBackToLegacy5Ceiling() {
            Assert.That(Call<double>("SnapAchievable", -12.0, Array.Empty<double>()), Is.EqualTo(-10.0));
            Assert.That(Call<double>("SnapAchievable", -12.0, (double[])null), Is.EqualTo(-10.0));
        }

        // ── NextWarmer ─────────────────────────────────────────────────────────
        [Test]
        public void NextWarmer_StepsToNextEnabledValue() {
            Assert.That(Call<double>("NextWarmer", -15.0, Grid), Is.EqualTo(-10.0));
            // Uneven grid: warmer than -10 is 0 (no -5 enabled).
            Assert.That(Call<double>("NextWarmer", -10.0, Uneven), Is.EqualTo(0.0));
        }

        [Test]
        public void NextWarmer_AtWarmest_ReturnsCurrent() {
            Assert.That(Call<double>("NextWarmer", 0.0, Grid), Is.EqualTo(0.0));
        }

        [Test]
        public void NextWarmer_EmptySet_LegacyPlus5() {
            Assert.That(Call<double>("NextWarmer", -15.0, Array.Empty<double>()), Is.EqualTo(-10.0));
        }

        // ── SnapNearest ────────────────────────────────────────────────────────
        [Test]
        public void SnapNearest_PicksNearest_TieResolvesWarmer() {
            Assert.That(Call<double>("SnapNearest", -12.0, Grid), Is.EqualTo(-10.0));
            // Exactly between -15 and -10 → warmer wins.
            Assert.That(Call<double>("SnapNearest", -12.5, Grid), Is.EqualTo(-10.0));
        }

        [Test]
        public void SnapNearest_EmptySet_LegacyRoundTo5() {
            Assert.That(Call<double>("SnapNearest", -12.0, Array.Empty<double>()), Is.EqualTo(-10.0));
            Assert.That(Call<double>("SnapNearest", -13.0, (double[])null), Is.EqualTo(-15.0));
        }

        // ── Describe ───────────────────────────────────────────────────────────
        [Test]
        public void Describe_FormatsSetAndLegacyFallback() {
            Assert.That(Call<string>("Describe", new object[] { new[] { -10.0, 0.0 } }), Is.EqualTo("[-10,0]°C"));
            Assert.That(Call<string>("Describe", new object[] { Array.Empty<double>() }), Is.EqualTo("5°C grid"));
        }
    }
}
