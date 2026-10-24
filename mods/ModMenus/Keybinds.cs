using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModMenus
{
    /// <summary>
    /// Key rows ("type": "keybind"): a key the player picks by pressing it, saved by name ("F5", "Ctrl+F",
    /// "Mouse2", "None"). Every mod's keys are on one tab, Hotkeys, where a key used by two rows is marked on
    /// both. Mods read their keys with KeybindPressed, KeybindHeld and KeybindReleased.
    /// </summary>
    partial class ModMenusUtils
    {
        public const string JSON_TYPE_KEYBIND = "keybind";

        /// <summary>The saved value of a key row with no key.</summary>
        public const string KEY_NONE = "None";

        public const string MENU_KEYBIND_OBJ_NAME = "ModMenuKeybind";
        public const string KEYBIND_BOX_OBJ_NAME = "Box";
        public const string KEYBIND_CLEAR_OBJ_NAME = "Clear";
        public const string KEYBIND_CLASH_OBJ_NAME = "Clash";
        public const string SECTION_OBJ_NAME = "ModMenuSection";

        /// <summary>The Hotkeys tab's name in lastModName; no mod has it, as it has no dots.</summary>
        public const string HOTKEYS_TAB_ID = "Hotkeys";
        public const string HOTKEYS_LABEL = "MODMENU__HOTKEYS";
        public const string PRESS_A_KEY_LABEL = "MODMENU__PRESS_A_KEY";
        public const string KEY_NONE_LABEL = "MODMENU__KEY_NONE";
        public const string ALSO_USED_BY_LABEL = "MODMENU__ALSO_USED_BY";

        /// <summary>The heading of ModMenus' own rows on the Hotkeys tab: the game's name (see IsOwnMenu).</summary>
        public const string GAME_KEYS_LABEL = "MODMENU__GAME_KEYS";

        // A mod's heading on the Hotkeys tab, between a page title and a label
        public const float SECTION_SIZE = 17;
        public const float SECTION_HEIGHT = 30;
        // The clear button beside a key's box, and the line under it naming the rows that use the same key
        public const float CLEAR_GAP = 4;
        public const float WARNING_HEIGHT = 20;
        public const int CLASH_MAX_LINES = 2;

        /// <summary>
        /// The keys held with a key: Ctrl, Shift and Alt, either side.
        /// </summary>
        [Flags]
        public enum KeyModifiers
        {
            None = 0,
            Ctrl = 1,
            Shift = 2,
            Alt = 4,
        }

        /// <summary>
        /// A key row's key: a key or mouse button, and the modifiers held with it.
        /// </summary>
        public struct Keybind : IEquatable<Keybind>
        {
            public KeyCode Key;
            public KeyModifiers Modifiers;

            public Keybind(KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
            {
                Key = key;
                Modifiers = modifiers;
            }

            public bool Equals(Keybind other) => Key == other.Key && Modifiers == other.Modifiers;
            public override bool Equals(object obj) => obj is Keybind other && Equals(other);
            public override int GetHashCode() => ((int)Key * 8) + (int)Modifiers;
        }

        private static readonly KeyModifiers[] ModifierOrder = { KeyModifiers.Ctrl, KeyModifiers.Shift, KeyModifiers.Alt };

        /// <summary>
        /// Reads a key's saved name: a KeyCode name (any case), after any of Ctrl, Shift and Alt joined by "+",
        /// or "None" (or empty) for no key.
        /// </summary>
        /// <returns>False if it isn't a key.</returns>
        public static bool TryParseKeybind(string text, out Keybind keybind)
        {
            keybind = new Keybind(KeyCode.None);
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, KEY_NONE, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] parts = trimmed.Split('+');
            KeyModifiers modifiers = KeyModifiers.None;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= KeyModifiers.Ctrl;
                        break;
                    case "shift":
                        modifiers |= KeyModifiers.Shift;
                        break;
                    case "alt":
                        modifiers |= KeyModifiers.Alt;
                        break;
                    default:
                        return false;
                }
            }

            string name = parts[parts.Length - 1].Trim();
            // Enum.TryParse also takes numbers, which aren't key names
            if (name.Length == 0 || char.IsDigit(name[0]) || name[0] == '-'
                || !Enum.TryParse(name, true, out KeyCode key) || !Enum.IsDefined(typeof(KeyCode), key) || key == KeyCode.None)
            {
                return false;
            }
            keybind = new Keybind(key, modifiers);
            return true;
        }

        /// <summary>
        /// A key's saved name: "None", or the modifiers in the order Ctrl, Shift, Alt, then the KeyCode name
        /// ("Ctrl+Shift+F", "Alpha4", "Mouse2").
        /// </summary>
        public static string FormatKeybind(Keybind keybind)
        {
            if (keybind.Key == KeyCode.None)
            {
                return KEY_NONE;
            }
            StringBuilder text = new();
            foreach (KeyModifiers modifier in ModifierOrder)
            {
                if ((keybind.Modifiers & modifier) != 0)
                {
                    text.Append(modifier).Append('+');
                }
            }
            return text.Append(keybind.Key).ToString();
        }

        /// <summary>
        /// A saved key as its name is written in the same form, or null if it isn't a key or nothing is saved (null).
        /// </summary>
        public static string NormalizeKeybind(string text)
        {
            return text != null && TryParseKeybind(text, out Keybind keybind) ? FormatKeybind(keybind) : null;
        }

        /// <summary>
        /// A saved key as the player reads it: "Ctrl+F", "4", "Middle mouse", "Page Down"; "Not set" for none.
        /// Key names are the keyboard's, so they aren't translated.
        /// </summary>
        public static string KeybindName(string saved)
        {
            if (!TryParseKeybind(saved, out Keybind keybind))
            {
                return saved ?? "";
            }
            if (keybind.Key == KeyCode.None)
            {
                return ShownText(KEY_NONE_LABEL);
            }
            StringBuilder text = new();
            foreach (KeyModifiers modifier in ModifierOrder)
            {
                if ((keybind.Modifiers & modifier) != 0)
                {
                    text.Append(modifier).Append('+');
                }
            }
            return text.Append(KeyName(keybind.Key)).ToString();
        }

        /// <summary>
        /// A key's name as printed on the keyboard or the mouse.
        /// </summary>
        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)(key - KeyCode.Alpha0)).ToString();
            }
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "Num " + (int)(key - KeyCode.Keypad0);
            }
            if (key >= KeyCode.Mouse3 && key <= KeyCode.Mouse6)
            {
                return "Mouse " + (int)(key - KeyCode.Mouse0 + 1);
            }
            switch (key)
            {
                case KeyCode.Mouse0: return "Left mouse";
                case KeyCode.Mouse1: return "Right mouse";
                case KeyCode.Mouse2: return "Middle mouse";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.KeypadPeriod: return "Num .";
                case KeyCode.KeypadDivide: return "Num /";
                case KeyCode.KeypadMultiply: return "Num *";
                case KeyCode.KeypadMinus: return "Num -";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadEquals: return "Num =";
                case KeyCode.Escape: return "Esc";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
            }

            // "PageDown" reads as "Page Down"
            string name = key.ToString();
            StringBuilder spaced = new();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                {
                    spaced.Append(' ');
                }
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }

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

        // The keys each key row's default and saved key, read at most once a frame (see RefreshKeybinds)
        private static Dictionary<string, Keybind> keybindDefaults;
        private static int keybindModCount = -1;
        private static readonly Dictionary<string, Keybind> keybinds = new();
        private static readonly HashSet<Keybind> combos = new();
        private static KeyModifiers heldModifiers;
        private static int keybindFrame = -1;

        // After a key row takes a key, the key is still down: no row reacts to it until every key is let go
        private static bool keysSuppressed;
        private static bool keysReleased;

        /// <summary>
        /// Whether keys are being picked: the Mod Settings menu is open, or a key row took a key that is still
        /// held after it closed. KeybindPressed, KeybindHeld and KeybindReleased return false meanwhile, so a key
        /// pressed to pick it (F9, say) doesn't also do what it's for.
        /// </summary>
        public static bool Capturing => ModMenuManager.open is not null || ModMenuItem.listening is not null || keysSuppressed;

        /// <summary>
        /// Whether the key saved for a key row was pressed this frame, with exactly its modifiers held. A key
        /// without modifiers is pressed unless the modifiers held make it another row's key ("Ctrl+F" isn't
        /// also "F"). With no game loaded, or nothing saved, it's the row's default. False for a varID no key
        /// row has, a row with no key, and while keys are being picked (see Capturing).
        /// </summary>
        public static bool KeybindPressed(string varID) => KeybindHappened(varID, 0);

        /// <summary>
        /// Whether the key saved for a key row is held down (see KeybindPressed).
        /// </summary>
        public static bool KeybindHeld(string varID) => KeybindHappened(varID, 1);

        /// <summary>
        /// Whether the key saved for a key row was let go this frame (see KeybindPressed).
        /// </summary>
        public static bool KeybindReleased(string varID) => KeybindHappened(varID, 2);

        /// <summary>
        /// Whether an enabled mod's menu has a key row with this varID. Without one, KeybindPressed and the others
        /// are always false, so a mod can fall back to its own key.
        /// </summary>
        public static bool HasKeybind(string varID)
        {
            if (varID == null)
            {
                return false;
            }
            RefreshKeybinds();
            return keybinds.ContainsKey(varID);
        }

        private static bool KeybindHappened(string varID, int kind)
        {
            if (varID == null)
            {
                return false;
            }
            RefreshKeybinds();
            if (Capturing || !keybinds.TryGetValue(varID, out Keybind keybind) || keybind.Key == KeyCode.None)
            {
                return false;
            }
            bool happened = kind switch
            {
                0 => Input.GetKeyDown(keybind.Key),
                1 => Input.GetKey(keybind.Key),
                _ => Input.GetKeyUp(keybind.Key),
            };
            return happened && ModifiersMatch(keybind, heldModifiers, combos);
        }

        /// <summary>
        /// Whether the modifiers held go with a key: exactly its own, or, for a key without modifiers, any that
        /// don't make it another key row's key.
        /// </summary>
        public static bool ModifiersMatch(Keybind keybind, KeyModifiers held, ICollection<Keybind> combos)
        {
            if (keybind.Modifiers != KeyModifiers.None)
            {
                return held == keybind.Modifiers;
            }
            return held == KeyModifiers.None || !combos.Contains(new Keybind(keybind.Key, held));
        }

        /// <summary>
        /// The keys the menus' key rows have, read again when the enabled mods may have changed (a game starts
        /// or loads).
        /// </summary>
        public static void ForgetKeybindDefaults()
        {
            keybindDefaults = null;
            keybindFrame = -1;
        }

        /// <summary>
        /// Reads every key row's key once a frame: its saved key, or its default if none is saved or the saved
        /// one isn't a key; and which modifiers are held.
        /// </summary>
        private static void RefreshKeybinds()
        {
            int frame = Time.frameCount;
            if (frame == keybindFrame)
            {
                return;
            }
            keybindFrame = frame;

            if (keybindDefaults == null || keybindModCount != Mods._Mods.Count)
            {
                keybindDefaults = ReadKeybindDefaults();
                keybindModCount = Mods._Mods.Count;
            }

            keybinds.Clear();
            foreach (KeyValuePair<string, Keybind> row in keybindDefaults)
            {
                keybinds[row.Key] = row.Value;
            }
            foreach (variables._variable saved in variables.variable)
            {
                if (saved != null && saved.name != null && keybindDefaults.ContainsKey(saved.name) && TryParseKeybind(saved.value, out Keybind keybind))
                {
                    keybinds[saved.name] = keybind;
                }
            }

            combos.Clear();
            foreach (Keybind keybind in keybinds.Values)
            {
                if (keybind.Modifiers != KeyModifiers.None)
                {
                    combos.Add(keybind);
                }
            }

            heldModifiers = HeldModifiers();

            // Let go for a whole frame first: a key read as released (GetKeyUp) is no longer down that frame
            if (keysSuppressed)
            {
                if (AnyKeyHeld())
                {
                    keysReleased = false;
                }
                else if (!keysReleased)
                {
                    keysReleased = true;
                }
                else
                {
                    keysSuppressed = false;
                }
            }
        }

        /// <summary>
        /// Every key row's default, by varID, from the enabled mods' menu files. The first row with a varID wins.
        /// </summary>
        private static Dictionary<string, Keybind> ReadKeybindDefaults()
        {
            Dictionary<string, Keybind> defaults = new();
            try
            {
                foreach (MenuSource menu in FindMenus())
                {
                    for (int i = 0; i < menu.Items.Count; i++)
                    {
                        JSONNode item = menu.Items[i];
                        if (item[JSON_FIELD_TYPE].Value == JSON_TYPE_KEYBIND && TryGetDefault(item, out string varID, out string value)
                            && !defaults.ContainsKey(varID) && TryParseKeybind(value, out Keybind keybind))
                        {
                            defaults[varID] = keybind;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ModMenus] Couldn't read the keys' defaults: " + e);
            }
            return defaults;
        }

        /// <summary>
        /// The modifiers held now.
        /// </summary>
        private static KeyModifiers HeldModifiers()
        {
            KeyModifiers held = KeyModifiers.None;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                held |= KeyModifiers.Ctrl;
            }
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                held |= KeyModifiers.Shift;
            }
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                held |= KeyModifiers.Alt;
            }
            return held;
        }

        /// <summary>
        /// Whether any key a key row can take is held. The game's Unity has no Input.anyKey.
        /// </summary>
        private static bool AnyKeyHeld()
        {
            foreach (KeyCode key in CaptureKeys)
            {
                if (Input.GetKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Stops key rows from reacting to the key just taken until every key is let go.
        /// </summary>
        internal static void SuppressKeybindsUntilReleased()
        {
            keysSuppressed = true;
            keysReleased = false;
        }

        /// <summary>
        /// Whether a key, pressed alone, is a modifier: it's taken as a key only when let go with no other key pressed.
        /// </summary>
        public static bool IsModifierKey(KeyCode key)
        {
            return key == KeyCode.LeftControl || key == KeyCode.RightControl
                || key == KeyCode.LeftShift || key == KeyCode.RightShift
                || key == KeyCode.LeftAlt || key == KeyCode.RightAlt;
        }

        /// <summary>
        /// The keys a key row can take: every key and mouse button but the left one, which picks things; not
        /// game controllers.
        /// </summary>
        public static readonly KeyCode[] CaptureKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
            .Where(key => key != KeyCode.None && key != KeyCode.Mouse0 && key < KeyCode.JoystickButton0)
            .Distinct()
            .ToArray();

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

        public partial class ModMenuItem
        {
            /// <summary>The key row waiting for a key, or null.</summary>
            internal static ModMenuItem listening;

            /// <summary>
            /// A key row's controls, set when it's built; null for every other item.
            /// </summary>
            public Button keybindBox;
            public TextMeshProUGUI keybindText;
            public Button keybindClear;
            public TextMeshProUGUI keybindClash;

            /// <summary>A key row's label, and the mod it's under, to name it when another row has its key.</summary>
            public string keybindLabel;
            public string keybindMod;

            /// <summary>The frame the row began waiting for a key: the click that began it isn't a click elsewhere.</summary>
            private int listenFrame;

            /// <summary>A modifier pressed while waiting, taken alone if it's let go before another key is pressed.</summary>
            private KeyCode pendingModifier;

            /// <summary>Whether the line naming the rows with the same key is shown.</summary>
            private bool clashShown;

            /// <summary>The row named in another row's clash line: its label, and its mod's name.</summary>
            public string KeybindTitle => string.IsNullOrEmpty(keybindMod) ? keybindLabel : keybindLabel + " (" + keybindMod + ")";

            /// <summary>
            /// The key saved for the row, in its saved form, or its default if none is saved or it isn't a key.
            /// </summary>
            public string SavedKeybind()
            {
                return NormalizeKeybind(variables.Get(varID)) ?? defText;
            }

            /// <summary>
            /// Shows a key and keeps it until it's changed; a row waiting for a key stops waiting.
            /// </summary>
            public void ShowKeybind(string value)
            {
                if (ReferenceEquals(listening, this))
                {
                    listening = null;
                }
                tempText = value;
                RenderKeybind();
            }

            /// <summary>
            /// Shows the row's key, or that it's waiting for one.
            /// </summary>
            public void RenderKeybind()
            {
                keybindText.text = ReferenceEquals(listening, this) ? ShownText(PRESS_A_KEY_LABEL) : KeybindName(tempText);
            }

            /// <summary>
            /// Clicking the box makes the row wait for a key; clicking it again stops it waiting.
            /// </summary>
            public void onClickKeybind()
            {
                Deselect();
                if (ReferenceEquals(listening, this))
                {
                    StopListening(false);
                    return;
                }
                if (listening is not null)
                {
                    listening.StopListening(false);
                }
                listening = this;
                listenFrame = Time.frameCount;
                pendingModifier = KeyCode.None;
                RenderKeybind();
            }

            /// <summary>
            /// The × beside the box: the row has no key.
            /// </summary>
            public void onClearKeybind()
            {
                Deselect();
                ShowKeybind(KEY_NONE);
                Changed();
            }

            /// <summary>
            /// Stops the row waiting for a key, if it is.
            /// </summary>
            /// <param name="suppress">Whether a key was just taken, which no row may react to until it's let go.</param>
            public void StopListening(bool suppress)
            {
                if (ReferenceEquals(listening, this))
                {
                    listening = null;
                }
                if (suppress)
                {
                    SuppressKeybindsUntilReleased();
                }
                RenderKeybind();
            }

            /// <summary>
            /// Stops any row waiting for a key: the menu is closing.
            /// </summary>
            public static void StopAnyListening()
            {
                if (listening is not null)
                {
                    listening.StopListening(false);
                }
            }

            /// <summary>
            /// While the row waits, takes the first key pressed, with the modifiers held; a modifier let go with no
            /// other key pressed is taken alone. A left click anywhere but the box stops it waiting.
            /// </summary>
            public void PollKeybind()
            {
                if (!ReferenceEquals(listening, this))
                {
                    return;
                }
                if (Input.GetKeyUp(KeyCode.Mouse0) && Time.frameCount != listenFrame)
                {
                    StopListening(false);
                    return;
                }
                foreach (KeyCode key in CaptureKeys)
                {
                    if (!Input.GetKeyDown(key))
                    {
                        continue;
                    }
                    if (IsModifierKey(key))
                    {
                        pendingModifier = key;
                        continue;
                    }
                    TakeKey(new Keybind(key, HeldModifiers()));
                    return;
                }
                if (pendingModifier != KeyCode.None && Input.GetKeyUp(pendingModifier))
                {
                    TakeKey(new Keybind(pendingModifier));
                }
            }

            private void TakeKey(Keybind keybind)
            {
                tempText = FormatKeybind(keybind);
                StopListening(true);
                Changed();
            }

            /// <summary>
            /// Shows the rows that have the same key under the box, or hides the line if there are none. The row
            /// grows by the line while it's shown, and by the lines it wraps onto.
            /// </summary>
            /// <param name="others">The other rows' names, or null.</param>
            public void ShowClash(string others)
            {
                if (keybindClash == null)
                {
                    return;
                }
                // A hidden line is empty, so its wrapping adds nothing to the row (see LabelWrap)
                string format = ShownText(ALSO_USED_BY_LABEL);
                keybindClash.text = others == null ? "" : format.Contains("{0}") ? format.Replace("{0}", others) : format + " " + others;
                if ((others != null) == clashShown)
                {
                    return;
                }
                clashShown = others != null;
                keybindClash.gameObject.SetActive(clashShown);
                GrowRow((RectTransform)transform, clashShown ? WARNING_HEIGHT : -WARNING_HEIGHT);
            }
        }
    }
}
