using System;
using System.Collections.Generic;
using System.Linq;
using TraitFix;
using Xunit;
using static traits._trait._type;
using static TraitFix.TraitsFix;
using PartnerStatus = data_girls.girls._dating_data._partner_status;

namespace TraitsFixTests
{
    /// <summary>
    /// What a leak roll produced: the leak event waiting to open. The event is the leak, so no event means
    /// no leak, and nothing happens to the couple until the player answers it.
    /// </summary>
    internal static class Leak
    {
        public static void AssertNone() => Assert.Empty(Seams.Events);

        /// <summary>
        /// Asserts the only thing the roll produced is this event, waiting to open now, with the couple as
        /// its actors and the culprit named in its text, and that nothing has happened to the couple yet.
        /// </summary>
        public static Event_Manager._activeEvent AssertEvent(string id, data_girls.girls culprit, params data_girls.girls[] couple)
        {
            Event_Manager._activeEvent leak = Assert.Single(Seams.Events);
            Assert.Equal(id, leak.data.id);
            Assert.Equal(Event_Manager._activeEvent._state.waiting, leak.state);
            Assert.Equal(TestGame.Today, leak.date);
            Assert.Equal(couple.Length, leak.actors.Count);
            for (int i = 0; i < couple.Length; i++)
            {
                Assert.Same(couple[i], leak.actors[i].girl);
                Assert.Same(leak.data.actor[i], leak.actors[i].data);
                Assert.Equal(100f, TestGame.Stat(couple[i], data_girls._paramType.mentalStamina));
                Assert.False(couple[i].DatingData.Is_Partner_Status_Known);
            }
            Assert.Equal(ExtensionMethods.color(culprit.GetName(false), mainScript.blue), variables.Get(id + INDISCREET_CULPRIT_SUFFIX));
            Assert.Empty(Seams.ParamsAdded);
            return leak;
        }
    }

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
    /// Each week, every active Indiscreet idol has a 2% chance of letting slip one secret relationship she
    /// isn't part of, picked at random: an idol dating someone outside the group, or two idols dating each
    /// other. So leaks grow with the number of Indiscreet idols, not with the number of secrets. What
    /// happens when the player answers is checked in game (InGameTests.TelMods) and its numbers in
    /// LeakOutcomeTests.
    /// </summary>
    public class IndiscreetWeeklyRollTests
    {
        public IndiscreetWeeklyRollTests() => TestGame.Reset();

