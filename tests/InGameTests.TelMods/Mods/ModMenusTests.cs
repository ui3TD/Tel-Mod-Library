using HarmonyLib;
using Michsky.UI.ModernUIPack;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// ModMenus clones the game's own Settings button and settings popup by object name, which
    /// the unit tests can only fake. Here they are the real prefabs, and the rows come from every
    /// installed mod's modmenu.json.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class ModMenusTests
    {
        private const string HarmonyId = "com.tel.modmenus";
        private const PopupManager._type ModMenuPopup = (PopupManager._type)999;
        private static readonly string MenuFile = Path.Combine(Path.Combine("JSON", "Mod Menu"), "modmenu.json");

        /// <summary>
        /// Opening the Settings tab adds one Mod Settings button right after Settings, and
        /// opening it again doesn't add a second. Clicking it opens the mod menu with a row
        /// for every setting in the enabled mods' modmenu.json, and Apply saves the rows.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator SettingsButtonOpensMenuThatSaves(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.settings);
            yield return null;
            Transform settingsTab = Tabs_Manager.GetTabObject(Tabs_Manager._tab._type.settings).transform;
            AssertOneButtonAfterSettings(ctx, settingsTab, "after opening Settings");

            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.settings);
            yield return null;
            AssertOneButtonAfterSettings(ctx, settingsTab, "after opening Settings again");

            Transform button = Find(settingsTab, "ModMenuButton").FirstOrDefault();
            if (button == null)
            {
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                yield break;
            }
            button.GetComponent<Button>().onClick.Invoke();
            yield return TestTools.WaitFor(ctx, () => PopupManager.GetOpenPopupType() == ModMenuPopup, 5f, "the mod menu popup to open");

            GameObject popup = Game.Main.Data.GetComponent<PopupManager>().GetByType(ModMenuPopup)?.obj;
            if (popup == null)
            {
                ctx.Fail("Clicking Mod Settings didn't register popup 999");
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                yield break;
            }

            Transform menu = Find(popup.transform, "MenuContainer").FirstOrDefault();
            ctx.Assert(menu != null, "The mod menu has no MenuContainer");
            int rows = 0;
            if (menu != null)
            {
                var names = new HashSet<string>(menu.Cast<Transform>().Select(t => t.name), StringComparer.Ordinal);
                foreach (var expected in ExpectedRows())
                {
                    rows++;
                    ctx.Assert(names.Contains(expected.Key), expected.Value + ": no row " + expected.Key + " in the mod menu");
                }
            }
            ctx.Record("rows", rows);

            Type itemType = mod.GetType("ModMenus.ModMenusUtils+ModMenuItem", true);
            Component[] items = popup.GetComponentsInChildren(itemType, true);
            Component slider = items.FirstOrDefault(i => i.name.StartsWith("ModMenuSlider_", StringComparison.Ordinal));
            if (slider == null)
            {
                ctx.Note("No enabled mod has a slider setting; Apply not tested");
            }
            else
            {
                // Apply saves every row, so put every variable back afterwards.
                var before = items.Select(i => Traverse.Create(i).Field("varID").GetValue<string>()).Distinct()
                    .ToDictionary(id => id, variables.Get);
                using (TestTools.Restore(() =>
                {
                    foreach (var pair in before)
                    {
                        if (pair.Value == null)
                            variables.Delete(pair.Key);
                        else
                            variables.Set(pair.Key, pair.Value);
                    }
                }))
                {
                    string varId = Traverse.Create(slider).Field("varID").GetValue<string>();
                    float value = Traverse.Create(slider).Field("tempValue").GetValue<float>() == 7f ? 8f : 7f;
                    Traverse.Create(slider).Field("tempValue").SetValue(value);
                    Component manager = popup.GetComponentInChildren(mod.GetType("ModMenus.ModMenusUtils+ModMenuManager", true), true);
                    AccessTools.Method(manager.GetType(), "OnApply").Invoke(manager, null);
                    ctx.Record("appliedSetting", varId);
                    ctx.Assert(variables.Get(varId) == value.ToString(),
                        "Apply saved " + varId + " = " + (variables.Get(varId) ?? "nothing") + ", expected " + value);
                }
            }

            yield return Game.CloseAllPopups(ctx);
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
        }

        /// <summary>
        /// Loading a save that has no value for a setting (one made before the mod was installed, say) gives
        /// it its default, through the game's real load. Every other setting the menus show has a value too.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator LoadingGivesMissingSettingsTheirDefaults(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            MethodInfo tryGetDefault = AccessTools.Method(mod.GetType("ModMenus.ModMenusUtils", true), "TryGetDefault");
            var defaults = new Dictionary<string, string>();
            foreach (Mods._mod installed in Mods._Mods.Where(m => m != null && m.IsEnabled()))
            {
                string file = Path.Combine(installed.Path, MenuFile);
                if (!File.Exists(file))
                    continue;
                JSONArray items = JSON.Parse(File.ReadAllText(file)).AsArray;
                for (int i = 0; i < items.Count; i++)
                {
                    object[] args = { items[i], null, null };
                    if ((bool)tryGetDefault.Invoke(null, args))
                        defaults[(string)args[1]] = (string)args[2];
                }
            }
            ctx.Record("settings", defaults.Count);
            if (defaults.Count == 0)
            {
                ctx.Note("No enabled mod has a setting; nothing to check");
                yield break;
            }

            foreach (string varID in defaults.Keys)
                ctx.Assert(variables.Get(varID) != null, varID + " has no value after the game loaded");

            string removed = defaults.Keys.First();
            string before = variables.Get(removed);
            ctx.Record("removedSetting", removed);
            using (TestTools.Restore(() => variables.Set(removed, before)))
            {
                variables.Delete(removed);
                yield return Game.Quicksave(ctx);
                yield return Game.Quickload(ctx);
                ctx.Assert(variables.Get(removed) == defaults[removed],
                    "After loading a save without " + removed + " it is " + (variables.Get(removed) ?? "missing") + ", expected its default " + defaults[removed]);
            }
            if (before != null)
                yield return Game.Quicksave(ctx);
        }

        /// <summary>
        /// A menu for a temporary test mod, with dropdowns at the top, in the middle and as the last row,
        /// a long dropdown, and enough rows to scroll. Labels are raw text, which ModMenus shows as written.
        /// </summary>
        private const string TestMenu = @"[
            { ""type"": ""text"", ""labelID"": ""Dropdowns at the top, middle and bottom; the menu scrolls."" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_Top"", ""labelID"": ""Top dropdown"", ""itemIDList"": [""One"", ""Two"", ""Three""] },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider1"", ""labelID"": ""Slider"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 5 },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check1"", ""labelID"": ""Checkbox"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_Long"", ""labelID"": ""Long dropdown"", ""itemIDList"": [""1"", ""2"", ""3"", ""4"", ""5"", ""6"", ""7"", ""8"", ""9"", ""10"", ""11"", ""12""] },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check2"", ""labelID"": ""Checkbox 2"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider2"", ""labelID"": ""Slider 2"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check3"", ""labelID"": ""Checkbox 3"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider3"", ""labelID"": ""Slider 3"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_Bottom"", ""labelID"": ""Bottom dropdown"", ""itemIDList"": [""North"", ""East"", ""South"", ""West""] }
        ]";

        private const string TestModTitle = "ModMenus Test Menu";
        private const string TestVarPrefix = "ModMenusTest_";

        /// <summary>
        /// Installs a temporary mod with this menu (<see cref="TestMenu"/> if none) and makes the next click on Mod
        /// Settings build the menu afresh. Disposing removes the mod, its settings, and the menu built with it.
        /// </summary>
        private static IDisposable AddTestMod(string menuJson = TestMenu)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ModMenusInGameTest");
            string menuDir = Path.Combine(Path.Combine(dir, "JSON"), "Mod Menu");
            Directory.CreateDirectory(menuDir);
            File.WriteAllText(Path.Combine(menuDir, "modmenu.json"), menuJson);
            var mod = new Mods._mod { ModName = "com.test.modmenustestmenu", Title = TestModTitle, Path = dir };
            ForgetMenuPopup();
            Mods._Mods.Add(mod);
            return TestTools.Restore(() =>
            {
                Mods._Mods.Remove(mod);
                variables.variable.RemoveAll(v => v.name.StartsWith(TestVarPrefix, StringComparison.Ordinal));
                ForgetMenuPopup();
                Directory.Delete(dir, true);
            });
        }

        /// <summary>
        /// ModMenus builds its popup on the first click and keeps it; removing it makes the next click build it again.
        /// </summary>
        private static void ForgetMenuPopup()
        {
            PopupManager manager = Game.Main.Data.GetComponent<PopupManager>();
            PopupManager._popup popup = manager.GetByType(ModMenuPopup);
            if (popup == null)
                return;
            manager.popups = manager.popups.Where(p => p != popup).ToArray();
            UnityEngine.Object.Destroy(popup.obj);
        }

        /// <summary>
        /// Opens the Settings tab and clicks Mod Settings; returns the popup, or null after failing the test.
        /// </summary>
        private static IEnumerator OpenModSettings(TestContext ctx, List<GameObject> opened)
        {
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.settings);
            yield return null;
            Transform settingsTab = Tabs_Manager.GetTabObject(Tabs_Manager._tab._type.settings).transform;
            Transform button = Find(settingsTab, "ModMenuButton").FirstOrDefault();
            if (button == null)
            {
                ctx.Fail("No Mod Settings button in the Settings tab");
                yield break;
            }
            button.GetComponent<Button>().onClick.Invoke();
            yield return TestTools.WaitFor(ctx, () => PopupManager.GetOpenPopupType() == ModMenuPopup, 5f, "the mod menu popup to open");
            GameObject popup = Game.Main.Data.GetComponent<PopupManager>().GetByType(ModMenuPopup)?.obj;
            if (popup == null)
                ctx.Fail("Clicking Mod Settings didn't register popup 999");
            else
                opened.Add(popup);
        }

        /// <summary>
        /// The rows sit top to bottom in file order without overlapping. Opening a dropdown moves it to the
        /// popup's top layer, above every row and the buttons; closing it puts it back in its row.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator DropdownsOpenAboveEveryRow(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            using (AddTestMod())
            {
                var opened = new List<GameObject>();
                yield return OpenModSettings(ctx, opened);
                if (opened.Count == 0)
                {
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }
                GameObject popup = opened[0];
                Canvas.ForceUpdateCanvases();

                Transform menu = Find(popup.transform, "MenuContainer").First();
                List<RectTransform> rows = menu.Cast<Transform>().Select(t => (RectTransform)t).ToList();
                int title = rows.FindIndex(r => r.name == "ModMenuText_" + TestModTitle);
                ctx.Assert(title >= 0, "No title row for the test mod");
                if (title >= 0)
                {
                    string[] expected = { "ModMenuText_" + TestModTitle, "ModMenuText_Dropdowns at the top, middle and bottom; the menu scrolls.",
                        "ModMenuDropdown_ModMenusTest_Top", "ModMenuSlider_ModMenusTest_Slider1", "ModMenuCheckbox_ModMenusTest_Check1",
                        "ModMenuDropdown_ModMenusTest_Long", "ModMenuCheckbox_ModMenusTest_Check2", "ModMenuSlider_ModMenusTest_Slider2",
                        "ModMenuCheckbox_ModMenusTest_Check3", "ModMenuSlider_ModMenusTest_Slider3", "ModMenuDropdown_ModMenusTest_Bottom" };
                    string[] actual = rows.Skip(title).Take(expected.Length).Select(r => r.name).ToArray();
                    ctx.Assert(actual.SequenceEqual(expected), "The test mod's rows are " + string.Join(", ", actual));
                }

                var corners = new Vector3[4];
                float previousBottom = float.MaxValue;
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].GetWorldCorners(corners);
                    ctx.Assert(corners[1].y <= previousBottom + 0.01f, rows[i].name + " overlaps the row above it");
                    previousBottom = corners[0].y;
                }
                ctx.Record("rows", rows.Count);

                Transform layer = popup.transform.Find("DropdownLayer");
                ctx.Assert(layer != null && layer.GetSiblingIndex() == popup.transform.childCount - 1, "The popup has no DropdownLayer as its last child");
                if (layer == null)
                {
                    yield return Game.CloseAllPopups(ctx);
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }

                string shots = Path.Combine(Path.GetTempPath(), "ModMenusScreens");
                if (Directory.Exists(shots))
                    Directory.Delete(shots, true);
                Directory.CreateDirectory(shots);
                ctx.Record("screenshots", shots);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Screenshot(Path.Combine(shots, "1-menu-open.png"));

                // As a player: open each dropdown, check every item they can see is on screen and is what a
                // click there would hit (not a row or button drawn over it), then pick one.
                yield return OpenPickAndCheck(ctx, rows, layer, "ModMenusTest_Top", 1, "Two", Path.Combine(shots, "2-top-open.png"));
                yield return OpenPickAndCheck(ctx, rows, layer, "ModMenusTest_Long", 11, "12", Path.Combine(shots, "3-long-open.png"));

                // Scroll to the bottom; the last row is then inside the visible area
                ScrollRect scroll = popup.GetComponentInChildren<ScrollRect>(true);
                scroll.verticalNormalizedPosition = 0f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform bottomRow = rows.First(r => r.name == "ModMenuDropdown_ModMenusTest_Bottom");
                ctx.Assert(Inside(ScreenRect(bottomRow), ScreenRect(scroll.viewport)), "After scrolling to the bottom, the last row isn't fully visible");
                // What is drawn can be bigger than a row's layout box (a dropdown's box is), so check the drawing
                Rect drawn = DrawnRect(bottomRow);
                ctx.Record("lastRowDrawn", drawn + " in view " + ScreenRect(scroll.viewport));
                ctx.Assert(Inside(drawn, ScreenRect(scroll.viewport)), "Scrolled to the bottom, the last row is cut off: it's drawn at " + drawn + ", the view is " + ScreenRect(scroll.viewport));
                yield return Screenshot(Path.Combine(shots, "3b-scrolled-to-bottom.png"));
                yield return OpenPickAndCheck(ctx, rows, layer, "ModMenusTest_Bottom", 3, "West", Path.Combine(shots, "4-bottom-open.png"));

                // Scroll the menu while a list is open: the list closes and its box stays in its row
                RectTransform longRow = rows.First(r => r.name == "ModMenuDropdown_ModMenusTest_Long");
                CustomDropdown longDropdown = longRow.GetComponentInChildren<CustomDropdown>(true);
                longDropdown.Animate();
                yield return new WaitForSecondsRealtime(0.5f);
                scroll.verticalNormalizedPosition = 0.5f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                ctx.Assert(!longDropdown.isOn && longDropdown.transform.parent == longRow, "Scrolling the menu didn't close an open list");
                ctx.Assert(Inside(ScreenRect((RectTransform)longDropdown.transform), ScreenRect(longRow)), "After scrolling with its list open, the long dropdown's box isn't inside its row");
                scroll.verticalNormalizedPosition = 0f;
                yield return null;

                // Close the menu with a list open, then reopen it: the list is back in its row, closed, and
                // the menu is scrolled to the top again
                CustomDropdown bottom = bottomRow.GetComponentInChildren<CustomDropdown>(true);
                bottom.Animate();
                yield return new WaitForSecondsRealtime(0.5f);
                ctx.Assert(bottom.isOn, "The bottom dropdown didn't open before closing the menu");
                yield return Game.CloseAllPopups(ctx);
                var reopened = new List<GameObject>();
                yield return OpenModSettings(ctx, reopened);
                yield return new WaitForSecondsRealtime(0.5f);
                ctx.Assert(!bottom.isOn && bottom.transform.parent == bottomRow, "A list left open was still open or out of its row after reopening the menu");
                Canvas.ForceUpdateCanvases();
                ctx.Assert(Inside(ScreenRect((RectTransform)bottom.transform), ScreenRect(bottomRow)), "After reopening, the bottom dropdown's box isn't inside its row");
                ctx.Assert(layer.childCount == 0, "Something was left on the top layer after reopening the menu");
                ctx.Assert(scroll.verticalNormalizedPosition > 0.99f, "The menu didn't reopen scrolled to the top");
                yield return Screenshot(Path.Combine(shots, "5-reopened.png"));

                // Cancel threw the picks away; pick again and Apply, which saves the choice
                yield return OpenPickAndCheck(ctx, rows, layer, "ModMenusTest_Bottom", 2, "South", null);
                Type managerType = AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuManager");
                Component manager = popup.GetComponentInChildren(managerType, true);
                AccessTools.Method(managerType, "OnApply").Invoke(manager, null);
                ctx.Assert(variables.Get("ModMenusTest_Bottom") == "2", "Apply saved the bottom dropdown as " + (variables.Get("ModMenusTest_Bottom") ?? "nothing") + ", expected 2");

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// A menu with text fields: one with a default, one limited to 5 characters, and a checkbox under them.
        /// </summary>
        private const string InputMenu = @"[
            { ""type"": ""text"", ""labelID"": ""Text fields"" },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Name"", ""labelID"": ""Agency motto"", ""defaultValue"": ""Hello"" },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Short"", ""labelID"": ""Five characters at most"", ""maxLength"": 5 },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check1"", ""labelID"": ""Checkbox"" }
        ]";

        /// <summary>
        /// A text field is a copy of the game's rival name field, cut loose from the rival popup: typing in it
        /// changes only the setting, Tab doesn't jump to the rival's fields, and the game's hotkeys are off
        /// while the menu is open. It shows its default, takes typing (Japanese too) up to its limit, is what a
        /// click on it hits, and fits its row. Apply saves the text as typed; Cancel throws it away.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator TextFieldsTakeTypingAndSave(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            using (AddTestMod(InputMenu))
            {
                var opened = new List<GameObject>();
                yield return OpenModSettings(ctx, opened);
                if (opened.Count == 0)
                {
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }
                GameObject popup = opened[0];
                yield return new WaitForSecondsRealtime(0.5f);
                Canvas.ForceUpdateCanvases();

                Transform menu = Find(popup.transform, "MenuContainer").First();
                RectTransform nameRow = (RectTransform)menu.Find("ModMenuInput_ModMenusTest_Name");
                RectTransform shortRow = (RectTransform)menu.Find("ModMenuInput_ModMenusTest_Short");
                if (nameRow == null || shortRow == null)
                {
                    ctx.Fail("The text field rows weren't built: " + string.Join(", ", menu.Cast<Transform>().Select(t => t.name)));
                    yield return Game.CloseAllPopups(ctx);
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }
                TMP_InputField name = nameRow.GetComponentInChildren<TMP_InputField>(true);
                TMP_InputField limited = shortRow.GetComponentInChildren<TMP_InputField>(true);

                // Cut loose from the rival popup
                GameObject rivalPopup = Game.Main.Data.GetComponent<ActiveDialogueController>().VN_Popups.GetComponent<vn_popups>().Popup_RivalName;
                foreach (TMP_InputField field in new[] { name, limited })
                {
                    ctx.Assert(field.GetComponent<InputField_Tab>() == null, field.transform.parent.name + " still jumps to the rival's fields on Tab");
                    var events = new UnityEventBase[] { field.onValueChanged, field.onEndEdit, field.onSubmit, field.onSelect, field.onDeselect };
                    foreach (UnityEventBase unityEvent in events)
                    {
                        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
                            ctx.Fail(field.transform.parent.name + " still calls " + unityEvent.GetPersistentTarget(i) + "." + unityEvent.GetPersistentMethodName(i));
                    }
                }
                ctx.Assert(!rivalPopup.activeSelf, "Building the menu opened the rival name popup");

                // The label, the default and the limit
                Transform label = nameRow.Find("Title");
                ctx.Assert(label != null && label.GetComponent<TextMeshProUGUI>().text == "Agency motto", "The text field's label isn't its labelID");
                ctx.Assert(name.text == "Hello", "The text field shows '" + name.text + "', expected its default 'Hello'");
                ctx.Assert(limited.text == "" && limited.characterLimit == 5, "The limited field shows '" + limited.text + "' with limit " + limited.characterLimit + ", expected '' and 5");

                // What's typed doesn't look like a label: it's another colour, in an opaque box
                if (label != null)
                {
                    Color labelColour = label.GetComponent<TextMeshProUGUI>().color;
                    ctx.Assert(name.textComponent.color != labelColour, "The typed text is the label's colour " + labelColour);
                }
                ctx.Assert(name.GetComponent<Image>().color.a > 0.99f, "The text field has no box behind it: " + name.GetComponent<Image>().color);

                // Each field is inside its row, as drawn, and is what a click on it hits
                ScrollIntoView(ctx, shortRow);
                yield return null;
                foreach (RectTransform row in new[] { nameRow, shortRow })
                    AssertFieldFitsAndTakesClicks(ctx, row);
                var corners = new Vector3[4];
                float previousBottom = float.MaxValue;
                foreach (RectTransform row in menu.Cast<Transform>().Cast<RectTransform>())
                {
                    row.GetWorldCorners(corners);
                    ctx.Assert(corners[1].y <= previousBottom + 0.01f, row.name + " overlaps the row above it");
                    previousBottom = corners[0].y;
                }

                string shots = Path.Combine(Path.GetTempPath(), "ModMenusInputScreens");
                if (Directory.Exists(shots))
                    Directory.Delete(shots, true);
                Directory.CreateDirectory(shots);
                ctx.Record("screenshots", shots);
                yield return Screenshot(Path.Combine(shots, "1-defaults.png"));

                // Type as a player does: click the field, then each key reaches it as a keyboard event
                yield return TypeInto(name, null, " スター☆");
                ctx.Assert(name.isFocused, "Clicking the text field didn't give it the keyboard");
                ctx.Assert(mainScript.IsBlockingHotkeys(), "The game's hotkeys are on while typing in the mod menu");
                yield return TypeInto(limited, null, "abcdefg");
                ctx.Assert(name.text == "Hello スター☆", "After typing, the field shows '" + name.text + "'");
                ctx.Assert(limited.text == "abcde", "Typing 7 characters into the 5 character field gave '" + limited.text + "'");
                Component nameItem = nameRow.GetComponent(AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuItem"));
                ctx.Assert(Traverse.Create(nameItem).Field("tempText").GetValue<string>() == "Hello スター☆", "Typing didn't reach the setting");
                ctx.Assert(variables.Get("ModMenusTest_Name") != name.text, "Typing saved the setting before Apply");
                yield return new WaitForSecondsRealtime(0.3f);
                Canvas.ForceUpdateCanvases();
                AssertFieldFitsAndTakesClicks(ctx, nameRow);
                yield return Screenshot(Path.Combine(shots, "2-typed.png"));

                // Cancel throws the typing away
                yield return Game.CloseAllPopups(ctx);
                var reopened = new List<GameObject>();
                yield return OpenModSettings(ctx, reopened);
                yield return new WaitForSecondsRealtime(0.5f);
                ctx.Assert(name.text == "Hello" && limited.text == "", "After Cancel the fields show '" + name.text + "' and '" + limited.text + "'");

                // Apply saves the text as typed, and the menu reopens showing it
                yield return TypeInto(name, "Bye", "!");
                Type managerType = AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuManager");
                AccessTools.Method(managerType, "OnApply").Invoke(popup.GetComponentInChildren(managerType, true), null);
                ctx.Assert(variables.Get("ModMenusTest_Name") == "Bye!", "Apply saved '" + variables.Get("ModMenusTest_Name") + "', expected 'Bye!'");
                ctx.Assert(variables.Get("ModMenusTest_Short") == "", "Apply saved the empty field as '" + (variables.Get("ModMenusTest_Short") ?? "nothing") + "'");
                yield return Game.CloseAllPopups(ctx);
                reopened.Clear();
                yield return OpenModSettings(ctx, reopened);
                yield return new WaitForSecondsRealtime(0.5f);
                ctx.Assert(name.text == "Bye!", "After Apply the menu reopened showing '" + name.text + "'");

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// One of each row: a short and a long text, a blank spacer, and each control.
        /// </summary>
        private const string LayoutMenu = @"[
            { ""type"": ""text"", ""labelID"": ""Short text."" },
            { ""type"": ""text"", ""labelID"": ""A long text that wraps over several lines, to check that its row grows with it and doesn't run into the next row. It goes on for a while longer so that it takes four lines in the menu."" },
            { ""type"": ""text"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider1"", ""labelID"": ""Slider"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 1 },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check1"", ""labelID"": ""Checkbox"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_Top"", ""labelID"": ""Dropdown"", ""itemIDList"": [""One"", ""Two""] },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Name"", ""labelID"": ""Text field"", ""defaultValue"": ""Typed text"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check2"", ""labelID"": ""Checkbox 2"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider2"", ""labelID"": ""Slider at the end"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 20 }
        ]";

        /// <summary>
        /// Rows are spaced evenly: each draws only inside itself and fills it, top to bottom, so the gaps between
        /// what's drawn are the same between any two rows (a title keeps room around it, and a blank text row
        /// draws nothing). A slider's track starts at the left edge, like the labels and boxes. The menu keeps the
        /// same margin on every side of the panel.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator RowsAreEvenlySpacedAndAligned(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            using (AddTestMod(LayoutMenu))
            {
                var opened = new List<GameObject>();
                yield return OpenModSettings(ctx, opened);
                if (opened.Count == 0)
                {
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }
                GameObject popup = opened[0];
                yield return new WaitForSecondsRealtime(0.5f);
                Canvas.ForceUpdateCanvases();
                float unit = popup.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                float halfGap = Find(popup.transform, "MenuContainer").First().GetComponent<VerticalLayoutGroup>().spacing / 2;

                // The same margin on every side of the panel
                Rect panel = ScreenRect((RectTransform)popup.transform.Find("Panel"));
                ScrollRect scroll = popup.GetComponentInChildren<ScrollRect>(true);
                Rect view = ScreenRect(scroll.viewport);
                Rect scrollbar = ScreenRect((RectTransform)scroll.verticalScrollbar.transform);
                float[] margins = { view.xMin - panel.xMin, panel.xMax - scrollbar.xMax, panel.yMax - view.yMax, view.yMin - panel.yMin };
                ctx.Record("margins", string.Join(", ", margins.Select(m => m.ToString("0.0"))));
                ctx.Assert(margins.Max() - margins.Min() <= 1f, "The menu's margins inside the panel differ: left, right, top, bottom = " + string.Join(", ", margins.Select(m => m.ToString("0.0"))));
                ctx.Assert(scrollbar.xMin >= view.xMax, "The scroll bar overlaps the rows");

                Transform menu = Find(popup.transform, "MenuContainer").First();
                List<RectTransform> rows = menu.Cast<Transform>().Cast<RectTransform>()
                    .SkipWhile(r => r.name != "ModMenuText_" + TestModTitle).ToList();
                foreach (RectTransform row in rows)
                {
                    ScrollToMiddle(row);
                    yield return new WaitForEndOfFrame();
                    Texture2D shot = ReadScreen();
                    try
                    {
                        Rect rect = ScreenRect(row);
                        // The panel's colour beside this row, in the gap before the scroll bar
                        Color background = shot.GetPixel((int)((view.xMax + scrollbar.xMin) / 2), (int)rect.center.y);
                        // Half the gap above and below the row, where a row that draws outside itself would show, as
                        // far as the menu shows: past its edge the panel is drawn, and the rows are cut off
                        Rect area = Rect.MinMaxRect(rect.xMin, Mathf.Max(rect.yMin - halfGap * unit, view.yMin), rect.xMax, Mathf.Min(rect.yMax + halfGap * unit, view.yMax));
                        bool found = FindDrawnBand(shot, area, background, out float drawnBottom, out float drawnTop);

                        if (row.name == "ModMenuText_")
                        {
                            ctx.Assert(!found, "The blank text row draws something");
                            continue;
                        }
                        ctx.Assert(found, row.name + " draws nothing");
                        if (!found)
                            continue;
                        float above = (rect.yMax - drawnTop) / unit;
                        float below = (drawnBottom - rect.yMin) / unit;
                        ctx.Record(row.name.Length > 40 ? row.name.Substring(0, 40) : row.name, "empty above " + above.ToString("0.0") + ", below " + below.ToString("0.0") + " (row " + (rect.height / unit).ToString("0") + ")");
                        ctx.Assert(above >= -1 && below >= -1, row.name + " draws outside itself: " + (-above).ToString("0.0") + " above, " + (-below).ToString("0.0") + " below");
                        // A title sits close to its own settings, set apart from the mod above
                        if (row.name == "ModMenuText_" + TestModTitle)
                            ctx.Assert(below <= 6 && (row.GetSiblingIndex() == 0 || above >= below + 20), "The title isn't closer to its settings than to the mod above: " + above.ToString("0.0") + " empty above, " + below.ToString("0.0") + " below");
                        else
                            ctx.Assert(above <= 6 && below <= 6, row.name + " doesn't fill its row: " + above.ToString("0.0") + " empty above, " + below.ToString("0.0") + " below");
                    }
                    finally
                    {
                        UnityEngine.Object.Destroy(shot);
                    }
                }

                // A slider's track starts at the left edge, like the labels and boxes
                foreach (RectTransform row in rows.Where(r => r.name.StartsWith("ModMenuSlider_", StringComparison.Ordinal)))
                {
                    var corners = new Vector3[4];
                    ((RectTransform)row.Find("Slider/Background")).GetWorldCorners(corners);
                    Canvas canvas = row.GetComponentInParent<Canvas>().rootCanvas;
                    Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    float trackLeft = corners.Min(c => RectTransformUtility.WorldToScreenPoint(camera, c).x);
                    ctx.Assert(Mathf.Abs(trackLeft - ScreenRect(row).xMin) <= unit, row.name + "'s track starts " + ((trackLeft - ScreenRect(row).xMin) / unit).ToString("0.0") + " right of the row's left edge");
                }
                yield return Screenshot(Path.Combine(Path.Combine(Path.GetTempPath(), "ModMenusInputScreens"), "3-layout.png"));

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// What's on screen this frame. Call it at the end of a frame (after WaitForEndOfFrame).
        /// </summary>
        private static Texture2D ReadScreen()
        {
            var shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            shot.Apply();
            return shot;
        }

        /// <summary>
        /// The lowest and highest screen rows in the area where anything but the background is drawn.
        /// </summary>
        private static bool FindDrawnBand(Texture2D shot, Rect area, Color background, out float bottom, out float top)
        {
            bottom = top = 0;
            bool found = false;
            int xMin = Mathf.Max(0, (int)area.xMin), xMax = Mathf.Min(shot.width, (int)area.xMax);
            for (int y = Mathf.Max(0, (int)area.yMin); y < Mathf.Min(shot.height, (int)area.yMax); y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    Color c = shot.GetPixel(x, y);
                    if (Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > 0.3f)
                    {
                        if (!found)
                            bottom = y;
                        found = true;
                        top = y + 1;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// A text field row draws inside its row, and its field is big enough to click and is what a click on it hits.
        /// </summary>
        private static void AssertFieldFitsAndTakesClicks(TestContext ctx, RectTransform row)
        {
            RectTransform field = (RectTransform)row.Find("Field");
            ctx.Record(row.name + "Drawn", DrawnRect(row) + " row " + ScreenRect(row) + " field " + ScreenRect(field));
            ctx.Assert(Inside(DrawnRect(row), ScreenRect(row)), row.name + " draws outside its row: " + DrawnRect(row) + ", the row is " + ScreenRect(row));
            ctx.Assert(ScreenRect(field).height >= 15 && ScreenRect(field).width >= 100, row.name + "'s field is too small to click: " + ScreenRect(field));
            GameObject hit = TopHit(ScreenRect(field).center);
            ctx.Assert(hit != null && hit.transform.IsChildOf(field), row.name + ": a click on the field would hit " + (hit != null ? hit.name : "nothing"));
        }

        /// <summary>
        /// Clicks a text field, replaces its text if asked, then types each character as a key press at the end.
        /// </summary>
        private static IEnumerator TypeInto(TMP_InputField field, string replaceWith, string typed)
        {
            Click(field.gameObject);
            yield return null;
            yield return null;
            if (replaceWith != null)
                field.text = replaceWith;
            field.MoveTextEnd(false);
            foreach (char c in typed)
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = c });
            field.ForceLabelUpdate();
            yield return null;
        }

        private static void Click(GameObject target)
        {
            EventSystem system = EventSystem.current;
            var data = new PointerEventData(system) { button = PointerEventData.InputButton.Left, position = ScreenRect((RectTransform)target.transform).center };
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>
        /// Opens a dropdown as a click on it does, checks that it moved to the top layer and that each of its
        /// visible items is on screen and is the first thing a click there hits, takes a screenshot, then picks
        /// an item as clicking it does and checks the dropdown closed back into its row showing that item.
        /// </summary>
        private static IEnumerator OpenPickAndCheck(TestContext ctx, List<RectTransform> rows, Transform layer, string varID, int pick, string pickName, string shot)
        {
            Transform row = rows.FirstOrDefault(r => r.name == "ModMenuDropdown_" + varID);
            CustomDropdown dropdown = row != null ? row.GetComponentInChildren<CustomDropdown>(true) : null;
            if (dropdown == null)
            {
                ctx.Fail("No dropdown " + varID);
                yield break;
            }
            ScrollIntoView(ctx, (RectTransform)row);
            yield return null;

            int index = dropdown.transform.GetSiblingIndex();
            dropdown.Animate();
            yield return new WaitForSecondsRealtime(0.5f);
            Canvas.ForceUpdateCanvases();
            ctx.Assert(dropdown.isOn && dropdown.transform.parent == layer, varID + " didn't move to the top layer when opened");

            Rect screen = new Rect(0, 0, Screen.width, Screen.height);
            RectTransform list = dropdown.itemParent.parent as RectTransform ?? (RectTransform)dropdown.itemParent;
            Rect listView = ScreenRect(list);
            int visible = 0;
            for (int i = 0; i < dropdown.dropdownItems.Count; i++)
            {
                RectTransform item = (RectTransform)dropdown.GetButton(i).transform;
                Rect itemRect = ScreenRect(item);
                // Items scrolled out of a long list's own view aren't visible; the rest must be clickable
                if (!listView.Contains(itemRect.center))
                    continue;
                visible++;
                ctx.Assert(Inside(itemRect, screen), varID + " item " + i + " is off screen at " + itemRect);
                GameObject hit = TopHit(itemRect.center);
                ctx.Assert(hit != null && hit.transform.IsChildOf(item),
                    varID + " item " + i + ": a click on it would hit " + (hit != null ? hit.name : "nothing"));
            }
            ctx.Assert(visible > 0, varID + " shows no items when open");
            ctx.Record(varID + "VisibleItems", visible + " of " + dropdown.dropdownItems.Count);
            if (shot != null)
                yield return Screenshot(shot);

            dropdown.GetButton(pick).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            ctx.Assert(!dropdown.isOn && dropdown.transform.parent == row && dropdown.transform.GetSiblingIndex() == index,
                varID + " didn't close back into its row after picking an item");
            Canvas.ForceUpdateCanvases();
            ctx.Assert(Inside(ScreenRect((RectTransform)dropdown.transform), ScreenRect((RectTransform)row)), varID + "'s box isn't inside its row after picking");
            ctx.Assert(dropdown.selectedText.text == pickName, varID + " shows " + dropdown.selectedText.text + ", expected " + pickName);
            Component item2 = row.GetComponent(AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuItem"));
            float value = Traverse.Create(item2).Field("tempValue").GetValue<float>();
            ctx.Assert(value == pick, varID + " would save " + value + ", expected " + pick);
        }

        /// <summary>
        /// Scrolls the menu down from the top until the row is in view, as a player does before clicking it.
        /// </summary>
        private static void ScrollIntoView(TestContext ctx, RectTransform row)
        {
            ScrollRect scroll = row.GetComponentInParent<ScrollRect>();
            for (float position = 1f; position >= 0f && !Inside(ScreenRect(row), ScreenRect(scroll.viewport)); position -= 0.01f)
            {
                scroll.verticalNormalizedPosition = position;
                Canvas.ForceUpdateCanvases();
            }
            ctx.Assert(Inside(ScreenRect(row), ScreenRect(scroll.viewport)), row.name + " can't be scrolled into view");
        }

        /// <summary>
        /// Scrolls the menu so the row is as near its middle as the menu scrolls.
        /// </summary>
        private static void ScrollToMiddle(RectTransform row)
        {
            ScrollRect scroll = row.GetComponentInParent<ScrollRect>();
            float best = 1f, bestDistance = float.MaxValue;
            for (int step = 200; step >= 0; step--)
            {
                scroll.verticalNormalizedPosition = step / 200f;
                Canvas.ForceUpdateCanvases();
                float distance = Mathf.Abs(ScreenRect(row).center.y - ScreenRect(scroll.viewport).center.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = step / 200f;
                }
            }
            scroll.verticalNormalizedPosition = best;
            Canvas.ForceUpdateCanvases();
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// The screen area a row actually draws on: every visible image and text in it, not counting a
        /// dropdown's closed list.
        /// </summary>
        private static Rect DrawnRect(RectTransform row)
        {
            Rect drawn = ScreenRect(row);
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>())
            {
                float alpha = graphic.color.a;
                foreach (CanvasGroup group in graphic.GetComponentsInParent<CanvasGroup>())
                    alpha *= group.alpha;
                if (!graphic.enabled || alpha < 0.01f)
                    continue;
                Rect rect = ScreenRect(graphic.rectTransform);
                drawn = Rect.MinMaxRect(Mathf.Min(drawn.xMin, rect.xMin), Mathf.Min(drawn.yMin, rect.yMin),
                    Mathf.Max(drawn.xMax, rect.xMax), Mathf.Max(drawn.yMax, rect.yMax));
            }
            return drawn;
        }

        private static bool Inside(Rect inner, Rect outer) =>
            inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;

        /// <summary>The topmost UI object a click at this screen point would reach.</summary>
        private static GameObject TopHit(Vector2 point)
        {
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static IEnumerator Screenshot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            float start = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - start < 5f)
                yield return null;
        }

        private static void AssertOneButtonAfterSettings(TestContext ctx, Transform settingsTab, string when)
        {
            List<Transform> buttons = Find(settingsTab, "ModMenuButton").ToList();
            ctx.Assert(buttons.Count == 1, "Expected one Mod Settings button " + when + ", found " + buttons.Count);
            if (buttons.Count != 1)
                return;
            Transform settings = buttons[0].parent.Cast<Transform>().FirstOrDefault(t => t.name == "Settings");
            ctx.Assert(settings != null, "The Mod Settings button isn't next to the game's Settings button " + when);
            if (settings != null)
            {
                ctx.Assert(buttons[0].GetSiblingIndex() == settings.GetSiblingIndex() + 1,
                    "Mod Settings is at position " + buttons[0].GetSiblingIndex() + ", Settings at " + settings.GetSiblingIndex() + " " + when);
            }
        }

        /// <summary>The row object each enabled mod's modmenu.json should produce, by the rules ModMenus documents, with the mod's title.</summary>
        private static IEnumerable<KeyValuePair<string, string>> ExpectedRows()
        {
            foreach (Mods._mod mod in Mods._Mods.Where(m => m != null && m.IsEnabled()))
            {
                string file = Path.Combine(mod.Path, MenuFile);
                if (!File.Exists(file))
                    continue;
                yield return new KeyValuePair<string, string>("ModMenuText_" + mod.Title, mod.Title);
                foreach (JSONNode item in JSON.Parse(File.ReadAllText(file)).AsArray)
                {
                    string type = item["type"];
                    string varId = item["varID"];
                    if (string.IsNullOrEmpty(varId) || string.IsNullOrEmpty(item["labelID"]) || item["ignore"].AsBool)
                        continue;
                    if (type == "dropdown" && (item["itemIDList"].AsArray == null || item["itemIDList"].Count == 0))
                        continue;
                    string prefix = type switch
                    {
                        "slider" => "ModMenuSlider_",
                        "checkbox" => "ModMenuCheckbox_",
                        "dropdown" => "ModMenuDropdown_",
                        "input" => "ModMenuInput_",
                        _ => null,
                    };
                    if (prefix != null)
                        yield return new KeyValuePair<string, string>(prefix + varId, mod.Title);
                }
            }
        }

        private static IEnumerable<Transform> Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);
        }
    }
}
