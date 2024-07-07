using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace StaleTheater
{

    // Theater show sales and subscriptions start to decay after 30 days
    [HarmonyPatch(typeof(Theaters._theater), "GetPriceCoeff")]
    public class Theaters__theater_GetPriceCoeff
    {
        public static void Postfix(int Price, Theaters._theater __instance, ref float __result)
        {
            float output = __result;
            if (Price <= 30000 && !staticVars.IsEasy())
            {
                int daysSinceSingle;
                LinearFunction._function linear = new LinearFunction._function();
                if(singles.GetLatestReleasedSingle(false, __instance.GetGroup()) != null)
                {
                    daysSinceSingle = (staticVars.dateTime - singles.GetLatestReleasedSingle(false, __instance.GetGroup()).ReleaseData.ReleaseDate).Days;
                }
                else
                {
                    daysSinceSingle = __instance.GetGroup().GetDaysSinceCreation();
                }
                if (daysSinceSingle > 30)
                {
                    if (staticVars.IsHard())
                    {
                        linear.Init(30f, 1f, 183f, 0.1f);
                        output *= linear.GetY((float)daysSinceSingle);
                    }
                    else if(staticVars.IsNormal())
                    {
                        linear.Init(30f, 1f, 366f, 0.1f);
                        output *= linear.GetY((float)daysSinceSingle);
                    }
                }
                if (output < 0.001f)
                {
                    output = 0.001f;
                }
            }
            __result = output;
        }
    }

    // Theater subscription revenue decreased by 90% / 70%
    [HarmonyPatch(typeof(Theaters._theater), "GetSubRevenue")]
    public class Theaters__theater_GetSubRevenue
    {
        public static void Postfix(ref long __result)
        {
            if (staticVars.IsHard())
            {
                __result = (long)Mathf.Round(__result * 0.1f);
            }
            else if(staticVars.IsNormal())
            {
                __result = (long)Mathf.Round(__result * 0.3f);
            }
        }
    }

    // Theater attendance increased for 'everyone', 'casual' and age groups
    [HarmonyPatch(typeof(Theaters._theater), "GetNumberOfVisitors")]
    public class Theaters__theater_GetNumberOfVisitors
    {
        //public static void Postfix(ref int __result, Theaters._theater __instance)
        //{

        //    if (__instance.Ticket_Price <= 40000)
        //    {
        //        float multiplier = 1;
        //        Groups._group group = __instance.GetGroup();
        //        Theaters._theater._schedule schedule = __instance.GetSchedule();
        //        long fans;
        //        if (schedule.FanType_Everyone)
        //        {
        //            fans = (long)group.GetFansOfType(null);
        //        }
        //        else
        //        {
        //            fans = (long)group.GetFansOfType(new resources.fanType?(schedule.FanType));
        //        }
        //        if (schedule.FanType_Everyone && __instance.Doing_Now == Theaters._theater._schedule._type.manzai)
        //        {
        //            multiplier = 0.2f;
        //        }
        //        else if (schedule.FanType_Everyone)
        //        {
        //            multiplier = 0.3f;
        //        }
        //        else if (schedule.FanType == resources.fanType.casual)
        //        {
        //            multiplier = 0.65f;
        //        }
        //        else if (schedule.FanType == resources.fanType.adult || schedule.FanType == resources.fanType.youngAdult)
        //        {
        //            multiplier = 0.95f;
        //        }
        //        else if (schedule.FanType == resources.fanType.teen)
        //        {
        //            multiplier = 0.9f;
        //        }
        //        else if (schedule.FanType != resources.fanType.hardcore)
        //        {
        //            multiplier = 0.75f;
        //        }
        //        if (!schedule.FanType_Everyone && __instance.Doing_Now == Theaters._theater._schedule._type.manzai && schedule.FanType != resources.fanType.hardcore)
        //        {
        //            multiplier *= 0.75f;
        //        }
        //        if (__instance.Doing_Now == Theaters._theater._schedule._type.manzai)
        //        {
        //            multiplier /= 2f;
        //        }
        //        var GetPriceCoeff = __instance.GetType().GetMethod("GetPriceCoeff", BindingFlags.NonPublic | BindingFlags.Instance);
        //        multiplier *= (float)GetPriceCoeff.Invoke(__instance, new object[] { __instance.Ticket_Price });
        //        fans = (long)Mathf.Round((float)fans * multiplier);
        //        LinearFunction._function function = new LinearFunction._function();
        //        function.Init(0f, 0f, 20000f, 100f);
        //        int attendees = Mathf.RoundToInt(function.GetY((float)fans));
        //        if (attendees > __instance.GetCapacity())
        //        {
        //            __result = __instance.GetCapacity();
        //            return;
        //        }
        //        if (attendees < 0)
        //        {
        //            __result = 0;
        //            return;
        //        }
        //        __result = attendees;
        //    }
        //}
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ret)
                {
                    index = i;
                }
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(Theaters._theater), "GetPriceCoeff"))
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Theaters__theater_GetNumberOfVisitors), "Infix")));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Stloc_0));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(Theaters._theater __this)
        {
            float multiplier = 1;
            Theaters._theater._schedule schedule = __this.GetSchedule();
            if (schedule.FanType_Everyone && __this.Doing_Now == Theaters._theater._schedule._type.manzai)
            {
                multiplier = 0.2f;
            }
            else if (schedule.FanType_Everyone)
            {
                multiplier = 0.3f;
            }
            else if (schedule.FanType == resources.fanType.casual)
            {
                multiplier = 0.65f;
            }
            else if (schedule.FanType == resources.fanType.adult || schedule.FanType == resources.fanType.youngAdult)
            {
                multiplier = 0.95f;
            }
            else if (schedule.FanType == resources.fanType.teen)
            {
                multiplier = 0.9f;
            }
            else if (schedule.FanType != resources.fanType.hardcore)
            {
                multiplier = 0.75f;
            }
            if (!schedule.FanType_Everyone && __this.Doing_Now == Theaters._theater._schedule._type.manzai && schedule.FanType != resources.fanType.hardcore)
            {
                multiplier *= 0.75f;
            }
            if (__this.Doing_Now == Theaters._theater._schedule._type.manzai)
            {
                multiplier /= 2f;
            }
            return multiplier;
        }
    }

}
