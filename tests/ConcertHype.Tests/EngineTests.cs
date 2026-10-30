using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ConcertHype.Tests
{
    public class RollTests
    {
        [Fact]
        public void ExpectedRoll_IsSkillPlusSenbatsuPlusRowEase_AndAppealDoesntChangeIt()
        {
            ShowSetup setup = Shows.Setup();
            ShowState plain = Shows.Start(setup);
            ShowSetup appealing = Shows.Setup();
            appealing.Members[0].FanMix = new double[] { 1, 0, 1, 0, 1, 0, 0 };
            appealing.Songs[0].Appeal = new double[] { 2, -1, 2, 0, 1, 0, 0 };
            ShowState state = Shows.Start(appealing);

            RollParts parts = Engine.ExpectedRoll(state, 0, 0);

            Assert.Equal(60, parts.Skill);
            Assert.Equal(1, parts.EnergyFactor);
            Assert.Equal(15, parts.Senbatsu);
            Assert.Equal(0, parts.RowEase);  // slot 0 is the front row
            Assert.Equal(75, parts.Expected, 9);
            Assert.Equal(Engine.ExpectedRoll(plain, 0, 0).Expected, parts.Expected, 9);
        }

        [Fact]
        public void RowEase_RaisesBackRowRolls_AndLeavesTheFrontRowAlone()
        {
            ShowState state = Shows.Start(Shows.Setup(slots: 6, skill: 40), new Config { PBase = 0, PRoll = 0, PTired = 0 });
            Config config = state.Config;
            Song song = state.CurrentSong;
            for (int j = 0; j < song.Slots.Count; j++)
            {
                RollParts parts = Engine.ExpectedRoll(state, j, song.Slots[j].Original);
                Assert.Equal(config.RowEaseOf(song.Slots[j].Row), parts.RowEase);
                Assert.Equal(30 + 0.7 * 40 + 15 + parts.RowEase, parts.Expected, 9);
            }
            Assert.Equal(new double[] { 0, 4, 8, 12, 16 }, config.RowEase);
            double front = PanelView.SuccessChance(config, Engine.ExpectedRoll(state, 0, song.Slots[0].Original).Expected);
            double back = PanelView.SuccessChance(config, Engine.ExpectedRoll(state, 5, song.Slots[5].Original).Expected);
            Assert.True(back > front);
        }

        [Fact]
        public void LowSkill_CountsCloserToHighSkill()
        {
            Config config = new Config { PBase = 0, PRoll = 0, PTired = 0, RowEase = new double[] { 0 } };
            double Expected(double skill) => Engine.ExpectedRoll(Shows.Start(Shows.Setup(skill: skill), config), 0, 0).Expected;

            Assert.Equal(30 + 0.7 * 18 + 15, Expected(18), 9);
            Assert.Equal(30 + 0.7 * 99 + 15, Expected(99), 9);
            // A fresh normal-tier idol in her own place now succeeds often; a strong one still does better
            Assert.InRange(PanelView.SuccessChance(config, Expected(18)), 35, 45);
            Assert.True(Expected(87) > Expected(50));

            // Tiredness still scales it down
            ShowState state = Shows.Start(Shows.Setup(skill: 50), config);
            state.Energy[0] = 0;
            Assert.Equal(0.7 * (30 + 0.7 * 50) + 15, Engine.ExpectedRoll(state, 0, 0).Expected, 9);
        }

        [Fact]
        public void ExpectedRoll_GivesNoSenbatsuPointsOutsideHerOwnSlot()
        {
            ShowState state = Shows.Start(Shows.Setup());
            Assert.Equal(0, Engine.ExpectedRoll(state, 0, 5).Senbatsu);
        }

        [Fact]
        public void ExpectedRoll_IsFlooredAtZero()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 0), new Config { RowEase = new double[] { -10 }, SkillBase = 0 });
            Assert.Equal(0, Engine.ExpectedRoll(state, 0, 5).Expected);
        }

        [Fact]
        public void EnergyFactor_ScalesSkillDownToTheMinimumAtZeroEnergy()
        {
            ShowState state = Shows.Start(Shows.Setup());
            state.Energy[0] = 50;
            Assert.Equal(0.85, Engine.ExpectedRoll(state, 0, 0).EnergyFactor, 9);
            state.Energy[0] = 0;
            Assert.Equal(0.7, Engine.ExpectedRoll(state, 0, 0).EnergyFactor, 9);
            Assert.Equal(0.7 * 60 + 15, Engine.ExpectedRoll(state, 0, 0).Expected, 9);
        }


    }

    public class HypeTests
    {
        private static SongResult PlayOriginals(ShowState state) => Engine.Play(state, Engine.DefaultLineup(state));

        [Fact]
        public void OriginalsRollingPar_LeaveHypeUnchanged()
        {
            // 45 skill + 15 senbatsu = 60 = par
            ShowState state = Shows.Start(Shows.Setup(skill: 45));
            SongResult result = PlayOriginals(state);
            Assert.Equal(60, result.Weighted, 9);
            Assert.Equal(0, result.Change, 9);
            Assert.Equal(50, state.Hype, 9);
        }

        [Fact]
        public void EachRollPointAbovePar_IsHalfAHypePoint()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 65));
            SongResult result = PlayOriginals(state);
            Assert.Equal(80, result.Weighted, 9);
            Assert.Equal(10, result.BaseChange, 9);
            Assert.Equal(60, state.Hype, 9);
            Assert.Equal(new[] { 60.0 }, state.History);
        }

        [Fact]
        public void WeightedPerformance_WeighsRowsAndFanShare()
        {
            ShowSetup setup = Shows.Setup(members: 2, slots: 2);
            Shows.SetSkill(setup, 0, 85);
            Shows.SetSkill(setup, 1, 25);
            Assert.Equal((1.5 * 100 + 1.0 * 40) / 2.5, PlayOriginals(Shows.Start(setup)).Weighted, 9);

            setup.Members[0].Fans = 300;
            setup.Members[1].Fans = 100;
            Assert.Equal((1.5 * 1.5 * 100 + 0.5 * 1.0 * 40) / (2.25 + 0.5), PlayOriginals(Shows.Start(setup)).Weighted, 9);
        }

        [Fact]
        public void FanWeight_HasAFloor_AndIsOneWithoutFans()
        {
            ShowSetup setup = Shows.Setup(members: 2, slots: 2);
            setup.Members[0].Fans = 100;
            setup.Members[1].Fans = 0;
            ShowState state = Shows.Start(setup);
            Assert.Equal(2, state.FanWeight[0], 9);
            Assert.Equal(0.25, state.FanWeight[1], 9);

            setup.Members[0].Fans = 0;
            Assert.Equal(new[] { 1.0, 1.0 }, Shows.Start(setup).FanWeight);
        }

        [Fact]
        public void EmptySlot_CountsAsARollOfZero()
        {
            ShowSetup setup = Shows.Setup(members: 3, slots: 3, skill: 45);
            setup.Members[2].CanPerform = false;
            ShowState state = Shows.Start(setup);
            int[] lineup = Engine.DefaultLineup(state);
            Assert.Equal(new[] { 0, 1, -1 }, lineup);

            SongResult result = Engine.Play(state, lineup);
            Assert.Equal((1.5 * 60 + 1.0 * 60 + 1.0 * 0) / 3.5, result.Weighted, 9);
            Assert.Equal(1, result.EmptyFills);
            Assert.Equal(1, state.EmptyFills);
        }

        private static Config Flowing()
        {
            Config config = Shows.Quiet();
            config.FlowBonus = 1;
            return config;
        }

        [Fact]
        public void EachPartShared_WithTheSongBefore_AddsTheFlowBonus_BeforeTheMultipliers()
        {
            ShowSetup setup = Shows.Setup(skill: 45);
            setup.Songs[0].Genre = setup.Songs[1].Genre = "Pop";
            setup.Songs[0].Lyrics = setup.Songs[1].Lyrics = "Love";
            setup.Songs[0].Choreography = "Cute";
            setup.Songs[1].Choreography = "Cool";
            setup.Production = 1;
            ShowState state = Shows.Start(setup, Flowing());

            SongResult first = PlayOriginals(state);
            Assert.Equal(SongMatch.None, first.Matches);
            Assert.Equal(0, first.Change, 9);
            // Fresh again, so the second song rolls par as well
            for (int m = 0; m < state.Energy.Length; m++)
                state.Energy[m] = 100;
            SongResult second = PlayOriginals(state);
            Assert.Equal(SongMatch.Genre | SongMatch.Lyrics, second.Matches);
            Assert.Equal(2, second.Flow);
            Assert.True(second.Gain);
            // A gain: the best production quality (x1.25) scales the flow as well
            Assert.Equal(2 * 1.25, second.Change, 9);
        }

        [Fact]
        public void Matches_NeedTheSameTitle_NotAnEmptyOne_AndTheFirstSongHasNone()
        {
            ShowSetup setup = Shows.Setup(songs: 3);
            setup.Songs[1].Genre = setup.Songs[0].Genre;
            setup.Songs[1].Choreography = setup.Songs[2].Choreography = "Cool";
            Assert.Equal("", setup.Songs[1].Lyrics);

            Assert.Equal(SongMatch.None, Engine.Matches(setup, 0));
            Assert.Equal(SongMatch.Genre, Engine.Matches(setup, 1));
            Assert.Equal(SongMatch.Choreography, Engine.Matches(setup, 2));
            Assert.Equal(SongMatch.None, Engine.Matches(setup, 3));
            Assert.Equal(3, Engine.Count(SongMatch.Genre | SongMatch.Lyrics | SongMatch.Choreography));
            Assert.Equal(0, Engine.Count(SongMatch.None));
        }

        [Fact]
        public void ATalkBreak_BetweenTwoSongs_KeepsTheirFlow()
        {
            ShowSetup setup = Shows.Setup(skill: 45);
            setup.Songs[0].Genre = setup.Songs[1].Genre = "Pop";
            ShowState state = Shows.Start(setup, Flowing());
            PlayOriginals(state);
            Engine.CallMc(state, 5);
            Assert.Equal(1, Engine.Preview(state, Engine.DefaultLineup(state)).Steps.Flow);
        }

        /// <summary>
        /// The hype rules written out in one go, without the flow: the performance against par, quality and crowd on a
        /// gain, novelty by hype band, the venue's hype, then the floor and cap.
        /// </summary>
        private static double Formula(Config c, ShowSetup s, double h, double weighted, double crowd, double novelty)
        {
            double d = c.KPerf * (weighted - c.Par);
            if (d > 0)
                d *= (c.ProductionFloor + c.ProductionRange * Engine.Clamp(s.Production, 0, 1)) * Engine.CrowdMultiplier(c, crowd);
            double band = c.NoveltyBlendBand;
            double t = band <= 0 ? (h >= 100 ? 1 : 0) : Engine.Clamp((h - 100 + band) / (2 * band), 0, 1);
            double gainAmp = c.AmpHigh + (c.AmpLow - c.AmpHigh) * t, lossAmp = c.AmpLow + (c.AmpHigh - c.AmpLow) * t;
            d *= 1 + novelty * (d >= 0 ? gainAmp : lossAmp);
            double filled = Engine.Clamp(s.CapacityFilled, 0, 1);
            d += filled >= 0.9995 ? c.VenueSoldOutBonus : filled >= c.VenueNeutralShare ? 0 : -c.VenueEmptyPenalty * (c.VenueNeutralShare - filled) / c.VenueNeutralShare;
            return Engine.Clamp(h + d, s.HypeFloor, Engine.MaxHype) - h;
        }

        [Fact]
        public void WithoutFlow_EveryPreviewAndSong_FollowsTheHypeFormula()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                ShowSetup setup = Balance.RandomSetups.Make(seed);
                setup.CapacityFilled = seed % 4 == 0 ? 1 : 0.3 + 0.07 * (seed % 10);
                setup.Production = 0.05 * seed;
                Config config = new Config { FlowBonus = 0 };
                ShowState state = Engine.Start(setup, config, seed);
                while (!state.Finished)
                {
                    int[] lineup = Engine.DefaultLineup(state);
                    SongPreview preview = Engine.Preview(state, lineup);
                    Assert.Equal(Formula(config, setup, state.Hype, preview.Weighted, preview.Steps.Crowd, preview.Novelty), preview.Change, 12);
                    double hype = state.Hype;
                    SongResult result = Engine.Play(state, lineup);
                    Assert.Equal(Formula(config, setup, hype, result.Weighted, result.Crowd, result.Novelty), result.Change, 12);
                }
            }
        }

        [Fact]
        public void ProductionQuality_ScalesGainsOnly()
        {
            ShowSetup setup = Shows.Setup(skill: 65);
            setup.Production = 0;
            SongResult gain = PlayOriginals(Shows.Start(setup));
            Assert.Equal(0.75, gain.QualityMultiplier, 9);
            Assert.Equal(7.5, gain.Change, 9);

            setup = Shows.Setup(skill: 25);
            setup.Production = 0;
            SongResult loss = PlayOriginals(Shows.Start(setup));
            Assert.False(loss.Gain);
            Assert.Equal(1.0, loss.QualityMultiplier);
            Assert.Equal(-10, loss.Change, 9);
        }

        /// <summary>
        /// The rule tests' config with the venue's default hype.
        /// </summary>
        private static Config WithVenue()
        {
            Config config = Shows.Quiet();
            Config defaults = new Config();
            config.VenueSoldOutBonus = defaults.VenueSoldOutBonus;
            config.VenueEmptyPenalty = defaults.VenueEmptyPenalty;
            return config;
        }

        [Theory]
        [InlineData(1.0, 10)]
        [InlineData(1.3, 10)]      // kept between 0 and 1
        [InlineData(0.9995, 10)]   // a hair short still counts as sold out
        [InlineData(0.999, 0)]
        [InlineData(0.95, 0)]
        [InlineData(0.9, 0)]
        [InlineData(0.8, -15 * 0.1 / 0.9)]
        [InlineData(0.5, -15 * 0.4 / 0.9)]
        [InlineData(0.3, -10)]
        [InlineData(0, -15)]
        public void TheVenue_GivesASellOutABonus_AndEmptySeatsAPenalty(double filled, double hype)
        {
            Assert.Equal(hype, Engine.VenueHype(new ShowSetup { CapacityFilled = filled }, new Config()), 9);
            Assert.Equal(filled >= 0.9995, Engine.SoldOut(new ShowSetup { CapacityFilled = filled }));
        }

        [Theory]
        [InlineData(65, 1.0, 7.5 + 10)]
        [InlineData(25, 1.0, -10 + 10)]
        [InlineData(65, 0.0, 7.5 - 15)]
        [InlineData(25, 0.0, -10 - 15)]
        [InlineData(25, 0.95, -10)]
        public void TheVenuesHype_IsAddedToAGainOrALossAlike(double skill, double filled, double change)
        {
            ShowSetup setup = Shows.Setup(skill: skill);   // rolls 80 (+10, x0.75 for the worst quality) or 40 (-10)
            setup.Production = 0;
            setup.CapacityFilled = filled;
            SongResult result = PlayOriginals(Shows.Start(setup, WithVenue()));
            Assert.Equal(Engine.VenueHype(setup, new Config()), result.Venue, 9);
            Assert.Equal(change, result.Change, 9);
        }

        [Fact]
        public void TheVenuesHype_ComesAfterNovelty_AndBeforeTheFloorAndCap()
        {
            ShowSetup setup = Shows.Setup(members: 6, slots: 3);
            for (int m = 0; m < 3; m++)
                setup.Members[m].CanPerform = false;
            for (int m = 3; m < 6; m++)
                Shows.SetSkill(setup, m, 65);
            ShowState state = Shows.Start(setup, WithVenue());
            state.Hype = 70;
            // An all-new lineup below 100% hype: +2.5 grows by 2.5 times, and the sold-out bonus is added as it is
            SongResult result = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Equal(2.5, result.Amplification, 9);
            Assert.Equal(2.5 * 2.5 + 10, result.Change, 9);

            setup = Shows.Setup(skill: 45);   // rolls par
            state = Shows.Start(setup, WithVenue());
            state.Hype = 195;
            Assert.Equal(200, PlayOriginals(state).HypeAfter);
            setup.CapacityFilled = 0;
            setup.HypeFloor = 40;
            state = Shows.Start(setup, WithVenue());
            state.Hype = 45;
            Assert.Equal(40, PlayOriginals(state).HypeAfter);
        }

        [Fact]
        public void Novelty_IsTheRowWeightedShareOfChangedSlots()
        {
            ShowState state = Shows.Start(Shows.Setup());
            Song song = state.CurrentSong;
            Assert.Equal(0, Engine.Novelty(state, song, new[] { 0, 1, 2 }));
            Assert.Equal(1.5 / 3.5, Engine.Novelty(state, song, new[] { 3, 1, 2 }), 9);
            Assert.Equal(1.0 / 3.5, Engine.Novelty(state, song, new[] { 0, 1, -1 }), 9);
        }

        /// <summary>
        /// Every original can't perform, so the bench fills all three slots: novelty 1.
        /// </summary>
        private static SongResult PlayAllNew(double hype, double benchSkill)
        {
            ShowSetup setup = Shows.Setup(members: 6, slots: 3);
            for (int m = 0; m < 3; m++)
                setup.Members[m].CanPerform = false;
            for (int m = 3; m < 6; m++)
                Shows.SetSkill(setup, m, benchSkill);
            ShowState state = Shows.Start(setup);
            state.Hype = hype;
            SongResult result = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Equal(1, result.Novelty);
            return result;
        }

        [Theory]
        [InlineData(70, 65, 2.5)]   // below 100: a surprise doubles up gains (x(1 + 1.5))
        [InlineData(80, 65, 2.5)]   // the blend band starts at 80
        [InlineData(100, 65, 2.0)]  // halfway through the band
        [InlineData(120, 65, 1.5)]  // the band ends at 120
        [InlineData(130, 65, 1.5)]  // above 100: gains grow less
        [InlineData(70, 45, 1.5)]   // below 100: losses grow less
        [InlineData(100, 45, 2.0)]
        [InlineData(130, 45, 2.5)]  // above 100: the crowd wanted the hits
        public void NoveltyAmplification_DependsOnTheHypeBand(double hype, double benchSkill, double amplification)
        {
            SongResult result = PlayAllNew(hype, benchSkill);
            Assert.Equal(amplification, result.Amplification, 9);
            Assert.Equal(0.5 * (benchSkill - 60) * amplification, result.Change, 9);
        }

        [Fact]
        public void Hype_IsClampedToTheFloorAnd200()
        {
            ShowSetup setup = Shows.Setup(skill: 25);
            setup.HypeFloor = 40;
            ShowState state = Shows.Start(setup);
            state.Hype = 45;
            Assert.Equal(40, PlayOriginals(state).HypeAfter);

            state = Shows.Start(Shows.Setup(skill: 65));
            state.Hype = 195;
            Assert.Equal(200, PlayOriginals(state).HypeAfter);
        }

        [Fact]
        public void StartingHype_IsClampedToTheFloor()
        {
            ShowSetup setup = Shows.Setup();
            setup.StartingHype = 20;
            setup.HypeFloor = 30;
            Assert.Equal(30, Shows.Start(setup).Hype);
        }
    }

    public class EnergyTests
    {
        [Fact]
        public void Performers_PayByRowAndRoll_BenchRecovers()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 45));
            state.Energy[5] = 50;
            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Equal(100 - 12, state.Energy[0], 9);  // center: 8 x 1.5
            Assert.Equal(100 - 8, state.Energy[1], 9);   // row 2: 8 x 1.0
            Assert.Equal(60, state.Energy[5], 9);
            Assert.Equal(100, state.Energy[6]);          // never above 100
        }

        [Fact]
        public void RollCost_IsLowerForGoodRolls_WithinItsLimits()
        {
            Config config = new Config();
            Assert.Equal(12, Engine.EnergyCost(config, 0, 60), 9);
            Assert.Equal(12 * 0.8, Engine.EnergyCost(config, 0, 80), 9);
            Assert.Equal(6, Engine.EnergyCost(config, 0, 160), 9);
            Assert.Equal(18, Engine.EnergyCost(config, 0, 0), 9);
            Assert.Equal(8 * 0.6, Engine.EnergyCost(config, 4, 60), 9);
        }

        [Fact]
        public void HighHype_DoesntChangeWhatPerformersPay()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 45));
            state.Hype = 140;
            SongResult result = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.True(result.HypeAfter >= 100);
            Assert.Equal(100 - 12, state.Energy[0], 9);
            Assert.Equal(100 - 8, state.Energy[1], 9);
        }

        [Fact]
        public void AnExhaustedIdol_SitsOutUntilSheRecoversToTheReturnThreshold()
        {
            ShowState state = Shows.Start(Shows.Setup(songs: 6, skill: 45));
            state.Energy[0] = 5;
            SongResult first = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Equal(new[] { 0 }, first.Exhausted);
            Assert.False(state.IsAvailable(0));
            Assert.True(Engine.ForcedSlots(state)[0]);

            List<int> returned = new List<int>();
            for (int song = 1; song <= 3; song++)
            {
                Assert.DoesNotContain(0, Engine.DefaultLineup(state));
                returned.AddRange(Engine.Play(state, Engine.DefaultLineup(state)).Returned);
            }
            Assert.Equal(30, state.Energy[0], 9);
            Assert.Equal(new[] { 0 }, returned);
            Assert.Equal(0, Engine.DefaultLineup(state)[0]);
        }
    }

    public class AccidentTests
    {
        [Fact]
        public void Chance_AddsPoorRollAndTirednessToTheBase_UpToTheCap()
        {
            Config config = new Config();
            Assert.Equal(0.005 + 0.01 * 10 + 0.05 * 0.5, Engine.AccidentChance(config, 30, 50), 9);
            Assert.Equal(0.005, Engine.AccidentChance(config, 90, 100), 9);
            config.PRoll = 0.02;
            Assert.Equal(0.5, Engine.AccidentChance(config, 0, 0));
        }

        [Fact]
        public void AnAccident_KeepsTheRoll_AndTakesHerOutFromTheNextSong()
        {
            Config config = Shows.Quiet();
            config.PBase = 1;
            config.PMax = 1;
            config.AbsenceMin = config.AbsenceMax = 1;
            ShowState state = Shows.Start(Shows.Setup(members: 6, songs: 4, skill: 45), config);

            SongResult first = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.All(first.Slots, slot => Assert.True(slot.Accident));
            Assert.Equal(60, first.Weighted, 9);
            Assert.Equal(1, state.Absence[0]);
            Assert.Equal(3, state.Accidents);

            config.PBase = 0;
            Assert.Equal(new[] { 3, 4, 5 }, Engine.DefaultLineup(state));
            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Equal(0, state.Absence[0]);
            Assert.Equal(new[] { 0, 1, 2 }, Engine.DefaultLineup(state));
        }

        [Fact]
        public void AbsenceLength_IsDrawnBetweenItsLimits()
        {
            Config config = Shows.Quiet();
            config.PBase = 1;
            config.PMax = 1;
            HashSet<int> lengths = new HashSet<int>();
            for (int seed = 1; seed <= 40; seed++)
            {
                ShowState state = Shows.Start(Shows.Setup(), config, seed);
                foreach (SlotResult slot in Engine.Play(state, Engine.DefaultLineup(state)).Slots)
                    lengths.Add(slot.AbsenceLength);
            }
            Assert.Equal(new[] { 1, 2, 3 }, lengths.OrderBy(x => x));
        }
    }

    public class McTests
    {
        private static ShowState AfterFirstSong(int songs = 4)
        {
            ShowState state = Shows.Start(Shows.Setup(songs: songs, skill: 45));
            Engine.Play(state, Engine.DefaultLineup(state));
            return state;
        }

        [Theory]
        [InlineData(40, 58)]
        [InlineData(160, 142)]
        public void Mc_PullsHypeTowardFullByTheHostsTalk(double before, double after)
        {
            ShowState state = AfterFirstSong();
            state.Setup.Members[4].Talk = 0.6;
            state.Hype = before;
            McResult mc = Engine.CallMc(state, 4);
            Assert.Equal(after, mc.HypeAfter, 9);
            Assert.Equal(after, state.Hype, 9);
            Assert.Single(state.History);
        }

        [Fact]
        public void Mc_SeveralHostsAddUpTheirTalk_UpToFull()
        {
            ShowState state = AfterFirstSong();
            state.Setup.Members[3].Talk = 0.3;
            state.Setup.Members[4].Talk = 0.2;
            state.Setup.Members[5].Talk = 0.7;
            state.Hype = 40;
            Assert.Equal(40 + 0.5 * 0.5 * 60, Engine.McHype(state, 3, 4), 9);
            // 1.2 together counts as 1
            Assert.Equal(40 + 0.5 * 1 * 60, Engine.McHype(state, 3, 4, 5), 9);
            McResult mc = Engine.CallMc(state, 3, 4, 5);
            Assert.Equal(new[] { 3, 4, 5 }, mc.Hosts);
            Assert.Equal(1, mc.Talk, 9);
            Assert.Equal(70, state.Hype, 9);
        }

        [Fact]
        public void Mc_RestsEveryoneButTheHosts_AndCountsDownAbsences()
        {
            ShowState state = AfterFirstSong();
            state.Energy[0] = 50;
            state.Energy[4] = 50;
            state.Energy[5] = 40;
            state.Absence[3] = 2;
            state.Exhausted[2] = true;
            state.Energy[2] = 25;
            Engine.CallMc(state, 4, 5);
            Assert.Equal(60, state.Energy[0], 9);
            Assert.Equal(50, state.Energy[4], 9);
            Assert.Equal(40, state.Energy[5], 9);
            Assert.Equal(1, state.Absence[3]);
            Assert.False(state.Exhausted[2]);
        }

        [Fact]
        public void Mc_OncePerGap_NotBeforeTheFirstSong_AndOnlyWithChargesLeft()
        {
            ShowState state = Shows.Start(Shows.Setup(songs: 4, skill: 45));
            Assert.NotNull(Engine.CanCallMc(state));
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 4));

            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.Null(Engine.CanCallMc(state));
            Engine.CallMc(state, 4);
            Assert.NotNull(Engine.CanCallMc(state));

            Engine.Play(state, Engine.DefaultLineup(state));
            Engine.CallMc(state, 4);
            Assert.Equal(0, state.McCharges);
            Engine.Play(state, Engine.DefaultLineup(state));
            Assert.NotNull(Engine.CanCallMc(state));
        }

        [Fact]
        public void Mc_NeedsOneToThreeDifferentAvailableHosts()
        {
            ShowState state = AfterFirstSong(songs: 4);
            state.Absence[4] = 1;
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 4));
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 3, 4));
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state));
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 0, 1, 2, 3));
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 3, 3));
            Assert.Equal(2, state.McCharges);  // nothing was called
        }
    }

    public class LineupTests
    {
        [Fact]
        public void DefaultLineup_IsTheSenbatsu_WithTheBestBenchIdolInEachForcedSlot()
        {
            ShowSetup setup = Shows.Setup();
            setup.Members[1].CanPerform = false;
            Shows.SetSkill(setup, 5, 70);
            ShowState state = Shows.Start(setup);
            Assert.Equal(new[] { 0, 5, 2 }, Engine.DefaultLineup(state));
            Assert.Equal(new[] { false, true, false }, Engine.ForcedSlots(state));
        }

        [Fact]
        public void PresetSubstitute_StartsInTheCenterSlot()
        {
            ShowSetup setup = Shows.Setup();
            setup.Songs[0].PresetSubstitute = 4;
            ShowState state = Shows.Start(setup);
            int[] lineup = Engine.DefaultLineup(state);
            Assert.Equal(new[] { 4, 1, 2 }, lineup);
            Assert.Null(Engine.Validate(state, lineup));
        }

        [Fact]
        public void Validate_AllowsAnyNumberOfSubstitutes()
        {
            ShowSetup setup = Shows.Setup();
            setup.Members[2].CanPerform = false;
            ShowState state = Shows.Start(setup);
            Assert.Null(Engine.Validate(state, new[] { 3, 1, 4 }));
            Assert.Null(Engine.Validate(state, new[] { 3, 5, 4 }));
        }

        [Fact]
        public void Validate_AllowsSwaps_AndRejectsDuplicatesEmptiesAndUnavailableIdols()
        {
            ShowSetup setup = Shows.Setup();
            setup.Members[7].CanPerform = false;
            ShowState state = Shows.Start(setup);
            Assert.Null(Engine.Validate(state, new[] { 1, 0, 2 }));      // two idols swapped
            Assert.NotNull(Engine.Validate(state, new[] { 3, 1, 3 }));   // twice
            Assert.NotNull(Engine.Validate(state, new[] { -1, 1, 2 }));  // original can perform
            Assert.NotNull(Engine.Validate(state, new[] { 7, 1, 2 }));   // can't perform
            Assert.NotNull(Engine.Validate(state, new[] { 0, 1 }));      // wrong size
        }

        [Fact]
        public void Validate_RequiresAForcedSlotToBeFilledWhenSomeoneCan()
        {
            ShowSetup setup = Shows.Setup();
            setup.Members[2].CanPerform = false;
            ShowState state = Shows.Start(setup);
            Assert.NotNull(Engine.Validate(state, new[] { 0, 1, -1 }));
            Assert.Throws<ArgumentException>(() => Engine.Play(state, new[] { 0, 1, -1 }));
        }

        [Fact]
        public void Substitution_SendsTheOriginalToTheBench()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 45));
            state.Energy[0] = 50;
            Engine.Play(state, new[] { 3, 1, 2 });
            Assert.Equal(60, state.Energy[0], 9);
            // Her roll is 45 (no senbatsu points), 15 under par: the center's 12 costs 15% more
            Assert.Equal(100 - 12 * 1.15, state.Energy[3], 9);
        }
    }

    public class PreviewScoreAndReplayTests
    {
        [Fact]
        public void Preview_MatchesThePlayedSong_WithoutRandomness()
        {
            ShowSetup setup = Shows.Setup(members: 8, slots: 6, skill: 55);
            Shows.SetSkill(setup, 7, 80);
            ShowState state = Shows.Start(setup);
            int[] lineup = { 7, 1, 2, 3, 4, 5 };
            SongPreview preview = Engine.Preview(state, lineup);
            SongResult result = Engine.Play(state, lineup);
            Assert.Equal(result.Change, preview.Change, 9);
            Assert.Equal(result.Novelty, preview.Novelty, 9);
            Assert.Equal(preview.Change, preview.Low, 9);
            Assert.Equal(preview.Change, preview.High, 9);
        }

        [Fact]
        public void Preview_GivesARangeAroundTheExpectedChange()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 55), new Config());
            SongPreview preview = Engine.Preview(state, Engine.DefaultLineup(state));
            Assert.True(preview.Low < preview.Change && preview.Change < preview.High);
        }

        [Fact]
        public void Score_BlendsAverageBestMomentAndFinale()
        {
            ShowState state = Shows.Start(Shows.Setup());
            state.History.AddRange(new double[] { 50, 150, 100 });
            Score score = Engine.Score(state);
            Assert.Equal(100, score.Average, 9);
            Assert.Equal(150, score.Peak);
            Assert.Equal(100, score.Finale);
            Assert.Equal(0.5 * 100 + 0.25 * 150 + 0.25 * 100, score.Total, 9);
        }

        [Fact]
        public void Score_WithoutSongs_IsTheCurrentHype()
        {
            ShowState state = Shows.Start(Shows.Setup());
            Assert.Equal(50, Engine.Score(state).Total);
        }

        private static List<double> PlayGreedyish(int seed)
        {
            ShowSetup setup = Shows.Setup(members: 10, slots: 6, songs: 8, skill: 55);
            ShowState state = Engine.Start(setup, new Config { PBase = 0.2 }, seed);
            while (!state.Finished)
            {
                if (state.SongIndex == 3)
                    Engine.CallMc(state, 9);
                int[] lineup = Engine.DefaultLineup(state);
                int[] withSubstitute = (int[])lineup.Clone();
                if (!lineup.Contains(8))
                    withSubstitute[withSubstitute.Length - 1] = 8;
                if (Engine.Validate(state, withSubstitute) == null)
                    lineup = withSubstitute;
                Engine.Play(state, lineup);
            }
            return state.History;
        }

        [Fact]
        public void SameSeedAndDecisions_ReplayTheSameShow()
        {
            Assert.Equal(PlayGreedyish(7), PlayGreedyish(7));
            Assert.NotEqual(PlayGreedyish(7), PlayGreedyish(8));
        }
    }

    public class CrowdTests
    {
        private static readonly double[] Teens = { 0.5, 0.5, 0.5, 0.5, 1, 0, 0 }, Adults = { 0.5, 0.5, 0.5, 0.5, 0, 0, 1 };

        /// <summary>
        /// Three places (rows 0, 1, 1: weights 1.5, 1, 1); member 0 has teen fans, the rest adult fans.
        /// </summary>
        private static ShowSetup TeensAndAdults(double skill = 60)
        {
            ShowSetup setup = Shows.Setup(slots: 3, skill: skill);
            foreach (Member member in setup.Members)
                member.FanMix = Adults;
            setup.Members[0].FanMix = Teens;
            return setup;
        }

        [Fact]
        public void CrowdMix_IsTheRowWeightedAverageOfEachPerformersOwnShares_NotHerFanCount()
        {
            ShowSetup setup = TeensAndAdults();
            setup.Members[0].Fans = 1;
            setup.Members[1].Fans = 1_000_000;
            ShowState state = Shows.Start(setup);

            double[] mix = Engine.CrowdMix(state, state.CurrentSong, new[] { 0, 1, 2 });

            Assert.Equal(1.5 / 3.5, mix[4], 9);
            Assert.Equal(2 / 3.5, mix[6], 9);
            Assert.Equal(0.5, mix[0], 9);
        }

        [Fact]
        public void CrowdMix_LeavesEmptyPlacesOut_AndIsEvenWithNobody()
        {
            ShowState state = Shows.Start(TeensAndAdults());
            Assert.Equal(1.5 / 2.5, Engine.CrowdMix(state, state.CurrentSong, new[] { 0, 1, -1 })[4], 9);
            Assert.Equal(FanTypes.EvenMix(), Engine.CrowdMix(state, state.CurrentSong, new[] { -1, -1, -1 }));
        }

        [Fact]
        public void TheSameIdol_MovesTheMixMoreInTheFrontRow()
        {
            ShowState state = Shows.Start(TeensAndAdults());
            double front = Engine.CrowdMix(state, state.CurrentSong, new[] { 0, 1, 2 })[4];
            double back = Engine.CrowdMix(state, state.CurrentSong, new[] { 1, 0, 2 })[4];
            Assert.True(front > back);
        }

        [Fact]
        public void CrowdAppeal_IsEachTypesShareTimesTheSongsAppealOverTheAverage()
        {
            ShowSetup setup = TeensAndAdults();
            setup.Songs[0].Appeal = new[] { 0.2, 0, 0.1, 0.3, 0.8, 0, -0.6 };
            setup.AverageSongAppeal = new[] { 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1 };
            ShowState state = Shows.Start(setup);
            int[] lineup = { 0, 1, 2 };
            double[] mix = Engine.CrowdMix(state, state.CurrentSong, lineup);

            double expected = Enumerable.Range(0, FanTypes.Count).Sum(i => mix[i] * (setup.Songs[0].Appeal[i] - 0.1));
            Assert.Equal(expected, Engine.CrowdAppeal(state, state.CurrentSong, lineup), 9);
        }

        [Fact]
        public void ASongLoved_ByTheLineupsFans_BeatsOneTheyDislike_AndATypicalSongIsNeutral()
        {
            ShowSetup setup = TeensAndAdults();
            setup.Songs[0].Appeal = new[] { 0, 0, 0, 0, 1.0, 0, -1.0 };   // loved by teens, disliked by adults
            ShowState state = Shows.Start(setup);
            Song song = state.CurrentSong;

            double teenFront = Engine.CrowdAppeal(state, song, new[] { 0, 1, 2 });
            for (int m = 0; m < setup.Members.Count; m++)
                setup.Members[m].FanMix = m == 0 ? Adults : Teens;
            double teensEverywhereElse = Engine.CrowdAppeal(state, song, new[] { 0, 1, 2 });
            Assert.True(teensEverywhereElse > teenFront);
            Assert.True(teenFront < 0);

            setup.AverageSongAppeal = (double[])song.Appeal.Clone();
            Assert.Equal(0, Engine.CrowdAppeal(state, song, new[] { 0, 1, 2 }), 9);
            Assert.Equal(1, Engine.CrowdMultiplier(state.Config, 0));
        }

        [Fact]
        public void CrowdMultiplier_IsOnePlusKTimesCrowd_KeptWithinItsLimits()
        {
            Config config = new Config();
            Assert.Equal(1 + 0.3 * 0.5, Engine.CrowdMultiplier(config, 0.5), 9);
            Assert.Equal(1 - 0.3 * 0.5, Engine.CrowdMultiplier(config, -0.5), 9);
            Assert.Equal(1.2, Engine.CrowdMultiplier(config, 5));
            Assert.Equal(0.8, Engine.CrowdMultiplier(config, -5));
        }

        [Theory]
        [InlineData(55, true)]    // rolls 70: a gain, scaled
        [InlineData(35, false)]   // rolls 50: a loss, left as it is
        public void TheCrowdMultiplier_ScalesGainsOnly(double skill, bool scaled)
        {
            SongResult Play(double teenAppeal)
            {
                ShowSetup setup = TeensAndAdults(skill);
                foreach (Member member in setup.Members)
                    member.FanMix = Teens;
                setup.Songs[0].Appeal = new[] { 0, 0, 0, 0, teenAppeal, 0, 0 };
                ShowState state = Shows.Start(setup);
                return Engine.Play(state, Engine.DefaultLineup(state));
            }
            SongResult plain = Play(0), loved = Play(0.4);

            Assert.Equal(1 + 0.3 * 0.4, loved.CrowdMultiplier, 9);
            Assert.Equal(scaled ? plain.Change * loved.CrowdMultiplier : plain.Change, loved.Change, 9);
        }

        [Fact]
        public void APreview_ShowsTheCrowdMultiplierOfItsLineup()
        {
            ShowSetup setup = TeensAndAdults(55);
            setup.Songs[0].Appeal = new[] { 0, 0, 0, 0, 0.6, 0, 0 };
            ShowState state = Shows.Start(setup);
            int[] lineup = { 0, 1, 2 };
            SongPreview preview = Engine.Preview(state, lineup);
            Assert.Equal(Engine.CrowdAppeal(state, state.CurrentSong, lineup), preview.Steps.Crowd, 9);
            Assert.Equal(Engine.CrowdMultiplier(state.Config, preview.Steps.Crowd), preview.Steps.CrowdMultiplier, 9);
        }
    }

    public class HypePartsTests
    {
        private static HypeParts Expected(ShowState state) => Engine.Preview(state, Engine.DefaultLineup(state)).Parts;

        private static Config WithVenue()
        {
            Config config = Shows.Quiet();
            config.VenueSoldOutBonus = 10;
            config.VenueEmptyPenalty = 15;
            return config;
        }

        [Fact]
        public void TheVenuePart_IsTheVenuesHype_OnAGainOrALoss()
        {
            ShowSetup setup = Shows.Setup(skill: 65);   // rolls 80: +10
            HypeParts parts = Expected(Shows.Start(setup, WithVenue()));
            Assert.Equal(10, parts.Performance, 9);
            Assert.Equal(10, parts.Venue, 9);       // sold out
            Assert.Equal(0, parts.Production, 9);   // quality 0.5 scales by 1
            Assert.Equal(20, parts.Total, 9);

            setup.CapacityFilled = 0.5;
            Assert.Equal(-15 * 0.4 / 0.9, Expected(Shows.Start(setup, WithVenue())).Venue, 9);

            setup = Shows.Setup(skill: 25);             // rolls 40: -10
            setup.CapacityFilled = 0;
            parts = Expected(Shows.Start(setup, WithVenue()));
            Assert.Equal((-10.0, -15.0), (parts.Performance, parts.Venue));
            Assert.Equal(-25, parts.Total, 9);
        }

        [Fact]
        public void Quality_AndAppeal_AddTheirShareOfAGain_InTurn()
        {
            ShowSetup setup = Shows.Setup(skill: 65);
            setup.Production = 1;                                    // x1.25
            setup.Songs[0].Appeal = new[] { 0, 0, 0, 0, 0.6, 0, 0 };    // an even mix is a third teen: crowd appeal 0.2
            ShowState state = Shows.Start(setup);
            HypeParts parts = Expected(state);
            double crowd = Engine.CrowdMultiplier(state.Config, 0.2);
            Assert.Equal(10, parts.Performance, 9);
            Assert.Equal(2.5, parts.Quality, 9);
            Assert.Equal(12.5 * (crowd - 1), parts.Appeal, 9);
            Assert.Equal(parts.Quality + parts.Appeal, parts.Production, 9);
        }

        [Fact]
        public void OnALoss_OnlyNoveltyAddsToTheProduction()
        {
            ShowSetup setup = Shows.Setup(members: 6, slots: 3);
            for (int m = 0; m < 3; m++)
                setup.Members[m].CanPerform = false;
            for (int m = 3; m < 6; m++)
                Shows.SetSkill(setup, m, 45);
            setup.CapacityFilled = 0.2;
            setup.Production = 0;
            ShowState state = Shows.Start(setup);
            state.Hype = 70;
            HypeParts parts = Expected(state);
            // Rolls 45: -7.5, and an all-new lineup below 100% hype grows a loss by half
            Assert.Equal(-7.5, parts.Performance, 9);
            Assert.Equal((0.0, 0.0, 0.0), (parts.Venue, parts.Quality, parts.Appeal));
            Assert.Equal(-3.75, parts.Novelty, 9);
            Assert.Equal(-11.25, parts.Total, 9);
        }

        [Fact]
        public void AtTheCap_EveryPartShrinksAlike()
        {
            ShowSetup setup = Shows.Setup(skill: 65);
            ShowState state = Shows.Start(setup, WithVenue());
            state.Hype = 195;
            HypeParts parts = Expected(state);
            // +10 performance and +10 sold out before the cap, 5 after it
            Assert.Equal(2.5, parts.Performance, 9);
            Assert.Equal(2.5, parts.Venue, 9);
            Assert.Equal(5, parts.Total, 9);
        }

        [Fact]
        public void TheParts_AddUpToTheChange_InEveryShowAndAtAnyHype()
        {
            foreach (double hype in new double[] { 10, 60, 95, 130, 195 })
            {
                for (int seed = 1; seed <= 15; seed++)
                {
                    ShowSetup setup = Balance.RandomSetups.Make(seed);
                    setup.CapacityFilled = 0.4 + 0.04 * seed;
                    setup.Production = 0.06 * seed;
                    setup.HypeFloor = seed % 3 == 0 ? 30 : 0;
                    ShowState state = Engine.Start(setup, new Config(), seed);
                    while (!state.Finished)
                    {
                        state.Hype = Math.Max(setup.HypeFloor, hype);
                        int[] lineup = Engine.DefaultLineup(state);
                        // A substitute in the center as well, so novelty counts
                        int bench = Enumerable.Range(0, setup.Members.Count).FirstOrDefault(m => !lineup.Contains(m) && state.IsAvailable(m));
                        int[] changed = (int[])lineup.Clone();
                        changed[0] = bench;
                        foreach (int[] tried in new[] { lineup, changed })
                        {
                            SongPreview preview = Engine.Preview(state, tried);
                            HypeParts parts = preview.Parts;
                            Assert.Equal(preview.Change, parts.Total, 9);
                            Assert.Equal(parts.Flow + parts.Quality + parts.Appeal + parts.Novelty, parts.Production, 9);
                            // The venue's hype, as long as the floor or cap doesn't cut the change
                            if (preview.Steps.HypeAfter > setup.HypeFloor && preview.Steps.HypeAfter < Engine.MaxHype)
                                Assert.Equal(Engine.VenueHype(setup, state.Config), parts.Venue, 9);
                            if (!preview.Steps.Gain)
                                Assert.Equal((0.0, 0.0), (parts.Quality, parts.Appeal));
                        }
                        Engine.Play(state, lineup);
                    }
                }
            }
        }
    }

    public class GapCostTests
    {
        private static double[] Costs(int unavailable)
        {
            ShowSetup setup = Shows.Setup(members: 3, slots: 3, skill: 55);
            setup.Members[unavailable].CanPerform = false;
            ShowState state = Shows.Start(setup);
            return Engine.GapCosts(state, Engine.DefaultLineup(state));
        }

        [Fact]
        public void NoGaps_CostNothing()
        {
            ShowState state = Shows.Start(Shows.Setup(slots: 3, skill: 55));
            Assert.All(Engine.GapCosts(state, Engine.DefaultLineup(state)), c => Assert.Equal(0, c));
        }

        [Fact]
        public void AnEmptyPlace_CostsHype_MoreInTheFrontRowThanFurtherBack()
        {
            double[] front = Costs(0), back = Costs(2);
            Assert.True(front[0] > 0);
            Assert.True(back[2] > 0);
            Assert.True(front[0] > back[2]);
            Assert.Equal(0, front[1]);
            Assert.Equal(0, back[0]);
        }

        [Fact]
        public void TwoGaps_ShareTheirCostByRowWeight()
        {
            ShowSetup setup = Shows.Setup(members: 6, slots: 6, skill: 55);
            setup.Members[0].CanPerform = false;   // row 0, weight 1.5
            setup.Members[3].CanPerform = false;   // row 2, weight 0.6
            for (int m = 0; m < 6; m++)
                setup.Members[m].CanPerform &= m != 0 && m != 3;
            ShowState state = Shows.Start(setup);
            int[] lineup = Engine.DefaultLineup(state);
            Assert.Equal(-1, lineup[0]);
            Assert.Equal(-1, lineup[3]);
            double[] costs = Engine.GapCosts(state, lineup);
            Assert.Equal(1.5 / 0.6, costs[0] / costs[3], 9);
        }
    }
}
