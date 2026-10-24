using System;
using System.Text;
using UnityEngine;

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
    }
}
