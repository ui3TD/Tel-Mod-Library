using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using static GoingViral.TrendingManager;
using UnityEngine;

namespace GoingViral
{


    // Apply fans increase to singles
    [HarmonyPatch(typeof(singles), "GenerateSales")]
    public class singles_GenerateSales
    {
        public static void Postfix(singles._single single)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                long totalNewFans = 0;
                long casualNewFans = 0;
                foreach (singles._single._sales sale in single.sales)
                {
                    totalNewFans += sale.new_fans;
                    if (sale.fan.IsType(resources.fanType.casual))
                    {
                        casualNewFans += sale.new_fans;
                    }
                }
                foreach (singles._single._sales sale in single.sales)
                {
                    if (sale.fan.IsType(resources.fanType.casual))
                    {
                        sale.new_fans = (long)Math.Round(sale.new_fans * totalNewFans / casualNewFans * GetTrendingCoeff());
                    }
                }
            }
        }
    }

    // Apply fans increase to shows
    [HarmonyPatch(typeof(Shows._show), "SetSales")]
    public class Shows__show_SetSales
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object newFansOperand = null;
            object fanOperand = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 12)
                {
                    index = i;
                    newFansOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Ldloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 7)
                {
                    fanOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(data_girls), "AddFans_Equally", new Type[] { typeof(long), typeof(resources._fan), typeof(List<data_girls.girls>) }))
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, fanOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Shows__show_SetSales), "Infix")));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Stloc_S, newFansOperand));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Ldloc_S, newFansOperand));
            }

            return instructionList.AsEnumerable();
        }

        public static int Infix(int fanCount, resources._fan fan)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                if (fan.IsType(resources.fanType.casual))
                {
                    fanCount = Mathf.RoundToInt(fanCount * GetTrendingCoeff());
                }
            }
            return fanCount;
        }
    }

    // Apply fans increase to businesses
    [HarmonyPatch(typeof(business._proposal), "set_newFans")]
    public class business__proposal_set_newFans
    {
        public static void Postfix(ref business._proposal __instance)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                __instance._newFans = Mathf.RoundToInt(__instance._newFans * GetTrendingCoeff());
            }
        }
    }


    // Reduce fans_per_week to orig value for contracts
    [HarmonyPatch(typeof(business), "AddActiveProposal")]
    public class business_AddActiveProposal
    {
        public static void Postfix(ref business __instance)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                __instance.ActiveProposals[__instance.ActiveProposals.Count - 1].Fans_per_week = Mathf.RoundToInt(__instance.ActiveProposals[__instance.ActiveProposals.Count - 1].Fans_per_week / GetTrendingCoeff());
            }
        }
    }

    // Apply fans increase to business contracts
    [HarmonyPatch(typeof(business), "DoWeeklyFans")]
    public class business_DoWeeklyFans
    {
        public static void Postfix(ref business __instance)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                foreach (business.active_proposal active_proposal in __instance.ActiveProposals)
                {
                    if (active_proposal.Fans_per_week > 0)
                    {
                        active_proposal.Girl.AddFans(Mathf.RoundToInt(active_proposal.Fans_per_week * (GetTrendingCoeff() - 1)), null);
                    }
                }
            }
        }
    }

    // Apply fans increase to business contracts
    [HarmonyPatch(typeof(Contracts_Line), "Set")]
    public class Contracts_Line_Set
    {
        public static void Postfix(ref Contracts_Line __instance)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                business.active_proposal ActiveProposal = Traverse.Create(__instance).Field("ActiveProposal").GetValue() as business.active_proposal;
                ExtensionMethods.SetText(__instance.NewFans, ExtensionMethods.formatNumber(Mathf.RoundToInt(ActiveProposal.Fans_per_week * GetTrendingCoeff()), false, false));
            }
        }
    }

    // Apply fans increase to tour
    [HarmonyPatch(typeof(SEvent_Tour.tour), "GetNewFansByAttendance")]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            if (IsTrending() == TrendingStatus.trending)
            {
                __result = Mathf.RoundToInt(__result * GetTrendingCoeff());
            }
        }
    }
}
