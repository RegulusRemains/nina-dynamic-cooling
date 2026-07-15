using Newtonsoft.Json;
using NUnit.Framework;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace NINA.Plugin.DynamicCooling.Tests {

    [TestFixture]
    public class DynamicCoolCameraInstructionTests {

        [Test]
        [Apartment(ApartmentState.STA)]
        public void CoolingDurationEditor_AllowsClearingTheSequenceOverride() {
            var instruction = new DynamicCoolCameraInstruction {
                CoolingDurationMinutes = 9
            };
            var textBox = new TextBox { DataContext = instruction };
            BindingOperations.SetBinding(
                textBox,
                TextBox.TextProperty,
                new Binding(nameof(DynamicCoolCameraInstruction.CoolingDurationMinutesText)) {
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

            textBox.Text = string.Empty;
            textBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            Assert.Multiple(() => {
                Assert.That(Validation.GetHasError(textBox), Is.False);
                Assert.That(instruction.CoolingDurationMinutes, Is.Null);
            });
        }

        [Test]
        public void DefaultCoolingDurationText_ShowsConfiguredFallbackInParentheses() {
            var instruction = new DynamicCoolCameraInstruction();

            Assert.That(instruction.DefaultCoolingDurationText, Is.EqualTo("(5)"));
        }

        [Test]
        public void ResolveCoolingDurationMinutes_UsesSequenceValueWhenPresent() {
            int result = DynamicCoolCameraInstruction.ResolveCoolingDurationMinutes(12, 5);

            Assert.That(result, Is.EqualTo(12));
        }

        [Test]
        public void ResolveCoolingDurationMinutes_UsesConfiguredValueWhenSequenceValueIsEmpty() {
            int result = DynamicCoolCameraInstruction.ResolveCoolingDurationMinutes(null, 7);

            Assert.That(result, Is.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ResolveCoolingDurationMinutes_RejectsNonPositiveEffectiveValue(int value) {
            Assert.That(
                () => DynamicCoolCameraInstruction.ResolveCoolingDurationMinutes(value, 5),
                Throws.Exception.With.Message.EqualTo("Dynamic Cooling: cooling time must be greater than 0 minutes"));
        }

        [Test]
        public void CoolingDurationMinutes_RoundTripsThroughSequenceJson() {
            var instruction = new DynamicCoolCameraInstruction {
                CoolingDurationMinutes = 9
            };

            string json = JsonConvert.SerializeObject(instruction);
            var restored = JsonConvert.DeserializeObject<DynamicCoolCameraInstruction>(json);

            Assert.That(restored.CoolingDurationMinutes, Is.EqualTo(9));
        }

        [Test]
        public void Clone_PreservesSequenceCoolingDuration() {
            var instruction = new DynamicCoolCameraInstruction {
                CoolingDurationMinutes = 11
            };

            var clone = (DynamicCoolCameraInstruction)instruction.Clone();

            Assert.That(clone.CoolingDurationMinutes, Is.EqualTo(11));
        }

        [Test]
        public void CoolingDurationMinutes_IsOmittedFromSequenceJsonWhenEmpty() {
            string json = JsonConvert.SerializeObject(new DynamicCoolCameraInstruction());

            Assert.That(json, Does.Not.Contain("CoolingDurationMinutes"));
        }
    }
}
