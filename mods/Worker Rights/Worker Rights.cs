using HarmonyLib;
using System;
using UnityEngine;
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

    [HarmonyPatch(typeof(staff._staff), "CanFire")]
    public class staff__staff_CanFire
    {
        public static void Postfix(staff._staff __instance, ref bool __result)
        {
            if (__instance != null && (staticVars.dateTime - __instance.HireDate).Days < FIRE_MIN_DAYS)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary")]
    public class data_girls_girls_GetExpectedSalary
    {
        public static void Postfix(data_girls.girls __instance, ref int __result)
        {
            if (__instance != null && __instance.GetFameLevel() < 1f)
                __result = Math.Max(__result, DEF_SALARY * 2);
        }
    }

    // Vanilla's own Graduation_Date_Update calls DateTime.AddDays without assigning the returned DateTime,
    // so the salary date adjustment is a no-op. Apply the complete advertised 10x low-salary penalty here.
    // This stays on the game's weekly Graduation_Date_Update cadence rather than silently becoming a daily 7x rebalance.
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Date_Update")]
    public class data_girls_girls_Graduation_Date_Update
    {
        [HarmonyBefore("com.tel.nevergraduate")]
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance == null || staticVars.IsEasy() || __instance.status == data_girls._status.announced_graduation)
                return;

            policies.value salaryPolicy = policies.GetSelectedPolicyValue(policies._type.salary);
            if (salaryPolicy == null || salaryPolicy.Value != policies._value.salary_manual)
                return;

            int satisfaction = __instance.GetSalarySatisfaction_Percentage();
            int daysModifier = 0;
            if (satisfaction < 20)
                daysModifier = -GRAD_DAYS_PENALTY_SEVERE;
            else if (satisfaction < 50)
                daysModifier = -GRAD_DAYS_PENALTY;

            if (daysModifier != 0)
                __instance.Graduation_Date = __instance.Graduation_Date.AddDays(daysModifier);
        }
    }

    // In hard mode, 10-fame idols expect AT LEAST 10% of average earnings.
    // Never lower a larger salary expectation produced by vanilla or another mod.
    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary_Total")]
    public class data_girls_girls_GetExpectedSalary_Total
    {
        public static void Postfix(data_girls.girls __instance, ref long __result)
        {
            if (__instance == null || !staticVars.IsHard() || __instance.GetFameLevel() != 10)
                return;

            float earning = __instance.GetAverageEarnings();
            if (float.IsNaN(earning) || float.IsInfinity(earning) || earning <= 0f)
                return;

            long floor = (long)Mathf.Round(earning * MAXFAME_SALARY_EARN_COEFF);
            if (floor > __result)
                __result = floor;
        }
    }

    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result)
        {
            if (__result != null)
                __result.salary = DEF_SALARY;
        }
    }
}
