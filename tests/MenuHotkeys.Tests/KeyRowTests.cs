using SimpleJSON;
using System.IO;
using System.Linq;
using UnityEngine;
using Xunit;
using static MenuHotkeys.Tests.TestGame;
using TabType = Tabs_Manager._tab._type;

namespace MenuHotkeys.Tests
{
    /// <summary>
    /// Added in 1.1.0: each key is a key row in ModMenus, where the player can change it. Without ModMenus, or
    /// without the row, each key is its default.
    /// </summary>
    public class KeyRowTests
    {
        public KeyRowTests() => Reset();

        /// <summary>
        /// ModMenus, with these keys pressed this frame by varID.
        /// </summary>
        private static void WithModMenus(params string[] pressed)
        {
            ModMenusKeys.HasKeybind = varID => varID.StartsWith("MenuHotkeys_");
            ModMenusKeys.KeybindPressed = varID => pressed.Contains(varID);
        }

        [Fact]
        public void TheMenuFileHasARowForEveryKeyWithItsDefault()
        {
            JSONArray menu = JSON.Parse(File.ReadAllText(ModAsset(Path.Combine("JSON", "Mod Menu", "modmenu.json")))).AsArray;
            string constants = File.ReadAllText(ModAsset(Path.Combine("JSON", "Constants", "constants.json")));

            Assert.Equal(Controls_Update.Hotkeys.Select(h => (h.VarID, h.Key.ToString())),
                Enumerable.Range(0, menu.Count).Select(i => (menu[i]["varID"].Value, menu[i]["defaultValue"].Value)));
            for (int i = 0; i < menu.Count; i++)
            {
                Assert.Equal("keybind", menu[i]["type"].Value);
                Assert.Contains("id: \"" + menu[i]["labelID"].Value + "\"", constants);
            }
        }

        [Fact]
        public void TheTableMatchesTheKeysTheDescriptionPromises()
        {
            Assert.Equal(Hotkeys.Select(h => (h.Key, h.Tab)), Controls_Update.Hotkeys.Select(h => (h.Key, h.Tab)));
        }

        [Fact]
        public void WithModMenusTheRowsKeyOpensTheTab()
        {
            WithModMenus("MenuHotkeys_Singles");

            Press(KeyCode.L);

            Assert.Equal(TabType.singles, Assert.Single(Seams.OpenedTabs).Tab);
        }

        [Fact]
        public void WithModMenusTheDefaultKeyDoesNothingIfTheRowDoesntSaySo()
        {
            WithModMenus();

            Press(KeyCode.A);

            Assert.Empty(Seams.OpenedTabs);
        }

        [Fact]
        public void WithoutTheRowTheDefaultKeyWorks()
        {
            ModMenusKeys.HasKeybind = varID => false;
            ModMenusKeys.KeybindPressed = varID => true;

            Press(KeyCode.A);

            Assert.Equal(TabType.idols, Assert.Single(Seams.OpenedTabs).Tab);
        }
    }
}
