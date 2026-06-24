using System.ComponentModel.Composition;
using System.Windows;

namespace NINA.Plugin.DynamicCooling {

    [Export(typeof(ResourceDictionary))]
    public partial class DewHeaterTriggerTemplate : ResourceDictionary {

        public DewHeaterTriggerTemplate() {
            InitializeComponent();
        }
    }
}
