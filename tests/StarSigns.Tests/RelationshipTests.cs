using System.Collections.Generic;
using Xunit;
using static StarSigns.StarSigns;
using PartnerStatus = data_girls.girls._dating_data._partner_status;

namespace StarSigns.Tests
{
    /// <summary>
    /// Each week, some signs may give a relationship a small boost: if her condition holds, a roll at the
    /// sign's chance adds 0.05, which the game halves (it halves every gain) to 0.025. For scale, the game
    /// moves a positive relationship by 0.05 a week.
    /// </summary>
    public class WeeklyBonusTests
    {
        public WeeklyBonusTests() => TestGame.Reset(patched: true);

        /// <summary>
        /// What the mod's roll adds to a relationship that otherwise wouldn't move.
        /// </summary>
        public const float Bonus = 0.05f / 2;

        /// <summary>
        /// A sign with no weekly bonus and no effect on how relationships move, as the other idol.
        /// </summary>
        private const Zodiac Plain = Zodiac.Scorpio;

        /// <summary>
        /// Runs the game's weekly relationship drift, with the mod's postfix, and returns how far it moved.
        /// </summary>
        private static float Week(Relationships._relationship relationship)
        {
            TestGame.CallPrivate(TestGame.Component<Relationships>(), typeof(Relationships), "Do_Dynamic");
            return relationship.Temp;
        }

        private static float Week(data_girls.girls a, data_girls.girls b) => Week(TestGame.Pair(a, b));

        [Theory]
        [InlineData(Zodiac.Pisces, PISCES_REL_CHANCE)]
        [InlineData(Zodiac.Cancer, CANCER_REL_CHANCE)]
        [InlineData(Zodiac.Virgo, VIRGO_REL_CHANCE)]
        [InlineData(Zodiac.Libra, LIBRA_REL_CHANCE)]
        [InlineData(Zodiac.Sagittarius, SAGG_REL_CHANCE)]
        [InlineData(Zodiac.Capricorn, CAPR_REL_CHANCE)]
        [InlineData(Zodiac.Aries, ARIES_REL_CHANCE)]
        public void ConditionMet_RollsTheSignsChance(Zodiac sign, int chance)
        {
            data_girls.girls idol = TestGame.Idol(sign);
            data_girls.girls other = TestGame.Idol(Plain, statValue: 80f);
            other.getParam(data_girls._paramType.scandalPoints)._val = 1f;
            TestGame.Clique(idol, other).Bullied_Girls.Add(other);
            Pushes.Girls[0] = idol;
            Seams.Chance = _ => true;

            Assert.Equal(Bonus, Week(idol, other), 5);
            Assert.Equal(new[] { chance }, Seams.ChancesRolled);
        }

        [Fact]
        public void FailedRoll_AddsNothing()
        {
            Assert.Equal(0f, Week(TestGame.Idol(Zodiac.Pisces), TestGame.Idol(Plain)));
            Assert.Equal(new[] { PISCES_REL_CHANCE }, Seams.ChancesRolled);
        }

        [Fact]
        public void Pisces_GetsItWithAnyone()
        {
            Seams.Chance = _ => true;
            Assert.Equal(Bonus, Week(TestGame.Idol(Zodiac.Pisces), TestGame.Idol(Plain)), 5);
        }

        /// <summary>
        /// Either idol in the pair can be the one with the sign.
        /// </summary>
        [Fact]
        public void SignOnEitherSide_Counts()
        {
            Seams.Chance = _ => true;
            Assert.Equal(Bonus, Week(TestGame.Idol(Plain), TestGame.Idol(Zodiac.Pisces)), 5);
        }

        [Fact]
        public void BothQualify_BothRoll()
        {
            Seams.Chance = _ => true;
            Assert.Equal(2 * Bonus, Week(TestGame.Idol(Zodiac.Pisces), TestGame.Idol(Zodiac.Pisces)), 5);
            Assert.Equal(2, Seams.ChancesRolled.Count);
        }

