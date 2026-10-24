using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleJSON;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// A mod with a menu: its settings, and the name its tab shows.
        /// </summary>
        public class MenuSource
        {
            public Mods._mod Mod;
            public JSONArray Items;
            public string Title;
        }

        /// <summary>
        /// The enabled mods with a menu file that can be read, in tab order: by the name each tab shows,
        /// ignoring case. Mods with the same name keep their load order.
        /// </summary>
        public static List<MenuSource> FindMenus()
        {
            string relativePath = Path.Combine("JSON", JSON_DIR, JSON_FILE);
            List<MenuSource> menus = new();
            foreach (Mods._mod mod in Mods._Mods)
            {
                if (mod == null || !mod.IsEnabled())
                    continue;

                string filepath = Path.Combine(mod.Path, relativePath).Replace("\\", "/");
                if (!File.Exists(filepath))
                    continue;

                JSONArray items = ReadMenuFile(mod.Title, filepath);
                if (items == null)
                    continue;

                menus.Add(new MenuSource { Mod = mod, Items = items, Title = ShownText(mod.Title).Trim() });
            }

            // OrderBy keeps equal names in load order. Ordinal, so it sorts the same in every language.
            return menus.OrderBy(menu => menu.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// A text ID's text in the player's language, or the ID as written if it isn't a constant.
        /// </summary>
        public static string ShownText(string textID)
        {
            return textID != null && Language.Data.TryGetValue(textID, out string text) ? text : textID ?? "";
        }

        /// <summary>
        /// Builds the menu: the Hotkeys tab with every mod's keys, if any mod has one, then a page of settings
        /// and a tab for each mod with other settings; then shows the tab the player last used, or the first one.
        /// </summary>
        /// <param name="manager">The menu's manager, which switches pages when a tab is clicked.</param>
        /// <param name="pageParent">Where the pages go: the settings' scroll area.</param>
        /// <param name="tabParent">Where the tabs go: the tab column's scroll area.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes, above every row (see AddMenuDropdown).</param>
        public static void AddMenuItems(ModMenuManager manager, Transform pageParent, Transform tabParent, Transform dropdownLayer)
        {
            manager.tabs = new List<ModMenuTab>();
            manager.selected = null;

            List<MenuSource> menus = FindMenus();
            if (menus.Count == 0)
            {
                GameObject message = AddMenuText(NO_SETTINGS_LABEL, pageParent, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Center);
                message.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                return;
            }

            // Every mod's keys are on one tab, so a key used twice is easy to see and change
            List<MenuSource> sections = new();
            List<MenuSource> pages = new();
            foreach (MenuSource menu in menus)
            {
                SplitMenu(menu, out JSONArray keys, out JSONArray others);
                if (keys.Count > 0)
                {
                    sections.Add(new MenuSource { Mod = menu.Mod, Items = keys, Title = ShownText(SectionTitle(menu)) });
                }
                if (keys.Count == 0 || HasSetting(others))
                {
                    pages.Add(new MenuSource { Mod = menu.Mod, Items = others, Title = menu.Title });
                }
            }

            List<string> modNames = new();
            if (sections.Count > 0)
            {
                MenuSource hotkeys = HotkeysMenu();
                GameObject page = AddHotkeysPage(hotkeys, OrderKeybindSections(sections), pageParent, dropdownLayer);
                manager.tabs.Add(AddMenuTab(hotkeys, page, manager, tabParent));
                modNames.Add(hotkeys.Mod.ModName);
            }
            foreach (MenuSource menu in pages)
            {
                GameObject page = AddMenuPage(menu, pageParent, dropdownLayer);
                manager.tabs.Add(AddMenuTab(menu, page, manager, tabParent));
                modNames.Add(menu.Mod.ModName);
            }

            // A changed setting may show or hide rows with enabledIf
            foreach (ModMenuItem item in pageParent.GetComponentsInChildren<ModMenuItem>(true))
            {
                item.manager = manager;
            }

            // Every page is built shown, as the menu's rows always were; only now are the others hidden
            manager.ShowPage(manager.tabs[RememberedTab(modNames, ModMenuManager.lastModName)]);
        }

        /// <summary>
        /// The tab to show: the one the player last used, or the first if that mod has no tab now.
        /// </summary>
        public static int RememberedTab(IList<string> modNames, string remembered)
        {
            for (int i = 0; i < modNames.Count; i++)
            {
                if (modNames[i] == remembered)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Adds a mod's page: its name, its "Use in all saves" box (see AddScopeRow), then its rows in file order.
        /// </summary>
        /// <param name="menu">The mod and its settings.</param>
        /// <param name="pageParent">Where the page goes: the settings' scroll area.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes, above every row (see AddMenuDropdown).</param>
        /// <returns>The page.</returns>
        public static GameObject AddMenuPage(MenuSource menu, Transform pageParent, Transform dropdownLayer)
        {
            GameObject page = new(PAGE_OBJ_NAME + "_" + menu.Mod.ModName, typeof(RectTransform), typeof(VerticalLayoutGroup));
            page.transform.SetParent(pageParent, false);
            SetColumnLayout(page.GetComponent<VerticalLayoutGroup>(), MENUITEM_SPACING, false);
            Transform parentTransform = page.transform;

            // The whole name, however long: a tab may cut it short
            GameObject titleRow = AddMenuText(menu.Mod.Title, parentTransform, TITLE_SIZE, mainScript.black32, TextAlignmentOptions.Bottom);
            ((RectTransform)titleRow.transform).sizeDelta = new Vector2(200, TITLE_HEIGHT);
            titleRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            JSONArray jsonArray = menu.Items;
            AddScopeRow(menu.Mod.Title, HasSecretField(jsonArray), parentTransform);
            for (int i = 0; i < jsonArray.Count; i++)
            {
                AddMenuRow(jsonArray[i], parentTransform, dropdownLayer);
            }

            return page;
        }

        /// <summary>
        /// The text field option that masked a field until ModMenus 5.0.0; secret rows replaced it.
        /// </summary>
        private const string REMOVED_FIELD_PASSWORD = "password";

        /// <summary>
        /// Whether a text field still has "password": true, which ModMenus 5.0.0 removed. Its row is left out:
        /// built without the option, the field would show its secret and share it in all saves.
        /// </summary>
        public static bool UsesRemovedPassword(JSONNode item)
        {
            return (string)item[JSON_FIELD_TYPE] == JSON_TYPE_INPUT && !string.IsNullOrEmpty(item[REMOVED_FIELD_PASSWORD]) && item[REMOVED_FIELD_PASSWORD].AsBool;
        }

        /// <summary>
        /// Adds a row for an item of a mod's menu file, with the options any row can have (see AddRowOptions).
        /// </summary>
        /// <param name="item">The item from the mod's menu file.</param>
        /// <param name="parentTransform">Where the row goes: a page.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes, above every row (see AddMenuDropdown).</param>
        /// <returns>The row, or null if the item has none (it's ignored, or misses its varID or labelID).</returns>
        public static GameObject AddMenuRow(JSONNode item, Transform parentTransform, Transform dropdownLayer)
        {
            string type = item[JSON_FIELD_TYPE];
            string label = item[JSON_FIELD_LABELID];

            // Text without a label is a blank row, the documented way to add spacing.
            // (The game's JSON parser drops empty strings, so an empty labelID reads as missing.)
            if (string.IsNullOrEmpty(type) || (string.IsNullOrEmpty(label) && type != JSON_TYPE_TEXT) || item[JSON_FIELD_IGNORE].AsBool) return null;
            string id;
            GameObject row = null;

            switch (type)
            {
                case JSON_TYPE_TEXT:
                    row = AddMenuText(label ?? "", parentTransform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);

                    // As tall as its lines, so long text doesn't run into the next row. A blank one keeps its height: it's a spacer.
                    if (!string.IsNullOrEmpty(label))
                        row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    break;

                case JSON_TYPE_SLIDER:
                    if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                        return null;

                    id = item[JSON_FIELD_VARID];
                    GetSliderRange(item, out float minValue, out float maxValue, out float defaultFloat);

                    row = AddMenuSlider(id, label, minValue, maxValue, defaultFloat, parentTransform);
                    break;

                case JSON_TYPE_CHECKBOX:
                    if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                        return null;

                    id = item[JSON_FIELD_VARID];
                    bool defaultBool = false;
                    if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
                    {
                        defaultBool = item[JSON_FIELD_DEF].AsBool;
                    }

                    row = AddMenuCheckbox(id, label, defaultBool, parentTransform);
                    break;

                case JSON_TYPE_DROPDOWN:
                    if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]) || string.IsNullOrEmpty(item[JSON_FIELD_LIST].ToString()) || item[JSON_FIELD_LIST].AsArray == null)
                        return null;

                    id = item[JSON_FIELD_VARID];
                    int defaultSelect = 0;
                    if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
                    {
                        defaultSelect = item[JSON_FIELD_DEF].AsInt;
                    }

                    JSONArray jsonArrayItems = item[JSON_FIELD_LIST].AsArray;
                    string[] itemList = new string[jsonArrayItems.Count];
                    for (int j = 0; j < jsonArrayItems.Count; j++)
                    {
                        itemList[j] = jsonArrayItems[j];
                    }

                    row = AddMenuDropdown(id, label, itemList, defaultSelect, parentTransform, dropdownLayer);
                    break;

                case JSON_TYPE_INPUT:
                    if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                        return null;

                    id = item[JSON_FIELD_VARID];
                    if (UsesRemovedPassword(item))
                    {
                        Debug.LogError("[ModMenus] The text field " + id + " uses password, which was removed in ModMenus 5.0.0, so it's left out. Use \"type\": \"secret\" instead.");
                        return null;
                    }
                    row = AddMenuInput(id, label, GetInputDefault(item), item[JSON_FIELD_MAXLEN].AsInt, parentTransform);
                    break;

                case JSON_TYPE_SECRET:
                    id = item[JSON_FIELD_SECRETID];
                    if (!SecretStore.IsValidID(id))
                    {
                        Debug.LogError("[ModMenus] The secret row " + label + " is left out: its secretID \"" + id + "\" isn't a name, or a folder and a name, of letters, digits, spaces, '-', '_' and '.'.");
                        return null;
                    }
                    row = AddMenuSecret(id, label, parentTransform);
                    break;

                case JSON_TYPE_KEYBIND:
                    if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                        return null;

                    id = item[JSON_FIELD_VARID];
                    if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]) && !TryParseKeybind(item[JSON_FIELD_DEF], out _))
                    {
                        Debug.LogWarning("[ModMenus] The key " + id + "'s defaultValue \"" + (string)item[JSON_FIELD_DEF] + "\" isn't a key, so it starts unset.");
                    }
                    row = AddMenuKeybind(id, label, GetKeybindDefault(item), parentTransform);
                    break;
            }

            if (row != null)
            {
                AddRowOptions(item, row);
            }
            return row;
        }

        /// <summary>
        /// Gives a row the options any item can have, which the row builders don't take: a slider's step, a
        /// number field's limits, "reset": false, and enabledIf.
        /// </summary>
        public static void AddRowOptions(JSONNode item, GameObject row)
        {
            ModMenuItem menuItem = row.GetComponent<ModMenuItem>();
            if (menuItem != null)
            {
                // Only an explicit false: a missing field reads as false too
                menuItem.noReset = !string.IsNullOrEmpty(item[JSON_FIELD_RESET]) && !item[JSON_FIELD_RESET].AsBool;

                if (menuItem.settingsSlider != null)
                {
                    if (!TryGetSliderStep(item, out menuItem.step))
                    {
                        Debug.LogWarning("[ModMenus] The slider " + menuItem.varID + "'s step isn't a whole number of at least 1, so it moves in steps of 1.");
                    }
                }

                if (menuItem.inputField != null && GetNumberField(item, out string inputType, out float? min, out float? max))
                {
                    MakeNumberField(menuItem, inputType, min, max);
                }
            }

            string condition = item[JSON_FIELD_ENABLEDIF];
            if (!string.IsNullOrEmpty(condition))
            {
                row.AddComponent<ModMenuCondition>().varID = condition;
            }
        }

        /// <summary>
        /// Makes a text field take only a number: the game's field lets only a number be typed, and leaving the
        /// field moves it inside its limits (see ValidNumber).
        /// </summary>
        public static void MakeNumberField(ModMenuItem menuItem, string inputType, float? min, float? max)
        {
            menuItem.inputType = inputType;
            menuItem.inputMin = min;
            menuItem.inputMax = max;

            TMP_InputField field = menuItem.inputField;
            field.contentType = inputType == JSON_INPUT_INTEGER ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
            field.onEndEdit = new TMP_InputField.SubmitEvent();
            field.onEndEdit.AddListener(menuItem.onEndInput);
        }

        /// <summary>
        /// Adds a mod's tab: its name, which wraps onto TAB_MAX_LINES and then ends in "…". Clicking it shows its page.
        /// </summary>
        /// <param name="menu">The mod.</param>
        /// <param name="page">The mod's page, which the tab shows.</param>
        /// <param name="manager">The menu's manager, which switches pages.</param>
        /// <param name="tabParent">Where the tab goes: the tab column's scroll area.</param>
        /// <returns>The tab.</returns>
        public static ModMenuTab AddMenuTab(MenuSource menu, GameObject page, ModMenuManager manager, Transform tabParent)
        {
            GameObject tabObj = new(TAB_OBJ_NAME + "_" + menu.Mod.ModName, typeof(RectTransform), typeof(Image), typeof(Button));
            tabObj.transform.SetParent(tabParent, false);
            ((RectTransform)tabObj.transform).sizeDelta = new Vector2(TAB_WIDTH, LABEL_HEIGHT + 2 * TAB_PADDING);

            // The name sits in the tab's padding and grows downwards as it wraps, making the tab taller
            GameObject labelObj = AddMenuText(menu.Mod.Title, tabObj.transform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);
            SetRectTransform((RectTransform)labelObj.transform, Vector2.up, Vector2.one, new Vector2(TAB_PADDING, -TAB_PADDING - LABEL_HEIGHT), new Vector2(-TAB_PADDING, -TAB_PADDING), new Vector2(0.5f, 1));
            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            AddLabelWrap(tabObj, label, null);
            tabObj.GetComponent<LabelWrap>().maxLines = TAB_MAX_LINES;

            ModMenuTab tab = tabObj.AddComponent<ModMenuTab>();
            tab.modName = menu.Mod.ModName;
            tab.page = page;
            tab.manager = manager;
            tab.image = tabObj.GetComponent<Image>();
            tab.label = label;
            tab.button = tabObj.GetComponent<Button>();
            SetTabLooks(tab.button, label);

            tab.button.onClick = new Button.ButtonClickedEvent();
            tab.button.onClick.AddListener(tab.OnClick);
            return tab;
        }

        /// <summary>
        /// A tab takes clicks on its whole area, not on its name. It draws its own fill (see ModMenuTab.Render),
        /// so the button doesn't tint it.
        /// </summary>
        private static void SetTabLooks(Button button, TextMeshProUGUI label)
        {
            label.raycastTarget = false;
            button.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// Lays out a column: each child is the full width; its height is its own, or, if the column controls
        /// it, its preferred height.
        /// </summary>
        private static void SetColumnLayout(VerticalLayoutGroup column, float spacing, bool controlChildHeight)
        {
            column.childControlWidth = true;
            column.childForceExpandWidth = true;
            column.childControlHeight = controlChildHeight;
            column.childForceExpandHeight = false;
            column.spacing = spacing;
        }
    }
}
