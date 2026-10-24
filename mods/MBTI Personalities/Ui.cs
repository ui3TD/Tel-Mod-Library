using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for rendering the extras tab in the profile popup.
    /// Adds MBTI information to the profile extras tab if applicable.
    /// </summary>
    [HarmonyPatch(typeof(Profile_Popup), nameof(Profile_Popup.RenderTab_Extras))]
    public class Profile_Popup_RenderTab_Extras
    {

        /// <summary>
        /// Harmony patch for rendering the extras tab in the profile popup.
        /// Adds MBTI information to the profile extras tab if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the Profile_Popup class being patched.</param>
        public static void Postfix(Profile_Popup __instance)
        {
            MBTI mBTI = GetGirlMBTI(__instance.Girl);
            if (mBTI == MBTI.None)
                return;

            string txt = "\n" + ExtensionMethods.color(Text(constantTitlePrefix + mBTI.ToString()) + ": ", mainScript.blue) + Text(constantDescPrefix + mBTI.ToString());

            // Another mod may have rebuilt the tab without the game's first text block
            Transform textBlock = __instance.Extras_Container is null ? null : __instance.Extras_Container.transform.Find("Text(Clone)");
            TextMeshProUGUI textComponent = textBlock is null ? null : textBlock.GetComponent<TextMeshProUGUI>();
            if (textComponent is null)
                return;
            textComponent.text += txt;
            textComponent.text = textComponent.text.Replace(mainScript.blue, mainScript.black);
            LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Extras_Container.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// Harmony patch for showing traits on the audition data card.
    /// Adds MBTI information to the audition card if applicable.
    /// </summary>
    [HarmonyPatch(typeof(Audition_Data_Card), nameof(Audition_Data_Card.ShowTrait))]
    public class Audition_Data_Card_ShowTrait
    {
        /// <summary>
        /// Harmony patch for showing traits on the audition data card.
        /// Adds MBTI information to the audition card if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the Audition_Data_Card class being patched.</param>
        public static void Postfix(Audition_Data_Card __instance)
        {
            MBTI mBTI = GetGirlMBTI(__instance.Girl.girl);
            if (mBTI == MBTI.None)
                return;

            string title = " / " + Text(constantTitlePrefix + mBTI.ToString());
            string desc = "\n" + Text(constantDescPrefix + mBTI.ToString());

            // Added once: if another mod skipped the game's ShowTrait, the texts weren't rewritten and already have them
            AddOnce(__instance.Special_Skill_Title.GetComponent<TextMeshProUGUI>(), title);
            AddOnce(__instance.Special_Skill_Description.GetComponent<TextMeshProUGUI>(), desc);
        }

        private static void AddOnce(TextMeshProUGUI text, string addition)
        {
            string current = text.text;
            string added = WithAddition(current, addition);
            if (added != current)
                text.text = added;
        }

        /// <summary>The text with the addition at the end, unless it already ends with it.</summary>
        public static string WithAddition(string text, string addition)
        {
            return text != null && text.EndsWith(addition) ? text : text + addition;
        }
    }

    /// <summary>
    /// Harmony patch for getting tooltip text when hovering over a girl's profile.
    /// Adds MBTI information to the tooltip if applicable.
    /// </summary>
    [HarmonyPatch(typeof(GirlProfileOnHover), nameof(GirlProfileOnHover.GetTooltipText))]
    public class GirlProfileOnHover_GetTooltipText
    {
        /// <summary>
        /// Harmony patch for getting tooltip text when hovering over a girl's profile.
        /// Adds MBTI information to the tooltip if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the GirlProfileOnHover class being patched.</param>
        /// <param name="__result">Tooltip text to be displayed.</param>
        public static void Postfix(GirlProfileOnHover __instance, ref string __result)
        {
            // GetGirlMBTI gives None for no idol, so nothing is added
            MBTI mBTI = GetGirlMBTI(__instance.girl);
            if (mBTI == MBTI.None)
                return;

            __result += "\n" + Text(constantTitlePrefix + mBTI.ToString()) + ": " + Text(constantDescPrefix + mBTI.ToString());
        }
    }

    /// <summary>
    /// Harmony patch for getting tooltip text for a girl.
    /// Adds MBTI information to the tooltip if applicable.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetTooltipText))]
    public class data_girls_girls_GetTooltipText
    {
        /// <summary>
        /// Harmony patch for getting tooltip text for a girl.
        /// Adds MBTI information to the tooltip if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the data_girls.girls class being patched.</param>
        /// <param name="__result">Tooltip text to be displayed.</param>
        public static void Postfix(data_girls.girls __instance, ref string __result)
        {
            MBTI mBTI = GetGirlMBTI(__instance);
            if (mBTI == MBTI.None)
                return;

            __result += "\n" + Text(constantTitlePrefix + mBTI.ToString()) + ": " + Text(constantDescPrefix + mBTI.ToString());
        }
    }
}