        /// <summary>
        /// The bonus comes on top of the game's own weekly drift.
        /// </summary>
        [Fact]
        public void AddsToTheGamesDrift()
        {
            Relationships._relationship relationship = TestGame.Pair(TestGame.Idol(Zodiac.Pisces), TestGame.Idol(Plain));
            relationship.Dynamic = Relationships._relationship._dynamic.positive;
            Seams.Chance = _ => true;

            Assert.Equal(0.1f / 2 + Bonus, Week(relationship), 5);
        }

        [Fact]
        public void Cancer_OnlyWithinHerClique()
        {
            Seams.Chance = _ => true;
            data_girls.girls cancer = TestGame.Idol(Zodiac.Cancer);
            data_girls.girls sameClique = TestGame.Idol(Plain);
            data_girls.girls otherClique = TestGame.Idol(Plain);
            data_girls.girls noClique = TestGame.Idol(Plain);
            TestGame.Clique(cancer, sameClique);
            TestGame.Clique(otherClique, TestGame.Idol(Plain));

            Relationships._relationship withSame = TestGame.Pair(cancer, sameClique);
            Relationships._relationship withOther = TestGame.Pair(cancer, otherClique);
            Relationships._relationship withNone = TestGame.Pair(cancer, noClique);
            Week(withSame);

            Assert.Equal(Bonus, withSame.Temp, 5);
            Assert.Equal(0f, withOther.Temp);
            Assert.Equal(0f, withNone.Temp);
        }

        /// <summary>
        /// Virgo needs the other idol's average skill above the threshold; her own doesn't count.
        /// </summary>
        [Theory]
        [InlineData(VIRGO_SKILL_THR + 1, 40, true)]
        [InlineData(VIRGO_SKILL_THR, 90, false)]
        [InlineData(40, 90, false)]
        public void Virgo_WithSkilledIdols(int otherSkill, int virgoSkill, bool bonus)
        {
            Seams.Chance = _ => true;
            float moved = Week(TestGame.Idol(Zodiac.Virgo, virgoSkill), TestGame.Idol(Plain, otherSkill));
            Assert.Equal(bonus ? Bonus : 0f, moved, 5);
        }

        /// <summary>
        /// Any clique bullying the other idol counts, including one Libra isn't in.
        /// </summary>
        [Fact]
        public void Libra_WithBulliedIdols()
        {
            Seams.Chance = _ => true;
            data_girls.girls libra = TestGame.Idol(Zodiac.Libra);
            data_girls.girls bullied = TestGame.Idol(Plain);
            data_girls.girls notBullied = TestGame.Idol(Plain);
            TestGame.Clique(TestGame.Idol(Plain), TestGame.Idol(Plain)).Bullied_Girls.Add(bullied);

            Relationships._relationship withBullied = TestGame.Pair(libra, bullied);
            Relationships._relationship withOther = TestGame.Pair(libra, notBullied);
            Week(withBullied);

            Assert.Equal(Bonus, withBullied.Temp, 5);
            Assert.Equal(0f, withOther.Temp);
        }

        [Theory]
        [InlineData(1f, true)]
        [InlineData(0f, false)]
        public void Sagittarius_WithScandalousIdols(float scandalPoints, bool bonus)
        {
            Seams.Chance = _ => true;
            data_girls.girls other = TestGame.Idol(Plain);
            TestGame.SetStat(other, data_girls._paramType.scandalPoints, scandalPoints);

            Assert.Equal(bonus ? Bonus : 0f, Week(TestGame.Idol(Zodiac.Sagittarius), other), 5);
        }

        [Theory]
        [InlineData(PartnerStatus.free, true)]
        [InlineData(PartnerStatus.taken_idol, false)]
        [InlineData(PartnerStatus.taken_outside_bf, false)]
        [InlineData(PartnerStatus.taken_outside_gf, false)]
        [InlineData(PartnerStatus.taken_player, false)]
        public void Capricorn_WithSingleIdols(PartnerStatus status, bool bonus)
        {
            Seams.Chance = _ => true;
            data_girls.girls other = TestGame.Idol(Plain);
            other.DatingData.Partner_Status = status;

            Assert.Equal(bonus ? Bonus : 0f, Week(TestGame.Idol(Zodiac.Capricorn), other), 5);
        }

