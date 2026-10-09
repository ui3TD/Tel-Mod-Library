using System.Collections;
using System.Linq;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Effortless Training's transpiler on DoGirlTraining, on the fixture's real trainee, with
    /// whatever else patches that method in this session (Policies That Matter's transpiler).
    /// </summary>
    internal static class EffortlessTrainingTests
    {
        private const string HarmonyId = "com.tel.effortlesstraining";
        private const int Trainee = 167;

        /// <summary>
        /// One training tick charges 1 physical stamina per day instead of 3, with the game's
        /// own multipliers (1.3x for the Quality performance policy, 5x for Moonlighters) intact.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator TrainingCostsOneStaminaPerDay(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            agency._room room = Game.TrainingRoom(Trainee);
            float moonlighter = room.girl.trait == traits._trait._type.Moonlighter ? 5f : 1f;
            using (Game.ClockSpeed(50))
            {
                foreach (policies._value performances in new[] { policies._value.performances_neutral, policies._value.performances_quality })
                {
                    using (Game.SelectPolicy(policies._type.performances, performances))
                    {
                        float quality = performances == policies._value.performances_quality ? 1.3f : 1f;
                        float expected = -1f / Game.TrainingTicksPerDay * quality * moonlighter;
                        float[] physical = Game.TrainingTickAddParams(room)
                            .Where(c => c.Key == data_girls._paramType.physicalStamina).Select(c => c.Value).ToArray();

                        ctx.Record(performances + ".physicalPerTick", physical.Length == 1 ? physical[0] : float.NaN);
                        if (physical.Length != 1)
                            ctx.Fail(performances + ": expected one physical stamina charge per tick, got " + physical.Length);
                        else
                            ctx.Assert(Mathf.Abs(physical[0] - expected) <= 1e-5f * Mathf.Abs(expected),
                                performances + ": a tick charged " + physical[0] + " physical stamina, expected " + expected
                                + " (1 per day over " + Game.TrainingTicksPerDay + " ticks)");
                    }
                }
            }
        }
    }
}
