using HarmonyLib;
using System;
using UnityEngine;

namespace GoingViral
{
    [HarmonyPatch(typeof(Show_Popup), "SetParam")]
    public class Show_Popup_SetParam
    {
        [HarmonyAfter("com.tel.traitsfix")]
        public static void Postfix(Show_Popup __instance, Show_Popup_Param_Button._type type, Shows._param ___medium)
        {
            if (type != Show_Popup_Param_Button._type.medium || __instance == null || __instance.Grid_Genre == null)
                return;

            Show_Popup_Param_Button[] buttons = __instance.Grid_Genre.GetComponentsInChildren<Show_Popup_Param_Button>();
            if (buttons == null)
                return;

            bool isTv = ___medium != null && ___medium.media_type == Shows._param._media_type.tv;
            foreach (Show_Popup_Param_Button button in buttons)
            {
                if (button == null || button.param == null)
                    continue;
                ButtonDefault buttonDefault = button.GetComponent<ButtonDefault>();
                if (buttonDefault == null)
                    continue;

                if (!isTv)
                {
                    buttonDefault.SetTooltip(button.param.GetTooltip());
                    continue;
                }

                // Rebuild from the parameter tooltip each time. Other mods patching GetTooltip are
                // preserved, while our own TV suffix cannot accumulate across repeated SetParam calls.
                string tooltip = button.param.GetTooltip() ?? "";

                DateTime? lastShowDate = GetLastTvShowDate(button.param.id);
                if (lastShowDate.HasValue)
                {
                    int days = Math.Max(0, (staticVars.dateTime - lastShowDate.Value).Days);
                    string color = days >= 365 ? mainScript.green : mainScript.red;
                    tooltip += mainScript.separator + Language.Data["TREND__GENRE"];
                    tooltip += days == 1
                        ? ExtensionMethods.color(Language.Data["ONE_DAY_AGO"], color)
                        : ExtensionMethods.color(Language.Insert("DAYS_AGO", new string[] { days.ToString() }), color);
                }
                else
                {
                    tooltip += mainScript.separator + Language.Data["TREND__GENRE_NEVER"];
                }

                buttonDefault.SetTooltip(button.param.DescriptionReplaceVariables(tooltip));
            }
        }

        private static DateTime? GetLastTvShowDate(int genreId)
        {
            DateTime? result = null;
            if (Shows.shows == null)
                return null;

            foreach (Shows._show show in Shows.shows)
            {
                if (show == null || show.medium == null || show.genre == null || show.LaunchDate == default(DateTime))
                    continue;
                if (show.medium.media_type != Shows._param._media_type.tv || show.genre.id != genreId)
                    continue;
                if (!result.HasValue || show.LaunchDate > result.Value)
                    result = show.LaunchDate;
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(Shows._param), "GetTooltip")]
    public class Shows__param_GetTooltip
    {
        public static void Postfix(Shows._param __instance, ref string __result)
        {
            if (__instance != null && __instance.ParamType == Shows._param._paramType.medium)
                __result = (__result ?? "") + mainScript.separator + Language.Data["TREND__MEDIUM"];
        }
    }

    [HarmonyPatch(typeof(SinglePopup_GenreButton), "RenderTooltip")]
    public class SinglePopup_GenreButton_RenderTooltip
    {
        public static void Postfix(SinglePopup_GenreButton __instance)
        {
            if (__instance == null || __instance.param == null)
                return;

            singles._param param = __instance.param;
            if (param.derived != "business" && !param.IsRiskyMarketing())
                return;

            singles._param._special_type marketing = param.Special_Type;
            if (marketing != singles._param._special_type.ad_campaign &&
                marketing != singles._param._special_type.viral_campaign &&
                marketing != singles._param._special_type.fake_scandal)
                return;

            ButtonDefault buttonDefault = Traverse.Create(__instance).Field("buttonDefault").GetValue() as ButtonDefault;
            if (buttonDefault == null)
                return;

            string tooltip = Traverse.Create(buttonDefault).Field("tooltipText").GetValue() as string;
            if (tooltip == null) tooltip = "";
            tooltip += mainScript.separator;

            float success = TrendingManager.GetTrendingChance(param, Single_Marketing_Roll._result.success_crit) *
                            param.GetSuccessChance(Single_Marketing_Roll._result.success_crit) / 100f;
            int chanceSuccess = Mathf.Clamp(Mathf.RoundToInt(success), 0, 100);
            tooltip += Language.Insert("TREND__SINGLE_SUCCESS", new string[] { chanceSuccess.ToString() });

            float failure = 0f;
            if (marketing == singles._param._special_type.ad_campaign || marketing == singles._param._special_type.viral_campaign)
            {
                failure += TrendingManager.GetTrendingChance(param, Single_Marketing_Roll._result.fail_crit) *
                           param.GetSuccessChance(Single_Marketing_Roll._result.fail_crit) / 100f;
            }
            else if (marketing == singles._param._special_type.fake_scandal)
            {
                failure += TrendingManager.GetTrendingChance(param.GetSuccessModifier(Single_Marketing_Roll._result.fail, false)) *
                           param.GetSuccessChance(Single_Marketing_Roll._result.fail) / 100f;
                failure += TrendingManager.GetTrendingChance(param.GetSuccessModifier(Single_Marketing_Roll._result.fail_crit, false)) *
                           param.GetSuccessChance(Single_Marketing_Roll._result.fail_crit) / 100f;
            }

            int chanceFailure = Mathf.Clamp(Mathf.RoundToInt(failure), 0, 100);
            tooltip += "\n" + Language.Insert("TREND__SINGLE_FAIL", new string[] { chanceFailure.ToString() });
            tooltip += "\n" + Language.Data["TREND__SINGLE_DESC"];
            buttonDefault.SetTooltip(param.DescriptionReplaceVariables(tooltip));
        }
    }
}
