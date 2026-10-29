using ConcertHype.Model;
using System.Linq;
using Xunit;

namespace ConcertHype.Tests
{
    public class StageViewTests
    {
        private static string Name(int member) => "M" + member;

        /// <summary>
        /// Six places (rows of 1, 2 and 3) with the spread on and no accidents.
        /// </summary>
        private static ShowState SixPlaces(Config config = null, int members = 8)
        {
            config ??= new Config { PBase = 0, PRoll = 0, PTired = 0 };
            ShowSetup setup = Shows.Setup(members: members, slots: 6, skill: 55);
            return Shows.Start(setup, config, seed: 4);
        }

        private static StageView PlayFirstSong(ShowState state, out SongResult result)
        {
            result = Engine.Play(state, Engine.DefaultLineup(state));
            return StageView.Build(state, result, Name);
        }

        [Fact]
        public void EachPlace_ShowsItsRoll_OutcomeAndWheel()
        {
            ShowState state = SixPlaces();
            StageView view = PlayFirstSong(state, out SongResult result);

            Assert.True(view.Shown);
            Assert.Equal(new[] { 0, 1, 1, 2, 2, 2 }, view.Slots.Select(s => s.Row));
            for (int j = 0; j < view.Slots.Count; j++)
            {
                StageSlotView place = view.Slots[j];
                SlotResult slot = result.Slots[j];
                Assert.Equal(slot.Member, place.Member);
                Assert.Equal(System.Math.Round(slot.Roll).ToString(), place.Roll);
                Assert.Equal(Outcomes.Of(state.Config, slot.Roll), place.Outcome);
                Assert.Equal(Outcomes.Chances(state.Config, slot.Parts.Expected), place.Chances);
                Assert.Equal(7200 + 3.6 * Outcomes.WheelPercent(state.Config, slot.Parts.Expected, slot.Roll), place.Angle, 9);
                Assert.Equal(slot.EnergyBefore, place.EnergyBefore);
                Assert.Equal(slot.EnergyAfter, place.EnergyAfter);
                Assert.False(place.Hurt);
            }
        }

        [Fact]
        public void TheWheels_LandBackRowFirst_TheCenterLast_BeforeTheSongEnds()
        {
            StageView view = PlayFirstSong(SixPlaces(), out _);
            double[] landings = view.Slots.Select(s => s.LandAt).ToArray();

            // Places 3-5 are the back row, left to right; place 0 is the center
            Assert.Equal(StageView.FirstLanding, landings[3], 9);
            Assert.True(landings[3] < landings[4] && landings[4] < landings[5]);
            Assert.True(landings[5] < landings[1] && landings[1] < landings[2]);
            Assert.True(landings[2] < landings[0]);
            Assert.Equal(StageView.LastLanding, landings[0], 9);
            Assert.True(StageView.LastLanding < 1);
        }

        [Fact]
        public void AnEmptyPlace_HasNoRollOrWheel_AndTheOthersStillLandInOrder()
        {
            // Three idols for six places: with M1 hurt, nobody can fill hers, and the bench is empty
            ShowState state = SixPlaces(members: 6);
            state.Absence[1] = 3;
            StageView view = PlayFirstSong(state, out _);

            StageSlotView empty = view.Slots[1];
            Assert.False(empty.HasRoll);
            Assert.Equal("", empty.Roll);
            Assert.Equal(0, empty.LandAt);
            Assert.Equal(StageView.LastLanding, view.Slots[0].LandAt, 9);
            Assert.True(view.Slots[2].LandAt < view.Slots[0].LandAt);
        }

        [Fact]
        public void AHurtIdol_ShowsTheHurtIconWithHowLongSheIsOut()
        {
            Config config = new Config { PBase = 1, PMax = 1, AbsenceMin = 2, AbsenceMax = 2 };
            StageView view = PlayFirstSong(SixPlaces(config), out _);
            Assert.All(view.Slots, place =>
            {
                Assert.True(place.Hurt);
                Assert.Equal("Hurt: out for 2 songs. A talk break counts as one song.", place.HurtTip);
            });
        }

