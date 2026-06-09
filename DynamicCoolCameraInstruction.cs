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

    [ExportMetadata("Name", "Dynamic Cool Camera")]
    [ExportMetadata("Description", "Set the camera cooling target from ambient temperature. Context-aware: full cool-down at the start of a night, incremental colder steps when re-checked between targets.")]
    [ExportMetadata("Icon", "SnowflakeSVG")]
    [ExportMetadata("Category", "Dynamic Cooling")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DynamicCoolCameraInstruction : SequenceItem {

        private ICameraMediator cameraMediator;
        private IWeatherDataMediator weatherDataMediator;
        private IFocuserMediator focuserMediator;

        [JsonProperty]
        public int TemperatureSource { get; set; }

        [JsonProperty]
        public double MaxDelta { get; set; } = 35.0;

        [JsonProperty]
        public double MinimumTarget { get; set; } = -20.0;

        [JsonProperty]
        public double FallbackTarget { get; set; } = -10.0;

        [JsonProperty]
        public int CoolingDurationMinutes { get; set; } = 5;

        [JsonProperty]
        public double Tolerance { get; set; } = 1.0;

        /// <summary>
        /// When true (default), a between-targets re-check only ever steps the
        /// setpoint colder — it never warms the camera back up mid-session. Has
        /// no effect on the initial cool-down.
        /// </summary>
        [JsonProperty]
        public bool OnlyColder { get; set; } = true;

        /// <summary>
        /// Live, human-readable temperature sources for the editor dropdown.
        /// Rebuilt from the connected devices each time the editor reads it.
        /// Not serialized — the selection is stored as the integer <see cref="TemperatureSource"/>.
        /// </summary>
        [JsonIgnore]
        public IList<TempSourceOption> SourceOptions => new List<TempSourceOption> {
            new TempSourceOption(0, DescribeWeatherSource()),
            new TempSourceOption(1, DescribeFocuserSource()),
            new TempSourceOption(2, "Manual (use fallback target)")
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
        public DynamicCoolCameraInstruction(ICameraMediator cameraMediator, IWeatherDataMediator weatherDataMediator, IFocuserMediator focuserMediator) {
            Name = "Dynamic Cool Camera";
            this.cameraMediator = cameraMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.focuserMediator = focuserMediator;
        }

        public DynamicCoolCameraInstruction() {
            Name = "Dynamic Cool Camera";
        }

        public override object Clone() {
            return new DynamicCoolCameraInstruction(cameraMediator, weatherDataMediator, focuserMediator) {
                TemperatureSource = TemperatureSource,
                MaxDelta = MaxDelta,
                MinimumTarget = MinimumTarget,
                FallbackTarget = FallbackTarget,
                CoolingDurationMinutes = CoolingDurationMinutes,
                Tolerance = Tolerance,
                OnlyColder = OnlyColder
            };
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            double ambient = GetAmbientTemperature();

            double calculatedTarget;
            if (TemperatureSource == 2) {
                calculatedTarget = FallbackTarget;
                Logger.Info($"DynamicCool: Manual mode, target = {calculatedTarget:F1}°C");
            } else if (double.IsNaN(ambient)) {
                calculatedTarget = FallbackTarget;
                Logger.Warning($"DynamicCool: No ambient temperature available, using fallback target {FallbackTarget:F1}°C");
            } else {
                string source = (TemperatureSource == 0) ? "Weather Device" : "Focuser";
                double rawTarget = ambient - MaxDelta;
                double roundedTarget = Math.Ceiling(rawTarget / 5.0) * 5.0;
                calculatedTarget = Math.Max(roundedTarget, MinimumTarget);
                Logger.Info($"DynamicCool: Ambient = {ambient:F1}°C (from {source}), Delta = {MaxDelta:F0}°C, Raw target = {rawTarget:F1}°C, Rounded to 5°C step = {roundedTarget:F0}°C, Final target = {calculatedTarget:F0}°C (min {MinimumTarget:F0}°C)");
            }

            CameraInfo info = cameraMediator.GetInfo();
            if (!info.Connected) {
                Logger.Error("DynamicCool: Camera is not connected!");
                throw new SequenceEntityFailedException("Camera is not connected");
            }

            // Context-aware: if the cooler is off or the sensor is still well above the
            // target, this is an initial cool-down. Otherwise it's a between-targets
            // re-check and we only nudge the setpoint.
            bool coldStart = !info.CoolerOn || (info.Temperature - calculatedTarget >= 3.0);

            if (coldStart) {
                await ExecuteColdStart(calculatedTarget, info, ambient, progress, token);
            } else {
                if (TemperatureSource == 2) {
                    Logger.Info("DynamicCool: Manual mode and already cooled — nothing to readjust.");
                    return;
                }
                await ExecuteReadjust(calculatedTarget, info, ambient, progress, token);
            }
        }

        // Full ramped cool-down from warm/off, with warm-night fallback to a sustainable step.
        private async Task ExecuteColdStart(double calculatedTarget, CameraInfo info, double ambient, IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Info($"DynamicCool: Cold start — sensor {info.Temperature:F1}°C, cooling to {calculatedTarget:F0}°C, timeout {CoolingDurationMinutes} min");
            progress?.Report(new ApplicationStatus {
                Status = $"Dynamic Cooling: {ambient:F0}°C ambient → {calculatedTarget:F0}°C target"
            });

            bool reached = await cameraMediator.CoolCamera(calculatedTarget, TimeSpan.FromMinutes(CoolingDurationMinutes), progress, token);
            if (reached) {
                Logger.Info($"DynamicCool: Target reached! Sensor at {calculatedTarget:F0}°C");
                return;
            }

            info = cameraMediator.GetInfo();
            double achieved = info.Temperature;
            double coolerPower = info.CoolerPower;
            Logger.Warning($"DynamicCool: Could not reach {calculatedTarget:F0}°C. Achieved = {achieved:F1}°C, Cooler power = {coolerPower:F0}%.");

            double fallbackStep = calculatedTarget + 5.0;
            if (fallbackStep <= 0.0 && coolerPower >= 80.0) {
                Logger.Info($"DynamicCool: Falling back to {fallbackStep:F0}°C (next 5°C step)...");
                progress?.Report(new ApplicationStatus {
                    Status = $"Cooling fallback: trying {fallbackStep:F0}°C"
                });
                bool fallbackReached = await cameraMediator.CoolCamera(fallbackStep, TimeSpan.FromMinutes(2.0), progress, token);
                if (fallbackReached) {
                    Logger.Info($"DynamicCool: Fallback target reached! Sensor at {fallbackStep:F0}°C");
                    return;
                }
            }

            // Settle on a sustainable target at the nearest 5°C step above what the TEC actually achieved.
            info = cameraMediator.GetInfo();
            double sensorTemp = info.Temperature;
            double sustainableTarget = Math.Ceiling(sensorTemp / 5.0) * 5.0;
            Logger.Info($"DynamicCool: Setting sustainable target to {sustainableTarget:F0}°C (nearest 5°C step above achieved {sensorTemp:F1}°C). TEC will run at reduced power.");
            await cameraMediator.CoolCamera(sustainableTarget, TimeSpan.FromSeconds(30.0), progress, token);
            Logger.Info($"DynamicCool: Imaging will proceed at {sustainableTarget:F0}°C. Darks should be taken at this temperature.");
        }

        // Between-targets re-check: only step the setpoint when ambient has moved a full 5°C library step.
        private async Task ExecuteReadjust(double newTarget, CameraInfo info, double ambient, IProgress<ApplicationStatus> progress, CancellationToken token) {
            double sensorTemp = info.Temperature;
            double coolerPower = info.CoolerPower;
            double currentStep = Math.Round(sensorTemp / 5.0) * 5.0;
            Logger.Info($"DynamicCool: Readjust check — Ambient = {ambient:F1}°C, Sensor = {sensorTemp:F1}°C (step {currentStep:F0}°C), Cooler = {coolerPower:F0}%, New optimal = {newTarget:F0}°C");

            double delta = currentStep - newTarget;
            if (delta < 5.0 && OnlyColder) {
                Logger.Info($"DynamicCool: No adjustment needed (current {currentStep:F0}°C, optimal {newTarget:F0}°C, need ≥5°C colder to trigger)");
                return;
            }
            if (delta > -5.0 && delta < 5.0) {
                Logger.Info($"DynamicCool: No adjustment needed (current {currentStep:F0}°C ≈ optimal {newTarget:F0}°C)");
                return;
            }
            if (delta < -5.0 && OnlyColder) {
                Logger.Info($"DynamicCool: Ambient warmed — optimal is {newTarget:F0}°C (warmer than current {currentStep:F0}°C), but OnlyColder=true — skipping");
                return;
            }

            if (delta > 0.0 && coolerPower >= 90.0) {
                Logger.Warning($"DynamicCool: TEC at {coolerPower:F0}% — too much load to step colder. Staying at {currentStep:F0}°C");
                return;
            }

            string direction = (delta > 0.0) ? "colder" : "warmer";
            Logger.Info($"DynamicCool: Stepping {direction} from {currentStep:F0}°C → {newTarget:F0}°C (ambient {ambient:F1}°C, delta {MaxDelta:F0}°C)");
            progress?.Report(new ApplicationStatus {
                Status = $"Dynamic Cool Readjust: {currentStep:F0}°C → {newTarget:F0}°C"
            });

            bool reached = await cameraMediator.CoolCamera(newTarget, TimeSpan.FromMinutes(CoolingDurationMinutes), progress, token);
            if (reached) {
                Logger.Info($"DynamicCool: New target {newTarget:F0}°C reached!");
                return;
            }

            info = cameraMediator.GetInfo();
            Logger.Warning($"DynamicCool: Could not reach {newTarget:F0}°C (sensor at {info.Temperature:F1}°C, cooler {info.CoolerPower:F0}%). Continuing at current temperature.");
            double achievable = Math.Ceiling(info.Temperature / 5.0) * 5.0;
            if (achievable > newTarget) {
                Logger.Info($"DynamicCool: Resetting to achievable {achievable:F0}°C");
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
                    Logger.Warning("DynamicCool: Weather device not connected or no temperature data");
                }

                FocuserInfo focuser = focuserMediator.GetInfo();
                if (focuser.Connected && !double.IsNaN(focuser.Temperature)) {
                    return focuser.Temperature;
                }
                Logger.Warning("DynamicCool: Focuser not connected or no temperature data");
                return double.NaN;
            } catch (Exception ex) {
                Logger.Error("DynamicCool: Error reading temperature: " + ex.Message);
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
            return $"Category: Dynamic Cooling, Item: DynamicCoolCamera, Source: {source}, MaxDelta: {MaxDelta}°C, MinTarget: {MinimumTarget}°C, Timeout: {CoolingDurationMinutes}min, OnlyColder: {OnlyColder}";
        }
    }
}