        /// <summary>
        /// Aries gets the bonus with everyone while she is pushed; the other idol being pushed doesn't count.
        /// </summary>
        [Fact]
        public void Aries_WhenSheIsPushed()
        {
            Seams.Chance = _ => true;
            data_girls.girls aries = TestGame.Idol(Zodiac.Aries);
            data_girls.girls pushedOther = TestGame.Idol(Plain);
            Pushes.Girls[2] = pushedOther;
            Assert.Equal(0f, Week(aries, pushedOther));

            Pushes.Girls[1] = aries;
            Assert.Equal(Bonus, Week(TestGame.Pair(aries, TestGame.Idol(Plain))), 5);
        }

        /// <summary>
        /// The other signs have no weekly bonus, even when every condition holds.
        /// </summary>
        [Theory]
        [InlineData(Zodiac.Taurus)]
        [InlineData(Zodiac.Gemini)]
        [InlineData(Zodiac.Leo)]
        [InlineData(Zodiac.Scorpio)]
        [InlineData(Zodiac.Aquarius)]
        public void OtherSigns_NoBonus(Zodiac sign)
        {
            data_girls.girls idol = TestGame.Idol(sign);
            data_girls.girls other = TestGame.Idol(Plain, statValue: 80f);
            other.getParam(data_girls._paramType.scandalPoints)._val = 1f;
            TestGame.Clique(idol, other).Bullied_Girls.Add(other);
            Pushes.Girls[0] = idol;
            Seams.Chance = _ => true;

            Week(idol, other);
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void RelationshipWithHerself_Skipped()
        {
            Seams.Chance = _ => true;
            data_girls.girls pisces = TestGame.Idol(Zodiac.Pisces);
            Assert.Equal(0f, Week(pisces, pisces));
            Assert.Empty(Seams.ChancesRolled);
        }
    }

    /// <summary>
    /// Taurus relationships move 20% less and Gemini's 20% more, both ways and from every source.
    /// </summary>
    public class FluctuationTests
    {
        public FluctuationTests() => TestGame.Reset();

        private static float Scaled(Zodiac a, Zodiac b, float val)
        {
            Relationships._relationship relationship = TestGame.Pair(TestGame.Idol(a), TestGame.Idol(b));
            Relationships__relationship_Add.Prefix(relationship, ref val);
            return val;
        }

        [Theory]
        [InlineData(Zodiac.Taurus, Zodiac.Leo, 2f, 2f * TAURUS_REL_COEFF)]
        [InlineData(Zodiac.Leo, Zodiac.Taurus, -2f, -2f * TAURUS_REL_COEFF)]
        [InlineData(Zodiac.Gemini, Zodiac.Leo, 2f, 2f * GEMINI_REL_COEFF)]
        [InlineData(Zodiac.Leo, Zodiac.Gemini, -2f, -2f * GEMINI_REL_COEFF)]
        public void OneSign_ScalesTheChange(Zodiac a, Zodiac b, float val, float expected)
        {
            Assert.Equal(expected, Scaled(a, b, val), 5);
        }

        /// <summary>
        /// Two Taurus (or two Gemini) idols scale it once, not twice.
        /// </summary>
        [Theory]
        [InlineData(Zodiac.Taurus, 1f * TAURUS_REL_COEFF)]
        [InlineData(Zodiac.Gemini, 1f * GEMINI_REL_COEFF)]
        public void SameSignTwice_ScalesOnce(Zodiac sign, float expected)
        {
            Assert.Equal(expected, Scaled(sign, sign, 1f), 5);
        }

        [Fact]
        public void TaurusWithGemini_BothApply()
        {
            Assert.Equal(TAURUS_REL_COEFF * GEMINI_REL_COEFF, Scaled(Zodiac.Taurus, Zodiac.Gemini, 1f), 5);
        }

        [Fact]
        public void OtherSigns_Unchanged()
        {
            foreach (Zodiac a in TestGame.Signs)
            {
                foreach (Zodiac b in TestGame.Signs)
                {
                    if (a is Zodiac.Taurus or Zodiac.Gemini || b is Zodiac.Taurus or Zodiac.Gemini)
                        continue;
                    Assert.Equal(1f, Scaled(a, b, 1f));
                }
            }
        }

