using HarmonyLib;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using static MBTIPersonalities.MBTIPersonalities;
using System.Linq;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for rendering the extras tab in the profile popup.
    /// Adds MBTI information to the profile extras tab if applicable.
    /// </summary>
    [HarmonyPatch(typeof(Profile_Popup), "RenderTab_Extras")]
    public class Profile_Popup_RenderTab_Extras
    {

        /// <summary>
        /// Harmony patch for rendering the extras tab in the profile popup.
        /// Adds MBTI information to the profile extras tab if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the Profile_Popup class being patched.</param>
        public static void Postfix(Profile_Popup __instance)
        {
            MBTI mBTI = GetGirlMBTI(__instance.Girl);
            if (mBTI == MBTI.None)
                return;

            string txt = "\n" + ExtensionMethods.color(Language.Data[constantTitlePrefix + mBTI.ToString()] + ": ", mainScript.blue) + Language.Data[constantDescPrefix + mBTI.ToString()];

            TextMeshProUGUI textComponent = __instance.Extras_Container.transform.Find("Text(Clone)").GetComponent<TextMeshProUGUI>();
            textComponent.text += txt;
            textComponent.text = textComponent.text.Replace(mainScript.blue, mainScript.black);
            LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Extras_Container.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// Harmony patch for showing traits on the audition data card.
    /// Adds MBTI information to the audition card if applicable.
    /// </summary>
    [HarmonyPatch(typeof(Audition_Data_Card), "ShowTrait")]
    public class Audition_Data_Card_ShowTrait
    {
        /// <summary>
        /// Harmony patch for showing traits on the audition data card.
        /// Adds MBTI information to the audition card if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the Audition_Data_Card class being patched.</param>
        public static void Postfix(ref Audition_Data_Card __instance)
        {
            MBTI mBTI = GetGirlMBTI(__instance.Girl.girl);
            if (mBTI == MBTI.None)
                return;

            string title = " / " + Language.Data[constantTitlePrefix + mBTI.ToString()];
            string desc = "\n" + Language.Data[constantDescPrefix + mBTI.ToString()];

            __instance.Special_Skill_Title.GetComponent<TextMeshProUGUI>().text += title;
            __instance.Special_Skill_Description.GetComponent<TextMeshProUGUI>().text += desc;
        }
    }

    /// <summary>
    /// Harmony patch for getting tooltip text when hovering over a girl's profile.
    /// Adds MBTI information to the tooltip if applicable.
    /// </summary>
    [HarmonyPatch(typeof(GirlProfileOnHover), "GetTooltipText")]
    public class GirlProfileOnHover_GetTooltipText
    {
        /// <summary>
        /// Harmony patch for getting tooltip text when hovering over a girl's profile.
        /// Adds MBTI information to the tooltip if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the GirlProfileOnHover class being patched.</param>
        /// <param name="__result">Tooltip text to be displayed.</param>
        public static void Postfix(GirlProfileOnHover __instance, ref string __result)
        {
            MBTI mBTI = GetGirlMBTI(__instance.girl);
            if (mBTI == MBTI.None)
                return;

            __result += "\n" + Language.Data[constantTitlePrefix + mBTI.ToString()] + ": " + Language.Data[constantDescPrefix + mBTI.ToString()];
        }
    }

    /// <summary>
    /// Harmony patch for getting tooltip text for a girl.
    /// Adds MBTI information to the tooltip if applicable.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "GetTooltipText")]
    public class data_girls_girls_GetTooltipText
    {
        /// <summary>
        /// Harmony patch for getting tooltip text for a girl.
        /// Adds MBTI information to the tooltip if applicable.
        /// </summary>
        /// <param name="__instance">Instance of the data_girls.girls class being patched.</param>
        /// <param name="__result">Tooltip text to be displayed.</param>
        public static void Postfix(data_girls.girls __instance, ref string __result)
        {
            MBTI mBTI = GetGirlMBTI(__instance);
            if (mBTI == MBTI.None)
                return;

            __result += "\n" + Language.Data[constantTitlePrefix + mBTI.ToString()] + ": " + Language.Data[constantDescPrefix + mBTI.ToString()];
        }
    }

    /// <summary>
    /// Harmony patch for generating a girl.
    /// Assigns an MBTI type to the generated girl based on texture data if applicable.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        /// <summary>
        /// Harmony patch for generating a girl.
        /// Assigns an MBTI type to the generated girl based on texture data if applicable.
        /// </summary>
        /// <param name="__result">Generated girl instance.</param>
        /// <param name="genTextures">Flag indicating whether textures should be generated.</param>
        public static void Postfix(ref data_girls.girls __result, bool genTextures)
        {
            if (!genTextures)
                return;

            foreach(MBTITextureData data in MBTITextureReferenceList)
            {
                if (__result.textureAssets.Count != 0 && __result.textureAssets[0].asset != null && __result.textureAssets[0].asset.ModName == data.ModName && __result.textureAssets[0].asset.body_id == data.body_id)
                {
                    SetGirlMBTI(__result, data.mbti);
                    break;
                }
            }
        }
    }


    /// <summary>
    /// Harmony patch for calculating the accident success chance during concerts.
    /// Reduces the failure chance if any girl in the group has the ISTP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "AccidentSuccessChance")]
    public class SEvent_Concerts__concert_AccidentSuccessChance
    {

        /// <summary>
        /// Harmony patch for calculating the accident success chance during concerts.
        /// Reduces the failure chance if any girl in the group has the ISTP MBTI type.
        /// </summary>
        /// <param name="__result">Calculated success chance.</param>
        public static void Postfix(ref int __result)
        {
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
                        __result = Mathf.RoundToInt(ISTPBonus + __result / 100 * (1 - ISTPBonus));
                        return;
                    }
                }
            }
        }
    }


    /// <summary>
    /// Harmony patch for calculating the accident stamina reduction during concerts.
    /// Reduces the accident chance based on the number of girls with the ISTJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Accident_Stamina")]
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
    /// Harmony patch for adding points in player relationships.
    /// Increases the influence gain if the girl has the ISFJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Relationships_Player), "AddPoints")]
    public class Relationships_Player_AddPoints
    {

        /// <summary>
        /// Harmony patch for adding points in player relationships.
        /// Increases the influence gain if the girl has the ISFJ MBTI type.
        /// </summary>
        /// <param name="Type">Type of relationship points being added.</param>
        /// <param name="Girl">Girl instance for which points are being added.</param>
        /// <param name="Points">Number of points being added.</param>
        public static void Prefix(Relationships_Player._type Type, data_girls.girls Girl, ref int Points)
        {
            MBTI mBTI = GetGirlMBTI(Girl);
            if (mBTI != MBTI.ISFJ)
                return;

            if(Type == Relationships_Player._type.Influence && Points > 0)
            {
                Points = Mathf.RoundToInt(Points * (1 + ISFJBonus));
            }
        }
    }

    /// <summary>
    /// Harmony patch for getting the appeal of a stat for a girl.
    /// Increases the appeal to hardcore fans if the girl has the ISFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "GetAppealOfStat")]
    public class Data_girls_girls_GetAppealOfStat
    {

        /// <summary>
        /// Harmony patch for getting the appeal of a stat for a girl.
        /// Increases the appeal to hardcore fans if the girl has the ISFP MBTI type.
        /// </summary>
        /// <param name="__result">Calculated appeal of the stat.</param>
        /// <param name="_FanType">Type of fan being appealed to.</param>
        /// <param name="__instance">Instance of the girl being patched.</param>
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            if (GetGirlMBTI(__instance) != MBTI.ISFP)
                return;

            if (_FanType == resources.fanType.hardcore)
            {
                __result *= 1 + ISFPBonus;
            }
        }
    }

    /// <summary>
    /// Harmony patch for generating sales for a single.
    /// Applies the INFP bonus to handshake events.
    /// </summary>
    [HarmonyPatch(typeof(singles), "GenerateSales")]
    public class singles_GenerateSales
    {
        /// <summary>
        /// Harmony patch for generating sales for a single.
        /// Applies the INFP bonus to handshake events.
        /// </summary>
        /// <param name="single">Single instance being patched.</param>
        public static void Prefix(singles._single single)
        {
            if(single.IsGroupHS() || single.IsIndividualHS())
            {
                patchGetFan_Count_INFP = true;
            }
        }

        /// <summary>
        /// Harmony patch for generating sales for a single.
        /// Applies the INFP bonus to handshake events.
        /// </summary>
        public static void Postfix()
        {
            patchGetFan_Count_INFP = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the fan count for a girl.
    /// Increases the fan count if the girl has the INFP MBTI type and it's a handshake event.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "GetFan_Count", new Type[] { typeof(resources.fanType), typeof(resources.fanType), typeof(resources.fanType) })]
    public class data_girls_girls_GetFan_Count_INFP
    {

        /// <summary>
        /// Harmony patch for getting the fan count for a girl.
        /// Increases the fan count if the girl has the INFP MBTI type and it's a handshake event.
        /// </summary>
        /// <param name="__result">Calculated fan count.</param>
        /// <param name="__instance">Instance of the girl being patched.</param>
        public static void Postfix(ref long __result, data_girls.girls __instance)
        {
            if (!patchGetFan_Count_INFP)
                return;

            MBTI mBTI = GetGirlMBTI(__instance);
            if (mBTI != MBTI.INFP)
                return;

            __result = (long)Mathf.Round(__result * (1 + INFPBonus));
        }
    }

    /// <summary>
    /// Harmony patch for setting up a business proposal.
    /// Reduces the stamina cost if the girl has the ENFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Business_Popup), "Set")]
    public class Business_Popup_Set
    {

        /// <summary>
        /// Harmony patch for setting up a business proposal.
        /// Reduces the stamina cost if the girl has the ENFP MBTI type.
        /// </summary>
        /// <param name="_proposal">Business proposal being set up.</param>
        /// <param name="__state">Original stamina cost state.</param>
        public static void Prefix(ref business._proposal _proposal, ref int __state)
        {
            __state = 0;
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
        public static void Postfix(ref business._proposal _proposal, ref int __state)
        {
            if (__state == 0)
                return;

            _proposal.stamina = __state;

        }
    }

    /// <summary>
    /// Harmony patch for accepting a business proposal.
    /// Reduces the stamina cost if the girl has the ENFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(business), "Accept")]
    public class business_Accept
    {
        /// <summary>
        /// Harmony patch for accepting a business proposal.
        /// Reduces the stamina cost if the girl has the ENFP MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the business being patched.</param>
        public static void Prefix(ref business __instance)
        {
            if (GetGirlMBTI(__instance.ActiveProposal.girl) != MBTI.ENFP)
                return;

            __instance.ActiveProposal.stamina = Mathf.RoundToInt(__instance.ActiveProposal.stamina * (1 - ENFPBonus));
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameter changes during birthdays.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(Birthday_Popup), "DoParam")]
    public class Birthday_Popup_DoParam
    {
        /// <summary>
        /// Harmony patch for calculating parameter changes during birthdays.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="Prm">Type of parameter being changed.</param>
        /// <param name="__instance">Instance of the birthday popup being patched.</param>
        [HarmonyPriority(Priority.High)]
        public static void Prefix(data_girls._paramType Prm, ref Birthday_Popup __instance)
        {
            if (GetGirlMBTI(__instance.Girl) != MBTI.INTJ)
                return;

            if (Prm == data_girls._paramType.smart)
            {
                patchSetVal_INTJ = true;
                patchSet_INTJ = true;
            }

        }

        /// <summary>
        /// Harmony patch for calculating parameter changes during birthdays.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        public static void Postfix()
        {
            patchSetVal_INTJ = false;
            patchSet_INTJ = false;
        }
    }

    /// <summary>
    /// Harmony patch for setting the value of a girl's parameter.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), "setVal")]
    public class data_girls_girls_param_setVal
    {
        /// <summary>
        /// Harmony patch for setting the value of a girl's parameter.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="newVal">New value of the parameter.</param>
        /// <param name="__instance">Instance of the girl's parameter being patched.</param>
        public static void Prefix(ref float newVal, data_girls.girls.param __instance)
        {
            if (!patchSetVal_INTJ)
                return;

            float paramVal = __instance.val;
            newVal = Mathf.Min(100, (1 + INTJBonus) * (newVal - paramVal) + paramVal);
        }
    }

    /// <summary>
    /// Harmony patch for setting the stat value during a birthday.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(Birthday_Stat), "Set")]
    public class Birthday_Stat_Set
    {
        /// <summary>
        /// Harmony patch for setting the stat value during a birthday.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="OldVal">Old value of the stat.</param>
        /// <param name="NewVal">New value of the stat.</param>
        public static void Prefix(float OldVal, ref float NewVal)
        {
            if (!patchSet_INTJ)
                return;

            NewVal = Mathf.Min(100, (1 + INTJBonus) * (NewVal - OldVal) + OldVal);
        }
    }

    /// <summary>
    /// Harmony patch for reducing novelty decreases of a cafe dish.
    /// Applies the INTP bonus to dish novelty.
    /// </summary>
    [HarmonyPatch(typeof(Cafes._cafe._dish), "AddNovelty")]
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
    /// Harmony patch for calculating the duration of a girl's training.
    /// Reduces the duration if the girl has the ESTJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), "GetDuration")]
    public class data_girls_girls_param_GetDuration
    {
        /// <summary>
        /// Harmony patch for calculating the duration of a girl's training.
        /// Reduces the duration if the girl has the ESTJ MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the girl's parameter being patched.</param>
        /// <param name="__result">Calculated training duration.</param>
        public static void Postfix(data_girls.girls.param __instance, ref float __result)
        {
            if (GetGirlMBTI(__instance.Parent) != MBTI.ESTJ)
                return;

            __result *= 1 - ESTJBonus;
        }
    }

    /// <summary>
    /// Harmony patch for calculating the price coefficient for a theater.
    /// Increases the price coefficient based on the number of girls with the ESFP MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Theaters._theater), "GetPriceCoeff")]
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
    [HarmonyPatch(typeof(data_girls), "GetTeamChemistry")]
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


    /// <summary>
    /// Harmony patch for generating results for the SSK event.
    /// Applies the ENFJ bonus to vote counts.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_SSK._SSK), "GenerateResults")]
    public class SEvent_SSK__SSK_GenerateResults
    {
        /// <summary>
        /// Harmony patch for generating results for the SSK event.
        /// Applies the ENFJ bonus to vote counts.
        /// </summary>
        public static void Prefix()
        {
            patchGetFan_Count_ENFJ = true;
        }

        /// <summary>
        /// Harmony patch for generating results for the SSK event.
        /// Applies the ENFJ bonus to vote counts.
        /// </summary>
        public static void Postfix()
        {
            patchGetFan_Count_ENFJ = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the fan count for a girl during the SSK event.
    /// Increases the fan count if the girl has the ENFJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "GetFan_Count", new Type[] { typeof(resources.fanType) })]
    public class data_girls_girls_GetFan_Count_ENFJ
    {
        /// <summary>
        /// Harmony patch for getting the fan count for a girl during the SSK event.
        /// Increases the fan count if the girl has the ENFJ MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the girl being patched.</param>
        /// <param name="__result">Calculated fan count.</param>
        public static void Postfix(data_girls.girls __instance, ref long __result)
        {
            if (!patchGetFan_Count_ENFJ)
                return;

            if (GetGirlMBTI(__instance) != MBTI.ENFJ)
                return;

            __result = (long)Mathf.Round(__result * (1 + ENFJBonus));
        }
    }



    /// <summary>
    /// Harmony patch for getting the girl coefficient for a business proposal.
    /// Applies MBTI-related bonuses to the proposal.
    /// </summary>
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff
    {
        /// <summary>
        /// Harmony patch for getting the girl coefficient for a business proposal.
        /// Applies MBTI-related bonuses to the proposal.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        /// <summary>
        /// Harmony patch for getting the girl coefficient for a business proposal.
        /// Applies MBTI-related bonuses to the proposal.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
        }
    }


    /// <summary>
    /// Harmony patch for getting the average parameter value for a group of girls.
    /// Applies MBTI-related bonuses to the parameter value.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam
    {
        /// <summary>
        /// Harmony patch for getting the average parameter value for a group of girls.
        /// Applies MBTI-related bonuses to the parameter value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
            isShow = true;
        }

        /// <summary>
        /// Harmony patch for getting the average parameter value for a group of girls.
        /// Applies MBTI-related bonuses to the parameter value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
            isShow = false;
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameters for a show's senbatsu.
    /// Applies MBTI-related bonuses to the parameters.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam
    {
        /// <summary>
        /// Harmony patch for calculating parameters for a show's senbatsu.
        /// Applies MBTI-related bonuses to the parameters.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
            isShow = true;
        }

        /// <summary>
        /// Harmony patch for calculating parameters for a show's senbatsu.
        /// Applies MBTI-related bonuses to the parameters.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
            isShow = false;
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameters for a single's senbatsu.
    /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
    /// </summary>
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam
    {
        /// <summary>
        /// Harmony patch for calculating parameters for a single's senbatsu.
        /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
        /// </summary>
        /// <param name="__instance">Instance of the single being patched.</param>
        [HarmonyPriority(Priority.First)]
        public static void Prefix(singles._single __instance)
        {
            patchGetVal = true;

            isRisky = __instance.GetRiskyMarketing() != null;
        }

        /// <summary>
        /// Harmony patch for calculating parameters for a single's senbatsu.
        /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
            isRisky = null;
        }
    }

    /// <summary>
    /// Harmony patch for getting the skill value for a concert song.
    /// Applies MBTI-related bonuses to the skill value.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        /// <summary>
        /// Harmony patch for getting the skill value for a concert song.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        /// <summary>
        /// Harmony patch for getting the skill value for a concert song.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the skill value for a concert MC.
    /// Applies MBTI-related bonuses to the skill value.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        /// <summary>
        /// Harmony patch for getting the skill value for a concert MC.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }
        /// <summary>
        /// Harmony patch for getting the skill value for a concert MC.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {

            patchGetVal = false;
        }
    }

    /// <summary>
    /// Harmony patch to modify game parameters based on MBTI traits assigned to idols.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        /// <summary>
        /// Postfix method to add trait-based modifiers to parameter values.
        /// </summary>
        /// <param name="__result">The original result of the parameter value.</param>
        /// <param name="__instance">Instance of the parameter being accessed.</param>
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (patchGetVal)
            {
                __result += GetTraitModifier(__instance.Parent, __instance.type);
            }
        }
    }

    /// <summary>
    /// Handles MBTI personalities and their effects on gameplay mechanics for idols.
    /// </summary>
    public class MBTIPersonalities
    {
        public const string MBTINodeID = "mbti";
        public const string constantTitlePrefix = "MBTI__TITLE_";
        public const string constantDescPrefix = "MBTI__DESC_";

        // Constants defining bonuses for each MBTI type
        public const float ENFPBonus = 0.1f;
        public const float INTJBonus = 1;
        public const float ISTPBonus = 0.5f;
        public const int ISTJBonus = 10;
        public const float ISFJBonus = 0.1f;
        public const float ISFPBonus = 0.1f;
        public const float INFPBonus = 0.1f;
        public const float INTPBonus = 0.5f;
        public const float ESTJBonus = 0.2f;
        public const float ESFPBonus = 0.05f;
        public const float ENFJBonus = 0.2f;
        public const float ESFJBonus = 5;

        // Stat bonuses for specific MBTI types
        public const float ENTJStatBonus = 5;
        public const float ENTPStatBonus = 5;
        public const float ESTPStatBonus = 5;
        public const float INFJStatBonus = 5;

        // Toggle flags for patching specific game behaviors
        public static bool patchGetVal = false;
        public static bool patchAddParam = false;
        public static bool patchGetFan_Count_INFP = false;
        public static bool patchGetFan_Count_ENFJ = false;
        public static bool patchSetVal_INTJ = false;
        public static bool patchSet_INTJ = false;

        // Other patch condition flags
        public static bool? isRisky = null;
        public static bool isShow = false;

        // Lists to store MBTI data references
        public static List<MBTITextureData> MBTITextureReferenceList = new();
        public static Dictionary<int, MBTI> MBTIReferenceDict = new();

        /// <summary>
        /// Calculates the modifier to idol parameters based on their MBTI traits.
        /// </summary>
        /// <param name="girls">The idol for whom the modifier is calculated.</param>
        /// <param name="type">Type of parameter to modify.</param>
        /// <returns>Modifier value based on MBTI traits.</returns>
        public static float GetTraitModifier(data_girls.girls girls, data_girls._paramType type)
        {
            float num = 0;
            if (girls != null && data_girls.IsStatParam(type))
            {
                MBTI mBTI = GetGirlMBTI(girls);
                switch (mBTI)
                {
                    case MBTI.ENTJ:
                        if (girls.Is_Pushed())
                            num += ENTJStatBonus;
                        break;

                    case MBTI.ENTP:
                        if (girls.GetScandalPoints() > 0)
                            num += ENTPStatBonus;
                        break;

                    case MBTI.ESTP:
                        if (isRisky ?? false)
                            num += ESTPStatBonus;
                        break;

                    case MBTI.INFJ:
                        if (isShow)
                            num += INFJStatBonus;
                        break;
                }
            }
            return num;
        }


        /// <summary>
        /// Retrieves the MBTI assigned to a specific idol.
        /// </summary>
        /// <param name="girls">The idol whose MBTI is retrieved.</param>
        /// <returns>MBTI type assigned to the idol.</returns>
        public static MBTI GetGirlMBTI(data_girls.girls girls)
        {
            if (MBTIReferenceDict.TryGetValue(girls.id, out MBTI mbti))
            {
                return mbti;
            }

            mbti = GenerateMBTI(girls);
            SetGirlMBTI(girls, mbti);
            return mbti;

        }

        /// <summary>
        /// Sets the MBTI for a specific idol.
        /// </summary>
        /// <param name="girls">The idol whose MBTI is set.</param>
        /// <param name="mbti">MBTI type to assign to the idol.</param>
        public static void SetGirlMBTI(data_girls.girls girls, MBTI mbti)
        {
            MBTIReferenceDict[girls.id] = mbti;
            girls.SetVariable(mbti.ToString());
        }

        /// <summary>
        /// Generates an MBTI type based on idol-specific attributes.
        /// </summary>
        /// <param name="girls">The idol for whom MBTI is generated.</param>
        /// <returns>Generated MBTI type.</returns>
        public static MBTI GenerateMBTI(data_girls.girls girls)
        {
            string id = girls.firstName + girls.lastName + girls.birthday.DayOfYear;
            uint hash = ComputeStringHash(id);
            int r = (int)(hash % 16U);
            MBTI mbtiType = (MBTI)(r + 1);

            return mbtiType;
        }

        /// <summary>
        /// Resets all stored MBTI data.
        /// </summary>
        public static void ResetMBTI()
        {
            MBTIReferenceDict.Clear();
        }

        /// <summary>
        /// Computes a hash value for a given string.
        /// </summary>
        /// <param name="s">String to compute hash for.</param>
        /// <returns>Computed hash value.</returns>
        private static uint ComputeStringHash(string s)
        {
            uint num = 0;
            if (s != null)
            {
                num = 2166136261U;
                for (int i = 0; i < s.Length; i++)
                {
                    num = (s[i] ^ num) * 16777619U;
                }
            }
            return num;
        }

        /// <summary>
        /// Class to store MBTI data associated with each unique idol.
        /// </summary>
        public class MBTITextureData
        {
            public string ModName;
            public int body_id;
            public MBTI mbti;
        }

        public enum MBTI
        {
            None,
            ISTJ,
            ISTP,
            ISFJ,
            ISFP,
            INFJ,
            INFP,
            INTJ,
            INTP,
            ESTP,
            ESTJ,
            ESFP,
            ESFJ,
            ENFP,
            ENFJ,
            ENTP,
            ENTJ
        }


    }

}
