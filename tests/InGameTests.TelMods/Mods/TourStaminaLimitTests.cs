using HarmonyLib;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Tour Stamina Limit's fan multiplier with Traits Expansion's Polyglot bonus on the same
    /// method. No unit test loads the two together.
    /// </summary>
    internal static class TourStaminaLimitTests
    {
        private const string HarmonyId = "com.tel.tourstamina";
        private const string TraitsExpansion = "com.tel.traitsexpansion";
        private const int Polyglot = 2802;
        private const int PolyglotIdol = 1;
        private const int Attendance = 10000;

        private static int gameResult;

        /// <summary>
        /// A tour stop's new fans are the game's figure times 3.5, and times 1 + 0.2 per active
        /// Polyglot idol when Traits Expansion is on, in either order of rounding.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator NewFansAreThreeAndAHalfTimesTheGames(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            // The fixture has no Polyglot idol; make one so both bonuses apply.
            data_girls.girls idol = Game.Girl(PolyglotIdol);
            traits._trait._type trait = idol.trait;
            if (ModTest.IsActive(TraitsExpansion))
                idol.trait = (traits._trait._type)Polyglot;
            int polyglots = ModTest.IsActive(TraitsExpansion)
                ? data_girls.GetActiveGirls().Count(g => g != null && (int)g.trait == Polyglot)
                : 0;
            ctx.Record("polyglots", polyglots);
            float bonus = 1 + 0.2f * polyglots;

            MethodInfo newFans = AccessTools.Method(typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.GetNewFansByAttendance));
            var tour = new SEvent_Tour.tour();
            using (TestTools.Restore(() => idol.trait = trait))
            using (TestTools.Spy(newFans, postfix: AccessTools.Method(typeof(TourStaminaLimitTests), nameof(RecordGameResult))))
            {
                for (int i = 0; i < 20; i++)
                {
                    int result = tour.GetNewFansByAttendance(Attendance);
                    int tourFirst = polyglots > 0 ? Mathf.RoundToInt(Mathf.RoundToInt(gameResult * 3.5f) * bonus) : Mathf.RoundToInt(gameResult * 3.5f);
                    int traitsFirst = polyglots > 0 ? Mathf.RoundToInt(Mathf.RoundToInt(gameResult * bonus) * 3.5f) : tourFirst;
                    if (result != tourFirst && result != traitsFirst)
                    {
                        ctx.Fail("Game figure " + gameResult + " became " + result + "; expected " + tourFirst
                                 + (traitsFirst != tourFirst ? " or " + traitsFirst : "") + " (x3.5" + (polyglots > 0 ? ", x" + bonus : "") + ")");
                        break;
                    }
                    if (i == 0)
                        ctx.Record("example", gameResult + " -> " + result);
                }
            }
        }

        // Runs before the mods' postfixes, so it sees the game's own figure.
        private static void RecordGameResult(int __result) => gameResult = __result;
    }
}
