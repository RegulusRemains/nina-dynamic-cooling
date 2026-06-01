using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using Newtonsoft.Json;

namespace NINA.Plugin.DynamicCooling {

    [ExportMetadata("Name", "Dynamic Cool Readjust")]
    [ExportMetadata("Description", "Re-check ambient temperature between targets and step colder if possible")]
    [ExportMetadata("Icon", "SnowflakeSVG")]
    [ExportMetadata("Category", "Dynamic Cooling")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DynamicCoolReadjustInstruction : SequenceItem {

        private ICameraMediator cameraMediator;
        private IWeatherDataMediator weatherDataMediator;
        private IFocuserMediator focuserMediator;

        [JsonProperty]
        public int TemperatureSource { get; set; }

        [JsonProperty]
        public double MaxDelta { get; set; } = 30.0;

        [JsonProperty]
        public double MinimumTarget { get; set; } = -20.0;

        [JsonProperty]
        public int CoolingDurationMinutes { get; set; } = 3;

        [JsonProperty]
        public bool OnlyColder { get; set; } = true;

        /// <summary>
        /// Live, human-readable temperature sources for the editor dropdown.
        /// Not serialized — the selection is stored as the integer <see cref="TemperatureSource"/>.
        /// </summary>
        [JsonIgnore]
        public IList<TempSourceOption> SourceOptions => new List<TempSourceOption> {
            new TempSourceOption(0, DescribeWeatherSource()),
            new TempSourceOption(1, DescribeFocuserSource()),
            new TempSourceOption(2, "Manual (skip readjust)")
        };

        private string DescribeWeatherSource() {
            try {
                var info = weatherDataMediator?.GetInfo();
                if (info == null || !info.Connected) { return "Weather device - not connected"; }
                string reading = double.IsNaN(info.Temperature) ? "no temp" : $"{info.Temperature:F1}°C";
                return string.IsNullOrWhiteSpace(info.Name) ? $"Weather device - {reading}" : $"Weather device - {info.Name} ({reading})";
            } catch { return "Weather device"; }
        }

        private string DescribeFocuserSource() {
            try {
                var info = focuserMediator?.GetInfo();
                if (info == null || !info.Connected) { return "Focuser probe - not connected"; }
                string reading = double.IsNaN(info.Temperature) ? "no temp probe" : $"{info.Temperature:F1}°C";
                return string.IsNullOrWhiteSpace(info.Name) ? $"Focuser probe - {reading}" : $"Focuser probe - {info.Name} ({reading})";
            } catch { return "Focuser probe"; }
        }

        [ImportingConstructor]
        public DynamicCoolReadjustInstruction(ICameraMediator cameraMediator, IWeatherDataMediator weatherDataMediator, IFocuserMediator focuserMediator) {
            Name = "Dynamic Cool Readjust";
            this.cameraMediator = cameraMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.focuserMediator = focuserMediator;
        }

        public DynamicCoolReadjustInstruction() {
            Name = "Dynamic Cool Readjust";
        }

        public override object Clone() {
            return new DynamicCoolReadjustInstruction(cameraMediator, weatherDataMediator, focuserMediator) {
                TemperatureSource = TemperatureSource,
                MaxDelta = MaxDelta,
                MinimumTarget = MinimumTarget,
                CoolingDurationMinutes = CoolingDurationMinutes,
                OnlyColder = OnlyColder
            };
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (TemperatureSource == 2) {
                Logger.Info("DynamicCoolReadjust: Manual mode — skipping readjust");
                return;
            }

            CameraInfo info = cameraMediator.GetInfo();
            if (!info.Connected) {
                Logger.Warning("DynamicCoolReadjust: Camera not connected — skipping");
                return;
            }
            if (!info.CoolerOn) {
                Logger.Warning("DynamicCoolReadjust: Cooler is off — skipping");
                return;
            }

            double ambient = GetAmbientTemperature();
            if (double.IsNaN(ambient)) {
                Logger.Warning("DynamicCoolReadjust: No ambient reading available — skipping");
                return;
            }

            double sensorTemp = info.Temperature;
            double coolerPower = info.CoolerPower;
            double currentStep = Math.Round(sensorTemp / 5.0) * 5.0;
            double rawTarget = ambient - MaxDelta;
            double newTarget = Math.Ceiling(rawTarget / 5.0) * 5.0;
            newTarget = Math.Max(newTarget, MinimumTarget);
            Logger.Info($"DynamicCoolReadjust: Ambient = {ambient:F1}°C, Sensor = {sensorTemp:F1}°C (step {currentStep:F0}°C), Cooler = {coolerPower:F0}%, New optimal = {newTarget:F0}°C");

            double delta = currentStep - newTarget;
            if (delta < 5.0 && OnlyColder) {
                Logger.Info($"DynamicCoolReadjust: No adjustment needed (current {currentStep:F0}°C, optimal {newTarget:F0}°C, need ≥5°C colder to trigger)");
                return;
            }
            if (delta > -5.0 && delta < 5.0) {
                Logger.Info($"DynamicCoolReadjust: No adjustment needed (current {currentStep:F0}°C ≈ optimal {newTarget:F0}°C)");
                return;
            }
            if (delta < -5.0 && OnlyColder) {
                Logger.Info($"DynamicCoolReadjust: Ambient warmed — optimal is {newTarget:F0}°C (warmer than current {currentStep:F0}°C), but OnlyColder=true — skipping");
                return;
            }

            if (delta > 0.0 && coolerPower >= 90.0) {
                Logger.Warning($"DynamicCoolReadjust: TEC at {coolerPower:F0}% — too much load to step colder. Staying at {currentStep:F0}°C");
                return;
            }

            string direction = (delta > 0.0) ? "colder" : "warmer";
            Logger.Info($"DynamicCoolReadjust: Stepping {direction} from {currentStep:F0}°C → {newTarget:F0}°C (ambient {ambient:F1}°C, delta {MaxDelta:F0}°C)");
            progress?.Report(new ApplicationStatus {
                Status = $"Dynamic Cool Readjust: {currentStep:F0}°C → {newTarget:F0}°C"
            });

            bool reached = await cameraMediator.CoolCamera(newTarget, TimeSpan.FromMinutes(CoolingDurationMinutes), progress, token);
            if (reached) {
                Logger.Info($"DynamicCoolReadjust: New target {newTarget:F0}°C reached!");
                return;
            }

            info = cameraMediator.GetInfo();
            Logger.Warning($"DynamicCoolReadjust: Could not reach {newTarget:F0}°C (sensor at {info.Temperature:F1}°C, cooler {info.CoolerPower:F0}%). Continuing at current temperature.");
            double achievable = Math.Ceiling(info.Temperature / 5.0) * 5.0;
            if (achievable > newTarget) {
                Logger.Info($"DynamicCoolReadjust: Resetting to achievable {achievable:F0}°C");
                await cameraMediator.CoolCamera(achievable, TimeSpan.FromSeconds(30.0), progress, token);
            }
        }

        private double GetAmbientTemperature() {
            try {
                if (TemperatureSource != 0 && TemperatureSource != 1) {
                    return double.NaN;
                }

                if (TemperatureSource == 0) {
                    WeatherDataInfo weather = weatherDataMediator.GetInfo();
                    if (weather.Connected && !double.IsNaN(weather.Temperature)) {
                        return weather.Temperature;
                    }
                    Logger.Warning("DynamicCoolReadjust: Weather device unavailable, trying focuser");
                }

                FocuserInfo focuser = focuserMediator.GetInfo();
                if (focuser.Connected && !double.IsNaN(focuser.Temperature)) {
                    return focuser.Temperature;
                }
                Logger.Warning("DynamicCoolReadjust: Focuser temperature unavailable");
                return double.NaN;
            } catch (Exception ex) {
                Logger.Error("DynamicCoolReadjust: Error reading temperature: " + ex.Message);
                return double.NaN;
            }
        }

        public override string ToString() {
            string source = TemperatureSource switch {
                0 => "Weather",
                1 => "Focuser",
                2 => "Manual",
                _ => "Unknown",
            };
            return $"Category: Dynamic Cooling, Item: DynamicCoolReadjust, Source: {source}, MaxDelta: {MaxDelta}°C, MinTarget: {MinimumTarget}°C, OnlyColder: {OnlyColder}";
        }
    }
}
