using System.Collections.Generic;
using System.Linq;
using TraitFix;
using Xunit;
using static data_girls._paramType;
using static traits._trait._type;
using static TraitFix.TraitsFix;
using Result = Single_Marketing_Roll._result;

namespace TraitsFixTests
{
    /// <summary>
    /// A Meme Queen who isn't sick gives an internet show's cast +10 to every stat.
    /// </summary>
    public class MemeQueenShowTests
    {
        public MemeQueenShowTests() => TestGame.Reset(patched: true);

        private static Shows._param Medium(Shows._param._media_type type) => new() { media_type = type };

        /// <summary>
        /// Runs the game's cast stat calculation for a show and returns the stat it added.
        /// </summary>
        private static float CastStat(Shows._param._media_type medium, data_girls._paramType type, params data_girls.girls[] cast)
        {
            Shows._show show = new() { medium = Medium(medium) };
            TestGame.CallPrivate(show, typeof(Shows._show), "AddCastParam", type, cast.ToList());
            return show.girlParams.Last().val;
        }

        [Fact]
        public void InternetShow_Plus10()
        {
            Assert.Equal(50f, CastStat(Shows._param._media_type.internet, cute, TestGame.Idol(Meme_queen), TestGame.Idol()));
            Assert.Equal(10f, MEME_INT_SHOW);
        }

        [Fact]
        public void OtherMedia_Unchanged()
        {
            Assert.Equal(40f, CastStat(Shows._param._media_type.tv, cute, TestGame.Idol(Meme_queen)));
        }

        [Fact]
        public void TwoMemeQueens_DontStack()
        {
            Assert.Equal(50f, CastStat(Shows._param._media_type.internet, cute, TestGame.Idol(Meme_queen), TestGame.Idol(Meme_queen)));
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.depressed)]
        public void SickMemeQueen_Unchanged(data_girls._status status)
        {
            data_girls.girls queen = TestGame.Idol(Meme_queen);
            data_girls.girls plain = TestGame.Idol();
            queen.status = status;
            plain.status = status;
            Assert.Equal(CastStat(Shows._param._media_type.internet, cute, plain, TestGame.Idol()),
                CastStat(Shows._param._media_type.internet, cute, queen, TestGame.Idol()));
        }

        [Fact]
        public void MemeQueenAnnouncedGraduation_StillCounts()
        {
            data_girls.girls queen = TestGame.Idol(Meme_queen);
            queen.status = data_girls._status.announced_graduation;
            Assert.Equal(50f, CastStat(Shows._param._media_type.internet, cute, queen, TestGame.Idol()));
        }

        [Fact]
        public void NoMemeQueen_Unchanged()
        {
            Assert.Equal(40f, CastStat(Shows._param._media_type.internet, cute, TestGame.Idol()));
        }

        [Fact]
        public void TeamChemistry_Unchanged()
        {
            Shows._show show = new() { medium = Medium(Shows._param._media_type.internet) };
            show.girlParams.Add(new data_girls.girls.param { type = teamChemistry, _val = 40f });
            Shows__show_AddCastParam.Postfix(teamChemistry, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, show);
            Assert.Equal(40f, show.girlParams[0].val);
        }

        /// <summary>
        /// Only the stat just added gets the bonus.
        /// </summary>
        [Fact]
        public void OnlyTheStatJustAdded()
        {
            Shows._show show = new() { medium = Medium(Shows._param._media_type.internet) };
            show.girlParams.Add(new data_girls.girls.param { type = cool, _val = 40f });
            Shows__show_AddCastParam.Postfix(cute, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, show);
            Assert.Equal(40f, show.girlParams[0].val);
        }

        [Fact]
        public void ShowPopup_Plus10()
        {
            List<data_girls.girls.param> girlParams = new() { new data_girls.girls.param { type = cool, _val = 40f } };
            Show_Popup_AddCastParam.Postfix(cool, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, girlParams, Medium(Shows._param._media_type.internet));
            Assert.Equal(50f, girlParams[0].val);

            Show_Popup_AddCastParam.Postfix(cool, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, girlParams, Medium(Shows._param._media_type.tv));
            Show_Popup_AddCastParam.Postfix(cool, new List<data_girls.girls> { TestGame.Idol() }, girlParams, Medium(Shows._param._media_type.internet));
            Show_Popup_AddCastParam.Postfix(teamChemistry, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, girlParams, Medium(Shows._param._media_type.internet));
            Show_Popup_AddCastParam.Postfix(cool, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, new List<data_girls.girls.param>(), Medium(Shows._param._media_type.internet));
            Show_Popup_AddCastParam.Postfix(cool, new List<data_girls.girls> { TestGame.Idol(Meme_queen) }, girlParams, null);
            Assert.Equal(50f, girlParams[0].val);
        }

