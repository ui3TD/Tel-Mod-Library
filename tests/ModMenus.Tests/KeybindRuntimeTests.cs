using SimpleJSON;
using UnityEngine;
using Xunit;
using static ModMenus.ModMenusUtils;

namespace ModMenus.Tests
{
    /// <summary>
    /// Added in 4.3.0: mods read their key rows' keys with KeybindPressed, KeybindHeld and KeybindReleased: the
    /// saved key, or the row's default, with exactly its modifiers; never while keys are being picked.
    /// </summary>
    public class KeybindRuntimeTests
    {
        public KeybindRuntimeTests() => TestGame.Reset();

        private const string Keys = """
            [
                { "type": "keybind", "varID": "K_Pause", "labelID": "TEST__VOLUME", "defaultValue": "Space" },
                { "type": "keybind", "varID": "K_Combo", "labelID": "TEST__LOUD", "defaultValue": "Ctrl+F" },
                { "type": "keybind", "varID": "K_Plain", "labelID": "TEST__PICK", "defaultValue": "F" },
                { "type": "keybind", "varID": "K_Unset", "labelID": "TEST__MOTTO" }
            ]
            """;

        /// <summary>
        /// The next frame: keys held, and which of them went down this frame.
        /// </summary>
        private static void Frame(KeyCode[] held, params KeyCode[] down)
        {
            Seams.Frame++;
            Seams.KeysHeld.Clear();
            Seams.KeysHeld.UnionWith(held);
            Seams.KeysDown.Clear();
            Seams.KeysDown.UnionWith(down);
            Seams.KeysUp.Clear();
        }

        private static void Press(params KeyCode[] keys) => Frame(keys, keys);

        [Fact]
        public void KeyRowsInAMenuFileAreKnown()
        {
            TestGame.AddMod("Keys", Keys);

            Assert.True(Seams.HasKeybind("K_Pause"));
            Assert.True(Seams.HasKeybind("K_Unset"));
            Assert.False(Seams.HasKeybind("K_Missing"));
            Assert.False(Seams.HasKeybind(null));
        }

        [Fact]
        public void WithNothingSavedTheDefaultKeyIsRead()
        {
            TestGame.AddMod("Keys", Keys);

            Press(KeyCode.Space);

            Assert.True(Seams.KeybindPressed("K_Pause"));
            Assert.True(Seams.KeybindHeld("K_Pause"));
            Assert.False(Seams.KeybindPressed("K_Plain"));
        }

        [Fact]
        public void TheSavedKeyReplacesTheDefault()
        {
            TestGame.AddMod("Keys", Keys);
            TestGame.Save("K_Pause", "P");

            Press(KeyCode.Space);
            Assert.False(Seams.KeybindPressed("K_Pause"));

            Press(KeyCode.P);
            Assert.True(Seams.KeybindPressed("K_Pause"));
        }

        [Fact]
        public void ASavedValueThatIsntAKeyReadsAsTheDefault()
        {
            TestGame.AddMod("Keys", Keys);
            TestGame.Save("K_Pause", "Banana");

            Press(KeyCode.Space);

            Assert.True(Seams.KeybindPressed("K_Pause"));
        }

        [Fact]
        public void ARowWithNoKeyIsNeverPressed()
        {
            TestGame.AddMod("Keys", Keys);
            TestGame.Save("K_Pause", "None");

            Press(KeyCode.Space);

            Assert.False(Seams.KeybindPressed("K_Pause"));
            Assert.False(Seams.KeybindPressed("K_Unset"));
        }

        /// <summary>
        /// Ctrl+F is only Ctrl+F, and F with Ctrl held is Ctrl+F, not also F; F with Shift held is still F.
        /// </summary>
        [Fact]
        public void ModifiersMustMatchExactly()
        {
            TestGame.AddMod("Keys", Keys);

            Press(KeyCode.F);
            Assert.Equal((false, true), (Seams.KeybindPressed("K_Combo"), Seams.KeybindPressed("K_Plain")));

            Frame(new[] { KeyCode.LeftControl, KeyCode.F }, KeyCode.F);
            Assert.Equal((true, false), (Seams.KeybindPressed("K_Combo"), Seams.KeybindPressed("K_Plain")));

            Frame(new[] { KeyCode.LeftShift, KeyCode.F }, KeyCode.F);
            Assert.Equal((false, true), (Seams.KeybindPressed("K_Combo"), Seams.KeybindPressed("K_Plain")));

            Frame(new[] { KeyCode.RightControl, KeyCode.LeftShift, KeyCode.F }, KeyCode.F);
            Assert.Equal((false, true), (Seams.KeybindPressed("K_Combo"), Seams.KeybindPressed("K_Plain")));
        }

        /// <summary>
        /// While the menu is open, a key pressed to pick it (F9 for quickload, say) doesn't also do what it's for.
        /// </summary>
        [Fact]
        public void NoKeyIsReadWhileTheMenuIsOpen()
        {
            TestGame.AddMod("Keys", Keys);
            Menu menu = TestGame.BuildMenu();
            menu.Open();

            Press(KeyCode.Space);
            Assert.False(Seams.KeybindPressed("K_Pause"));

            menu.Cancel();
            Press(KeyCode.Space);
            Assert.True(Seams.KeybindPressed("K_Pause"));
        }

        [Fact]
        public void SplitPutsKeysAndTheirHeadingsOnTheHotkeysTab()
        {
            JSONArray items = JSON.Parse("""
                [
                    { "type": "text", "labelID": "INTRO" },
                    { "type": "checkbox", "varID": "A", "labelID": "L" },
                    { "type": "text", "labelID": "KEYS" },
                    { "type": "keybind", "varID": "B", "labelID": "L" },
                    { "type": "keybind", "varID": "C", "labelID": "L", "ignore": true },
                    { "type": "text", "labelID": "END" }
                ]
                """).AsArray;

            SplitKeybindRows(items, out JSONArray keys, out JSONArray others);

            Assert.Equal(new[] { "KEYS", "B" }, Names(keys));
            Assert.Equal(new[] { "INTRO", "A", "END" }, Names(others));
            Assert.True(HasSetting(others));
            Assert.False(HasSetting(JSON.Parse("""[ { "type": "text", "labelID": "END" } ]""").AsArray));
        }

        private static string[] Names(JSONArray items)
        {
            string[] names = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
                names[i] = items[i]["varID"].Value != "" ? items[i]["varID"].Value : items[i]["labelID"].Value;
            return names;
        }
    }
}
