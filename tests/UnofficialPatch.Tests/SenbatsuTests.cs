using System.Collections.Generic;
using System.Linq;
using Xunit;
using static data_girls._paramType;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// A single's senbatsu stats weigh each row of the line-up equally, over the rows the line-up fills
    /// (1, 2, 3, 4, 5 slots per row).
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

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(1, 100f)]
        [InlineData(2, 50f)]
        [InlineData(3, 50f)]
        [InlineData(4, 100f / 3f)]
        [InlineData(6, 100f / 3f)]
        [InlineData(7, 25f)]
        [InlineData(10, 25f)]
        [InlineData(11, 20f)]
        [InlineData(15, 20f)]
        public void RowWeight_IsShareOfTheRowsTheLineupFills(int idols, float weight)
        {
            data_girls.girls[] girls = Enumerable.Range(0, idols).Select(_ => TestGame.Idol()).ToArray();
            Assert.Equal(weight, singles__single_SenbatsuCalcParam.Infix(Lineup(girls)), 4);
        }

        [Fact]
        public void RowWeight_CountsOnlyFilledSlots()
        {
            // Center and one idol in the third row: 2 idols, 2 rows
            List<data_girls.girls> slots = Lineup(TestGame.Idol());
            slots[3] = TestGame.Idol();
            Assert.Equal(50f, singles__single_SenbatsuCalcParam.Infix(slots), 4);
        }

        [Fact]
        public void FullRows_GiveTheIdolsAverage()
        {
            // Center 60, second row 40 + 80: each row averages 0.6
            singles._single single = Single(Hired(60f), Hired(40f), Hired(80f));
            Assert.Equal(60f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void SmallLineup_IsNotDilutedByAgencySize()
        {
            // The game divided by the rows the whole agency could fill (5 rows for 15 idols): 70 / 5 = 14
            for (int i = 0; i < 14; i++)
                Hired(10f);
            singles._single single = Single(Hired(70f));
            Assert.Equal(70f, single.GetSenbatsuParamValue(cute), 3);
        }

        [Fact]
        public void PartlyFilledRow_CountsItsEmptySlotsAsZero()
        {
            // Center 50, one idol of 50 in the second row: rows 0.5 and 0.25, each worth 50
            singles._single single = Single(Hired(50f), Hired(50f));
            Assert.Equal(37.5f, single.GetSenbatsuParamValue(cute), 3);
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
    }
}
