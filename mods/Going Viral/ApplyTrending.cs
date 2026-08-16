using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static GoingViral.TrendingManager;

namespace GoingViral
{
    [HarmonyPatch(typeof(singles), "GenerateSales")]
    public class singles_GenerateSales
    {
        public static void Postfix(singles._single single)
        {
            if (IsTrending() != TrendingStatus.trending || single == null || single.sales == null)
                return;

            long totalNewFans = 0;
            long casualNewFans = 0;
            List<singles._single._sales> casualSales = new List<singles._single._sales>();
            foreach (singles._single._sales sale in single.sales)
            {
                if (sale == null) continue;
                totalNewFans = SafeAdd(totalNewFans, sale.new_fans);
                if (sale.fan != null && sale.fan.IsType(resources.fanType.casual))
                {
                    casualSales.Add(sale);
                    casualNewFans = SafeAdd(casualNewFans, sale.new_fans);
                }
            }

            if (totalNewFans <= 0 || casualSales.Count == 0)
                return;

            long targetTotal = ScaleLong(totalNewFans, GetTrendingCoeff());
            long bonus = Math.Max(0, targetTotal - totalNewFans);
            if (bonus == 0)
                return;

            long assigned = 0;
            for (int i = 0; i < casualSales.Count; i++)
            {
                long share;
                if (i == casualSales.Count - 1)
                {
                    share = bonus - assigned;
                }
                else if (casualNewFans > 0)
                {
                    share = (long)Math.Round(bonus * Math.Max(0d, (double)casualSales[i].new_fans) / casualNewFans, MidpointRounding.AwayFromZero);
                    share = Math.Min(share, bonus - assigned);
                }
                else
                {
                    share = (bonus - assigned) / (casualSales.Count - i);
                }

                casualSales[i].new_fans = SafeAdd(casualSales[i].new_fans, share);
                assigned = SafeAdd(assigned, share);
            }
        }
    }

    // Scope show fan additions so the viral bonus can be added without relying on compiler local numbers.
    [HarmonyPatch(typeof(Shows._show), "SetSales")]
    public class Shows__show_SetSales
    {
        internal sealed class FanBucket
        {
            public resources._fan Fan;
            public long BaseFans;
        }

        internal sealed class Context
        {
            public Shows._show Show;
            public long BaseNewFans;
            public bool AddingTrendingBonus;
            public readonly List<FanBucket> CasualBuckets = new List<FanBucket>();
        }

        private static readonly Stack<Context> contexts = new Stack<Context>();
        internal static Context Current { get { return contexts.Count > 0 ? contexts.Peek() : null; } }

        [HarmonyPriority(Priority.First)]
        public static void Prefix(Shows._show __instance)
        {
            contexts.Push(new Context { Show = __instance });
        }

