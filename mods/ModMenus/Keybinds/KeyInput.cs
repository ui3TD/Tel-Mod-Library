using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using UnityEngine;

namespace ModMenus
{
    partial class ModMenusUtils
    {
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
    }
}
