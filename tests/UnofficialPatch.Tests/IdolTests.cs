using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using _partner_status = data_girls.girls._dating_data._partner_status;
using _sexuality = data_girls.girls._sexuality;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// After two idols dating each other break up, the player no longer knows either one's dating status.
    /// </summary>
    public class BreakUpTests
    {
        public BreakUpTests()
        {
            TestGame.Reset();
            Language.Data["IDOL__BROKE_UP_EACH_OTHER"] = "@1 and @2 broke up";
        }

        private static Relationships._relationship Couple(bool dating)
        {
            data_girls.girls a = TestGame.Hire(TestGame.Idol(name: "A"));
            data_girls.girls b = TestGame.Hire(TestGame.Idol(name: "B"));
            foreach (data_girls.girls girl in new[] { a, b })
            {
                girl.DatingData.Is_Partner_Status_Known = true;
                girl.DatingData.Partner_Status = dating ? _partner_status.taken_idol : _partner_status.free;
            }
            Relationships._relationship relationship = new() { Dating = dating };
            relationship.Girls.Add(a);
            relationship.Girls.Add(b);
            Relationships.RelationshipsData.Add(relationship);
            return relationship;
        }

        [Fact]
        public void DatingCouple_StatusBecomesUnknown()
        {
            Relationships._relationship couple = Couple(dating: true);

            couple.BreakUp();

            Assert.False(couple.Dating);
            Assert.Single(Seams.Notifications);
            Assert.All(couple.Girls, girl =>
            {
                Assert.Equal(_partner_status.free, girl.DatingData.Partner_Status);
                Assert.False(girl.DatingData.Is_Partner_Status_Known);
            });
        }

        [Fact]
        public void NotDating_NothingChanges()
        {
            // The game calls BreakUp on every relationship of an idol who starts dating someone
            Relationships._relationship friends = Couple(dating: false);

            friends.BreakUp();

            Assert.Empty(Seams.Notifications);
            Assert.All(friends.Girls, girl => Assert.True(girl.DatingData.Is_Partner_Status_Known));
        }
    }

    /// <summary>
    /// The profile shows dating status for idols under the age of consent too, with the same text the game
    /// shows for adults.
    /// </summary>
    public class PartnerStringTests
    {
        private static readonly string[] Keys =
        {
            "PROFILE__DATING_UNKNOWN", "PROFILE__DATING_NOT_DATING", "PROFILE__DATING_IDOL_UNKNOWN", "PROFILE__DATING_HAS_BF",
            "PROFILE__DATING_HAS_GF", "PROFILE__DATING_YOU", "PROFILE__DATING_STRAIGHT", "PROFILE__DATING_LESBIAN",
            "PROFILE__DATING_BI", "PROFILE__DATING_PREF_UNKNOWN", "PROFILE__DATING_INTERESTED", "PROFILE__DATING_NOT_INTERESTED",
        };

        public PartnerStringTests()
        {
            TestGame.Reset();
            foreach (string key in Keys)
                Language.Data[key] = "[" + key + "]";
            Language.Data["PROFILE__DATING_IDOL"] = "[dates @]";
        }

        public static IEnumerable<object[]> Statuses()
        {
            foreach (bool known in new[] { false, true })
                foreach (_partner_status status in new[] { _partner_status.free, _partner_status.taken_idol, _partner_status.taken_outside_bf, _partner_status.taken_outside_gf, _partner_status.taken_player })
                    foreach (_sexuality? sexuality in new _sexuality?[] { null, _sexuality.straight, _sexuality.lesbian, _sexuality.bi })
                        foreach (bool flirted in new[] { false, true })
                            foreach (bool uninterested in new[] { false, true })
                                yield return new object[] { known, status, sexuality, flirted, uninterested };
        }

        /// <summary>
        /// An idol of this age with this dating data. A null sexuality is one the player doesn't know.
        /// An idol dating another idol gets a girlfriend.
        /// </summary>
        private static data_girls.girls Idol(int age, bool known, _partner_status status, _sexuality? sexuality, bool flirted, bool uninterested)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol(name: "Idol", age: age));
            girl.sexuality = sexuality ?? _sexuality.lesbian;
            girl.DatingData.Is_Partner_Status_Known = known;
            girl.DatingData.Partner_Status = status;
            girl.DatingData.Partner_Status_Known_To_Player = status;
            girl.DatingData.Is_Sexuality_Known = sexuality.HasValue;
            girl.DatingData.Previous_Attempt = flirted ? Date_Flirt._flirt._category.oblivious : Date_Flirt._flirt._category.NONE;
            girl.DatingData.Is_Uninterested = uninterested;

            if (status == _partner_status.taken_idol)
            {
                data_girls.girls girlfriend = TestGame.Hire(TestGame.Idol(name: "Girlfriend"));
                Relationships._relationship couple = new() { Dating = true };
                couple.Girls.Add(girl);
                couple.Girls.Add(girlfriend);
                Relationships.RelationshipsData.Add(couple);
            }
            return girl;
        }

        [Theory]
        [MemberData(nameof(Statuses))]
        public void Underage_ShowsWhatAnAdultWouldShow(bool known, _partner_status status, _sexuality? sexuality, bool flirted, bool uninterested)
        {
            string adult = Idol(20, known, status, sexuality, flirted, uninterested).GetPartnerString();
            TestGame.Reset();
            string underage = Idol(16, known, status, sexuality, flirted, uninterested).GetPartnerString();

            Assert.NotEqual("", adult);
            Assert.Equal(adult, underage);
        }

        [Fact]
        public void Adult_IsTheGamesText()
        {
            // The game's own method builds the text; the mod only lets underage idols past its age check
            data_girls.girls girl = Idol(20, true, _partner_status.taken_idol, _sexuality.bi, true, false);
            Assert.Equal("[dates Girlfriend]\n[PROFILE__DATING_BI]\n[PROFILE__DATING_INTERESTED]", girl.GetPartnerString());
        }

        /// <summary>
        /// The mod replaces the one age check; transpiling again (or a game without the check) changes nothing.
        /// </summary>
        [Fact]
        public void Transpiler_ReplacesOnlyTheAgeCheck_AndOnlyOnce()
        {
            System.Reflection.MethodInfo method = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetPartnerString));
            List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method);
            List<CodeInstruction> once = data_girls_girls_GetPartnerString.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToList();
            List<CodeInstruction> twice = data_girls_girls_GetPartnerString.Transpiler(once).ToList();

            Assert.Equal(original.Count + 1, once.Count);
            Assert.DoesNotContain(once, ci => ci.operand is System.Reflection.MethodInfo m && m.Name == nameof(data_girls.girls.Is_AOC));
            Assert.Equal(once.Select(ci => ci.ToString()), twice.Select(ci => ci.ToString()));
        }
    }

    /// <summary>
    /// Changes to the agency's fan opinion (concerts, cancellations, events) reach every active idol's fans.
    /// </summary>
    public class FanOpinionTests
    {
        public FanOpinionTests() => TestGame.Reset();

        private static resources._fan Fan(resources.fanType age) => new()
        {
            gender = resources.fanType.male,
            hardcoreness = resources.fanType.casual,
            age = age,
            people = 100,
        };

        private static data_girls.girls IdolWithFans(data_girls._status status = data_girls._status.normal)
        {
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.status = status;
            girl.Fans.Add(Fan(resources.fanType.teen));
            girl.Fans.Add(Fan(resources.fanType.adult));
            return girl;
        }

        /// <summary>
        /// A fan's recorded opinion changes, as text so lists and NaN ratios compare by value.
        /// </summary>
        private static string Opinion(resources._fan fan) => $"[{string.Join(",", fan.Vals)}] temp {fan.Temp} ratio {fan.Ratio}";

        [Theory]
        [InlineData(2.5f)]
        [InlineData(-1.5f)]
        [InlineData(0.4f)]
        public void Opinion_ReachesTheMatchingFansOfActiveIdols(float change)
        {
            data_girls.girls girl = IdolWithFans();
            resources._fan expected = Fan(resources.fanType.teen);
            expected.AddOpinion(change);

            new resources._fanOpinion { type = resources.fanType.teen }.Add(change);

            Assert.Equal(Opinion(expected), Opinion(girl.Fans[0]));
            Assert.Equal(Opinion(Fan(resources.fanType.adult)), Opinion(girl.Fans[1]));
        }

        [Theory]
        [InlineData(data_girls._status.injured)]
        [InlineData(data_girls._status.depressed)]
        [InlineData(data_girls._status.graduated)]
        public void Opinion_SkipsSickAndGraduatedIdols(data_girls._status status)
        {
            data_girls.girls girl = IdolWithFans(status);

            new resources._fanOpinion { type = resources.fanType.teen }.Add(2f);

            Assert.Equal(Opinion(Fan(resources.fanType.teen)), Opinion(girl.Fans[0]));
        }
    }

    /// <summary>
    /// An idol never gossips about herself.
    /// </summary>
    public class GossipTests
    {
        public GossipTests() => TestGame.Reset();

        [Fact]
        public void GossipAboutTheSnitch_IsRemoved()
        {
            data_girls.girls snitch = TestGame.Idol(name: "Snitch");
            data_girls.girls other = TestGame.Idol(name: "Other");
            List<Date_Gossip._gossip> gossips = new()
            {
                new() { BullyingTarget = snitch },
                new() { BullyingTarget = other },
                new() { BullyingTarget = snitch },
                new() { BullyingTarget = null },
            };

            Date_Gossip_GetAvailableGossips.Postfix(ref gossips, snitch);

            Assert.Equal(new data_girls.girls[] { other, null }, gossips.Select(g => g.BullyingTarget));
        }

        [Fact]
        public void NoGossip_StaysEmpty()
        {
            List<Date_Gossip._gossip> gossips = new();
            Date_Gossip_GetAvailableGossips.Postfix(ref gossips, TestGame.Idol());
            Assert.Empty(gossips);
        }
    }
}
