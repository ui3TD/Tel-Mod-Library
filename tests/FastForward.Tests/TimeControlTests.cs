using UnityEngine;
using Xunit;
using static FastForward.Tests.TestGame;

namespace FastForward.Tests
{
    /// <summary>
    /// Clicking the fast-forward button a second time switches on super-fast.
    /// </summary>
    public class DoubleClickTests
    {
        public DoubleClickTests() => Reset();

        [Fact]
        public void FirstClick_IsVanillaFast()
        {
            Click(mainScript._time_state.fast);

            Assert.Equal(mainScript._time_state.fast, staticVars.timeState);
            Assert.Equal(200d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.green32, LabelColor(Main.TimeControls_Fast));
        }

        [Fact]
        public void SecondClick_SwitchesOnSuperFast()
        {
            Click(mainScript._time_state.fast);
            Click(mainScript._time_state.fast);

            Assert.Equal(mainScript._time_state.fast, staticVars.timeState);
            Assert.Equal(1000d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.gold32, LabelColor(Main.TimeControls_Fast));
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Pause));
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Normal));
        }

        [Fact]
        public void SecondClick_UsesSpeedSetting()
        {
            SetMultiplier("10");

            Click(mainScript._time_state.fast);
            Click(mainScript._time_state.fast);

            Assert.Equal(2000d, staticVars.dateTimeAddMinutesPerSecond);
        }

        [Fact]
        public void ThirdClick_ReturnsToVanillaFast()
        {
            Click(mainScript._time_state.fast);
            Click(mainScript._time_state.fast);
            Click(mainScript._time_state.fast);

            Assert.Equal(mainScript._time_state.fast, staticVars.timeState);
            Assert.Equal(200d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.green32, LabelColor(Main.TimeControls_Fast));
        }

        /// <summary>
        /// One click on fast is vanilla fast, two is super-fast; either way the other buttons work as in vanilla.
        /// </summary>
        [Theory]
        [InlineData(1, mainScript._time_state.pause, 0d)]
        [InlineData(1, mainScript._time_state.normal, 50d)]
        [InlineData(2, mainScript._time_state.pause, 0d)]
        [InlineData(2, mainScript._time_state.normal, 50d)]
        public void OtherButtons_AreVanilla(int fastClicks, mainScript._time_state button, double vanillaSpeed)
        {
            for (int i = 0; i < fastClicks; i++)
                Click(mainScript._time_state.fast);

            Click(button);

            Assert.Equal(button, staticVars.timeState);
            Assert.Equal(vanillaSpeed, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Fast));
        }
    }

    /// <summary>
    /// Pressing '4' switches on super-fast, unless a popup or dialogue is blocking hotkeys.
    /// </summary>
    public class HotkeyTests
    {
        public HotkeyTests() => Reset();

        [Theory]
        [InlineData(mainScript._time_state.pause)]
        [InlineData(mainScript._time_state.normal)]
        [InlineData(mainScript._time_state.fast)]
        public void Pressing4_SwitchesOnSuperFast(mainScript._time_state from)
        {
            Seams.TimeSetState(Main, from);

            Press(KeyCode.Alpha4);

            Assert.Equal(mainScript._time_state.fast, staticVars.timeState);
            Assert.Equal(1000d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.gold32, LabelColor(Main.TimeControls_Fast));
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Pause));
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Normal));
        }

        [Fact]
        public void Pressing4_UsesSpeedSetting()
        {
            SetMultiplier("2.5");

            Press(KeyCode.Alpha4);

            Assert.Equal(500d, staticVars.dateTimeAddMinutesPerSecond);
        }

        [Fact]
        public void Pressing4_WhenSuperFast_StaysSuperFast()
        {
            Press(KeyCode.Alpha4);
            Press(KeyCode.Alpha4);

            Assert.Equal(1000d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.gold32, LabelColor(Main.TimeControls_Fast));
        }

        /// <summary>
        /// '3' is vanilla's fast hotkey, which Controls.Update handles itself.
        /// </summary>
        [Theory]
        [InlineData(KeyCode.None)]
        [InlineData(KeyCode.Alpha3)]
        public void OtherKeys_DoNothing(KeyCode key)
        {
            Press(key);

            AssertStillNormalSpeed();
        }

        [Theory]
        [InlineData("popup")]
        [InlineData("dialogue")]
        [InlineData("debug popup")]
        public void Pressing4_WhileHotkeysBlocked_DoesNothing(string blocker)
        {
            switch (blocker)
            {
                case "popup":
                    PopupManager.PopupCounter = 1;
                    break;
                case "dialogue":
                    ActiveDialogueController.ShowingDialogue = true;
                    break;
                case "debug popup":
                    DEBUG.ShowingPopup = true;
                    break;
            }
            Assert.True(mainScript.IsBlockingHotkeys());

            Press(KeyCode.Alpha4);

            AssertStillNormalSpeed();
        }

        private static void AssertStillNormalSpeed()
        {
            Assert.Equal(mainScript._time_state.normal, staticVars.timeState);
            Assert.Equal(50d, staticVars.dateTimeAddMinutesPerSecond);
            Assert.Equal((Color)mainScript.white32, LabelColor(Main.TimeControls_Fast));
        }
    }
}
