using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A new cafe dish starts at 100% minus the novelty the idol's other dishes have lost. The game added the
    /// lost novelty instead, so every new dish started at 100%.
    /// </summary>
    public class CafeNoveltyTests
    {
        public CafeNoveltyTests() => TestGame.Reset();

        private static int Novelty(params float[] dishes)
        {
            data_girls.girls girl = TestGame.Idol();
            girl.id = 1;
            Cafes._cafe cafe = new();
            foreach (float novelty in dishes)
                cafe.Dishes.Add(new Cafes._cafe._dish { Girl = girl.id, Novelty = novelty });
            // Another idol's stale dish doesn't count
            cafe.Dishes.Add(new Cafes._cafe._dish { Girl = 2, Novelty = 0f });
            return cafe.GetNovelty(girl);
        }

        [Theory]
        [InlineData(100, new float[0])]
        [InlineData(100, new[] { 100f, 100f })]
        [InlineData(70, new[] { 70f })]
        [InlineData(40, new[] { 70f, 70f })]
        [InlineData(55, new[] { 90f, 85f, 80f })]
        [InlineData(0, new[] { 10f, 10f })]
        public void NewDish_LosesWhatTheOtherDishesLost(int expected, float[] dishes)
        {
            Assert.Equal(expected, Novelty(dishes));
        }

        private static MethodInfo Original => AccessTools.Method(typeof(Cafes._cafe), nameof(Cafes._cafe.GetNovelty));

        [Fact]
        public void Transpiler_AddsOneNegation_AndTwiceChangesNothing()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(Original);
            List<CodeInstruction> once = Cafes__cafe_GetNovelty.Transpiler(PatchProcessor.GetOriginalInstructions(Original)).ToList();
            List<string> onceText = once.Select(ci => ci.ToString()).ToList();
            List<CodeInstruction> twice = Cafes__cafe_GetNovelty.Transpiler(once).ToList();

            Assert.Equal(original.Count + 1, once.Count);
            Assert.Single(once, ci => ci.opcode == OpCodes.Neg);
            Assert.Equal(onceText, twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void Transpiler_UnexpectedCode_WarnsAndLeavesItUnchanged()
        {
            // Another mod already rewrote the formula: dishes * 100 + total
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(Original);
            codes.First(ci => ci.opcode == OpCodes.Sub).opcode = OpCodes.Add;
            List<string> before = codes.Select(ci => ci.ToString()).ToList();

            List<CodeInstruction> result = Cafes__cafe_GetNovelty.Transpiler(codes).ToList();

            Assert.Equal(before, result.Select(ci => ci.ToString()));
            Assert.Single(Log.Of(LogType.Warning));
        }
    }

    /// <summary>
    /// Depressed idols can work in the cafe but stay depressed. The game set them to "practice" when the shift started
    /// and to normal when it ended, curing their depression.
    /// </summary>
    public class CafeWaitstaffTests
    {
        public CafeWaitstaffTests() => TestGame.Reset();

        private static data_girls.girls StaffIdol(Cafes._cafe cafe, data_girls._status status, int id)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: "Idol " + id));
            girl.id = id;
            girl.status = status;
            cafe.WaitStaff.Add(girl);
            return girl;
        }

        private static void NextDay(Cafes._cafe cafe)
        {
            agency._room room = new() { roomObj = TestGame.Component<GameObject>() };
            TestGame.CallPrivate(TestGame.Component<Cafes>(), typeof(Cafes), nameof(Cafes.RenderCafe), room, cafe);
        }

        [Theory]
        [InlineData(Cafes._cafe._cafe_prio.high)]
        [InlineData(Cafes._cafe._cafe_prio.normal)]
        public void DepressedIdol_Works_AndStaysDepressed(Cafes._cafe._cafe_prio priority)
        {
            Cafes._cafe cafe = new() { Cafe_Prio = priority };
            data_girls.girls normal = StaffIdol(cafe, data_girls._status.normal, 1);
            data_girls.girls depressed = StaffIdol(cafe, data_girls._status.depressed, 2);

            NextDay(cafe);
            NextDay(cafe);

            Assert.Equal(new[] { normal, depressed }.ToHashSet(), cafe.WorkingGirls.ToHashSet());
            Assert.Equal(data_girls._status.practice, normal.status);
            Assert.Equal(data_girls._status.depressed, depressed.status);
        }

        [Fact]
        public void ShiftEnd_LeavesDepressedIdolsDepressed()
        {
            Cafes._cafe cafe = new();
            data_girls.girls working = TestGame.Idol();
            working.status = data_girls._status.practice;
            data_girls.girls depressed = TestGame.Idol();
            depressed.status = data_girls._status.depressed;
            cafe.WorkingGirls.AddRange(new[] { working, depressed });

            cafe.StopWorking();

            Assert.Empty(cafe.WorkingGirls);
            Assert.Equal(data_girls._status.normal, working.status);
            Assert.Equal(data_girls._status.depressed, depressed.status);
        }

        [Fact]
        public void OtherIdols_StillWork()
        {
            Cafes._cafe cafe = new() { Cafe_Prio = Cafes._cafe._cafe_prio.normal };
            data_girls.girls[] staff = Enumerable.Range(1, 3).Select(id => StaffIdol(cafe, data_girls._status.normal, id)).ToArray();

            NextDay(cafe);

            Assert.Equal(staff.ToHashSet(), cafe.WorkingGirls.ToHashSet());
            Assert.All(staff, girl => Assert.Equal(data_girls._status.practice, girl.status));
        }

        public static TheoryData<string> Patches => new() { nameof(Cafes_RenderCafe), nameof(Cafes__cafe_StopWorking) };

        private static (MethodInfo original, Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> transpiler, OpCode status) Patch(string name)
        {
            return name == nameof(Cafes_RenderCafe)
                ? (AccessTools.Method(typeof(Cafes), nameof(Cafes.RenderCafe)), Cafes_RenderCafe.Transpiler, OpCodes.Ldc_I4_1)
                : (AccessTools.Method(typeof(Cafes._cafe), nameof(Cafes._cafe.StopWorking)), Cafes__cafe_StopWorking.Transpiler, OpCodes.Ldc_I4_0);
        }

        private static readonly MethodInfo SetStatus = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.SetStatus));

        [Theory]
        [MemberData(nameof(Patches))]
        public void Transpiler_WrapsTheStatusChange_AndTwiceChangesNothing(string name)
        {
            (MethodInfo original, var transpiler, _) = Patch(name);
            List<CodeInstruction> before = PatchProcessor.GetOriginalInstructions(original);
            List<CodeInstruction> once = transpiler(PatchProcessor.GetOriginalInstructions(original)).ToList();
            List<string> onceText = once.Select(ci => ci.ToString()).ToList();
            List<CodeInstruction> twice = transpiler(once).ToList();

            Assert.Equal(before.Count, once.Count);
            Assert.Equal(before.Count(ci => Equals(ci.operand, SetStatus)) - 1, once.Count(ci => Equals(ci.operand, SetStatus)));
            Assert.Single(once, ci => ci.operand is MethodInfo m && m.Name == nameof(CafeShiftStatus.SetUnlessDepressed));
            Assert.Equal(onceText, twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }

        [Theory]
        [MemberData(nameof(Patches))]
        public void Transpiler_NoStatusChange_WarnsAndLeavesItUnchanged(string name)
        {
            (MethodInfo original, var transpiler, OpCode status) = Patch(name);
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(original);
            int call = codes.FindIndex(ci => Equals(ci.operand, SetStatus) && codes[codes.IndexOf(ci) - 1].opcode == status);
            codes[call - 1].opcode = OpCodes.Ldc_I4_7;
            List<string> before = codes.Select(ci => ci.ToString()).ToList();

            List<CodeInstruction> result = transpiler(codes).ToList();

            Assert.Equal(before, result.Select(ci => ci.ToString()));
            Assert.Single(Log.Of(LogType.Warning));
        }
    }

    /// <summary>
    /// The Most Prolific Group award or nomination and the cheaper events choice cut the regional saving by the
    /// same share as the production cost, so the discounts stack. The game took the full-size saving off the cut
    /// cost, so extra stops in a discounted region could cost nothing or less.
    /// </summary>
    public class TourSavingTests
    {
        // Asia: every stop but the dearest one costs half.
        private const float AsiaShare = 0.5f;
        private const int StopCost = 10_000_000;

        public TourSavingTests()
        {
            TestGame.Reset();
            staticVars.PlayerData.Chapter = tasks._chapter.NONE;
            SEvent_Tour.Const_CostCoeff = new List<int> { 1, 2, 3, 4, 5 };
            SEvent_Tour.Const_Area = new List<SEvent_Tour._area> { new() { Type = SEvent_Tour._areaType.asia, Saving = AsiaShare } };
        }

        private static SEvent_Tour.tour Tour(int stops)
        {
            SEvent_Tour.tour tour = new();
            for (int i = 0; i < stops; i++)
            {
                SEvent_Tour.country country = new() { Area = SEvent_Tour.Const_Area[0], Cost = StopCost };
                tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Country = country, Level = 1 });
            }
            TestGame.CallPrivate(tour, typeof(SEvent_Tour.tour), nameof(SEvent_Tour.tour.RecalcProductionCost));
            return tour;
        }

        private static void WinAward(bool nominationOnly)
        {
            Awards._award award = new() { Type = Awards._type.most_prolific_group, Year = TestGame.Today.Year };
            (nominationOnly ? Awards._Nominations : Awards._Awards).Add(award);
        }

        private static void ChooseCheaperEvents()
        {
            variables.variable.Add(new variables._variable { name = "CHEAPER_EVENTS", value = "true" });
            staticVars.PlayerData.Chapter = tasks._chapter.chapter_3;
        }

        [Theory]
        [InlineData(false, false, false, 1.0)]
        [InlineData(true, true, false, 0.75)]
        [InlineData(true, false, false, 0.5)]
        [InlineData(false, false, true, 0.75)]
        [InlineData(true, false, true, 0.375)]
        public void ThreeAsiaStops_CostTheCutShareOfTheDiscountedPrice(bool award, bool nominationOnly, bool cheaperEvents, double share)
        {
            if (award)
                WinAward(nominationOnly);
            if (cheaperEvents)
                ChooseCheaperEvents();

            // Without any cut: ¥30M, less ¥5M off each of the 2 cheaper stops
            const double discounted = 20_000_000;
            Assert.Equal((int)Math.Round(discounted * share), Tour(3).GetProductionCost());
            Assert.True(Tour(3).GetProductionCost() > Tour(2).GetProductionCost(), "an extra stop costs nothing");
        }

        [Fact]
        public void SkippedCostCalculation_KeepsTheSaving()
        {
            WinAward(nominationOnly: false);
            SEvent_Tour.tour tour = Tour(3);
            tour.Saving = tour.GetSaving();

            SEvent_Tour_tour_RecalcProductionCost.Postfix(tour, __runOriginal: false);

            Assert.Equal(tour.GetSaving(), tour.Saving);
        }

        [Fact]
        public void TwiceChangesNothing()
        {
            WinAward(nominationOnly: false);
            SEvent_Tour.tour tour = Tour(3);
            int saving = tour.Saving;

            SEvent_Tour_tour_RecalcProductionCost.Postfix(tour, __runOriginal: true);

            Assert.Equal(saving, tour.Saving);
        }

        [Fact]
        public void SavingAlreadyCutBySomethingElse_IsLeftAlone()
        {
            SEvent_Tour.tour tour = Tour(3);
            tour.ProductionCost = 15_000_000;
            tour.Saving = 4_000_000;

            SEvent_Tour_tour_RecalcProductionCost.Postfix(tour, __runOriginal: true);

            Assert.Equal(4_000_000, tour.Saving);
        }

        [Fact]
        public void ProductionCostChangedBySomethingElse_SetsTheShare()
        {
            // Another mod's postfix cut the cost to 80% before this one ran
            SEvent_Tour.tour tour = Tour(3);
            tour.ProductionCost = 24_000_000;

            SEvent_Tour_tour_RecalcProductionCost.Postfix(tour, __runOriginal: true);

            Assert.Equal(8_000_000, tour.Saving);
        }
    }

    /// <summary>
    /// Tour attendance drops 12% for each fame level the agency is short of twice the venue level, as the tour
    /// description says. The game compared fame points, so the penalty never applied.
    /// </summary>
    public class TourFamePenaltyTests
    {
        public TourFamePenaltyTests() => TestGame.Reset();

        private static void SetFame(long points) => resources.resource[(int)resources.type.fame] = points;

        [Theory]
        [InlineData(4, 1, 1f)]
        [InlineData(4, 3, 0.76f)]
        [InlineData(4, 5, 0.28f)]
        [InlineData(5, 5, 0.4f)]
        [InlineData(7, 5, 0.64f)]
        [InlineData(10, 5, 1f)]
        [InlineData(0, 1, 0.1f)]
        public void Penalty_UsesTheFameLevel(int fameLevel, int venueLevel, float expected)
        {
            SetFame((long)resources.FameLevelToPoints(fameLevel));
            Assert.Equal(expected, SEvent_Tour.GetFamePenalty(venueLevel), 4);
        }

        [Fact]
        public void Penalty_JustShortOfALevel_UsesTheLevelBelow()
        {
            SetFame((long)resources.FameLevelToPoints(5) - 1);
            Assert.Equal(1f - 0.12f * 6, SEvent_Tour.GetFamePenalty(5), 4);
        }

        private static MethodInfo Original => AccessTools.Method(typeof(SEvent_Tour), nameof(SEvent_Tour.GetFamePenalty));

        [Fact]
        public void Transpiler_SwapsOneCall_AndTwiceChangesNothing()
        {
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(Original);
            List<CodeInstruction> once = SEvent_Tour_GetFamePenalty.Transpiler(PatchProcessor.GetOriginalInstructions(Original)).ToList();
            List<string> onceText = once.Select(ci => ci.ToString()).ToList();
            List<CodeInstruction> twice = SEvent_Tour_GetFamePenalty.Transpiler(once).ToList();

            Assert.Equal(original.Count, once.Count);
            Assert.Equal(1, Enumerable.Range(0, original.Count).Count(i => original[i].ToString() != once[i].ToString()));
            Assert.Equal(onceText, twice.Select(ci => ci.ToString()));
            Assert.Empty(Log.Messages);
        }

        [Fact]
        public void Transpiler_SecondFameRead_WarnsAndLeavesItUnchanged()
        {
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(Original);
            codes.InsertRange(0, codes.Take(3).Select(ci => ci.Clone()).Append(new CodeInstruction(OpCodes.Pop)).ToList());
            List<string> before = codes.Select(ci => ci.ToString()).ToList();

            List<CodeInstruction> result = SEvent_Tour_GetFamePenalty.Transpiler(codes).ToList();

            Assert.Equal(before, result.Select(ci => ci.ToString()));
            Assert.Single(Log.Of(LogType.Warning));
        }
    }
}
