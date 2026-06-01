using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace NINA.Plugin.DynamicCooling {

    [Export(typeof(IPluginManifest))]
    public class DynamicCoolingPlugin : PluginBase {

        [ImportingConstructor]
        public DynamicCoolingPlugin() {
        }

        public override Task Initialize() {
            return Task.CompletedTask;
        }

        public override Task Teardown() {
            return Task.CompletedTask;
        }
    }
}