        [Fact]
        public void TheTip_NamesTheOutcomeAndHerChances()
        {
            ShowState state = SixPlaces(new Config { RollSigma = 0, PBase = 0, PRoll = 0, PTired = 0 });
            Shows.SetSkill(state.Setup, 0, 85);
            StageView view = PlayFirstSong(state, out SongResult result);
            string roll = System.Math.Round(result.Slots[0].Roll).ToString();
            Assert.Equal($"M0: Critical Success, with a roll of {roll} (expected {roll}). Her chances were 0% critical failure, 0% failure, 0% success and 100% critical success.", view.Slots[0].Tip);
        }

        /// <summary>
        /// The rows of a single's triangle with this many places, in place order (rows of 1, 2, 3, 4 and 5).
        /// </summary>
        private static int[] Triangle(int places) => Enumerable.Range(0, places).Select(Slot.RowOf).ToArray();

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(10)]
        [InlineData(12)]
        [InlineData(15)]
        public void TheLayout_KeepsEveryItemInsideTheFreeArea_ApartFromTheOthers(int count)
        {
            int[] rows = Triangle(count);
            StagePlace[] places = StageView.Layout(rows);
            double scale = places[0].Scale, half = StageView.ItemWidth * scale / 2, halfHeight = StageView.ItemHeight * scale / 2;
            Assert.All(places, place =>
            {
                Assert.Equal(scale, place.Scale);
                Assert.InRange(place.CenterX - half, StageView.AreaLeft - 1e-9, StageView.AreaRight - 2 * half + 1e-9);
                Assert.InRange(place.CenterY - halfHeight, StageView.AreaTop - 1e-9, StageView.AreaBottom - 2 * halfHeight + 1e-9);
            });
            for (int a = 0; a < places.Length; a++)
            {
                for (int b = a + 1; b < places.Length; b++)
                {
                    bool apartSideways = System.Math.Abs(places[a].CenterX - places[b].CenterX) >= 2 * half + StageView.MinGap - 1e-9;
                    bool apartDown = System.Math.Abs(places[a].CenterY - places[b].CenterY) >= 2 * halfHeight + StageView.MinGap - 1e-9;
                    Assert.True(apartSideways || apartDown, $"places {a} and {b} of {count} are too close");
                }
            }
            // Each row centred on the screen's middle
            foreach (IGrouping<int, int> row in Enumerable.Range(0, count).GroupBy(j => rows[j]))
                Assert.Equal(512, row.Average(j => places[j].CenterX), 6);
        }

        [Fact]
        public void TheLayout_ShrinksOnlyFormationsOfFourRowsOrMore_AndCapsTheSpacingForSmallFormations()
        {
            Assert.Equal(1, StageView.Layout(Triangle(6))[0].Scale);
            Assert.InRange(StageView.Layout(Triangle(10))[0].Scale, 0.94, 0.95);
            double five = StageView.Layout(Triangle(15))[0].Scale;
            Assert.InRange(five, 0.72, 0.75);

            StagePlace[] three = StageView.Layout(Triangle(6));
            Assert.Equal(StageView.MaxRowPitch, three[1].CenterY - three[0].CenterY, 6);
            Assert.Equal(StageView.MaxPlacePitch, three[2].CenterX - three[1].CenterX, 6);
            StagePlace[] one = StageView.Layout(Triangle(1));
            Assert.Equal(512, one[0].CenterX, 6);
            Assert.Equal((StageView.AreaTop + StageView.AreaBottom) / 2, one[0].CenterY, 6);
        }

        private static double[] Times(double until) => Enumerable.Range(0, (int)(until * 1000) + 1).Select(ms => ms / 1000.0).ToArray();

