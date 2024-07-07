using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine.UI;
using UnityEngine;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    // Render tooltip
    [HarmonyPatch(typeof(tooltip_fans), "Render", new Type[] { })]
    public class tooltip_fans_Render
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static bool Prefix(tooltip_fans __instance)
        {
            if (__instance != null && __instance.gameObject.transform != null)
            {
                if (!Harmony.HasAnyPatches("com.tel.fanattrition"))
                {
                    resources.RecalcFans();

                    __instance.gameObject.GetComponentsInChildren<Text>()[0].text = GetFanLine(resources.fanType.hardcore);
                    __instance.gameObject.GetComponentsInChildren<Text>()[1].text = GetFanLine(resources.fanType.casual);
                    __instance.gameObject.GetComponentsInChildren<Text>()[2].text = GetFanLine(resources.fanType.male);
                    __instance.gameObject.GetComponentsInChildren<Text>()[3].text = GetFanLine(resources.fanType.female);
                    __instance.gameObject.GetComponentsInChildren<Text>()[4].text = GetFanLine(resources.fanType.teen);
                    __instance.gameObject.GetComponentsInChildren<Text>()[5].text = GetFanLine(resources.fanType.youngAdult);
                    __instance.gameObject.GetComponentsInChildren<Text>()[6].text = GetFanLine(resources.fanType.adult);

                    __instance.gameObject.GetComponentsInChildren<Text>()[7].text = mainScript.separator_no_linebreaks;

                    __instance.gameObject.GetComponentsInChildren<Text>()[10].text = GetShowLine(Shows._param._media_type.internet);
                    __instance.gameObject.GetComponentsInChildren<Text>()[11].text = GetShowLine(Shows._param._media_type.tv);
                    __instance.gameObject.GetComponentsInChildren<Text>()[12].text = GetShowLine(Shows._param._media_type.radio);

                    __instance.gameObject.GetComponentsInChildren<Text>()[13].text = GetCafeLine();

                }
                __instance.gameObject.GetComponentsInChildren<Text>()[8].text = GetContractsLine(business._type.ad);
                __instance.gameObject.GetComponentsInChildren<Text>()[9].text = GetContractsLine(business._type.tv_drama);

                __instance.gameObject.GetComponentsInChildren<Text>()[14].text = GetChurnLine();
                __instance.gameObject.GetComponentsInChildren<Text>()[15].text = mainScript.separator_no_linebreaks;
                __instance.gameObject.GetComponentsInChildren<Text>()[16].text = GetTrendingLine();

                var RenderFanChange = __instance.GetType().GetMethod("RenderFanChange", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                RenderFanChange.Invoke(__instance, null);
                LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.gameObject.GetComponent<RectTransform>());
                return false;
            }
            return true;
        }

        static string GetChurnLine()
        {
            if (Harmony.HasAnyPatches("com.tel.fanattrition"))
            {
                if (resources.FansChange < 0)
                {
                    return Language.Data["CHURN"] + ": " + ExtensionMethods.color(ExtensionMethods.formatNumber(resources.FansChange * 7) + " " + Language.Data["PER_WEEK"], mainScript.red);
                }
                else
                {
                    return Language.Data["CHURN"] + ": " + 0 + " " + Language.Data["PER_WEEK"];
                }
            }
            return "";
        }

        static string GetTrendingLine()
        {
            if (trending > 1)
            {
                return Language.Data["TIP__TRENDING"] + ": " + ExtensionMethods.color(GetTrendingCoeff() + "x (" + trending + " " + Language.Data["TIP__DAYS_LEFT"] + ")", mainScript.green);
            }
            else if (trending > 0)
            {
                return Language.Data["TIP__TRENDING"] + ": " + ExtensionMethods.color(GetTrendingCoeff() + "x (" + trending + " " + Language.Data["TIP__DAY_LEFT"] + ")", mainScript.green);
            }
            else if (trending < -1)
            {
                return Language.Data["TIP__TRENDING"] + ": " + ExtensionMethods.color(GetTrendingCoeff() + "x (" + -trending + " " + Language.Data["TIP__DAY_LEFT"] + ")", mainScript.red);
            }
            else if (trending < 0)
            {
                return Language.Data["TIP__TRENDING"] + ": " + ExtensionMethods.color(GetTrendingCoeff() + "x (" + -trending + " " + Language.Data["TIP__DAYS_LEFT"] + ")", mainScript.red);
            }
            else
            {
                return Language.Data["TIP__TRENDING"] + ": " + Language.Data["TIP__NONE"];
            }
        }

        static string GetCafeLine()
        {
            if (cafeFans > 0)
            {
                return Language.Data["TIP__CAFE"] + ": " + ExtensionMethods.color("+" + ExtensionMethods.formatNumber(cafeFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
            }
            else
            {
                return Language.Data["TIP__CAFE"] + ": " + cafeFans + " " + Language.Data["PER_WEEK"];
            }
        }

        static string GetFanLine(resources.fanType FanType)
        {
            int num = 0;
            float appeal = 0f;
            foreach (data_girls.girls girls in data_girls.GetActiveGirls())
            {
                if (girls != null)
                {
                    if (girls.FanAppeal.Count == 0)
                    {
                        girls.RecalcFanAppeal();
                    }
                    appeal += girls.GetFanAppeal(FanType).ratio;
                    num++;
                }
            }

            string appealStr = "0%";
            string ratioStr = "0%";
            if (num > 0)
            {
                float avgAppeal = appeal / num;
                if (avgAppeal >= 0.4)
                {
                    appealStr = ExtensionMethods.color(ExtensionMethods.toPercent(avgAppeal) + "%", mainScript.green);
                }
                else if (avgAppeal <= 0.3)
                {
                    appealStr = ExtensionMethods.color(ExtensionMethods.toPercent(avgAppeal) + "%", mainScript.red);
                }
                else
                {
                    appealStr = ExtensionMethods.toPercent(avgAppeal) + "%";
                }

                float fanRatio = (float)resources.GetFansTotal(FanType) / resources.GetFansTotal();
                float upper = 0.6f;
                float lower = 0.4f;
                if (FanType == resources.fanType.teen || FanType == resources.fanType.youngAdult || FanType == resources.fanType.adult)
                {
                    upper = 0.4f;
                    lower = 0.25f;
                }
                if (fanRatio >= upper)
                {
                    ratioStr = ExtensionMethods.color(ExtensionMethods.toPercent(fanRatio) + "%", mainScript.green);
                }
                else if (fanRatio <= lower)
                {
                    ratioStr = ExtensionMethods.color(ExtensionMethods.toPercent(fanRatio) + "%", mainScript.red);
                }
                else
                {
                    ratioStr = ExtensionMethods.toPercent(fanRatio) + "%";
                }
            }

            return resources.GetFanTitle(FanType) + ": " + ratioStr + " " + Language.Data["TIP__OF_TOTAL"] + " (" + appealStr + " " + Language.Data["TIP__APPEAL"] + ")";
        }

        static string GetContractsLine(business._type Type)
        {

            string fanStr = "0 " + Language.Data["PER_WEEK"];
            if (Type == business._type.ad)
            {
                if (adFans > 0)
                {
                    fanStr = ExtensionMethods.color("+" + ExtensionMethods.formatNumber(adFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
                }
                return Language.Data["TIP__AD"] + ": " + fanStr;
            }
            else if (Type == business._type.tv_drama)
            {
                if (dramaFans > 0)
                {
                    fanStr = ExtensionMethods.color("+" + ExtensionMethods.formatNumber(dramaFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
                }
                return Language.Data["TIP__DRAMA"] + ": " + fanStr;
            }
            return "";
        }

        static string GetShowLine(Shows._param._media_type Type)
        {

            string fanStr = "0 " + Language.Data["PER_WEEK"];
            if (Type == Shows._param._media_type.tv)
            {
                if (tvFans > 0)
                {
                    fanStr = ExtensionMethods.color("+" + ExtensionMethods.formatNumber(tvFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
                }
                return Language.Data["TIP__TV"] + ": " + fanStr;
            }
            else if (Type == Shows._param._media_type.internet)
            {
                if (netFans > 0)
                {
                    fanStr = ExtensionMethods.color("+" + ExtensionMethods.formatNumber(netFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
                }
                return Language.Data["TIP__INTERNET"] + ": " + fanStr;
            }
            else if (Type == Shows._param._media_type.radio)
            {
                if (radioFans > 0)
                {
                    fanStr = ExtensionMethods.color("+" + ExtensionMethods.formatNumber(radioFans) + " " + Language.Data["PER_WEEK"], mainScript.green);
                }
                return Language.Data["TIP__RADIO"] + ": " + fanStr;
            }
            return "";
        }
    }

    // Set up the fan tooltip structure
    [HarmonyPatch(typeof(tooltip_fans), "Start", new Type[] { })]
    public class tooltip_fans_Start
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static bool Prefix(tooltip_fans __instance)
        {
            var AddPadding = __instance.GetType().GetMethod("AddPadding", BindingFlags.NonPublic | BindingFlags.Instance);

            if (!Harmony.HasAnyPatches("com.tel.fanattrition"))
            {
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
                AddLine(__instance, "");
            }
            AddLine(__instance, "");
            AddLine(__instance, "");

            return true;
        }

        static void AddLine(tooltip_fans instance, string txt)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(instance.prefab_line);
            gameObject.transform.SetParent(instance.gameObject.transform, false);
            gameObject.GetComponent<tooltip_fans_line>().Set(txt);
        }
    }



    // Render the bottom line of fan tooltip
    [HarmonyPatch(typeof(tooltip_fans), "RenderFanChange", new Type[] { })]
    public class tooltip_fans_RenderFanChange
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static void Postfix(tooltip_fans __instance)
        {
            long netChange = adFans + dramaFans + netFans + tvFans + radioFans + resources.FansChange * 7;
            string changeStr = ExtensionMethods.formatNumber(netChange, false, false);
            if (netChange > 0)
            {
                changeStr = ExtensionMethods.color("+" + changeStr + " " + Language.Data["PER_WEEK"], mainScript.green);
            }
            else if (netChange < 0)
            {
                changeStr = ExtensionMethods.color(changeStr + " " + Language.Data["PER_WEEK"], mainScript.red);
            }
            ExtensionMethods.SetText(__instance.fan_change, string.Concat(new string[]
            {
                Language.Data["TOTAL"] + ": ",
                changeStr
            }));
        }
    }
}
