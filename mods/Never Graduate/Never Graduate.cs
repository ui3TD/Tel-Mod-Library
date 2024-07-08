using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace NeverGraduate
{
    // Never check for graduations unless the girl is fired
    [HarmonyPatch(typeof(data_girls), "UpdateGraduationDates")]
    public class data_girls_UpdateGraduationDates
    {
        public static bool Prefix()
        {
            return false;
        }
    }

}