        /// <summary>
        /// Through the game's Add, which halves gains: a Taurus pair gains 0.8 of what others do.
        /// </summary>
        [Fact]
        public void AppliesInTheGamesAdd()
        {
            TestGame.Reset(patched: true);
            Relationships._relationship taurus = TestGame.Pair(TestGame.Idol(Zodiac.Taurus), TestGame.Idol(Zodiac.Leo));
            Relationships._relationship plain = TestGame.Pair(TestGame.Idol(Zodiac.Leo), TestGame.Idol(Zodiac.Leo));

            taurus.Add(0.5f);
            plain.Add(0.5f);

            Assert.Equal(0.25f, plain.Temp, 5);
            Assert.Equal(0.25f * TAURUS_REL_COEFF, taurus.Temp, 5);
        }
    }

    /// <summary>
    /// Leo gets +10 smart and +10 funny when the clique picks its leader, and only then.
    /// </summary>
    public class LeoLeaderTests
    {
        public LeoLeaderTests() => TestGame.Reset(patched: true);

        private static data_girls.girls Member(Zodiac sign, float funnyAndSmart)
        {
            data_girls.girls girl = TestGame.Idol(sign, name: sign.ToString());
            TestGame.SetStat(girl, data_girls._paramType.funny, funnyAndSmart);
            TestGame.SetStat(girl, data_girls._paramType.smart, funnyAndSmart);
            return girl;
        }

        /// <summary>
        /// Leo's 2 x 55 + 20 = 130 beats the other idol's 2 x 60 = 120.
        /// </summary>
        [Fact]
        public void Leo_LeadsOverASlightlyStrongerIdol()
        {
            data_girls.girls leo = Member(Zodiac.Leo, 55f);
            data_girls.girls other = Member(Zodiac.Virgo, 60f);
            Relationships._clique clique = TestGame.Clique(other, leo);

            clique.UpdateLeader();

            Assert.Same(leo, clique.Leader);
        }

        [Fact]
        public void Leo_StillLosesToAMuchStrongerIdol()
        {
            data_girls.girls leo = Member(Zodiac.Leo, 55f);
            data_girls.girls other = Member(Zodiac.Virgo, 66f);
            Relationships._clique clique = TestGame.Clique(other, leo);

            clique.UpdateLeader();

            Assert.Same(other, clique.Leader);
        }

        /// <summary>
        /// Without the mod's bonus the same clique picks the other idol.
        /// </summary>
        [Fact]
        public void NonLeo_NoBonus()
        {
            data_girls.girls virgo = Member(Zodiac.Aries, 55f);
            data_girls.girls other = Member(Zodiac.Virgo, 60f);
            Relationships._clique clique = TestGame.Clique(other, virgo);

            clique.UpdateLeader();

            Assert.Same(other, clique.Leader);
        }

        /// <summary>
        /// A Leo leader keeps the bonus when a challenger needs to beat her by 20.
        /// </summary>
        [Fact]
        public void LeoLeader_HoldsOffAChallenger()
        {
            data_girls.girls leo = Member(Zodiac.Leo, 50f);
            data_girls.girls challenger = Member(Zodiac.Virgo, 65f);
            Relationships._clique clique = TestGame.Clique(leo, challenger);
            clique.Leader = leo;
            TestGame.Pair(leo, challenger);

            clique.UpdateLeader();

            Assert.Same(leo, clique.Leader);
        }

        [Fact]
        public void OutsideLeaderChoice_StatsUnchanged()
        {
            data_girls.girls leo = Member(Zodiac.Leo, 55f);
            Relationships._clique clique = TestGame.Clique(leo, Member(Zodiac.Virgo, 60f));
            clique.UpdateLeader();

            Assert.Equal(55f, leo.getParam(data_girls._paramType.funny).val);
            Assert.Equal(55f, leo.getParam(data_girls._paramType.smart).val);
        }

        [Fact]
        public void OnlySmartAndFunny_GetTheBonus()
        {
            data_girls.girls leo = Member(Zodiac.Leo, 55f);
            patchGetVal = true;
            try
            {
                Assert.Equal(55f + LEO_LEADER_BONUS, leo.getParam(data_girls._paramType.funny).val);
                Assert.Equal(55f + LEO_LEADER_BONUS, leo.getParam(data_girls._paramType.smart).val);
                Assert.Equal(40f, leo.getParam(data_girls._paramType.dance).val);
                Assert.Equal(40f, leo.getParam(data_girls._paramType.vocal).val);
            }
            finally
            {
                patchGetVal = false;
            }
        }
    }

