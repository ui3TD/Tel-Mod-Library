using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using static WorkerRights.WorkerRights;

namespace WorkerRights
{
    public class WorkerRights
    {
        public const int FIRE_MIN_DAYS = 30;
        public const int DEF_SALARY = 20000;
        public const float MAXFAME_SALARY_EARN_COEFF = 0.1f;
        public const int GRAD_DAYS_PENALTY = 9;
        public const int GRAD_DAYS_PENALTY_SEVERE = 27;
    }

    // Staff cannot be fired using scandal points within the first month
    [HarmonyPatch(typeof(staff._staff), "CanFire")]
    public class staff__staff_CanFire
    {
        public static void Postfix(staff._staff __instance, ref bool __result)
        {
            if ((staticVars.dateTime - __instance.HireDate).Days < FIRE_MIN_DAYS)
            {
                __result = false;
            }
        }
    }
    // 20000 yen/wk is the expected starting salary for 100% satisfaction
    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary")]
    public class data_girls_girls_GetExpectedSalary
    {
        public static void Postfix(ref int __result, data_girls.girls __instance)
        {
            if (__instance.GetFameLevel() < 1f)
            {
                __result = DEF_SALARY * 2;
            }
        }
    }

    //In hard and normal mode, penalty for low salary satisfaction increased 10x
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Date_Update")]
    public class data_girls_girls_Graduation_Date_Update
    {
        public static void Postfix(ref data_girls.girls __instance)
        {
            int salarySatisfaction_Percentage = __instance.GetSalarySatisfaction_Percentage();
            if (staticVars.IsEasy() || __instance.status == data_girls._status.announced_graduation || policies.GetSelectedPolicyValue(policies._type.salary).Value != policies._value.salary_manual)
                return;

            int daysModifier = 0;
            if (salarySatisfaction_Percentage < 20)
            {
                daysModifier -= GRAD_DAYS_PENALTY_SEVERE;
            }
            else if (salarySatisfaction_Percentage < 50)
            {
                daysModifier -= GRAD_DAYS_PENALTY;
            }
            __instance.Graduation_Date.AddDays(daysModifier);
        }
    }

    // In hard mode, idols at 10 fame will expect at least 10% of their earnings as salary
    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary_Total")]
    public class data_girls_girls_GetExpectedSalary_Total
    {
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            if (!staticVars.IsHard())
                return;

            int expected = __instance.GetExpectedSalary();
            float earning = __instance.GetAverageEarnings();
            if (__instance.GetFameLevel() == 10 && (earning * MAXFAME_SALARY_EARN_COEFF) > expected)
            {
                __result = (long)Mathf.Round(earning * MAXFAME_SALARY_EARN_COEFF);
            }
        }
    }


    // Default salary set to 20000
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result)
        {
            __result.salary = DEF_SALARY;
        }
    }

}
