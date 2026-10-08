using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Xunit;
using GirlType = Auditions.data._girl._type;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The game's stat roll for a new idol reserves up to 3 stats, then spends the rest of the points on the
    /// other stats, up to 99 each. When the points can't fit (about 1 platinum roll in 12) it spins forever.
    /// The mod rolls those again; every roll that fits plays out exactly as in the game.
    /// </summary>
    public class StatRollTests
    {
        private static readonly GirlType[] Types = { GirlType.normal, GirlType.silver, GirlType.golden, GirlType.platinum };

        private static readonly data_girls._paramType[] Skills =
        {
            data_girls._paramType.cute, data_girls._paramType.cool, data_girls._paramType.sexy, data_girls._paramType.pretty,
            data_girls._paramType.vocal, data_girls._paramType.dance, data_girls._paramType.funny, data_girls._paramType.smart,
        };

        public StatRollTests() => TestGame.Reset();

        private static int BasePoints(GirlType type) => type switch
        {
            GirlType.normal => GameInt("Points_Normal"),
            GirlType.silver => GameInt("Points_Silver"),
            GirlType.golden => GameInt("Points_Gold"),
            _ => GameInt("Points_Plat"),
        };

        private static int GameInt(string field) => (int)AccessTools.Field(typeof(Auditions), field).GetValue(null);

        private static int[] PointsToTry(GirlType type)
        {
            int randomizer = GameInt("Points_Randomizer");
            return new[] { BasePoints(type) - randomizer + 1, BasePoints(type), BasePoints(type) + randomizer - 1 };
        }

        private static (float val, int potential)[] Stats(data_girls.girls girl) =>
            Skills.Select(p => (girl.getParam(p).val, girl.getParam(p).potential)).ToArray();

        /// <summary>
        /// The game's stat roll with the mod applied.
        /// </summary>
        private static data_girls.girls RunPatched(GirlType type)
        {
            data_girls.girls girl = TestGame.Idol(1f);
            TestGame.CallPrivate(TestGame.Component<data_girls>(), typeof(data_girls), "GenerateParams", girl, type);
            return girl;
        }

        /// <summary>
        /// The game's stat roll without the mod. Returns null when the game would hang.
        /// </summary>
        private static data_girls.girls RunVanilla(GirlType type)
        {
            data_girls.girls girl = TestGame.Idol(1f);
            try
            {
                Seams.VanillaGenerateParams(TestGame.Component<data_girls>(), girl, type);
            }
            catch (HangException)
            {
                return null;
            }
            return girl;
        }

        [Fact]
        public void RollsThatFit_MatchTheGame()
        {
            int compared = 0;
            int hangs = 0;

            foreach (GirlType type in Types)
            {
                foreach (int points in PointsToTry(type))
                {
                    Seams.Points = _ => points;
                    for (int seed = 0; seed < 100; seed++)
                    {
                        Seams.Seeded(seed);
                        data_girls.girls vanilla = RunVanilla(type);
                        Seams.Seeded(seed);
                        data_girls.girls patched = RunPatched(type);
                        AssertValid(patched, type);
                        if (vanilla == null)
                        {
                            hangs++;
                            continue;
                        }
                        Assert.Equal(Stats(vanilla), Stats(patched));
                        compared++;
                    }
                }
            }

            Assert.True(compared > 1000, $"Only {compared} rolls compared");
            Assert.True(hangs > 0, "Expected some platinum rolls to hang the game");
        }

        /// <summary>
        /// Platinum with three reserved stats: 700 points, minus 8 and 3 x 40 reserved, leaves 572 for five
        /// stats that hold at most 5 x 98 = 490. The game spins forever; the mod throws that roll away, and the
        /// idol is exactly the one the game makes from the rolls that follow.
        /// </summary>
        [Fact]
        public void RollThatCantFit_IsRolledAgain()
        {
            // chance(20) no, chance(20) no, chance(5) yes: 3 reserved stats, each rolled at 40
            int[] impossible = { 99, 99, 0, 40, 40, 40 };
            Seams.Points = _ => 700;

            UseRolls(impossible, seed: 3);
            Assert.Null(RunVanilla(GirlType.platinum));

            UseRolls(impossible, seed: 3);
            data_girls.girls patched = RunPatched(GirlType.platinum);

            Seams.Seeded(3);
            data_girls.girls expected = RunVanilla(GirlType.platinum);
            Assert.NotNull(expected);
            Assert.Equal(Stats(expected), Stats(patched));
        }

        /// <summary>
        /// Platinum at the top of its points range, where the game hangs most often, always finishes, and the
        /// stats add up to the points the kept roll asked for.
        /// </summary>
        [Fact]
        public void PlatinumRolls_AlwaysFinish()
        {
            int points = BasePoints(GirlType.platinum) + GameInt("Points_Randomizer") - 1;
            Seams.Points = _ => points;
            int hangs = 0;

            for (int seed = 0; seed < 300; seed++)
            {
                Seams.Seeded(seed);
                if (RunVanilla(GirlType.platinum) == null)
                    hangs++;

                Seams.Seeded(seed);
                data_girls.girls girl = RunPatched(GirlType.platinum);
                AssertValid(girl, GirlType.platinum);
                Assert.InRange(Skills.Sum(p => girl.getParam(p).val), points - 3, points);
            }

            Assert.True(hangs > 0, "Expected some platinum rolls to hang the game");
        }

        /// <summary>
        /// If another mod raises the points so far that no roll can fit, rerolling can't help. After 100
        /// rerolls the extra points are dropped, so the game still doesn't freeze.
        /// </summary>
        [Fact]
        public void PointsThatNeverFit_DropTheExtra()
        {
            Seams.Points = _ => 2000;
            Seams.Seeded(1);

            data_girls.girls girl = RunPatched(GirlType.platinum);

            AssertValid(girl, GirlType.platinum);
            Assert.True(Skills.Count(p => girl.getParam(p).val == 99f) >= 5);
            Assert.Contains(Log.Of(LogType.Warning), m => m.Contains("rerolls"));

            // The next idol rolls normally again
            Seams.Points = _ => 600;
            Seams.Seeded(2);
            data_girls.girls vanilla = RunVanilla(GirlType.golden);
            Seams.Seeded(2);
            Assert.Equal(Stats(vanilla), Stats(RunPatched(GirlType.golden)));
        }

        [Fact]
        public void Room_CountsOnlyStatsThatArentReserved()
        {
            List<int> stats = new() { 40, 60, 1, 1, 50, 99, 1, 1 };
            Assert.Equal(98 * 4 + 49 + 0, data_girls_GenerateParams.Room(stats, 2));
            Assert.Equal(400, data_girls_GenerateParams.FitBudget(stats, 2, 400));
            Assert.Equal(98 * 4 + 49, data_girls_GenerateParams.FitBudget(stats, 2, 1000));
        }

        /// <summary>
        /// The first given rolls, then a seeded generator.
        /// </summary>
        private static void UseRolls(int[] rolls, int seed)
        {
            Seams.Seeded(seed);
            Queue<int> fixedRolls = new(rolls);
            Func<int, int, int> seeded = Seams.Range;
            Seams.Range = (min, max) => fixedRolls.Count > 0 ? fixedRolls.Dequeue() : seeded(min, max);
        }

        /// <summary>
        /// Stats are 1-99. Potentials are 99 for platinum, otherwise at least the stat and within the type's
        /// band (normal 10-80, silver 55-85, gold 70-95).
        /// </summary>
        private static void AssertValid(data_girls.girls girl, GirlType type)
        {
            (int low, int high) = type switch
            {
                GirlType.normal => (10, 80),
                GirlType.silver => (55, 85),
                GirlType.golden => (70, 95),
                _ => (99, 99),
            };

            foreach (data_girls._paramType p in Skills)
            {
                data_girls.girls.param param = girl.getParam(p);
                Assert.InRange(param.val, 1f, 99f);
                if (type == GirlType.platinum)
                    Assert.Equal(99, param.potential);
                else if (param.val >= high)
                    Assert.Equal((int)param.val, param.potential);
                else
                    Assert.InRange(param.potential, Math.Max((int)param.val, low), high - 1);
            }
        }
    }
}
