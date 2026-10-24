using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for calculating the accident success chance during concerts.
    /// Reduces the failure chance if any girl in the group has the ISTP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.AccidentSuccessChance))]
    public class SEvent_Concerts__concert_AccidentSuccessChance
    {

        /// <summary>
        /// Harmony patch for calculating the accident success chance during concerts.
        /// Reduces the failure chance if any girl in the group has the ISTP MBTI type.
        /// </summary>
        /// <param name="__result">Calculated success chance.</param>
        public static void Postfix(ref int __result)
        {
            // AccidentSuccessChance takes no song, so the song comes from the concert popup. That's right today
            // because the game only calls it from the popup while it plays a song.
            Concert_Popup popup = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_concert).obj.GetComponent<Concert_Popup>();
            List<data_girls.girls> girls = popup.CurrentSong.GetGirls(true);
            MBTI mBTI;

            foreach (data_girls.girls _girls in girls)
            {
                if(_girls != null)
                {
                    mBTI = GetGirlMBTI(_girls);
                    if(mBTI == MBTI.ISTP)
                    {
                        __result = ApplyISTPBonus(__result);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Halves the chance of failing an accident.
        /// </summary>
        /// <param name="successChance">Success chance in whole percent (0-100).</param>
        /// <returns>Success chance with the ISTP bonus, in whole percent.</returns>
        public static int ApplyISTPBonus(int successChance)
        {
            return Mathf.RoundToInt(successChance + (100 - successChance) * ISTPBonus);
        }
    }

    /// <summary>
    /// Harmony patch for calculating the accident stamina reduction during concerts.
    /// Reduces the accident chance based on the number of girls with the ISTJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Accident_Stamina))]
    public class SEvent_Concerts__concert_Accident_Stamina
    {

        /// <summary>
        /// Harmony patch for calculating the accident stamina reduction during concerts.
        /// Reduces the accident chance based on the number of girls with the ISTJ MBTI type.
        /// </summary>
        /// <param name="__result">Calculated accident stamina reduction.</param>
        /// <param name="__instance">Instance of the concert being patched.</param>
        /// <param name="songID">ID of the song being performed.</param>
        public static void Postfix(ref int __result, SEvent_Concerts._concert __instance, int songID)
        {
            List<data_girls.girls> girls = __instance.SetListItems[songID].GetGirls(true);
            MBTI mBTI;
            int istjCount = 0;
            foreach (data_girls.girls _girls in girls)
            {
                if (_girls != null)
                {
                    mBTI = GetGirlMBTI(_girls);
                    if (mBTI == MBTI.ISTJ)
                    {
                        istjCount++;
                    }
                }
            }

            if (istjCount > 0)
            {
                __result -= ISTJBonus * istjCount;
            }
        }
    }

    /// <summary>
    /// Harmony patch for setting up a business proposal.
    /// Reduces the stamina cost if the girl has the ENFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Business_Popup), nameof(Business_Popup.Set))]
    public class Business_Popup_Set
    {

        /// <summary>
        /// Harmony patch for setting up a business proposal.
        /// Reduces the stamina cost if the girl has the ENFP MBTI type.
        /// </summary>
        /// <param name="_proposal">Business proposal being set up.</param>
        /// <param name="__state">Original stamina cost state.</param>
        public static void Prefix(business._proposal _proposal, out int? __state)
        {
            __state = null;
            if (GetGirlMBTI(_proposal.girl) != MBTI.ENFP)
                return;

            __state = _proposal.stamina;
            _proposal.stamina = Mathf.RoundToInt(_proposal.stamina * (1 - ENFPBonus));

        }

        /// <summary>
        /// Harmony patch for setting up a business proposal.
        /// Reduces the stamina cost if the girl has the ENFP MBTI type.
        /// </summary>
        /// <param name="_proposal">Business proposal being set up.</param>
        /// <param name="__state">Original stamina cost state.</param>
        public static void Finalizer(business._proposal _proposal, int? __state)
        {
            if (__state == null)
                return;

            _proposal.stamina = __state.Value;

        }
    }

    /// <summary>
    /// Harmony patch for accepting a business proposal.
    /// Reduces the stamina cost if the girl has the ENFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(business), nameof(business.Accept))]
    public class business_Accept
    {
        /// <summary>
        /// Harmony patch for accepting a business proposal.
        /// Reduces the stamina cost if the girl has the ENFP MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the business being patched.</param>
        public static void Prefix(business __instance)
        {
            if (GetGirlMBTI(__instance.ActiveProposal.girl) != MBTI.ENFP)
                return;

            __instance.ActiveProposal.stamina = Mathf.RoundToInt(__instance.ActiveProposal.stamina * (1 - ENFPBonus));
        }
    }

    /// <summary>
    /// Harmony patch for reducing novelty decreases of a cafe dish.
    /// Applies the INTP bonus to dish novelty.
    /// </summary>
    [HarmonyPatch(typeof(Cafes._cafe._dish), nameof(Cafes._cafe._dish.AddNovelty))]
    public class Cafes__cafe__dish_AddNovelty
    {

        /// <summary>
        /// Harmony patch for reducing novelty decreases of a cafe dish.
        /// Applies the INTP bonus to dish novelty.
        /// </summary>
        /// <param name="__instance">Instance of the dish being patched.</param>
        /// <param name="val">Novelty value being added.</param>
        public static void Prefix(Cafes._cafe._dish __instance, ref int val)
        {
            if (GetGirlMBTI(__instance.GetGirl()) != MBTI.INTP)
                return;

            if(val < 0)
            {
                val = Mathf.RoundToInt(val * INTPBonus);
            }
        }
    }

    /// <summary>
    /// Harmony patch for calculating the price coefficient for a theater.
    /// Increases the price coefficient based on the number of girls with the ESFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Theaters._theater), nameof(Theaters._theater.GetPriceCoeff))]
    public class Theaters__theater_GetPriceCoeff
    {
        /// <summary>
        /// Harmony patch for calculating the price coefficient for a theater.
        /// Increases the price coefficient based on the number of girls with the ESFP MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the theater being patched.</param>
        /// <param name="__result">Calculated price coefficient.</param>
        public static void Postfix(Theaters._theater __instance, ref float __result)
        {
            int count = 0;
            foreach(data_girls.girls girl in __instance.GetGroup().Girls)
            {
                if(GetGirlMBTI(girl) == MBTI.ESFP)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                __result *= 1 + ESFPBonus * count;
            }
        }
    }

    /// <summary>
    /// Harmony patch for calculating team chemistry.
    /// Increases team chemistry based on the number of girls with the ESFJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GetTeamChemistry))]
    public class data_girls_GetTeamChemistry
    {

        /// <summary>
        /// Harmony patch for calculating team chemistry.
        /// Increases team chemistry based on the number of girls with the ESFJ MBTI type.
        /// </summary>
        /// <param name="__result">Calculated team chemistry.</param>
        /// <param name="_girls">List of girls in the team.</param>
        public static void Postfix(ref float __result, List<data_girls.girls> _girls)
        {
            int count = 0;
            foreach (data_girls.girls girl in _girls)
            {
                if(girl != null)
                {
                    if (GetGirlMBTI(girl) == MBTI.ESFJ)
                    {
                        count++;
                    }
                }
            }
            if(count > 0)
            {
                __result += ESFJBonus * count;
            }
        }
    }
}
