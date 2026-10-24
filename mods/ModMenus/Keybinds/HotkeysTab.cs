using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// A key row's default from its JSON item: defaultValue in its saved form, or "None" if it isn't set or
        /// isn't a key.
        /// </summary>
        public static string GetKeybindDefault(JSONNode item)
        {
            return NormalizeKeybind(item[JSON_FIELD_DEF]) ?? KEY_NONE;
        }

        /// <summary>
        /// Splits a mod's menu file into the rows for the Hotkeys tab and the rest. Text rows go with the next
        /// row that isn't text, so a heading above keys moves with them; text at the end stays. Ignored rows
        /// are left out of both.
        /// </summary>
        public static void SplitKeybindRows(JSONArray items, out JSONArray keys, out JSONArray others)
        {
            keys = new JSONArray();
            others = new JSONArray();
            List<JSONNode> text = new();
            for (int i = 0; i < items.Count; i++)
            {
                JSONNode item = items[i];
                if (item[JSON_FIELD_IGNORE].AsBool)
                {
                    continue;
                }
                string type = item[JSON_FIELD_TYPE];
                if (type == JSON_TYPE_TEXT)
                {
                    text.Add(item);
                    continue;
                }
                JSONArray target = type == JSON_TYPE_KEYBIND ? keys : others;
                foreach (JSONNode heading in text)
                {
                    target.Add(heading);
                }
                text.Clear();
                target.Add(item);
            }
            foreach (JSONNode heading in text)
            {
                others.Add(heading);
            }
        }

        /// <summary>
        /// Whether a menu file's rows have a setting, not only text.
        /// </summary>
        public static bool HasSetting(JSONArray items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i][JSON_FIELD_TYPE].Value != JSON_TYPE_TEXT)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The mods' sections on the Hotkeys tab: the game's keys first; then the other mods' in tab order.
        /// </summary>
        public static List<MenuSource> OrderKeybindSections(List<MenuSource> sections)
        {
            return sections.Where(IsOwnMenu).Concat(sections.Where(s => !IsOwnMenu(s))).ToList();
        }

        /// <summary>
        /// Whether a menu is ModMenus' own, which holds the game's keys (see GameKeys) and the debug cheats switch:
        /// all its rows are on the Hotkeys tab, first, under the game's name. It's told apart by its switch, as the
        /// game names a mod after its folder, which differs between a copy from the Workshop and a local one.
        /// </summary>
        public static bool IsOwnMenu(MenuSource menu)
        {
            for (int i = 0; i < menu.Items.Count; i++)
            {
                if (menu.Items[i][JSON_FIELD_VARID].Value == GameKeys.DebugCheats.VAR_ID)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Splits a mod's menu into its rows for the Hotkeys tab and the rest (see SplitKeybindRows). ModMenus'
        /// own rows, the game's keys and the debug cheats switch, are all on the Hotkeys tab.
        /// </summary>
        public static void SplitMenu(MenuSource menu, out JSONArray keys, out JSONArray others)
        {
            SplitKeybindRows(menu.Items, out keys, out others);
            if (!IsOwnMenu(menu))
            {
                return;
            }
            keys = new JSONArray();
            for (int i = 0; i < menu.Items.Count; i++)
            {
                if (!menu.Items[i][JSON_FIELD_IGNORE].AsBool)
                {
                    keys.Add(menu.Items[i]);
                }
            }
            others = new JSONArray();
        }

        /// <summary>
        /// A section's heading on the Hotkeys tab: the mod's name, or the game's for the game's keys.
        /// </summary>
        public static string SectionTitle(MenuSource section)
        {
            return IsOwnMenu(section) ? GAME_KEYS_LABEL : section.Mod.Title;
        }

        /// <summary>
        /// The Hotkeys tab, as a menu with no rows of its own: AddHotkeysPage adds each mod's keys.
        /// </summary>
        public static MenuSource HotkeysMenu()
        {
            return new MenuSource
            {
                Mod = new Mods._mod { ModName = HOTKEYS_TAB_ID, Title = HOTKEYS_LABEL },
                Items = new JSONArray(),
                Title = ShownText(HOTKEYS_LABEL),
            };
        }

        /// <summary>
        /// Which rows use the same key as another row: for each varID, the varIDs of the other rows with its key.
        /// Rows with no key never clash. clash can say two rows don't: the game's keys work in different places
        /// (see GameKeys.Contexts).
        /// </summary>
        /// <param name="rows">Each row's varID and key, as saved.</param>
        public static Dictionary<string, List<string>> FindClashes(IList<KeyValuePair<string, string>> rows, Func<string, string, bool> clash)
        {
            List<KeyValuePair<string, string>> keyed = new();
            foreach (KeyValuePair<string, string> row in rows)
            {
                string key = NormalizeKeybind(row.Value);
                if (row.Key != null && key != null && key != KEY_NONE && !keyed.Any(k => k.Key == row.Key))
                {
                    keyed.Add(new KeyValuePair<string, string>(row.Key, key));
                }
            }

            Dictionary<string, List<string>> clashes = new();
            for (int i = 0; i < keyed.Count; i++)
            {
                for (int j = i + 1; j < keyed.Count; j++)
                {
                    if (keyed[i].Value != keyed[j].Value || !Clash(clash, keyed[i].Key, keyed[j].Key))
                    {
                        continue;
                    }
                    Add(keyed[i].Key, keyed[j].Key);
                    Add(keyed[j].Key, keyed[i].Key);
                }
            }
            return clashes;

            void Add(string varID, string other)
            {
                if (!clashes.TryGetValue(varID, out List<string> list))
                {
                    clashes[varID] = list = new List<string>();
                }
                list.Add(other);
            }
        }

        private static bool Clash(Func<string, string, bool> clash, string a, string b)
        {
            if (clash == null)
            {
                return true;
            }
            try
            {
                return clash(a, b);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] The key clash check failed, so " + a + " and " + b + " count as clashing: " + e.Message);
                return true;
            }
        }

        /// <summary>
        /// Adds the Hotkeys tab's page: its title, then each mod's heading and its keys.
        /// </summary>
        /// <param name="hotkeys">The Hotkeys tab (see HotkeysMenu).</param>
        /// <param name="sections">Each mod with keys, with only its key rows and their headings.</param>
        /// <param name="pageParent">Where the page goes: the settings' scroll area.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes.</param>
        public static GameObject AddHotkeysPage(MenuSource hotkeys, List<MenuSource> sections, Transform pageParent, Transform dropdownLayer)
        {
            GameObject page = AddMenuPage(hotkeys, pageParent, dropdownLayer);
            Transform parentTransform = page.transform;
            foreach (MenuSource section in sections)
            {
                GameObject heading = AddMenuText(SectionTitle(section), parentTransform, SECTION_SIZE, mainScript.black32, TextAlignmentOptions.BottomLeft);
                heading.name = SECTION_OBJ_NAME + "_" + section.Mod.ModName;
                ((RectTransform)heading.transform).sizeDelta = new Vector2(200, SECTION_HEIGHT);
                ModMenuSection rows = heading.AddComponent<ModMenuSection>();
                rows.rows = new List<GameObject>();

                JSONArray items = section.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    GameObject row = AddMenuRow(items[i], parentTransform, dropdownLayer);
                    if (row == null)
                    {
                        continue;
                    }
                    ModMenuItem key = row.GetComponent<ModMenuItem>();
                    if (key != null && key.keybindBox != null)
                    {
                        key.keybindMod = section.Title;
                        rows.rows.Add(row);
                    }
                }
            }
            return page;
        }

        /// <summary>
        /// Adds a key row: its label, a box showing the key that waits for a new one when clicked, a button that
        /// clears it, and a line under them, shown only while another row has the same key.
        /// </summary>
        /// <param name="varID">The variable the key is saved in.</param>
        /// <param name="labelID">The label's text ID.</param>
        /// <param name="def">The default key, in its saved form (see FormatKeybind).</param>
        /// <param name="parentTransform">Where the row goes.</param>
        public static GameObject AddMenuKeybind(string varID, string labelID, string def, Transform parentTransform)
        {
            string labelText = ShownText(labelID);

            GameObject modKeybind = new(MENU_KEYBIND_OBJ_NAME + "_" + varID, typeof(RectTransform));
            modKeybind.transform.SetParent(parentTransform, false);
            RectTransform modKeybindRect = modKeybind.GetComponent<RectTransform>();
            SetRectTransform(modKeybindRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modKeybindRect.sizeDelta = new Vector2(400f, LABEL_HEIGHT + BOX_HEIGHT);

            // The box looks like a text field's, but is clicked rather than typed in
            GameObject box = UnityEngine.Object.Instantiate(VanillaTextField(), modKeybind.transform);
            box.name = KEYBIND_BOX_OBJ_NAME;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<InputField_Tab>());
            UnityEngine.Object.DestroyImmediate(box.GetComponent<TMP_InputField>());
            RemoveCaret(box);
            TextMeshProUGUI title = PlaceTextBox(modKeybind, box, labelID, labelText);
            TextMeshProUGUI keyText = box.transform.Find("Container").Find("Text").GetComponent<TextMeshProUGUI>();

            ModMenuItem item = modKeybind.AddComponent<ModMenuItem>();
            item.varID = varID;
            item.defText = def;
            item.tempText = NormalizeKeybind(variables.Get(varID)) ?? def;
            item.keybindLabel = labelText;
            item.keybindText = keyText;

            Button boxButton = box.AddComponent<Button>();
            boxButton.onClick = new Button.ButtonClickedEvent();
            boxButton.onClick.AddListener(item.onClickKeybind);
            item.keybindBox = boxButton;

            // Clear: a ×, beside the box
            GameObject clear = new(KEYBIND_CLEAR_OBJ_NAME, typeof(RectTransform), typeof(Image), typeof(Button));
            clear.transform.SetParent(modKeybind.transform, false);
            SetRectTransform((RectTransform)clear.transform, Vector2.up, Vector2.up, new Vector2(BOX_WIDTH + CLEAR_GAP, -LABEL_HEIGHT - BOX_HEIGHT), new Vector2(BOX_WIDTH + CLEAR_GAP + BOX_HEIGHT, -LABEL_HEIGHT), Vector2.up);
            // Clicks land on it, but it draws only its ×
            clear.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            // The × fills its button exactly and takes no clicks itself, so it can't cover the box beside it
            GameObject cross = AddMenuText("×", clear.transform, TITLE_SIZE, mainScript.blue32, TextAlignmentOptions.Center);
            SetRectTransform((RectTransform)cross.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cross.GetComponent<TextMeshProUGUI>().raycastTarget = false;
            Button clearButton = clear.GetComponent<Button>();
            clearButton.onClick = new Button.ButtonClickedEvent();
            clearButton.onClick.AddListener(item.onClearKeybind);
            item.keybindClear = clearButton;

            // The rows using the same key, under the box: hidden until there are some
            GameObject clash = AddMenuText(ALSO_USED_BY_LABEL, modKeybind.transform, TEXT_SIZE, mainScript.red32, TextAlignmentOptions.Left);
            clash.name = KEYBIND_CLASH_OBJ_NAME;
            SetRectTransform((RectTransform)clash.transform, Vector2.up, Vector2.one, new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT - WARNING_HEIGHT), new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT));
            TextMeshProUGUI clashText = clash.GetComponent<TextMeshProUGUI>();
            clashText.text = "";
            clashText.raycastTarget = false;
            clash.SetActive(false);
            item.keybindClash = clashText;

            item.RenderKeybind();
            AddLabelWrap(modKeybind, title, null, (RectTransform)box.transform, (RectTransform)clear.transform, (RectTransform)clash.transform);

            // Many rows with the key make a long line: it wraps like a label, onto CLASH_MAX_LINES, then ends in "…"
            LabelWrap clashWrap = modKeybind.AddComponent<LabelWrap>();
            clashWrap.label = clashText;
            clashWrap.below = new RectTransform[0];
            clashWrap.maxLines = CLASH_MAX_LINES;

            return modKeybind;
        }

        /// <summary>
        /// The game's text field draws a blinking caret once it has been on; a key's box takes no typing.
        /// </summary>
        private static void RemoveCaret(GameObject box)
        {
            TMP_SelectionCaret caret = box.GetComponentInChildren<TMP_SelectionCaret>(true);
            if (caret != null)
            {
                UnityEngine.Object.Destroy(caret.gameObject);
            }
        }

        /// <summary>
        /// Makes a row taller or shorter and has its page laid out again.
        /// </summary>
        private static void GrowRow(RectTransform row, float delta)
        {
            row.sizeDelta += new Vector2(0, delta);
            if (row.parent != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)row.parent);
            }
        }

        /// <summary>
        /// A clicked button stays selected, and Enter or Space would click it again: they couldn't be picked as keys.
        /// </summary>
        private static void Deselect()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// A mod's heading on the Hotkeys tab: hidden while every key row under it is hidden by enabledIf.
        /// </summary>
        public class ModMenuSection : MonoBehaviour
        {
            /// <summary>The mod's key rows.</summary>
            public List<GameObject> rows;
        }

        public partial class ModMenuManager
        {
            /// <summary>
            /// Marks each shown key row whose key another shown row also has, naming the other rows. Rows hidden by
            /// enabledIf don't count.
            /// </summary>
            public void RefreshClashes()
            {
                List<ModMenuItem> keys = new();
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    if (item.keybindBox != null)
                    {
                        keys.Add(item);
                    }
                }
                if (keys.Count == 0)
                {
                    return;
                }

                List<KeyValuePair<string, string>> shown = new();
                Dictionary<string, ModMenuItem> byVarID = new();
                foreach (ModMenuItem item in keys)
                {
                    if (item.gameObject.activeSelf && !byVarID.ContainsKey(item.varID))
                    {
                        shown.Add(new KeyValuePair<string, string>(item.varID, item.tempText));
                        byVarID[item.varID] = item;
                    }
                }

                Dictionary<string, List<string>> clashes = FindClashes(shown, GameKeys.Contexts.Clash);
                foreach (ModMenuItem item in keys)
                {
                    string others = null;
                    if (byVarID.TryGetValue(item.varID, out ModMenuItem owner) && ReferenceEquals(owner, item) && clashes.TryGetValue(item.varID, out List<string> clashing))
                    {
                        List<string> names = new();
                        foreach (string varID in clashing)
                        {
                            // A row of the same mod is named by its label alone, keeping the line short
                            ModMenuItem other = byVarID[varID];
                            names.Add(other.keybindMod == item.keybindMod ? other.keybindLabel : other.KeybindTitle);
                        }
                        others = string.Join(", ", names.ToArray());
                    }
                    item.ShowClash(others);
                }
            }

            /// <summary>
            /// Hides each mod's heading on the Hotkeys tab while all its key rows are hidden.
            /// </summary>
            private void RefreshSections()
            {
                foreach (ModMenuSection section in GetComponentsInChildren<ModMenuSection>(true))
                {
                    bool any = false;
                    foreach (GameObject row in section.rows)
                    {
                        if (row != null && row.activeSelf)
                        {
                            any = true;
                            break;
                        }
                    }
                    section.gameObject.SetActive(any);
                }
            }
        }
    }
}
