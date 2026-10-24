using HarmonyLib;
using System;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.AgeDeterioration))]
    public class Data_girls_AgeDeterioration
    {
        // Live Fast: double random post-peak deterioration. Not if another mod turned ageing off.
        public static void Postfix(bool __runOriginal)
        {
            if (!__runOriginal || data_girls.girl == null)
                return;

            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl == null || girl.status == data_girls._status.graduated || girl.trait != traits._trait._type.Live_fast)
                    continue;

                for (int i = 0; i < LIVEFAST_DETERIORATION - 1; i++)
                    girl.AgeDeterioration();
            }
        }
    }

    // Live Fast birthday deterioration without a compiler-local-dependent transpiler.
    [HarmonyPatch(typeof(Birthday_Popup), nameof(Birthday_Popup.DoParam))]
    public class Birthday_Popup_DoParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Birthday_Popup __instance, data_girls._paramType Prm, ref bool __state)
        {
            __state = BeginBirthdayDeterioration(__instance?.Girl, Prm);
        }

        public static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
                EndBirthdayDeterioration();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.setVal))]
    public class data_girls_girls_param_setVal_Birthday
    {
        [HarmonyPriority(Priority.VeryLow)]
        public static void Prefix(data_girls.girls.param __instance, ref float newVal)
        {
            if (__instance == null)
                return;
            newVal = AdjustBirthdayDeterioration(__instance.Parent, __instance.type, newVal);
        }
    }

    [HarmonyPatch(typeof(Birthday_Stat), nameof(Birthday_Stat.Set))]
    public class Birthday_Stat_Set
    {
        [HarmonyPriority(Priority.VeryLow)]
        public static void Prefix(data_girls._paramType Type, float OldVal, ref float NewVal)
        {
            NewVal = AdjustBirthdayDeteriorationDisplay(Type, OldVal, NewVal);
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetAppealOfStat))]
    public class Data_girls_girls_GetAppealOfStat
    {
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            if (__instance == null || __instance.trait != traits._trait._type.Trendy)
                return;

            switch (_FanType)
            {
                case resources.fanType.adult:
                    __result *= TRENDY_ADULT_MODIFIER;
                    break;
                case resources.fanType.youngAdult:
                    __result *= TRENDY_YA_MODIFIER;
                    break;
                case resources.fanType.teen:
                    __result *= TRENDY_TEEN_MODIFIER;
                    break;
            }
        }
    }
}
