using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;

using ModMenus.GameKeys;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.3.1: ModMenus alongside mods it doesn't know. Errors stay in ModMenus' own patches, so the
    /// game's methods and other mods' patches on them still run, and the menu has its own popup number.
    /// </summary>
    public class CompatibilityTests
    {
        public CompatibilityTests()
        {
            TestGame.Reset();
            SiteTranspiler.Reset();
            DebugCheats.Forget();
            AccessTools.Field(typeof(Sites), "errorLogged").SetValue(null, false);
            DEBUG.Debug_Enabled = false;
        }

        private static void Press(params KeyCode[] keys)
        {
            Seams.Frame++;
            Seams.KeysHeld.Clear();
            Seams.KeysHeld.UnionWith(keys);
            Seams.KeysDown.Clear();
            Seams.KeysDown.UnionWith(keys);
            Seams.KeysUp.Clear();
        }

        [Fact]
        public void TheSettingsTabPostfixDoesNothingWhenAnotherModKeptTheTabClosed()
        {
            Tabs_Manager_OpenTab.Postfix(Tabs_Manager._tab._type.settings, __runOriginal: false);

            Assert.Empty(Log.Messages);
        }

        /// <summary>
        /// Outside the game, finding the Settings tab fails (the engine isn't there), as it might if another mod
        /// changed the tab: the error is logged, not passed on to the game's OpenTab.
        /// </summary>
        [Fact]
        public void AnErrorAddingTheSettingsButtonStaysInThePostfix()
        {
            Tabs_Manager_OpenTab.Postfix(Tabs_Manager._tab._type.settings, __runOriginal: true);

            Assert.Contains(Log.Messages, m => m.Contains("[ModMenus] Couldn't add the Mod Settings button"));
        }

        private static void ThrowOnDebugCheats(string name)
        {
            if (name == DebugCheats.VAR_ID)
            {
                throw new InvalidOperationException("another mod's error");
            }
        }

        [Fact]
        public void AnErrorCheckingDebugCheatsStaysInThePostfixAndIsLoggedOnce()
        {
            Harmony otherMod = new("tests.modmenus.othermod");
            otherMod.Patch(AccessTools.Method(typeof(variables), nameof(variables.Get)), postfix: new HarmonyMethod(typeof(CompatibilityTests), nameof(ThrowOnDebugCheats)));
            try
            {
                DebugCheats.CheckSafely();
                DebugCheats.CheckSafely();
                Assert.Single(Log.Messages, m => m.Contains("Couldn't check Enable Debug Cheats"));

                // A new game logs it again
                DebugCheats.Forget();
                DebugCheats.CheckSafely();
                Assert.Equal(2, Log.Messages.Count(m => m.Contains("Couldn't check Enable Debug Cheats")));
            }
            finally
            {
                otherMod.UnpatchSelf();
            }
        }

        /// <summary>
        /// Another mod's transpiler left code the key transpiler can't read (here the pause key's constant is a
        /// string). A transpiler that throws would stop Harmony changing Controls.Update at all, other mods'
        /// patches included, so the method is left as it is and its rows are hidden.
        /// </summary>
        [Fact]
        public void AnErrorInTheKeyTranspilerLeavesTheMethodAsItIs()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Controls), nameof(Controls.Update))).ToList();
            int pause = original.FindIndex(c => c.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(c.operand) == (int)KeyCode.Space);
            original[pause].operand = "not a number";
            List<string> before = original.Select(c => c.ToString()).ToList();

            List<CodeInstruction> code = SiteTranspiler.Apply(typeof(Controls), nameof(Controls.Update), original).ToList();

            Assert.Equal(before, code.Select(c => c.ToString()));
            Assert.False(SiteTranspiler.IsAvailable(Rows.ByVarID["ModMenus_Key_Pause"]));
            Assert.Contains(Log.Messages, m => m.Contains("Couldn't change the keys Controls.Update reads"));
        }

        /// <summary>
        /// The game's own Update methods call Sites, so an error reading a key row gives the game's own key
        /// instead of breaking the game's keys, and is logged once.
        /// </summary>
        [Fact]
        public void AnErrorReadingAKeyRowGivesTheGamesOwnKey()
        {
            TestGame.AddShippedMod("ModMenus");
            TestGame.Save("ModMenus_Key_Pause", "P");
            List<variables._variable> saved = variables.variable;
            variables.variable = null;
            try
            {
                Press(KeyCode.Space);
                Assert.True(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
                Press(KeyCode.P);
                Assert.False(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
                Assert.Single(Log.Messages, m => m.Contains("Couldn't read a key row's key"));
            }
            finally
            {
                variables.variable = saved;
            }

            // Once it can be read again, the row's key works
            Press(KeyCode.P);
            Assert.True(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
        }

        [Fact]
        public void TheMenuUsesPopup999WhenItIsFree()
        {
            Assert.Equal(ModMenusUtils.MOD_MENU_POPUP, ModMenusUtils.FreePopupType(null));
            Assert.Equal((PopupManager._type)999, ModMenusUtils.FreePopupType(new[]
            {
                new PopupManager._popup { type = PopupManager._type.settings_difficulty },
                new PopupManager._popup { type = (PopupManager._type)998 },
                null,
            }));
        }

        [Fact]
        public void TheMenuTakesTheNextFreeNumberWhenAnotherModHas999()
        {
            Assert.Equal((PopupManager._type)1000, ModMenusUtils.FreePopupType(new[] { new PopupManager._popup { type = (PopupManager._type)999 } }));
            Assert.Equal((PopupManager._type)1002, ModMenusUtils.FreePopupType(new[]
            {
                new PopupManager._popup { type = (PopupManager._type)1000 },
                new PopupManager._popup { type = (PopupManager._type)999 },
                new PopupManager._popup { type = (PopupManager._type)1001 },
            }));
        }
    }
}
