using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.UI;
using static FanAttrition.Utility;

namespace FanAttrition
{

    /// <summary>
    /// Modifies the fan tooltip to include attrition information.
    /// </summary>
    [HarmonyPatch(typeof(tooltip_fans), nameof(tooltip_fans.Render), new Type[] { })]
    public class tooltip_fans_Render
    {
        private static bool loggedError;

        /// <summary>
        /// Prefix method to completely replace the original tooltip rendering.
        /// </summary>
        /// <remarks>
        /// Writes to the lines tooltip_fans_Start added, so lines other mods add to the tooltip are left alone. If
        /// there are none (Start was skipped) or rendering fails, the game's tooltip is shown instead.
        /// </remarks>
        /// <param name="__instance">The tooltip_fans instance.</param>
        /// <returns>Whether to execute the original method.</returns>
        public static bool Prefix(tooltip_fans __instance)
        {
            if (__instance == null || __instance.gameObject.transform == null)
                return true;
            if (!tooltip_fans_Start.Lines.TryGetValue(__instance, out tooltip_fans_line[] lines) || RenderFanChangeDelegate == null)
                return true;

            try
            {
                resources.RecalcFans();
                UpdateFanCount();
                lines[0].Set(GetLineText(resources.fanType.hardcore));
                lines[1].Set(GetLineText(resources.fanType.casual));
                lines[2].Set(GetLineText(resources.fanType.male));
                lines[3].Set(GetLineText(resources.fanType.female));
                lines[4].Set(GetLineText(resources.fanType.teen));
                lines[5].Set(GetLineText(resources.fanType.youngAdult));
                lines[6].Set(GetLineText(resources.fanType.adult));

                lines[7].Set(GetLineText(lineType.padding));

                lines[8].Set(GetLineText(business._type.ad));
                lines[9].Set(GetLineText(business._type.tv_drama));
                lines[10].Set(GetLineText(Shows._param._media_type.internet));
                lines[11].Set(GetLineText(Shows._param._media_type.tv));
                lines[12].Set(GetLineText(Shows._param._media_type.radio));
                lines[13].Set(GetLineText(lineType.cafe));

                lines[14].Set(GetLineText(lineType.churn));

                RenderFanChangeDelegate(__instance);
                LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.gameObject.GetComponent<RectTransform>());
                return false;
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Debug.LogError("[Fan Attrition] Couldn't show the fan tooltip, so the game's is shown: " + e);
                }
                return true;
            }
        }

        static string GetLineText(lineType lineType)
        {
            string lineText = "";

            switch(lineType)
            {
                case lineType.padding:
                    lineText = mainScript.separator_no_linebreaks;
                    break;
                case lineType.cafe:
                    lineText = GetCafeLine();
                    break;
                case lineType.churn:
                    lineText = GetChurnLine();
                    break;
            }

            return lineText;
        }

        /// <summary>
        /// Generates the churn line for the fan tooltip.
        /// </summary>
        /// <returns>A formatted string representing the weekly fan churn.</returns>
        static string GetChurnLine()
        {
            string label = Language.Data[CHURNRATE_LABEL];
            long value = -GetDailyChurn() * 7;
            string valueText = ExtensionMethods.formatNumber(value) + " " + Language.Data["PER_WEEK"];

            if (value < 0)
            {
                valueText = ExtensionMethods.color(valueText, mainScript.red);
            }

            return label + ": " + valueText;
        }

        /// <summary>
        /// Generates the cafe line for the fan tooltip.
        /// </summary>
        /// <returns>A formatted string representing weekly fans gained from cafes.</returns>
        static string GetCafeLine()
        {
            string label = Language.Data["TIP__CAFE"];
            string valueText = ExtensionMethods.formatNumber(cafeFans) + " " + Language.Data["PER_WEEK"];

            if (cafeFans > 0)
            {
                valueText = ExtensionMethods.color("+" + valueText, mainScript.green);
            }

            return label + ": " + valueText;
        }

        /// <summary>
        /// Generates a line for a specific fan type in the tooltip.
        /// </summary>
        /// <param name="FanType">The type of fan to generate the line for.</param>
        /// <returns>A formatted string with fan type information and appeal.</returns>
        static string GetLineText(resources.fanType FanType)
        {
            int num = 0;
            float appeal = 0f;
            foreach (data_girls.girls girl in data_girls.GetActiveGirls())
            {
                if (girl == null) continue;

                appeal += GetAppealRatio(girl, FanType);
                num++;
            }

            float avgAppeal = 0;
            if (num > 0)
            {
                avgAppeal = appeal / num;
            }

            string appealStr = ExtensionMethods.toPercent(avgAppeal) + "%";
            if (avgAppeal >= 0.4)
            {
                appealStr = ExtensionMethods.color(appealStr, mainScript.green);
            }
            else if (avgAppeal <= 0.3)
            {
                appealStr = ExtensionMethods.color(appealStr, mainScript.red);
            }

            float fanRatio = 0;
            if (resources.GetFansTotal() > 0)
            {
                fanRatio = (float)resources.GetFansTotal(FanType) / resources.GetFansTotal();
            }
            string ratioStr = ExtensionMethods.toPercent(fanRatio) + "%";


            float greenLimit = 0.6f;
            float redLimit = 0.4f;
            if (FanType == resources.fanType.teen || FanType == resources.fanType.youngAdult || FanType == resources.fanType.adult)
            {
                greenLimit = 0.4f;
                redLimit = 0.25f;
            }
            if (fanRatio >= greenLimit)
            {
                ratioStr = ExtensionMethods.color(ratioStr, mainScript.green);
            }
            else if (fanRatio <= redLimit)
            {
                ratioStr = ExtensionMethods.color(ratioStr, mainScript.red);
            }

            string fanLabel = resources.GetFanTitle(FanType);
            string ofTotal = Language.Data[TOOLTIP_OFTOTAL_LABEL];
            string appealLabel = Language.Data["TIP__APPEAL"];
            return fanLabel + ": " + ratioStr + " " + ofTotal + " (" + appealStr + " " + appealLabel + ")";
        }

        /// <summary>
        /// Gets an idol's appeal to a fan type without changing any game state.
        /// </summary>
        /// <remarks>
        /// A loaded idol has no stored appeal until the game recalculates it. In that case the appeal is
        /// worked out from her stats the way data_girls.girls.RecalcFanAppeal() does. Calling that method
        /// here would also overwrite the saved appeal of each of her fans.
        /// </remarks>
        /// <param name="girl">The idol.</param>
        /// <param name="FanType">The type of fan.</param>
        /// <returns>The idol's appeal to the fan type.</returns>
        static float GetAppealRatio(data_girls.girls girl, resources.fanType FanType)
        {
            if (girl.FanAppeal.Count > 0)
            {
                return girl.GetFanAppeal(FanType).ratio;
            }

            float ratio = 0f;
            foreach (data_girls.girls.param param in girl.parameters)
            {
                ratio += girl.GetAppealOfStat(param.type, FanType);
            }
            return ratio;
        }

        /// <summary>
        /// Generates a line for contract-based fans in the tooltip.
        /// </summary>
        /// <param name="Type">The type of business contract.</param>
        /// <returns>A formatted string with contract-based fan information.</returns>
        static string GetLineText(business._type Type)
        {
            string label;
            long value;

            switch(Type)
            {
                case business._type.ad:
                    label = Language.Data[TOOLTIP_AD_LABEL];
                    value = Math.Max(adFans,0);
                    break;
                case business._type.tv_drama:
                    label = Language.Data[TOOLTIP_DRAMA_LABEL];
                    value = Math.Max(dramaFans, 0);
                    break;
                default:
                    label = "";
                    value = 0;
                    break;
            }

            string fanStr = ExtensionMethods.formatNumber(value) + " " + Language.Data["PER_WEEK"];
            if (value > 0)
            {
                fanStr = ExtensionMethods.color("+" + fanStr, mainScript.green);
            }
            return label + ": " + fanStr;
        }

        /// <summary>
        /// Generates a line for show-based fans in the tooltip.
        /// </summary>
        /// <param name="Type">The type of media for the show.</param>
        /// <returns>A formatted string with show-based fan information.</returns>
        static string GetLineText(Shows._param._media_type Type)
        {
            long value;
            string label;

            switch(Type)
            {
                case Shows._param._media_type.tv:
                    label = Language.Data[TOOLTIP_TV_LABEL];
                    value = Math.Max(tvFans,0);
                    break;
                case Shows._param._media_type.internet:
                    label = Language.Data[TOOLTIP_INTERNET_LABEL];
                    value = Math.Max(netFans, 0);
                    break;
                case Shows._param._media_type.radio:
                    label = Language.Data[TOOLTIP_RADIO_LABEL];
                    value = Math.Max(radioFans, 0);
                    break;
                default:
                    label = "";
                    value = 0;
                    break;
            }

            string fanStr = ExtensionMethods.formatNumber(value) + " " + Language.Data["PER_WEEK"];
            if (value > 0)
            {
                fanStr = ExtensionMethods.color("+" + fanStr, mainScript.green);
            }

            return label + ": " + fanStr;
        }

        enum lineType
        {
            padding,
            cafe,
            churn
        }
    }

    /// <summary>
    /// Sets up the structure for the modified fan tooltip.
    /// </summary>
    [HarmonyPatch(typeof(tooltip_fans), nameof(tooltip_fans.Start), new Type[] { })]
    public class tooltip_fans_Start
    {
        public const int LINES = 15;

        /// <summary>
        /// The lines this mod added to each tooltip, in order.
        /// </summary>
        public static readonly ConditionalWeakTable<tooltip_fans, tooltip_fans_line[]> Lines = new();

        /// <summary>
        /// Prefix method to add additional lines to the tooltip.
        /// </summary>
        /// <param name="__instance">The tooltip_fans instance.</param>
        /// <returns>Whether to execute the original method.</returns>
        public static void Prefix(tooltip_fans __instance)
        {
            // An open delegate: Render passes the tooltip, so it never calls into an older, destroyed one
            MethodInfo RenderFanChange = AccessTools.Method(typeof(tooltip_fans), nameof(tooltip_fans.RenderFanChange), Type.EmptyTypes);
            RenderFanChangeDelegate = AccessTools.MethodDelegate<Action<tooltip_fans>>(RenderFanChange);

            tooltip_fans_line[] lines = new tooltip_fans_line[LINES];
            for (int i = 0; i < LINES; i++)
            {
                lines[i] = AddLine(__instance, "");
            }
            Lines.Remove(__instance);
            Lines.Add(__instance, lines);
        }

        /// <summary>
        /// Adds a new line to the fan tooltip.
        /// </summary>
        /// <param name="instance">The tooltip_fans instance to add the line to.</param>
        /// <param name="txt">The text content of the line to be added.</param>
        /// <returns>The new line.</returns>
        static tooltip_fans_line AddLine(tooltip_fans instance, string txt)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(instance.prefab_line);
            gameObject.transform.SetParent(instance.gameObject.transform, false);
            tooltip_fans_line line = gameObject.GetComponent<tooltip_fans_line>();
            line.Set(txt);
            return line;
        }
    }



    /// <summary>
    /// Modifies the rendering of fan change information in the tooltip.
    /// </summary>
    [HarmonyPatch(typeof(tooltip_fans), nameof(tooltip_fans.RenderFanChange), new Type[] { })]
    public class tooltip_fans_RenderFanChange
    {
        /// <summary>
        /// Postfix method to adjust the display of fan change information.
        /// </summary>
        /// <param name="__instance">The tooltip_fans instance.</param>
        public static void Postfix(tooltip_fans __instance)
        {
            // Counted here, not once a day, so the total is right straight after loading a save
            UpdateFanCount();
            long baseChange = -GetDailyChurn() * 7;
            long totalChange = adFans + dramaFans + netFans + tvFans + radioFans + cafeFans + baseChange;

            string changeStr = ExtensionMethods.formatNumber(totalChange, false, false) + " " + Language.Data["PER_WEEK"];
            if (totalChange > 0)
            {
                changeStr = ExtensionMethods.color("+" + changeStr, mainScript.green);
            }
            else if (totalChange < 0)
            {
                changeStr = ExtensionMethods.color(changeStr, mainScript.red);
            }

            string finalText = Language.Data["TOTAL"] + ": " + changeStr;
            ExtensionMethods.SetText(__instance.fan_change, finalText);
        }
    }
}
