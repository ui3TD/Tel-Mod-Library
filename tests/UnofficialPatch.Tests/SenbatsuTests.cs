using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static data_girls._paramType;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A single's senbatsu stats weigh each row of the line-up equally (1, 2, 3, 4, 5 slots per row), over the
    /// rows the agency's idols can fill completely. A row the agency can't fill doesn't count against the single,
    /// but a row left short because idols are missing (e.g. ill) does.
    /// </summary>
    public class SenbatsuTests
    {
        public SenbatsuTests() => TestGame.Reset();

        /// <summary>
        /// The 15 senbatsu slots: the center, then rows of 2, 3, 4 and 5. Missing idols are empty slots.
        /// </summary>
        private static List<data_girls.girls> Lineup(params data_girls.girls[] girls)
        {
            List<data_girls.girls> slots = girls.ToList();
            while (slots.Count < 15)
                slots.Add(null);
            return slots;
        }

        private static singles._single Single(params data_girls.girls[] girls)
        {
            singles._single single = new();
            single.girls.AddRange(Lineup(girls));
            singles.Singles.Add(single);
            TestGame.MainGroup.Singles.Add(single);
            return single;
        }

        private static data_girls.girls Hired(float statValue) => TestGame.Hire(TestGame.Idol(statValue));

        private static data_girls.girls[] HiredMany(int count, float statValue) =>
            Enumerable.Range(0, count).Select(_ => Hired(statValue)).ToArray();

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(1, 100f)]
        [InlineData(2, 100f)]
        [InlineData(3, 50f)]
        [InlineData(5, 50f)]
        [InlineData(6, 100f / 3f)]
        [InlineData(9, 100f / 3f)]
        [InlineData(10, 25f)]
        [InlineData(14, 25f)]
        [InlineData(15, 20f)]
        [InlineData(40, 20f)]
        public void RowWeight_IsShareOfTheRowsTheAgencyCanFill(int idols, float weight)
        {
            // The 1.3.1 formula gave 3 rows for 11-14 idols and 4 for 15
            Assert.Equal(weight, singles__single_SenbatsuCalcParam.Infix(idols), 4);
        }

        [Fact]
        public void FullRows_GiveTheIdolsAverage()
        {
            // Center 60, second row 40 + 80: each row averages 0.6
            singles._single single = Single(Hired(60f), Hired(40f), Hired(80f));
            Assert.Equal(60f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void RowTheAgencyCantFill_DoesNotCountAgainstTheSingle()
        {
            // 14 idols fill 4 rows; the game counted 5 (48). The 4 in the last row add on top.
            singles._single single = Single(HiredMany(14, 50f));
            Assert.Equal(60f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void IdolsMissingFromTheLineup_CountAgainstTheSingle()
        {
            // 15 idols fill 5 rows; with 2 of them ill, the last row has 3 of 5
            data_girls.girls[] agency = HiredMany(15, 50f);
            singles._single single = Single(agency.Take(13).ToArray());
            Assert.Equal(46f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void SmallLineup_InABigAgency_IsDiluted()
        {
            data_girls.girls center = Hired(70f);
            HiredMany(14, 10f);
            singles._single single = Single(center);
            Assert.Equal(14f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void MainGroup_CountsTheWholeAgency()
        {
            // The main group's single counts the other groups' idols too: 3 idols, 2 rows
            Groups._group other = new() { ID = 1, Girls = new List<data_girls.girls>() };
            Groups.Groups_.Add(other);
            other.Girls.Add(TestGame.Idol());
            other.Girls.Add(TestGame.Idol());
            singles._single single = Single(Hired(70f));
            Assert.Equal(35f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void GraduatedIdols_AreNotCounted()
        {
            data_girls.girls center = Hired(70f);
            foreach (data_girls.girls graduate in HiredMany(2, 50f))
                graduate.status = data_girls._status.graduated;
            singles._single single = Single(center);
            Assert.Equal(70f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Theory]
        [InlineData(cute)]
        [InlineData(cool)]
        [InlineData(pretty)]
        [InlineData(smart)]
        [InlineData(funny)]
        [InlineData(vocal)]
        [InlineData(dance)]
        public void ParamValue_UsesTheRequestedStat(data_girls._paramType type)
        {
            // The game always used Cute
            data_girls.girls center = Hired(10f);
            center.getParam(type)._val = 90f;
            singles._single single = Single(center);
            Assert.Equal(90f, single.GetSenbatsuParamValue(type), 3);
        }

        [Fact]
        public void ParamValue_KeepsTheVibeBonus()
        {
            // The selected vibe (Sexy) adds 25%
            singles._single single = Single(Hired(40f));
            Assert.Equal(50f, single.GetSenbatsuParamValue(sexy), 3);
        }

        [Fact]
        public void ParamValue_IsCappedAt100()
        {
            singles._single single = Single(Hired(95f));
            Assert.Equal(100f, single.GetSenbatsuParamValue(sexy), 3);
        }

        /// <summary>
        /// The mod changes one instruction (cute becomes the Type argument); transpiling again changes nothing.
        /// </summary>
        [Fact]
        public void ParamValueTranspiler_ChangesOneInstruction_AndOnlyOnce()
        {
            System.Reflection.MethodInfo method = AccessTools.Method(typeof(singles._single), nameof(singles._single.GetSenbatsuParamValue));
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method);
            List<CodeInstruction> once = singles__single_GetSenbatsuParamValue.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList();
            List<CodeInstruction> twice = singles__single_GetSenbatsuParamValue.Transpiler(once).ToList();

            Assert.Equal(original.Count, once.Count);
            Assert.Equal(1, Enumerable.Range(0, original.Count).Count(i => original[i].ToString() != once[i].ToString()));
            Assert.Equal(once.Select(ci => ci.ToString()), twice.Select(ci => ci.ToString()));
        }
    }
}
