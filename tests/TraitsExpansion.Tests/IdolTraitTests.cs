using System;
using System.Collections.Generic;
using TraitsExpansion;
using Xunit;
using static data_girls._paramType;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// Old Money idols are always fully satisfied with their salary and lose no mental stamina over it.
    /// </summary>
    public class OldMoneyTests
    {
        [Theory]
        [InlineData(0f)]
        [InlineData(0.5f)]
        [InlineData(1f)]
        [InlineData(2f)]
        public void SalarySatisfaction_Always200Percent(float vanilla)
        {
            TestGame.Reset();
            float result = vanilla;
            data_girls_girls_GetSalarySatisfaction.Postfix(ref result, TestGame.Idol(NewTraits.Old_Money));
            Assert.Equal(2f, result);
        }

        [Fact]
        public void OtherIdols_SatisfactionUnchanged()
        {
            TestGame.Reset();
            float result = 0.5f;
            data_girls_girls_GetSalarySatisfaction.Postfix(ref result, TestGame.Idol(NewTraits.Polyglot));
            Assert.Equal(0.5f, result);
        }

        /// <summary>
        /// Through the game's weekly low-salary check: on Normal an unpaid idol loses 8 mental stamina.
        /// </summary>
        [Fact]
        public void LowSalaryCheck_SparesOldMoney()
        {
            TestGame.Reset(patched: true);
            data_girls.girls rich = TestGame.Hire(TestGame.Idol(NewTraits.Old_Money));
            data_girls.girls poor = TestGame.Hire(TestGame.Idol());
            rich.salary = 0;
            poor.salary = 0;

            TestGame.Component<data_girls>().CheckLowSalaries();

            Assert.Equal(100f, TestGame.Stat(rich, mentalStamina));
            Assert.Equal(92f, TestGame.Stat(poor, mentalStamina));
            Assert.False(patchAddParam);
        }

        /// <summary>
        /// Outside the salary check, Old Money idols lose mental stamina as usual.
        /// </summary>
        [Fact]
        public void OtherMentalLosses_StillApply()
        {
            TestGame.Reset(patched: true);
            data_girls.girls rich = TestGame.Idol(NewTraits.Old_Money);
            rich.addParam(mentalStamina, -10f);
            Assert.Equal(90f, TestGame.Stat(rich, mentalStamina));
        }

        [Theory]
        [InlineData(NewTraits.Old_Money, mentalStamina, true, 0f)]
        [InlineData(NewTraits.Old_Money, physicalStamina, true, -8f)]
        [InlineData(NewTraits.Old_Money, mentalStamina, false, -8f)]
        [InlineData(NewTraits.none, mentalStamina, true, -8f)]
        public void AddParam_ZeroedOnlyForOldMoneyMentalDuringCheck(NewTraits trait, data_girls._paramType type, bool duringCheck, float expected)
        {
            TestGame.Reset();
            patchAddParam = duringCheck;
            float val = -8f;
            data_girls_girls_addParam.Prefix(TestGame.Idol(trait), type, ref val);
            Assert.Equal(expected, val);
        }

        [Fact]
        public void SalaryCheck_SetsFlagOnlyWhileRunning()
        {
            TestGame.Reset();
            data_girls_CheckLowSalaries.Prefix();
            Assert.True(patchAddParam);
            data_girls_CheckLowSalaries.Finalizer(null);
            Assert.False(patchAddParam);
        }

        /// <summary>
        /// If the salary check throws, Old Money's protection still switches off.
        /// </summary>
        [Fact]
        public void SalaryCheckException_StillSwitchesOff()
        {
            TestGame.Reset(patched: true);
            data_girls.girl.Add(null);
            Assert.Throws<NullReferenceException>(() => TestGame.Component<data_girls>().CheckLowSalaries());
            Assert.False(patchAddParam);

            data_girls.girls rich = TestGame.Idol(NewTraits.Old_Money);
            rich.addParam(mentalStamina, -10f);
            Assert.Equal(90f, TestGame.Stat(rich, mentalStamina));
        }
    }

    /// <summary>
    /// Fashionista, Flirty and Idol Otaku idols appeal 20% more to female, male and hardcore fans.
    /// </summary>
    public class AppealTests
    {
        public AppealTests() => TestGame.Reset();

        private static float Appeal(NewTraits trait, resources.fanType fanType)
        {
            float result = 10f;
            Data_girls_girls_GetAppealOfStat.Postfix(ref result, fanType, TestGame.Idol(trait));
            return result;
        }

        [Theory]
        [InlineData(NewTraits.Fashionista, resources.fanType.female)]
        [InlineData(NewTraits.Flirty, resources.fanType.male)]
        [InlineData(NewTraits.Idol_Otaku, resources.fanType.hardcore)]
        public void Trait_Boosts20Percent(NewTraits trait, resources.fanType fanType)
        {
            Assert.Equal(12f, Appeal(trait, fanType), 4);
        }

        [Fact]
        public void OtherFanTypesAndTraits_Unchanged()
        {
            Dictionary<NewTraits, resources.fanType> boosted = new()
            {
                [NewTraits.Fashionista] = resources.fanType.female,
                [NewTraits.Flirty] = resources.fanType.male,
                [NewTraits.Idol_Otaku] = resources.fanType.hardcore,
            };

            foreach (NewTraits trait in Enum.GetValues(typeof(NewTraits)))
            {
                foreach (resources.fanType fanType in Enum.GetValues(typeof(resources.fanType)))
                {
                    if (boosted.TryGetValue(trait, out resources.fanType boost) && boost == fanType)
                        continue;
                    Assert.True(Appeal(trait, fanType) == 10f, $"{trait} changes appeal to {fanType}");
                }
            }
        }
    }

    /// <summary>
    /// Bullying in a clique with an active Sadistic bully costs each victim another 10 mental stamina.
    /// </summary>
    public class SadisticTests
    {
        public SadisticTests() => TestGame.Reset();

        private static Relationships._clique Clique(data_girls.girls victim, params data_girls.girls[] members)
        {
            Relationships._clique clique = new() { Leader = members[0], Members = new List<data_girls.girls>(members) };
            if (victim != null)
                clique.Bullied_Girls.Add(victim);
            Relationships.Cliques.Add(clique);
            return clique;
        }

        [Fact]
        public void SadisticBully_VictimLosesExtra10()
        {
            data_girls.girls victim = TestGame.Idol();
            Clique(victim, TestGame.Idol(), TestGame.Idol(NewTraits.Sadistic));

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(90f, TestGame.Stat(victim, mentalStamina));
            Assert.Equal(new[] { "An idol lost 10 mental stamina to bullying." }, Seams.Notifications);
        }

        /// <summary>
        /// When the player knows who is bullied, the notification names her.
        /// </summary>
        [Fact]
        public void KnownVictim_NotificationNamesHer()
        {
            data_girls.girls victim = TestGame.Idol(name: "Victim");
            Clique(victim, TestGame.Idol(NewTraits.Sadistic)).KnownBulliedGirls.Add(victim);

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(new[] { "Victim lost 10 mental stamina to bullying." }, Seams.Notifications);
        }

        /// <summary>
        /// Two Sadistic bullies don't double it again.
        /// </summary>
        [Fact]
        public void SeveralSadisticBullies_StillExtra10()
        {
            data_girls.girls victim = TestGame.Idol();
            Clique(victim, TestGame.Idol(NewTraits.Sadistic), TestGame.Idol(NewTraits.Sadistic));

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(90f, TestGame.Stat(victim, mentalStamina));
        }

        /// <summary>
        /// The trait belongs to the bully. A Sadistic victim of a clique without one loses nothing extra.
        /// </summary>
        [Fact]
        public void SadisticVictim_NoExtra()
        {
            data_girls.girls victim = TestGame.Idol(NewTraits.Sadistic);
            Clique(victim, TestGame.Idol(), TestGame.Idol());

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(100f, TestGame.Stat(victim, mentalStamina));
            Assert.Empty(Seams.Notifications);
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.depressed)]
        public void SickSadisticBully_NoExtra(data_girls._status status)
        {
            data_girls.girls victim = TestGame.Idol();
            data_girls.girls bully = TestGame.Idol(NewTraits.Sadistic);
            bully.status = status;
            Clique(victim, TestGame.Idol(), bully);

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(100f, TestGame.Stat(victim, mentalStamina));
        }

        /// <summary>
        /// A Sadistic member who has stopped bullying the only victim no longer counts.
        /// </summary>
        [Fact]
        public void StoppedBullying_NoExtra()
        {
            data_girls.girls victim = TestGame.Idol();
            data_girls.girls bully = TestGame.Idol(NewTraits.Sadistic);
            Relationships._clique clique = Clique(victim, TestGame.Idol(), bully);
            clique.StoppedBullying.Add(new Relationships._clique._stopped_bullying { Target = victim, Girls = { bully } });

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(100f, TestGame.Stat(victim, mentalStamina));
        }

        [Fact]
        public void SickVictim_Spared()
        {
            data_girls.girls victim = TestGame.Idol();
            victim.status = data_girls._status.injured;
            Clique(victim, TestGame.Idol(NewTraits.Sadistic));

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(100f, TestGame.Stat(victim, mentalStamina));
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void NoVictim_NothingHappens()
        {
            Clique(null, TestGame.Idol(NewTraits.Sadistic));
            Relationships_Do_Bullying.Postfix();
            Assert.Empty(Seams.Notifications);
        }

        /// <summary>
        /// Each clique is judged by its own members.
        /// </summary>
        [Fact]
        public void OtherCliquesVictims_Unaffected()
        {
            data_girls.girls victimOfSadist = TestGame.Idol();
            data_girls.girls otherVictim = TestGame.Idol();
            Clique(victimOfSadist, TestGame.Idol(NewTraits.Sadistic));
            Clique(otherVictim, TestGame.Idol());

            Relationships_Do_Bullying.Postfix();

            Assert.Equal(90f, TestGame.Stat(victimOfSadist, mentalStamina));
            Assert.Equal(100f, TestGame.Stat(otherVictim, mentalStamina));
        }
    }

    /// <summary>
    /// Job Hoppers plan to graduate 100 to 364 days after joining.
    /// </summary>
    public class JobHopperTests
    {
        public JobHopperTests() => TestGame.Reset();

        [Theory]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(364)]
        public void GraduatesWithinAYear(int roll)
        {
            Seams.Range = (_, _) => roll;
            data_girls.girls girl = TestGame.Idol(NewTraits.Job_Hopper);

            data_girls_girls_Graduation_Set_Default_Date.Postfix(ref girl);

            Assert.Equal(new[] { (100, 365) }, Seams.RangesRolled);
            Assert.Equal(TestGame.Today.AddDays(roll), girl.Graduation_Date);
        }

        [Fact]
        public void OtherIdols_KeepVanillaDate()
        {
            data_girls.girls girl = TestGame.Idol(NewTraits.Reckless);
            DateTime vanilla = TestGame.Today.AddYears(5);
            girl.Graduation_Date = vanilla;

            data_girls_girls_Graduation_Set_Default_Date.Postfix(ref girl);

            Assert.Equal(vanilla, girl.Graduation_Date);
            Assert.Empty(Seams.RangesRolled);
        }
    }

    /// <summary>
    /// Reckless idols roll for injury again each time the game does: 1% below 60 physical stamina,
    /// 2% at 5 or below. With vanilla's own 0.5% below 20 and 1% at 5 or below, that's 3x.
    /// </summary>
    public class RecklessTests
    {
        public RecklessTests() => TestGame.Reset();

        private static data_girls.girls Reckless(float stamina)
        {
            data_girls.girls girl = TestGame.Idol(NewTraits.Reckless);
            TestGame.SetStat(girl, physicalStamina, stamina);
            return girl;
        }

        [Theory]
        [InlineData(59.9f, 1f)]
        [InlineData(20f, 1f)]
        [InlineData(5.1f, 1f)]
        [InlineData(5f, 2f)]
        [InlineData(0f, 2f)]
        public void LowStamina_RollsForInjury(float stamina, float chance)
        {
            data_girls_girls_Try_Injury.Postfix(Reckless(stamina));
            Assert.Equal(new[] { chance }, Seams.ChancesRolled);
            Assert.Empty(Seams.Injured);
        }

        [Fact]
        public void FailedRoll_Injures()
        {
            Seams.Chance = _ => true;
            data_girls.girls girl = Reckless(30f);

            data_girls_girls_Try_Injury.Postfix(girl);

            Assert.Equal(new[] { girl }, Seams.Injured);
        }

        [Theory]
        [InlineData(60f)]
        [InlineData(100f)]
        public void HighStamina_NoRoll(float stamina)
        {
            data_girls_girls_Try_Injury.Postfix(Reckless(stamina));
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void AlreadyInjured_NoRoll()
        {
            data_girls.girls girl = Reckless(0f);
            girl.status = data_girls._status.injured;

            data_girls_girls_Try_Injury.Postfix(girl);

            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void InDoctorsOffice_NoRoll()
        {
            data_girls.girls girl = Reckless(0f);
            girl.room = new agency._room { type = agency._type.doctorsOffice };

            data_girls_girls_Try_Injury.Postfix(girl);

            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void OtherIdols_NoRoll()
        {
            data_girls.girls girl = TestGame.Idol(NewTraits.Job_Hopper);
            TestGame.SetStat(girl, physicalStamina, 0f);

            data_girls_girls_Try_Injury.Postfix(girl);

            Assert.Empty(Seams.ChancesRolled);
        }
    }
}