        /// <summary>
        /// The show popup recalculates its cast stats when the medium changes, so the bonus appears.
        /// </summary>
        [Fact]
        public void ShowPopup_MediumChange_Recalculates()
        {
            Show_Popup popup = TestGame.LiveComponent<Show_Popup>();
            Show_Popup_SetParam.Postfix(popup, Show_Popup_Param_Button._type.medium, Shows._show._castType.permanentCast);
            Show_Popup_SetParam.Postfix(popup, Show_Popup_Param_Button._type.genre, Shows._show._castType.permanentCast);
            Show_Popup_SetParam.Postfix(popup, Show_Popup_Param_Button._type.mc, Shows._show._castType.permanentCast);
            Show_Popup_SetParam.Postfix(popup, Show_Popup_Param_Button._type.medium, null);
            Show_Popup_SetParam.Postfix(null, Show_Popup_Param_Button._type.medium, Shows._show._castType.entireGroup);

            Assert.Equal(new[] { Shows._show._castType.permanentCast }, Seams.CastTypesSet);
        }
    }

    /// <summary>
    /// A Meme Queen in a single who isn't sick adds 10 to a viral campaign's success chance and 5 to its
    /// critical success chance. The game works out the fail chance from the others, so it falls by 15.
    /// </summary>
    public class MemeQueenViralTests
    {
        public MemeQueenViralTests() => TestGame.Reset(patched: true);

        private static readonly singles._param Viral = new() { type = singles._param._type.marketing, id = 4 };
        private static readonly singles._param Ad = new() { type = singles._param._type.marketing, id = 3 };

        private static singles._single Single(params data_girls.girls[] girls)
        {
            singles._single single = new();
            single.girls.AddRange(girls);
            return single;
        }

        private static float[] Chances(singles._param campaign, singles._single single) => new[]
        {
            campaign.GetSuccessChance(Result.success, 1, single),
            campaign.GetSuccessChance(Result.success_crit, 1, single),
            campaign.GetSuccessChance(Result.fail, 1, single),
            campaign.GetSuccessChance(Result.fail_crit, 1, single),
        };

        [Fact]
        public void NoMemeQueen_Vanilla()
        {
            Assert.Equal(new[] { 11f, 10f, 69f, 10f }, Chances(Viral, Single(TestGame.Idol())));
        }

        [Fact]
        public void MemeQueen_SuccessUp10_CritUp5_FailDown15()
        {
            float[] chances = Chances(Viral, Single(TestGame.Idol(), TestGame.Idol(Meme_queen)));
            Assert.Equal(new[] { 21f, 15f, 54f, 10f }, chances);
            Assert.Equal(100f, chances.Sum());
        }

        [Fact]
        public void TwoMemeQueens_DontStack()
        {
            Assert.Equal(new[] { 21f, 15f, 54f, 10f }, Chances(Viral, Single(TestGame.Idol(Meme_queen), TestGame.Idol(Meme_queen))));
        }

        [Fact]
        public void SickMemeQueen_Vanilla()
        {
            data_girls.girls queen = TestGame.Idol(Meme_queen);
            queen.status = data_girls._status.injured;
            Assert.Equal(new[] { 11f, 10f, 69f, 10f }, Chances(Viral, Single(queen)));
        }

        [Fact]
        public void OtherCampaigns_Vanilla()
        {
            Assert.Equal(Chances(Ad, Single(TestGame.Idol())), Chances(Ad, Single(TestGame.Idol(Meme_queen))));
        }

        [Fact]
        public void NoSingle_Vanilla()
        {
            Assert.Equal(11f, Viral.GetSuccessChance(Result.success, 1, null));
        }
    }

    /// <summary>
    /// An Annoying idol makes the rest of a show's cast spend 1.2x the physical stamina. With two or more
    /// Annoying idols, they annoy each other too.
    /// </summary>
    public class AnnoyingTests
    {
        public AnnoyingTests() => TestGame.Reset();

        /// <summary>
        /// A show costing 30 stamina, split across its cast.
        /// </summary>
        private static Shows._show Show(params data_girls.girls[] cast)
        {
            Shows._show show = new() { medium = new Shows._param { cost_stamina = 30 }, castType = Shows._show._castType.permanentCast };
            for (int i = 0; i < cast.Length; i++)
                show.girls[i] = cast[i];
            return show;
        }

        [Fact]
        public void OthersSpendExtra20Percent()
        {
            data_girls.girls annoying = TestGame.Idol(Annoying);
            data_girls.girls other = TestGame.Idol();

            Shows__show_SetStamina.Postfix(Show(annoying, other));

            Assert.Equal(new[] { (other, physicalStamina, -3f) }, Seams.ParamsAdded);
            Assert.Equal(0.2f, ANNOYING_MODIFIER);
        }

        [Fact]
        public void TwoAnnoying_EveryoneSpendsExtra()
        {
            data_girls.girls first = TestGame.Idol(Annoying);
            data_girls.girls second = TestGame.Idol(Annoying);
            data_girls.girls other = TestGame.Idol();

            Shows__show_SetStamina.Postfix(Show(first, second, other));

            Assert.Equal(new[] { (first, physicalStamina, -2f), (second, physicalStamina, -2f), (other, physicalStamina, -2f) }, Seams.ParamsAdded);
        }

        [Fact]
        public void InactiveAnnoying_NoEffect()
        {
            data_girls.girls annoying = TestGame.Idol(Annoying);
            annoying.status = data_girls._status.hiatus;
            Shows__show_SetStamina.Postfix(Show(annoying, TestGame.Idol()));
            Assert.Empty(Seams.ParamsAdded);
        }

        [Fact]
        public void InactiveOther_Spared()
        {
            data_girls.girls other = TestGame.Idol();
            other.status = data_girls._status.injured;
            Shows__show_SetStamina.Postfix(Show(TestGame.Idol(Annoying), other));
            Assert.Empty(Seams.ParamsAdded);
        }

        [Fact]
        public void NoAnnoying_OrNoShow_NoEffect()
        {
            Shows__show_SetStamina.Postfix(Show(TestGame.Idol(), TestGame.Idol()));
            Shows__show_SetStamina.Postfix(null);
            Assert.Empty(Seams.ParamsAdded);
        }
    }

    /// <summary>
    /// A Misandry idol in a single with handshakes has a 20% chance of losing appeal with male fans.
    /// </summary>
    public class MisandryTests
    {
        public MisandryTests() => TestGame.Reset();

        private static data_girls.girls WithFans(traits._trait._type trait)
        {
            data_girls.girls girl = TestGame.Idol(trait);
            girl.Fans.Add(new resources._fan { gender = resources.fanType.male, hardcoreness = resources.fanType.casual, age = resources.fanType.teen });
            girl.Fans.Add(new resources._fan { gender = resources.fanType.female, hardcoreness = resources.fanType.casual, age = resources.fanType.teen });
            return girl;
        }

        private static singles._single Single(bool individual, bool group, params data_girls.girls[] girls)
        {
            singles._single single = new();
            single.marketing.Add(new singles._param { Individual_Handshake = individual, Group_Handshake = group });
            single.girls.AddRange(girls);
            return single;
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Handshakes_MaleFansSour(bool individual, bool group)
        {
            data_girls.girls girl = WithFans(Misandry);
            Seams.Chance = _ => true;

            Singles_ReleaseSingle.Postfix(Single(individual, group, girl, TestGame.Idol()));

            Assert.Equal(new float[] { 20 }, Seams.ChancesRolled);
            Assert.Equal(new[] { -1 }, girl.Fans[0].Vals);
            Assert.Empty(girl.Fans[1].Vals);
            Assert.Equal(-1f, MISANDRY_MODIFIER);
        }

        [Fact]
        public void LostRoll_Unchanged()
        {
            data_girls.girls girl = WithFans(Misandry);
            Singles_ReleaseSingle.Postfix(Single(true, false, girl));
            Assert.Single(Seams.ChancesRolled);
            Assert.Empty(girl.Fans[0].Vals);
        }

        [Fact]
        public void NoHandshakes_NoRoll()
        {
            Seams.Chance = _ => true;
            Singles_ReleaseSingle.Postfix(Single(false, false, WithFans(Misandry)));
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void SickOrOtherTraits_NoRoll()
        {
            data_girls.girls sick = WithFans(Misandry);
            sick.status = data_girls._status.depressed;
            Seams.Chance = _ => true;

            Singles_ReleaseSingle.Postfix(Single(true, true, sick, WithFans(None), null));
            Singles_ReleaseSingle.Postfix(null);
            Assert.Empty(Seams.ChancesRolled);
        }
    }

    /// <summary>
    /// Perfectionist idols lose 20 mental stamina when a world tour ends below 80% average attendance,
    /// or when a concert they perform in ends below 100% hype.
    /// </summary>
    public class PerfectionistTests
    {
        public PerfectionistTests() => TestGame.Reset();

        private static SEvent_Tour TourEvent(params int[] attendance)
        {
            SEvent_Tour tourEvent = TestGame.Component<SEvent_Tour>();
            tourEvent.Tour = new SEvent_Tour.tour();
            foreach (int a in attendance)
                tourEvent.Tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Attendance = a });
            return tourEvent;
        }

        /// <summary>
        /// The game's FinishTour clears the tour before the postfix runs, so the prefix checks attendance.
        /// </summary>
        private static void FinishTour(SEvent_Tour tourEvent)
        {
            bool state = false;
            SEvent_Tour_FinishTour.Prefix(tourEvent, ref state);
            tourEvent.Tour = null;
            SEvent_Tour_FinishTour.Postfix(state);
        }

        [Fact]
        public void PoorTour_ActivePerfectionistsLose20()
        {
            data_girls.girls perfectionist = TestGame.Hire(TestGame.Idol(Perfectionist));
            data_girls.girls resting = TestGame.Hire(TestGame.Idol(Perfectionist));
            resting.status = data_girls._status.hiatus;
            data_girls.girls other = TestGame.Hire(TestGame.Idol());

            FinishTour(TourEvent(70, 85));

            Assert.Equal(80f, TestGame.Stat(perfectionist, mentalStamina));
            Assert.Equal(100f, TestGame.Stat(resting, mentalStamina));
            Assert.Equal(100f, TestGame.Stat(other, mentalStamina));
            Assert.Equal(-20f, PERFECTIONIST_MENTAL);
        }

        [Fact]
        public void GoodTour_Unchanged()
        {
            data_girls.girls perfectionist = TestGame.Hire(TestGame.Idol(Perfectionist));
            FinishTour(TourEvent(80, 80));
            Assert.Equal(100f, TestGame.Stat(perfectionist, mentalStamina));
        }

        [Fact]
        public void NoTour_NoError()
        {
            data_girls.girls perfectionist = TestGame.Hire(TestGame.Idol(Perfectionist));
            SEvent_Tour tourEvent = TestGame.Component<SEvent_Tour>();
            bool state = true;
            SEvent_Tour_FinishTour.Prefix(tourEvent, ref state);
            Assert.False(state);
            SEvent_Tour_FinishTour.Prefix(null, ref state);
            Assert.False(state);
            SEvent_Tour_FinishTour.Postfix(state);
            Assert.Equal(100f, TestGame.Stat(perfectionist, mentalStamina));
        }

        private static SEvent_Concerts._concert Concert(float hype, data_girls.girls center)
        {
            SEvent_Concerts._concert concert = new() { Hype = hype };
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = center });
            return concert;
        }

        [Fact]
        public void LowHypeConcert_PerformingPerfectionistLoses20()
        {
            data_girls.girls performer = TestGame.Hire(TestGame.Idol(Perfectionist));
            data_girls.girls offstage = TestGame.Hire(TestGame.Idol(Perfectionist));

            SEvent_Concerts__concert_Finish.Postfix(Concert(99f, performer));

            Assert.Equal(80f, TestGame.Stat(performer, mentalStamina));
            Assert.Equal(100f, TestGame.Stat(offstage, mentalStamina));
        }

        [Fact]
        public void FullHypeConcert_Unchanged()
        {
            data_girls.girls performer = TestGame.Hire(TestGame.Idol(Perfectionist));
            SEvent_Concerts__concert_Finish.Postfix(Concert(100f, performer));
            SEvent_Concerts__concert_Finish.Postfix(null);
            Assert.Equal(100f, TestGame.Stat(performer, mentalStamina));
        }
    }

    /// <summary>
    /// The shared stat limits keep scores in range after every trait has been applied.
    /// </summary>
    public class StatLimitTests
    {
        public StatLimitTests() => TestGame.Reset(patched: true);

        [Fact]
        public void BusinessCoeff_Between0And20()
        {
            float high = 25f, low = -1f;
            StatLimits.Business__proposal_GetGirlCoeff_Limits.Postfix(ref high);
            StatLimits.Business__proposal_GetGirlCoeff_Limits.Postfix(ref low);
            Assert.Equal(20f, high);
            Assert.Equal(0f, low);
        }

        [Fact]
        public void Averages_Between0And100()
        {
            Assert.Equal(100f, data_girls.GetAverageParam(vocal, new List<data_girls.girls> { TestGame.Idol(Lone_Wolf, statValue: 90f) }));
            Assert.Equal(0f, data_girls.GetAverageParam(dance, new List<data_girls.girls> { TestGame.Idol(Clumsy, statValue: 10f), TestGame.Idol(Clumsy, statValue: 10f) }));
        }

        [Fact]
        public void ConcertScores_Between0And100()
        {
            int song = 130, mc = -5;
            StatLimits.SEvent_Concerts__concert__song_GetSkillValue_Limits.Postfix(ref song);
            StatLimits.SEvent_Concerts__concert__mc_GetSkillValue_Limits.Postfix(ref mc);
            Assert.Equal(100, song);
            Assert.Equal(0, mc);
        }

        [Fact]
        public void CastParams_Between0And100()
        {
            Shows._show show = new() { medium = new Shows._param { media_type = Shows._param._media_type.internet } };
            TestGame.CallPrivate(show, typeof(Shows._show), "AddCastParam", cute, new List<data_girls.girls> { TestGame.Idol(Meme_queen, statValue: 95f) });
            Assert.Equal(100f, show.girlParams.Last().val);

            List<data_girls.girls.param> popupParams = new() { new data_girls.girls.param { type = cute, _val = 120f } };
            StatLimits.Show_Popup_AddCastParam_Limits.Postfix(popupParams);
            Assert.Equal(100f, popupParams[0].val);
        }

        [Fact]
        public void SenbatsuAndChemistry_Between0And100()
        {
            data_girls.girls.param single = new() { type = vocal, _val = 110f };
            data_girls.girls.param show = new() { type = vocal, _val = -10f };
            StatLimits.Singles__single_SenbatsuCalcParam_Limits.Postfix(ref single);
            StatLimits.Shows__show_SenbatsuCalcParam_Limits.Postfix(ref show);
            Assert.Equal(100f, single.val);
            Assert.Equal(0f, show.val);

            float chemistry = 140f;
            StatLimits.data_girls_GetTeamChemistry_Patch.Postfix(ref chemistry);
            Assert.Equal(100f, chemistry);
        }

        [Fact]
        public void Limits_IgnoreMissingParams()
        {
            StatLimits.Show_Popup_AddCastParam_Limits.Postfix(null);
            StatLimits.Show_Popup_AddCastParam_Limits.Postfix(new List<data_girls.girls.param>());
            StatLimits.Show_Popup_AddCastParam_Limits.Postfix(new List<data_girls.girls.param> { null });

            StatLimits.Shows__show_AddCastParam_Limits.Postfix(null);
            StatLimits.Shows__show_AddCastParam_Limits.Postfix(new Shows._show { girlParams = null });
            StatLimits.Shows__show_AddCastParam_Limits.Postfix(new Shows._show { girlParams = new List<data_girls.girls.param>() });
            StatLimits.Shows__show_AddCastParam_Limits.Postfix(new Shows._show { girlParams = new List<data_girls.girls.param> { null } });

            data_girls.girls.param none = null;
            StatLimits.Singles__single_SenbatsuCalcParam_Limits.Postfix(ref none);
            StatLimits.Shows__show_SenbatsuCalcParam_Limits.Postfix(ref none);
            Assert.Null(none);
        }

        [Fact]
        public void CastParams_OnlyLastParamClamped()
        {
            List<data_girls.girls.param> popupParams = new()
            {
                new data_girls.girls.param { type = cute, _val = 130f },
                new data_girls.girls.param { type = cute, _val = -5f },
            };
            StatLimits.Show_Popup_AddCastParam_Limits.Postfix(popupParams);
            Assert.Equal(130f, popupParams[0]._val);
            Assert.Equal(0f, popupParams[1].val);

            Shows._show show = new() { girlParams = new List<data_girls.girls.param>
            {
                new data_girls.girls.param { type = cute, _val = -5f },
                new data_girls.girls.param { type = cute, _val = 130f },
            } };
            StatLimits.Shows__show_AddCastParam_Limits.Postfix(show);
            Assert.Equal(-5f, show.girlParams[0]._val);
            Assert.Equal(100f, show.girlParams[1].val);
        }
    }
}
