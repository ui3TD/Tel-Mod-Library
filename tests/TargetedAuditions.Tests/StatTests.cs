using CustomAuditions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using static CustomAuditions.CustomAuditions;
using GirlType = Auditions.data._girl._type;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// Candidate stats: the mod's copy of the game's stat roll must match the game whenever the game
    /// finishes, and must finish where the game spins forever.
    /// </summary>
    public class StatTests
    {
        private static readonly MethodInfo ModGenerate = AccessTools.Method(typeof(data_girls_GenerateParams), "GenerateParamsSafely");
        private static readonly MethodInfo VanillaGenerate = AccessTools.Method(typeof(data_girls), "GenerateParams");

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

        private static (float val, int potential)[] Stats(data_girls.girls girl) =>
            paramTypes.Select(p => (girl.getParam(p).val, girl.getParam(p).potential)).ToArray();

        private static data_girls.girls RunMod(int seed, GirlType type, int points)
        {
            Seams.Seeded(seed);
            Seams.Points = _ => points;
            data_girls.girls girl = Seams.NewGirl();
            ModGenerate.Invoke(null, new object[] { girl, type });
            return girl;
        }

        /// <summary>
        /// Returns null when the game would hang.
        /// </summary>
        private static data_girls.girls RunVanilla(int seed, GirlType type, int points)
        {
            Seams.Seeded(seed);
            Seams.Points = _ => points;
            data_girls.girls girl = Seams.NewGirl();
            try
            {
                VanillaGenerate.Invoke(Seams.Component<data_girls>(), new object[] { girl, type });
            }
            catch (TargetInvocationException e) when (e.InnerException is HangException)
            {
                return null;
            }
            return girl;
        }

        /// <summary>
        /// Every type at the low, middle and high end of its points range, over many seeds. Skill
        /// priorities are off here (they run only during an audition); see PriorityTests.
        /// </summary>
        [Fact]
        public void MatchesTheGame_WheneverTheGameFinishes()
        {
            int randomizer = GameInt("Points_Randomizer");
            int compared = 0;
            int hangs = 0;

            foreach (GirlType type in Types)
            {
                foreach (int points in new[] { BasePoints(type) - randomizer + 1, BasePoints(type), BasePoints(type) + randomizer - 1 })
                {
                    for (int seed = 0; seed < 300; seed++)
                    {
                        data_girls.girls vanilla = RunVanilla(seed, type, points);
                        data_girls.girls mod = RunMod(seed, type, points);
                        AssertValid(mod, type);
                        if (vanilla == null)
                        {
                            hangs++;
                            continue;
                        }
                        Assert.Equal(Stats(vanilla), Stats(mod));
                        compared++;
                    }
                }
            }

            Assert.True(compared > 3000, $"Only {compared} rolls compared");
            Assert.True(hangs > 0, "Expected some platinum rolls to hang the game");
        }

        /// <summary>
        /// Platinum with three reserved stats: 700 points, minus 8 and 3 x 40 reserved, leaves 572
        /// for five stats that hold at most 5 x 98 = 490. The game spins forever.
        /// </summary>
        [Fact]
        public void ImpossiblePlatinumRoll_HangsTheGame_ButNotTheMod()
        {
            int[] rolls = { 99, 99, 0, 40, 40, 40 };

            Seams.Points = _ => 700;
            Queue<int> fixedRolls = new(rolls);
            System.Random rng = new(3);
            Seams.Range = (lo, hi) => fixedRolls.Count > 0 ? fixedRolls.Dequeue() : rng.Next(lo, hi);
            Seams.RangeCalls = 0;
            data_girls.girls vanillaGirl = Seams.NewGirl();
            TargetInvocationException hang = Assert.Throws<TargetInvocationException>(() =>
                VanillaGenerate.Invoke(Seams.Component<data_girls>(), new object[] { vanillaGirl, GirlType.platinum }));
            Assert.IsType<HangException>(hang.InnerException);

            fixedRolls = new(rolls);
            Seams.RangeCalls = 0;
            data_girls.girls girl = Seams.NewGirl();
            ModGenerate.Invoke(null, new object[] { girl, GirlType.platinum });

            AssertValid(girl, GirlType.platinum);
            // Five stats full at 99; the 82 points left over go to the three reserved stats.
            float[] values = paramTypes.Select(p => girl.getParam(p).val).OrderByDescending(v => v).ToArray();
            Assert.Equal(new float[] { 99, 99, 99, 99, 99 }, values.Take(5));
            Assert.Equal(40 * 3 + 82, values.Skip(5).Sum());
        }

        /// <summary>
        /// More points than all eight stats can hold: every stat ends at 99 and the rest is dropped.
        /// </summary>
        [Fact]
        public void BudgetOverEveryStat_FillsAllStats()
        {
            data_girls.girls girl = RunMod(1, GirlType.platinum, 2000);
            Assert.All(paramTypes, p => Assert.Equal(99f, girl.getParam(p).val));
        }

        [Theory]
        [InlineData(GirlType.normal)]
        [InlineData(GirlType.silver)]
        [InlineData(GirlType.golden)]
        [InlineData(GirlType.platinum)]
        public void Stats_AreValidAcrossTheRange(GirlType type)
        {
            int randomizer = GameInt("Points_Randomizer");
            for (int seed = 0; seed < 500; seed++)
            {
                int points = BasePoints(type) + (seed % (2 * randomizer - 1)) - randomizer + 1;
                AssertValid(RunMod(seed, type, points), type);
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

        [Fact]
        public void OutsideAudition_TheGameGeneratesStats()
        {
            Seams.Rolls();
            Assert.True(data_girls_GenerateParams.Prefix(null, Seams.NewGirl(), GirlType.normal));
        }

        /// <summary>
        /// During an audition the mod's copy runs instead, with skill priorities applied.
        /// </summary>
        [Fact]
        public void DuringAudition_ModGeneratesStatsWithPriorities()
        {
            BeginAuditionGeneration();
            foreach (data_girls._paramType p in paramTypes)
                priorityDict[p] = 1;
            priorityDict[data_girls._paramType.dance] = 1_000_000;
            Seams.Points = _ => 600;
            Seams.Seeded(5);

            for (int i = 0; i < 50; i++)
            {
                data_girls.girls girl = Seams.NewGirl();
                Assert.False(data_girls_GenerateParams.Prefix(null, girl, GirlType.golden));

                float dance = girl.getParam(data_girls._paramType.dance).val;
                Assert.Equal(paramTypes.Max(p => girl.getParam(p).val), dance);
            }
        }
    }
}
