using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using Newtonsoft.Json;

namespace NINA.Plugin.DynamicCooling {

    [ExportMetadata("Name", "Dynamic Cool Camera")]
    [ExportMetadata("Description", "Cools the camera to the coldest enabled dark-library temperature it can reach for the current ambient temperature. Configured in Plugins ▸ Dynamic Cooling.")]
    [ExportMetadata("Icon", "SnowflakeSVG")]
    [ExportMetadata("Category", "Dynamic Cooling")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DynamicCoolCameraInstruction : SequenceItem {

        private readonly ICameraMediator cameraMediator;
        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IProfileService profileService;

        [ImportingConstructor]
        public DynamicCoolCameraInstruction(ICameraMediator cameraMediator, IWeatherDataMediator weatherDataMediator, IFocuserMediator focuserMediator, IProfileService profileService) {
            Name = "Dynamic Cool Camera";
            this.cameraMediator = cameraMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.focuserMediator = focuserMediator;
            this.profileService = profileService;
        }

        public DynamicCoolCameraInstruction() {
            Name = "Dynamic Cool Camera";
        }

        public override object Clone() {
            return new DynamicCoolCameraInstruction(cameraMediator, weatherDataMediator, focuserMediator, profileService);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (profileService == null) {
                Logger.Error("DynamicCool: profile service unavailable — cannot read plugin settings.");
                throw new SequenceEntityFailedException("Dynamic Cooling: plugin settings unavailable");
            }

            // All configuration comes from the Plugins ▸ Dynamic Cooling options page.
            var s = DynamicCoolingOptions.CreateAccessor(profileService);
            int source = s.GetValueInt32(DynamicCoolingOptions.KeySource, DynamicCoolingOptions.DefSource);
            double maxDelta = s.GetValueDouble(DynamicCoolingOptions.KeyMaxDelta, DynamicCoolingOptions.DefMaxDelta);
            int timeoutMin = s.GetValueInt32(DynamicCoolingOptions.KeyTimeout, DynamicCoolingOptions.DefTimeout);
            double[] steps = DynamicCoolingOptions.GetAllowedSet(s);  // enabled dark-library temps (empty → 5°C grid)

            double ambient = GetAmbientTemperature(source);

            double calculatedTarget;
            if (double.IsNaN(ambient)) {
                // No reading available — fall back to the warmest enabled temp (always reachable).
                if (steps.Length > 0) {
                    calculatedTarget = steps[steps.Length - 1];
                    Logger.Warning($"DynamicCool: No ambient temperature available — using warmest enabled dark-library temp {calculatedTarget:F0}°C");
                } else {
                    Logger.Warning("DynamicCool: No ambient temperature and no dark-library temps enabled — skipping cooling.");
                    return;
                }
            } else {
                string srcName = (source == 0) ? "Weather device" : "Focuser";
                double rawTarget = ambient - maxDelta;
                // Snap to the coldest enabled temp the camera can actually reach (rounds toward warmer).
                calculatedTarget = TemperatureSteps.SnapAchievable(rawTarget, steps);
                Logger.Info($"DynamicCool: Ambient = {ambient:F1}°C (from {srcName}), Delta = {maxDelta:F0}°C, Raw = {rawTarget:F1}°C, Target = {calculatedTarget:F0}°C (from {TemperatureSteps.Describe(steps)})");
            }

            CameraInfo info = cameraMediator.GetInfo();
            if (!info.Connected) {
                Logger.Error("DynamicCool: Camera is not connected!");
                throw new SequenceEntityFailedException("Camera is not connected");
            }

            // Context-aware: cooler off or sensor still well above target = initial cool-down;
            // otherwise it's a between-targets re-check that only nudges the setpoint colder.
            bool coldStart = !info.CoolerOn || (info.Temperature - calculatedTarget >= 3.0);

            if (coldStart) {
                await ExecuteColdStart(calculatedTarget, info, ambient, steps, timeoutMin, progress, token);
            } else {
                await ExecuteReadjust(calculatedTarget, info, ambient, steps, timeoutMin, progress, token);
            }
        }

        // Full ramped cool-down from warm/off, with warm-night fallback to a sustainable step.
        private async Task ExecuteColdStart(double calculatedTarget, CameraInfo info, double ambient, double[] steps, int timeoutMin, IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Info($"DynamicCool: Cold start — sensor {info.Temperature:F1}°C, cooling to {calculatedTarget:F0}°C, timeout {timeoutMin} min");
            progress?.Report(new ApplicationStatus { Status = $"Dynamic Cooling: cooling to {calculatedTarget:F0}°C" });

            bool reached = await cameraMediator.CoolCamera(calculatedTarget, TimeSpan.FromMinutes(timeoutMin), progress, token);
            if (reached) {
                Logger.Info($"DynamicCool: Target reached! Sensor at {calculatedTarget:F0}°C");
                return;
            }

            info = cameraMediator.GetInfo();
            double achieved = info.Temperature;
            double coolerPower = info.CoolerPower;
            Logger.Warning($"DynamicCool: Could not reach {calculatedTarget:F0}°C. Achieved = {achieved:F1}°C, Cooler power = {coolerPower:F0}%.");

            double fallbackStep = TemperatureSteps.NextWarmer(calculatedTarget, steps);
            if (fallbackStep > calculatedTarget && fallbackStep <= 0.0 && coolerPower >= 80.0) {
                Logger.Info($"DynamicCool: Falling back to {fallbackStep:F0}°C (next warmer step)...");
                progress?.Report(new ApplicationStatus { Status = $"Cooling fallback: trying {fallbackStep:F0}°C" });
                bool fallbackReached = await cameraMediator.CoolCamera(fallbackStep, TimeSpan.FromMinutes(2.0), progress, token);
                if (fallbackReached) {
                    Logger.Info($"DynamicCool: Fallback target reached! Sensor at {fallbackStep:F0}°C");
                    return;
                }
            }

            // Settle on the nearest enabled step above what the TEC actually reached.
            info = cameraMediator.GetInfo();
            double sensorTemp = info.Temperature;
            double sustainableTarget = TemperatureSteps.SnapAchievable(sensorTemp, steps);
            Logger.Info($"DynamicCool: Setting sustainable target to {sustainableTarget:F0}°C (nearest enabled step above achieved {sensorTemp:F1}°C). TEC will run at reduced power.");
            await cameraMediator.CoolCamera(sustainableTarget, TimeSpan.FromSeconds(30.0), progress, token);
            Logger.Info($"DynamicCool: Imaging will proceed at {sustainableTarget:F0}°C. Darks should be taken at this temperature.");
        }

        // Between-targets re-check: step the setpoint to a colder enabled temp as the night cools.
        // Never warms the camera back up mid-session.
        private async Task ExecuteReadjust(double newTarget, CameraInfo info, double ambient, double[] steps, int timeoutMin, IProgress<ApplicationStatus> progress, CancellationToken token) {
            double sensorTemp = info.Temperature;
            double coolerPower = info.CoolerPower;
            double currentStep = TemperatureSteps.SnapNearest(sensorTemp, steps);
            Logger.Info($"DynamicCool: Readjust check — Ambient = {ambient:F1}°C, Sensor = {sensorTemp:F1}°C (step {currentStep:F0}°C), Cooler = {coolerPower:F0}%, New optimal = {newTarget:F0}°C");

            if (newTarget >= currentStep) {
                Logger.Info($"DynamicCool: No colder step needed (current {currentStep:F0}°C, optimal {newTarget:F0}°C).");
                return;
            }

            if (coolerPower >= 90.0) {
                Logger.Warning($"DynamicCool: TEC at {coolerPower:F0}% — too much load to step colder. Staying at {currentStep:F0}°C");
                return;
            }

            Logger.Info($"DynamicCool: Stepping colder from {currentStep:F0}°C → {newTarget:F0}°C (ambient {ambient:F1}°C)");
            progress?.Report(new ApplicationStatus { Status = $"Dynamic Cooling: {currentStep:F0}°C → {newTarget:F0}°C" });

            bool reached = await cameraMediator.CoolCamera(newTarget, TimeSpan.FromMinutes(timeoutMin), progress, token);
            if (reached) {
                Logger.Info($"DynamicCool: New target {newTarget:F0}°C reached!");
                return;
            }

            info = cameraMediator.GetInfo();
            Logger.Warning($"DynamicCool: Could not reach {newTarget:F0}°C (sensor at {info.Temperature:F1}°C, cooler {info.CoolerPower:F0}%). Continuing at current temperature.");
            double achievable = TemperatureSteps.SnapAchievable(info.Temperature, steps);
            if (achievable > newTarget) {
                Logger.Info($"DynamicCool: Resetting to achievable {achievable:F0}°C");
                await cameraMediator.CoolCamera(achievable, TimeSpan.FromSeconds(30.0), progress, token);
            }
        }

        // Ambient from the selected source. Weather (0) falls back to the focuser probe.
        private double GetAmbientTemperature(int source) {
            try {
                if (source == 0) {
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
            return "Category: Dynamic Cooling, Item: DynamicCoolCamera (settings on Plugins ▸ Dynamic Cooling page)";
        }
    }
}
