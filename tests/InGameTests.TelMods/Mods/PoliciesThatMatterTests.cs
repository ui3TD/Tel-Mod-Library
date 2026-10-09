using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Policies That Matter on the real policy list (every enabled mod's policies.json loaded)
    /// and on the fixture's real trainee, alongside the other mods that patch training.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class PoliciesThatMatterTests
    {
        private const string HarmonyId = "com.tel.policiesthatmatter";
        private const int Trainee = 167;

        /// <summary>
        /// After every enabled mod's policy file has loaded, each policy type has one header, one
        /// selected choice, and no choice listed twice.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator EveryPolicyTypeHasOneHeaderAndOneChoice(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            foreach (policies._type type in Enum.GetValues(typeof(policies._type)))
            {
                var values = policies.Values.Where(v => v.Type == type).ToList();
                int headers = values.Count(v => v.Value == policies._value.NONE);
                int selected = values.Count(v => v.Value != policies._value.NONE && v.Selected);
                ctx.Assert(headers == 1, type + " has " + headers + " headers, expected 1");
                ctx.Assert(selected == 1, type + " has " + selected + " selected choices, expected 1");
                foreach (var duplicate in values.GroupBy(v => v.Value).Where(g => g.Count() > 1))
                    ctx.Fail(type + "." + duplicate.Key + " is listed " + duplicate.Count() + " times");
            }
            ctx.Record("policyValues", policies.Values.Count);
        }

        /// <summary>
        /// Under the Quality performance policy, one training tick costs 0.5/7 mental stamina per
        /// day (0.5 a week); under any other policy it costs none.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator QualityTrainingCostsMentalStamina(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            agency._room room = Training.Room(Trainee);
            using (Game.ClockSpeed(50))
            {
                foreach (policies._value performances in new[] { policies._value.performances_neutral, policies._value.performances_quality })
                {
                    using (Game.SelectPolicy(policies._type.performances, performances))
                    {
                        float[] mental = Training.TickAddParams(room)
                            .Where(c => c.Key == data_girls._paramType.mentalStamina).Select(c => c.Value).ToArray();
                        ctx.Record(performances + ".mentalPerTick", mental.Length == 1 ? mental[0] : float.NaN);

                        if (performances != policies._value.performances_quality)
                        {
                            ctx.Assert(mental.Length == 0, performances + ": a tick charged mental stamina " + mental.Length + " times, expected none");
                            continue;
                        }
                        float expected = -0.5f / 7 / Training.TicksPerDay;
                        if (mental.Length != 1)
                            ctx.Fail(performances + ": expected one mental stamina charge per tick, got " + mental.Length);
                        else
                            ctx.Assert(Mathf.Abs(mental[0] - expected) <= 1e-5f * Mathf.Abs(expected),
                                performances + ": a tick charged " + mental[0] + " mental stamina, expected " + expected);
                    }
                }
            }
        }

        /// <summary>
        /// The Quality performance policy halves the trainee's training time, on top of whatever
        /// else changes it in this session (MBTI's ESTJ bonus, for one).
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator QualityHalvesTrainingTime(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            agency._room room = Training.Room(Trainee);
            data_girls.girls.param param = room.girl.getParam(room.trainingParam().Value);
            float neutral, quality;
            using (Game.SelectPolicy(policies._type.performances, policies._value.performances_neutral))
                neutral = param.GetDuration();
            using (Game.SelectPolicy(policies._type.performances, policies._value.performances_quality))
                quality = param.GetDuration();

            ctx.Record("neutral", neutral);
            ctx.Record("quality", quality);
            ctx.Assert(neutral > 0 && Mathf.Abs(quality / neutral - 0.5f) < 1e-4f,
                "Training " + param.type + " takes " + quality + " under Quality and " + neutral + " otherwise; expected half");
        }
    }
}
