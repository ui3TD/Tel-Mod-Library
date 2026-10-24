using HarmonyLib;
using System;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansion
{
    // Old Money is always at 200% salary satisfaction
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetSalarySatisfaction))]
    public class data_girls_girls_GetSalarySatisfaction
    {
        [HarmonyPriority(Priority.LowerThanNormal)]
        public static void Postfix(ref float __result, data_girls.girls __instance)
        {
            if (__instance.trait != (traits._trait._type)NewTraits.Old_Money)
                return;

            __result = 2f;
        }
    }

    // Old Money doesn't lose mental stamina on low salary
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.CheckLowSalaries))]
    public class data_girls_CheckLowSalaries
    {
        public static void Prefix()
        {
            patchAddParam = true;
        }
        public static Exception Finalizer(Exception __exception)
        {
            patchAddParam = false;
            return __exception;
        }
    }

    // Apply Old Money effects to mental decrease
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.addParam))]
    public class data_girls_girls_addParam
    {
        public static void Prefix(data_girls.girls __instance, data_girls._paramType type, ref float val)
        {
            if (!patchAddParam)
                return;

            if (__instance.trait != (traits._trait._type)NewTraits.Old_Money || type != data_girls._paramType.mentalStamina)
                return;

            val = 0;
        }
    }

    // appeal adjustments for traits
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetAppealOfStat))]
    public class Data_girls_girls_GetAppealOfStat
    {
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            switch((NewTraits)__instance.trait)
            {
                case NewTraits.Fashionista:
                    if (_FanType == resources.fanType.female)
                        __result *= FASHIONISTA_COEFF;
                    break;
                case NewTraits.Flirty:
                    if (_FanType == resources.fanType.male)
                        __result *= FLIRTY_COEFF;
                    break;
                case NewTraits.Idol_Otaku:
                    if (_FanType == resources.fanType.hardcore)
                        __result *= OTAKU_COEFF;
                    break;
            }
        }
    }

    // Sadistic bullying
    [HarmonyPatch(typeof(Relationships), nameof(Relationships.Do_Bullying))]
    public class Relationships_Do_Bullying
    {
        public static void Postfix()
        {
            int sadistic;
            foreach (Relationships._clique clique in Relationships.Cliques)
            {
                sadistic = 0;
                foreach(data_girls.girls member in clique.Members)
                {
                    if (member.IsSick())
                        continue;

                    if (member.trait == (traits._trait._type)NewTraits.Sadistic && clique.IsBully(member))
                    {
                        sadistic++;
                    }
                }

                if (sadistic == 0)
                    continue;

                foreach (data_girls.girls girls in clique.Bullied_Girls)
                {
                    if (girls.IsSick())
                        continue;

                    string notif = Language.Insert("IDOL__BULLIED_UNKNOWN", new string[] { SADISTIC_MODIFIER.ToString() });

                    girls.getParam(data_girls._paramType.mentalStamina).add(-SADISTIC_MODIFIER, false);
                    if (clique.KnownBulliedGirls.Contains(girls))
                    {
                        notif = girls.GetName() + Language.Insert("REL__BULLYING_LOST", new string[] { SADISTIC_MODIFIER.ToString() });
                    }

                    NotificationManager.AddNotification(notif, mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                }

            }
        }
    }

    // Job Hopper graduation date within the next year
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Set_Default_Date))]
    public class data_girls_girls_Graduation_Set_Default_Date
    {
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance.trait != (traits._trait._type)NewTraits.Job_Hopper)
                return;

            __instance.Graduation_Date = staticVars.dateTime.AddDays(UnityEngine.Random.Range(JOBHOPPER_DAYS_LOWER, JOBHOPPER_DAYS_UPPER));
        }
    }

    // Reckless
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Try_Injury))]
    public class data_girls_girls_Try_Injury
    {
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance.trait == (traits._trait._type)NewTraits.Reckless && __instance.status != data_girls._status.injured)
            {
                if (__instance.room != null && __instance.room.type == agency._type.doctorsOffice)
                    return;

                float stam = __instance.getParam(data_girls._paramType.physicalStamina).val;
                if (stam >= RECKLESS_THR_UPPER)
                    return;

                float injuryChance;
                // 1% chance below 60
                if (stam > RECKLESS_THR_LOWER)
                {
                    injuryChance = RECKLESS_CHANCE;
                }
                // 2% chance below 5
                else
                {
                    injuryChance = RECKLESS_CHANCE_SEVERE;
                }
                if (mainScript.chance(injuryChance))
                {
                    __instance.Set_Injured();
                }
            }
        }
    }
}
