using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    internal static class ViralFanTooltip
    {
        private const string Hardcore = "GV_Hardcore";
        private const string Casual = "GV_Casual";
        private const string Male = "GV_Male";
        private const string Female = "GV_Female";
        private const string Teen = "GV_Teen";
        private const string YoungAdult = "GV_YoungAdult";
        private const string Adult = "GV_Adult";
        private const string Separator1 = "GV_Separator1";
        private const string Ad = "GV_Ad";
        private const string Drama = "GV_Drama";
        private const string Internet = "GV_Internet";
        private const string TV = "GV_TV";
        private const string Radio = "GV_Radio";
        private const string Cafe = "GV_Cafe";
        private const string Churn = "GV_Churn";
        private const string Separator2 = "GV_Separator2";
        private const string Trending = "GV_Trending";

        internal static readonly string[] BaseLines =
        {
            Hardcore, Casual, Male, Female, Teen, YoungAdult, Adult, Separator1,
            Ad, Drama, Internet, TV, Radio, Cafe, Churn
        };

        internal static void EnsureLine(tooltip_fans instance, string name)
        {
            if (instance == null || instance.prefab_line == null || instance.transform == null || instance.transform.Find(name) != null)
                return;
            GameObject line = UnityEngine.Object.Instantiate(instance.prefab_line);
            line.name = name;
            line.transform.SetParent(instance.transform, false);
            tooltip_fans_line component = line.GetComponent<tooltip_fans_line>();
            if (component != null) component.Set("");
        }

        internal static void EnsureExtraLines(tooltip_fans instance)
        {
            EnsureLine(instance, Separator2);
            EnsureLine(instance, Trending);
        }

        internal static void SetLine(tooltip_fans instance, string name, string text)
        {
            if (instance == null || instance.transform == null)
                return;
            Transform transform = instance.transform.Find(name);
            if (transform == null)
                return;
            tooltip_fans_line line = transform.GetComponent<tooltip_fans_line>();
            if (line != null) line.Set(text ?? "");
        }

        internal static void RenderBaseLines(tooltip_fans instance)
        {
            resources.RecalcFans();
            SetLine(instance, Hardcore, GetFanLine(resources.fanType.hardcore));
            SetLine(instance, Casual, GetFanLine(resources.fanType.casual));
            SetLine(instance, Male, GetFanLine(resources.fanType.male));
            SetLine(instance, Female, GetFanLine(resources.fanType.female));
            SetLine(instance, Teen, GetFanLine(resources.fanType.teen));
            SetLine(instance, YoungAdult, GetFanLine(resources.fanType.youngAdult));
            SetLine(instance, Adult, GetFanLine(resources.fanType.adult));
            SetLine(instance, Separator1, mainScript.separator_no_linebreaks);
            SetLine(instance, Ad, GetContractsLine(business._type.ad));
            SetLine(instance, Drama, GetContractsLine(business._type.tv_drama));
            SetLine(instance, Internet, GetShowLine(Shows._param._media_type.internet));
            SetLine(instance, TV, GetShowLine(Shows._param._media_type.tv));
            SetLine(instance, Radio, GetShowLine(Shows._param._media_type.radio));
            SetLine(instance, Cafe, GetCafeLine());
            SetLine(instance, Churn, GetChurnLine());
        }

        internal static void RenderExtras(tooltip_fans instance)
        {
            EnsureExtraLines(instance);
            SetLine(instance, Separator2, mainScript.separator_no_linebreaks);
            SetLine(instance, Trending, GetTrendingLine());
            RenderTotalChange(instance);
            if (instance != null)
            {
                RectTransform rect = instance.GetComponent<RectTransform>();
                if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        internal static void RenderTotalChange(tooltip_fans instance)
        {
            if (instance == null || instance.fan_change == null)
                return;
            long netChange = 0;
            netChange = SafeAdd(netChange, adFans);
            netChange = SafeAdd(netChange, dramaFans);
            netChange = SafeAdd(netChange, netFans);
            netChange = SafeAdd(netChange, tvFans);
            netChange = SafeAdd(netChange, radioFans);
            netChange = SafeAdd(netChange, cafeFans);
            netChange = SafeAdd(netChange, ScaleLong(resources.FansChange, 7f));

            string text = ExtensionMethods.formatNumber(netChange, false, false) + " " + Language.Data["PER_WEEK"];
            if (netChange > 0) text = ExtensionMethods.color("+" + text, mainScript.green);
            else if (netChange < 0) text = ExtensionMethods.color(text, mainScript.red);
            ExtensionMethods.SetText(instance.fan_change, Language.Data["TOTAL"] + ": " + text);
        }

        private static string GetChurnLine()
        {
            if (!Harmony.HasAnyPatches("com.tel.fanattrition"))
                return "";
            long churn = Math.Min(ScaleLong(resources.FansChange, 7f), 0L);
            string text = ExtensionMethods.formatNumber(churn) + " " + Language.Data["PER_WEEK"];
            if (churn < 0) text = ExtensionMethods.color(text, mainScript.red);
            return Language.Data["CHURN"] + ": " + text;
        }

        private static string GetTrendingLine()
        {
            if (trending == 0)
                return Language.Data["TIP__TRENDING"] + ": " + Language.Data["TIP__NONE"];

            long days = Math.Abs(trending);
            string daysKey = days == 1 ? "TIP__DAY_LEFT" : "TIP__DAYS_LEFT";
            string value = GetTrendingCoeff() + "x (" + days + " " + Language.Data[daysKey] + ")";
            return Language.Data["TIP__TRENDING"] + ": " +
                ExtensionMethods.color(value, trending > 0 ? mainScript.green : mainScript.red);
        }

        private static string GetCafeLine()
        {
            string value = ExtensionMethods.formatNumber(Math.Max(0, cafeFans)) + " " + Language.Data["PER_WEEK"];
            if (cafeFans > 0) value = ExtensionMethods.color("+" + value, mainScript.green);
            return Language.Data["TIP__CAFE"] + ": " + value;
        }

        private static string GetFanLine(resources.fanType type)
        {
            int count = 0;
            float appeal = 0f;
            var girls = data_girls.GetActiveGirls();
            if (girls != null)
            {
                foreach (data_girls.girls girl in girls)
                {
                    if (girl == null) continue;
                    if (girl.FanAppeal == null || girl.FanAppeal.Count == 0) girl.RecalcFanAppeal();
                    singles._fanAppeal fanAppeal = girl.GetFanAppeal(type);
                    if (fanAppeal == null) continue;
                    appeal += fanAppeal.ratio;
                    count++;
                }
            }

            float avgAppeal = count > 0 ? appeal / count : 0f;
            string appealText = ExtensionMethods.toPercent(avgAppeal) + "%";
            if (avgAppeal >= 0.4f) appealText = ExtensionMethods.color(appealText, mainScript.green);
            else if (avgAppeal <= 0.3f) appealText = ExtensionMethods.color(appealText, mainScript.red);

            long totalFans = resources.GetFansTotal();
            float fanRatio = totalFans > 0 ? (float)resources.GetFansTotal(type) / totalFans : 0f;
            string ratioText = ExtensionMethods.toPercent(fanRatio) + "%";
            float upper = (type == resources.fanType.teen || type == resources.fanType.youngAdult || type == resources.fanType.adult) ? 0.4f : 0.6f;
            float lower = (type == resources.fanType.teen || type == resources.fanType.youngAdult || type == resources.fanType.adult) ? 0.25f : 0.4f;
            if (fanRatio >= upper) ratioText = ExtensionMethods.color(ratioText, mainScript.green);
            else if (fanRatio <= lower) ratioText = ExtensionMethods.color(ratioText, mainScript.red);

            return resources.GetFanTitle(type) + ": " + ratioText + " " + Language.Data["TIP__OF_TOTAL"] +
                " (" + appealText + " " + Language.Data["TIP__APPEAL"] + ")";
        }

        private static string GetContractsLine(business._type type)
        {
            long value = type == business._type.ad ? adFans : type == business._type.tv_drama ? dramaFans : 0;
            string label = type == business._type.ad ? Language.Data["TIP__AD"] : Language.Data["TIP__DRAMA"];
            string text = ExtensionMethods.formatNumber(Math.Max(0, value)) + " " + Language.Data["PER_WEEK"];
            if (value > 0) text = ExtensionMethods.color("+" + text, mainScript.green);
            return label + ": " + text;
        }

        private static string GetShowLine(Shows._param._media_type type)
        {
            long value = 0;
            string label = "";
            if (type == Shows._param._media_type.tv) { value = tvFans; label = Language.Data["TIP__TV"]; }
            else if (type == Shows._param._media_type.internet) { value = netFans; label = Language.Data["TIP__INTERNET"]; }
            else if (type == Shows._param._media_type.radio) { value = radioFans; label = Language.Data["TIP__RADIO"]; }
            string text = ExtensionMethods.formatNumber(Math.Max(0, value)) + " " + Language.Data["PER_WEEK"];
            if (value > 0) text = ExtensionMethods.color("+" + text, mainScript.green);
            return label + ": " + text;
        }
    }

    [HarmonyPatch(typeof(tooltip_fans), "Start", new Type[] { })]
    public class tooltip_fans_Start
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static void Prefix(tooltip_fans __instance)
        {
            if (!Harmony.HasAnyPatches("com.tel.fanattrition"))
            {
                foreach (string name in ViralFanTooltip.BaseLines)
                    ViralFanTooltip.EnsureLine(__instance, name);
            }
            ViralFanTooltip.EnsureExtraLines(__instance);
        }
    }

    [HarmonyPatch(typeof(tooltip_fans), "Render", new Type[] { })]
    public class tooltip_fans_Render
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static bool Prefix(tooltip_fans __instance)
        {
            if (Harmony.HasAnyPatches("com.tel.fanattrition"))
                return true;
            if (__instance == null || __instance.transform == null)
                return true;

            ViralFanTooltip.RenderBaseLines(__instance);
            ViralFanTooltip.RenderExtras(__instance);
            return false;
        }

        public static void Postfix(tooltip_fans __instance)
        {
            ViralFanTooltip.RenderExtras(__instance);
        }
    }

    [HarmonyPatch(typeof(tooltip_fans), "RenderFanChange", new Type[] { })]
    public class tooltip_fans_RenderFanChange
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static void Postfix(tooltip_fans __instance)
        {
            ViralFanTooltip.RenderTotalChange(__instance);
        }
    }
}