    /// <summary>
    /// Scorpio shrugs off 20% of attempts to bully her.
    /// </summary>
    public class ScorpioBullyingTests
    {
        public ScorpioBullyingTests() => TestGame.Reset();

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Scorpio_RollsToAvoidIt(bool roll, bool bullied)
        {
            Seams.Chance = _ => roll;
            Assert.Equal(bullied, Relationships__clique_AddBulliedGirl.Prefix(TestGame.Idol(Zodiac.Scorpio)));
            Assert.Equal(new[] { SCORP_BULLY_BONUS }, Seams.ChancesRolled);
        }

        [Fact]
        public void OtherSigns_NoRoll()
        {
            Seams.Chance = _ => true;
            foreach (Zodiac sign in TestGame.Signs)
            {
                if (sign != Zodiac.Scorpio)
                    Assert.True(Relationships__clique_AddBulliedGirl.Prefix(TestGame.Idol(sign)));
            }
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void AvoidedBullying_LeavesHerOffTheList()
        {
            TestGame.Reset(patched: true);
            Seams.Chance = _ => true;
            data_girls.girls scorpio = TestGame.Idol(Zodiac.Scorpio);
            Relationships._clique clique = TestGame.Clique(TestGame.Idol(Zodiac.Leo), TestGame.Idol(Zodiac.Leo));

            clique.AddBulliedGirl(scorpio);

            Assert.Empty(clique.Bullied_Girls);
            Assert.Equal(100f, scorpio.getParam(data_girls._paramType.mentalStamina).val);
        }
    }

    /// <summary>
    /// Idols more skilled than a pushed idol grow to dislike her each day; Aquarius does so 20% less.
    /// </summary>
    public class AquariusPushTests
    {
        public AquariusPushTests() => TestGame.Reset(patched: true);

        /// <summary>
        /// The game's daily jealousy: AddRelationship(-0.033), which it halves on the way to Add.
        /// </summary>
        private const float Jealousy = -0.033f / 2;

        /// <summary>
        /// Runs the game's daily push update with a 40-skill pushed idol and a 60-skill one, and returns
        /// how far their relationship moved.
        /// </summary>
        private static float Day(Zodiac pushedSign, Zodiac otherSign)
        {
            data_girls.girls pushed = TestGame.Idol(pushedSign, 40f);
            data_girls.girls other = TestGame.Idol(otherSign, 60f);
            data_girls.girl.AddRange(new[] { pushed, other });
            Pushes.Girls[0] = pushed;
            Relationships._relationship relationship = TestGame.Pair(other, pushed);

            TestGame.CallPrivate(TestGame.Component<Pushes>(), typeof(Pushes), "OnNewDay");
            return relationship.Temp;
        }

        [Fact]
        public void Aquarius_IsLessJealous()
        {
            Assert.Equal(Jealousy * AQUA_REL_COEFF, Day(Zodiac.Leo, Zodiac.Aquarius), 5);
        }

        [Fact]
        public void OtherSigns_FullJealousy()
        {
            Assert.Equal(Jealousy, Day(Zodiac.Leo, Zodiac.Virgo), 5);
        }

        /// <summary>
        /// It's the jealous idol's sign that counts, not the pushed idol's.
        /// </summary>
        [Fact]
        public void PushedAquarius_NoChange()
        {
            Assert.Equal(Jealousy, Day(Zodiac.Aquarius, Zodiac.Virgo), 5);
        }

        [Fact]
        public void OutsideThePushUpdate_NoChange()
        {
            data_girls.girls aquarius = TestGame.Idol(Zodiac.Aquarius);
            data_girls.girls other = TestGame.Idol(Zodiac.Leo);
            Relationships._relationship relationship = TestGame.Pair(aquarius, other);

            aquarius.AddRelationship(other, -1f);

            Assert.Equal(-0.5f, relationship.Temp, 5);
        }
    }
}
