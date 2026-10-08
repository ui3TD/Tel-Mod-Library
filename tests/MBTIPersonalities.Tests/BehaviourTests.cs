using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;
using static MBTIPersonalities.Tests.TestGirls;
using Patch = MBTIPersonalities.SEvent_Concerts__concert_AccidentSuccessChance;

namespace MBTIPersonalities.Tests
{
    [Collection(MBTICollection.Name)]
    public class ISTPAccidentTests
    {
        /// <summary>
        /// ISTP halves the chance of failing an accident: the success chance closes half the gap to 100%.
        /// </summary>
        [Theory]
        [InlineData(0, 50)]
        [InlineData(40, 70)]
        [InlineData(60, 80)]
        [InlineData(90, 95)]
        [InlineData(100, 100)]
        public void HalvesTheFailureChance(int successChance, int expected)
        {
            Assert.Equal(expected, Patch.ApplyISTPBonus(successChance));
        }

        /// <summary>
        /// The bonus never lowers the success chance or pushes it past 100%.
        /// </summary>
        [Fact]
        public void NeverWorseThanWithoutISTP()
        {
            for (int chance = 0; chance <= 100; chance++)
            {
                int result = Patch.ApplyISTPBonus(chance);
                Assert.InRange(result, chance, 100);
            }
        }

        /// <summary>
        /// Odd failure chances leave half a percent, which rounds to the nearest whole percent.
        /// </summary>
        [Fact]
        public void FailureChanceIsHalvedToWithinRounding()
        {
            for (int chance = 0; chance <= 100; chance++)
            {
                float failure = 100 - Patch.ApplyISTPBonus(chance);
                Assert.InRange(failure, (100 - chance) / 2f - 0.5f, (100 - chance) / 2f + 0.5f);
            }
        }
    }

    [Collection(MBTICollection.Name)]
    public class ISTJAccidentTests
    {
        private static int Run(int accidentChance, params data_girls.girls[] mcGirls)
        {
            SEvent_Concerts._concert concert = new();
            SEvent_Concerts._concert._mc mc = new() { Girls = new List<data_girls.girls>(mcGirls) };
            concert.SetListItems.Add(mc);
            SEvent_Concerts__concert_Accident_Stamina.Postfix(ref accidentChance, concert, 0);
            return accidentChance;
        }

        /// <summary>
        /// Each ISTJ on stage takes 10 points off the accident chance.
        /// </summary>
        [Fact]
        public void EachISTJCutsTenPoints()
        {
            Assert.Equal(-20, Run(0, Make(MBTI.ISTJ), Make(MBTI.ISTJ), Make(MBTI.ENFP)));
        }

        [Fact]
        public void NoISTJLeavesTheChance()
        {
            Assert.Equal(5, Run(5, Make(MBTI.ISTP), null, Make(MBTI.ENFP)));
        }

        /// <summary>
        /// A song counts only its center.
        /// </summary>
        [Fact]
        public void SongCountsTheCenter()
        {
            SEvent_Concerts._concert concert = new();
            concert.SetListItems.Add(new SEvent_Concerts._concert._song { Center = Make(MBTI.ISTJ) });
            int chance = 0;
            SEvent_Concerts__concert_Accident_Stamina.Postfix(ref chance, concert, 0);
            Assert.Equal(-10, chance);
        }
    }

    [Collection(MBTICollection.Name)]
    public class ISFJInfluenceTests
    {
        private static int Run(MBTI mbti, Relationships_Player._type type, int points)
        {
            Relationships_Player_AddPoints.Prefix(type, Make(mbti), ref points);
            return points;
        }

        /// <summary>
        /// ISFJ gains 10% more influence.
        /// </summary>
        [Theory]
        [InlineData(50, 55)]
        [InlineData(25, 28)]
        public void InfluenceGainsAreTenPercentLarger(int points, int expected)
        {
            Assert.Equal(expected, Run(MBTI.ISFJ, Relationships_Player._type.Influence, points));
        }

