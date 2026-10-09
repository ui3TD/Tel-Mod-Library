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
        public const int GRAD_DAYS_PENALTY = 10;
        public const int GRAD_DAYS_PENALTY_SEVERE = 30;
    }

    // Staff cannot be fired using scandal points within the first month
    [HarmonyPatch(typeof(staff._staff), nameof(staff._staff.CanFire))]
    public class staff__staff_CanFire
    {
        public static void Postfix(staff._staff __instance, ref bool __result)
        {
            if (__instance != null && (staticVars.dateTime - __instance.HireDate).Days < FIRE_MIN_DAYS)
            {
                __result = false;
            }
        }
    }
    // 20000 yen/wk is the expected starting salary for 100% satisfaction
    // Never lower a larger expectation from vanilla or another mod
    // Why twice DEF_SALARY: the game's GetExpectedSalary_Total clamps an idol's average earnings to between
    // half and double this base. A new idol earns nothing, so she expects half of it: exactly DEF_SALARY.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetExpectedSalary))]
    public class data_girls_girls_GetExpectedSalary
    {
        public static void Postfix(ref int __result, data_girls.girls __instance)
        {
            if (__instance != null && __instance.GetFameLevel() < 1f)
            {
                __result = Math.Max(__result, DEF_SALARY * 2);
            }
        }
    }

    //In hard and normal mode, penalty for low salary satisfaction increased 10x
    // Vanilla's own salary adjustment is a no-op (it discards the result of AddDays), so apply the full penalty here
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update))]
    public class data_girls_girls_Graduation_Date_Update
    {
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance == null || staticVars.IsEasy() || __instance.status == data_girls._status.announced_graduation)
                return;

            policies.value salaryPolicy = policies.GetSelectedPolicyValue(policies._type.salary);
            if (salaryPolicy == null || salaryPolicy.Value != policies._value.salary_manual)
                return;

            int salarySatisfaction_Percentage = __instance.GetSalarySatisfaction_Percentage();
            int daysModifier = 0;
            if (salarySatisfaction_Percentage < 20)
            {
                daysModifier = -GRAD_DAYS_PENALTY_SEVERE;
            }
            else if (salarySatisfaction_Percentage < 50)
            {
                daysModifier = -GRAD_DAYS_PENALTY;
            }

            if (daysModifier != 0)
                __instance.Graduation_Date = __instance.Graduation_Date.AddDays(daysModifier);
        }
    }

    // In hard mode, idols at 10 fame will expect at least 10% of their earnings as salary
    // Never lower a larger expectation from vanilla or another mod
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetExpectedSalary_Total))]
    public class data_girls_girls_GetExpectedSalary_Total
    {
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            if (__instance == null || !staticVars.IsHard() || __instance.GetFameLevel() != 10)
                return;

            float earning = __instance.GetAverageEarnings();
            if (float.IsNaN(earning) || float.IsInfinity(earning) || earning <= 0f)
                return;

            long floor = (long)Mathf.Round(earning * MAXFAME_SALARY_EARN_COEFF);
            if (floor > __result)
            {
                __result = floor;
            }
        }
    }


    // Default salary set to 20000
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateGirl))]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result)
        {
            if (__result != null)
                __result.salary = DEF_SALARY;
        }
    }

}
