using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using UnityEngine.UI;
using static FanAttrition.Utility;
using System.Runtime.InteropServices;

namespace FanAttrition
{

    /// <summary>
    /// Modifies the fan tooltip to include attrition information.
    /// </summary>
    [HarmonyPatch(typeof(tooltip_fans), "Render", new Type[] { })]
    public class tooltip_fans_Render
    {
        /// <summary>
        /// Prefix method to completely replace the original tooltip rendering.
        /// </summary>
        /// <param name="__instance">The tooltip_fans instance.</param>
        /// <returns>Whether to execute the original method.</returns>
        public static bool Prefix(tooltip_fans __instance)
        {
            if (__instance == null || __instance.gameObject.transform == null)
                return true;

            resources.RecalcFans();
            Text[] textLines = __instance.gameObject.GetComponentsInChildren<Text>();
            textLines[0].text = GetLineText(resources.fanType.hardcore);
            textLines[1].text = GetLineText(resources.fanType.casual);
            textLines[2].text = GetLineText(resources.fanType.male);
            textLines[3].text = GetLineText(resources.fanType.female);
            textLines[4].text = GetLineText(resources.fanType.teen);
            textLines[5].text = GetLineText(resources.fanType.youngAdult);
            textLines[6].text = GetLineText(resources.fanType.adult);

            textLines[7].text = GetLineText(lineType.padding);

            textLines[8].text = GetLineText(business._type.ad);
            textLines[9].text = GetLineText(business._type.tv_drama);
            textLines[10].text = GetLineText(Shows._param._media_type.internet);
            textLines[11].text = GetLineText(Shows._param._media_type.tv);
            textLines[12].text = GetLineText(Shows._param._media_type.radio);
            textLines[13].text = GetLineText(lineType.cafe);

            textLines[14].text = GetLineText(lineType.churn);

            RenderFanChangeDelegate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.gameObject.GetComponent<RectTransform>());
            return false;
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
            long value = Math.Min(resources.FansChange * 7, 0);
            string valueText = ExtensionMethods.formatNumber(value) + " " + Language.Data["PER_WEEK"];

            if (resources.FansChange < 0)
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

                if (girl.FanAppeal.Count == 0)
                {
                    girl.RecalcFanAppeal();
                }
                appeal += girl.GetFanAppeal(FanType).ratio;
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
    [HarmonyPatch(typeof(tooltip_fans), "Start", new Type[] { })]
    public class tooltip_fans_Start
    {
        /// <summary>
        /// Prefix method to add additional lines to the tooltip.
        /// </summary>
        /// <param name="__instance">The tooltip_fans instance.</param>
        /// <returns>Whether to execute the original method.</returns>
        public static void Prefix(tooltip_fans __instance)
        {
            MethodInfo RenderFanChange = AccessTools.Method(__instance.GetType(), "RenderFanChange");
            RenderFanChangeDelegate = AccessTools.MethodDelegate<Action>(RenderFanChange, __instance);

            for (int i = 0; i < 15; i++)
            {
                AddLine(__instance, "");
            }
        }

        /// <summary>
        /// Adds a new line to the fan tooltip.
        /// </summary>
        /// <param name="instance">The tooltip_fans instance to add the line to.</param>
        /// <param name="txt">The text content of the line to be added.</param>
        static void AddLine(tooltip_fans instance, string txt)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(instance.prefab_line);
            gameObject.transform.SetParent(instance.gameObject.transform, false);
            gameObject.GetComponent<tooltip_fans_line>().Set(txt);
        }
    }



    /// <summary>
    /// Modifies the rendering of fan change information in the tooltip.
    /// </summary>
    [HarmonyPatch(typeof(tooltip_fans), "RenderFanChange", new Type[] { })]
    public class tooltip_fans_RenderFanChange
    {
        /// <summary>
        /// Postfix method to adjust the display of fan change information.
        /// </summary>
        /// <param name="__instance">The tooltip_fans instance.</param>
        public static void Postfix(tooltip_fans __instance)
        {
            long baseChange = resources.FansChange * 7;
            long totalChange = adFans + dramaFans + netFans + tvFans + radioFans + baseChange;

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
