using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Linq;
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
        /// <remarks>
        /// The game stores each fan type's audience (num6) in sales.sales (as a long) and then uses it only to work
        /// out that fan type's new fans. Boosting it right after that store boosts new fans but not the audience.
        /// </remarks>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new CodeMatcher(instructions).MatchEndForward(
                new CodeMatch(ci => ci.IsLdloc()),
                new CodeMatch(OpCodes.Conv_I8),
                new CodeMatch(ci => ci.StoresField(AccessTools.Field(typeof(singles._single._sales), nameof(singles._single._sales.sales)))));
            if (matcher.IsInvalid)
            {
                LogPatchNotFound("the audience store for the MC bonus");
                return instructions;
            }

            CodeInstruction loadAudience = matcher.InstructionAt(-2);
            matcher.Advance(1).Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(loadAudience.opcode, loadAudience.operand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Shows__show_SetSales_MC), nameof(Infix))),
                StoreTo(loadAudience));

            return matcher.InstructionEnumeration();
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
                float mcCoeff = Mathf.Max(1f, 1f + __this.mc.fame * __this.mc.fame / 10f);
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
        /// <remarks>
        /// The game works out the base audience (num2) as (... + GetAllNewFans()) / 12 and stores it. The fatigue
        /// is applied to that value on the stack, just before the store.
        /// </remarks>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new CodeMatcher(instructions)
                .MatchEndForward(new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(Shows._show), "GetAllNewFans"))))
                .MatchEndForward(new CodeMatch(ci => ci.IsStloc()));
            if (matcher.IsInvalid)
            {
                LogPatchNotFound("the base audience for show fatigue");
                return instructions;
            }

            matcher.Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Shows__show_SetSales_Fatigue), nameof(Infix))));

            return matcher.InstructionEnumeration();
        }

        /// <summary>
        /// Calculates the modified fan count based on show fatigue.
        /// </summary>
        /// <param name="num2">The original fan count.</param>
        /// <param name="__this">The current show instance.</param>
        /// <returns>The modified fan count.</returns>
        public static float Infix(float num2, Shows._show __this)
        {
            if (__this.medium != null && __this.medium.media_type == Shows._param._media_type.internet)
            {
                return num2;
            }

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
    [HarmonyPatch(typeof(resources), nameof(resources.OnNewDay))]
    public class resources_OnNewDay
    {
        /// <summary>
        /// Postfix method to apply daily fan churn.
        /// </summary>
        public static void Postfix()
        {
            DailyFanChurn();
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
        public static Action<tooltip_fans> RenderFanChangeDelegate;

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
            long churn = GetDailyChurn();
            if (churn > 0)
            {
                resources.Add(resources.type.fans, -churn);
            }
        }

        /// <summary>
        /// Works out how many fans the agency loses in a day at its current fan count.
        /// </summary>
        /// <remarks>
        /// It's worked out from the current fans whenever it's needed rather than stored, so a value from
        /// another save can't carry over after a load.
        /// </remarks>
        /// <returns>The number of fans lost, 0 in Easy mode.</returns>
        public static long GetDailyChurn()
        {
            long fansTotal = resources.GetFansTotal();

            float churn = staticVars.PlayerData.Difficulty switch
            {
                staticVars._playerData._difficulty.hard => Mathf.Pow(fansTotal, CHURN_POWER_HARD) * CHURN_COEFF_HARD + CHURN_OFFSET_HARD,
                staticVars._playerData._difficulty.normal => Mathf.Pow(fansTotal, CHURN_POWER_NORMAL) * CHURN_COEFF_NORMAL + CHURN_OFFSET_NORMAL,
                _ => 0f
            };

            return churn > 0f ? (long)Mathf.Ceil(churn) : 0L;
        }

        /// <summary>
        /// Logs that a transpiler couldn't find the code it changes, which leaves that change out.
        /// </summary>
        /// <param name="what">What the transpiler looked for.</param>
        public static void LogPatchNotFound(string what)
        {
            Debug.LogError("[Fan Attrition] Couldn't find " + what + " in the game's code, so that change is off. The game may have been updated.");
        }

        /// <summary>
        /// Makes the instruction that stores into the local variable an ldloc instruction reads.
        /// </summary>
        /// <param name="load">An ldloc instruction.</param>
        /// <returns>The matching stloc instruction.</returns>
        public static CodeInstruction StoreTo(CodeInstruction load)
        {
            if (load.opcode == OpCodes.Ldloc_0) return new CodeInstruction(OpCodes.Stloc_0);
            if (load.opcode == OpCodes.Ldloc_1) return new CodeInstruction(OpCodes.Stloc_1);
            if (load.opcode == OpCodes.Ldloc_2) return new CodeInstruction(OpCodes.Stloc_2);
            if (load.opcode == OpCodes.Ldloc_3) return new CodeInstruction(OpCodes.Stloc_3);
            if (load.opcode == OpCodes.Ldloc_S) return new CodeInstruction(OpCodes.Stloc_S, load.operand);
            return new CodeInstruction(OpCodes.Stloc, load.operand);
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
                    // Released shows always have an episode, but a damaged save or another mod could leave none
                    if (show.fans.Count == 0) continue;

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