        [Fact]
        public void InfluenceLossesAreUnchanged()
        {
            Assert.Equal(-50, Run(MBTI.ISFJ, Relationships_Player._type.Influence, -50));
        }

        [Fact]
        public void OtherRelationshipsAreUnchanged()
        {
            Assert.Equal(50, Run(MBTI.ISFJ, Relationships_Player._type.Friendship, 50));
            Assert.Equal(50, Run(MBTI.ISFJ, Relationships_Player._type.Romance, 50));
        }

        [Fact]
        public void OtherTypesAreUnchanged()
        {
            Assert.Equal(50, Run(MBTI.ESFJ, Relationships_Player._type.Influence, 50));
        }
    }

    [Collection(MBTICollection.Name)]
    public class ISFPAppealTests
    {
        private static float Run(MBTI mbti, resources.fanType fanType, float appeal)
        {
            Data_girls_girls_GetAppealOfStat.Postfix(ref appeal, fanType, Make(mbti));
            return appeal;
        }

        /// <summary>
        /// ISFP appeals 10% more to hardcore fans.
        /// </summary>
        [Fact]
        public void HardcoreAppealIsTenPercentHigher()
        {
            Assert.Equal(2.2f, Run(MBTI.ISFP, resources.fanType.hardcore, 2f), 4);
        }

        [Fact]
        public void OtherFansAreUnchanged()
        {
            Assert.Equal(2f, Run(MBTI.ISFP, resources.fanType.casual, 2f));
        }

        [Fact]
        public void OtherTypesAreUnchanged()
        {
            Assert.Equal(2f, Run(MBTI.INFP, resources.fanType.hardcore, 2f));
        }
    }

    [Collection(MBTICollection.Name)]
    public class FanCountTests
    {
        private static long RunINFP(MBTI mbti, long fans)
        {
            data_girls_girls_GetFan_Count_INFP.Postfix(ref fans, Make(mbti));
            return fans;
        }

        private static long RunENFJ(MBTI mbti, long fans)
        {
            data_girls_girls_GetFan_Count_ENFJ.Postfix(Make(mbti), ref fans);
            return fans;
        }

        /// <summary>
        /// INFP counts 10% more fans while handshake sales are generated.
        /// </summary>
        [Fact]
        public void INFPHandshakeFansAreTenPercentHigher()
        {
            WithFlag(on => patchGetFan_Count_INFP = on, () =>
            {
                Assert.Equal(1100L, RunINFP(MBTI.INFP, 1000));
                Assert.Equal(1000L, RunINFP(MBTI.ENFJ, 1000));
            });
        }

        [Fact]
        public void INFPOutsideHandshakesIsUnchanged()
        {
            Assert.Equal(1000L, RunINFP(MBTI.INFP, 1000));
        }

        /// <summary>
        /// ENFJ counts 20% more fans while election results are generated.
        /// </summary>
        [Fact]
        public void ENFJElectionFansAreTwentyPercentHigher()
        {
            WithFlag(on => patchGetFan_Count_ENFJ = on, () =>
            {
                Assert.Equal(1200L, RunENFJ(MBTI.ENFJ, 1000));
                Assert.Equal(1000L, RunENFJ(MBTI.INFP, 1000));
            });
        }

        [Fact]
        public void ENFJOutsideElectionsIsUnchanged()
        {
            Assert.Equal(1000L, RunENFJ(MBTI.ENFJ, 1000));
        }
    }

    [Collection(MBTICollection.Name)]
    public class ENFPStaminaTests
    {
        /// <summary>
        /// The proposal popup shows 10% less stamina for ENFP, then restores the proposal.
        /// </summary>
        [Fact]
        public void PopupShowsReducedStaminaThenRestores()
        {
            business._proposal proposal = new() { girl = Make(MBTI.ENFP), stamina = 20 };
            int state = 0;

            Business_Popup_Set.Prefix(ref proposal, ref state);
            Assert.Equal(18, proposal.stamina);

            Business_Popup_Set.Finalizer(ref proposal, ref state);
            Assert.Equal(20, proposal.stamina);
        }

