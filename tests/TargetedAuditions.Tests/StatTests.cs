using CustomAuditions;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using Xunit;
using static CustomAuditions.CustomAuditions;
using GirlType = Auditions.data._girl._type;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// Candidate stats: the game rolls them, and the mod only reorders them by skill priority during an
    /// audition. The game's freeze on some platinum rolls is left to Unofficial Patch.
    /// </summary>
    public class StatTests
    {
        private static readonly MethodInfo Generate = AccessTools.Method(typeof(data_girls), "GenerateParams");

        private static readonly GirlType[] Types = { GirlType.normal, GirlType.silver, GirlType.golden, GirlType.platinum };

        public StatTests()
        {
            Seams.Reset();
        }

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
            paramTypes.Select(p => (girl.getParam(p).val, girl.getParam(p).potential)).ToArray();

        private static float[] SortedValues(data_girls.girls girl) =>
            paramTypes.Select(p => girl.getParam(p).val).OrderBy(v => v).ToArray();

        /// <summary>
        /// The game's stat roll with the mod's patch on it. Returns null when the game would hang.
        /// </summary>
        private static data_girls.girls RunPatched(int seed, GirlType type, int points)
        {
            Seams.Seeded(seed);
            Seams.Points = _ => points;
            data_girls.girls girl = Seams.NewGirl();
            try
            {
                Generate.Invoke(Seams.Component<data_girls>(), new object[] { girl, type });
            }
            catch (TargetInvocationException e) when (e.InnerException is HangException)
            {
                return null;
            }
            return girl;
        }

        /// <summary>
        /// The game's stat roll without the mod. Returns null when the game would hang.
        /// </summary>
        private static data_girls.girls RunVanilla(int seed, GirlType type, int points)
        {
            Seams.Seeded(seed);
            Seams.Points = _ => points;
            data_girls.girls girl = Seams.NewGirl();
            try
            {
                Seams.VanillaGenerateParams(Seams.Component<data_girls>(), girl, type);
            }
            catch (HangException)
            {
                return null;
            }
            return girl;
        }

        /// <summary>
        /// Outside an audition (unique idols, rivals), the mod changes nothing, including the game's hangs.
        /// </summary>
        [Fact]
        public void OutsideAudition_MatchesTheGame()
        {
            int compared = 0;
            int hangs = 0;

            foreach (GirlType type in Types)
            {
                foreach (int points in PointsToTry(type))
                {
                    for (int seed = 0; seed < 100; seed++)
                    {
                        data_girls.girls vanilla = RunVanilla(seed, type, points);
                        data_girls.girls patched = RunPatched(seed, type, points);
                        if (vanilla == null)
                        {
                            Assert.Null(patched);
                            hangs++;
                            continue;
                        }
                        Assert.NotNull(patched);
                        Assert.Equal(Stats(vanilla), Stats(patched));
                        compared++;
                    }
                }
            }

            Assert.True(compared > 1000, $"Only {compared} rolls compared");
            Assert.True(hangs > 0, "Expected some platinum rolls to hang the game");
        }

        /// <summary>
        /// During an audition, the candidate gets the game's stat values, only reassigned to skills.
        /// </summary>
        [Fact]
        public void DuringAudition_PrioritiesOnlyReorderTheGamesStats()
        {
            BeginAuditionGeneration();
            priorityDict[data_girls._paramType.vocal] = 500;
            int compared = 0;

            foreach (GirlType type in Types)
            {
                foreach (int points in PointsToTry(type))
                {
                    for (int seed = 0; seed < 30; seed++)
                    {
                        data_girls.girls vanilla = RunVanilla(seed, type, points);
                        if (vanilla == null)
                            continue;
                        data_girls.girls patched = RunPatched(seed, type, points);
                        Assert.Equal(SortedValues(vanilla), SortedValues(patched));
                        AssertValid(patched, type);
                        compared++;
                    }
                }
            }

            Assert.True(compared > 300, $"Only {compared} rolls compared");
        }

        [Fact]
        public void DuringAudition_TopPriorityGetsTheBestStat()
        {
            BeginAuditionGeneration();
            foreach (data_girls._paramType p in paramTypes)
                priorityDict[p] = 1;
            priorityDict[data_girls._paramType.dance] = 1_000_000;

            for (int seed = 0; seed < 50; seed++)
            {
                data_girls.girls girl = RunPatched(seed, GirlType.golden, 600);
                Assert.NotNull(girl);

                float dance = girl.getParam(data_girls._paramType.dance).val;
                Assert.Equal(paramTypes.Max(p => girl.getParam(p).val), dance);
            }
        }

        /// <summary>
        /// Stats are 1-99. Potentials are 99 for platinum, otherwise at least the stat and within the
        /// type's band (vanilla: normal 10-80, silver 55-85, gold 70-95).
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

            foreach (data_girls._paramType p in paramTypes)
            {
                data_girls.girls.param param = girl.getParam(p);
                Assert.InRange(param.val, 1f, 99f);
                if (type == GirlType.platinum)
                {
                    Assert.Equal(99, param.potential);
                }
                else if (param.val >= high)
                {
                    Assert.Equal((int)param.val, param.potential);
                }
                else
                {
                    Assert.InRange(param.potential, Math.Max((int)param.val, low), high - 1);
                }
            }
        }
    }
}
