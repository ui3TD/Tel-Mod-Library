using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static resources.fanType;

namespace FanAttrition.Tests
{
    /// <summary>
    /// New fans drift toward each idol's natural fan mix: hardcore fans are rarer, and a segment already above its
    /// natural share passes part of its gain to her other segments.
    /// </summary>
    public class GainSpillTests
    {
        private static readonly resources.fanType[] Genders = { male, female };
        private static readonly resources.fanType[] Ages = { teen, youngAdult, adult };

        public GainSpillTests()
        {
            TestGame.Reset();
            // Fractions of a fan round down
            GainSpill.Chance = () => 1.0;
        }

        /// <summary>
        /// An idol with the game's 12 segments, every one at neutral opinion and appeal (weight 1).
        /// </summary>
        private static data_girls.girls Idol(long hardcorePerSegment, long casualPerSegment)
        {
            List<resources._fan> fans = new();
            foreach (resources.fanType gender in Genders)
                foreach (resources.fanType hardness in new[] { hardcore, casual })
                    foreach (resources.fanType age in Ages)
                    {
                        resources._fan fan = TestGame.Fans(gender, hardness, age, hardness == hardcore ? hardcorePerSegment : casualPerSegment);
                        fan.Ratio = 0.5f;
                        fan.appeal = 0.5f;
                        fans.Add(fan);
                    }
            return TestGame.Idol(0.5f, fans.ToArray());
        }

        private static resources._fan Segment(data_girls.girls girl, resources.fanType gender, resources.fanType hardness, resources.fanType age) =>
            girl.Fans.First(fan => fan.gender == gender && fan.hardcoreness == hardness && fan.age == age);

        private static long Total(data_girls.girls girl) => girl.Fans.Sum(fan => fan.people);

        private static double HardcoreShare(data_girls.girls girl) =>
            (double)girl.Fans.Where(fan => fan.hardcoreness == hardcore).Sum(fan => fan.people) / Total(girl);

        /// <summary>
        /// A gain the way the hooks see it: snapshot, the game adds the fans, then the spill.
        /// </summary>
        private static void Gain(data_girls.girls girl, resources._fan fan, long fans)
        {
            Dictionary<resources._fan, long> before = GainSpill.Snapshot(girl);
            fan.people += fans;
            GainSpill.Apply(girl, before);
        }

        [Fact]
        public void NaturalMix_NeutralIdol_Is40PercentHardcore()
        {
            data_girls.girls girl = Idol(100, 100);

            Dictionary<resources._fan, double> natural = GainSpill.NaturalMix(girl.Fans);

            Assert.Equal(0.4, natural.Where(entry => entry.Key.hardcoreness == hardcore).Sum(entry => entry.Value), 6);
            Assert.Equal(1.0, natural.Values.Sum(), 6);
        }

        /// <summary>
        /// Higher opinion and appeal raise a segment's natural share; no votes count as neutral.
        /// </summary>
        [Fact]
        public void NaturalMix_FollowsOpinionAndAppeal()
        {
            data_girls.girls girl = Idol(100, 100);
            resources._fan liked = Segment(girl, male, casual, teen);
            resources._fan noVotes = Segment(girl, female, casual, teen);
            liked.Ratio = 1f;
            noVotes.Ratio = float.NaN;

            Dictionary<resources._fan, double> natural = GainSpill.NaturalMix(girl.Fans);

            Assert.Equal(1.5 / 1.0, natural[liked] / natural[Segment(girl, male, casual, adult)], 6);
            Assert.Equal(natural[Segment(girl, male, casual, adult)], natural[noVotes], 6);
        }

        /// <summary>
        /// An idol who is 80% hardcore (natural 40%) gains 1,000 hardcore fans in one segment: 200 arrive as casual
        /// fans of the same gender and age, and the segment, at twice its natural share, keeps
        /// 800 x 0.5^0.1 (747) and spreads the other 53 by her natural mix. The total gained is unchanged.
        /// </summary>
        [Fact]
        public void OverRepresentedHardcoreGain_ConvertsAndSpills()
        {
            data_girls.girls girl = Idol(2000, 500);
            resources._fan target = Segment(girl, male, hardcore, teen);
            resources._fan sameCasual = Segment(girl, male, casual, teen);
            long before = Total(girl);

            Gain(girl, target, 1000);

            Assert.Equal(before + 1000, Total(girl));
            Assert.InRange(target.people, 2000 + 747, 2000 + 747 + 53);
            Assert.InRange(sameCasual.people, 500 + 200, 500 + 200 + 53);
            Assert.True(girl.Fans.Where(fan => fan != target && fan != sameCasual).Sum(fan => fan.people) > before - 2500);
        }

