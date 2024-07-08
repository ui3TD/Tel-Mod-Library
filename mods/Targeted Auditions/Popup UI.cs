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

}
