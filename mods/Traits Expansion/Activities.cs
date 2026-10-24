using HarmonyLib;
using UnityEngine;
using System;
using System.Globalization;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansion
{
    // World tours give 1.2x more fans for each multilingual member
    [HarmonyPatch(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.GetNewFansByAttendance))]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            int count = 0;
            foreach (data_girls.girls girl in data_girls.GetActiveGirls())
            {
                if (girl != null && girl.trait == (traits._trait._type)NewTraits.Polyglot)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                __result = Mathf.RoundToInt(__result * (1 + POLYGLOT_BONUS * count));
            }
        }
    }

    // Aerophobic stamina penalty
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.UseStamina))]
    public class SEvent_Tour_UseStamina
    {
        public static void Postfix()
        {
            foreach (data_girls.girls girls in data_girls.GetActiveGirls())
            {
                if (girls != null && girls.trait == (traits._trait._type)NewTraits.Aerophobia)
                {
                    girls.addParam(data_girls._paramType.mentalStamina, -AEROPHOB_PENALTY);
                }
            }
        }
    }

    // Stage Fright stamina penalty
    // Only for the call that finishes the concert: not if another mod skipped the game's Finish,
    // and not again if Finish is called on a concert that was already finished
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Prefix(SEvent_Concerts._concert __instance, out bool __state)
        {
            __state = __instance.Status == SEvent_Tour.tour._status.finished;
        }

        public static void Postfix(SEvent_Concerts._concert __instance, bool __runOriginal, bool __state)
        {
            if (!__runOriginal || __state || __instance.Status != SEvent_Tour.tour._status.finished)
                return;

            foreach (SEvent_Concerts._concert.ISetlistItem setlistItem in __instance.SetListItems)
            {
                if (setlistItem == null)
                    continue;

                foreach (data_girls.girls girls in setlistItem.GetGirls(true))
                {
                    if (girls != null && girls.trait == (traits._trait._type)NewTraits.Stage_Fright)
                    {
                        NotificationManager.AddNotification(
                            Language.Insert("IDOL__STAGEFRIGHT", girls.GetName(), STAGEFRIGHT_PENALTY.ToString(CultureInfo.InvariantCulture)),
                            mainScript.red32,
                            NotificationManager._notification._type.idol_stat_change
                            );
                        girls.addParam(data_girls._paramType.mentalStamina, -STAGEFRIGHT_PENALTY);
                    }
                }
            }
        }
    }

    // Cult Leader vote bonus
    [HarmonyPatch(typeof(SEvent_SSK._SSK), nameof(SEvent_SSK._SSK.GenerateResults))]
    public class SEvent_SSK__SSK_GenerateResults
    {
        public static void Prefix()
        {
            patchGetFan_Count = true;
        }
        public static Exception Finalizer(Exception __exception)
        {
            patchGetFan_Count = false;
            return __exception;
        }
    }

    // Cult Leader vote bonus
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetFan_Count), new Type[] {typeof(resources.fanType)})]
    public class data_girls_girls_GetFan_Count
    {
        public static void Postfix(data_girls.girls __instance, ref long __result)
        {
            if (!patchGetFan_Count)
                return;

            if (__instance.trait != (traits._trait._type)NewTraits.Cult_Leader)
                return;

            __result = (long)Mathf.Round(__result * CULT_COEFF);
        }
    }

    // Thespian drama bonus (popup UI)
    [HarmonyPatch(typeof(Business_Popup), nameof(Business_Popup.Set))]
    public class Business_Popup_Set
    {
        public static void Prefix(business._proposal _proposal, out int? __state)
        {
            __state = null;
            if (_proposal?.girl == null)
                return;
            if (_proposal.type != business._type.tv_drama || _proposal.girl.trait != (traits._trait._type)NewTraits.Thespian)
                return;

            int lowered = Mathf.RoundToInt(_proposal.stamina * THESPIAN_COEFF);
            __state = _proposal.stamina - lowered;
            _proposal.stamina = lowered;
        }

        // Finalizer so the stamina is restored even if Set throws. It puts back only what the prefix took off,
        // so other mods that lower it for the popup too (MBTI's ENFP, for one), or change it for good, all end
        // up right in any order.
        public static void Finalizer(business._proposal _proposal, int? __state)
        {
            if (__state == null || _proposal == null)
                return;

            _proposal.stamina += __state.Value;
        }
    }

    // Thespian drama bonus (popup UI)
    [HarmonyPatch(typeof(business), nameof(business.Accept))]
    public class business_Accept
    {
        // __runOriginal is false if another mod skipped the game's Accept: then the offer isn't taken, so it isn't lowered
        public static void Prefix(business __instance, bool __runOriginal)
        {
            if (!__runOriginal || __instance.ActiveProposal?.girl == null)
                return;
            if (__instance.ActiveProposal.type != business._type.tv_drama || __instance.ActiveProposal.girl.trait != (traits._trait._type)NewTraits.Thespian)
                return;

            __instance.ActiveProposal.stamina = Mathf.RoundToInt(__instance.ActiveProposal.stamina * THESPIAN_COEFF);
        }
    }
}
