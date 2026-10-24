using System;
using UnityEngine;
using SimpleJSON;
using System.IO;
using System.Globalization;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Saves the default of every setting in the enabled mods' menus that has no saved value yet.
        /// Saved values are never changed.
        /// </summary>
        public static void SaveMissingDefaults()
        {
            // The enabled mods may have changed since the keys' defaults were read
            ForgetKeybindDefaults();

            // This runs while a game is starting or loading, so nothing may escape into the game's own code
            try
            {
                foreach (MenuSource menu in FindMenus())
                {
                    JSONArray settings = menu.Items;
                    for (int i = 0; i < settings.Count; i++)
                    {
                        if (!TryGetDefault(settings[i], out string varID, out string value) || variables.Get(varID) != null)
                            continue;

                        // Not variables.Set: it also logs every setting and raises the game's task events,
                        // which may not be ready while the scene is starting.
                        variables.variable.Add(new variables._variable { name = varID, value = value });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ModMenus] Couldn't save the settings' defaults: " + e);
            }
        }

        /// <summary>
        /// The value a setting starts at, written as Apply would save it. Only settings the menu shows have one:
        /// text, ignored items, items missing their varID or labelID, sliders whose default is outside their
        /// range and dropdowns without items are left out, as they are from the menu. A slider's default moves
        /// to its nearest step, and a number field's is checked as typing is.
        /// </summary>
        public static bool TryGetDefault(JSONNode item, out string varID, out string value)
        {
            varID = item?[JSON_FIELD_VARID];
            value = null;
            if (item == null
                || string.IsNullOrEmpty(varID)
                || string.IsNullOrEmpty(item[JSON_FIELD_LABELID])
                || item[JSON_FIELD_IGNORE].AsBool)
            {
                return false;
            }

            bool hasDefault = !string.IsNullOrEmpty(item[JSON_FIELD_DEF]);
            switch ((string)item[JSON_FIELD_TYPE])
            {
                case JSON_TYPE_SLIDER:
                    GetSliderRange(item, out float min, out float max, out float def);
                    if (max < min || def < min || def > max)
                        return false;
                    TryGetSliderStep(item, out int step);
                    value = FormatSetting(SnapToStep(def, min, max, step));
                    return true;

                case JSON_TYPE_CHECKBOX:
                    value = hasDefault && item[JSON_FIELD_DEF].AsBool ? "1" : "0";
                    return true;

                case JSON_TYPE_DROPDOWN:
                    JSONArray items = item[JSON_FIELD_LIST].AsArray;
                    if (items == null || items.Count == 0)
                        return false;
                    int choice = hasDefault ? item[JSON_FIELD_DEF].AsInt : 0;
                    value = FormatSetting(Mathf.Clamp(choice, 0, items.Count - 1));
                    return true;

                case JSON_TYPE_INPUT:
                    value = GetNumberField(item, out string inputType, out float? low, out float? high)
                        ? NumberDefault(GetInputDefault(item), inputType == JSON_INPUT_INTEGER, low, high)
                        : GetInputDefault(item);
                    return true;

                case JSON_TYPE_KEYBIND:
                    value = GetKeybindDefault(item);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads a saved setting. Settings are saved the same way whatever the player's language, so a
        /// comma-decimal language doesn't misread them.
        /// </summary>
        public static bool TryParseSetting(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Writes a setting as plain digits ("7", "-2", "10000000"), never as "1E+07" or with a comma.
        /// </summary>
        public static string FormatSetting(float value)
        {
            return ((double)value).ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a mod's modmenu.json. A file that can't be read or isn't a list of settings is left out
        /// with a warning, so one mod's broken file doesn't break the menu for every mod.
        /// </summary>
        /// <returns>The file's settings, or null if it's left out.</returns>
        public static JSONArray ReadMenuFile(string modTitle, string filepath)
        {
            JSONNode root;
            try
            {
                root = JSON.Parse(File.ReadAllText(filepath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] " + modTitle + "'s " + JSON_FILE + " couldn't be read, so its settings are left out: " + e.Message);
                return null;
            }

            JSONArray settings = root?.AsArray;
            if (settings == null)
            {
                Debug.LogWarning("[ModMenus] " + modTitle + "'s " + JSON_FILE + " isn't a list of settings ([ ... ]), so its settings are left out.");
            }
            return settings;
        }
    }
}
