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
                float mcCoeff = Mathf.Max(1f, 1f + __this.mc.fame * __this.mc.fame / 10);
                if (__this.mc.fame >= 10)
                {
                    mcCoeff += MC_MAX_FAME_BONUS;
                }
                num6 = Mathf.RoundToInt(num6 * mcCoeff);
            }


            return num6;
        }

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
                num2 *= 1f - __this.GetFatigue() * __this.GetFatigue() / SHOW_FATIGUE_COEFF_HARD;
            }
            else if (staticVars.IsNormal())
            {
                num2 *= 1f - __this.GetFatigue() * __this.GetFatigue() / SHOW_FATIGUE_COEFF_NORMAL;
            }

            return num2;
        }

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
                Single_Marketing_Roll._result.success => paramValue * (1 - MARK_SUCCESSCRIT_COEFF),
                Single_Marketing_Roll._result.success_crit => paramValue * MARK_SUCCESSCRIT_COEFF,
                Single_Marketing_Roll._result.fail_crit => (100f - paramValue) * MARK_FAIL_CRIT_COEFF,
                Single_Marketing_Roll._result.fail => (100f - paramValue) * (1 - MARK_FAIL_CRIT_COEFF),
                _ => 0f
            };
        }
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
                    __result = FAKESCANDAL_MOD_BASE + Level * FAKESCANDAL_MOD_SCALING;
                }
            }
            else if (__instance.Special_Type == singles._param._special_type.edgy_pv || __instance.Special_Type == singles._param._special_type.lewd_pv || __instance.Special_Type == singles._param._special_type.artsy_pv)
            {
                if (!Secondary)
                {
                    __result = PVPRIMARY_MOD_BASE + Level * PVPRIMARY_MOD_SCALING;
                }
                else
                {
                    __result = PVSECONDARY_MOD_BASE + Level * PVSECONDARY_MOD_SCALING;
                }
            }
        }
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

        private const float CHURN_POWER_HARD = 0.83f;
        private const float CHURN_COEFF_HARD = 0.012f;
        private const float CHURN_OFFSET_HARD = 2f;
        private const float CHURN_POWER_NORMAL = 0.75f;
        private const float CHURN_COEFF_NORMAL = 0.012f;
        private const float CHURN_OFFSET_NORMAL = 0f;

        public const float MC_MAX_FAME_BONUS = 5f;
        public const float SHOW_FATIGUE_COEFF_HARD = 12500f;
        public const float SHOW_FATIGUE_COEFF_NORMAL = 20000f;
        public const float MARK_SUCCESSCRIT_COEFF = 0.1f;
        public const float MARK_FAIL_CRIT_COEFF = 0.1f;
        public const float FAKESCANDAL_MOD_BASE = 100f;
        public const float FAKESCANDAL_MOD_SCALING = 290f;
        public const float PVPRIMARY_MOD_BASE = 100f;
        public const float PVPRIMARY_MOD_SCALING = 80f;
        public const float PVSECONDARY_MOD_BASE = 50f;
        public const float PVSECONDARY_MOD_SCALING = 30f;

        public const string CHURNRATE_LABEL = "CHURN";
        public const string TOOLTIP_OFTOTAL_LABEL = "TIP__OF_TOTAL";
        public const string TOOLTIP_AD_LABEL = "TIP__AD";
        public const string TOOLTIP_DRAMA_LABEL = "TIP__DRAMA";
        public const string TOOLTIP_TV_LABEL = "TIP__TV";
        public const string TOOLTIP_INTERNET_LABEL = "TIP__INTERNET";
        public const string TOOLTIP_RADIO_LABEL = "TIP__RADIO";

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
                staticVars._playerData._difficulty.hard => Mathf.Pow(fansTotal, CHURN_POWER_HARD) * CHURN_COEFF_HARD + CHURN_OFFSET_HARD,
                staticVars._playerData._difficulty.normal => Mathf.Pow(fansTotal, CHURN_POWER_NORMAL) * CHURN_COEFF_NORMAL + CHURN_OFFSET_NORMAL,
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

            mainScript mainScript = Camera.main.GetComponent<mainScript>();
            business businessComponent = mainScript.Data.GetComponent<business>();

            foreach (business.active_proposal activeProposal in businessComponent.ActiveProposals)
            {
                if (activeProposal.Fans_per_week <= 0) continue;

                switch (activeProposal.Type)
                {
                    case business._type.ad:
                        adFans += activeProposal.Fans_per_week;
                        break;
                    case business._type.tv_drama:
                        dramaFans += activeProposal.Fans_per_week;
                        break;
                }
            }

            foreach (Shows._show show in Shows.shows)
            {
                if (show.status != Shows._show._status.normal &&
                    show.status != Shows._show._status.working &&
                    show.status != Shows._show._status.canceled)
                {
                    int lastFans = show.fans.Last();
                    switch (show.medium.media_type)
                    {
                        case Shows._param._media_type.tv:
                            tvFans += lastFans;
                            break;
                        case Shows._param._media_type.radio:
                            radioFans += lastFans;
                            break;
                        case Shows._param._media_type.internet:
                            netFans += lastFans;
                            break;
                    }
                }
            }

            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                for (int i = Math.Max(0, cafe.Stats.Count - 7); i < cafe.Stats.Count; i++)
                {
                    cafeFans += cafe.Stats[i].New_Fans;
                }
            }

        }
    }

}