        /// <summary>
        /// Accepting a proposal costs ENFP 10% less stamina.
        /// </summary>
        [Fact]
        public void AcceptCostsTenPercentLess()
        {
            business biz = (business)FormatterServices.GetUninitializedObject(typeof(business));
            biz.ActiveProposal = new business._proposal { girl = Make(MBTI.ENFP), stamina = 20 };

            business_Accept.Prefix(ref biz);
            Assert.Equal(18, biz.ActiveProposal.stamina);
        }

        [Fact]
        public void OtherTypesAreUnchanged()
        {
            business._proposal proposal = new() { girl = Make(MBTI.ESTJ), stamina = 20 };
            int state = 0;
            Business_Popup_Set.Prefix(ref proposal, ref state);
            Assert.Equal(20, proposal.stamina);
        }
    }

    [Collection(MBTICollection.Name)]
    public class INTJBirthdayTests
    {
        private static float RunSetVal(float oldVal, float newVal)
        {
            data_girls.girls.param param = new() { type = data_girls._paramType.smart, _val = oldVal };
            data_girls_girls_param_setVal.Prefix(ref newVal, param);
            return newVal;
        }

        private static float RunStat(float oldVal, float newVal)
        {
            Birthday_Stat_Set.Prefix(oldVal, ref newVal);
            return newVal;
        }

        /// <summary>
        /// INTJ's birthday smartness change is doubled, capped at 100.
        /// The stored value and the birthday popup's display must agree.
        /// </summary>
        [Theory]
        [InlineData(40f, 45f, 50f)]
        [InlineData(95f, 99f, 100f)]
        public void BirthdayGainIsDoubled(float oldVal, float newVal, float expected)
        {
            WithFlag(on => patchSetVal_INTJ = patchSet_INTJ = on, () =>
            {
                Assert.Equal(expected, RunSetVal(oldVal, newVal));
                Assert.Equal(expected, RunStat(oldVal, newVal));
            });
        }

        [Fact]
        public void OutsideBirthdaysIsUnchanged()
        {
            Assert.Equal(45f, RunSetVal(40f, 45f));
            Assert.Equal(45f, RunStat(40f, 45f));
        }
    }

    [Collection(MBTICollection.Name)]
    public class INTPNoveltyTests
    {
        private static int Run(MBTI mbti, int val)
        {
            data_girls.girls girl = Make(mbti);
            data_girls.girl.Add(girl);
            try
            {
                Cafes._cafe._dish dish = new() { Girl = girl.id };
                Cafes__cafe__dish_AddNovelty.Prefix(dish, ref val);
                return val;
            }
            finally
            {
                data_girls.girl.Remove(girl);
            }
        }

        /// <summary>
        /// INTP's dishes lose novelty at half speed. The game's -3 rounds to -2.
        /// </summary>
        [Theory]
        [InlineData(-10, -5)]
        [InlineData(-3, -2)]
        public void NoveltyLossIsHalved(int val, int expected)
        {
            Assert.Equal(expected, Run(MBTI.INTP, val));
        }

        [Fact]
        public void NoveltyGainIsUnchanged()
        {
            Assert.Equal(10, Run(MBTI.INTP, 10));
        }

        [Fact]
        public void OtherTypesAreUnchanged()
        {
            Assert.Equal(-10, Run(MBTI.ISTJ, -10));
        }
    }

    [Collection(MBTICollection.Name)]
    public class ESTJTrainingTests
    {
        private static float Run(MBTI mbti, float duration)
        {
            data_girls.girls.param param = new() { type = data_girls._paramType.vocal, Parent = Make(mbti) };
            data_girls_girls_param_GetDuration.Postfix(param, ref duration);
            return duration;
        }

