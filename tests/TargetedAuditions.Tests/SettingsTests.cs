using CustomAuditions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CustomAuditions.CustomAuditions;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// What the mod reads from the Mod Menu when an audition starts.
    /// </summary>
    public class AuditionStartTests
    {
        public AuditionStartTests()
        {
            Seams.Reset();
        }

        private static Auditions StartAudition()
        {
            Auditions auditions = Seams.Component<Auditions>();
            Auditions_GenerateGirls.Prefix(auditions, out bool state);
            Assert.True(state);
            return auditions;
        }

        [Fact]
        public void Defaults_MatchTheModMenu()
        {
            Auditions auditions = StartAudition();

            Assert.Equal((12, 23), (minAge, maxAge));
            Assert.Equal(5, auditions.NumberOfGirls);
            Assert.All(paramTypes, p => Assert.Equal(50, priorityDict[p]));
            Assert.Equal(7, chanceLesbian);
        }

        [Fact]
        public void ConfiguredValues_Apply()
        {
            Seams.SetVariable(VARID_MINAGE, "16");
            Seams.SetVariable(VARID_MAXAGE, "20");
            Seams.SetVariable(VARID_COUNT, "25");
            Seams.SetVariable(VARID_PRIO_PREFIX + "vocal", "90");

            Auditions auditions = StartAudition();

            Assert.Equal((16, 20), (minAge, maxAge));
            Assert.Equal(25, auditions.NumberOfGirls);
            Assert.Equal(90, priorityDict[data_girls._paramType.vocal]);
            Assert.Equal(50, priorityDict[data_girls._paramType.cute]);
        }

        [Fact]
        public void ReversedAgeRange_IsSwappedAndSaved()
        {
            Seams.SetVariable(VARID_MINAGE, "30");
            Seams.SetVariable(VARID_MAXAGE, "18");

            StartAudition();

            Assert.Equal((18, 30), (minAge, maxAge));
            Assert.Equal("18", variables.Get(VARID_MINAGE));
            Assert.Equal("30", variables.Get(VARID_MAXAGE));
        }

        /// <summary>
        /// With the (hidden) per-audition popup on, the range typed into the popup is kept.
        /// </summary>
        [Fact]
        public void AgePopupOn_KeepsThePopupRange()
        {
            Seams.SetVariable(VARID_AGELIMIT_POPUP_TOGGLE, "1");
            Seams.SetVariable(VARID_MINAGE, "16");
            Seams.SetVariable(VARID_MAXAGE, "20");
            minAge = 14;
            maxAge = 15;

            StartAudition();

            Assert.Equal((14, 15), (minAge, maxAge));
        }

        [Theory]
        [InlineData(60, 60, 50, 50)]
        [InlineData(80, 40, 66, 34)]
        [InlineData(7, 14, 7, 14)]
        public void OrientationChancesOver100_AreScaledDown(int lesbian, int bi, int expectedLesbian, int expectedBi)
        {
            Seams.SetVariable(VARID_LESCHANCE, lesbian.ToString());
            Seams.SetVariable(VARID_BICHANCE, bi.ToString());

            StartAudition();

            Assert.Equal(expectedLesbian, chanceLesbian);
            Assert.Equal(expectedLesbian.ToString(), variables.Get(VARID_LESCHANCE));
            Assert.Equal(expectedBi.ToString(), variables.Get(VARID_BICHANCE));
        }

        /// <summary>
        /// Bisexual is rolled only after lesbian fails, so its chance is scaled up to keep the
        /// overall share at the Mod Menu value (to within the floor's rounding).
        /// </summary>
        [Theory]
        [InlineData(7, 14)]
        [InlineData(0, 30)]
        [InlineData(50, 50)]
        [InlineData(20, 10)]
        [InlineData(0, 0)]
        public void BisexualShare_MatchesTheModMenu(int lesbian, int bi)
        {
            Seams.SetVariable(VARID_LESCHANCE, lesbian.ToString());
            Seams.SetVariable(VARID_BICHANCE, bi.ToString());

            StartAudition();

            double overallBi = (100 - chanceLesbian) / 100.0 * chanceBi;
            Assert.InRange(overallBi, bi - 1.0, bi);
        }

        [Fact]
        public void AuditionGeneration_EndsWithTheMethod()
        {
            StartAudition();
            Assert.True(IsGeneratingAudition);

            Assert.Null(Auditions_GenerateGirls.Finalizer(null, true));

            Assert.False(IsGeneratingAudition);
            Assert.Empty(Seams.Errors);
        }

        [Fact]
        public void AuditionGeneration_EndsAndLogsOnException()
        {
            StartAudition();
            Exception thrown = new InvalidOperationException("boom");

            Assert.Same(thrown, Auditions_GenerateGirls.Finalizer(thrown, true));

            Assert.False(IsGeneratingAudition);
            Assert.Contains(Seams.Errors, e => e.Contains("boom"));
        }

        [Fact]
        public void NoAuditionGeneration_OutsideGenerateGirls()
        {
            Assert.False(IsGeneratingAudition);
        }
    }

    /// <summary>
    /// Sexual orientation is set on audition candidates only.
    /// </summary>
    public class OrientationTests
    {
        public OrientationTests()
        {
            Seams.Reset();
            chanceLesbian = 7;
            chanceBi = 15;
        }

        private static data_girls.girls Generate(bool inAudition)
        {
            if (inAudition)
                BeginAuditionGeneration();
            data_girls.girls girl = Seams.NewGirl();
            girl.sexuality = data_girls.girls._sexuality.straight;
            data_girls_GenerateGirl.Postfix(ref girl);
            return girl;
        }

        [Theory]
        [InlineData(new[] { 6 }, data_girls.girls._sexuality.lesbian)]
        [InlineData(new[] { 7, 14 }, data_girls.girls._sexuality.bi)]
        [InlineData(new[] { 7, 15 }, data_girls.girls._sexuality.straight)]
        public void Candidate_GetsTheRolledOrientation(int[] rolls, data_girls.girls._sexuality expected)
        {
            Seams.Rolls(rolls);
            Assert.Equal(expected, Generate(inAudition: true).sexuality);
        }

        [Fact]
        public void OtherGirls_AreLeftAlone()
        {
            chanceLesbian = 100;
            Seams.Rolls();
            Assert.Equal(data_girls.girls._sexuality.straight, Generate(inAudition: false).sexuality);
        }

        [Fact]
        public void NullGirl_IsIgnored()
        {
            BeginAuditionGeneration();
            data_girls.girls girl = null;
            data_girls_GenerateGirl.Postfix(ref girl);
        }
    }

    /// <summary>
    /// Skill priorities decide which stat gets each of a candidate's values, highest value first.
    /// </summary>
    public class PriorityTests
    {
        private static readonly List<int> Values = new() { 90, 70, 50, 30, 20, 10, 5, 1 };

        public PriorityTests()
        {
            Seams.Reset();
        }

        [Fact]
        public void OutsideAudition_LeavesValuesAlone()
        {
            Seams.Rolls();
            Assert.Equal(Values, data_girls_GenerateParams.Infix(Values));
        }

        [Fact]
        public void Values_AreRearrangedNotChanged()
        {
            BeginAuditionGeneration();
            for (int seed = 0; seed < 50; seed++)
            {
                Seams.Seeded(seed);
                List<int> output = data_girls_GenerateParams.Infix(Values);
                Assert.Equal(Values.OrderBy(v => v), output.OrderBy(v => v));
            }
        }

        /// <summary>
        /// Each value goes to a remaining stat with probability priority / remaining total.
        /// </summary>
        [Fact]
        public void Rolls_PickStatsByCumulativePriority()
        {
            BeginAuditionGeneration();
            int[] priorities = { 10, 20, 30, 40, 50, 60, 70, 80 };
            for (int i = 0; i < paramTypes.Count; i++)
                priorityDict[paramTypes[i]] = priorities[i];

            // Total 360: roll 11 lands in cool (11-30). Then without cool (340): roll 340 is smart.
            // Then 260: roll 1 is cute. The rest go to the first remaining stat each time.
            Seams.Rolls(11, 340, 1, 1, 1, 1, 1, 1);
            List<int> output = data_girls_GenerateParams.Infix(Values);

            Assert.Equal(new List<int> { 50, 90, 30, 20, 10, 5, 1, 70 }, output);
        }

        [Theory]
        [InlineData(100, 1)]
        [InlineData(50, 50)]
        [InlineData(1, 100)]
        public void BestValue_GoesToAStatInProportionToItsPriority(int vocalPriority, int otherPriority)
        {
            BeginAuditionGeneration();
            foreach (data_girls._paramType p in paramTypes)
                priorityDict[p] = otherPriority;
            priorityDict[data_girls._paramType.vocal] = vocalPriority;
            int vocalIndex = paramTypes.IndexOf(data_girls._paramType.vocal);

            Seams.Seeded(7);
            const int runs = 20000;
            int vocalBest = 0;
            for (int i = 0; i < runs; i++)
            {
                if (data_girls_GenerateParams.Infix(Values)[vocalIndex] == 90)
                    vocalBest++;
            }

            double expected = vocalPriority / (double)(vocalPriority + 7 * otherPriority);
            Assert.InRange(vocalBest / (double)runs, expected - 0.01, expected + 0.01);
        }
    }

    /// <summary>
    /// The age range text box of the per-audition popup (hidden in the Mod Menu).
    /// </summary>
    public class AgePopupInputTests
    {
        public AgePopupInputTests()
        {
            Seams.Reset();
        }

        [Theory]
        [InlineData("12 - 23", true)]
        [InlineData("12-23", true)]
        [InlineData("18-18", true)]
        [InlineData("23 - 12", false)]
        [InlineData("0 - 12", false)]
        [InlineData("12", false)]
        [InlineData("12 - 23 - 30", false)]
        [InlineData("a - b", false)]
        [InlineData("", false)]
        public void Input_IsValidated(string input, bool valid)
        {
            Assert.Equal(valid, IsInputValid(input));
        }

        [Fact]
        public void ValidInput_SetsTheRange()
        {
            ParseAgeRange("15 - 19");
            Assert.Equal((15, 19), (minAge, maxAge));
        }

        [Fact]
        public void InvalidInput_FallsBackToTheModMenuRange()
        {
            defaultMinAge = 14;
            defaultMaxAge = 21;
            ParseAgeRange("nonsense");
            Assert.Equal((14, 21), (minAge, maxAge));
        }
    }
}
