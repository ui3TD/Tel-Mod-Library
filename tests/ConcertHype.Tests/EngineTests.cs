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
        public void ExpectedRoll_IsSkillPlusAppealPlusSenbatsu()
        {
            ShowSetup setup = Shows.Setup();
            setup.Members[0].Appeal = new double[] { 1, 0, 0, 0, 0, 0, 0 };
            setup.Songs[0].Appeal = new double[] { 2, 0, 0, 0, 0, 0, 0 };
            ShowState state = Shows.Start(setup);

            RollParts parts = Engine.ExpectedRoll(state, 0, 0);

            Assert.Equal(60, parts.Skill);
            Assert.Equal(1, parts.EnergyFactor);
            Assert.Equal(15, parts.Appeal, 9);
            Assert.Equal(15, parts.Senbatsu);
            Assert.Equal(90, parts.Expected, 9);
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
            ShowSetup setup = Shows.Setup(skill: 0);
            setup.Members[5].Appeal = new double[] { 1, 0, 0, 0, 0, 0, 0 };
            setup.Songs[0].Appeal = new double[] { 0, 1, 0, 0, 0, 0, 0 };
            ShowState state = Shows.Start(setup);

            Assert.Equal(-15, Engine.ExpectedRoll(state, 0, 5).Appeal, 9);
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

        [Fact]
        public void Alignment_ComparesLeansWithinEachAxis()
        {
            // Same lean on every axis, different levels
            Assert.Equal(1, Engine.Alignment(new double[] { 1, 0, 3, 1, 1, 0, 0 }, new double[] { 3, 2, 5, 3, 2, 1, 1 }), 9);
            Assert.Equal(-1, Engine.Alignment(new double[] { 1, 0, 0, 0, 0, 0, 0 }, new double[] { 0, 1, 0, 0, 0, 0, 0 }), 9);
            // A flat lean (the same value within each axis) aligns with nothing
            Assert.Equal(0, Engine.Alignment(new double[] { 2, 2, 5, 5, 1, 1, 1 }, new double[] { 1, 0, 0, 0, 0, 0, 0 }));
            Assert.Equal(0, Engine.Alignment(new double[7], new double[7]));
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

        [Fact]
        public void GenreFlow_AddsHypeWhenTheGenreRepeats_NotOnTheFirstSong()
        {
            ShowSetup setup = Shows.Setup(skill: 45);
            setup.Songs[0].Genre = setup.Songs[1].Genre = "Pop";
            ShowState state = Shows.Start(setup);
            Assert.Equal(0, PlayOriginals(state).GenreFlow);
            // Fresh again, so the second song rolls par as well
            for (int m = 0; m < state.Energy.Length; m++)
                state.Energy[m] = 100;
            SongResult second = PlayOriginals(state);
            Assert.Equal(3, second.GenreFlow);
            Assert.Equal(53, state.Hype, 9);
        }

        [Fact]
        public void ShowQuality_ScalesGainsOnly()
        {
            ShowSetup setup = Shows.Setup(skill: 65);
            setup.CapacityFilled = 0;
            setup.Production = 0;
            SongResult gain = PlayOriginals(Shows.Start(setup));
            Assert.Equal(0.375, gain.ShowMultiplier, 9);
            Assert.Equal(3.75, gain.Change, 9);

            setup = Shows.Setup(skill: 25);
            setup.CapacityFilled = 0;
            setup.Production = 0;
            SongResult loss = PlayOriginals(Shows.Start(setup));
            Assert.Equal(1, loss.ShowMultiplier);
            Assert.Equal(-10, loss.Change, 9);
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
        public void BelowFullHype_PerformersPayByRowAndRoll_BenchRecovers()
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
        public void AtOrAbove100_TheCrowdLiftsPerformers()
        {
            ShowState state = Shows.Start(Shows.Setup(skill: 45));
            state.Hype = 140;
            state.Energy[0] = 50;
            SongResult result = Engine.Play(state, Engine.DefaultLineup(state));
            Assert.True(result.HypeAfter >= 100);
            Assert.Equal(50 + 2 + 0.1 * (result.HypeAfter - 100), state.Energy[0], 9);
            Assert.Equal(100, state.Energy[1]);
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
        public void Mc_RestsEveryone_AndCountsDownAbsences()
        {
            ShowState state = AfterFirstSong();
            state.Energy[0] = 50;
            state.Absence[3] = 2;
            state.Exhausted[2] = true;
            state.Energy[2] = 25;
            Engine.CallMc(state, 4);
            Assert.Equal(60, state.Energy[0], 9);
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
        public void Mc_NeedsAnAvailableHost()
        {
            ShowState state = AfterFirstSong();
            state.Absence[4] = 1;
            Assert.Throws<InvalidOperationException>(() => Engine.CallMc(state, 4));
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
}