        public static Exception Finalizer(Exception __exception)
        {
            if (contexts.Count > 0) contexts.Pop();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls), "AddFans_Equally", new Type[] { typeof(long), typeof(resources._fan), typeof(List<data_girls.girls>) })]
    public class data_girls_AddFans_Equally_TrendingRecorder
    {
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(long total_fans, resources._fan _Fan)
        {
            Shows__show_SetSales.Context context = Shows__show_SetSales.Current;
            if (context == null || context.AddingTrendingBonus || total_fans <= 0)
                return;

            context.BaseNewFans = SafeAdd(context.BaseNewFans, total_fans);
            if (_Fan != null && _Fan.IsType(resources.fanType.casual))
                context.CasualBuckets.Add(new Shows__show_SetSales.FanBucket { Fan = _Fan, BaseFans = total_fans });
        }
    }

    [HarmonyPatch(typeof(Shows._show), "SetNewFans")]
    public class Shows__show_SetNewFans_Trending
    {
        [HarmonyPriority(Priority.Last)]
        public static void Prefix(ref int val)
        {
            Shows__show_SetSales.Context context = Shows__show_SetSales.Current;
            if (context == null || IsTrending() != TrendingStatus.trending || context.BaseNewFans <= 0)
                return;

            long target = ScaleLong(context.BaseNewFans, GetTrendingCoeff());
            long bonus = Math.Max(0, target - context.BaseNewFans);
            if (bonus == 0 || context.CasualBuckets.Count == 0)
            {
                val = context.BaseNewFans > int.MaxValue ? int.MaxValue : (int)context.BaseNewFans;
                return;
            }

            List<data_girls.girls> cast = context.Show != null ? context.Show.GetCast() : null;
            if (cast == null || cast.Count == 0)
            {
                val = context.BaseNewFans > int.MaxValue ? int.MaxValue : (int)context.BaseNewFans;
                return;
            }

            long casualBase = 0;
            foreach (Shows__show_SetSales.FanBucket bucket in context.CasualBuckets)
                casualBase = SafeAdd(casualBase, Math.Max(0, bucket.BaseFans));

            long assigned = 0;
            context.AddingTrendingBonus = true;
            try
            {
                for (int i = 0; i < context.CasualBuckets.Count; i++)
                {
                    Shows__show_SetSales.FanBucket bucket = context.CasualBuckets[i];
                    long share;
                    if (i == context.CasualBuckets.Count - 1)
                        share = bonus - assigned;
                    else if (casualBase > 0)
                    {
                        share = (long)Math.Round(bonus * (double)Math.Max(0, bucket.BaseFans) / casualBase, MidpointRounding.AwayFromZero);
                        share = Math.Min(share, bonus - assigned);
                    }
                    else
                        share = (bonus - assigned) / (context.CasualBuckets.Count - i);

                    if (share > 0 && bucket.Fan != null)
                    {
                        data_girls.AddFans_Equally(share, bucket.Fan, cast);
                        assigned = SafeAdd(assigned, share);
                    }
                }
            }
            finally
            {
                context.AddingTrendingBonus = false;
            }

            long displayed = SafeAdd(context.BaseNewFans, assigned);
            if (displayed > int.MaxValue) val = int.MaxValue;
            else if (displayed < int.MinValue) val = int.MinValue;
            else val = (int)displayed;
        }
    }

    [HarmonyPatch(typeof(business._proposal), "set_newFans")]
    public class business__proposal_set_newFans
    {
        public static void Postfix(business._proposal __instance)
        {
            if (IsTrending() == TrendingStatus.trending && __instance != null && __instance._newFans > 0)
                __instance._newFans = ScaleInt(__instance._newFans, GetTrendingCoeff());
        }
    }

    [HarmonyPatch(typeof(business), "AddActiveProposal")]
    public class business_AddActiveProposal
    {
        public static void Postfix(business __instance)
        {
            if (IsTrending() != TrendingStatus.trending || __instance == null || __instance.ActiveProposals == null || __instance.ActiveProposals.Count == 0)
                return;

            business.active_proposal proposal = __instance.ActiveProposals[__instance.ActiveProposals.Count - 1];
            float coeff = GetTrendingCoeff();
            if (proposal != null && proposal.Fans_per_week > 0 && coeff > 0f)
                proposal.Fans_per_week = Mathf.RoundToInt(proposal.Fans_per_week / coeff);
        }
    }

    [HarmonyPatch(typeof(business), "DoWeeklyFans")]
    public class business_DoWeeklyFans
    {
        public static void Postfix(business __instance)
        {
            if (IsTrending() != TrendingStatus.trending || __instance == null || __instance.ActiveProposals == null)
                return;

            float extraCoeff = GetTrendingCoeff() - 1f;
            foreach (business.active_proposal proposal in __instance.ActiveProposals)
            {
                if (proposal != null && proposal.Girl != null && proposal.Fans_per_week > 0)
                    proposal.Girl.AddFans(ScaleLong(proposal.Fans_per_week, extraCoeff), null);
            }
        }
    }

    [HarmonyPatch(typeof(Contracts_Line), "Set")]
    public class Contracts_Line_Set
    {
        public static void Postfix(Contracts_Line __instance, business.active_proposal ___ActiveProposal)
        {
            if (IsTrending() != TrendingStatus.trending || __instance == null || __instance.NewFans == null || ___ActiveProposal == null)
                return;

            ExtensionMethods.SetText(__instance.NewFans,
                ExtensionMethods.formatNumber(ScaleLong(___ActiveProposal.Fans_per_week, GetTrendingCoeff()), false, false));
        }
    }

    [HarmonyPatch(typeof(SEvent_Tour.tour), "GetNewFansByAttendance")]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            if (IsTrending() == TrendingStatus.trending && __result > 0)
                __result = ScaleInt(__result, GetTrendingCoeff());
        }
    }
}
