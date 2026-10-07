using System;
using System.Linq;
using TraitsExpansion;
using Xunit;
using static data_girls._paramType;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// World tours win 20% more fans for each active Polyglot idol.
    /// </summary>
    public class PolyglotTests
    {
        public PolyglotTests() => TestGame.Reset();

        private static int NewFans(int vanilla)
        {
            int result = vanilla;
            SEvent_Tour_tour_GetNewFansByAttendance.Postfix(ref result);
            return result;
        }

        [Fact]
        public void NoPolyglot_Unchanged()
        {
            TestGame.Hire(TestGame.Idol());
            Assert.Equal(1000, NewFans(1000));
        }

        [Theory]
        [InlineData(1, 1000, 1200)]
        [InlineData(2, 1000, 1400)]
        [InlineData(5, 1000, 2000)]
        [InlineData(1, 333, 400)]
        public void EachPolyglot_Adds20Percent(int polyglots, int vanilla, int expected)
        {
            TestGame.Hire(TestGame.Idol());
            for (int i = 0; i < polyglots; i++)
                TestGame.Hire(TestGame.Idol(NewTraits.Polyglot));

            Assert.Equal(expected, NewFans(vanilla));
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.hiatus)]
        [InlineData(data_girls._status.graduated)]
        [InlineData(data_girls._status.announced_graduation)]
        public void InactivePolyglot_NotCounted(data_girls._status status)
        {
            TestGame.Hire(TestGame.Idol(NewTraits.Polyglot)).status = status;
            Assert.Equal(1000, NewFans(1000));
        }
    }

    /// <summary>
    /// Aerophobic idols lose 30 mental stamina each time the agency goes on a world tour.
    /// </summary>
    public class AerophobiaTests
    {
        public AerophobiaTests() => TestGame.Reset();

        [Fact]
        public void ActiveAerophobicIdols_LoseMentalStamina()
        {
            data_girls.girls first = TestGame.Hire(TestGame.Idol(NewTraits.Aerophobia));
            data_girls.girls second = TestGame.Hire(TestGame.Idol(NewTraits.Aerophobia));
            TestGame.Hire(TestGame.Idol());
            TestGame.Hire(TestGame.Idol(NewTraits.Polyglot));

            SEvent_Tour_UseStamina.Postfix();

            Assert.Equal(new[] { (first, mentalStamina, -30f), (second, mentalStamina, -30f) }, Seams.ParamsAdded);
        }

        [Fact]
        public void InactiveAerophobicIdol_Spared()
        {
            TestGame.Hire(TestGame.Idol(NewTraits.Aerophobia)).status = data_girls._status.hiatus;
            SEvent_Tour_UseStamina.Postfix();
            Assert.Empty(Seams.ParamsAdded);
        }
    }

    /// <summary>
    /// Stage Fright idols lose 10 mental stamina for each song they center and each MC they take
    /// part in, with a notification each time.
    /// </summary>
    public class StageFrightTests
    {
        public StageFrightTests() => TestGame.Reset();

        [Fact]
        public void EachCenterAndMC_Costs10()
        {
            data_girls.girls nervous = TestGame.Idol(NewTraits.Stage_Fright, name: "Nervous");
            data_girls.girls calm = TestGame.Idol(name: "Calm");
            SEvent_Concerts._concert concert = new();
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = nervous });
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = calm });
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = nervous });
            SEvent_Concerts._concert._mc mc = new();
            mc.Girls[1] = nervous;
            mc.Girls[2] = calm;
            concert.SetListItems.Add(mc);

            SEvent_Concerts__concert_Finish.Postfix(concert);

            Assert.Equal(3, Seams.ParamsAdded.Count);
            Assert.All(Seams.ParamsAdded, p => Assert.Equal((nervous, mentalStamina, -10f), p));
            Assert.Equal(Enumerable.Repeat("Nervous lost 10 mental stamina due to stage fright.", 3), Seams.Notifications);
        }

        [Fact]
        public void EmptySlotsAndOtherIdols_Ignored()
        {
            SEvent_Concerts._concert concert = new();
            concert.SetListItems.Add(new SEvent_Concerts._concert._song());
            concert.SetListItems.Add(new SEvent_Concerts._concert._mc());
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = TestGame.Idol(NewTraits.Aerophobia) });

            SEvent_Concerts__concert_Finish.Postfix(concert);

            Assert.Empty(Seams.ParamsAdded);
            Assert.Empty(Seams.Notifications);
        }
    }

    /// <summary>
    /// In elections, Cult Leader idols' fans count half again.
    /// </summary>
    public class CultLeaderTests
    {
        public CultLeaderTests() => TestGame.Reset(patched: true);

        private static data_girls.girls WithFans(NewTraits trait)
        {
            data_girls.girls girl = TestGame.Idol(trait);
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.hardcore, age = resources.fanType.teen, people = 1000 });
            girl.Fans.Add(new resources._fan { gender = resources.fanType.female, hardcoreness = resources.fanType.casual, age = resources.fanType.adult, people = 333 });
            return girl;
        }

        [Fact]
        public void DuringElection_FansCountHalfAgain()
        {
            data_girls.girls girl = WithFans(NewTraits.Cult_Leader);

            SEvent_SSK__SSK_GenerateResults.Prefix();
            Assert.Equal(1500L, girl.GetFan_Count(resources.fanType.hardcore));
            Assert.Equal(500L, girl.GetFan_Count(resources.fanType.casual));
            Assert.Equal(1500L, girl.GetFan_Count(resources.fanType.male));
            SEvent_SSK__SSK_GenerateResults.Finalizer(null);

            Assert.False(patchGetFan_Count);
        }

        [Fact]
        public void OutsideElection_Unchanged()
        {
            data_girls.girls girl = WithFans(NewTraits.Cult_Leader);
            Assert.Equal(1000L, girl.GetFan_Count(resources.fanType.hardcore));
            Assert.Equal(1333L, girl.GetFan_Count(resources.fanType.teen) + girl.GetFan_Count(resources.fanType.adult));
        }

        [Fact]
        public void OtherIdols_Unchanged()
        {
            data_girls.girls girl = WithFans(NewTraits.none);

            SEvent_SSK__SSK_GenerateResults.Prefix();
            Assert.Equal(1000L, girl.GetFan_Count(resources.fanType.hardcore));
            SEvent_SSK__SSK_GenerateResults.Finalizer(null);
        }

        /// <summary>
        /// The election's Finalizer switches the bonus off whether or not the election threw.
        /// </summary>
        [Fact]
        public void ElectionException_StillSwitchesOff()
        {
            SEvent_SSK__SSK_GenerateResults.Prefix();
            Exception thrown = new InvalidOperationException();
            Assert.Same(thrown, SEvent_SSK__SSK_GenerateResults.Finalizer(thrown));
            Assert.False(patchGetFan_Count);
            Assert.Equal(1000L, WithFans(NewTraits.Cult_Leader).GetFan_Count(resources.fanType.hardcore));
        }

        /// <summary>
        /// Only the single-type count is changed; the election reads hardcore and casual counts.
        /// </summary>
        [Fact]
        public void ThreeTypeCount_Unchanged()
        {
            data_girls.girls girl = WithFans(NewTraits.Cult_Leader);

            SEvent_SSK__SSK_GenerateResults.Prefix();
            Assert.Equal(1000L, girl.GetFan_Count(resources.fanType.male, resources.fanType.hardcore, resources.fanType.teen));
            SEvent_SSK__SSK_GenerateResults.Finalizer(null);
        }
    }
}
