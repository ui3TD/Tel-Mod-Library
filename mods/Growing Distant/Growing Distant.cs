using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace GrowingDistant
{
    /// <summary>
    /// This class contains a patch for updating the relationship weekly based on salary.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "UpdateRelationshipBasedOnSalary")]
    public class data_girls_girls_UpdateRelationshipBasedOnSalary
    {
        public const int influenceBonus = 1;
        public const int influencePenalty = -1;
        public const int influencePenaltyCrit = -2;
        public const int salaryThreshUpper = 150;
        public const int salaryThreshMid = 100;
        public const int salaryThreshLower = 50;
        public const int romancePenalty = -1;
        public const int friendshipPenalty = -2;

        /// <summary>
        /// Harmony patch for the UpdateRelationshipBasedOnSalary method in data_girls.girls.
        /// </summary>
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (staticVars.IsEasy())
                return;

            int salarySatisfaction_Percentage = __instance.GetSalarySatisfaction_Percentage();
            if (salarySatisfaction_Percentage >= salaryThreshUpper)
            {
                __instance.Rel_Influence_Points += influenceBonus;
            }
            else if (salarySatisfaction_Percentage <= salaryThreshLower && __instance.Rel_Influence_Points >= -influencePenaltyCrit)
            {
                __instance.Rel_Influence_Points += influencePenaltyCrit;
            }
            else if (salarySatisfaction_Percentage <= salaryThreshMid && __instance.Rel_Influence_Points >= -influencePenalty)
            {
                __instance.Rel_Influence_Points += influencePenalty;
            }
            if (__instance.Rel_Romance_Points >= -romancePenalty)
            {
                __instance.Rel_Romance_Points += romancePenalty;
            }
            if (__instance.Rel_Friendship_Points >= -friendshipPenalty)
            {
                __instance.Rel_Friendship_Points += friendshipPenalty;
            }
        }
    }

}
