using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Extended SSK on a real election: the game's own results, transpiled to rank more than 10
    /// idols, revealed place by place in the real results popup. The unit tests check the fame
    /// bonuses and the transpiler's IL, but never run the results or the popup.
    /// An election can't be undone: the rest of the boot sees it finished.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class ExtendedSskTests
    {
        private const string HarmonyId = "com.tel.extendedssk";
        private const string LimitVariable = "ExtendedSSK_Limit";
        private const int Limit = 64;
        private const int VanillaRanks = 10;

        private static readonly Dictionary<data_girls.girls, float> fameAwarded = new Dictionary<data_girls.girls, float>();

        /// <summary>
        /// Every idol who can take part is ranked, up to the limit, past vanilla's 10; the popup awards
        /// every place once and finishes the election.
        /// </summary>
        [InGameTest(Suite = ModTest.ElectionsSuite, Order = 10)]
        private static IEnumerator ElectionRevealsEveryRank(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            int eligible = data_girls.girl.Count(g => g.CanParticipateInSSK());
            int expected = System.Math.Min(Limit, eligible);
            ctx.Record("eligibleIdols", eligible);
            if (expected <= VanillaRanks)
            {
                ctx.Fail($"The save has {eligible} idols who can take part; the check needs more than {VanillaRanks}");
                yield break;
            }

            fameAwarded.Clear();
            SEvent_SSK._SSK election;
            using (Game.Variable(LimitVariable, Limit.ToString()))
            using (TestTools.Spy(AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam)),
                       prefix: AccessTools.Method(typeof(ExtendedSskTests), nameof(RecordFame))))
            {
                election = Game.NewElection();
                yield return TestTools.WaitFor(ctx, () => !PopupManager.IsThereAnOpenPopup_(), 5f, "the new-election popup to close");

                // As the election's concert does when it ends (Concert_Popup.FinishConcert). The single's
                // release, the 14-day wait and the concert minigame are skipped: the mod doesn't touch them.
                Game.Main.Data.GetComponent<SEvent_SSK>().StartSSK();
                yield return Game.ClickThroughElection(ctx);
            }

            List<SEvent_SSK._SSK._result> results = election.Results;
            ctx.Record("ranks", results.Count);
            ctx.Assert(results.Count == expected, $"The election ranked {results.Count} idols; expected {expected}, every idol who can take part up to {Limit}");
            ctx.Assert(results.Select(r => r.Place).OrderBy(p => p).SequenceEqual(Enumerable.Range(1, results.Count)),
                "The places aren't 1 to " + results.Count + ": " + string.Join(" ", results.Select(r => r.Place.ToString()).ToArray()));

            List<int> notAwarded = results
                .Where(r => !fameAwarded.TryGetValue(r.Girl, out float fame) || fame != r.FamePoints)
                .Select(r => r.Place)
                .OrderBy(p => p)
                .ToList();
            ctx.Assert(notAwarded.Count == 0, "The popup didn't award its fame bonus once to place " + string.Join(", ", notAwarded.Select(p => p.ToString()).ToArray()));
            if (results.Count > VanillaRanks)
                ctx.Record("rank11Fame", results.First(r => r.Place == VanillaRanks + 1).FamePoints);

            ctx.Assert(election.Status == SEvent_Tour.tour._status.finished, "The election isn't finished: " + election.Status);
            ctx.Assert(PopupManager.GetOpenPopupType() != PopupManager._type.sevent_SSK, "The results popup is still open");
        }

        /// <summary>Sums the fame each idol is given while the results popup runs (before award bonuses).</summary>
        private static void RecordFame(data_girls.girls __instance, data_girls._paramType type, float val)
        {
            if (type != data_girls._paramType.famePoints)
                return;
            fameAwarded.TryGetValue(__instance, out float sum);
            fameAwarded[__instance] = sum + val;
        }
    }
}
