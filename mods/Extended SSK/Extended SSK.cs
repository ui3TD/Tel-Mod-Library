using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using static ExtendedSSK.ExtendedSSK;

namespace ExtendedSSK
{
    public class ExtendedSSK
    {
        public const string varID = "ExtendedSSK_Limit";
        public const int defaultRankings = 64;
        // The lowest place an idol wishes for in an election; a lower wish is raised to it.
        public const int MAX_WISH_RANK = 16;

        /// <summary>
        /// The configured number of ranks. A missing or unreadable value (e.g. a hand-edited save) gives the default,
        /// so the election results can still be generated.
        /// </summary>
        public static int GetLimit()
        {
            return int.TryParse(variables.Get(varID), NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) ? limit : defaultRankings;
        }
    }

    /// <summary>
    /// Patches the GenerateResults method of SEvent_SSK._SSK class to extend the idol limit.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK))]
    [HarmonyPatch(nameof(SEvent_SSK._SSK.GenerateResults))]
    public static class SSK_GenerateResultsPatch
    {
        /// <summary>
        /// Transpiler method to modify the IL code of the original method.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions with extended idol limit.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo infix = AccessTools.Method(typeof(SSK_GenerateResultsPatch), nameof(Infix));

            // The game's 10-rank limit: i < 10, then the loop's branch. Infix replaces the 10 with the setting.
            CodeMatcher matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => ci.LoadsConstant(10)),
                new CodeMatch(ci => ci.opcode == OpCodes.Blt || ci.opcode == OpCodes.Blt_S || ci.Calls(infix)));
            if (matcher.IsInvalid)
            {
                Debug.LogError("[Extended SSK] Couldn't find the game's 10-rank limit in GenerateResults; elections keep the game's or the other mod's rank limit");
                return instructions;
            }
            if (matcher.InstructionAt(1).Calls(infix))
            {
                // Already replaced: this patch ran twice
                return matcher.InstructionEnumeration();
            }

            return matcher.Advance(1).Insert(new CodeInstruction(OpCodes.Call, infix)).InstructionEnumeration();
        }


        /// <summary>
        /// Infix method to replace the hardcoded idol limit with a configurable value.
        /// </summary>
        /// <param name="i">The original limit value (unused).</param>
        /// <returns>The new configurable limit for idols.</returns>
        public static int Infix(int i)
        {
            int limit = GetLimit();
            return limit;
        }
    }

    /// <summary>
    /// Patches the RecalcFameBonus method of SEvent_SSK._SSK class to adjust fame bonuses for extended idol count.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK))]
    [HarmonyPatch(nameof(SEvent_SSK._SSK.RecalcFameBonus))]
    public static class SSK_RecalcFameBonusPatch
    {
        // The game's private GetFameBaseVal (fame for 1st place, by broadcast tier), as a typed delegate
        static readonly Func<SEvent_SSK._SSK, int> GetFameBaseVal =
            AccessTools.MethodDelegate<Func<SEvent_SSK._SSK, int>>(AccessTools.Method(typeof(SEvent_SSK._SSK), nameof(SEvent_SSK._SSK.GetFameBaseVal)));

        /// <summary>
        /// Postfix method to recalculate fame bonuses after the original method execution.
        /// Runs last and only adds the places the list is missing, so values another mod set are kept.
        /// </summary>
        /// <param name="__instance">The instance of SEvent_SSK._SSK being patched.</param>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_SSK._SSK __instance)
        {
            int girlCount = 0;
            foreach (data_girls.girls girls2 in data_girls.girl)
            {
                if (girls2.CanParticipateInSSK())
                {
                    girlCount++;
                }
            }
            if(girlCount > 10)
            {
                int target = Math.Min(girlCount, GetLimit());
                List<int> list = __instance.FameBonus ?? new List<int>();

                int fameBaseVal = GetFameBaseVal(__instance);
                for (int i = list.Count; i < target; i++)
                {
                    // Places 1-10 are only missing if another mod shortened the list: the game's rule fills them
                    if (i == 0)
                        list.Add(fameBaseVal);
                    else if (i == 10)
                        list.Add(Rank11(list[9], fameBaseVal));
                    else
                        list.Add(Mathf.RoundToInt(list[i - 1] * 0.75f));
                }
                __instance.FameBonus = list;
            }
        }

        /// <summary>
        /// 5.6% of the broadcast's base fame. If another mod changed place 10, scaled by the same ratio.
        /// </summary>
        private static int Rank11(int place10, int fameBaseVal)
        {
            int rank11 = Mathf.RoundToInt(fameBaseVal * 0.056f);

            // The game's place 10, as RecalcFameBonus builds it
            int gamePlace10 = fameBaseVal;
            for (int i = 0; i < 9; i++)
                gamePlace10 = Mathf.RoundToInt(gamePlace10 * 0.75f);

            if (place10 == gamePlace10 || gamePlace10 == 0)
                return rank11;
            return (int)Math.Round((double)rank11 * place10 / gamePlace10);
        }
    }


    [HarmonyPatch(typeof(girl_wishes))]
    [HarmonyPatch(nameof(girl_wishes.GenerateWish))]
    public static class girl_wishes_GenerateWish
    {
        public static void Postfix(data_girls.girls Girl)
        {
            if (Girl.Wish_Type != girl_wishes._type.ssk_rank)
                return;

            int ranking = int.TryParse(Girl.Wish_Formula, out int r) ? r : 10;
            if (ranking > MAX_WISH_RANK)
            {
                Girl.Wish_Formula = MAX_WISH_RANK.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

}
