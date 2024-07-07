using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace WorkerRights
{
    // Staff cannot be fired using scandal points within the first month
    [HarmonyPatch(typeof(staff._staff), "CanFire")]
    public class staff__staff_CanFire
    {
        public static void Postfix(staff._staff __instance, ref bool __result)
        {
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            bool output = __result;
            if ((staticVars.dateTime - __instance.HireDate).Days < 30)
            {
                output = false;
            }
            __result = output;
        }
    }
    // 20000 yen/wk is the expected starting salary for 100% satisfaction
    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary")]
    public class data_girls_girls_GetExpectedSalary
    {
        public static void Postfix(ref int __result, data_girls.girls __instance)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            float num = (float)__instance.GetFameLevel();
            if (num < 1f)
            {
                __result = 40000;
            }
        }
    }

    //In hard and normal mode, penalty for low salary satisfaction increased 10x
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Date_Update")]
    public class data_girls_girls_Graduation_Date_Update
    {
        public static void Postfix(ref data_girls.girls __instance)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            int salarySatisfaction_Percentage = __instance.GetSalarySatisfaction_Percentage();
            if (__instance.status == data_girls._status.announced_graduation)
            {
                return;
            }
            float num = 0;
            if (salarySatisfaction_Percentage < 20)
            {
                if (!staticVars.IsEasy() && policies.GetSelectedPolicyValue(policies._type.salary).Value == policies._value.salary_manual)
                {
                    num -= 27f;
                }
            }
            else if (salarySatisfaction_Percentage < 50)
            {
                if (!staticVars.IsEasy() && policies.GetSelectedPolicyValue(policies._type.salary).Value == policies._value.salary_manual)
                {
                    num -= 9f;
                }
            }
            __instance.Graduation_Date.AddDays((double)Mathf.RoundToInt(num));
        }
    }

    // In hard mode, idols at 10 fame will expect at least 10% of their earnings as salary
    [HarmonyPatch(typeof(data_girls.girls), "GetExpectedSalary_Total")]
    public class data_girls_girls_GetExpectedSalary_Total
    {
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            if (staticVars.IsHard())
            {
                int num = __instance.GetExpectedSalary();
                float num2 = __instance.GetAverageEarnings();
                if (__instance.GetFameLevel() == 10 && num2 / 10 > num)
                {
                    __result = (long)Mathf.Round(num2 / 10);
                }
            }
        }
    }


    // Default salary set to 20000
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result)
        {
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            __result.salary = 20000;
        }
    }

}
