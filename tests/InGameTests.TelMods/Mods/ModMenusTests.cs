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
                // Each mod's rows are on its page, shown or not
                var names = new HashSet<string>(menu.Cast<Transform>().SelectMany(page => page.Cast<Transform>()).Select(t => t.name), StringComparer.Ordinal);
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
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check4"", ""labelID"": ""Checkbox 4"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider4"", ""labelID"": ""Slider 4"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check5"", ""labelID"": ""Checkbox 5"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider5"", ""labelID"": ""Slider 5"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check6"", ""labelID"": ""Checkbox 6"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider6"", ""labelID"": ""Slider 6"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check7"", ""labelID"": ""Checkbox 7"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider7"", ""labelID"": ""Slider 7"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_Bottom"", ""labelID"": ""Bottom dropdown"", ""itemIDList"": [""North"", ""East"", ""South"", ""West""] }
        ]";

        private const string TestModTitle = "ModMenus Test Menu";
        private const string TestModName = "com.test.modmenustestmenu";
        private const string TestVarPrefix = "ModMenusTest_";

        /// <summary>
        /// Installs a temporary mod with this menu (<see cref="TestMenu"/> if none) and makes the next click on Mod
        /// Settings build the menu afresh. Disposing removes the mod, its settings, and the menu built with it.
        /// </summary>
        private static IDisposable AddTestMod(string menuJson = TestMenu)
        {
            return AddTestMods(new[] { TestModTitle }, new[] { TestModName }, new[] { menuJson });
        }

        /// <summary>
        /// Installs temporary mods, each with its title, mod name and menu, as <see cref="AddTestMod"/> does.
        /// </summary>
        private static IDisposable AddTestMods(string[] titles, string[] modNames, string[] menus)
        {
            string root = Path.Combine(Path.GetTempPath(), "ModMenusInGameTest");
            var added = new List<Mods._mod>();
            for (int i = 0; i < titles.Length; i++)
            {
                string dir = Path.Combine(root, i.ToString());
                string menuDir = Path.Combine(Path.Combine(dir, "JSON"), "Mod Menu");
                Directory.CreateDirectory(menuDir);
                File.WriteAllText(Path.Combine(menuDir, "modmenu.json"), menus[i]);
                added.Add(new Mods._mod { ModName = modNames[i], Title = titles[i], Path = dir });
            }
            ForgetMenuPopup();
            Mods._Mods.AddRange(added);
            return TestTools.Restore(() =>
            {
                foreach (Mods._mod mod in added)
                    Mods._Mods.Remove(mod);
                variables.variable.RemoveAll(v => v.name.StartsWith(TestVarPrefix, StringComparison.Ordinal));
                ForgetMenuPopup();
                Directory.Delete(root, true);
            });
        }

        /// <summary>
        /// A mod's page of settings, shown or not.
        /// </summary>
        private static Transform PageOf(GameObject popup, string modName) => Find(popup.transform, "ModMenuPage_" + modName).FirstOrDefault();

        /// <summary>
        /// The settings' scroll area (the tabs have their own).
        /// </summary>
        private static ScrollRect SettingsScroll(GameObject popup) => Find(popup.transform, "ScrollContainer").First().GetComponent<ScrollRect>();

        /// <summary>
        /// Shows a mod's settings as a player does: scrolls the tab column to its tab, checks a click there
        /// reaches the tab, and clicks it.
        /// </summary>
        private static IEnumerator ShowTab(TestContext ctx, GameObject popup, string modName)
        {
            RectTransform tab = (RectTransform)Find(popup.transform, "ModMenuTab_" + modName).FirstOrDefault();
            if (tab == null)
            {
                ctx.Fail("No tab for " + modName);
                yield break;
            }
            if (!tab.gameObject.activeInHierarchy || tab.GetComponentInParent<ScrollRect>() == null)
            {
                ctx.Fail(modName + "'s tab isn't shown in a scroll area: " + PathOf(tab));
                yield break;
            }
            ScrollIntoView(ctx, tab);
            yield return null;
            GameObject hit = TopHit(ScreenRect(tab).center);
            ctx.Assert(hit != null && hit.transform.IsChildOf(tab), modName + "'s tab: a click on it would hit " + (hit != null ? hit.name : "nothing"));
            Click(tab.gameObject);
            yield return null;
            Transform page = PageOf(popup, modName);
            ctx.Assert(page != null && page.gameObject.activeInHierarchy, "Clicking " + modName + "'s tab didn't show its page");
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
                yield return new WaitForSecondsRealtime(0.5f);
                yield return ShowTab(ctx, popup, TestModName);
                Canvas.ForceUpdateCanvases();

                Transform menu = PageOf(popup, TestModName);
                List<RectTransform> rows = menu.Cast<Transform>().Select(t => (RectTransform)t).ToList();
                int title = rows.FindIndex(r => r.name == "ModMenuText_" + TestModTitle);
                ctx.Assert(title >= 0, "No title row for the test mod");
                if (title >= 0)
                {
                    string[] expected = { "ModMenuText_" + TestModTitle, "ModMenuText_Dropdowns at the top, middle and bottom; the menu scrolls.",
                        "ModMenuDropdown_ModMenusTest_Top", "ModMenuSlider_ModMenusTest_Slider1", "ModMenuCheckbox_ModMenusTest_Check1",
                        "ModMenuDropdown_ModMenusTest_Long", "ModMenuCheckbox_ModMenusTest_Check2", "ModMenuSlider_ModMenusTest_Slider2",
                        "ModMenuCheckbox_ModMenusTest_Check3", "ModMenuSlider_ModMenusTest_Slider3", "ModMenuCheckbox_ModMenusTest_Check4", "ModMenuSlider_ModMenusTest_Slider4",
                        "ModMenuCheckbox_ModMenusTest_Check5", "ModMenuSlider_ModMenusTest_Slider5", "ModMenuCheckbox_ModMenusTest_Check6", "ModMenuSlider_ModMenusTest_Slider6",
                        "ModMenuCheckbox_ModMenusTest_Check7", "ModMenuSlider_ModMenusTest_Slider7", "ModMenuDropdown_ModMenusTest_Bottom" };
                    string[] actual = rows.Skip(title).Take(expected.Length).Select(r => r.name).ToArray();
                    ctx.Assert(actual.SequenceEqual(expected), "The test mod's rows are " + string.Join(", ", actual));
                }

                // A row of several lines takes its height a frame after the menu opens; measure once it has
                yield return new WaitForSecondsRealtime(0.5f);
                Canvas.ForceUpdateCanvases();
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
                ScrollRect scroll = SettingsScroll(popup);
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
                ctx.Assert(scroll.content.rect.height > scroll.viewport.rect.height + 100, "The test menu is too short to scroll, so scrolling with a list open isn't checked");
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
        /// A menu with text fields: one with a default, one limited to 5 characters, a password field, and a
        /// checkbox under them.
        /// </summary>
        private const string InputMenu = @"[
            { ""type"": ""text"", ""labelID"": ""Text fields"" },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Name"", ""labelID"": ""Agency motto"", ""defaultValue"": ""Hello"" },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Short"", ""labelID"": ""Five characters at most"", ""maxLength"": 5 },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_Secret"", ""labelID"": ""Secret"", ""password"": true },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check1"", ""labelID"": ""Checkbox"" }
        ]";

        /// <summary>
        /// A text field is a copy of the game's rival name field, cut loose from the rival popup: typing in it
        /// changes only the setting, Tab doesn't jump to the rival's fields, and the game's hotkeys are off
        /// while the menu is open. It shows its default, takes typing (Japanese too) up to its limit, is what a
        /// click on it hits, and fits its row. A password field shows asterisks. Apply saves the text as typed;
        /// Cancel throws it away.
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
                yield return ShowTab(ctx, popup, TestModName);
                Canvas.ForceUpdateCanvases();

                Transform menu = PageOf(popup, TestModName);
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
                TMP_InputField secret = menu.Find("ModMenuInput_ModMenusTest_Secret")?.GetComponentInChildren<TMP_InputField>(true);
                ctx.Assert(secret != null && secret.contentType == TMP_InputField.ContentType.Password, "The password field isn't a password field");
                ctx.Assert(name.contentType == TMP_InputField.ContentType.Standard, "A plain text field is " + name.contentType);

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

                // Apply saves the text as typed, and the menu reopens showing it. The password field draws only asterisks.
                yield return TypeInto(name, "Bye", "!");
                if (secret != null)
                {
                    yield return TypeInto(secret, null, "key-42");
                    ctx.Assert(secret.text == "key-42", "The password field holds '" + secret.text + "', expected what was typed");
                    ctx.Assert(!secret.textComponent.text.Contains("key") && secret.textComponent.text.Contains("*"), "The password field draws '" + secret.textComponent.text + "'");
                }
                Type managerType = AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuManager");
                AccessTools.Method(managerType, "OnApply").Invoke(popup.GetComponentInChildren(managerType, true), null);
                ctx.Assert(variables.Get("ModMenusTest_Name") == "Bye!", "Apply saved '" + variables.Get("ModMenusTest_Name") + "', expected 'Bye!'");
                ctx.Assert(variables.Get("ModMenusTest_Short") == "", "Apply saved the empty field as '" + (variables.Get("ModMenusTest_Short") ?? "nothing") + "'");
                ctx.Assert(variables.Get("ModMenusTest_Secret") == "key-42", "Apply saved the password field as '" + (variables.Get("ModMenusTest_Secret") ?? "nothing") + "'");
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
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_LongSlider"", ""labelID"": ""A slider whose label is too long for one line of the menu, so it wraps onto a second line under the first one"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 1 },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_LongCheck"", ""labelID"": ""A checkbox whose label is too long for one line of the menu, so it wraps onto a second line under the first one"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_LongDropdown"", ""labelID"": ""A dropdown whose label is too long for one line of the menu, so it wraps onto a second line under the first one"", ""itemIDList"": [""One"", ""Two""] },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_LongName"", ""labelID"": ""A text field whose label is too long for one line of the menu, so it wraps onto a second line under the first one"" },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Check2"", ""labelID"": ""Checkbox 2"" },
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_Slider2"", ""labelID"": ""Slider at the end"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 20 }
        ]";

        /// <summary>
        /// Rows are spaced evenly: each draws only inside itself and fills it, top to bottom, so the gaps between
        /// what's drawn are the same between any two rows (a title keeps room around it, and a blank text row
        /// draws nothing). A slider's track starts at the left edge, like the labels and boxes. The menu keeps the
        /// same margin on every side of the panel, and either side of the line between the tabs and the settings.
        /// A label too long for one line wraps inside its row, above its control, and a dropdown's box goes back
        /// under its wrapped label when its list closes.
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
                yield return ShowTab(ctx, popup, TestModName);
                yield return new WaitForSecondsRealtime(0.5f);
                Canvas.ForceUpdateCanvases();
                float unit = popup.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                Transform menu = PageOf(popup, TestModName);
                float halfGap = menu.GetComponent<VerticalLayoutGroup>().spacing / 2;

                // The same margin on every side of the panel, and either side of the line between the two columns
                Rect panel = ScreenRect((RectTransform)popup.transform.Find("Panel"));
                ScrollRect scroll = SettingsScroll(popup);
                ScrollRect tabScroll = Find(popup.transform, "ModMenuTabs").First().GetComponent<ScrollRect>();
                Rect view = ScreenRect(scroll.viewport);
                Rect scrollbar = ScreenRect((RectTransform)scroll.verticalScrollbar.transform);
                Rect tabs = ScreenRect((RectTransform)tabScroll.transform);
                Rect line = ScreenRect((RectTransform)Find(popup.transform, "ModMenuDivider").First());
                float[] margins = { tabs.xMin - panel.xMin, panel.xMax - scrollbar.xMax, panel.yMax - view.yMax, view.yMin - panel.yMin,
                    panel.yMax - tabs.yMax, tabs.yMin - panel.yMin, line.xMin - tabs.xMax, view.xMin - line.xMax };
                string marginText = string.Join(", ", margins.Select(m => m.ToString("0.0")));
                ctx.Record("margins", marginText);
                ctx.Assert(margins.Max() - margins.Min() <= 1f, "The menu's margins differ: left, right, top, bottom, tabs' top and bottom, either side of the line = " + marginText);
                ctx.Assert(scrollbar.xMin >= view.xMax, "The scroll bar overlaps the rows");
                ctx.Assert(ScreenRect((RectTransform)tabScroll.verticalScrollbar.transform).xMin >= ScreenRect(tabScroll.viewport).xMax, "The tabs' scroll bar overlaps the tabs");

                List<RectTransform> rows = menu.Cast<Transform>().Cast<RectTransform>().ToList();
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
                        // The title is the page's first row, and sits close to its settings
                        if (row.name == "ModMenuText_" + TestModTitle)
                            ctx.Assert(below <= 6, "The title isn't close to its settings: " + below.ToString("0.0") + " empty below");
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

                // Long labels wrap inside their rows, above their controls (beside it, for a checkbox)
                foreach (RectTransform row in rows.Where(r => r.name.Contains("_ModMenusTest_Long")))
                {
                    ScrollToMiddle(row);
                    yield return null;
                    Rect text = TextRect(LabelOf(row));
                    Rect rect = ScreenRect(row);
                    ctx.Record(row.name + "Label", text + " row " + rect);
                    ctx.Assert(text.height >= 1.8f * 15 * unit, row.name + "'s label is on one line: " + (text.height / unit).ToString("0.0") + " tall");
                    ctx.Assert(text.xMax <= rect.xMax + unit && text.yMin >= rect.yMin - unit && text.yMax <= rect.yMax + unit, row.name + "'s label runs outside its row: " + text + ", the row is " + rect);
                    Transform control = row.Find("Slider") ?? row.Find("Dropdown") ?? row.Find("Field");
                    if (control != null)
                        ctx.Assert(ScreenRect((RectTransform)control).yMax <= text.yMin + unit, row.name + "'s control overlaps its label: its top is " + ((ScreenRect((RectTransform)control).yMax - text.yMin) / unit).ToString("0.0") + " above the label's last line");
                }
                RectTransform longDropdownRow = rows.First(r => r.name == "ModMenuDropdown_ModMenusTest_LongDropdown");
                ScrollToMiddle(longDropdownRow);
                CustomDropdown longDropdown = longDropdownRow.GetComponentInChildren<CustomDropdown>(true);
                longDropdown.Animate();
                yield return new WaitForSecondsRealtime(0.5f);
                Component longItem = longDropdownRow.GetComponent(AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuItem"));
                AccessTools.Method(longItem.GetType(), "CloseList").Invoke(longItem, null);
                yield return new WaitForSecondsRealtime(0.5f);
                Rect boxAfter = ScreenRect((RectTransform)longDropdown.transform);
                Rect labelAfter = TextRect(LabelOf(longDropdownRow));
                ctx.Assert(longDropdown.transform.parent == longDropdownRow && boxAfter.yMax <= labelAfter.yMin + unit && Inside(boxAfter, ScreenRect(longDropdownRow)),
                    "After its list closed, the long dropdown's box isn't under its label in its row: box " + boxAfter + ", label " + labelAfter);

                yield return Screenshot(Path.Combine(Path.Combine(Path.GetTempPath(), "ModMenusInputScreens"), "3-layout.png"));

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        private const string HiddenModName = "com.test.modmenustesthidden";
        private const string OtherModName = "com.test.modmenustestother";

        private const string HiddenMenu = @"[
            { ""type"": ""slider"", ""varID"": ""ModMenusTest_HiddenSlider"", ""labelID"": ""Hidden slider"", ""minValue"": 1, ""maxValue"": 20, ""defaultValue"": 5 },
            { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_HiddenCheck"", ""labelID"": ""Hidden checkbox"" },
            { ""type"": ""dropdown"", ""varID"": ""ModMenusTest_HiddenPick"", ""labelID"": ""Hidden dropdown"", ""itemIDList"": [""One"", ""Two"", ""Three""] },
            { ""type"": ""input"", ""varID"": ""ModMenusTest_HiddenName"", ""labelID"": ""Hidden text field"", ""defaultValue"": ""Default"" }
        ]";

        private const string OtherMenu = @"[ { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_OtherCheck"", ""labelID"": ""Other checkbox"" } ]";

        /// <summary>
        /// A page shown for the first time long after the menu opened still shows the saved settings: the game's
        /// dropdown sets itself up and the game's labels load their text only when they're first shown, which the
        /// unit tests can't run. Hiding a page and showing it again (Unity disables and enables its controls)
        /// keeps what the player changed and hasn't applied.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator HiddenPagesShowTheirValues(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            using (AddTestMods(new[] { "ModMenus Test Hidden", "ModMenus Test Other" }, new[] { HiddenModName, OtherModName }, new[] { HiddenMenu, OtherMenu }))
            {
                variables.Set("ModMenusTest_HiddenSlider", "15");
                variables.Set("ModMenusTest_HiddenCheck", "1");
                variables.Set("ModMenusTest_HiddenPick", "2");
                variables.Set("ModMenusTest_HiddenName", "Saved text");

                // The menu opens on the other mod's tab, so the hidden page has never been shown
                AccessTools.Field(mod.GetType("ModMenus.ModMenusUtils+ModMenuManager", true), "lastModName").SetValue(null, OtherModName);
                var opened = new List<GameObject>();
                yield return OpenModSettings(ctx, opened);
                if (opened.Count == 0)
                {
                    Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                    yield break;
                }
                GameObject popup = opened[0];
                yield return new WaitForSecondsRealtime(0.5f);
                Transform page = PageOf(popup, HiddenModName);
                ctx.Assert(page != null && !page.gameObject.activeInHierarchy, "The hidden mod's page is shown when the menu opens");

                Settings_Slider slider = page.Find("ModMenuSlider_ModMenusTest_HiddenSlider").GetComponent<Settings_Slider>();
                Checkbox_Text check = page.Find("ModMenuCheckbox_ModMenusTest_HiddenCheck").GetComponent<Checkbox_Text>();
                CustomDropdown pick = page.Find("ModMenuDropdown_ModMenusTest_HiddenPick").GetComponentInChildren<CustomDropdown>(true);
                TMP_InputField field = page.Find("ModMenuInput_ModMenusTest_HiddenName").GetComponentInChildren<TMP_InputField>(true);

                // First shown: the game's controls start up and keep the saved settings
                yield return ShowTab(ctx, popup, HiddenModName);
                yield return new WaitForSecondsRealtime(0.3f);
                AssertShows(ctx, "first shown", slider, 15, check, true, pick, 2, "Three", field, "Saved text");

                // Change each one as a player does
                slider.Slider_Obj.GetComponent<Slider>().value = 2f / 19f;
                check.GetComponentInChildren<Button>().onClick.Invoke();
                pick.Animate();
                yield return new WaitForSecondsRealtime(0.5f);
                pick.GetButton(0).GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return TypeInto(field, "Changed", "");

                // Hidden and shown again: the changes are still there
                yield return ShowTab(ctx, popup, OtherModName);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return ShowTab(ctx, popup, HiddenModName);
                yield return new WaitForSecondsRealtime(0.3f);
                AssertShows(ctx, "shown again", slider, 3, check, false, pick, 0, "One", field, "Changed");
                ctx.Assert(variables.Get("ModMenusTest_HiddenSlider") == "15", "Switching tabs saved the slider");

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// The controls show these values, and Apply would save them.
        /// </summary>
        private static void AssertShows(TestContext ctx, string when, Settings_Slider slider, float sliderValue, Checkbox_Text check, bool ticked,
            CustomDropdown pick, int picked, string pickedName, TMP_InputField field, string text)
        {
            string label = slider.Title.GetComponent<TextMeshProUGUI>().text;
            float position = slider.Slider_Obj.GetComponent<Slider>().value;
            ctx.Assert(label == "Hidden slider: " + sliderValue && Mathf.Abs(position - (sliderValue - 1) / 19f) < 0.001f,
                when + ": the slider shows '" + label + "' at " + position.ToString("0.000") + ", expected " + sliderValue);
            ctx.Assert(check.Checkbox.GetComponent<Image>().sprite == (ticked ? check.Checked : check.Empty), when + ": the checkbox isn't " + (ticked ? "ticked" : "empty"));
            ctx.Assert(pick.selectedText.text == pickedName && pick.selectedItemIndex == picked, when + ": the dropdown shows " + pick.selectedText.text + ", expected " + pickedName);
            ctx.Assert(field.text == text, when + ": the text field shows '" + field.text + "', expected '" + text + "'");

            Type itemType = AccessTools.TypeByName("ModMenus.ModMenusUtils+ModMenuItem");
            float Saves(Component row) => Traverse.Create(row.GetComponentInParent(itemType)).Field("tempValue").GetValue<float>();
            ctx.Assert(Saves(slider) == sliderValue && Saves(check) == (ticked ? 1 : 0) && Saves(pick) == picked,
                when + ": Apply would save " + Saves(slider) + ", " + Saves(check) + ", " + Saves(pick));
            ctx.Assert(Traverse.Create(field.GetComponentInParent(itemType)).Field("tempText").GetValue<string>() == text, when + ": Apply wouldn't save the text shown");
        }

        private const string LongModTitle = "ModMenus Test with a name far too long for its tab, which goes on and on so that it would take four or five lines if the tab let it wrap that far";
        private const string LongModName = "com.test.modmenustestlong";

        /// <summary>
        /// With many mods, the tab column scrolls on its own and its last tab can be reached and clicked. Tabs are
        /// drawn inside the column without overlapping. A name too long for its tab is drawn on two lines at most,
        /// cut short with "…", and the mod's page shows the whole name.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator LongTabListAndLongNamesDrawInside(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            var titles = new List<string> { LongModTitle };
            var modNames = new List<string> { LongModName };
            var menus = new List<string> { @"[ { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Long"", ""labelID"": ""Checkbox"" } ]" };
            for (int i = 1; i <= 30; i++)
            {
                titles.Add("ModMenus Test " + i.ToString("00"));
                modNames.Add("com.test.modmenustest" + i);
                menus.Add(@"[ { ""type"": ""checkbox"", ""varID"": ""ModMenusTest_Many" + i + @""", ""labelID"": ""Checkbox"" } ]");
            }
            using (AddTestMods(titles.ToArray(), modNames.ToArray(), menus.ToArray()))
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

                ScrollRect tabScroll = Find(popup.transform, "ModMenuTabs").First().GetComponent<ScrollRect>();
                Rect column = ScreenRect(tabScroll.viewport);
                List<RectTransform> tabs = tabScroll.content.Cast<Transform>().Cast<RectTransform>().ToList();
                ctx.Record("tabs", tabs.Count);
                ctx.Assert(tabScroll.content.rect.height > tabScroll.viewport.rect.height, "The tabs fit without scrolling, so this checks nothing");

                // Inside the column, left of its scroll bar, and not overlapping
                for (int i = 0; i < tabs.Count; i++)
                {
                    Rect tab = ScreenRect(tabs[i]);
                    ctx.Assert(tab.xMin >= column.xMin - 1 && tab.xMax <= column.xMax + 1, tabs[i].name + " is wider than the tab column: " + tab + ", the column is " + column);
                    if (i > 0)
                        ctx.Assert(tab.yMax <= ScreenRect(tabs[i - 1]).yMin + 0.5f, tabs[i].name + " overlaps the tab above it");
                }

                // Anywhere down the column, from the first tab's top to the last tab in view, a click reaches a tab
                List<RectTransform> inView = tabs.Where(t => Inside(ScreenRect(t), column)).ToList();
                var dead = new List<string>();
                foreach (float x in new[] { column.xMin + 3, column.center.x, column.xMax - 3 })
                {
                    for (float y = ScreenRect(inView[0]).yMax - 1; y > ScreenRect(inView[inView.Count - 1]).yMin + 1; y--)
                    {
                        GameObject under = TopHit(new Vector2(x, y));
                        if (under == null || !tabs.Any(t => under.transform.IsChildOf(t)))
                            dead.Add(x.ToString("0") + "," + y.ToString("0") + "=" + (under != null ? under.name : "nothing"));
                    }
                }
                ctx.Record("deadPoints", dead.Count + ": " + string.Join(" ", dead.Take(12)));
                ctx.Assert(dead.Count == 0, dead.Count + " points down the tab column reach no tab, e.g. " + string.Join(" ", dead.Take(6)));

                // The last tab can be scrolled to, and clicked
                tabScroll.verticalNormalizedPosition = 0;
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform last = tabs[tabs.Count - 1];
                ctx.Assert(Inside(ScreenRect(last), column), "Scrolled to the bottom, the last tab isn't fully visible: " + ScreenRect(last) + ", the column is " + column);
                GameObject hit = TopHit(ScreenRect(last).center);
                ctx.Assert(hit != null && hit.transform.IsChildOf(last), "The last tab: a click on it would hit " + (hit != null ? hit.name : "nothing"));
                Click(last.gameObject);
                yield return null;
                Transform lastPage = PageOf(popup, last.name.Substring("ModMenuTab_".Length));
                ctx.Assert(lastPage != null && lastPage.gameObject.activeInHierarchy, "Clicking the last tab didn't show its page");

                // The long name: two lines at most, cut short with "…", inside its tab
                yield return ShowTab(ctx, popup, LongModName);
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform longTab = (RectTransform)Find(popup.transform, "ModMenuTab_" + LongModName).First();
                TextMeshProUGUI longLabel = longTab.GetComponentInChildren<TextMeshProUGUI>(true);
                TextMeshProUGUI shortLabel = tabs.First(t => t != longTab).GetComponentInChildren<TextMeshProUGUI>(true);
                longLabel.ForceMeshUpdate();
                TMP_TextInfo info = longLabel.textInfo;
                string shown = new string(info.characterInfo.Take(info.characterCount).Where(c => c.isVisible).Select(c => c.character).ToArray());
                Rect longText = TextRect(longLabel);
                float oneLine = TextRect(shortLabel).height;
                ctx.Record("longTab", info.lineCount + " lines, " + (longText.height / oneLine).ToString("0.00") + " lines tall: " + shown);
                ctx.Assert(info.lineCount <= 2 && longText.height <= 2.2f * oneLine, "The long tab's name takes " + info.lineCount + " lines");
                ctx.Assert(longLabel.isTextTruncated && shown.EndsWith("…"), "The long tab's name isn't cut short with an ellipsis: '" + shown + "'");
                ctx.Assert(Inside(longText, ScreenRect(longTab)), "The long tab's name is drawn outside its tab: " + longText + ", the tab is " + ScreenRect(longTab));

                // Its page shows the whole name, wrapped inside the settings
                TextMeshProUGUI title = PageOf(popup, LongModName).GetChild(0).GetComponent<TextMeshProUGUI>();
                title.ForceMeshUpdate();
                Rect titleText = TextRect(title);
                Rect view = ScreenRect(SettingsScroll(popup).viewport);
                ctx.Assert(title.text == LongModTitle && !title.isTextTruncated && title.textInfo.lineCount >= 2, "The long mod's page title isn't its whole name on several lines: " + title.textInfo.lineCount + " lines");
                ctx.Assert(titleText.xMin >= view.xMin - 1 && titleText.xMax <= view.xMax + 1 && Inside(titleText, ScreenRect((RectTransform)title.transform)),
                    "The long mod's page title is drawn outside the settings: " + titleText + ", the view is " + view);

                yield return Game.CloseAllPopups(ctx);
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// On a short screen the whole menu is on screen, and Apply and Cancel take clicks. The game's popups are
        /// 1024 wide whatever the screen, so a 21:9 screen makes them 432 tall; the popups' scaling is changed to
        /// that height here, as no other screen shape can be tried without changing the player's window. Going back
        /// to the normal size with the menu open fits it again.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator MenuFitsAShortScreen(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
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
                CanvasScaler scaler = popup.GetComponentInParent<Canvas>().rootCanvas.GetComponent<CanvasScaler>();
                Vector2 reference = scaler.referenceResolution;
                float match = scaler.matchWidthOrHeight;
                yield return Game.CloseAllPopups(ctx);

                using (TestTools.Restore(() =>
                {
                    scaler.referenceResolution = reference;
                    scaler.matchWidthOrHeight = match;
                }))
                {
                    scaler.matchWidthOrHeight = 0;
                    scaler.referenceResolution = new Vector2(432f * Screen.width / Screen.height, reference.y);
                    yield return null;
                    yield return null;

                    var reopened = new List<GameObject>();
                    yield return OpenModSettings(ctx, reopened);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Canvas.ForceUpdateCanvases();

                    RectTransform root = (RectTransform)popup.transform;
                    ctx.Record("shortPopup", root.rect.size);
                    ctx.Assert(Mathf.Abs(root.rect.height - 432) < 2, "The popup is " + root.rect.height + " tall, expected 432");
                    Rect screen = new Rect(0, 0, Screen.width, Screen.height);
                    foreach (string part in new[] { "Panel", "ModMenuTabs", "ScrollContainer", "ModMenuDivider", "ModMenuApply", "ModMenuCancel" })
                    {
                        RectTransform rect = (RectTransform)Find(popup.transform, part).First();
                        ctx.Assert(Inside(ScreenRect(rect), screen), part + " is off the screen: " + ScreenRect(rect));
                    }
                    foreach (string button in new[] { "ModMenuApply", "ModMenuCancel" })
                    {
                        RectTransform rect = (RectTransform)popup.transform.Find(button);
                        GameObject hit = TopHit(ScreenRect(rect).center);
                        ctx.Assert(hit != null && hit.transform.IsChildOf(rect), button + ": a click on it would hit " + (hit != null ? hit.name : "nothing"));
                    }
                    AssertPanelFits(ctx, mod, popup, "on the short screen");

                    // Back to the normal size, with the menu open
                    scaler.referenceResolution = reference;
                    scaler.matchWidthOrHeight = match;
                    yield return null;
                    yield return null;
                    yield return null;
                    AssertPanelFits(ctx, mod, popup, "back at the normal size");

                    yield return Game.CloseAllPopups(ctx);
                }
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
            }
        }

        /// <summary>
        /// The panel is the size ModMenus works out for the popup's size now.
        /// </summary>
        private static void AssertPanelFits(TestContext ctx, Assembly mod, GameObject popup, string when)
        {
            RectTransform root = (RectTransform)popup.transform;
            RectTransform panel = (RectTransform)popup.transform.Find("Panel");
            float buttonHeight = ((RectTransform)popup.transform.Find("ModMenuApply")).rect.height;
            object layout = AccessTools.Method(mod.GetType("ModMenus.ModMenusUtils", true), "LayoutFor").Invoke(null, new object[] { root.rect.size, buttonHeight });
            Rect expected = Traverse.Create(layout).Field("Panel").GetValue<Rect>();
            ctx.Record("panel " + when, panel.rect.size + " in " + root.rect.size);
            ctx.Assert(Mathf.Abs(panel.rect.width - expected.width) < 1 && Mathf.Abs(panel.rect.height - expected.height) < 1,
                when + ", the panel is " + panel.rect.size + ", expected " + expected.size + " for a popup " + root.rect.size);
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

            // The list touches its box, whether it opened below it or, near the bottom of the screen, above it
            Rect box = ScreenRect((RectTransform)dropdown.selectedText.transform.parent);
            float unit = dropdown.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            bool above = listView.center.y > box.center.y;
            float gap = above ? listView.yMin - box.yMax : box.yMin - listView.yMax;
            ctx.Record(varID + "ListGap", (above ? "above, " : "below, ") + (gap / unit).ToString("0.0"));
            ctx.Assert(gap <= unit, varID + "'s list is " + (gap / unit).ToString("0.0") + " units " + (above ? "above" : "below") + " its box, not touching it");
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

        /// <summary>The label ModMenus wraps in this row.</summary>
        private static TextMeshProUGUI LabelOf(RectTransform row) =>
            Traverse.Create(row.GetComponent(AccessTools.TypeByName("ModMenus.ModMenusUtils+LabelWrap"))).Field("label").GetValue<TextMeshProUGUI>();

        /// <summary>The screen area a text's characters take up, rather than its box.</summary>
        private static Rect TextRect(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Bounds bounds = text.textBounds;
            Canvas canvas = text.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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

        /// <summary>
        /// Where an object is, and which of it and its parents are switched off.
        /// </summary>
        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent)
                parts.Insert(0, t.name + (t.gameObject.activeSelf ? "" : " (off)"));
            return string.Join("/", parts);
        }

        private static IEnumerable<Transform> Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);
        }
    }
}