        /// <summary>
        /// ESTJ trains 20% faster.
        /// </summary>
        [Fact]
        public void TrainingIsTwentyPercentShorter()
        {
            Assert.Equal(800f, Run(MBTI.ESTJ, 1000f), 3);
        }

        [Fact]
        public void OtherTypesAreUnchanged()
        {
            Assert.Equal(1000f, Run(MBTI.ESFP, 1000f));
        }
    }

    [Collection(MBTICollection.Name)]
    public class ESFPTheaterTests
    {
        private const int GroupId = 900001;

        private static float Run(float coeff, params data_girls.girls[] members)
        {
            Groups._group group = new() { ID = GroupId, Girls = new List<data_girls.girls>(members) };
            Groups.Groups_.Add(group);
            try
            {
                Theaters._theater theater = new() { Group = GroupId };
                Theaters__theater_GetPriceCoeff.Postfix(theater, ref coeff);
                return coeff;
            }
            finally
            {
                Groups.Groups_.Remove(group);
            }
        }

        /// <summary>
        /// Each ESFP in the theater's group adds 5% to ticket sales.
        /// </summary>
        [Fact]
        public void EachESFPAddsFivePercent()
        {
            Assert.Equal(1.1f, Run(1f, Make(MBTI.ESFP), Make(MBTI.ESFP), Make(MBTI.INFJ)), 4);
        }

        [Fact]
        public void NoESFPLeavesTheCoefficient()
        {
            Assert.Equal(1f, Run(1f, Make(MBTI.INFJ), Make(MBTI.ESFJ)));
        }
    }

    [Collection(MBTICollection.Name)]
    public class ESFJChemistryTests
    {
        private static float Run(float chemistry, params data_girls.girls[] team)
        {
            data_girls_GetTeamChemistry.Postfix(ref chemistry, new List<data_girls.girls>(team));
            return chemistry;
        }

        /// <summary>
        /// Each ESFJ on the team adds 5 chemistry. Empty slots are skipped.
        /// </summary>
        [Fact]
        public void EachESFJAddsFive()
        {
            Assert.Equal(60f, Run(50f, Make(MBTI.ESFJ), null, Make(MBTI.ESFJ), Make(MBTI.ISFJ)));
        }

        [Fact]
        public void NoESFJLeavesChemistry()
        {
            Assert.Equal(50f, Run(50f, Make(MBTI.ISFJ), null));
        }
    }

