using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    [HarmonyPatch(typeof(data_girls), "AgeDeterioration")]
    public class Data_girls_AgeDeterioration
    {
        // Live Fast: double random post-peak deterioration.
        public static void Postfix()
        {
            if (data_girls.girl == null)
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
    [HarmonyPatch(typeof(Birthday_Popup), "DoParam")]
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

    [HarmonyPatch(typeof(data_girls.girls.param), "setVal")]
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

    [HarmonyPatch(typeof(Birthday_Stat), "Set")]
    public class Birthday_Stat_Set
    {
        [HarmonyPriority(Priority.VeryLow)]
        public static void Prefix(data_girls._paramType Type, float OldVal, ref float NewVal)
        {
            NewVal = AdjustBirthdayDeteriorationDisplay(Type, OldVal, NewVal);
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), "GetAppealOfStat")]
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

    [HarmonyPatch(typeof(data_girls.girls), "UpdateDatingStatus")]
    public class Data_girls_girls_UpdateDatingStatus
    {
        // Outside relationships get one reveal roll; idol-idol dating is handled by CheckDating below.
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance?.DatingData == null
                || !__instance.DatingData.Is_Taken_Outside()
                || __instance.DatingData.Is_Partner_Status_Known
                || !mainScript.chance(INDISCREET_CHANCE)
                || !HasIndiscreetLeaker(__instance))
            {
                return;
            }

            string labelID = INDISCREET_LABEL_OUTSIDE;
            if (IsDatingForbidden())
            {
                labelID = INDISCREET_LABEL_OUTSIDE_SCANDAL;
                __instance.addParam(data_girls._paramType.scandalPoints, 1f, false);
            }

            NotificationManager.AddNotification(
                Language.Insert(labelID, new string[] { __instance.GetName() }),
                mainScript.red32,
                NotificationManager._notification._type.idol_relationship_change);

            __instance.getParam(data_girls._paramType.mentalStamina)?.add(-30f, false);
            __instance.DatingData.Is_Partner_Status_Known = true;
            __instance.DatingData.Partner_Status_Known_To_Player = __instance.DatingData.Partner_Status;
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), "CheckDating")]
    public class Relationships__relationship_CheckDating
    {
        public static void Postfix(Relationships._relationship __instance)
        {
            if (!TryGetRelationshipGirls(__instance, out data_girls.girls girl0, out data_girls.girls girl1) || !__instance.Dating)
                return;

            // Repair old one-sided knowledge and prevent the same relationship leaking every week.
            if (girl0.DatingData.Is_Partner_Status_Known || girl1.DatingData.Is_Partner_Status_Known)
            {
                MarkIdolRelationshipKnown(girl0, girl1);
                return;
            }
            if (__instance.IsRelationshipKnown())
                return;

            if (!mainScript.chance(INDISCREET_CHANCE) || !HasIndiscreetLeaker(girl0, girl1))
                return;

            string labelID = INDISCREET_LABEL_OUTSIDE;
            if (IsDatingForbidden())
            {
                labelID = INDISCREET_LABEL_INSIDE;
                girl0.addParam(data_girls._paramType.scandalPoints, 1f, false);
                girl1.addParam(data_girls._paramType.scandalPoints, 1f, false);
            }

            NotificationManager.AddNotification(
                Language.Insert(labelID, new string[] { girl0.GetName(), girl1.GetName() }),
                mainScript.red32,
                NotificationManager._notification._type.idol_relationship_change);

            girl0.getParam(data_girls._paramType.mentalStamina)?.add(-30f, false);
            girl1.getParam(data_girls._paramType.mentalStamina)?.add(-30f, false);
            MarkIdolRelationshipKnown(girl0, girl1);
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), "Initialize")]
    public class Relationships__relationship_Initialize
    {
        public static void Postfix(Relationships._relationship __instance)
        {
            if (!TryGetRelationshipGirls(__instance, out data_girls.girls girl0, out data_girls.girls girl1))
                return;

            int age0 = girl0.GetAge();
            int age1 = girl1.GetAge();
            if (girl0.trait == traits._trait._type.Maternal && age0 > age1)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl1.trait == traits._trait._type.Maternal && age1 > age0)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl0.trait == traits._trait._type.Precocious && age0 < age1)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            else if (girl1.trait == traits._trait._type.Precocious && age1 < age0)
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
        }
    }

    [HarmonyPatch(typeof(Relationships), "Do_Dynamic")]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            if (Relationships.RelationshipsData == null)
                return;

            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if (!TryGetRelationshipGirls(relationship, out data_girls.girls girl0, out data_girls.girls girl1))
                    continue;

                if (relationship.Dynamic == Relationships._relationship._dynamic.positive)
                    AdjustForAgeTraits(relationship, girl0, girl1);

                if (IsCenter(girl0) && girl0.trait == traits._trait._type.Arrogant)
                    relationship.Add(ARROGANT_PENALTY / 2f);
                else if (IsCenter(girl1) && girl1.trait == traits._trait._type.Arrogant)
                    relationship.Add(ARROGANT_PENALTY / 2f);
            }
        }

        private static void AdjustForAgeTraits(Relationships._relationship relationship, data_girls.girls girl0, data_girls.girls girl1)
        {
            // Relationship.Add halves positive values. Vanilla already contributes +0.05;
            // Add(0.3) contributes +0.15 more, producing the advertised 4x (+0.20 total).
            if (girl0.trait == traits._trait._type.Maternal && girl0.GetAge() > girl1.GetAge())
                relationship.Add(MATERNAL_BONUS);
            else if (girl1.trait == traits._trait._type.Maternal && girl1.GetAge() > girl0.GetAge())
                relationship.Add(MATERNAL_BONUS);

            if (girl0.trait == traits._trait._type.Precocious && girl0.GetAge() < girl1.GetAge())
                relationship.Add(PRECOCIOUS_BONUS);
            else if (girl1.trait == traits._trait._type.Precocious && girl1.GetAge() < girl0.GetAge())
                relationship.Add(PRECOCIOUS_BONUS);
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), "Recalc")]
    public class Relationships__relationship_Recalc
    {
        public static void Postfix(Relationships._relationship __instance)
        {
            if (!TryGetRelationshipGirls(__instance, out data_girls.girls girl0, out data_girls.girls girl1)
                || __instance.Ratio >= FORGIVING_THR)
                return;

            if (girl0.trait == traits._trait._type.Forgiving || girl1.trait == traits._trait._type.Forgiving)
                __instance.Ratio = FORGIVING_THR;
        }
    }

    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, Shows._show __instance)
        {
            if (type == data_girls._paramType.teamChemistry
                || __instance?.medium == null
                || __instance.medium.media_type != Shows._param._media_type.internet
                || !HasActiveMemeQueen(girlList)
                || __instance.girlParams == null
                || __instance.girlParams.Count == 0)
                return;

            data_girls.girls.param param = __instance.girlParams[__instance.girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, List<data_girls.girls.param> ___girlParams, Shows._param ___medium)
        {
            if (type == data_girls._paramType.teamChemistry
                || ___medium == null
                || ___medium.media_type != Shows._param._media_type.internet
                || !HasActiveMemeQueen(girlList)
                || ___girlParams == null
                || ___girlParams.Count == 0)
                return;

            data_girls.girls.param param = ___girlParams[___girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    // Recalculate only when the medium changes, avoiding a recalculation on every SetParam.
    [HarmonyPatch(typeof(Show_Popup), "SetParam")]
    public class Show_Popup_SetParam
    {
        public static void Postfix(Show_Popup __instance, Show_Popup_Param_Button._type type, Shows._show._castType? ___castType)
        {
            if (__instance == null || type != Show_Popup_Param_Button._type.medium || ___castType == null)
                return;
            __instance.SetCastType(___castType.Value);
        }
    }

    [HarmonyPatch(typeof(singles._param), "GetSuccessChance", new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
    public class Singles__param_GetSuccessChance
    {
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, singles._single Single)
        {
            if (__instance == null || Single?.girls == null || __instance.Special_Type != singles._param._special_type.viral_campaign)
                return;

            bool hasMemeQueen = false;
            foreach (data_girls.girls girl in Single.girls)
            {
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Meme_queen)
                {
                    hasMemeQueen = true;
                    break;
                }
            }
            if (!hasMemeQueen)
                return;

            // Do not patch fail directly: vanilla derives regular fail chance from the
            // success/crit chances, so subtracting 15 from fail double-counted this bonus.
            if (Result == Single_Marketing_Roll._result.success)
                __result += MEME_VIRAL_SUCCESS;
            else if (Result == Single_Marketing_Roll._result.success_crit)
                __result += MEME_VIRAL_SUCCESS_CRIT;
        }
    }

    [HarmonyPatch(typeof(Shows._show), "SetStamina")]
    public class Shows__show_SetStamina
    {
        public static void Postfix(Shows._show __instance)
        {
            List<data_girls.girls> cast = __instance?.GetCast();
            if (cast == null || cast.Count == 0)
                return;

            float staminaCost = __instance.GetStaminaCost();
            int annoyingCount = 0;
            foreach (data_girls.girls girl in cast)
            {
                if (girl != null && girl.trait == traits._trait._type.Annoying && girl.IsActive())
                    annoyingCount++;
            }
            if (annoyingCount == 0)
                return;

            foreach (data_girls.girls girl in cast)
            {
                if (girl == null || !girl.IsActive())
                    continue;
                if (annoyingCount > 1 || girl.trait != traits._trait._type.Annoying)
                    girl.addParam(data_girls._paramType.physicalStamina, -staminaCost * ANNOYING_MODIFIER, false);
            }
        }
    }

    [HarmonyPatch(typeof(singles), "ReleaseSingle")]
    public class Singles_ReleaseSingle
    {
        public static void Postfix(singles._single single)
        {
            if (single?.girls == null)
                return;

            bool hasHandshake = single.IsIndividualHS() || single.IsGroupHS();
            if (!hasHandshake)
                return;

            foreach (data_girls.girls girl in single.girls)
            {
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Misandry && mainScript.chance(20))
                    girl.AddAppeal(resources.fanType.male, MISANDRY_MODIFIER);
            }
        }
    }

    // FinishTour clears Tour before returning, so capture the attendance result first.
    [HarmonyPatch(typeof(SEvent_Tour), "FinishTour")]
    public class SEvent_Tour_FinishTour
    {
        public static void Prefix(SEvent_Tour __instance, ref bool __state)
        {
            __state = __instance?.Tour != null && __instance.Tour.GetAverageAttendance() < PERFECTIONIST_TOUR_ATT;
        }

        public static void Postfix(bool __state)
        {
            if (!__state)
                return;
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls();
            if (activeGirls == null)
                return;

            foreach (data_girls.girls girl in activeGirls)
            {
                if (girl != null && girl.trait == traits._trait._type.Perfectionist)
                    girl.getParam(data_girls._paramType.mentalStamina)?.add(PERFECTIONIST_MENTAL, false);
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Finish")]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            if (__instance == null || __instance.Hype >= PERFECTIONIST_HYPE)
                return;
            List<data_girls.girls> girls = __instance.GetGirls(true);
            if (girls == null)
                return;

            foreach (data_girls.girls girl in girls)
            {
                if (girl != null && girl.trait == traits._trait._type.Perfectionist)
                    girl.getParam(data_girls._paramType.mentalStamina)?.add(PERFECTIONIST_MENTAL, false);
            }
        }
    }

    // Trait stat contexts use a stack plus Finalizers so nested calls and exceptions cannot poison later GetVal calls.
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            if (__instance != null && _girl != null && __instance.type == business._type.photoshoot && _girl.trait == traits._trait._type.Photogenic)
                __result += PHOTOGENIC_MODIFIER;
        }

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> Girls) => BeginTraitCalculation(Girls);

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(List<data_girls.girls> _girls) => BeginTraitCalculation(_girls);

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix() => BeginTraitCalculation();

        public static Exception Finalizer(Exception __exception)
        {
            EndTraitCalculation();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!IsTraitCalculationActive || __instance == null)
                return;
            __result += GetTraitModifier(__instance.Parent, __instance.type, CurrentTraitCast);
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
        public const float MEME_INT_SHOW = 10;
        public const float FORGIVING_THR = 0.5f;
        public const float MATERNAL_BONUS = 0.3f;
        public const float PRECOCIOUS_BONUS = 0.3f;
        public const float ARROGANT_PENALTY = -0.5f;
        public const int INDISCREET_CHANCE = 2;
        public const float TRENDY_ADULT_MODIFIER = 0.5f;
        public const float TRENDY_YA_MODIFIER = 1.5f;
        public const float TRENDY_TEEN_MODIFIER = 1.5f;
        public const float LIVEFAST_MODIFIER = 2f;
        public const int LIVEFAST_DETERIORATION = 2;

        public const string INDISCREET_LABEL_OUTSIDE = "IDOL__OUTSIDE_LEAK";
        public const string INDISCREET_LABEL_INSIDE = "IDOL__INSIDE_LEAK_SCANDAL";
        public const string INDISCREET_LABEL_OUTSIDE_SCANDAL = "IDOL__OUTSIDE_LEAK_SCANDAL";

        private sealed class TraitCalculationContext
        {
            public List<data_girls.girls> Cast;
        }

        private sealed class BirthdayDeteriorationContext
        {
            public data_girls.girls Girl;
            public data_girls._paramType Type;
            public float OldValue;
        }

        private static readonly Stack<TraitCalculationContext> traitCalculationContexts = new Stack<TraitCalculationContext>();
        private static readonly Stack<BirthdayDeteriorationContext> birthdayDeteriorationContexts = new Stack<BirthdayDeteriorationContext>();

        public static bool IsTraitCalculationActive => traitCalculationContexts.Count > 0;
        public static List<data_girls.girls> CurrentTraitCast => IsTraitCalculationActive ? traitCalculationContexts.Peek().Cast : null;

        public static int GetTraitModifier(data_girls.girls girl, data_girls._paramType type, List<data_girls.girls> cast = null)
        {
            if (girl == null || !data_girls.IsStatParam(type))
                return 0;

            switch (girl.trait)
            {
                case traits._trait._type.Anxiety:
                    if (IsEventUpcoming()) return ANXIETY_MODIFIER;
                    break;
                case traits._trait._type.Clumsy:
                    if (type == data_girls._paramType.dance) return CLUMSY_DANCE_MODIFIER;
                    if (type == data_girls._paramType.funny) return CLUMSY_FUNNY_MODIFIER;
                    break;
                case traits._trait._type.Worrier:
                    if (resources.GetScandalPointsTotal() > 0L) return WORRIER_MODIFIER;
                    break;
                case traits._trait._type.Complacent:
                    if (IsCenter(girl) && (type == data_girls._paramType.vocal || type == data_girls._paramType.dance))
                        return COMPLACENT_MODIFIER;
                    break;
                case traits._trait._type.Lone_Wolf:
                    if (cast != null)
                    {
                        int count = 0;
                        foreach (data_girls.girls castGirl in cast)
                        {
                            if (castGirl != null && castGirl.IsActive() && !castGirl.IsSick())
                                count++;
                            if (count > 1) break;
                        }
                        if (count == 1) return LONEWOLF_MODIFIER;
                    }
                    break;
            }

            if (girl.trait == traits._trait._type.Defeatist || girl.trait == traits._trait._type.Underdog)
            {
                singles._single mainSingle = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
                Groups._group girlGroup = girl.GetGroup();
                singles._single groupSingle = girlGroup != null ? singles.GetLatestReleasedSingle(false, girlGroup) : null;
                singles._single recentSingle = GetRecentSingle(groupSingle, mainSingle);
                if (DidSingleMissNumberOne(recentSingle))
                    return girl.trait == traits._trait._type.Defeatist ? DEFEATIST_MODIFIER : UNDERDOG_MODIFIER;
            }

            return 0;
        }

        public static bool IsCenter(data_girls.girls girl)
        {
            if (girl == null)
                return false;
            singles._single mainSingle = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
            Groups._group girlGroup = girl.GetGroup();
            singles._single groupSingle = girlGroup != null ? singles.GetLatestReleasedSingle(false, girlGroup) : null;
            return groupSingle?.GetCenter() == girl || mainSingle?.GetCenter() == girl;
        }

        public static void BeginTraitCalculation(List<data_girls.girls> cast = null)
        {
            traitCalculationContexts.Push(new TraitCalculationContext { Cast = cast });
        }

        public static void EndTraitCalculation()
        {
            if (traitCalculationContexts.Count > 0)
                traitCalculationContexts.Pop();
        }

        public static bool BeginBirthdayDeterioration(data_girls.girls girl, data_girls._paramType type)
        {
            if (girl == null
                || girl.trait != traits._trait._type.Live_fast
                || girl.GetAge() <= girl.peakAge
                || type == data_girls._paramType.funny
                || type == data_girls._paramType.smart)
                return false;

            data_girls.girls.param param = girl.getParam(type);
            if (param == null)
                return false;

            birthdayDeteriorationContexts.Push(new BirthdayDeteriorationContext
            {
                Girl = girl,
                Type = type,
                OldValue = param.val
            });
            return true;
        }

        public static void EndBirthdayDeterioration()
        {
            if (birthdayDeteriorationContexts.Count > 0)
                birthdayDeteriorationContexts.Pop();
        }

        public static float AdjustBirthdayDeterioration(data_girls.girls girl, data_girls._paramType type, float newValue)
        {
            if (birthdayDeteriorationContexts.Count == 0)
                return newValue;
            BirthdayDeteriorationContext context = birthdayDeteriorationContexts.Peek();
            if (context.Girl != girl || context.Type != type || newValue >= context.OldValue)
                return newValue;
            return Mathf.Clamp(context.OldValue + LIVEFAST_MODIFIER * (newValue - context.OldValue), 1f, 100f);
        }

        public static float AdjustBirthdayDeteriorationDisplay(data_girls._paramType type, float oldValue, float newValue)
        {
            if (birthdayDeteriorationContexts.Count == 0)
                return newValue;
            BirthdayDeteriorationContext context = birthdayDeteriorationContexts.Peek();
            if (context.Type != type || newValue >= oldValue)
                return newValue;
            return Mathf.Clamp(oldValue + LIVEFAST_MODIFIER * (newValue - oldValue), 1f, 100f);
        }

        public static bool IsDatingForbidden()
        {
            policies.value datingPolicy = policies.GetSelectedPolicyValue(policies._type.dating);
            return datingPolicy != null && datingPolicy.Value == policies._value.dating_forbidden;
        }

        public static bool HasActiveMemeQueen(List<data_girls.girls> girls)
        {
            if (girls == null)
                return false;
            foreach (data_girls.girls girl in girls)
            {
                if (girl != null && girl.IsActive() && !girl.IsSick() && girl.trait == traits._trait._type.Meme_queen)
                    return true;
            }
            return false;
        }

        public static bool HasIndiscreetLeaker(params data_girls.girls[] excludedGirls)
        {
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls(null);
            if (activeGirls == null)
                return false;

            foreach (data_girls.girls girl in activeGirls)
            {
                if (girl == null || girl.trait != traits._trait._type.Indiscreet)
                    continue;
                bool excluded = false;
                if (excludedGirls != null)
                {
                    foreach (data_girls.girls excludedGirl in excludedGirls)
                    {
                        if (girl == excludedGirl)
                        {
                            excluded = true;
                            break;
                        }
                    }
                }
                if (!excluded)
                    return true;
            }
            return false;
        }

        public static bool TryGetRelationshipGirls(Relationships._relationship relationship, out data_girls.girls girl0, out data_girls.girls girl1)
        {
            girl0 = null;
            girl1 = null;
            if (relationship?.Girls == null || relationship.Girls.Count < 2)
                return false;
            girl0 = relationship.Girls[0];
            girl1 = relationship.Girls[1];
            return girl0 != null && girl1 != null && girl0.DatingData != null && girl1.DatingData != null;
        }

        public static void MarkIdolRelationshipKnown(data_girls.girls girl0, data_girls.girls girl1)
        {
            if (girl0?.DatingData == null || girl1?.DatingData == null)
                return;
            girl0.DatingData.Is_Partner_Status_Known = true;
            girl1.DatingData.Is_Partner_Status_Known = true;
            girl0.DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
            girl1.DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
        }

        private static bool DidSingleMissNumberOne(singles._single single)
        {
            if (single?.ReleaseData == null)
                return false;

            DateTime releaseDate = single.ReleaseData.ReleaseDate;
            if (releaseDate.Year == staticVars.dateTime.Year && releaseDate.Month == staticVars.dateTime.Month)
                return false;

            int chartPosition = single.ReleaseData.Chart_Position;
            if (chartPosition <= 0)
                chartPosition = ResolveChartPosition(single);
            return chartPosition > 1;
        }

        private static int ResolveChartPosition(singles._single single)
        {
            if (single?.ReleaseData == null || Rivals.Date_To_Month == null)
                return 0;

            // Player singles released in a month are part of that month's chart data.
            DateTime releaseMonth = single.ReleaseData.ReleaseDate;
            foreach (Rivals._date_to_month_id month in Rivals.Date_To_Month)
            {
                if (month == null || month.Date.Year != releaseMonth.Year || month.Date.Month != releaseMonth.Month)
                    continue;

                List<Rivals._group._single> chartSingles = Rivals.GetSingles(month.ID);
                if (chartSingles == null)
                    return 0;
                for (int i = 0; i < chartSingles.Count; i++)
                {
                    Rivals._group._single chartSingle = chartSingles[i];
                    if (chartSingle != null && chartSingle.Player && chartSingle.SingleID == single.id)
                        return i + 1;
                }
                return 0;
            }
            return 0;
        }

        private static bool IsEventUpcoming()
        {
            if (SEvent_Tour.Tours != null)
            {
                foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
                    if (tour != null && tour.Status != SEvent_Tour.tour._status.finished) return true;
            }
            if (SEvent_SSK.Elections != null)
            {
                foreach (SEvent_SSK._SSK election in SEvent_SSK.Elections)
                    if (election != null && election.Status != SEvent_Tour.tour._status.finished) return true;
            }
            if (SEvent_Concerts.Concerts != null)
            {
                foreach (SEvent_Concerts._concert concert in SEvent_Concerts.Concerts)
                    if (concert != null && concert.Status != SEvent_Tour.tour._status.finished) return true;
            }
            return false;
        }

        private static singles._single GetRecentSingle(singles._single groupSingle, singles._single mainSingle)
        {
            if (groupSingle?.ReleaseData == null) return mainSingle?.ReleaseData != null ? mainSingle : null;
            if (mainSingle?.ReleaseData == null) return groupSingle;
            if (groupSingle.ReleaseData.ReleaseDate > mainSingle.ReleaseData.ReleaseDate) return groupSingle;
            if (groupSingle.ReleaseData.ReleaseDate < mainSingle.ReleaseData.ReleaseDate) return mainSingle;
            return groupSingle.ReleaseData.Sales > mainSingle.ReleaseData.Sales ? groupSingle : mainSingle;
        }
    }
}
