using System;
using TraitFix;
using Xunit;
using static data_girls._paramType;
using static traits._trait._type;
using static TraitFix.TraitsFix;

namespace TraitsFixTests
{
    /// <summary>
    /// Live Fast idols past their peak age roll for random stat loss twice as often.
    /// </summary>
    public class LiveFastAgingTests
    {
        public LiveFastAgingTests() => TestGame.Reset();

        [Fact]
        public void LiveFast_RollsAgainOnce()
        {
            data_girls.girls fast = TestGame.Hire(TestGame.Idol(Live_fast));
            TestGame.Hire(TestGame.Idol());

            Data_girls_AgeDeterioration.Postfix();

            Assert.Equal(new[] { fast }, Seams.Deteriorated);
            Assert.Equal(2, LIVEFAST_DETERIORATION);
        }

        [Fact]
        public void GraduatedLiveFast_Skipped()
        {
            TestGame.Hire(TestGame.Idol(Live_fast)).status = data_girls._status.graduated;
            Data_girls_AgeDeterioration.Postfix();
            Assert.Empty(Seams.Deteriorated);
        }

        [Fact]
        public void NoIdols_NoError()
        {
            data_girls.girl = null;
            Data_girls_AgeDeterioration.Postfix();
            data_girls.girl = new() { null };
            Data_girls_AgeDeterioration.Postfix();
            Assert.Empty(Seams.Deteriorated);
        }
    }

    /// <summary>
    /// On a Live Fast idol's birthday past her peak age, each stat's loss is doubled, both in her stats
    /// and on the birthday popup. Funny and smart grow with age and are left alone.
    /// </summary>
    public class LiveFastBirthdayTests
    {
        public LiveFastBirthdayTests() => TestGame.Reset(patched: true);

        private static Birthday_Popup Popup(data_girls.girls girl)
        {
            Birthday_Popup popup = TestGame.Component<Birthday_Popup>();
            popup.Girl = girl;
            return popup;
        }

        /// <summary>
        /// Runs the stat write the game's DoParam makes, inside the mod's prefix and finalizer.
        /// </summary>
        private static void Birthday(data_girls.girls girl, data_girls._paramType type, float newVal)
        {
            bool state = false;
            Birthday_Popup_DoParam.Prefix(Popup(girl), type, ref state);
            try
            {
                girl.getParam(type).setVal(newVal);
            }
            finally
            {
                Assert.Null(Birthday_Popup_DoParam.Finalizer(null, state));
            }
        }

        [Fact]
        public void PastPeak_LossDoubled()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast);
            Birthday(girl, vocal, 39f);
            Assert.Equal(38f, TestGame.Stat(girl, vocal), 3);
        }

        [Fact]
        public void Loss_StopsAt1()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast, statValue: 1.5f);
            Birthday(girl, cute, 1.2f);
            Assert.Equal(1f, TestGame.Stat(girl, cute), 3);
        }

        [Theory]
        [InlineData(funny)]
        [InlineData(smart)]
        public void FunnyAndSmart_Unchanged(data_girls._paramType type)
        {
            data_girls.girls girl = TestGame.Idol(Live_fast);
            Birthday(girl, type, 46f);
            Assert.Equal(46f, TestGame.Stat(girl, type), 3);
        }

        [Fact]
        public void AtOrBeforePeak_Unchanged()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast, age: 18);
            bool state = true;
            Birthday_Popup_DoParam.Prefix(Popup(girl), vocal, ref state);
            Assert.False(state);
            girl.getParam(vocal).setVal(39f);
            Assert.Equal(39f, TestGame.Stat(girl, vocal), 3);
        }

        [Fact]
        public void OtherTraits_Unchanged()
        {
            data_girls.girls girl = TestGame.Idol();
            Birthday(girl, vocal, 39f);
            Assert.Equal(39f, TestGame.Stat(girl, vocal), 3);
        }

        [Fact]
        public void OnlyTheBirthdayStatChanges()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast);
            data_girls.girls other = TestGame.Idol(Live_fast);
            bool state = false;
            Birthday_Popup_DoParam.Prefix(Popup(girl), vocal, ref state);

            girl.getParam(dance).setVal(39f);
            other.getParam(vocal).setVal(39f);
            girl.getParam(vocal).setVal(41f);
            Birthday_Popup_DoParam.Finalizer(null, state);

            Assert.Equal(39f, TestGame.Stat(girl, dance), 3);
            Assert.Equal(39f, TestGame.Stat(other, vocal), 3);
            Assert.Equal(41f, TestGame.Stat(girl, vocal), 3);
        }

        [Fact]
        public void AfterBirthday_StatsWriteNormally()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast);
            Birthday(girl, vocal, 39f);
            girl.getParam(vocal).setVal(30f);
            Assert.Equal(30f, TestGame.Stat(girl, vocal), 3);
        }

        [Fact]
        public void Finalizer_PassesExceptionOn()
        {
            Exception thrown = new InvalidOperationException();
            Assert.Same(thrown, Birthday_Popup_DoParam.Finalizer(thrown, false));
        }

        /// <summary>
        /// The popup shows the doubled loss for the birthday stat.
        /// </summary>
        [Fact]
        public void Popup_ShowsDoubledLoss()
        {
            data_girls.girls girl = TestGame.Idol(Live_fast);
            bool state = false;
            Birthday_Popup_DoParam.Prefix(Popup(girl), vocal, ref state);

            float shown = 39f;
            Birthday_Stat_Set.Prefix(vocal, 40f, ref shown);
            Assert.Equal(38f, shown, 3);

            float otherStat = 39f;
            Birthday_Stat_Set.Prefix(dance, 40f, ref otherStat);
            Assert.Equal(39f, otherStat, 3);

            Birthday_Popup_DoParam.Finalizer(null, state);
            shown = 39f;
            Birthday_Stat_Set.Prefix(vocal, 40f, ref shown);
            Assert.Equal(39f, shown, 3);
        }
    }

    /// <summary>
    /// Trendy idols have 1.5x the appeal to teens and young adults, and 0.5x to adults.
    /// </summary>
    public class TrendyTests
    {
        public TrendyTests() => TestGame.Reset(patched: true);

        [Theory]
        [InlineData(resources.fanType.teen, 1.5f)]
        [InlineData(resources.fanType.youngAdult, 1.5f)]
        [InlineData(resources.fanType.adult, 0.5f)]
        [InlineData(resources.fanType.male, 1f)]
        [InlineData(resources.fanType.casual, 1f)]
        public void Appeal_ScaledByAge(resources.fanType fanType, float scale)
        {
            float plain = TestGame.Idol().GetAppealOfStat(funny, fanType);
            float trendy = TestGame.Idol(Trendy).GetAppealOfStat(funny, fanType);
            Assert.NotEqual(0f, plain);
            Assert.Equal(plain * scale, trendy, 4);
        }

        [Fact]
        public void Postfix_OtherTraitsAndNoIdol_Unchanged()
        {
            float result = 10f;
            Data_girls_girls_GetAppealOfStat.Postfix(ref result, resources.fanType.adult, TestGame.Idol());
            Data_girls_girls_GetAppealOfStat.Postfix(ref result, resources.fanType.adult, null);
            Assert.Equal(10f, result);
        }
    }
}
