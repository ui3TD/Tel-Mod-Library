using System.Collections.Generic;
using UnityEngine;

namespace TraitFix
{
    public partial class TraitsFix
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
        // Weekly chance that each Indiscreet idol lets a secret relationship slip: about once a year each
        public const int INDISCREET_CHANCE = 2;
        public const float INDISCREET_MENTAL = -30f;
        public const int MISANDRY_CHANCE = 20;
        public const float TRENDY_ADULT_MODIFIER = 0.5f;
        public const float TRENDY_YA_MODIFIER = 1.5f;
        public const float TRENDY_TEEN_MODIFIER = 1.5f;
        public const float LIVEFAST_MODIFIER = 2f;
        public const int LIVEFAST_DETERIORATION = 2;

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
    }
}
