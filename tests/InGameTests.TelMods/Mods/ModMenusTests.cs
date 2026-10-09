using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// ModMenus clones the game's own Settings button and settings popup by object name, which
    /// the unit tests can only fake. Here they are the real prefabs, and the rows come from every
    /// installed mod's modmenu.json.
    /// </summary>
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
