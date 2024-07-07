using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;

namespace GoingViral
{

    // Trending stat and fan counts
    // Apply trending to churn
    [HarmonyPatch(typeof(resources), "OnNewDay")]
    public class resources_OnNewDay
    {
        [HarmonyAfter("com.tel.fanattrition")]
        public static void Postfix()
        {
            if(TrendingManager.trending > 0)
            {
                TrendingManager.trending = Math.Min(90, Math.Max(0, TrendingManager.trending - 1));
            }
            else if(TrendingManager.trending < 0)
            {
                TrendingManager.trending = Math.Max(-90, Math.Min(0, TrendingManager.trending + 1));
                resources.FansChange = (long)Mathf.Round(resources.FansChange * TrendingManager.GetTrendingCoeff());
            }
            Debug.Log("Trending: " + TrendingManager.trending);

            TrendingManager.UpdateFanCount();
        }
    }

    // Save trending resource
    [HarmonyPatch(typeof(resources), "SaveFunction")]
    public class resources_SaveFunction
    {
        public static void Postfix()
        {
            Camera.main.GetComponent<mainScript>().GetSavedData().resources__Resources.Add(new resources.ResourceData{Type = resources.type.buzz, Val = TrendingManager.trending + 10000 });
        }
    }

    // Set trending resource
    [HarmonyPatch(typeof(resources), "Set")]
    public class resources_Set
    {
        public static bool Prefix(resources.type _type, long val)
        {
            if(_type == resources.type.buzz)
            {
                if(val >= 10000)
                {
                    TrendingManager.trending = val - 10000;
                }
            }
            return true;
        }
    }

}
