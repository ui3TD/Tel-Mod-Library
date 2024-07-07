using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace GoingViral
{
    // tooltip for shows
    [HarmonyPatch(typeof(Show_Popup), "SetParam")]
    public class Show_Popup_SetParam
    {
        public static void Postfix(Show_Popup __instance, Show_Popup_Param_Button._type type)
        {
            if (type != Show_Popup_Param_Button._type.medium)
            {
                return;
            }

            Shows._param medium = Traverse.Create(__instance).Field("medium").GetValue() as Shows._param;
            if (medium.media_type == Shows._param._media_type.tv)
            {
                Show_Popup_Param_Button[] buttons = __instance.Grid_Genre.GetComponentsInChildren<Show_Popup_Param_Button>();
                foreach (Show_Popup_Param_Button button in buttons)
                {
                    // Get last show
                    DateTime? lastShowDate = null;
                    foreach (Shows._show show in Shows.shows)
                    {
                        Shows._param genre = Traverse.Create(__instance).Field("genre").GetValue() as Shows._param;
                        if (show.LaunchDate != null && show.medium.media_type == Shows._param._media_type.tv && show.genre.id == genre.id)
                        {
                            if (lastShowDate == null || show.LaunchDate > (lastShowDate ?? staticVars.dateTime))
                            {
                                lastShowDate = show.LaunchDate;
                            }
                        }
                    }

                    string tooltipText = Traverse.Create(button.GetComponent<ButtonDefault>()).Field("tooltipText").GetValue() as string;


                    if (lastShowDate != null)
                    {
                        int days = (staticVars.dateTime - (lastShowDate ?? staticVars.dateTime)).Days;

                        string clr = mainScript.green;
                        if (days < 365)
                        {
                            clr = mainScript.red;
                        }
                        tooltipText += mainScript.separator + Language.Data["TREND__GENRE"];

                        if (days != 1)
                        {
                            tooltipText += ExtensionMethods.color(Language.Insert("DAYS_AGO", new[] { days.ToString() }), clr);
                        }
                        else
                        {
                            tooltipText += ExtensionMethods.color(Language.Data["ONE_DAY_AGO"], clr);
                        }
                    }
                    else
                    {
                        tooltipText += mainScript.separator + Language.Data["TREND__GENRE_NEVER"];
                    }

                    button.GetComponent<ButtonDefault>().SetTooltip(button.param.DescriptionReplaceVariables(tooltipText));
                }
            }
            else
            {
                Show_Popup_Param_Button[] buttons = __instance.Grid_Genre.GetComponentsInChildren<Show_Popup_Param_Button>();
                foreach (Show_Popup_Param_Button button in buttons)
                {
                    button.GetComponent<ButtonDefault>().SetTooltip(button.param.GetTooltip());
                }
            }
        }

    }


    // tooltip for single medium
    [HarmonyPatch(typeof(Shows._param), "GetTooltip")]
    public class Shows__param_GetTooltip
    {
        public static void Postfix(ref Shows._param __instance, ref string __result)
        {
            if (__instance.ParamType == Shows._param._paramType.medium)
            {
                __result += mainScript.separator + Language.Data["TREND__MEDIUM"];
            }
        }
    }

    // tooltip for single genres
    [HarmonyPatch(typeof(SinglePopup_GenreButton), "RenderTooltip")]
    public class SinglePopup_GenreButton_RenderTooltip
    {
        public static void Postfix(ref SinglePopup_GenreButton __instance)
        {
            singles._param param = __instance.param;
            if (param.derived != "business" && !param.IsRiskyMarketing())
            {
                return;
            }

            singles._param._special_type marketing = param.Special_Type;
            if(marketing != singles._param._special_type.ad_campaign && marketing != singles._param._special_type.viral_campaign && marketing != singles._param._special_type.fake_scandal)
            {
                return;
            }

            ButtonDefault buttonDefault = Traverse.Create(__instance).Field("buttonDefault").GetValue() as ButtonDefault;
            string tooltipText = Traverse.Create(buttonDefault).Field("tooltipText").GetValue() as string;


            tooltipText += mainScript.separator;
            int chanceSuccess = Mathf.RoundToInt(TrendingManager.GetTrendingChance(param, Single_Marketing_Roll._result.success_crit) * param.GetSuccessChance(Single_Marketing_Roll._result.success_crit) / 100);
            tooltipText += Language.Insert("TREND__SINGLE_SUCCESS", chanceSuccess.ToString());

            int chanceFailure = 0;
            switch (marketing)
            {
                case singles._param._special_type.ad_campaign:
                case singles._param._special_type.viral_campaign:
                    chanceFailure = Mathf.RoundToInt(TrendingManager.GetTrendingChance(param, Single_Marketing_Roll._result.fail) * param.GetSuccessChance(Single_Marketing_Roll._result.fail) / 100 +
                        TrendingManager.GetTrendingChance(param, Single_Marketing_Roll._result.fail_crit) * param.GetSuccessChance(Single_Marketing_Roll._result.fail_crit) / 100);
                    break;
                case singles._param._special_type.fake_scandal:
                    chanceFailure = Mathf.RoundToInt(TrendingManager.GetTrendingChance(param.GetSuccessModifier(Single_Marketing_Roll._result.fail, false)) * param.GetSuccessChance(Single_Marketing_Roll._result.fail) / 100 +
                        TrendingManager.GetTrendingChance(param.GetSuccessModifier(Single_Marketing_Roll._result.fail_crit, false)) * param.GetSuccessChance(Single_Marketing_Roll._result.fail_crit) / 100);
                    break;
                default:
                    break;
            }

            tooltipText += "\n" + Language.Insert("TREND__SINGLE_FAIL", chanceFailure.ToString());
            tooltipText += "\n" + Language.Data["TREND__SINGLE_DESC"];

            buttonDefault.SetTooltip(param.DescriptionReplaceVariables(tooltipText));
        }

    }
}