        private static data_girls.girls Dating(string name = "Aya", PartnerStatus status = PartnerStatus.taken_outside_bf)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: name));
            girl.DatingData.Partner_Status = status;
            return girl;
        }

        private static Relationships._relationship SecretCouple(string name0, string name1)
        {
            data_girls.girls girl0 = Dating(name0, PartnerStatus.taken_idol);
            data_girls.girls girl1 = Dating(name1, PartnerStatus.taken_idol);
            Relationships._relationship couple = Pair.Of(girl0, girl1);
            couple.Dating = true;
            Relationships.RelationshipsData.Add(couple);
            return couple;
        }

        private static void Week() => Data_girls_UpdateDatingStatus.Postfix();

        private static string Culprit(Event_Manager._activeEvent leak) =>
            variables.variable.Find(v => v.name == leak.data.id + INDISCREET_CULPRIT_SUFFIX)?.value;

        [Fact]
        public void Leak_StartsEvent()
        {
            data_girls.girls culprit = TestGame.Hire(TestGame.Idol(Indiscreet, name: "Cleo"));
            data_girls.girls girl = Dating();
            Seams.Chance = _ => true;

            Week();

            Assert.Equal(new float[] { INDISCREET_CHANCE }, Seams.ChancesRolled);
            Leak.AssertEvent("traitsfix_indiscreet_bf_1", culprit, girl);
        }

        [Fact]
        public void Girlfriend_StartsGirlfriendEvent()
        {
            data_girls.girls culprit = TestGame.Hire(TestGame.Idol(Indiscreet, name: "Cleo"));
            data_girls.girls girl = Dating(status: PartnerStatus.taken_outside_gf);
            Seams.Chance = _ => true;

            Week();

            Leak.AssertEvent("traitsfix_indiscreet_gf_1", culprit, girl);
        }

        [Fact]
        public void Couple_NamesBoth()
        {
            data_girls.girls culprit = TestGame.Hire(TestGame.Idol(Indiscreet, name: "Cleo"));
            Relationships._relationship couple = SecretCouple("Aya", "Beth");
            Seams.Chance = _ => true;

            Week();

            Assert.Equal(new float[] { INDISCREET_CHANCE }, Seams.ChancesRolled);
            Leak.AssertEvent("traitsfix_indiscreet_idols_1", culprit, couple.Girls[0], couple.Girls[1]);
        }

        /// <summary>
        /// The point of the weekly roll: one Indiscreet idol rolls once, however many secrets there are.
        /// </summary>
        [Fact]
        public void OneIndiscreetIdol_OneRoll_HoweverManySecrets()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            for (int i = 0; i < 5; i++)
                Dating("Out" + i);
            for (int i = 0; i < 3; i++)
                SecretCouple("A" + i, "B" + i);

            Week();

            Assert.Equal(new float[] { INDISCREET_CHANCE }, Seams.ChancesRolled);
        }

        /// <summary>
        /// Each Indiscreet idol rolls; two succeeding the same week leak two different secrets, each naming
        /// the idol who let it slip.
        /// </summary>
        [Fact]
        public void TwoIndiscreetIdols_TwoRolls_TwoDifferentLeaks()
        {
            data_girls.girls cleo = TestGame.Hire(TestGame.Idol(Indiscreet, name: "Cleo"));
            data_girls.girls dana = TestGame.Hire(TestGame.Idol(Indiscreet, name: "Dana"));
            data_girls.girls aya = Dating("Aya");
            data_girls.girls beth = Dating("Beth");
            Seams.Chance = _ => true;

            Week();

            Assert.Equal(new float[] { INDISCREET_CHANCE, INDISCREET_CHANCE }, Seams.ChancesRolled);
            Assert.Equal(2, Seams.Events.Count);
            Assert.Same(aya, Seams.Events[0].actors[0].girl);
            Assert.Same(beth, Seams.Events[1].actors[0].girl);
            Assert.Contains(cleo.girlName(false), Culprit(Seams.Events[0]));
            Assert.Contains(dana.girlName(false), Culprit(Seams.Events[1]));
        }

        [Theory]
        [InlineData(0, "traitsfix_indiscreet_bf_1")]
        [InlineData(1, "traitsfix_indiscreet_gf_1")]
        [InlineData(2, "traitsfix_indiscreet_idols_1")]
        public void RandomPick_CanBeAnySecret(int pick, string expected)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Dating("Aya");
            Dating("Beth", PartnerStatus.taken_outside_gf);
            SecretCouple("Cleo", "Dana");
            Seams.Chance = _ => true;
            Seams.RandomIndex = count => count == 3 ? pick : 0;

            Week();

            Assert.Equal(expected, Assert.Single(Seams.Events).data.id);
        }

        [Fact]
        public void NoLeakRoll_NothingHappens()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls girl = Dating();

            Week();

            Assert.Single(Seams.ChancesRolled);
            Leak.AssertNone();
            Assert.False(girl.DatingData.Is_Partner_Status_Known);
            Assert.Equal(100f, TestGame.Stat(girl, data_girls._paramType.mentalStamina));
        }

        [Theory]
        [InlineData(data_girls._status.hiatus)]
        [InlineData(data_girls._status.graduated)]
        public void NoActiveIndiscreetIdol_NoRoll(data_girls._status status)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet)).status = status;
            Dating();
            Seams.Chance = _ => true;

            Week();

            Assert.Empty(Seams.ChancesRolled);
            Leak.AssertNone();
        }

        [Fact]
        public void NoSecrets_NoRoll()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            TestGame.Hire(TestGame.Idol());

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        /// <summary>
        /// Until the player answers, the relationship isn't a secret to leak again.
        /// </summary>
        [Fact]
        public void LeakWaiting_NotLeakedAgain()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Dating();
            Seams.Chance = _ => true;

            Week();
            Seams.Events[0].state = Event_Manager._activeEvent._state.active;
            Week();

            Assert.Single(Seams.ChancesRolled);
            Assert.Single(Seams.Events);
        }

        /// <summary>
        /// Two Indiscreet idols with secrets of their own: each leaks only the other's.
        /// </summary>
        [Fact]
        public void IndiscreetIdol_DoesntLeakHerOwn()
        {
            data_girls.girls girl = Dating();
            girl.trait = Indiscreet;
            Relationships._relationship couple = SecretCouple("Beth", "Cleo");
            couple.Girls[0].trait = Indiscreet;
            Seams.Chance = _ => true;

            Week();

            Assert.Equal(2, Seams.Events.Count);
            Assert.Equal("traitsfix_indiscreet_idols_1", Seams.Events[0].data.id);
            Assert.Contains(girl.girlName(false), Culprit(Seams.Events[0]));
            Assert.Same(girl, Seams.Events[1].actors[0].girl);
            Assert.Contains(couple.Girls[0].girlName(false), Culprit(Seams.Events[1]));
        }

        [Fact]
        public void OnlyHerOwnSecret_NoRoll()
        {
            data_girls.girls girl = Dating();
            girl.trait = Indiscreet;
            Seams.Chance = _ => true;

            Week();

            Assert.Empty(Seams.ChancesRolled);
            Leak.AssertNone();
        }

        [Fact]
        public void AlreadyKnown_NotASecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Dating().DatingData.Is_Partner_Status_Known = true;

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void GraduatedIdol_NotASecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Dating().status = data_girls._status.graduated;

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        /// <summary>
        /// Idols dating each other leak as a couple, through their relationship.
        /// </summary>
        [Theory]
        [InlineData(PartnerStatus.free)]
        [InlineData(PartnerStatus.taken_idol)]
        [InlineData(PartnerStatus.taken_player)]
        public void NotDatingOutside_NotASecret(PartnerStatus status)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Dating(status: status);

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        /// <summary>
        /// While the leak waits for the player, and after it reveals the couple, the couple isn't leaked again.
        /// </summary>
        [Fact]
        public void Couple_LeaksOnlyOnce()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = SecretCouple("Aya", "Beth");
            Seams.Chance = _ => true;

            Week();
            Week();
            // Answered: the leak revealed the couple
            Seams.Events[0].state = Event_Manager._activeEvent._state.complete;
            MarkIdolRelationshipKnown(couple.Girls[0], couple.Girls[1]);
            Week();

            Assert.Single(Seams.ChancesRolled);
            Assert.Single(Seams.Events);
        }

        /// <summary>
        /// The player knows Aya is dating an idol (e.g. from flirting), but not that it's Beth.
        /// </summary>
        [Fact]
        public void Couple_OneKnown_StillSecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = SecretCouple("Aya", "Beth");
            couple.Girls[0].DatingData.Is_Partner_Status_Known = true;
            couple.Girls[0].DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_idol;
            Seams.Chance = _ => true;

            Week();

            Assert.Single(Seams.Events);
        }

        /// <summary>
        /// Aya was known to have a boyfriend before; the game doesn't clear that when she moves on.
        /// </summary>
        [Fact]
        public void Couple_StaleKnowledge_StillSecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships._relationship couple = SecretCouple("Aya", "Beth");
            couple.Girls[0].DatingData.Is_Partner_Status_Known = true;
            couple.Girls[0].DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_outside_bf;
            couple.Girls[1].DatingData.Is_Partner_Status_Known = true;
            couple.Girls[1].DatingData.Partner_Status_Known_To_Player = PartnerStatus.taken_idol;

            Week();

            Assert.Single(Seams.ChancesRolled);
            Assert.Equal(PartnerStatus.taken_outside_bf, couple.Girls[0].DatingData.Partner_Status_Known_To_Player);
        }

        [Fact]
        public void Couple_RelationshipsKnown_NotASecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            SecretCouple("Aya", "Beth").Girls[0].RelationshipsKnown = true;

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void Couple_NotDating_NotASecret()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            SecretCouple("Aya", "Beth").Dating = false;

            Week();

            Assert.Empty(Seams.ChancesRolled);
        }

        [Fact]
        public void NoOrIncompleteRelationships_NoError()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Relationships.RelationshipsData = null;
            Week();
            Relationships.RelationshipsData = new()
            {
                null,
                new Relationships._relationship { Dating = true },
                new Relationships._relationship { Dating = true, Girls = new List<data_girls.girls> { TestGame.Idol(), null } },
            };
            Week();

            Assert.Empty(Seams.ChancesRolled);
        }
    }

    /// <summary>
    /// A leak shows as one of its kind's five events, naming the couple and, as the culprit, the Indiscreet
    /// idol who let it slip. The event is the leak: when no event of that kind can start, nothing happens
    /// and the relationship stays secret.
    /// </summary>
    public class IndiscreetLeakEventTests
    {
        public IndiscreetLeakEventTests() => TestGame.Reset();

        private static data_girls.girls DatingOutside(string name)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: name));
            girl.DatingData.Partner_Status = PartnerStatus.taken_outside_bf;
            return girl;
        }

        private static void LeakAll(params data_girls.girls[] girls)
        {
            Seams.Chance = _ => true;
            // One Indiscreet idol: each week leaks the first secret not already waiting
            foreach (data_girls.girls _ in girls)
                Data_girls_UpdateDatingStatus.Postfix();
        }

        /// <summary>
        /// The game won't open the same event twice at once, so a second leak picks another of the kind.
        /// </summary>
        [Fact]
        public void EventAlreadyWaiting_PicksAnotherOfTheKind()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls aya = DatingOutside("Aya");
            data_girls.girls beth = DatingOutside("Beth");

            LeakAll(aya, beth);

            Assert.Equal(new[] { "traitsfix_indiscreet_bf_1", "traitsfix_indiscreet_bf_2" }, Seams.Events.ConvertAll(e => e.data.id));
            Assert.Same(beth, Seams.Events[1].actors[0].girl);
        }

        /// <summary>
        /// Leaks one couple of the kind after another, each answered before the next, and returns the
        /// story numbers shown.
        /// </summary>
        private static List<int> LeakInTurn(int count, string kind = "bf")
        {
            List<int> stories = new();
            for (int i = 0; i < count; i++)
            {
                data_girls.girls girl = DatingOutside("Idol" + i);
                if (kind == "gf")
                    girl.DatingData.Partner_Status = PartnerStatus.taken_outside_gf;
                LeakAll(girl);
                Event_Manager._activeEvent leak = Seams.Events[Seams.Events.Count - 1];
                leak.state = Event_Manager._activeEvent._state.complete;
                // Answered: the leak revealed it
                girl.DatingData.Is_Partner_Status_Known = true;
                stories.Add(int.Parse(leak.data.id.Substring(leak.data.id.LastIndexOf('_') + 1)));
            }
            return stories;
        }

        private static string Order(string kind) =>
            variables.variable.Find(v => v.name == INDISCREET_EVENT_PREFIX + kind + INDISCREET_ORDER_SUFFIX)?.value;

        [Fact]
        public void CompletedEvent_CanShowAgain()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));

            List<int> stories = LeakInTurn(INDISCREET_EVENTS_PER_KIND + 1);

            Assert.Contains(stories[INDISCREET_EVENTS_PER_KIND], stories.GetRange(0, INDISCREET_EVENTS_PER_KIND));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EveryStoryOfTheKind_ShownBeforeAnyRepeats(int pick)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Seams.RandomIndex = count => Math.Min(pick, count - 1);

            List<int> stories = LeakInTurn(INDISCREET_EVENTS_PER_KIND);

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, stories.OrderBy(s => s));
        }

        /// <summary>
        /// Once every story was shown, the two shown last are kept back and the three older ones are
        /// equally likely, whatever order they were shown in.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void AllShown_RepeatIsOneOfTheOlderThree(int pick)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            variables.variable.Add(new variables._variable { name = INDISCREET_EVENT_PREFIX + "bf" + INDISCREET_ORDER_SUFFIX, value = "4,2,5,1,3" });
            Seams.RandomIndex = count => count == 3 ? pick : throw new InvalidOperationException("Expected 3 candidates, got " + count);

            List<int> stories = LeakInTurn(1);

            Assert.Equal(new[] { 5, 1, 3 }.OrderBy(s => s).ElementAt(pick), stories[0]);
            Assert.Equal(stories[0] + ",4,2," + string.Join(",", new[] { 5, 1, 3 }.Where(s => s != stories[0])), Order("bf"));
        }

        [Fact]
        public void LongRun_NeverRepeatsEitherOfTheLastTwo()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            int call = 0;
            Seams.RandomIndex = count => call++ * 7 % count;

            List<int> stories = LeakInTurn(40);

            for (int i = 2; i < stories.Count; i++)
                Assert.DoesNotContain(stories[i], new[] { stories[i - 1], stories[i - 2] });
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, stories.Distinct().OrderBy(s => s));
        }

        [Fact]
        public void Kinds_KeepSeparateRecords()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));

            LeakInTurn(2, "bf");
            List<int> girlfriend = LeakInTurn(1, "gf");

            Assert.Equal("2,1", Order("bf"));
            Assert.Equal(new[] { 1 }, girlfriend);
            Assert.Equal("1", Order("gf"));
        }

        /// <summary>
        /// When the older stories are all waiting, a recent one is shown rather than no leak.
        /// </summary>
        [Fact]
        public void OnlyRecentStoriesFree_ShowsARecentOne()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            variables.variable.Add(new variables._variable { name = INDISCREET_EVENT_PREFIX + "bf" + INDISCREET_ORDER_SUFFIX, value = "4,2,5,1,3" });
            foreach (int waiting in new[] { 5, 1, 3 })
                Seams.Events.Add(new Event_Manager._activeEvent
                {
                    data = Event_Manager.GetRandomEvent("traitsfix_indiscreet_bf_" + waiting),
                    state = Event_Manager._activeEvent._state.waiting,
                });

            LeakAll(DatingOutside("Aya"));

            Assert.Equal("traitsfix_indiscreet_bf_2", Seams.Events[3].data.id);
            Assert.Equal("2,4,5,1,3", Order("bf"));
        }

        [Theory]
        [InlineData(null, "1")]
        [InlineData("", "1")]
        [InlineData("garbage", "1")]
        [InlineData("9,0,-1,x,,2,2", "1,2")]
        public void DamagedRecord_UnreadableEntriesCountAsUnseen(string record, string after)
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            variables.variable.Add(new variables._variable { name = INDISCREET_EVENT_PREFIX + "bf" + INDISCREET_ORDER_SUFFIX, value = record });

            List<int> stories = LeakInTurn(1);

            Assert.Equal(1, stories[0]);
            Assert.Equal(after, Order("bf"));
        }

        [Fact]
        public void NoEventQueued_RecordUnchanged()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls aya = DatingOutside("Aya");
            Event_Manager.RandomEvents.Clear();

            LeakAll(aya);

            Assert.Null(Order("bf"));
        }

        [Fact]
        public void AllEventsOfTheKindWaiting_NoLeak()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            List<data_girls.girls> girls = new();
            foreach (string name in new[] { "Aya", "Beth", "Cleo", "Dana", "Emi", "Fumi" })
                girls.Add(DatingOutside(name));

            LeakAll(girls.ToArray());

            Assert.Equal(INDISCREET_EVENTS_PER_KIND, Seams.Events.Count);
            Assert.DoesNotContain(Seams.Events, e => e.actors[0].girl == girls[5]);
        }

        [Fact]
        public void AllCoupleEventsWaiting_NoLeak()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            Seams.Chance = _ => true;
            List<data_girls.girls> girls = new();
            for (int i = 0; i < 2 * (INDISCREET_EVENTS_PER_KIND + 1); i++)
            {
                data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: "Idol" + i));
                girl.DatingData.Partner_Status = PartnerStatus.taken_idol;
                girls.Add(girl);
            }
            for (int i = 0; i < girls.Count; i += 2)
            {
                Relationships._relationship couple = Pair.Of(girls[i], girls[i + 1]);
                couple.Dating = true;
                Relationships.RelationshipsData.Add(couple);
            }
            for (int i = 0; i < girls.Count / 2; i++)
                Data_girls_UpdateDatingStatus.Postfix();

            Assert.Equal(INDISCREET_EVENTS_PER_KIND, Seams.Events.Count);
            Assert.DoesNotContain(Seams.Events, e => e.actors[0].girl == girls[girls.Count - 2]);
        }

        [Fact]
        public void NoEventManager_NoLeak()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls aya = DatingOutside("Aya");
            Seams.EventManager = null;

            LeakAll(aya);

            Assert.False(aya.DatingData.Is_Partner_Status_Known);
        }

        [Fact]
        public void EventsNotLoaded_NoLeak()
        {
            TestGame.Hire(TestGame.Idol(Indiscreet));
            data_girls.girls aya = DatingOutside("Aya");
            Event_Manager.RandomEvents.Clear();

            LeakAll(aya);

            Leak.AssertNone();
        }

        /// <summary>
        /// Answering any other event does nothing.
        /// </summary>
        [Fact]
        public void OtherEventAnswered_NothingHappens()
        {
            data_girls.girls aya = DatingOutside("Aya");
            Event_Manager._activeEvent other = new()
            {
                data = new Event_Manager._randomEvent { id = "dating_possible_exposure" },
                state = Event_Manager._activeEvent._state.active,
                actors = { new Event_Manager._activeEvent._actor { girl = aya } },
            };
            Seams.Events.Add(other);

            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(null, answered, true);

            Assert.Same(other, answered);
            Assert.Equal(Event_Manager._activeEvent._state.active, other.state);
            Assert.False(aya.DatingData.Is_Partner_Status_Known);
            Assert.False(aya.DatingData.Had_Scandal);
        }

        [Fact]
        public void NoActiveEvent_NoError()
        {
            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(null, answered, true);
            Event_Manager_ConcludeEvent_Leak.Prefix(null, out answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(null, answered, true);
        }
    }

    /// <summary>
    /// A leak's consequences are the game's dating scandal template answered with "Do nothing"
    /// (Event_Templates._active_template.DoCustom): damage starts at 50; an orthodox image adds 50 and costs
    /// 2 hardcore opinion, a rebellious one takes 15 and adds 1 opinion of each kind; forbidden dating adds
    /// 50 and costs 1 hardcore opinion, allowed dating takes 25 and adds 1 of each; a female partner halves
    /// the damage (rounding down) and adds 1 of each; damage over 100 then grows by half. Buzz is 20 and
    /// scandal points 1, each times damage / 100, rounded to the nearest whole number (halves to even).
    /// The in-game tests check these against the game's own template.
    /// </summary>
    public class LeakOutcomeTests
    {
        private const policies._value Neutral = policies._value.image_neutral;
        private const policies._value Orthodox = policies._value.image_orthodox;
        private const policies._value Rebellious = policies._value.image_rebellious;
        private const policies._value Forbidden = policies._value.dating_forbidden;
        private const policies._value Ambiguous = policies._value.dating_ambiguous;
        private const policies._value Allowed = policies._value.dating_allowed;

        [Theory]
        // Boyfriend
        [InlineData(false, Neutral, Forbidden, 100, 20, 1, -1, 0)]
        [InlineData(false, Neutral, Ambiguous, 50, 10, 0, 0, 0)]
        [InlineData(false, Neutral, Allowed, 25, 5, 0, 1, 1)]
        [InlineData(false, Orthodox, Forbidden, 225, 45, 2, -3, 0)]
        [InlineData(false, Orthodox, Ambiguous, 100, 20, 1, -2, 0)]
        [InlineData(false, Orthodox, Allowed, 75, 15, 1, -1, 1)]
        [InlineData(false, Rebellious, Forbidden, 85, 17, 1, 0, 1)]
        [InlineData(false, Rebellious, Ambiguous, 35, 7, 0, 1, 1)]
        [InlineData(false, Rebellious, Allowed, 10, 2, 0, 2, 2)]
        // Girlfriend, or two idols
        [InlineData(true, Neutral, Forbidden, 50, 10, 0, 0, 1)]
        [InlineData(true, Neutral, Ambiguous, 25, 5, 0, 1, 1)]
        [InlineData(true, Neutral, Allowed, 12, 2, 0, 2, 2)]
        [InlineData(true, Orthodox, Forbidden, 75, 15, 1, -2, 1)]
        [InlineData(true, Orthodox, Ambiguous, 50, 10, 0, -1, 1)]
        [InlineData(true, Orthodox, Allowed, 37, 7, 0, 0, 2)]
        [InlineData(true, Rebellious, Forbidden, 42, 8, 0, 1, 2)]
        [InlineData(true, Rebellious, Ambiguous, 17, 3, 0, 2, 2)]
        [InlineData(true, Rebellious, Allowed, 5, 1, 0, 3, 3)]
        public void MatchesScandalTemplateDoNothing(bool femalePartner, policies._value image, policies._value dating,
            int damage, int buzz, int scandalPoints, float hardcoreOpinion, float casualOpinion)
        {
            LeakOutcome outcome = GetLeakOutcome(femalePartner, image, dating);

            Assert.Equal(damage, outcome.Damage);
            Assert.Equal(buzz, outcome.Buzz);
            Assert.Equal(scandalPoints, outcome.ScandalPoints);
            Assert.Equal(hardcoreOpinion, outcome.HardcoreOpinion);
            Assert.Equal(casualOpinion, outcome.CasualOpinion);
            // The template breaks contracts, and calls a reaction "high damage", from 50
            Assert.Equal(damage >= 50, outcome.IsHighDamage);
        }
    }

    /// <summary>
    /// Answering a leak (Event_Manager.ConcludeEvent, as its reply is clicked) applies the dating scandal
    /// template's "Do nothing" consequences to each idol in the couple, with the mod's stamina loss, the
    /// template's scandal flags and the reveal, and shows the results as the template does. The game's own
    /// methods it calls are stubbed and recorded; the in-game tests check the popups.
    /// </summary>
    public class LeakAnswerTests
    {
        private const traits._trait._type ThickSkin = traits._trait._type.Thick_Skin;
        private const traits._trait._type Shameless = traits._trait._type.Shameless;

        public LeakAnswerTests() => TestGame.Reset();

        private static void SetPolicies(policies._value image, policies._value dating)
        {
            TestGame.SetDatingPolicy(dating);
            policies.Values.Add(new policies.value { Type = policies._type.image, Value = image, Selected = true });
        }

        private static Event_Manager._activeEvent Answer(string id, params data_girls.girls[] couple) => AnswerWith(id, 1, couple);

        /// <summary>
        /// Answers the leak with its reply of this number, from 1.
        /// </summary>
        private static Event_Manager._activeEvent AnswerWith(string id, int reply, params data_girls.girls[] couple)
        {
            Event_Manager._randomEvent data = Event_Manager.GetRandomEvent(id);
            Event_Manager._activeEvent leak = new() { data = data, state = Event_Manager._activeEvent._state.active };
            for (int i = 0; i < couple.Length; i++)
                leak.actors.Add(new Event_Manager._activeEvent._actor { data = data.actor[i], girl = couple[i] });
            Seams.Events.Add(leak);

            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(data.reply[reply - 1], answered, true);
            return leak;
        }

        /// <summary>
        /// The results start with the chosen reply's own result, its names filled in, then the reaction.
        /// </summary>
        private static string Results(string id, int reply, string reaction) =>
            ExtensionMethods.color("{" + Language.Data[id + INDISCREET_RESULT_SUFFIX + reply] + "}", mainScript.blue)
            + mainScript.separator + ExtensionMethods.color(Language.Data[reaction], mainScript.blue);

        private static data_girls.girls Idol(string name, PartnerStatus status)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: name));
            girl.DatingData.Partner_Status = status;
            return girl;
        }

        private static void AssertRevealedAndFlagged(data_girls.girls girl, PartnerStatus known)
        {
            Assert.True(girl.DatingData.Is_Partner_Status_Known);
            Assert.Equal(known, girl.DatingData.Partner_Status_Known_To_Player);
            Assert.True(girl.DatingData.Had_Scandal);
            Assert.True(girl.DatingData.Had_Scandal_Ever);
            Assert.True(girl.DatingData.Used_Goods);
            Assert.Equal(TestGame.Today, girl.LastDatingScandal);
        }

        /// <summary>
        /// Two idols, forbidden dating, orthodox image: damage 75 (high), 1 scandal point and 2 hardcore
        /// opinion lost each, 15 buzz, contracts broken, and the Seiso achievement as in the template.
        /// </summary>
        [Fact]
        public void Couple_ForbiddenOrthodox()
        {
            SetPolicies(policies._value.image_orthodox, policies._value.dating_forbidden);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_idol);
            data_girls.girls beth = Idol("Beth", PartnerStatus.taken_idol);
            Seams.Liability = 5000;

            Event_Manager._activeEvent leak = Answer("traitsfix_indiscreet_idols_1", aya, beth);

            Assert.Equal(Event_Manager._activeEvent._state.complete, leak.state);
            Assert.Equal(new[]
            {
                (aya, data_girls._paramType.mentalStamina, INDISCREET_MENTAL),
                (beth, data_girls._paramType.mentalStamina, INDISCREET_MENTAL),
                (aya, data_girls._paramType.scandalPoints, 1f),
                (beth, data_girls._paramType.scandalPoints, 1f),
            }, Seams.ParamsAdded);
            Assert.Equal(new[] { (resources.type.buzz, 15L) }, Seams.ResourcesAdded);
            Assert.Equal(new[] { ((data_girls.girls)aya, (resources.fanType?)resources.fanType.hardcore, -2f, true), (beth, resources.fanType.hardcore, -2f, true) }, Seams.FansAdded);
            Assert.Equal(new[] { (aya, resources.fanType.hardcore, -2f), (beth, resources.fanType.hardcore, -2f) }, Seams.AppealAdded);
            Assert.Same(leak.actors, Assert.Single(Seams.ContractsBroken));
            Assert.Equal(new[] { Achievements.ID.ACH_SEISO }, Seams.AchievementsUnlocked);
            AssertRevealedAndFlagged(aya, PartnerStatus.taken_idol);
            AssertRevealedAndFlagged(beth, PartnerStatus.taken_idol);
            Assert.True(IsKnownToDateIdol(aya));

            (string text, data_girls.girls girl0, data_girls.girls girl1) = Assert.Single(Seams.ResultsShown);
            Assert.Same(aya, girl0);
            Assert.Same(beth, girl1);
            Assert.StartsWith(Results("traitsfix_indiscreet_idols_1", 1, INDISCREET_LABEL_REACTION_HIGH), text);
            Assert.Contains("Aya: ", text);
            Assert.Contains("Beth: ", text);
            Assert.Contains("SCANDAL_POINTS", text);
            Assert.Contains("+1", text);
            Assert.Contains("BIZ__LIABILITY", text);
            Assert.Contains("Aya's fans", text);
            Assert.Contains("Beth's fans", text);
            Assert.DoesNotContain("FAN_C", text);
        }

        /// <summary>
        /// A boyfriend, allowed dating, neutral image: damage 25 (low), no scandal points, contracts or fans,
        /// 5 buzz; still the stamina loss, the flags and the reveal.
        /// </summary>
        [Fact]
        public void Boyfriend_AllowedNeutral()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_allowed);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_bf);
            Seams.Liability = 5000;

            Answer("traitsfix_indiscreet_bf_1", aya);

            Assert.Equal(new[] { (aya, data_girls._paramType.mentalStamina, INDISCREET_MENTAL) }, Seams.ParamsAdded);
            Assert.Equal(new[] { (resources.type.buzz, 5L) }, Seams.ResourcesAdded);
            Assert.Empty(Seams.FansAdded);
            Assert.Empty(Seams.ContractsBroken);
            Assert.Empty(Seams.AchievementsUnlocked);
            AssertRevealedAndFlagged(aya, PartnerStatus.taken_outside_bf);

            (string text, data_girls.girls girl0, data_girls.girls girl1) = Assert.Single(Seams.ResultsShown);
            Assert.Same(aya, girl0);
            Assert.Null(girl1);
            Assert.StartsWith(Results("traitsfix_indiscreet_bf_1", 1, INDISCREET_LABEL_REACTION_LOW), text);
            Assert.DoesNotContain("SCANDAL_POINTS", text);
            Assert.DoesNotContain("BIZ__LIABILITY", text);
        }

        /// <summary>
        /// A girlfriend halves the damage: forbidden dating, neutral image gives damage 50, which still
        /// breaks contracts but rounds the scandal point to 0, and no fans are lost (the halving adds 1).
        /// </summary>
        [Fact]
        public void Girlfriend_ForbiddenNeutral()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_forbidden);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_gf);
            Seams.Liability = 5000;

            Answer("traitsfix_indiscreet_gf_1", aya);

            Assert.Equal(new[] { (aya, data_girls._paramType.mentalStamina, INDISCREET_MENTAL) }, Seams.ParamsAdded);
            Assert.Empty(Seams.FansAdded);
            Assert.Single(Seams.ContractsBroken);
            AssertRevealedAndFlagged(aya, PartnerStatus.taken_outside_gf);
        }

        /// <summary>
        /// Another mod's prefix skipped the game's handling of the reply: no leak.
        /// </summary>
        [Fact]
        public void ReplySkippedByAnotherMod_NothingHappens()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_forbidden);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_bf);
            Event_Manager._activeEvent leak = new()
            {
                data = Event_Manager.GetRandomEvent("traitsfix_indiscreet_bf_1"),
                state = Event_Manager._activeEvent._state.active,
                actors = { new Event_Manager._activeEvent._actor { girl = aya } },
            };
            Seams.Events.Add(leak);

            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(null, answered, false);

            Assert.Equal(Event_Manager._activeEvent._state.active, leak.state);
            Assert.Empty(Seams.ParamsAdded);
            Assert.Empty(Seams.ResultsShown);
            Assert.False(aya.DatingData.Had_Scandal);
        }

        /// <summary>
        /// An idol of the couple is gone by the time the reply is chosen (e.g. another mod removed her):
        /// nothing happens, but the event is finished so it can't keep her partner from leaking again.
        /// </summary>
        [Fact]
        public void IdolGone_NothingHappensButEventFinishes()
        {
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_idol);
            Event_Manager._activeEvent leak = new()
            {
                data = Event_Manager.GetRandomEvent("traitsfix_indiscreet_idols_1"),
                state = Event_Manager._activeEvent._state.active,
                actors = { new Event_Manager._activeEvent._actor { girl = aya }, new Event_Manager._activeEvent._actor() },
            };
            Seams.Events.Add(leak);

            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(null, answered, true);

            Assert.Equal(Event_Manager._activeEvent._state.complete, leak.state);
            Assert.Empty(Seams.ParamsAdded);
            Assert.Empty(Seams.ResultsShown);
            Assert.False(IsInWaitingLeak(aya));
        }

        /// <summary>
        /// As in the template, Thick Skin and Shameless idols lose no mental stamina to the scandal.
        /// </summary>
        [Theory]
        [InlineData(ThickSkin)]
        [InlineData(Shameless)]
        public void ThickSkinOrShameless_NoStaminaLoss(traits._trait._type trait)
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_allowed);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_idol);
            data_girls.girls beth = Idol("Beth", PartnerStatus.taken_idol);
            aya.trait = trait;

            Answer("traitsfix_indiscreet_idols_1", aya, beth);

            Assert.Equal(new[] { (beth, data_girls._paramType.mentalStamina, INDISCREET_MENTAL) }, Seams.ParamsAdded);
            string text = Assert.Single(Seams.ResultsShown).text;
            Assert.DoesNotContain("Aya: ", text);
            Assert.Contains("Beth: ", text);
            AssertRevealedAndFlagged(aya, PartnerStatus.taken_idol);
        }

        [Fact]
        public void BothSpared_NoStaminaLines()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_allowed);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_idol);
            data_girls.girls beth = Idol("Beth", PartnerStatus.taken_idol);
            aya.trait = ThickSkin;
            beth.trait = Shameless;

            Answer("traitsfix_indiscreet_idols_1", aya, beth);

            Assert.Empty(Seams.ParamsAdded);
            Assert.Equal(Results("traitsfix_indiscreet_idols_1", 1, INDISCREET_LABEL_REACTION_LOW), Assert.Single(Seams.ResultsShown).text);
        }

        /// <summary>
        /// The second reply has its own result and the same consequences.
        /// </summary>
        [Fact]
        public void SecondReply_OwnResultSameConsequences()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_allowed);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_bf);

            AnswerWith("traitsfix_indiscreet_bf_1", 2, aya);

            Assert.Equal(new[] { (aya, data_girls._paramType.mentalStamina, INDISCREET_MENTAL) }, Seams.ParamsAdded);
            Assert.StartsWith(Results("traitsfix_indiscreet_bf_1", 2, INDISCREET_LABEL_REACTION_LOW), Assert.Single(Seams.ResultsShown).text);
            AssertRevealedAndFlagged(aya, PartnerStatus.taken_outside_bf);
        }

        /// <summary>
        /// A reply the event doesn't have (another mod changed it) has no result of its own.
        /// </summary>
        [Fact]
        public void UnknownReply_ReactionOnly()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_allowed);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_bf);
            Event_Manager._activeEvent leak = new()
            {
                data = Event_Manager.GetRandomEvent("traitsfix_indiscreet_bf_1"),
                state = Event_Manager._activeEvent._state.active,
                actors = { new Event_Manager._activeEvent._actor { girl = aya } },
            };
            Seams.Events.Add(leak);

            Event_Manager_ConcludeEvent_Leak.Prefix(Seams.EventManager, out Event_Manager._activeEvent answered);
            Event_Manager_ConcludeEvent_Leak.Postfix(new Event_Manager._randomEvent._reply(), answered, true);

            Assert.StartsWith(ExtensionMethods.color(Language.Data[INDISCREET_LABEL_REACTION_LOW], mainScript.blue), Assert.Single(Seams.ResultsShown).text);
        }

        [Fact]
        public void NoContracts_NoLiabilityLine()
        {
            SetPolicies(policies._value.image_neutral, policies._value.dating_forbidden);
            data_girls.girls aya = Idol("Aya", PartnerStatus.taken_outside_bf);

            Answer("traitsfix_indiscreet_bf_1", aya);

            Assert.Empty(Seams.ContractsBroken);
            Assert.DoesNotContain("BIZ__LIABILITY", Assert.Single(Seams.ResultsShown).text);
        }
    }
}
