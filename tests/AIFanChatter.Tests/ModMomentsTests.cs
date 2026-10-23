using HarmonyLib;
using System;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Another mod's event through ModMoments.Trigger, found by name the way the modders' guide shows.
    /// </summary>
    public class ModMomentsTests
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string A = Moment.AgencySection;
        private const string Kind = "the group opened a theme park";

        private readonly FakeHost host = new();
        private readonly FakeTransport transport = new();

        public ModMomentsTests()
        {
            TestEnv.Reset();
            TestEnv.Configure();
            Dispatcher.Instance = new Dispatcher(host, transport, new Random(1));
        }

        /// <summary>The guide's lookup: by name, as a delegate of plain .NET types.</summary>
        private static readonly Func<string, string, string[], int[], string> Trigger =
            (Func<string, string, string[], int[], string>)Delegate.CreateDelegate(typeof(Func<string, string, string[], int[], string>),
                AccessTools.Method("AIFanChatter.ModMoments:Trigger"));

        [Fact]
        public void Event_IsSent_WithItsFacts()
        {
            Assert.Null(Trigger(Kind, "the theme park opening", new[] { "Ride: the Starlight Coaster", "Visitors on the first day: 12,000" }, null));
            string body = transport.Sent.Single().Body;
            Assert.Contains("Event: " + Kind, body);
            Assert.Contains("Ride: the Starlight Coaster", body);
            Assert.Contains("Visitors on the first day: 12,000", body);
        }

        [Fact]
        public void Facts_KeepColonsInTheirValue_AndLabelFallsBackToTheKind()
        {
            Moment moment = MomentContext.ModEvent(Kind, null, new[] { "Opening time: 10:00" }, null);
            Assert.Equal("10:00", moment.Fact(F, "Opening time"));
            Assert.Equal(Kind, moment.Label);
            Assert.Equal("the park", MomentContext.ModEvent(Kind, "the park", null, null).Label);
        }

        /// <summary>
        /// Named idols get the member lines every moment gives, and the first one's group is the group;
        /// unknown IDs and repeats are left out.
        /// </summary>
        [Fact]
        public void Idols_GetMemberLines_AndTheFirstOnesGroup()
        {
            data_girls.girls aoi = Idol("Aoi");
            data_girls.girls rin = Idol("Rin");
            aoi.id = 1;
            rin.id = 2;
            Groups._group main = Group("Stars", Today.AddYears(-2), aoi);
            Group("Moons", Today.AddYears(-1), rin);

            Moment moment = MomentContext.ModEvent(Kind, null, null, new[] { 2, 99, 2 });
            string members = moment.Lines(M);
            Assert.Contains("Rin", members);
            Assert.DoesNotContain("Aoi", members);
            Assert.Single(moment.Section(M));
            Assert.Equal(Members.GroupName(main), moment.Fact(A, "Sister group of"));
        }

        [Fact]
        public void NoIdols_IsTheMainGroup()
        {
            Group("Stars", Today.AddYears(-2), Idol("Aoi"));
            Moment moment = MomentContext.ModEvent(Kind, null, null, null);
            Assert.Empty(moment.Section(M));
            Assert.Null(moment.Fact(A, "Sister group of"));
            Assert.NotNull(moment.Fact(A, "Date"));
        }

        [Theory]
        [InlineData("", null, "the kind is empty")]
        [InlineData("   ", null, "the kind is empty")]
        [InlineData(Kind, "Ride the Starlight Coaster", "a fact isn't \"Name: value\": Ride the Starlight Coaster")]
        [InlineData(Kind, ": the Starlight Coaster", "a fact isn't \"Name: value\": : the Starlight Coaster")]
        [InlineData(Kind, "Ride: ", "a fact isn't \"Name: value\": Ride: ")]
        public void BadInput_IsRefused_WithTheReason(string kind, string fact, string reason)
        {
            Assert.Equal(reason, Trigger(kind, null, fact == null ? null : new[] { fact }, null));
            Assert.Empty(transport.Sent);
            Assert.Contains(TestEnv.Logs, l => l.Contains(reason));
        }

        [Fact]
        public void Limits_KeepOneEventFromFillingThePrompt()
        {
            string longText = new('x', ModMoments.MaxLength + 1);
            Assert.Equal("the kind is longer than 200 characters", Trigger(longText, null, null, null));
            Assert.Equal("the label is longer than 200 characters", Trigger(Kind, longText, null, null));
            Assert.StartsWith("a fact is longer than 200 characters", Trigger(Kind, null, new[] { "Ride: " + longText }, null));
            Assert.Equal("there are more than 20 facts", Trigger(Kind, null, Enumerable.Range(0, 21).Select(i => "Fact " + i + ": yes").ToArray(), null));
            Assert.Equal("a fact is null", Trigger(Kind, null, new string[] { null }, null));
            Assert.Null(Trigger(Kind, null, Enumerable.Range(0, 20).Select(i => "Fact " + i + ": yes").ToArray(), null));
        }

        /// <summary>The player's limits apply as to every other moment, and the reason comes back.</summary>
        [Fact]
        public void Blocked_SendsNothing_AndSaysWhy()
        {
            TestEnv.Vars[Settings.EnabledVar] = "0";
            Assert.Equal("the mod is off", Trigger(Kind, null, null, null));
            TestEnv.Vars[Settings.EnabledVar] = "1";
            Assert.Null(Trigger(Kind, null, null, null));
            Assert.Equal("a request is still running", Trigger(Kind, null, null, null));
            Assert.Single(transport.Sent);
        }
    }
}
