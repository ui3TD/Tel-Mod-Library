using HarmonyLib;
using UnityEngine.UI;
using UnityEngine;
using static CustomAuditions.CustomAuditions;
using System.Collections.Generic;

namespace CustomAuditions
{
    /// <summary>
    /// Patches the Staff_Nickname class to handle age input rendering.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Nickname), "Render")]
    public class Staff_Nickname_Render
    {
        /// <summary>
        /// Prefix method to customize the rendering of the age input popup.
        /// </summary>
        /// <param name="__instance">The instance of Staff_Nickname being patched.</param>
        /// <returns>False if the original method should be skipped, true otherwise.</returns>
        public static bool Prefix(ref Staff_Nickname __instance)
        {
            if (!agePopup)
            {
                return true;
            }

            __instance.NicknameField.GetComponent<InputField>().characterLimit = 50;
            __instance.NicknameField.GetComponent<InputField>().text = Language.Insert("AGELIMIT__INPUT_FIELD", new string[] { defaultMinAge.ToString(), defaultMaxAge.ToString() });

            ExtensionMethods.SetColor(__instance.NicknameText, mainScript.blue32);
            __instance.NicknameImage.GetComponent<Image>().color = mainScript.blue32;

            __instance.transform.Find("Remove").gameObject.GetComponentInChildren<Lang_Button>().Constant = "AGELIMIT__DEFAULT";
            __instance.transform.Find("Save").gameObject.GetComponentInChildren<Lang_Button>().Constant = "AGELIMIT__SAVE";
            __instance.transform.Find("Save").gameObject.GetComponentInChildren<ButtonDefault>().SetTooltip(ExtensionMethods.color(Language.Data["AGELIMIT__TOOLTIP"], mainScript.red));

            __instance.Nickname_Set(__instance.NicknameField.GetComponent<InputField>().text);

            return false;
        }
    }

    /// <summary>
    /// Patches the Staff_Nickname class to handle saving age input.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Nickname), "Nickname_onSave")]
    public class Staff_Nickname_Nickname_onSave
    {
        /// <summary>
        /// Prefix method to handle saving the age input.
        /// </summary>
        /// <param name="__instance">The instance of Staff_Nickname being patched.</param>
        /// <returns>False if the original method should be skipped, true otherwise.</returns>
        public static bool Prefix(Staff_Nickname __instance)
        {
            if (!agePopup)
            {
                return true;
            }
            ParseAgeRange(__instance.NicknameField.GetComponent<InputField>().text);
            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().Close();
            agePopup = false;
            return false;
        }
    }

    /// <summary>
    /// Patches the Staff_Nickname class to handle resetting age input.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Nickname), "Nickname_onRemove")]
    public class Staff_Nickname_Nickname_onRemove
    {
        /// <summary>
        /// Prefix method to handle resetting the age input to default values.
        /// </summary>
        /// <returns>False if the original method should be skipped, true otherwise.</returns>
        public static bool Prefix()
        {
            if (!agePopup)
            {
                return true;
            }
            minAge = defaultMinAge;
            maxAge = defaultMaxAge;
            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().Close();
            agePopup = false;
            return false;
        }
    }

    /// <summary>
    /// Patches the Staff_Nickname class to handle clicking on the age input field.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Nickname), "Nickname_onClick")]
    public class Staff_Nickname_Nickname_onClick
    {
        /// <summary>
        /// Prefix method to handle clicking on the age input field.
        /// </summary>
        /// <param name="__instance">The instance of Staff_Nickname being patched.</param>
        /// <returns>False if the original method should be skipped, true otherwise.</returns>
        public static bool Prefix(Staff_Nickname __instance)
        {
            if (!agePopup)
            {
                return true;
            }

            if (__instance.NicknameField.GetComponent<InputField>().text == Language.Insert("AGELIMIT__INPUT_FIELD", new string[] { defaultMinAge.ToString(), defaultMaxAge.ToString() }))
            {
                __instance.NicknameField.GetComponent<InputField>().text = "";
            }
            return false;
        }
    }

    /// <summary>
    /// Patches the Staff_Nickname class to handle setting the age input.
    /// </summary>
    [HarmonyPatch(typeof(Staff_Nickname), "Nickname_Set")]
    public class Staff_Nickname_Nickname_Set
    {
        /// <summary>
        /// Prefix method to handle setting and validating the age input.
        /// </summary>
        /// <param name="__instance">The instance of Staff_Nickname being patched.</param>
        /// <param name="val">The input value to set.</param>
        /// <returns>False if the original method should be skipped, true otherwise.</returns>
        public static bool Prefix(Staff_Nickname __instance, string val)
        {
            if (!agePopup)
            {
                return true;
            }

            inputValid = IsInputValid(val);

            if (inputValid)
            {
                __instance.transform.Find("Save").gameObject.GetComponentInChildren<ButtonDefault>().SetTooltip("");
            }
            else
            {
                __instance.transform.Find("Save").gameObject.GetComponentInChildren<ButtonDefault>().SetTooltip(ExtensionMethods.color(Language.Data["AGELIMIT__TOOLTIP"], mainScript.red));
            }

            __instance.NicknameSave.GetComponent<ButtonDefault>().Activate(inputValid, true);


            return false;
        }
    }

    /// <summary>
    /// Contains utility methods and variables for custom auditions.
    /// </summary>
    class CustomAuditions
    {
        public static int defaultMinAge = 12;
        public static int defaultMaxAge = 23;
        public static int minAge = defaultMinAge;
        public static int maxAge = defaultMaxAge;

        public static bool agePopup = false;
        public static bool inputValid = false;

        /// <summary>
        /// Parses the age range string and sets the minAge and maxAge values.
        /// </summary>
        /// <param name="ageRange">The age range string to parse.</param>
        public static void ParseAgeRange(string ageRange)
        {
            if (IsInputValid(ageRange))
            {
                string[] ageLimits = ageRange.Split('-');
                minAge = int.Parse(ageLimits[0].Trim());
                maxAge = int.Parse(ageLimits[1].Trim());
            }
            else
            {
                minAge = defaultMinAge;
                maxAge = defaultMaxAge;
            }


        }

        /// <summary>
        /// Validates the input age range string.
        /// </summary>
        /// <param name="ageRange">The age range string to validate.</param>
        /// <returns>True if the input is valid, false otherwise.</returns>
        public static bool IsInputValid(string ageRange)
        {

            string[] ageLimits = ageRange.Split('-');

            if (ageLimits == null || ageLimits.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(ageLimits[0].Trim(), out int min))
            {
                return false;
            }
            if (!int.TryParse(ageLimits[1].Trim(), out int max))
            {
                return false;
            }

            if (max < 1 || min < 1)
            {
                return false;
            }

            if (max < min)
            {
                return false;
            }

            return true;
        }

        public static int chanceLesbian = 7;
        public static int chanceBi = 14;

        public static List<data_girls._paramType> paramTypes = new()
        {
            data_girls._paramType.cute,
            data_girls._paramType.cool,
            data_girls._paramType.sexy,
            data_girls._paramType.pretty,
            data_girls._paramType.vocal,
            data_girls._paramType.dance,
            data_girls._paramType.funny,
            data_girls._paramType.smart
        };

        public static Dictionary<data_girls._paramType, int> priorityDict = new();


    }
}
