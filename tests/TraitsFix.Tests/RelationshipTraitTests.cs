using System.Collections.Generic;
using TraitFix;
using Xunit;
using static traits._trait._type;
using static TraitFix.TraitsFix;
using PartnerStatus = data_girls.girls._dating_data._partner_status;

namespace TraitsFixTests
{
    internal static class Pair
    {
        public static Relationships._relationship Of(data_girls.girls a, data_girls.girls b)
        {
            Relationships._relationship relationship = new();
            relationship.Girls.Add(a);
            relationship.Girls.Add(b);
            return relationship;
        }
    }

    /// <summary>
    /// Maternal idols start on good terms with younger idols, and Precocious idols with older ones.
    /// </summary>
    public class AgeTraitStartTests
    {
        public AgeTraitStartTests() => TestGame.Reset();

        private static Relationships._relationship._dynamic Start(data_girls.girls a, data_girls.girls b)
        {
            Relationships._relationship relationship = Pair.Of(a, b);
            relationship.Dynamic = Relationships._relationship._dynamic.negative;
            Relationships__relationship_Initialize.Postfix(relationship);
            return relationship.Dynamic;
        }

        [Fact]
        public void Maternal_WithYounger_Positive()
        {
            Assert.Equal(Relationships._relationship._dynamic.positive, Start(TestGame.Idol(Maternal, age: 25), TestGame.Idol(age: 20)));
            Assert.Equal(Relationships._relationship._dynamic.positive, Start(TestGame.Idol(age: 20), TestGame.Idol(Maternal, age: 25)));
        }

        [Fact]
        public void Precocious_WithOlder_Positive()
        {
            Assert.Equal(Relationships._relationship._dynamic.positive, Start(TestGame.Idol(Precocious, age: 16), TestGame.Idol(age: 20)));
            Assert.Equal(Relationships._relationship._dynamic.positive, Start(TestGame.Idol(age: 20), TestGame.Idol(Precocious, age: 16)));
        }

        [Theory]
        [InlineData(Maternal, 20, 25)]
        [InlineData(Maternal, 20, 20)]
        [InlineData(Precocious, 25, 20)]
        [InlineData(Precocious, 20, 20)]
        [InlineData(None, 25, 20)]
        public void OtherwiseUnchanged(traits._trait._type trait, int age, int otherAge)
        {
            Assert.Equal(Relationships._relationship._dynamic.negative, Start(TestGame.Idol(trait, age: age), TestGame.Idol(age: otherAge)));
        }
    }

    /// <summary>
    /// The game moves a positive relationship up by 0.05 a week. Maternal and Precocious add 0.075, so
    /// those relationships develop 2.5x as fast. Arrogant idols who centered the latest single lose 0.25
    /// with everyone each week.
    /// </summary>
    public class WeeklyRelationshipTests
    {
        public WeeklyRelationshipTests() => TestGame.Reset(patched: true);

        /// <summary>
        /// Runs the game's weekly relationship drift, with the mod's postfix, and returns how far it moved.
        /// </summary>
        private static float Week(Relationships._relationship relationship)
        {
            Relationships.RelationshipsData.Add(relationship);
            TestGame.CallPrivate(TestGame.Component<Relationships>(), typeof(Relationships), "Do_Dynamic");
            return relationship.Temp;
        }

        private static Relationships._relationship Positive(data_girls.girls a, data_girls.girls b)
        {
            Relationships._relationship relationship = Pair.Of(a, b);
            relationship.Dynamic = Relationships._relationship._dynamic.positive;
            return relationship;
        }

        [Fact]
        public void Vanilla_Moves005()
        {
            Assert.Equal(0.05f, Week(Positive(TestGame.Idol(age: 25), TestGame.Idol(age: 20))), 4);
        }

        [Fact]
        public void Maternal_WithYounger_2Point5xAsFast()
        {
            float moved = Week(Positive(TestGame.Idol(Maternal, age: 25), TestGame.Idol(age: 20)));
            Assert.Equal(0.125f, moved, 4);
            Assert.Equal(2.5f, moved / 0.05f, 3);
        }

        [Fact]
        public void Precocious_WithOlder_2Point5xAsFast()
        {
            Assert.Equal(0.125f, Week(Positive(TestGame.Idol(age: 25), TestGame.Idol(Precocious, age: 20))), 4);
        }

