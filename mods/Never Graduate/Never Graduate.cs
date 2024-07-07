using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace NeverGraduate
{

    // Never check for graduations unless the girl is fired
    [HarmonyPatch(typeof(data_girls), "CheckGraduations")]
    public class data_girls_CheckGraduations
    {
        public static bool Prefix()
        {
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            foreach (data_girls.girls girls in data_girls.girl)
            {
                if (girls.status == data_girls._status.announced_graduation)
                {
                    girls.Graduation_Date_Update();
                    break;
                }
            }
            return false;
        }
    }


    // Never check for graduations unless the girl is fired
    [HarmonyPatch(typeof(data_girls), "UpdateGraduationDates")]
    public class data_girls_UpdateGraduationDates
    {
        public static bool Prefix()
        {
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            return false;
        }
    }

}
