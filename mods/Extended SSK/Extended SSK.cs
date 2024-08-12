using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using static ExtendedSSK.ExtendedSSK;

namespace ExtendedSSK
{
    public class ExtendedSSK
    {
        public const string varID = "ExtendedSSK_Limit";
        public const string defaultRankingsStr = "64";
    }

    /// <summary>
    /// Patches the GenerateResults method of SEvent_SSK._SSK class to extend the idol limit.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK))]
    [HarmonyPatch("GenerateResults")]
    public static class SSK_GenerateResultsPatch
    {
        /// <summary>
        /// Transpiler method to modify the IL code of the original method.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>Modified IL instructions with extended idol limit.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count - 1; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldc_I4_S && (sbyte)instructionList[i].operand == 10 && instructionList[i + 1].opcode == OpCodes.Blt)
                {
                    index = i;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SSK_GenerateResultsPatch), "Infix")));
            }

            return instructionList.AsEnumerable();
        }


        /// <summary>
        /// Infix method to replace the hardcoded idol limit with a configurable value.
        /// </summary>
        /// <param name="i">The original limit value (unused).</param>
        /// <returns>The new configurable limit for idols.</returns>
        public static int Infix(int i)
        {
            int limit = int.Parse(variables.Get(varID) ?? defaultRankingsStr);
            return limit;
        }
    }

    /// <summary>
    /// Patches the RecalcFameBonus method of SEvent_SSK._SSK class to adjust fame bonuses for extended idol count.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK))]
    [HarmonyPatch("RecalcFameBonus")]
    public static class SSK_RecalcFameBonusPatch
    {
        /// <summary>
        /// Postfix method to recalculate fame bonuses after the original method execution.
        /// </summary>
        /// <param name="__instance">The instance of SEvent_SSK._SSK being patched.</param>
        public static void Postfix(ref SEvent_SSK._SSK __instance)
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
                int limit = int.Parse(variables.Get(varID) ?? defaultRankingsStr);
                List<int> list = __instance.FameBonus;

                var GetFameBaseVal = __instance.GetType().GetMethod("GetFameBaseVal", BindingFlags.NonPublic | BindingFlags.Instance);
                int num = Mathf.RoundToInt((int)GetFameBaseVal.Invoke(__instance, null) * 0.056f);
                for (int i = 10; i < Math.Min(girlCount, limit); i++)
                {
                    list.Add(num);
                    num = Mathf.RoundToInt(num * 0.75f);
                }
                __instance.FameBonus = list;
            }
        }
    }


    [HarmonyPatch(typeof(girl_wishes))]
    [HarmonyPatch("GenerateWish")]
    public static class girl_wishes_GenerateWish
    {
        public static void Postfix(data_girls.girls Girl)
        {
            if(Girl.Wish_Type == girl_wishes._type.ssk_rank)
            {
                int ranking = int.TryParse(Girl.Wish_Formula, out int r) ? r : 10;
                if(ranking > 16)
                {
                    Girl.Wish_Formula = "16";
                }

            }
        }
    }

}
