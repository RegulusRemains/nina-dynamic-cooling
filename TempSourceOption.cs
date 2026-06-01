namespace NINA.Plugin.DynamicCooling {

    /// <summary>
    /// One selectable temperature source for the dropdown. Value is the stored
    /// integer (0 = Weather, 1 = Focuser, 2 = Manual); Label is the live,
    /// human-readable description shown in the ComboBox.
    /// </summary>
    public class TempSourceOption {
        public int Value { get; }
        public string Label { get; }

        public TempSourceOption(int value, string label) {
            Value = value;
            Label = label;
        }
    }
}
