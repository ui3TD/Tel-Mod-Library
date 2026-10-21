using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.AgeDeterioration))]
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

    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.UpdateDatingStatus))]
    public class Data_girls_girls_UpdateDatingStatus
    {
        // Outside relationships get one reveal roll; idol-idol dating is handled by CheckDating below.
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance?.DatingData == null
                || !__instance.DatingData.Is_Taken_Outside()
                || __instance.DatingData.Is_Partner_Status_Known
                || IsInWaitingLeak(__instance)
                || !mainScript.chance(INDISCREET_CHANCE))
            {
                return;
            }
            data_girls.girls culprit = FindIndiscreetLeaker(__instance);
            if (culprit == null)
                return;

            // The event is the leak: its effects apply when it opens, and with no event there's no leak
            string kind = __instance.DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.taken_outside_gf
                ? INDISCREET_KIND_GIRLFRIEND
                : INDISCREET_KIND_BOYFRIEND;
            StartLeakEvent(kind, culprit, new[] { __instance });
        }
    }

    // Last, so other mods' patches see the leak event as the active one, as they would see any event whose
    // reply shows no results of the game's own.
    [HarmonyPatch(typeof(Event_Manager), nameof(Event_Manager.ConcludeEvent))]
    [HarmonyPriority(Priority.Last)]
    public class Event_Manager_ConcludeEvent_Leak
    {
        public static void Prefix(Event_Manager __instance, out Event_Manager._activeEvent __state)
        {
            __state = __instance?.GetActiveEvent();
        }

        // The leak happens as its reply is chosen, after the game has closed the event popup, as the dating
        // scandal template's replies do. Not if another mod skipped the game's handling of the reply.
        public static void Postfix(Event_Manager._randomEvent._reply reply, Event_Manager._activeEvent __state, bool __runOriginal)
        {
            if (__runOriginal)
                ResolveLeak(__state, reply);
        }
    }

    // The game's weekly dating check stops at the first couple that starts or ends, so a patch on each
    // couple's own check would miss the couples after it. Every couple gets its leak roll here instead.
    [HarmonyPatch(typeof(Relationships), nameof(Relationships.CheckDating))]
    public class Relationships_CheckDating
    {
        public static void Postfix()
        {
            if (Relationships.RelationshipsData == null)
                return;

            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
                TryLeakCouple(relationship);
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.Initialize))]
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

    [HarmonyPatch(typeof(Relationships), nameof(Relationships.Do_Dynamic))]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            if (Relationships.RelationshipsData == null)
                return;

            // Every pair asks whether its idols are center; the singles are looked up once for all of them
            TraitLookups lookups = new TraitLookups();
            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if (!TryGetRelationshipGirls(relationship, out data_girls.girls girl0, out data_girls.girls girl1))
                    continue;

                if (relationship.Dynamic == Relationships._relationship._dynamic.positive)
                    AdjustForAgeTraits(relationship, girl0, girl1);

                if (girl0.trait == traits._trait._type.Arrogant && IsCenter(girl0, lookups))
                    relationship.Add(ARROGANT_WEEKLY_PENALTY);
                else if (girl1.trait == traits._trait._type.Arrogant && IsCenter(girl1, lookups))
                    relationship.Add(ARROGANT_WEEKLY_PENALTY);
            }
        }

        private static void AdjustForAgeTraits(Relationships._relationship relationship, data_girls.girls girl0, data_girls.girls girl1)
        {
            if (girl0.trait == traits._trait._type.Maternal && girl0.GetAge() > girl1.GetAge())
                relationship.Add(MATERNAL_WEEKLY_BONUS);
            else if (girl1.trait == traits._trait._type.Maternal && girl1.GetAge() > girl0.GetAge())
                relationship.Add(MATERNAL_WEEKLY_BONUS);

            if (girl0.trait == traits._trait._type.Precocious && girl0.GetAge() < girl1.GetAge())
                relationship.Add(PRECOCIOUS_WEEKLY_BONUS);
            else if (girl1.trait == traits._trait._type.Precocious && girl1.GetAge() < girl0.GetAge())
                relationship.Add(PRECOCIOUS_WEEKLY_BONUS);
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.Recalc))]
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

    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.AddCastParam))]
    public class Shows__show_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, Shows._show __instance)
        {
            if (type == data_girls._paramType.teamChemistry
                || __instance?.medium == null
                || __instance.medium.media_type != Shows._param._media_type.internet
                || !HasMemeQueen(girlList)
                || __instance.girlParams == null
                || __instance.girlParams.Count == 0)
                return;

            data_girls.girls.param param = __instance.girlParams[__instance.girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    [HarmonyPatch(typeof(Show_Popup), nameof(Show_Popup.AddCastParam))]
    public class Show_Popup_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, List<data_girls.girls.param> ___girlParams, Shows._param ___medium)
        {
            if (type == data_girls._paramType.teamChemistry
                || ___medium == null
                || ___medium.media_type != Shows._param._media_type.internet
                || !HasMemeQueen(girlList)
                || ___girlParams == null
                || ___girlParams.Count == 0)
                return;

            data_girls.girls.param param = ___girlParams[___girlParams.Count - 1];
            if (param != null && param.type == type)
                param.val += MEME_INT_SHOW;
        }
    }

    // Recalculate only when the medium changes, avoiding a recalculation on every SetParam.
    [HarmonyPatch(typeof(Show_Popup), nameof(Show_Popup.SetParam))]
    public class Show_Popup_SetParam
    {
        public static void Postfix(Show_Popup __instance, Show_Popup_Param_Button._type type, Shows._show._castType? ___castType)
        {
            if (__instance == null || type != Show_Popup_Param_Button._type.medium || ___castType == null)
                return;
            __instance.SetCastType(___castType.Value);
        }
    }

    [HarmonyPatch(typeof(singles._param), nameof(singles._param.GetSuccessChance), new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
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

    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SetStamina))]
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

    [HarmonyPatch(typeof(singles), nameof(singles.ReleaseSingle))]
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
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Misandry && mainScript.chance(MISANDRY_CHANCE))
                    girl.AddAppeal(resources.fanType.male, MISANDRY_MODIFIER);
            }
        }
    }

    // FinishTour clears Tour before returning, so capture the attendance result first.
    [HarmonyPatch(typeof(SEvent_Tour), nameof(SEvent_Tour.FinishTour))]
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

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
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
    [HarmonyPatch(typeof(business._proposal), nameof(business._proposal.GetGirlCoeff))]
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

    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GetAverageParam))]
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

    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SenbatsuCalcParam))]
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

    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
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

    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetSkillValue))]
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

    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetSkillValue))]
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

    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetVal))]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!IsTraitCalculationActive || __instance == null)
                return;
            __result += GetCurrentTraitModifier(__instance.Parent, __instance.type);
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
        // What Do_Dynamic adds to a relationship each week
        public const float MATERNAL_WEEKLY_BONUS = MATERNAL_BONUS / 2;
        public const float PRECOCIOUS_WEEKLY_BONUS = PRECOCIOUS_BONUS / 2;
        public const float ARROGANT_WEEKLY_PENALTY = ARROGANT_PENALTY / 2;
        public const int INDISCREET_CHANCE = 2;
        public const float INDISCREET_MENTAL = -30f;
        public const int MISANDRY_CHANCE = 20;
        public const float TRENDY_ADULT_MODIFIER = 0.5f;
        public const float TRENDY_YA_MODIFIER = 1.5f;
        public const float TRENDY_TEEN_MODIFIER = 1.5f;
        public const float LIVEFAST_MODIFIER = 2f;
        public const int LIVEFAST_DETERIORATION = 2;

        // Leak events are <prefix><kind>_1 to _<count> in the mod's random_events.json. The culprit is named
        // by the variable <event id><culprit suffix>.
        public const string INDISCREET_EVENT_PREFIX = "traitsfix_indiscreet_";
        public const string INDISCREET_CULPRIT_SUFFIX = "_culprit";
        // Each reply's result is the text <event id><result suffix><reply number from 1>
        public const string INDISCREET_RESULT_SUFFIX = "_result_";
        public const string INDISCREET_LABEL_REACTION_LOW = "IDOL__LEAK_REACTION_LOW";
        public const string INDISCREET_LABEL_REACTION_HIGH = "IDOL__LEAK_REACTION_HIGH";

        // A leak's consequences are the game's dating scandal template (Event_Templates) answered with its
        // "Do nothing" reply: the template's starting damage, its image and dating policy adjustments, half
        // damage for a female partner, then the reply's own buzz and scandal points scaled by damage.
        public const int SCANDAL_TEMPLATE_DAMAGE = 50;
        public const int SCANDAL_TEMPLATE_HIGH_DAMAGE = 50;
        public const int SCANDAL_TEMPLATE_BUZZ = 20;
        public const int SCANDAL_TEMPLATE_SCANDAL_POINTS = 1;
        public const int SCANDAL_TEMPLATE_BLOCK_DAYS = 180;
        public const string INDISCREET_KIND_BOYFRIEND = "bf";
        public const string INDISCREET_KIND_GIRLFRIEND = "gf";
        public const string INDISCREET_KIND_IDOLS = "idols";
        public const int INDISCREET_EVENTS_PER_KIND = 5;
        // Which stories of a kind were shown is the variable <prefix><kind><order suffix>. Once all are
        // shown, the most recent few are kept back.
        public const string INDISCREET_ORDER_SUFFIX = "_order";
        public const int INDISCREET_RECENT_STORIES = 2;

        private sealed class TraitCalculationContext
        {
            public List<data_girls.girls> Cast;
            public readonly TraitLookups Lookups = new TraitLookups();
        }

        /// <summary>
        /// The game state trait modifiers depend on: upcoming events, the latest singles and their chart
        /// results. A calculation reads stats many times, and each read would otherwise search the
        /// events, singles and charts again. Each value is found the first time it's needed and reused
        /// for the rest of the calculation.
        /// </summary>
        public sealed class TraitLookups
        {
            private bool? eventUpcoming;
            private bool mainSingleFound;
            private singles._single mainSingle;
            private Dictionary<Groups._group, singles._single> groupSingles;
            private Dictionary<singles._single, bool> missedNumberOne;

            public bool EventUpcoming => eventUpcoming ??= IsEventUpcoming();

            public singles._single MainSingle
            {
                get
                {
                    if (!mainSingleFound)
                    {
                        mainSingle = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
                        mainSingleFound = true;
                    }
                    return mainSingle;
                }
            }

            public singles._single LatestSingle(Groups._group group)
            {
                if (group == null)
                    return null;
                groupSingles ??= new Dictionary<Groups._group, singles._single>();
                if (!groupSingles.TryGetValue(group, out singles._single single))
                {
                    single = singles.GetLatestReleasedSingle(false, group);
                    groupSingles[group] = single;
                }
                return single;
            }

            public bool MissedNumberOne(singles._single single)
            {
                if (single == null)
                    return false;
                missedNumberOne ??= new Dictionary<singles._single, bool>();
                if (!missedNumberOne.TryGetValue(single, out bool missed))
                {
                    missed = DidSingleMissNumberOne(single);
                    missedNumberOne[single] = missed;
                }
                return missed;
            }
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

        /// <summary>
        /// The trait modifier for a stat read inside the current trait calculation.
        /// </summary>
        public static int GetCurrentTraitModifier(data_girls.girls girl, data_girls._paramType type)
        {
            if (!IsTraitCalculationActive)
                return 0;
            TraitCalculationContext context = traitCalculationContexts.Peek();
            return GetTraitModifier(girl, type, context.Cast, context.Lookups);
        }

        public static int GetTraitModifier(data_girls.girls girl, data_girls._paramType type, List<data_girls.girls> cast = null, TraitLookups lookups = null)
        {
            if (girl == null || !data_girls.IsStatParam(type))
                return 0;

            lookups ??= new TraitLookups();
            switch (girl.trait)
            {
                case traits._trait._type.Anxiety:
                    if (lookups.EventUpcoming) return ANXIETY_MODIFIER;
                    break;
                case traits._trait._type.Clumsy:
                    if (type == data_girls._paramType.dance) return CLUMSY_DANCE_MODIFIER;
                    if (type == data_girls._paramType.funny) return CLUMSY_FUNNY_MODIFIER;
                    break;
                case traits._trait._type.Worrier:
                    if (resources.GetScandalPointsTotal() > 0L) return WORRIER_MODIFIER;
                    break;
                case traits._trait._type.Complacent:
                    if ((type == data_girls._paramType.vocal || type == data_girls._paramType.dance) && IsCenter(girl, lookups))
                        return COMPLACENT_MODIFIER;
                    break;
                case traits._trait._type.Lone_Wolf:
                    if (cast != null)
                    {
                        int count = 0;
                        foreach (data_girls.girls castGirl in cast)
                        {
                            if (castGirl != null && !castGirl.IsSick())
                                count++;
                            if (count > 1) break;
                        }
                        if (count == 1) return LONEWOLF_MODIFIER;
                    }
                    break;
            }

            if (girl.trait == traits._trait._type.Defeatist || girl.trait == traits._trait._type.Underdog)
            {
                singles._single recentSingle = GetRecentSingle(lookups.LatestSingle(girl.GetGroup()), lookups.MainSingle);
                if (lookups.MissedNumberOne(recentSingle))
                    return girl.trait == traits._trait._type.Defeatist ? DEFEATIST_MODIFIER : UNDERDOG_MODIFIER;
            }

            return 0;
        }

        public static bool IsCenter(data_girls.girls girl, TraitLookups lookups = null)
        {
            if (girl == null)
                return false;
            lookups ??= new TraitLookups();
            return lookups.LatestSingle(girl.GetGroup())?.GetCenter() == girl || lookups.MainSingle?.GetCenter() == girl;
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

        public static bool HasMemeQueen(List<data_girls.girls> girls)
        {
            if (girls == null)
                return false;
            foreach (data_girls.girls girl in girls)
            {
                if (girl != null && !girl.IsSick() && girl.trait == traits._trait._type.Meme_queen)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A random active Indiscreet idol other than the excluded ones, or null if there is none.
        /// </summary>
        public static data_girls.girls FindIndiscreetLeaker(params data_girls.girls[] excludedGirls)
        {
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls(null);
            if (activeGirls == null)
                return null;

            List<data_girls.girls> leakers = new List<data_girls.girls>();
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
                    leakers.Add(girl);
            }
            if (leakers.Count == 0)
                return null;
            return leakers.Count == 1 ? leakers[0] : leakers[RandomIndex(leakers.Count)];
        }

        public static int RandomIndex(int count) => UnityEngine.Random.Range(0, count);

        /// <summary>
        /// Queues one of the kind's leak events to open now, with the couple as its actors and the culprit
        /// named in its text. Returns false, queuing nothing, when no event of the kind can start (all
        /// already waiting, or not loaded). Built here rather than through Event_Manager.StartEvent, which
        /// can only name one actor and doesn't start events while the player has random events turned off.
        /// </summary>
        public static bool StartLeakEvent(string kind, data_girls.girls culprit, data_girls.girls[] couple)
        {
            Event_Manager manager = GetEventManager();
            if (manager == null || manager.activeEvents == null)
                return false;

            List<int> available = new List<int>();
            for (int i = 1; i <= INDISCREET_EVENTS_PER_KIND; i++)
            {
                Event_Manager._randomEvent randomEvent = Event_Manager.GetRandomEvent(INDISCREET_EVENT_PREFIX + kind + "_" + i);
                if (randomEvent?.actor != null && randomEvent.actor.Count == couple.Length && !IsWaiting(manager, randomEvent))
                    available.Add(i);
            }
            if (available.Count == 0)
                return false;

            List<int> shown = GetShownStories(kind);
            int story = PickStory(available, shown);
            Event_Manager._randomEvent chosen = Event_Manager.GetRandomEvent(INDISCREET_EVENT_PREFIX + kind + "_" + story);
            Event_Manager._activeEvent activeEvent = new Event_Manager._activeEvent
            {
                data = chosen,
                state = Event_Manager._activeEvent._state.waiting,
                date = staticVars.dateTime,
            };
            for (int i = 0; i < chosen.actor.Count; i++)
                activeEvent.actors.Add(new Event_Manager._activeEvent._actor { data = chosen.actor[i], girl = couple[i] });
            // Coloured like the actors' names, which the game colours as it fills them in
            SetVariable(chosen.id + INDISCREET_CULPRIT_SUFFIX, ExtensionMethods.color(culprit.girlName(false), mainScript.blue));
            manager.activeEvents.Add(activeEvent);
            shown.Remove(story);
            shown.Insert(0, story);
            SetVariable(INDISCREET_EVENT_PREFIX + kind + INDISCREET_ORDER_SUFFIX, string.Join(",", shown));
            return true;
        }

        /// <summary>
        /// A story not shown yet if there is one; otherwise any but the most recently shown few, so the
        /// stories don't settle into a fixed cycle. Falls back to any available story when only recent
        /// ones are left (the others already waiting).
        /// </summary>
        private static int PickStory(List<int> available, List<int> shown)
        {
            List<int> candidates = available.FindAll(story => !shown.Contains(story));
            if (candidates.Count == 0)
            {
                List<int> recent = shown.GetRange(0, Math.Min(INDISCREET_RECENT_STORIES, shown.Count));
                candidates = available.FindAll(story => !recent.Contains(story));
            }
            if (candidates.Count == 0)
                candidates = available;
            return candidates[RandomIndex(candidates.Count)];
        }

        /// <summary>
        /// The kind's story numbers, most recently shown first, saved with the game. Entries that aren't
        /// one of the kind's stories are dropped, so a damaged record only makes stories count as unseen.
        /// </summary>
        private static List<int> GetShownStories(string kind)
        {
            List<int> shown = new List<int>();
            string record = GetVariable(INDISCREET_EVENT_PREFIX + kind + INDISCREET_ORDER_SUFFIX);
            if (string.IsNullOrEmpty(record))
                return shown;
            foreach (string entry in record.Split(','))
            {
                if (int.TryParse(entry.Trim(), out int story) && story >= 1 && story <= INDISCREET_EVENTS_PER_KIND && !shown.Contains(story))
                    shown.Add(story);
            }
            return shown;
        }

        public static bool IsLeakEvent(Event_Manager._activeEvent activeEvent) =>
            activeEvent?.data?.id != null && activeEvent.data.id.StartsWith(INDISCREET_EVENT_PREFIX);

        /// <summary>
        /// Whether the idol is in a leak event that hasn't been answered yet, so she isn't rolled for again.
        /// </summary>
        public static bool IsInWaitingLeak(data_girls.girls girl)
        {
            Event_Manager manager = GetEventManager();
            if (manager == null || manager.activeEvents == null)
                return false;
            foreach (Event_Manager._activeEvent activeEvent in manager.activeEvents)
            {
                if (activeEvent.state == Event_Manager._activeEvent._state.complete || !IsLeakEvent(activeEvent))
                    continue;
                foreach (Event_Manager._activeEvent._actor actor in activeEvent.actors)
                {
                    if (actor.girl == girl)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the dating scandal template's "Do nothing" reply does for this partner and these policies:
        /// the scandal's damage, the buzz and scandal points it scales, and the fans' opinion changes.
        /// </summary>
        public struct LeakOutcome
        {
            public int Damage;
            public int Buzz;
            public int ScandalPoints;
            public float HardcoreOpinion;
            public float CasualOpinion;

            public bool IsHighDamage => Damage >= SCANDAL_TEMPLATE_HIGH_DAMAGE;
        }

        // Event_Templates._active_template.DoCustom, in the same order
        public static LeakOutcome GetLeakOutcome(bool femalePartner, policies._value image, policies._value dating)
        {
            LeakOutcome outcome = new LeakOutcome { Damage = SCANDAL_TEMPLATE_DAMAGE };
            switch (image)
            {
                case policies._value.image_orthodox:
                    outcome.Damage += 50;
                    outcome.HardcoreOpinion -= 2f;
                    break;
                case policies._value.image_rebellious:
                    outcome.Damage -= 15;
                    outcome.CasualOpinion++;
                    outcome.HardcoreOpinion++;
                    break;
            }
            switch (dating)
            {
                case policies._value.dating_forbidden:
                    outcome.HardcoreOpinion--;
                    outcome.Damage += 50;
                    break;
                case policies._value.dating_allowed:
                    outcome.Damage -= 25;
                    outcome.HardcoreOpinion++;
                    outcome.CasualOpinion++;
                    break;
            }
            if (femalePartner)
            {
                outcome.Damage /= 2;
                outcome.HardcoreOpinion++;
                outcome.CasualOpinion++;
            }
            // The "Do nothing" reply
            if (outcome.Damage > 100)
                outcome.Damage = Mathf.RoundToInt(outcome.Damage * 1.5f);
            outcome.Buzz = Mathf.RoundToInt(SCANDAL_TEMPLATE_BUZZ * (outcome.Damage / 100f));
            outcome.ScandalPoints = Mathf.RoundToInt(SCANDAL_TEMPLATE_SCANDAL_POINTS * (outcome.Damage / 100f));
            return outcome;
        }

        /// <summary>
        /// The leak, as its reply is chosen. Each idol in the couple (the event's actors) loses mental
        /// stamina, and gets the dating scandal template's consequences and scandal flags; the player learns
        /// who they're dating; and the results show as the template shows them, after the chosen reply's own
        /// result. Both replies have the same consequences.
        /// </summary>
        public static void ResolveLeak(Event_Manager._activeEvent leak, Event_Manager._randomEvent._reply reply)
        {
            if (!IsLeakEvent(leak))
                return;
            // Answered either way: an event left active would keep its idols from leaking again
            leak.state = Event_Manager._activeEvent._state.complete;
            if (leak.actors == null || leak.actors.Count == 0)
                return;
            List<data_girls.girls> couple = new List<data_girls.girls>();
            foreach (Event_Manager._activeEvent._actor actor in leak.actors)
            {
                if (actor.girl?.DatingData == null)
                    return;
                couple.Add(actor.girl);
            }

            // Two idols dating each other are both women, like an outside girlfriend
            bool femalePartner = couple.Count == 2
                || couple[0].DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.taken_outside_gf;
            policies._value image = policies.GetSelectedPolicyValue(policies._type.image)?.Value ?? policies._value.image_neutral;
            policies._value dating = policies.GetSelectedPolicyValue(policies._type.dating)?.Value ?? policies._value.dating_ambiguous;
            LeakOutcome outcome = GetLeakOutcome(femalePartner, image, dating);
            if (dating == policies._value.dating_forbidden && image == policies._value.image_orthodox)
                Achievements.Unlock(Achievements.ID.ACH_SEISO);

            string text = ExtensionMethods.color(Language.Data[outcome.IsHighDamage ? INDISCREET_LABEL_REACTION_HIGH : INDISCREET_LABEL_REACTION_LOW], mainScript.blue);
            // The reply's result is in the mod's text, since the game would show its own results for it
            int replyNumber = leak.data.reply == null ? 0 : leak.data.reply.IndexOf(reply) + 1;
            if (replyNumber > 0 && Language.Data.TryGetValue(leak.data.id + INDISCREET_RESULT_SUFFIX + replyNumber, out string result))
                text = ExtensionMethods.color(leak.SwitchVariables(result), mainScript.blue) + mainScript.separator + text;
            string staminaLines = "";
            foreach (data_girls.girls girl in couple)
            {
                // The template spares these two (Event_Templates._active_template.MentalDamageDating)
                if (girl.trait == traits._trait._type.Thick_Skin || girl.trait == traits._trait._type.Shameless)
                    continue;
                girl.addParam(data_girls._paramType.mentalStamina, INDISCREET_MENTAL);
                staminaLines += (staminaLines == "" ? "" : "\n") + girl.GetName() + ": "
                    + ExtensionMethods.color(Language.Data["MENTAL_STAMINA"].ToLower() + " " + INDISCREET_MENTAL + Language.Data["PT"], mainScript.red);
            }
            if (staminaLines != "")
                text += mainScript.separator + staminaLines;

            if (outcome.Buzz != 0)
                resources.Add(resources.type.buzz, outcome.Buzz);
            if (outcome.ScandalPoints != 0)
            {
                foreach (data_girls.girls girl in couple)
                    girl.addParam(data_girls._paramType.scandalPoints, outcome.ScandalPoints);
                text += mainScript.separator + Language.Data["SCANDAL_POINTS"] + ExtensionMethods.color(": ", mainScript.blue)
                    + ExtensionMethods.color("+" + outcome.ScandalPoints, mainScript.red);
            }

            if (outcome.IsHighDamage)
            {
                business business = GetBusiness();
                long liability = business == null ? 0L : business.GetLiability(leak.actors);
                if (liability != 0L)
                {
                    text += mainScript.separator + Language.Data["BIZ__LIABILITY"] + ExtensionMethods.color(": ", mainScript.blue)
                        + ExtensionMethods.color(ExtensionMethods.formatMoney(-liability), mainScript.red);
                    business.BreakContracts(leak.actors);
                }
            }

            if (outcome.HardcoreOpinion < 0f || outcome.CasualOpinion < 0f)
            {
                foreach (data_girls.girls girl in couple)
                    text += mainScript.separator + LoseFans(girl, outcome);
            }

            foreach (data_girls.girls girl in couple)
            {
                girl.DatingData.Had_Scandal = true;
                girl.DatingData.Had_Scandal_Ever = true;
                girl.DatingData.Used_Goods = true;
                girl.LastDatingScandal = staticVars.dateTime;
            }
            if (couple.Count == 2)
            {
                MarkIdolRelationshipKnown(couple[0], couple[1]);
            }
            else
            {
                couple[0].DatingData.Is_Partner_Status_Known = true;
                couple[0].DatingData.Partner_Status_Known_To_Player = couple[0].DatingData.Partner_Status;
            }

            ShowLeakResults(text, couple[0], couple.Count > 1 ? couple[1] : null);
        }

        // As the template reports an idol's lost fans
        private static string LoseFans(data_girls.girls girl, LeakOutcome outcome)
        {
            string text = Language.Insert("ACTORS_FANS", girl.GetName()) + ":";
            if (outcome.HardcoreOpinion < 0f)
            {
                girl.AddFans(resources.fanType.hardcore, outcome.HardcoreOpinion, oshihen: true);
                girl.AddAppeal(resources.fanType.hardcore, outcome.HardcoreOpinion);
                text += FansLine(resources.fanType.hardcore, outcome.HardcoreOpinion, girl.Fans_Oshihened_Hardcore, girl.Fans_Quit_Hardcore);
            }
            if (outcome.CasualOpinion < 0f)
            {
                girl.AddFans(resources.fanType.casual, outcome.CasualOpinion, oshihen: true);
                girl.AddAppeal(resources.fanType.casual, outcome.CasualOpinion);
                text += FansLine(resources.fanType.casual, outcome.CasualOpinion, girl.Fans_Oshihened_Casual, girl.Fans_Quit_Casual);
            }
            return text;
        }

        private static string FansLine(resources.fanType type, float opinion, long switched, long quit)
        {
            return ExtensionMethods.color("\n" + resources.GetFanTitle(type) + " " + Language.Data["FANS_OPINION"] + ": " + opinion, mainScript.red)
                + "\n" + resources.GetFanTitle(type) + " " + Language.Data["FANS"] + ": "
                + ExtensionMethods.color(ExtensionMethods.formatNumber(switched) + " " + Language.Data["FANS__SWITCHED"], mainScript.blue) + " | "
                + ExtensionMethods.color(ExtensionMethods.formatNumber(quit) + " " + Language.Data["FANS__ABANDONED"], mainScript.red);
        }

        public static void ShowLeakResults(string text, data_girls.girls girl0, data_girls.girls girl1)
        {
            PopupManager popups = GetGameComponent<PopupManager>();
            if (popups == null)
                return;
            popups.Open(PopupManager._type.static_event);
            popups.GetByType(PopupManager._type.static_event).obj.GetComponent<Event_Static_Popup>().Set(text, girl0, girl1);
        }

        private static bool IsWaiting(Event_Manager manager, Event_Manager._randomEvent randomEvent)
        {
            foreach (Event_Manager._activeEvent activeEvent in manager.activeEvents)
            {
                if (activeEvent?.data == randomEvent && activeEvent.state != Event_Manager._activeEvent._state.complete)
                    return true;
            }
            return false;
        }

        // variables.Set also logs and checks story tasks, neither of which a leak needs
        private static void SetVariable(string name, string value)
        {
            foreach (variables._variable variable in variables.variable)
            {
                if (variable.name == name)
                {
                    variable.value = value;
                    return;
                }
            }
            variables.variable.Add(new variables._variable { name = name, value = value });
        }

        private static string GetVariable(string name)
        {
            if (variables.variable == null)
                return null;
            foreach (variables._variable variable in variables.variable)
            {
                if (variable?.name == name)
                    return variable.value;
            }
            return null;
        }

        public static Event_Manager GetEventManager() => GetGameComponent<Event_Manager>();

        public static business GetBusiness() => GetGameComponent<business>();

        // The game's managers are components of mainScript.Data
        private static T GetGameComponent<T>() where T : Component
        {
            Camera camera = Camera.main;
            if (camera == null)
                return null;
            mainScript main = camera.GetComponent<mainScript>();
            if (main == null || main.Data == null)
                return null;
            return main.Data.GetComponent<T>();
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

        public static bool IsKnownToDateIdol(data_girls.girls girl)
        {
            return girl?.DatingData != null
                && girl.DatingData.Is_Partner_Status_Known
                && girl.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_idol;
        }

        /// <summary>
        /// Rolls for an Indiscreet idol leaking that two idols are secretly dating each other.
        /// </summary>
        public static void TryLeakCouple(Relationships._relationship relationship)
        {
            if (!TryGetRelationshipGirls(relationship, out data_girls.girls girl0, out data_girls.girls girl1) || !relationship.Dating)
                return;

            if (relationship.IsRelationshipKnown())
                return;

            // A leak marks both idols as dating an idol, so this stops the same couple leaking every week.
            // Knowing only one of them is dating (but not who) leaves the couple secret.
            if (IsKnownToDateIdol(girl0) && IsKnownToDateIdol(girl1))
                return;
            if (IsInWaitingLeak(girl0) || IsInWaitingLeak(girl1))
                return;

            if (!mainScript.chance(INDISCREET_CHANCE))
                return;
            data_girls.girls culprit = FindIndiscreetLeaker(girl0, girl1);
            if (culprit == null)
                return;

            // The event is the leak: its effects apply when it opens, and with no event there's no leak
            StartLeakEvent(INDISCREET_KIND_IDOLS, culprit, new[] { girl0, girl1 });
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

            // The single charts the month after its release.
            DateTime releaseDate = single.ReleaseData.ReleaseDate;
            if (releaseDate.Year == staticVars.dateTime.Year && releaseDate.Month == staticVars.dateTime.Month)
                return false;

            // The chart popup records the position, except for the last-ranked single or when the popup is skipped.
            // A single that can't be found on any chart counts as having missed #1.
            int chartPosition = single.ReleaseData.Chart_Position;
            if (chartPosition <= 0)
                chartPosition = ResolveChartPosition(single);
            return chartPosition != 1;
        }

        private static int ResolveChartPosition(singles._single single)
        {
            if (Rivals.Date_To_Month == null)
                return 0;

            // A month's chart lists the singles released the month before.
            DateTime chartMonth = single.ReleaseData.ReleaseDate.AddMonths(1);
            foreach (Rivals._date_to_month_id month in Rivals.Date_To_Month)
            {
                if (month == null || month.Date.Year != chartMonth.Year || month.Date.Month != chartMonth.Month)
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
