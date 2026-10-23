using SimpleJSON;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.1.0: a slider's "step" makes it save only min, min + step, min + 2 × step and so on, and max.
    /// </summary>
    public class SliderStepTests
    {
        public SliderStepTests() => TestGame.Reset();

        private const string Stepped = "A_Stepped";

        /// <summary>
        /// A 0 to 100 slider in steps of 5, starting at 12.
        /// </summary>
        private static Menu OpenMenu(string step = "5")
        {
            TestGame.AddMod("Mod A", $$"""
                [ { "type": "slider", "varID": "A_Stepped", "labelID": "TEST__VOLUME", "minValue": 0, "maxValue": 100, "defaultValue": 12, "step": {{step}} } ]
                """);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            return menu;
        }

        [Theory]
        [InlineData(12, 5, 64, 10, 15)]
        [InlineData(9.9f, 5, 64, 10, 5)]
        [InlineData(10, 5, 64, 10, 15)]     // halfway goes up
        [InlineData(59.6f, 5, 64, 10, 64)]  // max can be picked though 64 isn't a step
        [InlineData(58, 5, 64, 10, 55)]
        [InlineData(64, 5, 64, 10, 64)]
        [InlineData(70, 5, 64, 10, 64)]
        [InlineData(0, 5, 64, 10, 5)]
        [InlineData(-12, -20, 20, 5, -10)]
        [InlineData(40, 0, 100, 25, 50)]
        public void SnapsToTheNearestStep(float value, float min, float max, int step, float expected)
        {
            Assert.Equal(expected, SnapToStep(value, min, max, step));
        }

        /// <summary>
        /// Without a step, sliders save what they always have: the nearest whole number.
        /// </summary>
        [Fact]
        public void StepOneIsTheNearestWholeNumberAsBefore()
        {
            for (int i = 0; i <= 1000; i++)
            {
                float position = i / 1000f;
                float value = 1 + position * 19;
                Assert.Equal(Mathf.Round(value), SnapToStep(value, 1, 20, 1));
            }
        }

        [Theory]
        [InlineData("{}", true, 1)]
        [InlineData("""{ "step": 5 }""", true, 5)]
        [InlineData("""{ "step": 1 }""", true, 1)]
        [InlineData("""{ "step": 0 }""", false, 1)]
        [InlineData("""{ "step": -2 }""", false, 1)]
        [InlineData("""{ "step": 2.5 }""", false, 1)]
        [InlineData("""{ "step": "abc" }""", false, 1)]
        public void StepMustBeAWholeNumberOfAtLeastOne(string json, bool valid, int expected)
        {
            Assert.Equal(valid, TryGetSliderStep(JSON.Parse(json), out int step));
            Assert.Equal(expected, step);
        }

        [Fact]
        public void OpensAtItsDefaultOnTheNearestStep()
        {
            Menu menu = OpenMenu();

            Assert.Equal(10, menu.Item(Stepped).tempValue);
            Assert.Equal(0.1, menu.SliderPosition(Stepped), 5);
            Assert.Equal("Volume: 10", menu.SliderText(Stepped));
        }

        /// <summary>
        /// Dragging keeps the nearest step, and the handle jumps there, so it moves in steps.
        /// </summary>
        [Theory]
        [InlineData(0.33f, 35)]
        [InlineData(0.32f, 30)]
        [InlineData(0.999f, 100)]
        [InlineData(0f, 0)]
        public void DraggingSavesOnlySteps(float position, float expected)
        {
            Menu menu = OpenMenu();

            menu.Drag(Stepped, position);

            Assert.Equal(expected, menu.Item(Stepped).tempValue);
            Assert.Equal(expected / 100, menu.SliderPosition(Stepped), 5);
            Assert.Equal("Volume: " + expected, menu.SliderText(Stepped));
            menu.Apply();
            Assert.Equal(FormatSetting(expected), TestGame.Saved(Stepped));
        }

        /// <summary>
        /// A value saved before the slider had its step opens at the nearest step.
        /// </summary>
        [Fact]
        public void ASavedValueOffItsStepsOpensAtTheNearest()
        {
            TestGame.Save(Stepped, "33");
            Menu menu = OpenMenu();

            Assert.Equal(35, menu.Item(Stepped).tempValue);
            menu.Apply();
            Assert.Equal("35", TestGame.Saved(Stepped));
        }

        /// <summary>
        /// A step that can't be used is 1, with a warning, so the slider works as it did before steps.
        /// </summary>
        [Theory]
        [InlineData("2.5")]
        [InlineData("0")]
        public void ABadStepMovesInOnes(string step)
        {
            Menu menu = OpenMenu(step);

            menu.Drag(Stepped, 0.333f);

            Assert.Equal(33, menu.Item(Stepped).tempValue);
            Assert.Equal(0.333, menu.SliderPosition(Stepped), 5);
            Assert.Contains(Log.Messages, m => m.StartsWith("[ModMenus] The slider A_Stepped's step"));
        }

        /// <summary>
        /// Without a step the handle stays where it was dragged, as before.
        /// </summary>
        [Fact]
        public void WithoutAStepTheHandleIsNotMoved()
        {
            TestGame.AddMod("Mod A", """[ { "type": "slider", "varID": "A_Plain", "labelID": "TEST__VOLUME" } ]""");
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            menu.Drag("A_Plain", 0.333f);

            Assert.Equal(33, menu.Item("A_Plain").tempValue);
            Assert.Equal(0.333, menu.SliderPosition("A_Plain"), 5);
            Assert.DoesNotContain(Log.Messages, m => m.Contains("step"));
        }
    }
}
