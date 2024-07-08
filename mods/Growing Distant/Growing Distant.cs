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
        public const int INFLUENCE_BONUS = 1;
        public const int INFLUENCE_PENALTY = -1;
        public const int INFLUENCE_PENALTYCRIT = -2;
        public const int SALARY_UPPER_THR = 150;
        public const int SALARY_MID_THR = 100;
        public const int SALARY_LOWER_THR = 50;
        public const int ROMANCE_PENALTY = -1;
        public const int FRIEND_PENALTY = -2;

        /// <summary>
        /// Harmony patch for the UpdateRelationshipBasedOnSalary method in data_girls.girls.
        /// </summary>
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (staticVars.IsEasy())
                return;

            int salarySatisfaction_Percentage = __instance.GetSalarySatisfaction_Percentage();
            if (salarySatisfaction_Percentage >= SALARY_UPPER_THR)
            {
                __instance.Rel_Influence_Points += INFLUENCE_BONUS;
            }
            else if (salarySatisfaction_Percentage <= SALARY_LOWER_THR && __instance.Rel_Influence_Points >= -INFLUENCE_PENALTYCRIT)
            {
                __instance.Rel_Influence_Points += INFLUENCE_PENALTYCRIT;
            }
            else if (salarySatisfaction_Percentage <= SALARY_MID_THR && __instance.Rel_Influence_Points >= -INFLUENCE_PENALTY)
            {
                __instance.Rel_Influence_Points += INFLUENCE_PENALTY;
            }

            if (__instance.Rel_Romance_Points >= -ROMANCE_PENALTY)
            {
                __instance.Rel_Romance_Points += ROMANCE_PENALTY;
            }

            if (__instance.Rel_Friendship_Points >= -FRIEND_PENALTY)
            {
                __instance.Rel_Friendship_Points += FRIEND_PENALTY;
            }
        }
    }

}
