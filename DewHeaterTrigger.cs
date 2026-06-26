using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyWeatherData;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using Newtonsoft.Json;

namespace NINA.Plugin.DynamicCooling {

    /// <summary>
    /// Sequence trigger that manages the camera's anti-dew heater. Before each
    /// instruction it compares the ambient air temperature to the dew point; when
    /// the gap closes (humid air → condensation risk on the camera window) it turns
    /// the heater on, and once the air dries out it turns it off again. A small
    /// hysteresis band keeps it from flapping. All settings live on the
    /// Plugins ▸ Dynamic Cooling options page, so the trigger has no per-step config.
    /// Dew point comes from a connected weather device (humidity is required).
    /// </summary>
    [ExportMetadata("Name", "Dew Heater Control")]
    [ExportMetadata("Description", "Turns the camera's dew heater on when the air gets close to the dew point and off again once it dries out. Configured in Plugins ▸ Dynamic Cooling. Needs a connected weather device and a camera connected with its native driver, not ASCOM.")]
    [ExportMetadata("Icon", "SnowflakeSVG")]
    [ExportMetadata("Category", "Dynamic Cooling")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DewHeaterTrigger : SequenceTrigger {

        private readonly ICameraMediator cameraMediator;
        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly IProfileService profileService;

        // Computed in ShouldTrigger, applied in Execute.
        private bool desiredState;
        private string lastReason = "";
        // Latches the "no weather data" warning so it's logged once per outage, not every instruction.
        private bool warnedNoData = false;

        [ImportingConstructor]
        public DewHeaterTrigger(ICameraMediator cameraMediator, IWeatherDataMediator weatherDataMediator, IProfileService profileService) {
            Name = "Dew Heater Control";
            this.cameraMediator = cameraMediator;
            this.weatherDataMediator = weatherDataMediator;
            this.profileService = profileService;
        }

        public DewHeaterTrigger() {
            Name = "Dew Heater Control";
        }

        /// <summary>
        /// Live one-line summary of the dew-heater settings, shown on the trigger
        /// block in the sequencer. Reflects whatever is configured on the options page.
        /// </summary>
        [JsonIgnore]
        public string Summary {
            get {
                if (profileService == null) { return "Configure in Plugins ▸ Dynamic Cooling"; }
                var s = DynamicCoolingOptions.CreateAccessor(profileService);
                bool enabled = s.GetValueBoolean(DynamicCoolingOptions.KeyDewEnabled, DynamicCoolingOptions.DefDewEnabled);
                if (!enabled) { return "Dew heater: off (enable in Plugins ▸ Dynamic Cooling)"; }
                double margin = s.GetValueDouble(DynamicCoolingOptions.KeyDewMargin, DynamicCoolingOptions.DefDewMargin);
                return $"Dew heater: on within {margin:0.#}°C of dew point";
            }
        }

        public override object Clone() {
            return new DewHeaterTrigger(cameraMediator, weatherDataMediator, profileService) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description
            };
        }

        // Evaluated before each instruction. Returns true only when the heater state
        // actually needs to change, so Execute only runs on a transition.
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            try {
                if (profileService == null) { return false; }
                var s = DynamicCoolingOptions.CreateAccessor(profileService);
                if (!s.GetValueBoolean(DynamicCoolingOptions.KeyDewEnabled, DynamicCoolingOptions.DefDewEnabled)) { return false; }

                CameraInfo cam = cameraMediator.GetInfo();
                if (!cam.Connected) { return false; }
                if (!cam.HasDewHeater) { return false; }

                double margin = s.GetValueDouble(DynamicCoolingOptions.KeyDewMargin, DynamicCoolingOptions.DefDewMargin);
                double spread = GetSpread();   // ambient − dew point; NaN if unavailable
                if (double.IsNaN(spread)) {
                    if (!warnedNoData) {
                        Logger.Warning("DynamicCool/Dew: no weather/dew-point reading — leaving dew heater unchanged. Dew control needs a connected weather device.");
                        warnedNoData = true;
                    }
                    return false;
                }
                warnedNoData = false;   // data is back; allow the warning to fire again on a future outage

                bool currentlyOn = cam.DewHeaterOn;
                // Hysteresis: ON when the spread is at/below the margin; OFF only once
                // it climbs past margin + band; otherwise hold the current state.
                bool wantOn;
                if (spread <= margin) {
                    wantOn = true;
                } else if (spread >= margin + DynamicCoolingOptions.DewHysteresis) {
                    wantOn = false;
                } else {
                    wantOn = currentlyOn;
                }

                if (wantOn == currentlyOn) { return false; }

                desiredState = wantOn;
                lastReason = $"spread {spread:0.0}°C vs margin {margin:0.#}°C";
                return true;
            } catch (Exception ex) {
                Logger.Error("DynamicCool/Dew: ShouldTrigger error: " + ex.Message);
                return false;
            }
        }

        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            bool on = desiredState;
            try {
                Logger.Info($"DynamicCool/Dew: turning dew heater {(on ? "ON" : "OFF")} ({lastReason}).");
                progress?.Report(new ApplicationStatus { Status = $"Dew heater {(on ? "ON" : "OFF")} ({lastReason})" });
                cameraMediator.SetDewHeater(on);
            } catch (Exception ex) {
                // A dew-heater toggle failure must never abort the imaging run.
                Logger.Error("DynamicCool/Dew: failed to set dew heater: " + ex.Message);
            }
            return Task.CompletedTask;
        }

        // Ambient air temp minus dew point, from the weather device (dew point needs
        // humidity). Prefers the device's reported DewPoint; computes it from T + RH
        // when the device doesn't report one directly.
        private double GetSpread() {
            try {
                WeatherDataInfo w = weatherDataMediator.GetInfo();
                if (!w.Connected) { return double.NaN; }
                double t = w.Temperature;
                double dp = w.DewPoint;
                if (double.IsNaN(dp) && !double.IsNaN(t) && !double.IsNaN(w.Humidity)) {
                    dp = DynamicCoolingOptions.DewPointFrom(t, w.Humidity);
                }
                if (double.IsNaN(t) || double.IsNaN(dp)) { return double.NaN; }
                return t - dp;
            } catch (Exception ex) {
                Logger.Error("DynamicCool/Dew: error reading weather: " + ex.Message);
                return double.NaN;
            }
        }

        public override string ToString() {
            return "Category: Dynamic Cooling, Item: DewHeaterTrigger (settings on Plugins ▸ Dynamic Cooling page)";
        }
    }
}
