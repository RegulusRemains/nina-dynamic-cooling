using Moq;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NUnit.Framework;
using System;
using System.Reflection;

namespace NINA.Plugin.DynamicCooling.Tests {

    /// <summary>
    /// Behavioural tests for <see cref="DewHeaterTrigger"/> against real NINA types.
    /// Default margin is 3 °C with a 2 °C hysteresis band, so the heater turns ON when
    /// (ambient − dewpoint) is at or below 3, OFF when it climbs to 5 or above, and holds
    /// the current state in between.
    ///
    /// We assert on <c>ShouldTrigger</c> (does a transition need to happen?) plus the
    /// trigger's computed <c>desiredState</c> — which is exactly the value <c>Execute</c>
    /// passes to <c>ICameraMediator.SetDewHeater</c>. We deliberately do NOT call
    /// <c>Execute</c>: it routes through NINA's static <c>Logger</c> (Serilog file/console
    /// sinks), which a unit test should not spin up. Verifying the decision is equivalent
    /// and keeps the tests fast, hermetic, and side-effect free.
    /// </summary>
    [TestFixture]
    public class DewHeaterTriggerTests {

        private static readonly Guid PluginGuid = Guid.Parse("25ac9c96-885e-4733-a437-a5d4863a1c7e");

        private Mock<ICameraMediator> cam;
        private Mock<IWeatherDataMediator> weather;
        private Mock<IProfileService> profileService;
        private PluginSettings settings;
        private CameraInfo camInfo;
        private WeatherDataInfo wxInfo;

        [SetUp]
        public void Setup() {
            settings = new PluginSettings();
            profileService = new Mock<IProfileService>();
            profileService.SetupGet(p => p.ActiveProfile.PluginSettings).Returns(settings);

            camInfo = new CameraInfo { Connected = true, HasDewHeater = true, DewHeaterOn = false };
            cam = new Mock<ICameraMediator>();
            cam.Setup(c => c.GetInfo()).Returns(camInfo);

            wxInfo = new WeatherDataInfo { Connected = true, Temperature = 25, DewPoint = 21, Humidity = double.NaN };
            weather = new Mock<IWeatherDataMediator>();
            weather.Setup(w => w.GetInfo()).Returns(wxInfo);
        }

        private DewHeaterTrigger Sut() => new DewHeaterTrigger(cam.Object, weather.Object, profileService.Object);

        private void Weather(double temp, double dewPoint, double humidity = double.NaN) {
            wxInfo.Temperature = temp; wxInfo.DewPoint = dewPoint; wxInfo.Humidity = humidity;
        }

        // The on/off value the trigger decided on; this is what Execute() hands to SetDewHeater.
        private static bool DesiredState(DewHeaterTrigger t) =>
            (bool)typeof(DewHeaterTrigger).GetField("desiredState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t);

        // Pre-arm the "already warned about missing weather data" latch so the no-data
        // gate test doesn't touch the static Logger.
        private static void SuppressNoDataWarning(DewHeaterTrigger t) =>
            typeof(DewHeaterTrigger).GetField("warnedNoData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(t, true);

        // ── ON / OFF transitions ─────────────────────────────────────────────

        [Test]
        public void TurnsOn_WhenSpreadAtOrBelowMargin() {
            Weather(22, 21);            // spread 1 -> within margin (Louie's 2026-06-24/25 condition)
            camInfo.DewHeaterOn = false;
            var sut = Sut();
            Assert.That(sut.ShouldTrigger(null, null), Is.True);
            Assert.That(DesiredState(sut), Is.True);
        }

        [Test]
        public void TurnsOff_WhenSpreadAtOrAboveUpperBand() {
            Weather(27, 21);            // spread 6 -> >= margin + hysteresis
            camInfo.DewHeaterOn = true;
            var sut = Sut();
            Assert.That(sut.ShouldTrigger(null, null), Is.True);
            Assert.That(DesiredState(sut), Is.False);
        }

        // ── No-change cases ──────────────────────────────────────────────────

        [Test]
        public void NoChange_WhenDryAndAlreadyOff() {
            Weather(31, 21);            // spread 10
            camInfo.DewHeaterOn = false;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        [Test]
        public void NoChange_WhenHumidAndAlreadyOn() {
            Weather(22, 21);            // spread 1
            camInfo.DewHeaterOn = true;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        [Test]
        public void HoldsState_InHysteresisDeadband_Off() {
            Weather(25, 21);            // spread 4 -> between 3 and 5
            camInfo.DewHeaterOn = false;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        [Test]
        public void HoldsState_InHysteresisDeadband_On() {
            Weather(25, 21);            // spread 4 -> between 3 and 5
            camInfo.DewHeaterOn = true;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        // ── Dew-point fallback ───────────────────────────────────────────────

        [Test]
        public void UsesMagnusFallback_WhenDeviceReportsNoDewPoint() {
            // T=20, RH=50% -> dew point ~9.3 -> spread ~10.7 -> heater should turn OFF
            Weather(20, double.NaN, humidity: 50);
            camInfo.DewHeaterOn = true;
            var sut = Sut();
            Assert.That(sut.ShouldTrigger(null, null), Is.True);
            Assert.That(DesiredState(sut), Is.False);
        }

        // ── Gating / safety ──────────────────────────────────────────────────

        [Test]
        public void NoOp_WhenCameraHasNoDewHeater() {
            Weather(22, 21);
            camInfo.HasDewHeater = false; camInfo.DewHeaterOn = false;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        [Test]
        public void NoOp_WhenWeatherDeviceDisconnected() {
            wxInfo.Connected = false;
            camInfo.DewHeaterOn = false;
            var sut = Sut();
            SuppressNoDataWarning(sut);
            Assert.That(sut.ShouldTrigger(null, null), Is.False);
        }

        [Test]
        public void NoOp_WhenDisabledInOptions() {
            settings.SetValue(PluginGuid, "DewHeaterEnabled", false);
            Weather(22, 21);
            camInfo.DewHeaterOn = false;
            Assert.That(Sut().ShouldTrigger(null, null), Is.False);
        }

        // ── Configuration is honoured ────────────────────────────────────────

        [Test]
        public void HonoursCustomMargin_FromOptions() {
            settings.SetValue(PluginGuid, "DewPointMargin", 10.0);
            Weather(27, 21);            // spread 6 -> within the widened 10 margin
            camInfo.DewHeaterOn = false;
            var sut = Sut();
            Assert.That(sut.ShouldTrigger(null, null), Is.True);
            Assert.That(DesiredState(sut), Is.True);
        }
    }
}