        /// <summary>
        /// A segment below its natural share keeps its whole gain, and casual gains aren't converted.
        /// </summary>
        [Fact]
        public void UnderRepresentedGain_IsKept()
        {
            data_girls.girls girl = Idol(2000, 500);
            resources._fan casualTeen = Segment(girl, male, casual, teen);
            long[] others = girl.Fans.Where(fan => fan != casualTeen).Select(fan => fan.people).ToArray();

            Gain(girl, casualTeen, 1000);

            Assert.Equal(1500, casualTeen.people);
            Assert.Equal(others, girl.Fans.Where(fan => fan != casualTeen).Select(fan => fan.people).ToArray());
        }

        /// <summary>
        /// Below her natural hardcore share, hardcore gains arrive whole: rarity isn't counted twice.
        /// </summary>
        [Fact]
        public void HardcoreBelowItsNaturalShare_IsNotConverted()
        {
            data_girls.girls girl = Idol(200, 1000);
            resources._fan target = Segment(girl, male, hardcore, teen);

            Gain(girl, target, 100);

            Assert.Equal(300, target.people);
            Assert.Equal(1000, Segment(girl, male, casual, teen).people);
        }

        [Fact]
        public void IdolWithoutFans_KeepsEverything()
        {
            data_girls.girls girl = Idol(0, 0);
            resources._fan target = Segment(girl, male, hardcore, teen);

            Gain(girl, target, 1000);

            Assert.Equal(1000, target.people);
            Assert.Equal(1000, Total(girl));
        }

        [Fact]
        public void Losses_AreLeftAlone()
        {
            data_girls.girls girl = Idol(2000, 500);
            resources._fan target = Segment(girl, male, hardcore, teen);

            Gain(girl, target, -300);

            Assert.Equal(1700, target.people);
            Assert.Equal(6 * 2000 + 6 * 500 - 300, Total(girl));
        }

        [Fact]
        public void Hooks_IgnoreLossesAndSkippedGains()
        {
            data_girls.girls girl = Idol(2000, 500);

            data_girls_girls_AddFans_Segment.Prefix(girl, -5, out Dictionary<resources._fan, long> loss);
            data_girls_girls_AddFans_Split.Prefix(girl, 1000, out Dictionary<resources._fan, long> skipped);
            data_girls_girls_AddFans_Split.Postfix(girl, skipped);

            Assert.Null(loss);
            Assert.Equal(6 * 2000 + 6 * 500, Total(girl));
        }

        /// <summary>
        /// A show's hook covers every idol in the cast who hasn't graduated.
        /// </summary>
        [Fact]
        public void ShowHook_SpillsForEachCastIdol()
        {
            data_girls.girls first = Idol(2000, 500);
            data_girls.girls second = Idol(2000, 500);
            data_girls.girls graduated = Idol(2000, 500);
            graduated.status = data_girls._status.graduated;
            List<data_girls.girls> cast = new() { first, second, graduated };

            data_girls_AddFans_Equally_Segment.Prefix(2000, cast, out var state);
            foreach (data_girls.girls girl in cast)
                Segment(girl, male, hardcore, teen).people += 1000;
            data_girls_AddFans_Equally_Segment.Postfix(state);

            Assert.Equal(2, state.Count);
            Assert.All(new[] { first, second }, girl => Assert.InRange(Segment(girl, male, hardcore, teen).people, 2000 + 747, 2000 + 747 + 53));
            Assert.Equal(3000, Segment(graduated, male, hardcore, teen).people);
        }

        /// <summary>
        /// Over many years of churn by size and new fans of a fixed mix, a neutral idol settles near the planned
        /// shares: 95% hardcore new fans settle at about 73% hardcore, half-and-half at about 40%, and a type
        /// with no new fans at all keeps a foothold.
        /// </summary>
        [Theory]
        [InlineData(0.95, 0.70, 0.76)]
        [InlineData(0.50, 0.38, 0.42)]
        [InlineData(1.00, 0.74, 0.80)]
        [InlineData(0.00, 0.01, 0.03)]
        public void LongRun_SettlesNearThePlannedMix(double hardcoreNew, double low, double high)
        {
            data_girls.girls girl = Idol(50000, 50000);
            const long daily = 2000;
            for (int day = 0; day < 6000; day++)
            {
                Utility.TakeFromSegments(girl, daily);
                long hardcoreFans = (long)Math.Round(daily * hardcoreNew);
                foreach (resources.fanType gender in Genders)
                    foreach (resources.fanType age in Ages)
                    {
                        Gain(girl, Segment(girl, gender, hardcore, age), hardcoreFans / 6);
                        Gain(girl, Segment(girl, gender, casual, age), (daily - hardcoreFans) / 6);
                    }
            }

            Assert.InRange(HardcoreShare(girl), low, high);
        }
    }
}