        [Theory]
        [InlineData(Outcome.Failure)]
        [InlineData(Outcome.Success)]
        [InlineData(Outcome.CriticalSuccess)]
        [InlineData(Outcome.CriticalFailure)]
        public void ANumber_SlamsDownFromLarge_DippingOnce_ThenSettles(Outcome outcome)
        {
            NumberPose start = StageView.NumberMotion(0, outcome);
            Assert.Equal(StageView.SlamFrom, start.Scale, 6);
            Assert.Equal(0, start.Alpha);
            Assert.Equal(1, start.Flash);
            Assert.Equal(1, StageView.NumberMotion(StageView.FadeInSeconds, outcome).Alpha);
            Assert.Equal(0, StageView.NumberMotion(StageView.FlashSeconds, outcome).Flash);

            double[] slam = Times(StageView.SlamSeconds).Select(t => StageView.NumberMotion(t, outcome).Scale).ToArray();
            Assert.Equal(1, slam.Last(), 6);
            // Under its size once: one stretch of the slam below 1, not lower than 0.8
            int dips = slam.Zip(slam.Skip(1), (a, b) => a >= 1 && b < 1).Count(started => started);
            Assert.Equal(1, dips);
            Assert.InRange(slam.Min(), 0.8, 1);

            NumberPose settled = StageView.NumberMotion(StageView.SlamSeconds + StageView.AfterSeconds + 0.01, outcome);
            Assert.Equal(1, settled.Scale, 6);
            Assert.Equal(0, settled.OffsetX, 6);
        }

        [Fact]
        public void ACriticalSuccess_Pulses_ACriticalFailure_Shakes_OthersNeither()
        {
            double[] after = Times(StageView.AfterSeconds).Select(t => StageView.SlamSeconds + t).ToArray();
            double peak = after.Max(t => StageView.NumberMotion(t, Outcome.CriticalSuccess).Scale);
            Assert.Equal(StageView.PulseScale, peak, 3);
            Assert.All(after, t => Assert.Equal(0, StageView.NumberMotion(t, Outcome.CriticalSuccess).OffsetX));

            double[] shake = after.Select(t => StageView.NumberMotion(t, Outcome.CriticalFailure).OffsetX).ToArray();
            Assert.All(shake, x => Assert.InRange(x, -StageView.ShakePixels, StageView.ShakePixels));
            Assert.True(shake.Max() > 2 && shake.Min() < -2);
            Assert.All(after, t => Assert.Equal(1, StageView.NumberMotion(t, Outcome.CriticalFailure).Scale, 6));

            foreach (Outcome plain in new[] { Outcome.Success, Outcome.Failure })
            {
                Assert.All(after, t =>
                {
                    Assert.Equal(1, StageView.NumberMotion(t, plain).Scale, 6);
                    Assert.Equal(0, StageView.NumberMotion(t, plain).OffsetX);
                });
            }
        }

        [Fact]
        public void Critical_IsTheTwoCriticalOutcomes_ForAFilledPlace()
        {
            Assert.True(new StageSlotView { Member = 0, Outcome = Outcome.CriticalSuccess }.Critical);
            Assert.True(new StageSlotView { Member = 0, Outcome = Outcome.CriticalFailure }.Critical);
            Assert.False(new StageSlotView { Member = 0, Outcome = Outcome.Success }.Critical);
            Assert.False(new StageSlotView { Member = 0, Outcome = Outcome.Failure }.Critical);
            Assert.False(new StageSlotView { Outcome = Outcome.CriticalFailure }.Critical);
        }

        [Fact]
        public void AHurtIdolsPicture_FlashesRed_ThenFades()
        {
            Assert.Equal(1, StageView.HurtFlash(0));
            Assert.Equal(0.5, StageView.HurtFlash(StageView.HurtFlashSeconds / 2), 6);
            Assert.Equal(0, StageView.HurtFlash(StageView.HurtFlashSeconds));
            Assert.Equal(0, StageView.HurtFlash(5));
        }

        [Fact]
        public void ATalkBreak_ShowsNoStage()
        {
            Assert.False(StageView.Build(SixPlaces(), null, Name).Shown);
        }

        [Fact]
        public void EveryOutcome_HasAName()
        {
            Assert.Equal(new[] { "Critical Failure", "Failure", "Success", "Critical Success" },
                System.Enum.GetValues(typeof(Outcome)).Cast<Outcome>().Select(StageView.OutcomeName));
        }
    }
}
