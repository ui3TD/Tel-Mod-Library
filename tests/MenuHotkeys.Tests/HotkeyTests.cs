using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;
using static MenuHotkeys.Tests.TestGame;
using TabType = Tabs_Manager._tab._type;

namespace MenuHotkeys.Tests
{
    /// <summary>
    /// Each home row key opens its main menu tab, unless a popup or dialogue is blocking hotkeys.
    /// </summary>
    public class HotkeyTests
    {
        public HotkeyTests() => Reset();

        [Theory]
        [MemberData(nameof(HotkeyData), MemberType = typeof(TestGame))]
        public void Key_OpensItsTab(KeyCode key, TabType tab)
        {
            Press(key);

            (Tabs_Manager manager, TabType opened) = Assert.Single(Seams.OpenedTabs);
            Assert.Same(Tabs, manager);
            Assert.Equal(tab, opened);
        }

        [Fact]
        public void EveryKeyOpensADifferentTab()
        {
            Assert.Equal(Hotkeys.Length, Hotkeys.Select(h => h.Key).Distinct().Count());
            Assert.Equal(Hotkeys.Length, Hotkeys.Select(h => h.Tab).Distinct().Count());
        }

        [Fact]
        public void Key_WhileItsTabIsOpen_OpensItAgain()
        {
            Press(KeyCode.A);
            Press(KeyCode.A);

            Assert.Equal(new[] { TabType.idols, TabType.idols }, Seams.OpenedTabs.Select(t => t.Tab));
        }

        [Fact]
        public void SeveralKeysInOneFrame_OpenOneTab()
        {
            Press(KeyCode.S, KeyCode.K);

            Assert.Single(Seams.OpenedTabs);
        }

        /// <summary>
        /// L is the next home row key; the rest are vanilla hotkeys, which Controls.Update handles itself.
        /// </summary>
        [Theory]
        [InlineData(KeyCode.None)]
        [InlineData(KeyCode.L)]
        [InlineData(KeyCode.Space)]
        [InlineData(KeyCode.Alpha1)]
        [InlineData(KeyCode.Tab)]
        [InlineData(KeyCode.Escape)]
        [InlineData(KeyCode.M)]
        public void OtherKeys_DoNothing(KeyCode key)
        {
            Press(key);

            Assert.Empty(Seams.OpenedTabs);
        }

        [Theory]
        [InlineData("popup")]
        [InlineData("dialogue")]
        [InlineData("debug popup")]
        public void Keys_WhileHotkeysBlocked_DoNothing(string blocker)
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

            foreach ((KeyCode key, _, _) in Hotkeys)
                Press(key);

            Assert.Empty(Seams.OpenedTabs);
        }
    }

    /// <summary>
    /// The mod's keys don't already do something in vanilla.
    /// </summary>
    public class VanillaKeyTests
    {
        [Fact]
        public void Keys_AreNotVanillaHotkeys()
        {
            HashSet<KeyCode> vanilla = VanillaHotkeys();
            Assert.Contains(KeyCode.Space, vanilla);
            Assert.Contains(KeyCode.M, vanilla);

            foreach ((KeyCode key, _, _) in Hotkeys)
                Assert.DoesNotContain(key, vanilla);
        }

        /// <summary>
        /// The keys vanilla's Controls.Update reads before its debug-mode cheats.
        /// </summary>
        private static HashSet<KeyCode> VanillaHotkeys()
        {
            MethodInfo update = AccessTools.Method(typeof(Controls), "Update");
            FieldInfo debugEnabled = AccessTools.Field(typeof(DEBUG), nameof(DEBUG.Debug_Enabled));
            HashSet<KeyCode> keys = new();
            int? lastConstant = null;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(update))
            {
                if (instruction.LoadsField(debugEnabled))
                    return keys;
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(Input) && method.Name.StartsWith("GetKey")
                    && method.GetParameters().Single().ParameterType == typeof(KeyCode))
                {
                    Assert.NotNull(lastConstant);
                    keys.Add((KeyCode)lastConstant.Value);
                }
                lastConstant = IntConstant(instruction);
            }
            Assert.Fail("Controls.Update no longer checks DEBUG.Debug_Enabled");
            return keys;
        }

        private static int? IntConstant(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldc_I4 || instruction.opcode == OpCodes.Ldc_I4_S)
                return System.Convert.ToInt32(instruction.operand);
            OpCode[] shortForms = { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1, OpCodes.Ldc_I4_2, OpCodes.Ldc_I4_3, OpCodes.Ldc_I4_4, OpCodes.Ldc_I4_5, OpCodes.Ldc_I4_6, OpCodes.Ldc_I4_7, OpCodes.Ldc_I4_8 };
            int index = System.Array.IndexOf(shortForms, instruction.opcode);
            return index >= 0 ? index : null;
        }
    }
}
