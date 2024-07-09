using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using static TraitFix.TraitsFix;

namespace TraitFix
{

    [HarmonyPatch(typeof(data_girls), "AgeDeterioration")]
    public class Data_girls_AgeDeterioration
    {
        // Girls with Live Fast trait have double the rate of stat decreases after their peak age
        public static void Postfix()
        {
            foreach (data_girls.girls girls in data_girls.girl)
            {
                if (girls != null
                    && girls.status != data_girls._status.graduated
                    && girls.trait == traits._trait._type.Live_fast)
                { 
                    for(int i = 0; i < LIVEFAST_DETERIORATION - 1; i++)
                    {
                        girls.AgeDeterioration();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Birthday_Popup), "DoParam")]
    public class Birthday_Popup_DoParam
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            bool breakFlag = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (breakFlag && instructionList[i].opcode == OpCodes.Stloc_1)
                {
                    index = i;
                    break;
                }
                if (instructionList[i].opcode == OpCodes.Ldc_R4 && (float)instructionList[i].operand == 0.975f)
                {
                    breakFlag = true;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_1));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Ldloc_0));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Birthday_Popup_DoParam), "Infix")));
                instructionList.Insert(index + 5, new CodeInstruction(OpCodes.Stloc_1));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(Birthday_Popup __this, float newVal, float orig)
        {
            if (__this.Girl.trait != traits._trait._type.Live_fast)
                return orig;

            return LIVEFAST_MODIFIER * (newVal - orig) + orig;
        }
    }


    [HarmonyPatch(typeof(data_girls.girls), "GetAppealOfStat")]
    public class Data_girls_girls_GetAppealOfStat
    {
        // Girls with Trendy trait have 1.5x the appeal to non-adults and 0.5x the appeal to adults.
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            if (__instance.trait != traits._trait._type.Trendy)
                return;

            switch(_FanType)
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

    [HarmonyPatch(typeof(data_girls.girls), "UpdateDatingStatus")]
    public class Data_girls_girls_UpdateDatingStatus
    {

        // If there is an Indiscreet member, girls in dating relationships unknown to the player have a 2% chance
        // of having the relationship revealed each week
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (mainScript.chance(INDISCREET_CHANCE) && __instance.DatingData.Is_Taken() && !__instance.DatingData.Is_Partner_Status_Known)
            {
                bool leaker = false;
                foreach (data_girls.girls girls in data_girls.GetActiveGirls())
                {
                    if (girls != __instance && girls.trait == traits._trait._type.Indiscreet)
                    {
                        leaker = true;
                    }
                }
                if (!leaker)
                    return;

                string labelID = INDISCREET_LABEL_OUTSIDE;
                if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_forbidden)
                {
                    labelID = INDISCREET_LABEL_OUTSIDE_SCANDAL;
                    __instance.addParam(data_girls._paramType.scandalPoints, 1f, false);
                }

                NotificationManager.AddNotification(
                    Language.Insert(labelID, new string[] { __instance.GetName() }),
                    mainScript.red32,
                    NotificationManager._notification._type.idol_relationship_change
                    );

                __instance.getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                __instance.DatingData.Is_Partner_Status_Known = true;
                __instance.DatingData.Partner_Status_Known_To_Player = __instance.DatingData.Partner_Status;
            }
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), "CheckDating")]
    public class Relationships__relationship_CheckDating
    {
        // If there is an Indiscreet member, girls in dating relationships unknown to the player have a 2% chance
        // of having the relationship revealed each week
        public static void Postfix(ref Relationships._relationship __instance)
        {
            if (mainScript.chance(INDISCREET_CHANCE) && __instance.Dating && !__instance.IsRelationshipKnown())
            {
                bool leaker = false;
                foreach (data_girls.girls girls in data_girls.GetActiveGirls(null))
                {
                    if (girls != __instance.Girls[0] && girls != __instance.Girls[1] && girls.trait == traits._trait._type.Indiscreet)
                    {
                        leaker = true;
                    }
                }
                if (!leaker)
                    return;

                string labelID = INDISCREET_LABEL_OUTSIDE;
                if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_forbidden)
                {
                    labelID = INDISCREET_LABEL_INSIDE;
                    __instance.Girls[0].addParam(data_girls._paramType.scandalPoints, 1f, false);
                    __instance.Girls[1].addParam(data_girls._paramType.scandalPoints, 1f, false);
                }

                NotificationManager.AddNotification(Language.Insert(labelID, new string[]
                {
                        __instance.Girls[0].GetName(),
                        __instance.Girls[1].GetName()
                }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);

                __instance.Girls[0].getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                __instance.Girls[1].getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                __instance.Girls[0].DatingData.Is_Partner_Status_Known = true;
                __instance.Girls[1].DatingData.Is_Partner_Status_Known = true;
                __instance.Girls[0].DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
                __instance.Girls[1].DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
            }
        }
    }

    // Maternal and Precocious default relationship is positive based on age criteria
    [HarmonyPatch(typeof(Relationships._relationship), "Initialize")]
    public class Relationships__relationship_Initialize
    {
        public static void Postfix(ref Relationships._relationship __instance)
        {
            int age0 = __instance.Girls[0].GetAge();
            int age1 = __instance.Girls[1].GetAge();
            if (__instance.Girls[0].trait == traits._trait._type.Maternal && age0 > age1)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[1].trait == traits._trait._type.Maternal && age1 > age0)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[0].trait == traits._trait._type.Precocious && age0 < age1)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[1].trait == traits._trait._type.Precocious && age1 < age0)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
        }
    }

    // Maternal and Precocious default relationship is positive based on age criteria
    [HarmonyPatch(typeof(Relationships), "Do_Dynamic")]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if (relationship.Dynamic == Relationships._relationship._dynamic.positive)
                {
                    AdjustForAgeTraits(relationship);
                }

                // Get either last center of main group or last center of girl's group
                if (IsCenter(relationship.Girls[0])
                    && relationship.Girls[0].trait == traits._trait._type.Arrogant)
                {
                    relationship.Add(ARROGANT_PENALTY / 2);
                }
                else if(IsCenter(relationship.Girls[1])
                    && relationship.Girls[1].trait == traits._trait._type.Arrogant)
                {
                    relationship.Add(ARROGANT_PENALTY / 2);
                }
            }
        }

        private static void AdjustForAgeTraits(Relationships._relationship relationship)
        {
            // Maternal
            if (relationship.Girls[0].trait == traits._trait._type.Maternal && relationship.Girls[0].GetAge() > relationship.Girls[1].GetAge())
            {
                relationship.Add(MATERNAL_BONUS / 2);
            }
            else if (relationship.Girls[1].trait == traits._trait._type.Maternal && relationship.Girls[1].GetAge() > relationship.Girls[0].GetAge())
            {
                relationship.Add(MATERNAL_BONUS / 2);
            }

            // Precocious
            if (relationship.Girls[0].trait == traits._trait._type.Precocious && relationship.Girls[0].GetAge() < relationship.Girls[1].GetAge())
            {
                relationship.Add(PRECOCIOUS_BONUS / 2);
            }
            else if (relationship.Girls[1].trait == traits._trait._type.Precocious && relationship.Girls[1].GetAge() < relationship.Girls[0].GetAge())
            {
                relationship.Add(PRECOCIOUS_BONUS / 2);
            }
        }
    }

    // Girls with Forgiving trait will never dislike or hate any other girls
    [HarmonyPatch(typeof(Relationships._relationship), "Recalc")]
    public class Relationships__relationship_Recalc
    {
        public static void Postfix(ref Relationships._relationship __instance)
        {
            if (__instance.Ratio >= FORGIVING_THR)
                return;

            if (__instance.Girls[0].trait == traits._trait._type.Forgiving || __instance.Girls[1].trait == traits._trait._type.Forgiving)
            {
                __instance.Ratio = FORGIVING_THR;
            }

        }
    }

    // Girls with Meme Queen trait get +10 to all stats for internet shows
    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, ref Shows._show __instance)
        {
            if (type == data_girls._paramType.teamChemistry)
                return;

            foreach (data_girls.girls girls in girlList)
            {
                if (girls != null 
                    && !girls.IsSick()
                    && girls.trait == traits._trait._type.Meme_queen
                    && __instance.medium.media_type == Shows._param._media_type.internet)
                {
                    __instance.girlParams.Last().val += MEME_INT_SHOW;
                    break;
                }
            }
        }
    }

    // Girls with Meme Queen trait get +10 to all stats for internet shows
    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, ref List<data_girls.girls.param> ___girlParams, Shows._param ___medium)
        {
            if (type == data_girls._paramType.teamChemistry)
                return;

            foreach (data_girls.girls girls in girlList)
            {
                if (girls != null 
                    && !girls.IsSick()
                    && girls.trait == traits._trait._type.Meme_queen
                    && ___medium.media_type == Shows._param._media_type.internet)
                {
                    ___girlParams.Last().val += MEME_INT_SHOW;
                    break;
                }
            }
        }
    }

    // Update shows on medium selection just in case of meme queen
    [HarmonyPatch(typeof(Show_Popup), "SetParam")]
    public class Show_Popup_SetParam
    {
        public static void Postfix(ref Show_Popup __instance, Shows._show._castType? ___castType)
        {
            if (___castType == null)
                return;

            __instance.SetCastType(___castType.Value);
        }
    }

    // Girls with Meme Queen trait get +10% success rate and +5% crit success rate when participating in viral marketing campaigns
    [HarmonyPatch(typeof(singles._param), "GetSuccessChance", new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
    public class Singles__param_GetSuccessChance
    {
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, singles._single Single)
        {
            if (Single == null || __instance.Special_Type != singles._param._special_type.viral_campaign)
                return;

            bool flag = false;
            foreach (data_girls.girls girls in Single.girls)
            {
                if (girls != null 
                    && !girls.IsSick() 
                    && girls.trait == traits._trait._type.Meme_queen)
                {
                    flag = true;
                    break;
                }
            }
            if (flag)
            {
                switch(Result)
                {
                    case Single_Marketing_Roll._result.success:
                        __result += MEME_VIRAL_SUCCESS;
                        break;
                    case Single_Marketing_Roll._result.success_crit:
                        __result += MEME_VIRAL_SUCCESS_CRIT;
                        break;
                    case Single_Marketing_Roll._result.fail:
                        __result += MEME_VIRAL_FAIL;
                        break;
                }
            }
        }
    }

    // Girls with Annoying trait cause other members to spend 1.2x physical stamina in shows
    [HarmonyPatch(typeof(Shows._show), "SetStamina")]
    public class Shows__show_SetStamina
    {
        public static void Postfix(Shows._show __instance)
        {
            List<data_girls.girls> cast = __instance.GetCast();
            float staminaCost = __instance.GetStaminaCost();
            int annoyCount = 0;
            foreach (data_girls.girls girls in cast)
            {
                if (girls != null 
                    && girls.trait == traits._trait._type.Annoying 
                    && girls.IsActive())
                {
                    annoyCount++;
                }
            }
            if (annoyCount == 0)
                return;

            foreach (data_girls.girls girls2 in cast)
            {
                if (girls2.IsActive())
                {
                    if (annoyCount > 1 || (annoyCount == 1 && girls2.trait != traits._trait._type.Annoying))
                    {
                        girls2.addParam(data_girls._paramType.physicalStamina, -staminaCost * ANNOYING_MODIFIER, false);
                    }
                }
            }
        }
    }

    // Girls with Misandry trait have a 20% chance of receiving bad opinions from Male fans when participating in a single with handshakes
    [HarmonyPatch(typeof(singles), "ReleaseSingle")]
    public class Singles_ReleaseSingle
    {
        public static void Postfix(singles._single single)
        {
            foreach (data_girls.girls girls in single.girls)
            {
                if (girls != null 
                    && !girls.IsSick()
                    && girls.trait == traits._trait._type.Misandry 
                    && (single.IsIndividualHS() || single.IsGroupHS()) 
                    && mainScript.chance(20))
                {
                        girls.AddAppeal(resources.fanType.male, MISANDRY_MODIFIER);
                }
            }
        }
    }

    // Girls with Perfectionist trait get -20 to mental stamina when world tours end with less than 80% average attendance
    [HarmonyPatch(typeof(SEvent_Tour), "FinishTour")]
    public class SEvent_Tour_FinishTour
    {
        public static void Postfix(SEvent_Tour __instance)
        {
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls();
            foreach (data_girls.girls girls in activeGirls)
            {
                if (girls.trait == traits._trait._type.Perfectionist 
                    && __instance.Tour.GetAverageAttendance() < PERFECTIONIST_TOUR_ATT)
                {
                    girls.getParam(data_girls._paramType.mentalStamina).add(PERFECTIONIST_MENTAL, false);
                }
            }
        }
    }

    // Girls with Perfectionist trait get -20 to mental stamina when they participate in concerts with less than 100% hype.
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Finish")]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            foreach (data_girls.girls girls in __instance.GetGirls(true))
            {
                if (girls.trait == traits._trait._type.Perfectionist 
                    && __instance.Hype < PERFECTIONIST_HYPE)
                {
                    girls.getParam(data_girls._paramType.mentalStamina).add(PERFECTIONIST_MENTAL, false);
                }
            }
        }
    }


    // Apply traits to businesses
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            // Girls with Photogenic trait have +100% to photoshoots
            if (__instance.type == business._type.photoshoot 
                && _girl.trait == traits._trait._type.Photogenic)
            {
                __result += PHOTOGENIC_MODIFIER;
            }

            patchGetVal = false;
        }
    }


    // Stat changes for shows
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> Girls)
        {
            patchGetVal = true;
            showCast = Girls;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
            showCast = null;
        }
    }

    // Stat changes for shows (for team chemistray calc)
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> _girls)
        {
            patchGetVal = true;
            showCast = _girls;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
            showCast = null;
        }
    }

    // Stat changes for singles
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
        }
    }

    // Stat changes for concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
        }
    }


    // Stat changes for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }


        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            patchGetVal = false;
        }
    }

    // Apply traits to parameters and skills
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!patchGetVal)
                return;

            __result += GetTraitModifier(__instance.Parent, __instance.type, showCast);
        }
    }


    public class TraitsFix
    {
        public const int ANXIETY_MODIFIER = -10;
        public const int CLUMSY_DANCE_MODIFIER = -30;
        public const int CLUMSY_FUNNY_MODIFIER = 30;
        public const int WORRIER_MODIFIER = -20;
        public const int COMPLACENT_MODIFIER = -20;
        public const int LONEWOLF_MODIFIER = 40;
        public const int DEFEATIST_MODIFIER = -20;
        public const int UNDERDOG_MODIFIER = 20;
        public const float PHOTOGENIC_MODIFIER = 1f;
        public const float PERFECTIONIST_MENTAL = -20;
        public const float PERFECTIONIST_HYPE = 100;
        public const float PERFECTIONIST_TOUR_ATT = 80;
        public const float MISANDRY_MODIFIER = -1;
        public const float ANNOYING_MODIFIER = 0.2f;
        public const float MEME_VIRAL_SUCCESS = 10;
        public const float MEME_VIRAL_SUCCESS_CRIT = 5;
        public const float MEME_VIRAL_FAIL = -15;
        public const float MEME_INT_SHOW = 10;
        public const float FORGIVING_THR = 0.5f;
        public const float MATERNAL_BONUS = 0.3f;
        public const float PRECOCIOUS_BONUS = 0.3f;
        public const float ARROGANT_PENALTY = -0.5f;
        public const int INDISCREET_CHANCE = 2;
        public const float TRENDY_ADULT_MODIFIER = 0.5f;
        public const float TRENDY_YA_MODIFIER = 1.5f;
        public const float TRENDY_TEEN_MODIFIER = 1.5f;
        public const float LIVEFAST_MODIFIER = 2;
        public const int LIVEFAST_DETERIORATION = 2;

        public const string INDISCREET_LABEL_OUTSIDE = "IDOL__OUTSIDE_LEAK";
        public const string INDISCREET_LABEL_INSIDE = "IDOL__INSIDE_LEAK_SCANDAL";
        public const string INDISCREET_LABEL_OUTSIDE_SCANDAL = "IDOL__OUTSIDE_LEAK_SCANDAL";

        public static bool patchGetVal = false;
        public static bool patchSetVal = false;
        public static bool patchSet = false;

        public static List<data_girls.girls> showCast = null;

        // This method calculates the modifier to girl parameters based on their trait.
        public static int GetTraitModifier(data_girls.girls girls, data_girls._paramType type, List<data_girls.girls> cast = null)
        {
            if (girls != null && data_girls.IsStatParam(type))
            {
                switch(girls.trait)
                {
                    case traits._trait._type.Anxiety:
                        if (IsEventUpcoming())
                            return ANXIETY_MODIFIER;
                        break;
                    case traits._trait._type.Clumsy:
                        if (type == data_girls._paramType.dance)
                            return CLUMSY_DANCE_MODIFIER;

                        if (type == data_girls._paramType.funny)
                            return CLUMSY_FUNNY_MODIFIER;
                        break;
                    case traits._trait._type.Worrier:
                        if (resources.GetScandalPointsTotal() > 0L)
                            return WORRIER_MODIFIER;
                        break;
                    case traits._trait._type.Complacent:
                        if (IsCenter(girls)
                            && (type == data_girls._paramType.vocal || type == data_girls._paramType.dance))
                            return COMPLACENT_MODIFIER;
                        break;
                    case traits._trait._type.Lone_Wolf:
                        if (cast != null)
                        {
                            int count = 0;
                            foreach (data_girls.girls g in cast)
                            {
                                if (g != null && !g.IsSick())
                                    count++;

                                if (count > 1) break;
                            }
                            if (count == 1)
                                return LONEWOLF_MODIFIER;
                        }
                        break;
                }

                singles._single mainSingle = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
                singles._single groupSingle = singles.GetLatestReleasedSingle(false, girls.GetGroup());

                singles._single recentSingle = GetRecentSingle(groupSingle, mainSingle);

                if (recentSingle != null 
                    && (staticVars.dateTime - recentSingle.ReleaseData.ReleaseDate).Days >= staticVars.dateTime.Day 
                    && recentSingle.ReleaseData.Chart_Position != 1)
                {
                    if (girls.trait == traits._trait._type.Defeatist)
                    {
                        return DEFEATIST_MODIFIER;
                    }
                    else if (girls.trait == traits._trait._type.Underdog)
                    {
                        return UNDERDOG_MODIFIER;
                    }
                }
            }
            return 0;
        }

        public static bool IsCenter(data_girls.girls girl)
        {
            singles._single mainSingle = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
            singles._single groupSingle = singles.GetLatestReleasedSingle(false, girl.GetGroup());

            return (groupSingle?.GetCenter() == girl) || (mainSingle?.GetCenter() == girl);
        }

        private static bool IsEventUpcoming()
        {
            foreach (var tour in SEvent_Tour.Tours)
            {
                if (tour.Status != SEvent_Tour.tour._status.finished)
                {
                    return true;
                }
            }
            foreach (var sSK in SEvent_SSK.Elections)
            {
                if (sSK.Status != SEvent_Tour.tour._status.finished)
                {
                    return true;
                }
            }
            foreach (var concert in SEvent_Concerts.Concerts)
            {
                if (concert.Status != SEvent_Tour.tour._status.finished)
                {
                    return true;
                }
            }
            return false;
        }

        // Helper method to get the more recent single based on release date and sales
        private static singles._single GetRecentSingle(singles._single groupSingle, singles._single mainSingle)
        {
            if (groupSingle == null) return mainSingle;
            if (mainSingle == null) return groupSingle;

            if (groupSingle.ReleaseData.ReleaseDate > mainSingle.ReleaseData.ReleaseDate)
            {
                return groupSingle;
            }
            else if (groupSingle.ReleaseData.ReleaseDate < mainSingle.ReleaseData.ReleaseDate)
            {
                return mainSingle;
            }
            else if (groupSingle.ReleaseData.Sales > mainSingle.ReleaseData.Sales)
            {
                return groupSingle;
            }
            else
            {
                return mainSingle;
            }
        }
    }

}