        [Fact]
        public void Maternal_WithOlder_Vanilla()
        {
            Assert.Equal(0.05f, Week(Positive(TestGame.Idol(Maternal, age: 20), TestGame.Idol(age: 25))), 4);
        }

        [Fact]
        public void NotPositive_NoBonus()
        {
            Relationships._relationship relationship = Pair.Of(TestGame.Idol(Maternal, age: 25), TestGame.Idol(age: 20));
            relationship.Dynamic = Relationships._relationship._dynamic.neutral;
            Assert.Equal(0f, Week(relationship), 4);
        }

        [Fact]
        public void Arrogant_CenterOfLatestSingle_Loses025()
        {
            data_girls.girls arrogant = TestGame.Hire(TestGame.Idol(Arrogant));
            TestGame.ReleaseSingle(TestGame.Today.AddMonths(-1), girls: arrogant);
            Relationships._relationship relationship = Pair.Of(TestGame.Idol(), arrogant);
            relationship.Dynamic = Relationships._relationship._dynamic.neutral;

            Assert.Equal(ARROGANT_PENALTY / 2f, Week(relationship), 4);
            Assert.Equal(-0.25f, ARROGANT_PENALTY / 2f);
        }

        [Fact]
        public void Arrogant_NotCenter_Unchanged()
        {
            Relationships._relationship relationship = Pair.Of(TestGame.Hire(TestGame.Idol(Arrogant)), TestGame.Idol());
            relationship.Dynamic = Relationships._relationship._dynamic.neutral;
            Assert.Equal(0f, Week(relationship), 4);
        }

        [Fact]
        public void NoRelationships_NoError()
        {
            Relationships.RelationshipsData = null;
            Relationships_Do_Dynamic.Postfix();
            Relationships.RelationshipsData = new() { null, new Relationships._relationship() };
            Relationships_Do_Dynamic.Postfix();
        }
    }

    /// <summary>
    /// Forgiving idols never fall below a neutral relationship.
    /// </summary>
    public class ForgivingTests
    {
        public ForgivingTests() => TestGame.Reset(patched: true);

        private static Relationships._relationship WithVals(data_girls.girls a, data_girls.girls b, params int[] vals)
        {
            Relationships._relationship relationship = Pair.Of(a, b);
            relationship.Vals.AddRange(vals);
            relationship.Recalc();
            return relationship;
        }

        [Fact]
        public void Forgiving_BadRelationship_Neutral()
        {
            Relationships._relationship relationship = WithVals(TestGame.Idol(), TestGame.Idol(Forgiving), -1, -1, -1, 1);
            Assert.Equal(FORGIVING_THR, relationship.Ratio);
            Assert.Equal(Relationships._relationship._status.normal, relationship.Status);
        }

        [Fact]
        public void Forgiving_GoodRelationship_Unchanged()
        {
            Assert.Equal(0.75f, WithVals(TestGame.Idol(Forgiving), TestGame.Idol(), 1, 1, 1, -1).Ratio);
        }

        [Fact]
        public void OtherTraits_CanDislike()
        {
            Assert.Equal(0.25f, WithVals(TestGame.Idol(), TestGame.Idol(), -1, -1, -1, 1).Ratio);
        }
    }

    /// <summary>
    /// With an Indiscreet idol in the group, an idol secretly dating someone outside the group has a 2%
    /// chance each week of it leaking. She loses 30 mental stamina, and gains a scandal point if dating is
    /// forbidden. The Indiscreet idol doesn't leak her own relationship.
    /// </summary>
    public class IndiscreetOutsideTests
    {
        public IndiscreetOutsideTests() => TestGame.Reset();

