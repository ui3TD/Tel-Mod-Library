using System;
using System.Collections.Generic;
using TraitFix;
using Xunit;
using static data_girls._paramType;
using static traits._trait._type;
using static TraitFix.TraitsFix;

namespace TraitsFixTests
{
    /// <summary>
    /// Anxiety, Clumsy, Worrier and Complacent change stats in their situations, and leave every
    /// non-stat parameter alone.
    /// </summary>
    public class StatModifierTests
    {
        public StatModifierTests() => TestGame.Reset();

        private static readonly data_girls._paramType[] Stats = { cute, cool, sexy, pretty, vocal, dance, funny, smart };

        [Fact]
        public void Clumsy_LosesDance_GainsFunny()
        {
            data_girls.girls girl = TestGame.Idol(Clumsy);
            Assert.Equal(CLUMSY_DANCE_MODIFIER, GetTraitModifier(girl, dance));
            Assert.Equal(CLUMSY_FUNNY_MODIFIER, GetTraitModifier(girl, funny));
            Assert.Equal(-30, CLUMSY_DANCE_MODIFIER);
            Assert.Equal(30, CLUMSY_FUNNY_MODIFIER);
            foreach (data_girls._paramType stat in new[] { cute, cool, sexy, pretty, vocal, smart })
                Assert.Equal(0, GetTraitModifier(girl, stat));
        }

        [Fact]
        public void Anxiety_NoEventWaiting_Unchanged()
        {
            Assert.Equal(0, GetTraitModifier(TestGame.Idol(Anxiety), vocal));
        }

        [Fact]
        public void Anxiety_TourWaiting_AllStatsDown10()
        {
            SEvent_Tour.Tours.Add(new SEvent_Tour.tour { Status = SEvent_Tour.tour._status.normal });
            data_girls.girls girl = TestGame.Idol(Anxiety);
            foreach (data_girls._paramType stat in Stats)
                Assert.Equal(-10, GetTraitModifier(girl, stat));
        }

        [Fact]
        public void Anxiety_ElectionOrConcertWaiting_Down10()
        {
            SEvent_SSK.Elections.Add(new SEvent_SSK._SSK { Status = SEvent_Tour.tour._status.working });
            Assert.Equal(ANXIETY_MODIFIER, GetTraitModifier(TestGame.Idol(Anxiety), vocal));

            TestGame.Reset();
            SEvent_Concerts.Concerts.Add(new SEvent_Concerts._concert { Status = SEvent_Tour.tour._status.normal });
            Assert.Equal(ANXIETY_MODIFIER, GetTraitModifier(TestGame.Idol(Anxiety), vocal));
        }

        [Fact]
        public void Anxiety_FinishedEvents_Unchanged()
        {
            SEvent_Tour.Tours.Add(new SEvent_Tour.tour { Status = SEvent_Tour.tour._status.finished });
            SEvent_SSK.Elections.Add(new SEvent_SSK._SSK { Status = SEvent_Tour.tour._status.finished });
            SEvent_Concerts.Concerts.Add(new SEvent_Concerts._concert { Status = SEvent_Tour.tour._status.finished });
            Assert.Equal(0, GetTraitModifier(TestGame.Idol(Anxiety), vocal));
        }

        [Fact]
        public void Worrier_ScandalPoints_AllStatsDown20()
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(Worrier));
            Assert.Equal(0, GetTraitModifier(girl, vocal));

