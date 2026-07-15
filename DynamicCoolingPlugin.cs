using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;

namespace NINA.Plugin.DynamicCooling {

    /// <summary>
    /// Plugin manifest + settings store. All Dynamic Cooling configuration lives
    /// here (shown on the Plugins ▸ Dynamic Cooling options page) and is persisted
    /// per profile via <see cref="PluginOptionsAccessor"/>. The Dynamic Cool Camera
    /// instruction reads these settings and may override the cooling time per step.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class DynamicCoolingPlugin : PluginBase, INotifyPropertyChanged {

        private readonly PluginOptionsAccessor settings;

        [ImportingConstructor]
        public DynamicCoolingPlugin(IProfileService profileService) {
            settings = DynamicCoolingOptions.CreateAccessor(profileService);
        }

        public override Task Initialize() => Task.CompletedTask;
        public override Task Teardown() => Task.CompletedTask;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ── General cooling settings ──────────────────────────────────────────
        public int TemperatureSource {
            get => settings.GetValueInt32(DynamicCoolingOptions.KeySource, DynamicCoolingOptions.DefSource);
            set { settings.SetValueInt32(DynamicCoolingOptions.KeySource, value); RaisePropertyChanged(); }
        }

        public double MaxDelta {
            get => settings.GetValueDouble(DynamicCoolingOptions.KeyMaxDelta, DynamicCoolingOptions.DefMaxDelta);
            set { settings.SetValueDouble(DynamicCoolingOptions.KeyMaxDelta, value); RaisePropertyChanged(); }
        }

        public int CoolingDurationMinutes {
            get => settings.GetValueInt32(DynamicCoolingOptions.KeyTimeout, DynamicCoolingOptions.DefTimeout);
            set { settings.SetValueInt32(DynamicCoolingOptions.KeyTimeout, value); RaisePropertyChanged(); }
        }

        // ── Temperature grid (5°C steps) — each bound to a checkbox ────────────
        private bool GridGet(string key) => settings.GetValueBoolean(key, DynamicCoolingOptions.DefaultFor(key));
        private void GridSet(string key, bool value, string prop) {
            settings.SetValueBoolean(key, value);
            RaisePropertyChanged(prop);
        }

        public bool Use_p5  { get => GridGet("Use_p5");  set => GridSet("Use_p5",  value, nameof(Use_p5)); }
        public bool Use_0   { get => GridGet("Use_0");   set => GridSet("Use_0",   value, nameof(Use_0)); }
        public bool Use_m5  { get => GridGet("Use_m5");  set => GridSet("Use_m5",  value, nameof(Use_m5)); }
        public bool Use_m10 { get => GridGet("Use_m10"); set => GridSet("Use_m10", value, nameof(Use_m10)); }
        public bool Use_m15 { get => GridGet("Use_m15"); set => GridSet("Use_m15", value, nameof(Use_m15)); }
        public bool Use_m20 { get => GridGet("Use_m20"); set => GridSet("Use_m20", value, nameof(Use_m20)); }
        public bool Use_m25 { get => GridGet("Use_m25"); set => GridSet("Use_m25", value, nameof(Use_m25)); }
        public bool Use_m30 { get => GridGet("Use_m30"); set => GridSet("Use_m30", value, nameof(Use_m30)); }
        public bool Use_m35 { get => GridGet("Use_m35"); set => GridSet("Use_m35", value, nameof(Use_m35)); }
        public bool Use_m40 { get => GridGet("Use_m40"); set => GridSet("Use_m40", value, nameof(Use_m40)); }
    }
}