        private static data_girls.girls Dating(PartnerStatus status = PartnerStatus.taken_outside_bf)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: "Aya"));
            girl.DatingData.Partner_Status = status;
            return girl;
        }

        [Fact]
        public void Leak_NotifiesAndCostsStamina()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating();
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);

            Assert.Equal(new float[] { INDISCREET_CHANCE }, Seams.ChancesRolled);
            Assert.Equal(new[] { "An indiscreet person has leaked that Aya is dating. She lost 30 mental stamina points." }, Seams.Notifications);
            Assert.Equal(70f, TestGame.Stat(girl, data_girls._paramType.mentalStamina));
            Assert.Empty(Seams.ParamsAdded);
            Assert.True(girl.DatingData.Is_Partner_Status_Known);
            Assert.Equal(PartnerStatus.taken_outside_bf, girl.DatingData.Partner_Status_Known_To_Player);
        }

        [Fact]
        public void DatingForbidden_AddsScandalPoint()
        {
            TestGame.SetDatingPolicy(policies._value.dating_forbidden);
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating(PartnerStatus.taken_outside_gf);
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);

            Assert.Equal(new[] { "An indiscreet person has leaked that Aya is dating. She lost 30 mental stamina points and gained 1 scandal point." }, Seams.Notifications);
            Assert.Equal(new[] { (girl, data_girls._paramType.scandalPoints, 1f) }, Seams.ParamsAdded);
            Assert.Equal(PartnerStatus.taken_outside_gf, girl.DatingData.Partner_Status_Known_To_Player);
        }

        [Fact]
        public void NoLeakRoll_NothingHappens()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating();

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);

            Assert.Empty(Seams.Notifications);
            Assert.False(girl.DatingData.Is_Partner_Status_Known);
            Assert.Equal(100f, TestGame.Stat(girl, data_girls._paramType.mentalStamina));
        }

        [Theory]
        [InlineData(data_girls._status.hiatus)]
        [InlineData(data_girls._status.graduated)]
        public void NoActiveIndiscreetIdol_NoLeak(data_girls._status status)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet)).status = status;
            data_girls.girls girl = Dating();
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void IndiscreetIdol_DoesntLeakHerself()
        {
            data_girls.girls girl = Dating();
            girl.trait = Indiscreet;
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void AlreadyKnown_NoRoll()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating();
            girl.DatingData.Is_Partner_Status_Known = true;
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);
            Assert.Empty(Seams.ChancesRolled);
            Assert.Empty(Seams.Notifications);
        }

        /// <summary>
        /// Idols dating each other leak through the couple's roll instead.
        /// </summary>
        [Theory]
        [InlineData(PartnerStatus.free)]
        [InlineData(PartnerStatus.taken_idol)]
        [InlineData(PartnerStatus.taken_player)]
        public void NotDatingOutside_NoRoll(PartnerStatus status)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating(status);
            Seams.Chance = _ => true;

            Data_girls_girls_UpdateDatingStatus.Postfix(girl);
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void NoIdol_NoError()
        {
            Data_girls_girls_UpdateDatingStatus.Postfix(null);
        }
    }

    /// <summary>
    /// With an Indiscreet idol in the group, two idols secretly dating each other have a 2% chance each
    /// week of it leaking, once. Both lose 30 mental stamina, and gain a scandal point if dating is
    /// forbidden. Knowing that one of them is dating someone, but not who, keeps the couple secret.
    /// </summary>
    public class IndiscreetCoupleTests
    {
        public IndiscreetCoupleTests() => TestGame.Reset();

        private data_girls.girls aya;
        private data_girls.girls beth;

        private Relationships._relationship Couple()
        {
            aya = TestGame.Hire(TestGame.Idol(name: "Aya"));
            beth = TestGame.Hire(TestGame.Idol(name: "Beth"));
            aya.DatingData.Partner_Status = PartnerStatus.taken_idol;
            beth.DatingData.Partner_Status = PartnerStatus.taken_idol;
            Relationships._relationship relationship = Pair.Of(aya, beth);
            relationship.Dating = true;
            return relationship;
        }

        [Fact]
        public void Leak_NamesBothAndCostsStamina()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            Seams.Chance = _ => true;

            TryLeakCouple(couple);

            Assert.Equal(new float[] { INDISCREET_CHANCE }, Seams.ChancesRolled);
            Assert.Equal(new[] { "An indiscreet person has leaked that Aya and Beth are dating each other. They both lost 30 mental stamina points." }, Seams.Notifications);
            Assert.Empty(Seams.ParamsAdded);
            foreach (data_girls.girls girl in new[] { aya, beth })
            {
                Assert.Equal(70f, TestGame.Stat(girl, data_girls._paramType.mentalStamina));
                Assert.True(IsKnownToDateIdol(girl));
            }
        }

        [Fact]
        public void DatingForbidden_AddsScandalPoints()
        {
            TestGame.SetDatingPolicy(policies._value.dating_forbidden);
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            Seams.Chance = _ => true;

            TryLeakCouple(couple);

            Assert.Equal(new[] { "An indiscreet person has leaked that Aya and Beth are dating each other. They both lost 30 mental stamina points and gained 1 scandal point." }, Seams.Notifications);
            Assert.Equal(new[] { (aya, data_girls._paramType.scandalPoints, 1f), (beth, data_girls._paramType.scandalPoints, 1f) }, Seams.ParamsAdded);
        }

        [Fact]
        public void LeaksOnlyOnce()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            Seams.Chance = _ => true;

            TryLeakCouple(couple);
            TryLeakCouple(couple);

            Assert.Single(Seams.ChancesRolled);
            Assert.Single(Seams.Notifications);
        }

        /// <summary>
        /// The player knows Aya is dating an idol (e.g. from flirting), but not that it's Beth.
        /// </summary>
        [Fact]
        public void OneKnown_StaysSecretUntilLeak()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            aya.DatingData.Is_Partner_Status_Known = true;
            aya.DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_idol;

            TryLeakCouple(couple);

            Assert.Single(Seams.ChancesRolled);
            Assert.Empty(Seams.Notifications);
            Assert.False(beth.DatingData.Is_Partner_Status_Known);

            Seams.Chance = _ => true;
            TryLeakCouple(couple);
            Assert.Single(Seams.Notifications);
            Assert.True(IsKnownToDateIdol(beth));
        }

        /// <summary>
        /// Aya was known to have a boyfriend before; the game doesn't clear that when she moves on.
        /// </summary>
        [Fact]
        public void StaleKnowledge_StillRolls()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            aya.DatingData.Is_Partner_Status_Known = true;
            aya.DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_outside_bf;
            beth.DatingData.Is_Partner_Status_Known = true;
            beth.DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_idol;

            TryLeakCouple(couple);

            Assert.Single(Seams.ChancesRolled);
            Assert.Equal(PartnerStatus.taken_outside_bf, aya.DatingData.Partner_Status_Known_To_Player);
        }

        [Fact]
        public void RelationshipsKnown_NoRoll()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            aya.RelationshipsKnown = true;
            Seams.Chance = _ => true;

            TryLeakCouple(couple);
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void NotDating_NoRoll()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();
            couple.Dating = false;
            Seams.Chance = _ => true;

            TryLeakCouple(couple);
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void IndiscreetIdolInCouple_DoesntLeakIt()
        {
            Relationships._relationship couple = Couple();
            aya.trait = Indiscreet;
            Seams.Chance = _ => true;

            TryLeakCouple(couple);
            Assert.Empty(Seams.Notifications);
        }

        [Fact]
        public void NoLeakRoll_NothingHappens()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = Couple();

            TryLeakCouple(couple);

            Assert.Empty(Seams.Notifications);
            Assert.False(aya.DatingData.Is_Partner_Status_Known);
            Assert.False(beth.DatingData.Is_Partner_Status_Known);
        }

        /// <summary>
        /// Fixed in 1.3.0: the leak roll was made on each couple's own weekly check, which the game stops
        /// at the first couple that starts or ends. Now every couple gets its roll after the check.
        /// </summary>
        [Fact]
        public void WeeklyCheck_RollsForEveryCouple()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship first = Couple();
            data_girls.girls cleo = TestGame.Hire(TestGame.Idol(name: "Cleo"));
            data_girls.girls dana = TestGame.Hire(TestGame.Idol(name: "Dana"));
            Relationships._relationship second = Pair.Of(cleo, dana);
            second.Dating = true;
            Relationships._relationship friends = Pair.Of(aya, cleo);
            Relationships.RelationshipsData = new() { first, friends, second };
            Seams.Chance = _ => false;

            Relationships_CheckDating.Postfix();

            Assert.Equal(new float[] { INDISCREET_CHANCE, INDISCREET_CHANCE }, Seams.ChancesRolled);
        }

        [Fact]
        public void WeeklyCheck_NoRelationships_NoError()
        {
            Relationships.RelationshipsData = null;
            Relationships_CheckDating.Postfix();
            Relationships.RelationshipsData = new() { null };
            Relationships_CheckDating.Postfix();
            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void IncompleteRelationship_NoError()
        {
            TryLeakCouple(null);
            TryLeakCouple(new Relationships._relationship { Dating = true });
            TryLeakCouple(new Relationships._relationship { Dating = true, Girls = new List<data_girls.girls> { TestGame.Idol(), null } });
            Assert.Empty(Seams.ChancesRolled);
        }
    }
}
