using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The agency's policies that fans notice: the concept in every moment, costumes and performance style at
    /// concerts, the dating rule in events, and handshake security only for a single promoted with handshakes.
    /// </summary>
    public class AgencyRulesTests
    {
        private const string A = Moment.AgencySection;

        public AgencyRulesTests() => TestEnv.Reset();

        private static void GameDefaults()
        {
            Policy(policies._type.vibe, policies._value.vibe_neutral);
            Policy(policies._type.image, policies._value.image_neutral);
            Policy(policies._type.dating, policies._value.dating_ambiguous);
            Policy(policies._type.skirts, policies._value.skirts_safety_shorts);
            Policy(policies._type.performances, policies._value.performances_neutral);
            Policy(policies._type.security, policies._value.security_normal);
        }

        private static Moment SingleMoment(bool handshake)
        {
            singles._single single = Single("Song", Today, 1, Idol("A"));
            return MomentContext.Single(handshake ? single.WithHandshake() : single);
        }

        private static Moment ConcertMoment() => MomentContext.Concert(Concert("Live", Today, 1000));

        private static Moment EventMoment() =>
            MomentContext.Event(Event("scandal", Today, Idol("B")), new Event_Manager._randomEvent._reply());

        [Fact]
        public void GameDefaults_LeaveOutEveryRule_ButTheDatingRuleAndCostumes()
        {
            GameDefaults();
            foreach (Moment moment in new[] { SingleMoment(handshake: true), ConcertMoment(), EventMoment() })
            {
                Assert.Null(moment.Fact(A, "Group concept"));
                Assert.Null(moment.Fact(A, "Performance style"));
                Assert.Null(moment.Fact(A, "Group handshake event security"));
            }
            Assert.Equal("short skirts with safety shorts", ConcertMoment().Fact(A, "Stage costumes"));
            Assert.Equal("unclear: dating is neither allowed nor forbidden", EventMoment().Fact(A, "Group dating rule"));
        }

        [Theory]
        [InlineData(policies._value.vibe_cute, policies._value.image_rebellious, "cute, rebellious image")]
        [InlineData(policies._value.vibe_smart, policies._value.image_orthodox, "smart, traditional idol image")]
        [InlineData(policies._value.vibe_sexy, policies._value.image_neutral, "sexy")]
        [InlineData(policies._value.vibe_neutral, policies._value.image_orthodox, "traditional idol image")]
        public void Concept_CombinesVibeAndImage_InEveryMoment(policies._value vibe, policies._value image, string expected)
        {
            Policy(policies._type.vibe, vibe);
            Policy(policies._type.image, image);
            Assert.Equal(expected, SingleMoment(handshake: false).Fact(A, "Group concept"));
            Assert.Equal(expected, ConcertMoment().Fact(A, "Group concept"));
            Assert.Equal(expected, EventMoment().Fact(A, "Group concept"));
            Assert.Equal(expected, MomentContext.Graduation(Idol("C"), null).Fact(A, "Group concept"));
        }

        [Theory]
        [InlineData(policies._value.skirts_short, "short skirts without safety shorts")]
        [InlineData(policies._value.skirts_safety_shorts, "short skirts with safety shorts")]
        [InlineData(policies._value.skirts_long_skirts, "long skirts")]
        [InlineData(policies._value.skirts_pants, "pants")]
        public void Costumes_OnlyAtConcerts(policies._value skirts, string expected)
        {
            Policy(policies._type.skirts, skirts);
            Assert.Equal(expected, ConcertMoment().Fact(A, "Stage costumes"));
            Assert.Null(SingleMoment(handshake: true).Fact(A, "Stage costumes"));
            Assert.Null(EventMoment().Fact(A, "Stage costumes"));
        }

        [Theory]
        [InlineData(policies._value.performances_energy, "energy and enthusiasm")]
        [InlineData(policies._value.performances_quality, "quality and polish")]
        public void PerformanceStyle_OnlyAtConcerts(policies._value style, string expected)
        {
            Policy(policies._type.performances, style);
            Assert.Equal(expected, ConcertMoment().Fact(A, "Performance style"));
            Assert.Null(SingleMoment(handshake: true).Fact(A, "Performance style"));
            Assert.Null(EventMoment().Fact(A, "Performance style"));
        }

        [Theory]
        [InlineData(policies._value.dating_forbidden, "dating is forbidden")]
        [InlineData(policies._value.dating_allowed, "dating is allowed")]
        public void DatingRule_OnlyInEvents(policies._value dating, string expected)
        {
            Policy(policies._type.dating, dating);
            Assert.Equal(expected, EventMoment().Fact(A, "Group dating rule"));
            Assert.Null(SingleMoment(handshake: true).Fact(A, "Group dating rule"));
            Assert.Null(ConcertMoment().Fact(A, "Group dating rule"));
        }

        [Theory]
        [InlineData(policies._value.security_relaxed, true, "relaxed")]
        [InlineData(policies._value.security_restrictive, false, "restrictive")]
        public void HandshakeSecurity_OnlyForASinglePromotedWithHandshakes(policies._value security, bool groupHandshake, string expected)
        {
            Policy(policies._type.security, security);
            singles._single single = Single("Song", Today, 1, Idol("A")).WithHandshake(groupHandshake);
            Assert.Equal(expected, MomentContext.Single(single).Fact(A, "Group handshake event security"));
            Assert.Null(SingleMoment(handshake: false).Fact(A, "Group handshake event security"));
            Assert.Null(ConcertMoment().Fact(A, "Group handshake event security"));
            Assert.Null(EventMoment().Fact(A, "Group handshake event security"));
        }

        /// <summary>
        /// Policies the game hasn't loaded, or a value it doesn't know (another mod's), leave the lines out.
        /// </summary>
        [Fact]
        public void MissingOrUnknownPolicies_LeaveTheLinesOut()
        {
            Moment none = EventMoment();
            Assert.Null(none.Fact(A, "Group concept"));
            Assert.Null(none.Fact(A, "Group dating rule"));
            Assert.NotNull(none.Fact(A, "Date"));

            Policy(policies._type.dating, (policies._value)999);
            Policy(policies._type.skirts, policies._value.vibe_cute);
            Assert.Null(EventMoment().Fact(A, "Group dating rule"));
            Assert.Null(ConcertMoment().Fact(A, "Stage costumes"));
        }

        /// <summary>The rules come after the agency's other facts and before the date.</summary>
        [Fact]
        public void Rules_ComeBeforeTheDate()
        {
            Policy(policies._type.vibe, policies._value.vibe_cool);
            Policy(policies._type.skirts, policies._value.skirts_pants);
            string lines = ConcertMoment().Lines(A);
            int concept = lines.IndexOf("Group concept: cool");
            int costumes = lines.IndexOf("Stage costumes: pants");
            int date = lines.IndexOf("Date: ");
            Assert.True(concept > lines.IndexOf("Group reputation") && concept < costumes && costumes < date, lines);
        }
    }
}
