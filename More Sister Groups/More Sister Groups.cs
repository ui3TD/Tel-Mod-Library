﻿using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace SisterGroups
{

    //fan gain from sister groups reduced 3x
    [HarmonyPatch(typeof(Groups._group), "GetNewFansPerSingle")]
    public class Groups__group_GetNewFansPerSingle
    {
        public static void Postfix(ref int __result, Groups._group __instance)
        {
            if(staticVars.IsHard())
            {
                __result = Mathf.RoundToInt((float)__result / 50 * Math.Min(10, __instance.GetGirls().Count));
            }
        }
    }


    // Removed minimum member limit for sister group creation
    [HarmonyPatch(typeof(Groups), "GetIdolsNeededForNewGroup")]
    public class Groups_GetIdolsNeededForNewGroup
    {
        public static void Postfix(ref int __result)
        {
            __result = Groups.CountActiveGroups() + 1;
        }

    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group
    [HarmonyPatch(typeof(singles), "AddOpinion")]
    public class singles_AddOpinion
    {
        public static bool Prefix(singles._single single)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            Utility.groupSales = single.GetGroup();
            return true;
        }

        public static void Postfix()
        {
            Utility.groupSales = null;
        }
    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group
    [HarmonyPatch(typeof(singles), "GenerateSales")]
    public class singles_GenerateSales
    {
        public static bool Prefix(singles._single single)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            Utility.groupSales = single.GetGroup();
            return true;
        }

        public static void Postfix()
        {
            Utility.groupSales = null;
        }
    }

    // When releasing a single, the penalty for a decrease in fame and appeal now only considers past singles of the same group
    [HarmonyPatch(typeof(singles), "GetLatestReleasedSingles")]
    public class singles_GetLatestReleasedSingles
    {
        public static void Postfix(int count, ref List<singles._single> __result)
        {
            if(Utility.groupSales != null)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                __result = Utility.GetLatestReleasedSingles(count, Utility.groupSales);
            }
        }
    }

    public class Utility
    {
        public static Groups._group groupSales = null;

        // This method gets latest released singles of a given group. It is based on singles.GetReleasedSingles(int count)
        // which does not take group as a parameter.
        public static List<singles._single> GetLatestReleasedSingles(int count, Groups._group group = null)
        {
            if (group == null)
            {
                return singles.GetLatestReleasedSingles(count);
            }
            List<singles._single> latestReleasedSingles = new();
            for (int i = singles.Singles.Count - 1; i >= 0; i--)
            {
                if (singles.Singles[i].status == singles._single._status.released && singles.Singles[i].GetGroup() == group)
                {
                    latestReleasedSingles.Add(singles.Singles[i]);
                    if (latestReleasedSingles.Count >= count)
                    {
                        break;
                    }
                }
            }
            return latestReleasedSingles;
        }

        // This method compares fan appeals in order to sort them. This is a duplicate of an internal method
        // in the class singles so that it can be referenced publicly.
        public static int CompareAppeal(singles._fanAppeal x, singles._fanAppeal y)
        {
            return y.ratio.CompareTo(x.ratio);
        }
    }


}