            TestGame.SetStat(TestGame.Hire(TestGame.Idol()), scandalPoints, 1f);
            foreach (data_girls._paramType stat in Stats)
                Assert.Equal(WORRIER_MODIFIER, GetTraitModifier(girl, stat));
            Assert.Equal(-20, WORRIER_MODIFIER);
        }

        [Fact]
        public void Complacent_CenterOfLatestSingle_LosesVocalAndDance()
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(Complacent));
            Assert.Equal(0, GetTraitModifier(girl, vocal));

            TestGame.ReleaseSingle(TestGame.Today.AddMonths(-2), girls: girl);
            Assert.Equal(COMPLACENT_MODIFIER, GetTraitModifier(girl, vocal));
            Assert.Equal(COMPLACENT_MODIFIER, GetTraitModifier(girl, dance));
            Assert.Equal(0, GetTraitModifier(girl, cute));
            Assert.Equal(-20, COMPLACENT_MODIFIER);
        }

        [Fact]
        public void Complacent_NotCenter_Unchanged()
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(Complacent));
            TestGame.ReleaseSingle(TestGame.Today.AddMonths(-2), girls: new[] { TestGame.Hire(TestGame.Idol()), girl });
            Assert.Equal(0, GetTraitModifier(girl, vocal));
        }

        /// <summary>
        /// Being center of her sub-group's latest single counts too.
        /// </summary>
        [Fact]
        public void IsCenter_ChecksMainAndOwnGroup()
        {
            data_girls.girls girl = TestGame.Idol(Complacent);
            Groups._group subGroup = new() { ID = 1, Girls = new List<data_girls.girls> { girl } };
            Groups.Groups_.Add(subGroup);
            singles._single single = new() { status = singles._single._status.released };
            single.girls.Add(girl);
            singles.Singles.Add(single);
            subGroup.Singles.Add(single);

            Assert.True(IsCenter(girl));
            Assert.False(IsCenter(TestGame.Idol()));
            Assert.False(IsCenter(null));
        }

        [Fact]
        public void NonStatParameters_Unchanged()
        {
            TestGame.SetStat(TestGame.Hire(TestGame.Idol()), scandalPoints, 1f);
            SEvent_Tour.Tours.Add(new SEvent_Tour.tour());
            foreach (traits._trait._type trait in new[] { Anxiety, Clumsy, Worrier, Complacent, Lone_Wolf, Defeatist, Underdog })
            {
                data_girls.girls girl = TestGame.Idol(trait);
                foreach (data_girls._paramType type in new[] { physicalStamina, mentalStamina, famePoints, teamChemistry, scandalPoints })
                    Assert.Equal(0, GetTraitModifier(girl, type, new List<data_girls.girls> { girl }));
            }
        }

        [Fact]
        public void OtherTraitsAndNoIdol_Unchanged()
        {
            Assert.Equal(0, GetTraitModifier(TestGame.Idol(), vocal));
            Assert.Equal(0, GetTraitModifier(TestGame.Idol(Photogenic), vocal));
            Assert.Equal(0, GetTraitModifier(null, vocal));
        }
    }

    /// <summary>
    /// Lone Wolf gets +40 to all stats when she's the only one assigned to a show. Sick idols don't count
    /// as assigned; idols who have announced their graduation still do.
    /// </summary>
    public class LoneWolfTests
    {
        public LoneWolfTests() => TestGame.Reset();

        [Fact]
        public void Alone_Plus40()
        {
            data_girls.girls wolf = TestGame.Idol(Lone_Wolf);
            Assert.Equal(LONEWOLF_MODIFIER, GetTraitModifier(wolf, vocal, new List<data_girls.girls> { wolf }));
            Assert.Equal(40, LONEWOLF_MODIFIER);
        }

        [Fact]
        public void WithAnotherIdol_Unchanged()
        {
            data_girls.girls wolf = TestGame.Idol(Lone_Wolf);
            Assert.Equal(0, GetTraitModifier(wolf, vocal, new List<data_girls.girls> { wolf, TestGame.Idol() }));
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.depressed)]
        public void SickCoStar_NotCounted(data_girls._status status)
        {
            data_girls.girls wolf = TestGame.Idol(Lone_Wolf);
            data_girls.girls other = TestGame.Idol();
            other.status = status;
            Assert.Equal(LONEWOLF_MODIFIER, GetTraitModifier(wolf, vocal, new List<data_girls.girls> { wolf, other, null }));
        }

        [Fact]
        public void CoStarAnnouncedGraduation_StillCounted()
        {
            data_girls.girls wolf = TestGame.Idol(Lone_Wolf);
            data_girls.girls other = TestGame.Idol();
            other.status = data_girls._status.announced_graduation;
            Assert.Equal(0, GetTraitModifier(wolf, vocal, new List<data_girls.girls> { wolf, other }));
        }

        /// <summary>
        /// Outside a show's cast (business, singles, concerts) there's no cast to be alone in.
        /// </summary>
        [Fact]
        public void NoCast_Unchanged()
        {
            Assert.Equal(0, GetTraitModifier(TestGame.Idol(Lone_Wolf), vocal));
        }
    }

    /// <summary>
    /// Defeatist (-20) and Underdog (+20) apply to all stats while the latest single, by the idol's group
    /// or the main group, didn't reach #1. The single charts the month after its release.
    /// </summary>
    public class ChartTraitTests
    {
        public ChartTraitTests() => TestGame.Reset();

        private static readonly DateTime LastMonth = TestGame.Today.AddMonths(-1);

        private static int Modifier(traits._trait._type trait)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(trait));
            return GetTraitModifier(girl, vocal);
        }

        [Fact]
        public void NoSingle_Unchanged()
        {
            Assert.Equal(0, Modifier(Defeatist));
            Assert.Equal(0, Modifier(Underdog));
        }

        [Theory]
        [InlineData(Defeatist, -20)]
        [InlineData(Underdog, 20)]
        public void MissedNumberOne_Applies(traits._trait._type trait, int expected)
        {
            TestGame.ReleaseSingle(LastMonth, chartPosition: 2);
            Assert.Equal(expected, Modifier(trait));
        }

        [Fact]
        public void NumberOne_Unchanged()
        {
            TestGame.ReleaseSingle(LastMonth, chartPosition: 1);
            Assert.Equal(0, Modifier(Defeatist));
            Assert.Equal(0, Modifier(Underdog));
        }

        /// <summary>
        /// A single released this month hasn't charted yet.
        /// </summary>
        [Fact]
        public void ReleasedThisMonth_Unchanged()
        {
            TestGame.ReleaseSingle(new DateTime(TestGame.Today.Year, TestGame.Today.Month, 1), chartPosition: 0);
            Assert.Equal(0, Modifier(Defeatist));
        }

        /// <summary>
        /// The chart popup didn't record the position (e.g. it was skipped in the tutorial), but this
        /// month's chart lists the single at #1.
        /// </summary>
        [Fact]
        public void PositionNotRecorded_NumberOneOnChart_Unchanged()
        {
            TestGame.ReleaseSingle(LastMonth, chartPosition: 0, sales: 5000);
            TestGame.AddChart(LastMonth, 9000, 8000);
            TestGame.AddChart(TestGame.Today, 3000, 2000);
            Assert.Equal(0, Modifier(Defeatist));
            Assert.Equal(0, Modifier(Underdog));
        }

        /// <summary>
        /// The popup never records the last-ranked single's position; the chart still lists it.
        /// </summary>
        [Theory]
        [InlineData(Defeatist, -20)]
        [InlineData(Underdog, 20)]
        public void PositionNotRecorded_LastOnChart_Applies(traits._trait._type trait, int expected)
        {
            TestGame.ReleaseSingle(LastMonth, chartPosition: 0, sales: 1000);
            TestGame.AddChart(LastMonth, 500);
            TestGame.AddChart(TestGame.Today, 3000, 2000);
            Assert.Equal(expected, Modifier(trait));
        }

        [Fact]
        public void PositionNotRecorded_NoChart_CountsAsMissed()
        {
            TestGame.ReleaseSingle(LastMonth.AddMonths(-3), chartPosition: 0);
            Assert.Equal(DEFEATIST_MODIFIER, Modifier(Defeatist));
        }

        /// <summary>
        /// The more recent of the main group's and her own group's latest singles counts.
        /// </summary>
        [Fact]
        public void OwnGroupSingle_MoreRecent_Counts()
        {
            TestGame.ReleaseSingle(LastMonth.AddMonths(-1), chartPosition: 1);
            data_girls.girls girl = TestGame.Idol(Underdog);
            Groups._group subGroup = new() { ID = 1, Girls = new List<data_girls.girls> { girl } };
            Groups.Groups_.Add(subGroup);
            singles._single groupSingle = new() { status = singles._single._status.released };
            groupSingle.ReleaseData.ReleaseDate = LastMonth;
            groupSingle.ReleaseData.Chart_Position = 4;
            singles.Singles.Add(groupSingle);
            subGroup.Singles.Add(groupSingle);

            Assert.Equal(UNDERDOG_MODIFIER, GetTraitModifier(girl, vocal));
        }

        [Fact]
        public void AllStats_Change()
        {
            TestGame.ReleaseSingle(LastMonth, chartPosition: 3);
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(Defeatist));
            foreach (data_girls._paramType stat in new[] { cute, cool, sexy, pretty, vocal, dance, funny, smart })
                Assert.Equal(-20, GetTraitModifier(girl, stat));
        }
    }

    /// <summary>
    /// The stat modifiers apply inside each of the game's scoring calculations, and nowhere else.
    /// </summary>
    public class ScoringContextTests
    {
        public ScoringContextTests() => TestGame.Reset(patched: true);

        [Fact]
        public void StatsReadNormallyOutsideScoring()
        {
            data_girls.girls girl = TestGame.Idol(Clumsy);
            Assert.Equal(40f, TestGame.Stat(girl, dance));
            Assert.False(IsTraitCalculationActive);
        }

        [Fact]
        public void GetVal_AddsModifierOnlyWhileScoring()
        {
            data_girls.girls.param param = TestGame.Idol(Clumsy).getParam(dance);

            float result = 40f;
            data_girls_girls_param_GetVal.Postfix(ref result, param);
            Assert.Equal(40f, result);

            BeginTraitCalculation();
            result = 40f;
            data_girls_girls_param_GetVal.Postfix(ref result, param);
            Assert.Equal(10f, result);
            EndTraitCalculation();
        }

        /// <summary>
        /// Show averages pass the cast, so Lone Wolf counts: alone she scores 80.
        /// </summary>
        [Fact]
        public void AverageParam_IncludesModifiersAndCast()
        {
            data_girls.girls wolf = TestGame.Idol(Lone_Wolf);
            Assert.Equal(80f, data_girls.GetAverageParam(vocal, new List<data_girls.girls> { wolf }));
            Assert.Equal(40f, data_girls.GetAverageParam(vocal, new List<data_girls.girls> { wolf, TestGame.Idol() }));
            Assert.False(IsTraitCalculationActive);
            Assert.Equal(40f, TestGame.Stat(wolf, vocal));
        }

        [Fact]
        public void ShowSenbatsu_IncludesModifiersAndCast()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol(Lone_Wolf) };
            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                TestGame.Component<Shows._show>(), typeof(Shows._show), "SenbatsuCalcParam", girls, vocal);

            Assert.Equal(80f, result._val, 3);
            Assert.False(IsTraitCalculationActive);
        }

        /// <summary>
        /// StatLimits caps show senbatsu scores at 100 after the modifier.
        /// </summary>
        [Fact]
        public void ShowSenbatsu_CappedAt100()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol(Lone_Wolf, statValue: 80f) };
            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                TestGame.Component<Shows._show>(), typeof(Shows._show), "SenbatsuCalcParam", girls, vocal);
            Assert.Equal(100f, result._val);
        }

        [Theory]
        [InlineData(dance, 10f)]
        [InlineData(funny, 70f)]
        [InlineData(vocal, 40f)]
        public void SingleSenbatsu_IncludesModifiers(data_girls._paramType stat, float expected)
        {
            data_girls.girls center = TestGame.Idol(Clumsy);
            List<data_girls.girls> girls = new(new data_girls.girls[15]) { [0] = center };
            // One idol in a sub-group: one row, so the score is the center's stat
            Groups._group group = new() { ID = 1, Girls = new List<data_girls.girls> { center } };

            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                new singles._single(), typeof(singles._single), "SenbatsuCalcParam", girls, stat, group);

            Assert.Equal(expected, result._val, 3);
            Assert.False(IsTraitCalculationActive);
        }

        /// <summary>
        /// A concert song scores the center's average of dance and vocal: a Clumsy center has (10 + 40) / 2.
        /// </summary>
        [Fact]
        public void ConcertSong_IncludesModifiers()
        {
            singles._single single = new();
            single.girls.Add(TestGame.Idol());
            SEvent_Concerts._concert._song song = new() { Single = single, Center = TestGame.Idol(Clumsy) };

            Assert.Equal(25, song.GetSkillValue());
            Assert.False(IsTraitCalculationActive);
        }

        /// <summary>
        /// A concert MC scores the sum of each MC's average of smart and funny: a Clumsy MC has (40 + 70) / 2.
        /// </summary>
        [Fact]
        public void ConcertMC_IncludesModifiers()
        {
            SEvent_Concerts._concert._mc mc = new();
            mc.Girls[0] = TestGame.Idol(Clumsy);
            mc.Girls[1] = TestGame.Idol();

            Assert.Equal(95, mc.GetSkillValue());
            Assert.False(IsTraitCalculationActive);
        }

        /// <summary>
        /// Business pay reads the proposal's skill: a Clumsy idol pays like a plain idol with 10 dance.
        /// </summary>
        [Fact]
        public void BusinessCoeff_IncludesModifiers()
        {
            business._proposal proposal = new() { type = business._type.tv_drama, skill = dance };
            Assert.Equal(proposal.GetGirlCoeff(TestGame.Idol(statValue: 10f)), proposal.GetGirlCoeff(TestGame.Idol(Clumsy)), 3);
            Assert.False(IsTraitCalculationActive);
        }

        [Fact]
        public void Photogenic_PhotoshootsPlus100Percent()
        {
            business._proposal photoshoot = new() { type = business._type.photoshoot, skill = pretty };
            float plain = photoshoot.GetGirlCoeff(TestGame.Idol());
            Assert.Equal(plain + PHOTOGENIC_MODIFIER, photoshoot.GetGirlCoeff(TestGame.Idol(Photogenic)), 3);
            Assert.Equal(1f, PHOTOGENIC_MODIFIER);

            business._proposal drama = new() { type = business._type.tv_drama, skill = pretty };
            Assert.Equal(drama.GetGirlCoeff(TestGame.Idol()), drama.GetGirlCoeff(TestGame.Idol(Photogenic)), 3);
        }

        /// <summary>
        /// If a scoring calculation throws, the modifiers still switch off.
        /// </summary>
        [Fact]
        public void Exception_StillSwitchesModifiersOff()
        {
            business._proposal proposal = new() { type = business._type.photoshoot, skill = smart };
            Assert.Throws<NullReferenceException>(() => proposal.GetGirlCoeff(null));
            Assert.False(IsTraitCalculationActive);

            Assert.Throws<NullReferenceException>(() => data_girls.GetAverageParam(vocal, null));
            Assert.False(IsTraitCalculationActive);
            Assert.Null(CurrentTraitCast);

            Assert.Equal(40f, TestGame.Stat(TestGame.Idol(Clumsy), dance));
        }

        /// <summary>
        /// A calculation inside another keeps the outer one's cast once it ends.
        /// </summary>
        [Fact]
        public void NestedCalculations_RestoreOuterCast()
        {
            List<data_girls.girls> cast = new() { TestGame.Idol() };
            BeginTraitCalculation(cast);
            BeginTraitCalculation();
            Assert.Null(CurrentTraitCast);
            EndTraitCalculation();
            Assert.Same(cast, CurrentTraitCast);
            EndTraitCalculation();
            Assert.False(IsTraitCalculationActive);

            EndTraitCalculation();
            Assert.False(IsTraitCalculationActive);
        }

        [Fact]
        public void EveryScoringPatch_SwitchesOffAfterwards()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol() };

            Data_girls_GetAverageParam.Prefix(girls);
            Assert.Same(girls, CurrentTraitCast);
            Data_girls_GetAverageParam.Finalizer(null);
            Assert.False(IsTraitCalculationActive);

            Shows__show_SenbatsuCalcParam.Prefix(girls);
            Assert.Same(girls, CurrentTraitCast);
            Shows__show_SenbatsuCalcParam.Finalizer(null);
            Assert.False(IsTraitCalculationActive);

            Action[] prefixes =
            {
                Business__proposal_GetGirlCoeff.Prefix,
                Singles__single_SenbatsuCalcParam.Prefix,
                SEvent_Concerts__concert__song_GetSkillValue.Prefix,
                SEvent_Concerts__concert__mc_GetSkillValue.Prefix,
            };
            Func<Exception, Exception>[] finalizers =
            {
                Business__proposal_GetGirlCoeff.Finalizer,
                Singles__single_SenbatsuCalcParam.Finalizer,
                SEvent_Concerts__concert__song_GetSkillValue.Finalizer,
                SEvent_Concerts__concert__mc_GetSkillValue.Finalizer,
            };
            for (int i = 0; i < prefixes.Length; i++)
            {
                prefixes[i]();
                Assert.True(IsTraitCalculationActive);
                Assert.Null(CurrentTraitCast);
                Exception thrown = new InvalidOperationException();
                Assert.Same(thrown, finalizers[i](thrown));
                Assert.False(IsTraitCalculationActive);
            }
        }
    }
}
