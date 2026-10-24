using System;
using UnityEngine;
using SimpleJSON;
using System.Collections.Generic;
using System.Globalization;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Reads a slider's range and default from its JSON item. The range is 0-100 unless
        /// both minValue and maxValue are set; the default is the midpoint unless defaultValue is set.
        /// </summary>
        public static void GetSliderRange(JSONNode item, out float minValue, out float maxValue, out float defaultValue)
        {
            minValue = 0;
            maxValue = 100;
            if (!string.IsNullOrEmpty(item[JSON_FIELD_MIN]) && !string.IsNullOrEmpty(item[JSON_FIELD_MAX]))
            {
                minValue = item[JSON_FIELD_MIN].AsFloat;
                maxValue = item[JSON_FIELD_MAX].AsFloat;
            }
            defaultValue = (maxValue + minValue) / 2f;
            if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
            {
                defaultValue = item[JSON_FIELD_DEF].AsFloat;
            }
        }

        /// <summary>
        /// Reads a slider's step from its JSON item: 1 unless step is set. A step that isn't a whole number of
        /// at least 1 is 1.
        /// </summary>
        /// <returns>False if step is set but isn't a whole number of at least 1.</returns>
        public static bool TryGetSliderStep(JSONNode item, out int step)
        {
            step = 1;
            if (string.IsNullOrEmpty(item[JSON_FIELD_STEP]))
            {
                return true;
            }
            float written = item[JSON_FIELD_STEP].AsFloat;
            if (written < 1 || written != Mathf.Floor(written))
            {
                return false;
            }
            step = (int)written;
            return true;
        }

        /// <summary>
        /// The value a slider with a step saves: the nearest of min, min + step, min + 2 × step and so on up to
        /// max, or max itself, so max can be picked even when the range isn't a whole number of steps. Halfway
        /// goes up. With step 1 it's the whole number nearest the value, as sliders have always saved.
        /// </summary>
        public static float SnapToStep(float value, float min, float max, int step)
        {
            float clamped = Mathf.Clamp(value, min, max);
            if (step <= 1)
            {
                return Mathf.Round(clamped);
            }
            float below = min + Mathf.Floor((clamped - min) / step) * step;
            float above = Mathf.Min(below + step, max);
            return Mathf.Round(clamped - below < above - clamped ? below : above);
        }

        /// <summary>
        /// Reads a text field's default from its JSON item: defaultValue as written, or empty if it isn't set.
        /// </summary>
        public static string GetInputDefault(JSONNode item)
        {
            return string.IsNullOrEmpty(item[JSON_FIELD_DEF]) ? "" : (string)item[JSON_FIELD_DEF];
        }

        /// <summary>
        /// Reads what a text field takes from its JSON item: any text, or, with inputType "integer" or
        /// "decimal", a number, between minValue and maxValue where they're set. Each limit works on its own.
        /// </summary>
        /// <param name="inputType">"integer" or "decimal"; null for any text.</param>
        /// <returns>Whether the field takes a number.</returns>
        public static bool GetNumberField(JSONNode item, out string inputType, out float? min, out float? max)
        {
            string type = item[JSON_FIELD_INPUTTYPE];
            inputType = type == JSON_INPUT_INTEGER || type == JSON_INPUT_DECIMAL ? type : null;
            min = string.IsNullOrEmpty(item[JSON_FIELD_MIN]) ? null : item[JSON_FIELD_MIN].AsFloat;
            max = string.IsNullOrEmpty(item[JSON_FIELD_MAX]) ? null : item[JSON_FIELD_MAX].AsFloat;
            return inputType != null;
        }

        /// <summary>
        /// What a number field keeps of what was typed: the number, rounded to a whole number in an integer
        /// field, and moved inside its limits; written as settings are saved ("7", "-2", "0.5"). A comma counts
        /// as the decimal point. Text that isn't a number gives the fallback.
        /// </summary>
        public static string ValidNumber(string typed, bool integer, float? min, float? max, string fallback)
        {
            string text = (typed ?? "").Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            double low = min ?? double.MinValue;
            double high = max ?? double.MaxValue;
            if (integer)
            {
                // The limits move in to whole numbers, so the result is both whole and inside them
                value = Math.Round(value);
                low = Math.Ceiling(low);
                high = Math.Floor(high);
            }
            if (value < low)
            {
                value = low;
            }
            else if (value > high)
            {
                value = high;
            }
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A number field's default: its defaultValue, checked as typing is; or, if that isn't a number,
        /// 0 moved inside its limits.
        /// </summary>
        public static string NumberDefault(string def, bool integer, float? min, float? max)
        {
            return ValidNumber(def, integer, min, max, ValidNumber("0", integer, min, max, "0"));
        }

        /// <summary>
        /// Whether a setting is on, for enabledIf: its value isn't missing, empty or a number equal to 0. So a
        /// ticked checkbox, a slider above 0, a dropdown on any item but the first, or a text field with text.
        /// </summary>
        public static bool IsSettingOn(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            return !TryParseSetting(value, out float number) || number != 0;
        }

        /// <summary>
        /// Whether a row with enabledIf: varID is shown: the setting is on, and its own row, if it has an
        /// enabledIf too, is shown. Settings that lead back to one already being checked form a loop, and the
        /// loop's link counts as met.
        /// </summary>
        /// <param name="valueOf">A setting's value: its row's, if the menu has one, or the saved value.</param>
        /// <param name="conditionOf">The enabledIf of a setting's row, or null if it has none.</param>
        /// <param name="checking">The settings already being checked, which this adds to and takes from.</param>
        /// <param name="loop">Set to true if a loop was found.</param>
        public static bool ConditionMet(string varID, Func<string, string> valueOf, Func<string, string> conditionOf, HashSet<string> checking, ref bool loop)
        {
            if (!checking.Add(varID))
            {
                loop = true;
                return true;
            }
            try
            {
                string parent = conditionOf(varID);
                if (!string.IsNullOrEmpty(parent) && !ConditionMet(parent, valueOf, conditionOf, checking, ref loop))
                {
                    return false;
                }
                return IsSettingOn(valueOf(varID));
            }
            finally
            {
                checking.Remove(varID);
            }
        }

        /// <summary>
        /// What the open mod menu shows for a variable, applied or not, on any tab: the text Apply would save,
        /// so a checkbox's 1 or 0 and a dropdown's item counted from 0. Null while the menu isn't open, or if no
        /// row has the variable. A mod can read the player's choices before Apply with it, for example to work
        /// out a variable that a row's enabledIf names.
        /// </summary>
        public static string MenuValue(string varID)
        {
            ModMenuManager menu = ModMenuManager.open;
            if (menu == null || varID == null)
            {
                return null;
            }
            foreach (ModMenuItem item in menu.GetComponentsInChildren<ModMenuItem>(true))
            {
                if (item.varID == varID)
                {
                    return item.CurrentValue;
                }
            }
            return null;
        }
    }
}
