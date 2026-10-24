using ModMenus.GameKeys;
using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.3.0: the game's own keys are ModMenus' key rows. The game reads each key through its row, or its
    /// own key if ModMenus has no such row; a row is hidden if the game doesn't read its key any more; and keys
    /// clash only where both work.
    /// </summary>
    public class GameKeyTests
    {
        public GameKeyTests()
        {
            TestGame.Reset();
            SiteTranspiler.Reset();
            DEBUG.Debug_Enabled = false;
        }

        private static string ModFile(string mod, params string[] path) => Path.Combine(new[] { TestGame.RepoRoot(), "mods", mod, "assets" }.Concat(path).ToArray());

        private static JSONArray OwnMenu() => JSON.Parse(File.ReadAllText(ModFile("ModMenus", "JSON", JSON_DIR, JSON_FILE))).AsArray;

        /// <summary>
        /// ModMenus itself, with its shipped menu file.
        /// </summary>
        private static void InstallModMenus() => TestGame.AddShippedMod("ModMenus");

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
        public void TheMenuFileHasARowForEveryGameKeyWithItsDefault()
        {
            List<JSONNode> keys = Enumerable.Range(0, OwnMenu().Count).Select(i => OwnMenu()[i]).Where(i => i[JSON_FIELD_TYPE].Value == JSON_TYPE_KEYBIND).ToList();

            Assert.Equal(Rows.All.Select(r => r.VarID), keys.Select(k => k[JSON_FIELD_VARID].Value));
            foreach (JSONNode key in keys)
            {
                Row row = Rows.ByVarID[key[JSON_FIELD_VARID]];
                Assert.Equal(FormatKeybind(new Keybind(row.Default)), GetKeybindDefault(key));
                Assert.Equal(Rows.HAS_PREFIX + row.VarID.Substring(Rows.PREFIX.Length), key[JSON_FIELD_ENABLEDIF].Value);
            }
        }

        [Fact]
        public void EveryRowOfTheMenuFileHasItsText()
        {
            InstallModMenus();
            JSONArray menu = OwnMenu();
            for (int i = 0; i < menu.Count; i++)
            {
                Assert.True(Language.Data.ContainsKey(menu[i][JSON_FIELD_LABELID]), menu[i][JSON_FIELD_LABELID]);
            }
            Assert.True(Language.Data.ContainsKey(GAME_KEYS_LABEL));
        }

        [Fact]
        public void TheDebugCheatsSwitchIsFirstAndOff()
        {
            JSONNode first = OwnMenu()[0];

            Assert.Equal((JSON_TYPE_CHECKBOX, DebugCheats.VAR_ID, false), (first[JSON_FIELD_TYPE].Value, first[JSON_FIELD_VARID].Value, first[JSON_FIELD_DEF].AsBool));
        }

        /// <summary>
        /// The keys used twice by default, in the game itself and with Menu Hotkeys: Z (zoom out, promotion and the
        /// local audition cheat), F4 (scandal reset and word count), and Menu Hotkeys' F and S (the fans and fame cheats).
        /// </summary>
        [Fact]
        public void TheDefaultClashesAreTheGamesOwnAndMenuHotkeys()
        {
            JSONArray menuHotkeys = JSON.Parse(File.ReadAllText(ModFile("Menu Hotkeys", "JSON", JSON_DIR, JSON_FILE))).AsArray;
            List<KeyValuePair<string, string>> rows = Rows.All.Select(r => new KeyValuePair<string, string>(r.VarID, r.Default.ToString()))
                .Concat(Enumerable.Range(0, menuHotkeys.Count).Select(i => new KeyValuePair<string, string>(menuHotkeys[i][JSON_FIELD_VARID], menuHotkeys[i][JSON_FIELD_DEF])))
                .ToList();

            Dictionary<string, List<string>> clashes = FindClashes(rows, Contexts.Clash);

            Assert.Equal(new[]
            {
                "MenuHotkeys_Singles: ModMenus_Key_CheatFans",
                "MenuHotkeys_Staff: ModMenus_Key_CheatFame",
                "ModMenus_Key_CheatFame: MenuHotkeys_Staff",
                "ModMenus_Key_CheatFans: MenuHotkeys_Singles",
                "ModMenus_Key_CheatLocalAudition: ModMenus_Key_ZoomOut",
                "ModMenus_Key_CheatScandal: ModMenus_Key_WordCount",
                "ModMenus_Key_Promotion: ModMenus_Key_ZoomOut",
                "ModMenus_Key_WordCount: ModMenus_Key_CheatScandal",
                "ModMenus_Key_ZoomOut: ModMenus_Key_Promotion, ModMenus_Key_CheatLocalAudition",
            }, clashes.Select(c => c.Key + ": " + string.Join(", ", c.Value)).OrderBy(c => c, System.StringComparer.Ordinal));
        }

        [Fact]
        public void EveryRowWorksSomewhereKnown()
        {
            string[] known = { "normal", "debug", "dialogue", "profile", "credits" };
            foreach (Row row in Rows.All)
            {
                Assert.NotEmpty(row.Contexts);
                Assert.All(row.Contexts, c => Assert.Contains(c, known));
            }
            Assert.Equal(new[] { "normal", "debug" }, Contexts.Of("SomeMod_Key"));
        }

        [Fact]
        public void TheGameReadsTheRowsKey()
        {
            InstallModMenus();
            TestGame.Save("ModMenus_Key_Pause", "P");

            Press(KeyCode.Space);
            Assert.False(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));

            Press(KeyCode.P);
            Assert.True(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
        }

        [Fact]
        public void EachKindOfReadReadsTheRow()
        {
            InstallModMenus();
            TestGame.Save("ModMenus_Key_ScrollUp", "W");
            TestGame.Save("ModMenus_Key_ZoomOut", "X");
            TestGame.Save("ModMenus_Key_QuickLoad", "F10");
            TestGame.Save("ModMenus_Key_NextTabMouse", "Mouse4");

            Press(KeyCode.W, KeyCode.Mouse4);
            Seams.KeysUp.UnionWith(new[] { KeyCode.X, KeyCode.F10 });

            Assert.True(Seams.SitesGetKey(KeyCode.UpArrow, "ModMenus_Key_ScrollUp"));
            Assert.True(Seams.SitesGetKeyUp(KeyCode.Z, "ModMenus_Key_ZoomOut"));
            Assert.True(Seams.SitesGetKeyUpByName("f9", "ModMenus_Key_QuickLoad"));
            Assert.True(Seams.SitesGetMouseButtonDown(2, "ModMenus_Key_NextTabMouse"));
        }

        [Fact]
        public void WithoutTheRowTheGameReadsItsOwnKey()
        {
            Press(KeyCode.Space, KeyCode.Mouse2);
            Seams.KeysUp.Add(KeyCode.F9);

            Assert.True(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
            Assert.True(Seams.SitesGetKeyUpByName("f9", "ModMenus_Key_QuickLoad"));
            Assert.True(Seams.SitesGetMouseButtonDown(2, "ModMenus_Key_NextTabMouse"));
        }

        [Fact]
        public void NoGameKeyIsReadWhileTheMenuIsOpen()
        {
            InstallModMenus();
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            Press(KeyCode.Space);
            Seams.KeysUp.Add(KeyCode.F9);

            Assert.False(Seams.SitesGetKeyDown(KeyCode.Space, "ModMenus_Key_Pause"));
            Assert.False(Seams.SitesGetKeyUpByName("f9", "ModMenus_Key_QuickLoad"));
        }

        [Fact]
        public void ARowIsShownWhileTheGameReadsItsKey()
        {
            string Has(string suffix)
            {
                string result = null;
                variables_Get.Postfix(Rows.HAS_PREFIX + suffix, ref result);
                return result;
            }

            Assert.Equal("0", Has("Pause"));
            SiteTranspiler.Apply(typeof(Controls), nameof(Controls.Update), HarmonyLib.PatchProcessor.GetOriginalInstructions(HarmonyLib.AccessTools.Method(typeof(Controls), "Update"))).ToList();
            Assert.Equal("1", Has("Pause"));

            string other = "kept";
            variables_Get.Postfix("ModMenus_Other", ref other);
            Assert.Equal("kept", other);
            Assert.Null(Has("NotARow"));
        }

        [Fact]
        public void TheDebugCheatsSwitchTurnsDebugModeOnAndOff()
        {
            DebugCheats.Forget();
            TestGame.Save(DebugCheats.VAR_ID, "0");
            DebugCheats.Check();
            Assert.False(DEBUG.Debug_Enabled);

            variables.Set(DebugCheats.VAR_ID, "1");
            DebugCheats.Check();
            Assert.True(DEBUG.Debug_Enabled);

            variables.Set(DebugCheats.VAR_ID, "0");
            DebugCheats.Check();
            Assert.False(DEBUG.Debug_Enabled);
        }

        /// <summary>
        /// Pressing F7 five times still turns debug mode on; a save loaded with the switch off leaves it on.
        /// </summary>
        [Fact]
        public void ASaveLoadedWithTheSwitchOffLeavesDebugModeAsItIs()
        {
            TestGame.Save(DebugCheats.VAR_ID, "1");
            DebugCheats.Forget();
            DebugCheats.Check();
            Assert.True(DEBUG.Debug_Enabled);

            variables.variable.Clear();
            TestGame.Save(DebugCheats.VAR_ID, "0");
            DebugCheats.Forget();
            DebugCheats.Check();

            Assert.True(DEBUG.Debug_Enabled);
        }
    }
}
