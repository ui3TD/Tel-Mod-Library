using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Linq;
using UnityEngine.UI;
using static FanAttrition.Utility;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FanAttrition
{
    /// <summary>
    /// Modifies the fan gain from shows based on the MC's fame.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), "SetSales")]
    public class Shows__show_SetSales_MC
    {
        /// <summary>
        /// Transpiler method to inject custom logic for fan calculation.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object num6Operand = null;
            bool breakFlag = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (breakFlag && instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 12)
                {
                    index = i;
                    break;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 11)
                {
                    num6Operand = instructionList[i].operand;
                    breakFlag = true;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, num6Operand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Shows__show_SetSales_MC), "Infix")));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Stloc_S, num6Operand));
            }

            return instructionList.AsEnumerable();
        }

        /// <summary>
        /// Calculates the modified fan count based on MC's fame.
        /// </summary>
        /// <param name="__this">The current show instance.</param>
        /// <param name="num6">The original fan count.</param>
        /// <returns>The modified fan count.</returns>
        public static int Infix(Shows._show __this, int num6)
        {
            if (__this.mc != null)
            {
                float mcCoeff = Mathf.Max(1f, 1f + __this.mc.fame * __this.mc.fame / maxFame);
                if (__this.mc.fame >= maxFame)
                {
                    mcCoeff += maxMCFameBonus;
                }
                num6 = Mathf.RoundToInt(num6 * mcCoeff);
            }


            return num6;
        }

        private const float maxFame = 10f;
        private const float maxMCFameBonus = 5f;
    }

    /// <summary>
    /// Implements fan attrition based on show fatigue.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), "SetSales")]
    public class Shows__show_SetSales_Fatigue
    {
        /// <summary>
        /// Transpiler method to inject custom logic for fan attrition.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_1)
                {
                    index = i;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_2)
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_1));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Shows__show_SetSales_Fatigue), "Infix")));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Stloc_1));
            }

            return instructionList.AsEnumerable();
        }

        /// <summary>
        /// Calculates the modified fan count based on show fatigue.
        /// </summary>
        /// <param name="__this">The current show instance.</param>
        /// <param name="num2">The original fan count.</param>
        /// <returns>The modified fan count.</returns>
        public static float Infix(Shows._show __this, float num2)
        {
            if (staticVars.IsHard())
            {
                num2 *= 1f - __this.GetFatigue() * __this.GetFatigue() / fatigueCoeffHard;
            }
            else if (staticVars.IsNormal())
            {
                num2 *= 1f - __this.GetFatigue() * __this.GetFatigue() / fatigueCoeffNormal;
            }

            return num2;
        }

        private const float fatigueCoeffHard = 12500f;
        private const float fatigueCoeffNormal = 20000f;
    }

    /// <summary>
    /// Modifies the success chance for single PVs.
    /// </summary>
    [HarmonyPatch(typeof(singles._param), "GetSuccessChance", new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
    public class singles__param_GetSuccessChance
    {
        /// <summary>
        /// Postfix method to adjust the success chance for single PVs.
        /// </summary>
        /// <param name="__result">The original success chance, passed by reference for modification.</param>
        /// <param name="__instance">The instance of singles._param being patched.</param>
        /// <param name="Result">The result of the marketing roll.</param>
        /// <param name="Single">The single being evaluated.</param>
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, singles._single Single)
        {
            if (Single == null)
                return;

            float paramValue;

            switch (__instance.Special_Type)
            {
                case singles._param._special_type.lewd_pv:
                    paramValue = (Single.GetSenbatsuParamValue(data_girls._paramType.sexy) + Single.GetSenbatsuParamValue(data_girls._paramType.cute)) / 2f;
                    break;
                case singles._param._special_type.edgy_pv:
                    paramValue = (Single.GetSenbatsuParamValue(data_girls._paramType.cool) + Single.GetSenbatsuParamValue(data_girls._paramType.funny)) / 2f;
                    break;
                case singles._param._special_type.artsy_pv:
                    paramValue = (Single.GetSenbatsuParamValue(data_girls._paramType.pretty) + Single.GetSenbatsuParamValue(data_girls._paramType.smart)) / 2f;
                    break;
                default:
                    return;
            }

            __result = CalculateSuccessChance(paramValue, Result);
        }

        /// <summary>
        /// Calculates the success chance for a single based on the parameter value and marketing roll result.
        /// </summary>
        /// <param name="paramValue">The calculated parameter value for the single.</param>
        /// <param name="Result">The result of the marketing roll.</param>
        /// <returns>The calculated success chance as a float between 0 and 100.</returns>
        private static float CalculateSuccessChance(float paramValue, Single_Marketing_Roll._result Result)
        {
            return Result switch
            {
                Single_Marketing_Roll._result.success => paramValue * (1 - successCritCoeff),
                Single_Marketing_Roll._result.success_crit => paramValue * successCritCoeff,
                Single_Marketing_Roll._result.fail_crit => (100f - paramValue) * failCritCoeff,
                Single_Marketing_Roll._result.fail => 100f * (1 - failCritCoeff) - paramValue * (1 - failCritCoeff),
                _ => 0f
            };
        }

        private const float successCritCoeff = 0.1f;
        private const float failCritCoeff = 0.1f;
    }

    /// <summary>
    /// Modifies the success modifier for Fake Scandal single types.
    /// </summary>
    [HarmonyPatch(typeof(singles._param), "GetSuccessModifier", new Type[] { typeof(Single_Marketing_Roll._result), typeof(bool), typeof(int) })]
    public class singles__param_GetSuccessModifier
    {
        /// <summary>
        /// Postfix method to adjust the success modifier for different single types.
        /// </summary>
        /// <param name="__result">The original success modifier, passed by reference for modification.</param>
        /// <param name="__instance">The instance of singles._param being patched.</param>
        /// <param name="Result">The result of the marketing roll.</param>
        /// <param name="Secondary">Indicates if this is a secondary effect.</param>
        /// <param name="Level">The level of the single or marketing action.</param>
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, bool Secondary, int Level)
        {
            if (Result != Single_Marketing_Roll._result.success_crit)
                return;

            if (__instance.Special_Type == singles._param._special_type.fake_scandal)
            {
                if (!Secondary)
                {
                    __result = fakeScandalBase + Level * fakeScandalScaling;
                }
            }
            else if (__instance.Special_Type == singles._param._special_type.edgy_pv || __instance.Special_Type == singles._param._special_type.lewd_pv || __instance.Special_Type == singles._param._special_type.artsy_pv)
            {
                if (!Secondary)
                {
                    __result = PVPrimaryBase + Level * PVPrimaryScaling;
                }
                else
                {
                    __result = PVSecondaryBase + Level * PVSecondaryScaling;
                }
            }
        }

        private const float fakeScandalBase = 100f;
        private const float fakeScandalScaling = 290f;
        private const float PVPrimaryBase = 100f;
        private const float PVPrimaryScaling = 80f;
        private const float PVSecondaryBase = 50f;
        private const float PVSecondaryScaling = 30f;
    }


    /// <summary>
    /// Implements daily fan attrition.
    /// </summary>
    [HarmonyPatch(typeof(resources), "OnNewDay")]
    public class resources_OnNewDay
    {
        /// <summary>
        /// Postfix method to update fan count and apply daily fan churn.
        /// </summary>
        public static void Postfix()
        {
            UpdateFanCount();
            DailyFanChurn();
        }

        /// <summary>
        /// Transpiler method to modify the original fan change calculation.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stsfld && (FieldInfo)instructionList[i].operand == AccessTools.Field(typeof(resources), "FansChange"))
                {
                    //Debug.Log($"Fan Attrition Transpiler: FansChange code found at {i}");
                    index = i;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList[index].opcode = OpCodes.Nop;
                instructionList[index - 1].opcode = OpCodes.Nop;
                instructionList[index - 2].opcode = OpCodes.Nop;
            }

            return instructionList.AsEnumerable();
        }
    }

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
            if (__instance != null && __instance.gameObject.transform != null)
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

                __instance.gameObject.GetComponentsInChildren<Text>()[8].text = GetContractsLine(business._type.ad);
                __instance.gameObject.GetComponentsInChildren<Text>()[9].text = GetContractsLine(business._type.tv_drama);
                __instance.gameObject.GetComponentsInChildren<Text>()[10].text = GetShowLine(Shows._param._media_type.internet);
                __instance.gameObject.GetComponentsInChildren<Text>()[11].text = GetShowLine(Shows._param._media_type.tv);
                __instance.gameObject.GetComponentsInChildren<Text>()[12].text = GetShowLine(Shows._param._media_type.radio);
                __instance.gameObject.GetComponentsInChildren<Text>()[13].text = GetCafeLine();

                __instance.gameObject.GetComponentsInChildren<Text>()[14].text = GetChurnLine();

                var RenderFanChange = __instance.GetType().GetMethod("RenderFanChange", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                RenderFanChange.Invoke(__instance, null);
                LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.gameObject.GetComponent<RectTransform>());
                return false;
            }
            return true;
        }


        /// <summary>
        /// Generates the churn line for the fan tooltip.
        /// </summary>
        /// <returns>A formatted string representing the weekly fan churn.</returns>
        static string GetChurnLine()
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

        /// <summary>
        /// Generates the cafe line for the fan tooltip.
        /// </summary>
        /// <returns>A formatted string representing weekly fans gained from cafes.</returns>
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

        /// <summary>
        /// Generates a line for a specific fan type in the tooltip.
        /// </summary>
        /// <param name="FanType">The type of fan to generate the line for.</param>
        /// <returns>A formatted string with fan type information and appeal.</returns>
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

        /// <summary>
        /// Generates a line for contract-based fans in the tooltip.
        /// </summary>
        /// <param name="Type">The type of business contract.</param>
        /// <returns>A formatted string with contract-based fan information.</returns>
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

        /// <summary>
        /// Generates a line for show-based fans in the tooltip.
        /// </summary>
        /// <param name="Type">The type of media for the show.</param>
        /// <returns>A formatted string with show-based fan information.</returns>
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
        public static bool Prefix(tooltip_fans __instance)
        {
            //var AddPadding = __instance.GetType().GetMethod("AddPadding", BindingFlags.NonPublic | BindingFlags.Instance);

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

            return true;
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
            return;
        }
    }


    /// <summary>
    /// Utility class containing helper methods for fan attrition mechanics.
    /// </summary>
    public class Utility
    {
        public static long adFans = 0;
        public static long dramaFans = 0;
        public static long tvFans = 0;
        public static long radioFans = 0;
        public static long netFans = 0;
        public static long cafeFans = 0;

        private const float churnPowerHard = 0.83f;
        private const float churnCoeffHard = 0.012f;
        private const float churnOffsetHard = 2f;
        private const float churnPowerNormal = 0.75f;
        private const float churnCoeffNormal = 0.012f;
        private const float churnOffsetNormal = 0f;

        /// <summary>
        /// Applies daily fan churn based on game difficulty.
        /// </summary>
        public static void DailyFanChurn()
        {
            if(staticVars.IsEasy())
                return;

            long fansTotal;

            resources.Add(resources.type.fans, resources.FansChange);
            fansTotal = resources.GetFansTotal();

            float churn = staticVars.PlayerData.Difficulty switch
            {
                staticVars._playerData._difficulty.hard => Mathf.Pow(fansTotal, churnPowerHard) * churnCoeffHard + churnOffsetHard,
                staticVars._playerData._difficulty.normal => Mathf.Pow(fansTotal, churnPowerNormal) * churnCoeffNormal + churnOffsetNormal,
                _ => 0f
            };

            if (churn > 0f)
            {
                resources.FansChange = -(long)Mathf.Ceil(churn);
            }
            else
            {
                resources.FansChange = 0L;
            }
        }

        /// <summary>
        /// Updates fan counts from various sources.
        /// </summary>
        public static void UpdateFanCount()
        {
            adFans = 0;
            dramaFans = 0;
            tvFans = 0;
            radioFans = 0;
            netFans = 0;
            cafeFans = 0;
            foreach (business.active_proposal active_proposal in Camera.main.GetComponent<mainScript>().Data.GetComponent<business>().ActiveProposals)
            {
                if (active_proposal.Fans_per_week > 0)
                {
                    if (active_proposal.Type == business._type.ad)
                    {
                        adFans += active_proposal.Fans_per_week;
                    }
                    else if (active_proposal.Type == business._type.tv_drama)
                    {
                        dramaFans += active_proposal.Fans_per_week;
                    }
                }
            }

            foreach (Shows._show show in Shows.shows)
            {
                if (show.status != Shows._show._status.normal && show.status != Shows._show._status.working && show.status != Shows._show._status.canceled)
                {
                    if (show.medium.media_type == Shows._param._media_type.tv)
                    {
                        tvFans += show.fans[show.fans.Count - 1];
                    }
                    else if (show.medium.media_type == Shows._param._media_type.radio)
                    {
                        radioFans += show.fans[show.fans.Count - 1];
                    }
                    else if (show.medium.media_type == Shows._param._media_type.internet)
                    {
                        netFans += show.fans[show.fans.Count - 1];
                    }
                }
            }

            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                int dayCount = 0;
                for (int i = cafe.Stats.Count - 1; i >= 0; i--)
                {
                    cafeFans += cafe.Stats[i].New_Fans;
                    dayCount++;
                    if (dayCount >= 7)
                    {
                        break;
                    }
                }
            }

        }
    }

}
