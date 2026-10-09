using System;
using System.Collections.Generic;
using TraitsExpansion;
using Xunit;
using static data_girls._paramType;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// Perfect Pitch, Beauty Guru, Mensa Member, Well Endowed, Tone Deaf and Homely change stats,
    /// but only while the game scores a performance.
    /// </summary>
    public class StatModifierTests
    {
        public StatModifierTests() => TestGame.Reset();

        [Theory]
        [InlineData(NewTraits.Perfect_Pitch, vocal, 50)]
        [InlineData(NewTraits.Beauty_Guru, pretty, 30)]
        [InlineData(NewTraits.Mensa_Member, smart, 50)]
        [InlineData(NewTraits.Well_Endowed, sexy, 30)]
        [InlineData(NewTraits.Tone_Deaf, vocal, -30)]
        [InlineData(NewTraits.Homely, pretty, -10)]
        [InlineData(NewTraits.Homely, cute, -10)]
        [InlineData(NewTraits.Homely, cool, -10)]
        [InlineData(NewTraits.Homely, sexy, -10)]
        public void Trait_ChangesItsStat(NewTraits trait, data_girls._paramType stat, int expected)
        {
            Assert.Equal(expected, GetTraitModifier(TestGame.Idol(trait), stat));
        }

        /// <summary>
        /// Every other stat and parameter, for every trait, is left alone.
        /// </summary>
        [Fact]
        public void OtherStats_Unchanged()
        {
            Dictionary<NewTraits, data_girls._paramType[]> changed = new()
            {
                [NewTraits.Perfect_Pitch] = new[] { vocal },
                [NewTraits.Beauty_Guru] = new[] { pretty },
                [NewTraits.Mensa_Member] = new[] { smart },
                [NewTraits.Well_Endowed] = new[] { sexy },
                [NewTraits.Tone_Deaf] = new[] { vocal },
                [NewTraits.Homely] = new[] { pretty, cute, cool, sexy },
            };

            foreach (NewTraits trait in Enum.GetValues(typeof(NewTraits)))
            {
                data_girls.girls girl = TestGame.Idol(trait);
                foreach (data_girls._paramType stat in Enum.GetValues(typeof(data_girls._paramType)))
                {
                    if (changed.TryGetValue(trait, out data_girls._paramType[] stats) && Array.IndexOf(stats, stat) >= 0)
                        continue;
                    Assert.True(GetTraitModifier(girl, stat) == 0, $"{trait} changes {stat}");
                }
            }
        }

        /// <summary>
        /// Vanilla traits share the enum's number space below 2801 and get no bonus.
        /// </summary>
        [Fact]
        public void VanillaTraits_Unchanged()
        {
            foreach (traits._trait._type trait in Enum.GetValues(typeof(traits._trait._type)))
            {
                data_girls.girls girl = TestGame.Idol();
                girl.trait = trait;
                foreach (data_girls._paramType stat in Enum.GetValues(typeof(data_girls._paramType)))
                    Assert.Equal(0, GetTraitModifier(girl, stat));
            }
        }

        [Fact]
        public void NoIdol_NoModifier()
        {
            Assert.Equal(0, GetTraitModifier(null, vocal));
        }

        [Fact]
        public void GetVal_AddsModifierOnlyWhileScoring()
        {
            data_girls.girls girl = TestGame.Idol(NewTraits.Perfect_Pitch);
            data_girls.girls.param param = girl.getParam(vocal);

            float result = 40f;
            data_girls_girls_param_GetVal.Postfix(ref result, param);
            Assert.Equal(40f, result);

            patchGetVal = true;
            result = 40f;
            data_girls_girls_param_GetVal.Postfix(ref result, param);
            Assert.Equal(90f, result);
        }

        /// <summary>
        /// Through the game: an idol's stats read normally outside performance scoring.
        /// </summary>
        [Fact]
        public void ThroughGame_StatsReadNormallyOutsideScoring()
        {
            TestGame.Reset(patched: true);
            data_girls.girls girl = TestGame.Idol(NewTraits.Perfect_Pitch);
            Assert.Equal(40f, TestGame.Stat(girl, vocal));
        }
    }

    /// <summary>
    /// The stat bonuses apply inside each of the game's scoring calculations, and nowhere else afterwards.
    /// </summary>
    public class ScoringContextTests
    {
        public ScoringContextTests() => TestGame.Reset(patched: true);

        /// <summary>
        /// Show averages: a Perfect Pitch idol on 40 vocal counts as 90.
        /// </summary>
        [Fact]
        public void AverageParam_IncludesBonuses()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol(NewTraits.Perfect_Pitch), TestGame.Idol() };
            Assert.Equal(65f, data_girls.GetAverageParam(vocal, girls));
            Assert.False(patchGetVal);
            Assert.Equal(40f, TestGame.Stat(girls[0], vocal));
        }

        [Theory]
        [InlineData(NewTraits.Perfect_Pitch, 90f)]
        [InlineData(NewTraits.Tone_Deaf, 10f)]
        [InlineData(NewTraits.none, 40f)]
        public void ShowSenbatsu_IncludesBonuses(NewTraits trait, float expected)
        {
            List<data_girls.girls> girls = new() { TestGame.Idol(trait) };
            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                TestGame.Component<Shows._show>(), typeof(Shows._show), "SenbatsuCalcParam", girls, vocal);

            Assert.Equal(expected, result._val, 3);
            Assert.False(patchGetVal);
        }

        /// <summary>
        /// The show senbatsu score is capped at 100 after the bonus, as in vanilla.
        /// </summary>
        [Fact]
        public void ShowSenbatsu_CappedAt100()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol(NewTraits.Perfect_Pitch, statValue: 80f) };
            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                TestGame.Component<Shows._show>(), typeof(Shows._show), "SenbatsuCalcParam", girls, vocal);
            Assert.Equal(100f, result._val);
        }

        [Theory]
        [InlineData(NewTraits.Mensa_Member, smart, 90f)]
        [InlineData(NewTraits.Mensa_Member, vocal, 40f)]
        [InlineData(NewTraits.Perfect_Pitch, vocal, 90f)]
        public void SingleSenbatsu_IncludesBonuses(NewTraits trait, data_girls._paramType stat, float expected)
        {
            data_girls.girls center = TestGame.Idol(trait);
            List<data_girls.girls> girls = new(new data_girls.girls[15]) { [0] = center };
            // One idol in a sub-group: one row, so the score is the center's stat
            Groups._group group = new() { ID = 1, Girls = new List<data_girls.girls> { center } };

            data_girls.girls.param result = (data_girls.girls.param)TestGame.CallPrivate(
                new singles._single(), typeof(singles._single), "SenbatsuCalcParam", girls, stat, group);

            Assert.Equal(expected, result._val, 3);
            Assert.False(patchGetVal);
        }

        /// <summary>
        /// A concert song scores the center's average of dance and vocal.
        /// </summary>
        [Theory]
        [InlineData(NewTraits.Perfect_Pitch, 65)]
        [InlineData(NewTraits.Tone_Deaf, 25)]
        [InlineData(NewTraits.none, 40)]
        public void ConcertSong_IncludesBonuses(NewTraits trait, int expected)
        {
            singles._single single = new();
            single.girls.Add(TestGame.Idol());
            SEvent_Concerts._concert._song song = new() { Single = single, Center = TestGame.Idol(trait) };

            Assert.Equal(expected, song.GetSkillValue());
            Assert.False(patchGetVal);
        }

        /// <summary>
        /// A concert MC scores the sum of each MC's average of smart and funny.
        /// </summary>
        [Fact]
        public void ConcertMC_IncludesBonuses()
        {
            SEvent_Concerts._concert._mc mc = new();
            mc.Girls[0] = TestGame.Idol(NewTraits.Mensa_Member);
            mc.Girls[1] = TestGame.Idol();

            Assert.Equal(105, mc.GetSkillValue());
            Assert.False(patchGetVal);
        }

        /// <summary>
        /// Business pay reads the proposal's skill: Mensa's +50 smart takes 40 to 90.
        /// </summary>
        [Fact]
        public void BusinessCoeff_IncludesBonuses()
        {
            business._proposal proposal = new() { type = business._type.photoshoot, skill = smart };
            Assert.Equal(1.4f, proposal.GetGirlCoeff(TestGame.Idol(NewTraits.Mensa_Member)), 3);
            Assert.Equal(0.95f, proposal.GetGirlCoeff(TestGame.Idol()), 3);
            Assert.False(patchGetVal);
        }

        /// <summary>
        /// If a scoring calculation throws, the bonuses still switch off.
        /// </summary>
        [Fact]
        public void Exception_StillSwitchesBonusesOff()
        {
            business._proposal proposal = new() { type = business._type.photoshoot, skill = smart };
            Assert.Throws<NullReferenceException>(() => proposal.GetGirlCoeff(null));
            Assert.False(patchGetVal);

            Assert.Throws<NullReferenceException>(() => data_girls.GetAverageParam(vocal, null));
            Assert.False(patchGetVal);

            data_girls.girls girl = TestGame.Idol(NewTraits.Perfect_Pitch);
            Assert.Equal(40f, TestGame.Stat(girl, vocal));
        }

        [Fact]
        public void EveryScoringPatch_SwitchesOffAfterwards()
        {
            List<data_girls.girls> girls = new() { TestGame.Idol() };

            Data_girls_GetAverageParam.Prefix();
            Assert.True(patchGetVal);
            Data_girls_GetAverageParam.Finalizer(null);
            Assert.False(patchGetVal);

            Shows__show_SenbatsuCalcParam.Prefix();
            Assert.True(patchGetVal);
            Shows__show_SenbatsuCalcParam.Finalizer(null);
            Assert.False(patchGetVal);

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
                Assert.True(patchGetVal);
                Exception thrown = new InvalidOperationException();
                Assert.Same(thrown, finalizers[i](thrown));
                Assert.False(patchGetVal);
            }
        }
    }
}