    /// <summary>
    /// ENTJ, ENTP, ESTP and INFJ add +5 to all stats when their condition holds.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class StatBonusTests
    {
        private const data_girls._paramType Stat = data_girls._paramType.vocal;

        private static data_girls.girls WithScandalPoints(MBTI mbti, float points)
        {
            data_girls.girls girl = Make(mbti);
            girl.parameters.Add(new data_girls.girls.param { type = data_girls._paramType.scandalPoints, _val = points });
            return girl;
        }

        [Fact]
        public void ENTJWhenPushed()
        {
            data_girls.girls girl = Make(MBTI.ENTJ);
            Assert.Equal(0f, GetTraitModifier(girl, Stat));

            data_girls.girls previous = Pushes.Girls[0];
            Pushes.Girls[0] = girl;
            try
            {
                Assert.Equal(5f, GetTraitModifier(girl, Stat));
            }
            finally
            {
                Pushes.Girls[0] = previous;
            }
        }

        [Fact]
        public void ENTPWithScandalPoints()
        {
            Assert.Equal(5f, GetTraitModifier(WithScandalPoints(MBTI.ENTP, 1f), Stat));
            Assert.Equal(0f, GetTraitModifier(WithScandalPoints(MBTI.ENTP, 0f), Stat));
        }

        [Fact]
        public void ESTPWithRiskyMarketing()
        {
            data_girls.girls girl = Make(MBTI.ESTP);
            Assert.Equal(0f, GetTraitModifier(girl, Stat));

            isRisky = false;
            try
            {
                Assert.Equal(0f, GetTraitModifier(girl, Stat));
                isRisky = true;
                Assert.Equal(5f, GetTraitModifier(girl, Stat));
            }
            finally
            {
                isRisky = null;
            }
        }

        [Fact]
        public void INFJInShows()
        {
            data_girls.girls girl = Make(MBTI.INFJ);
            Assert.Equal(0f, GetTraitModifier(girl, Stat));
            WithFlag(on => isShow = on, () => Assert.Equal(5f, GetTraitModifier(girl, Stat)));
        }

        /// <summary>
        /// Only the eight stats get the bonus, not fame, stamina and the like.
        /// </summary>
        [Fact]
        public void OnlyStatsGetTheBonus()
        {
            data_girls.girls girl = Make(MBTI.INFJ);
            WithFlag(on => isShow = on, () =>
            {
                Assert.Equal(5f, GetTraitModifier(girl, data_girls._paramType.smart));
                Assert.Equal(0f, GetTraitModifier(girl, data_girls._paramType.famePoints));
                Assert.Equal(0f, GetTraitModifier(girl, data_girls._paramType.physicalStamina));
            });
        }

        /// <summary>
        /// The bonus only applies while the mod's patched calculations are running.
        /// </summary>
        [Fact]
        public void GetValAddsTheBonusOnlyWhenEnabled()
        {
            data_girls.girls girl = Make(MBTI.INFJ);
            data_girls.girls.param param = new() { type = Stat, Parent = girl };
            WithFlag(on => isShow = on, () =>
            {
                float val = 50f;
                data_girls_girls_param_GetVal.Postfix(ref val, param);
                Assert.Equal(50f, val);

                WithFlag(on => patchGetVal = on, () =>
                {
                    data_girls_girls_param_GetVal.Postfix(ref val, param);
                    Assert.Equal(55f, val);
                });
            });
        }
    }

    [Collection(MBTICollection.Name)]
    public class AssignmentTests
    {
        private static data_girls.girls Unassigned(string first, string last, DateTime birthday)
        {
            return new data_girls.girls { id = 200000 + Math.Abs((first + last).GetHashCode() % 100000), firstName = first, lastName = last, birthday = birthday };
        }

        /// <summary>
        /// The same name and birthday always give the same type, and never None.
        /// </summary>
        [Fact]
        public void GeneratedTypeIsStableAndValid()
        {
            data_girls.girls a = Unassigned("Aiko", "Tanaka", new DateTime(2005, 3, 14));
            data_girls.girls b = Unassigned("Aiko", "Tanaka", new DateTime(2005, 3, 14));

            MBTI type = GenerateMBTI(a);
            Assert.Equal(type, GenerateMBTI(b));
            Assert.InRange((int)type, (int)MBTI.ISTJ, (int)MBTI.ENTJ);
        }

        /// <summary>
        /// Names spread across all 16 types.
        /// </summary>
        [Fact]
        public void GeneratedTypesCoverAllSixteen()
        {
            HashSet<MBTI> seen = new();
            for (int i = 0; i < 2000; i++)
            {
                seen.Add(GenerateMBTI(Unassigned("Idol" + i, "Test", new DateTime(2005, 1, 1).AddDays(i % 365))));
            }
            Assert.Equal(16, seen.Count);
        }

        /// <summary>
        /// The type is written to the girl's save variables so it survives save/load.
        /// </summary>
        [Fact]
        public void TypeIsSavedToVariables()
        {
            data_girls.girls girl = Unassigned("Yui", "Sato", new DateTime(2004, 7, 1));
            MBTIReferenceDict.Remove(girl.id);
            try
            {
                MBTI type = GetGirlMBTI(girl);
                Assert.Contains(type.ToString(), girl.Variables);
                Assert.Equal(type, GetGirlMBTI(girl));
            }
            finally
            {
                MBTIReferenceDict.Remove(girl.id);
            }
        }
    }
}
