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
        public void EachPlace_ShowsItsOutcomeAndWheel()
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
            Assert.Equal("", empty.Tip);
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
        public void TheTip_NamesHerAndTheOutcome_WithNoNumbers()
        {
            ShowState state = SixPlaces(new Config { RollSigma = 0, PBase = 0, PRoll = 0, PTired = 0 });
            Shows.SetSkill(state.Setup, 0, 85);
            StageView view = PlayFirstSong(state, out _);
            Assert.Equal("M0: Critical Success", view.Slots[0].Tip);
        }

        [Fact]
        public void EachOutcome_HasItsOwnEffect()
        {
            Outcome[] outcomes = System.Enum.GetValues(typeof(Outcome)).Cast<Outcome>().ToArray();
            Assert.Equal(outcomes.Length, outcomes.Select(StageView.EffectOf).Distinct().Count());
            Assert.Equal(IconKind.CriticalSuccess, StageView.EffectOf(Outcome.CriticalSuccess));
            Assert.Equal(IconKind.Success, StageView.EffectOf(Outcome.Success));
            Assert.Equal(IconKind.Failure, StageView.EffectOf(Outcome.Failure));
            Assert.Equal(IconKind.CriticalFailure, StageView.EffectOf(Outcome.CriticalFailure));
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

        private static bool IsCritical(Outcome outcome) => outcome == Outcome.CriticalSuccess || outcome == Outcome.CriticalFailure;

        [Theory]
        [InlineData(Outcome.CriticalSuccess)]
        [InlineData(Outcome.CriticalFailure)]
        public void ACriticalEffect_SlamsDownFromLarge_DippingOnce_ToItsMomentSize(Outcome outcome)
        {
            EffectPose start = StageView.EffectMotion(0, outcome);
            Assert.Equal(StageView.SlamFrom * StageView.CriticalMomentScale, start.Scale, 6);
            Assert.Equal(0, start.Alpha);
            Assert.Equal(1, StageView.EffectMotion(StageView.FadeInSeconds, outcome).Alpha);

            double[] slam = Times(StageView.SlamSeconds).Select(t => StageView.Slam(t)).ToArray();
            Assert.Equal(1, slam.Last(), 6);
            // Under its size once: one stretch of the slam below 1, not lower than 0.8
            int dips = slam.Zip(slam.Skip(1), (a, b) => a >= 1 && b < 1).Count(started => started);
            Assert.Equal(1, dips);
            Assert.InRange(slam.Min(), 0.8, 1);
            Assert.Equal(StageView.CriticalMomentScale, StageView.EffectMotion(StageView.SlamSeconds + StageView.AfterSeconds + 0.01, outcome).Scale, 6);
        }

        [Fact]
        public void ASuccess_PopsInALittleTooBig_ThenBobsUpOnce()
        {
            Assert.Equal(0, StageView.EffectMotion(0, Outcome.Success).Scale, 6);
            double[] pop = Times(StageView.PopSeconds).Select(t => StageView.EffectMotion(t, Outcome.Success).Scale / StageView.MomentScale).ToArray();
            Assert.InRange(pop.Max(), 1.05, 1.15);
            Assert.Equal(1, pop.Last(), 6);
            Assert.All(Times(StageView.PopSeconds), t => Assert.Equal(1, StageView.EffectMotion(t, Outcome.Success).Alpha));

            double[] bob = Times(StageView.AfterSeconds).Select(t => StageView.EffectMotion(StageView.PopSeconds + t, Outcome.Success).OffsetY).ToArray();
            Assert.Equal(-StageView.BobPixels, bob.Min(), 2);
            Assert.All(bob, y => Assert.True(y <= 1e-9));
        }

        [Fact]
        public void AFailure_FadesIn_ThenSinksAndComesBack()
        {
            Assert.Equal(0, StageView.EffectMotion(0, Outcome.Failure).Alpha);
            Assert.Equal(0.5, StageView.EffectMotion(StageView.FailFadeSeconds / 2, Outcome.Failure).Alpha, 6);
            Assert.Equal(StageView.MomentScale, StageView.EffectMotion(0, Outcome.Failure).Scale, 6);

            double[] sink = Times(2 * StageView.AfterSeconds).Select(t => StageView.EffectMotion(StageView.FailFadeSeconds + t, Outcome.Failure).OffsetY).ToArray();
            Assert.Equal(StageView.SinkPixels, sink.Max(), 2);
            Assert.All(sink, y => Assert.True(y >= -1e-9));
            Assert.Equal(0, sink.Last(), 6);
        }

        [Theory]
        [InlineData(Outcome.Failure)]
        [InlineData(Outcome.Success)]
        [InlineData(Outcome.CriticalSuccess)]
        [InlineData(Outcome.CriticalFailure)]
        public void EveryEffect_HoldsItsMoment_ThenShrinksIntoHerMark_AndStaysThere(Outcome outcome)
        {
            double moment = IsCritical(outcome) ? StageView.CriticalMomentScale : StageView.MomentScale;
            Assert.Equal(moment, StageView.EffectMotion(StageView.HoldSeconds, outcome).Scale, 6);
            double[] shrink = Times(StageView.ShrinkSeconds).Select(t => StageView.EffectMotion(StageView.HoldSeconds + t, outcome).Scale).ToArray();
            Assert.All(shrink.Zip(shrink.Skip(1), (a, b) => b <= a + 1e-9), Assert.True);

            foreach (double t in new[] { StageView.SettledSeconds, StageView.SettledSeconds + 2 })
            {
                EffectPose mark = StageView.EffectMotion(t, outcome);
                Assert.Equal(1, mark.Scale, 6);
                Assert.Equal(1, mark.Alpha);
                Assert.Equal(0, mark.OffsetX, 6);
                Assert.Equal(0, mark.OffsetY, 6);
            }
        }

        [Fact]
        public void ACriticalSuccess_Pulses_ACriticalFailure_Shakes_OthersNeither()
        {
            double[] after = Times(StageView.AfterSeconds).Select(t => StageView.SlamSeconds + t).ToArray();
            double peak = after.Max(t => StageView.EffectMotion(t, Outcome.CriticalSuccess).Scale);
            Assert.Equal(StageView.PulseScale * StageView.CriticalMomentScale, peak, 3);
            Assert.All(after, t => Assert.Equal(0, StageView.EffectMotion(t, Outcome.CriticalSuccess).OffsetX));

            double[] shake = after.Select(t => StageView.EffectMotion(t, Outcome.CriticalFailure).OffsetX).ToArray();
            Assert.All(shake, x => Assert.InRange(x, -StageView.ShakePixels, StageView.ShakePixels));
            Assert.True(shake.Max() > 2 && shake.Min() < -2);
            Assert.All(after, t => Assert.Equal(StageView.CriticalMomentScale, StageView.EffectMotion(t, Outcome.CriticalFailure).Scale, 6));

            foreach (Outcome plain in new[] { Outcome.Success, Outcome.Failure })
                Assert.All(Times(StageView.SettledSeconds), t => Assert.Equal(0, StageView.EffectMotion(t, plain).OffsetX));
        }

        [Fact]
        public void ACriticalSuccess_SendsFourSparklesOut_OnePerDiagonal_ThatFadeAway()
        {
            Assert.Equal(4, StageView.SparkleCount);
            for (int k = 0; k < StageView.SparkleCount; k++)
            {
                Assert.Equal(0, StageView.SparkleMotion(0, k).Alpha);
                Assert.Equal(0, StageView.SparkleMotion(StageView.SparkleDelay + StageView.SparkleSeconds, k).Alpha);
                EffectPose mid = StageView.SparkleMotion(StageView.SparkleDelay + StageView.SparkleSeconds / 2, k);
                Assert.InRange(mid.Alpha, 0.4, 0.6);
                Assert.True(mid.Scale < 1);
                double[] away = Times(StageView.SparkleSeconds).Select(t => System.Math.Abs(StageView.SparkleMotion(StageView.SparkleDelay + t, k).OffsetX)).ToArray();
                Assert.All(away.Zip(away.Skip(1), (a, b) => b >= a - 1e-9 || b == 0), Assert.True);
                Assert.True(away.Max() <= StageView.SparkleDistance + 1e-9);
            }
            EffectPose[] sparkles = Enumerable.Range(0, StageView.SparkleCount).Select(k => StageView.SparkleMotion(StageView.SparkleDelay + 0.3, k)).ToArray();
            Assert.Equal(4, sparkles.Select(p => (System.Math.Sign(p.OffsetX), System.Math.Sign(p.OffsetY))).Distinct().Count());
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
