using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Paid policy changes: the agency spending scandal points on its image.
    /// </summary>
    public class PolicyChangeTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string H = Moment.HistorySection;

        private readonly staticVars._playerData playerBefore = staticVars.PlayerData;
        private readonly List<staff._staff> staffBefore = staff.Staff.ToList();
        private readonly DateTime startBefore = staticVars.StartDate;

        public PolicyChangeTests()
        {
            TestEnv.Reset();
            // An event's text names the player, as the game always has one
            staticVars.PlayerData = new staticVars._playerData { FirstName = "Haruto", LastName = "Sato" };
            staff.Staff.Clear();
            staff.Staff.Add(new staff._staff { type = staff._type.player });
            staticVars.StartDate = Today.AddYears(-1);
            Points().Clear();
        }

        public void Dispose()
        {
            Points().Clear();
            staticVars.PlayerData = playerBefore;
            staff.Staff.Clear();
            staff.Staff.AddRange(staffBefore);
            staticVars.StartDate = startBefore;
        }

        private static IList Points() => (IList)PolicyChange.PointsField.GetValue(null);

        /// <summary>
        /// The dating policy as policies.json has it: its header, then Ambiguous (selected) and Forbidden, each
        /// costing 1 scandal point. A released single ends the first month's free changes.
        /// </summary>
        private static (policies.value Ambiguous, policies.value Forbidden) Dating(int cost = 1, bool singleReleased = true)
        {
            if (singleReleased)
            {
                Single("Song", Today.AddDays(-30), 1000);
            }
            policies.Values.Add(new policies.value { Type = policies._type.dating, Value = policies._value.NONE, Title = "Dating" });
            policies.value ambiguous = new() { Type = policies._type.dating, Value = policies._value.dating_ambiguous, Title = "Ambiguous", Cost_Value = cost, Selected = true };
            policies.value forbidden = new() { Type = policies._type.dating, Value = policies._value.dating_forbidden, Title = "Forbidden", Cost_Value = cost };
            policies.Values.Add(ambiguous);
            policies.Values.Add(forbidden);
            return (ambiguous, forbidden);
        }

        /// <summary>The game's _Select: the new value selected, its siblings not.</summary>
        private static bool Select(policies.value from, policies.value to, bool free, bool ran = true)
        {
            policies_value_Select.Prefix(to, free, out policies_value_Select.Before before);
            from.Selected = false;
            to.Selected = true;
            return policies_value_Select.ShouldPost(before, to, ran);
        }

        [Fact]
        public void PaidChange_Posts()
        {
            var (ambiguous, forbidden) = Dating();
            Assert.True(Select(ambiguous, forbidden, free: false));
        }

        /// <summary>
        /// Free policies, the first month's free changes, the game's own selections (loading a save, story
        /// scripts), choosing the selected value again and a skipped method get no posts.
        /// </summary>
        [Fact]
        public void FreeChanges_DoNotPost()
        {
            var (ambiguous, forbidden) = Dating(cost: 0);
            Assert.False(Select(ambiguous, forbidden, free: false));

            TestEnv.Reset();
            staticVars.StartDate = Today.AddDays(-10);
            (ambiguous, forbidden) = Dating(singleReleased: false);
            Assert.False(Select(ambiguous, forbidden, free: false));

            TestEnv.Reset();
            (ambiguous, forbidden) = Dating();
            Assert.False(Select(ambiguous, forbidden, free: true));

            TestEnv.Reset();
            (ambiguous, forbidden) = Dating();
            Assert.False(Select(ambiguous, ambiguous, free: false));

            TestEnv.Reset();
            (ambiguous, forbidden) = Dating();
            Assert.False(Select(ambiguous, forbidden, free: false, ran: false));
        }

        /// <summary>
        /// The points the player chose in the game's popup, read from its private list: each idol's and the
        /// group's, leaving out entries with no points.
        /// </summary>
        [Fact]
        public void ClearedPoints_AreReadFromThePopup()
        {
            data_girls.girls aya = Idol("Aya");
            data_girls.girls rin = Idol("Rin");
            Points().Add(new ScandalPoints_Popup._points { Girl = aya, ToRemove = 2 });
            Points().Add(new ScandalPoints_Popup._points { Girl = rin, ToRemove = 0 });
            Points().Add(new ScandalPoints_Popup._points { Group = true, ToRemove = 1 });

            List<PolicyChange.Cleared> cleared = PolicyChange.ClearedPoints();
            Assert.Equal(2, cleared.Count);
            Assert.Same(aya, cleared[0].Girl);
            Assert.Equal(2, cleared[0].Points);
            Assert.True(cleared[1].Group);
            Assert.Null(cleared[1].Girl);
        }

        [Fact]
        public void PaidChange_KeepsThePointsSpent()
        {
            data_girls.girls aya = Idol("Aya");
            var (ambiguous, forbidden) = Dating();
            Points().Add(new ScandalPoints_Popup._points { Girl = aya, ToRemove = 1 });
            policies_value_Select.Prefix(forbidden, false, out policies_value_Select.Before before);
            Assert.Same(ambiguous, before.Previous);
            Assert.Same(aya, Assert.Single(before.Cleared).Girl);
        }

        [Theory]
        [InlineData(true, false, "scandals involving Aya")]
        [InlineData(false, true, "scandals involving the group itself")]
        [InlineData(true, true, "scandals involving Aya and the group itself")]
        [InlineData(false, false, null)]
        public void Moment_GivesThePolicyAndWhoseScandalsPaidForIt(bool idol, bool group, string reason)
        {
            data_girls.girls aya = Idol("Aya");
            var (ambiguous, forbidden) = Dating();
            List<PolicyChange.Cleared> cleared = new();
            if (idol)
            {
                cleared.Add(new PolicyChange.Cleared { Girl = aya, Points = 1 });
            }
            if (group)
            {
                cleared.Add(new PolicyChange.Cleared { Group = true, Points = 1 });
            }
            Moment moment = MomentContext.PolicyChange(ambiguous, forbidden, cleared);
            Assert.Equal("the agency changed one of its policies for all groups", moment.Kind);
            Assert.Equal("Dating", moment.Fact(F, "Policy"));
            Assert.Equal("Ambiguous", moment.Fact(F, "Changed from"));
            Assert.Equal("Forbidden", moment.Fact(F, "Changed to"));
            Assert.Equal(reason, moment.Fact(F, "Reason for change"));
            Assert.Equal(idol, moment.Lines(Moment.MembersSection).Contains("Aya:"));
        }

        /// <summary>
        /// An idol's known friends, as for a graduation, labelled by idol when several paid for the change.
        /// </summary>
        [Fact]
        public void Moment_NamesKnownFriends()
        {
            data_girls.girls aya = Idol("Aya");
            data_girls.girls rin = Idol("Rin");
            data_girls.girls mao = Idol("Mao");
            string main = "Friends in " + Members.GroupName(Group("Main", Today.AddYears(-2), aya, rin, mao));
            Clique(aya, mao);
            var (ambiguous, forbidden) = Dating();

            Moment one = MomentContext.PolicyChange(ambiguous, forbidden, new List<PolicyChange.Cleared> { new() { Girl = aya, Points = 1 } });
            Assert.Equal("Mao", one.Fact(F, main));
            Assert.Null(one.Fact(F, "Suspected to be bullied by"));

            Moment two = MomentContext.PolicyChange(ambiguous, forbidden, new List<PolicyChange.Cleared>
            {
                new() { Girl = aya, Points = 1 }, new() { Girl = rin, Points = 1 },
            });
            Assert.Equal("Mao", two.Fact(F, "Aya: f" + main.Substring(1)));
            Assert.Null(two.Fact(F, main));
        }

        private static Event_Manager._activeEvent Scandal(string id, DateTime date, string text, bool canAddPoints = true, params data_girls.girls[] girls)
        {
            Event_Manager._activeEvent scandal = Event(id, date, girls);
            scandal.data.description = text;
            // The event's parts, as the game's events have them
            for (int i = 0; i < scandal.actors.Count; i++)
            {
                scandal.actors[i].data = new Event_Manager._randomEvent._actor { name = "@actor_" + i + "@" };
            }
            Event_Manager._randomEvent._reply reply = new();
            reply.Effects.Add(new data_dialogues._action { target = "resource", parameter = canAddPoints ? "scandalPoints" : "money", formula = "1" });
            scandal.data.reply.Add(reply);
            return scandal;
        }

        /// <summary>
        /// Each paying idol's latest scandal, and the group's: her latest event that can add scandal points.
        /// Events that can't, later events and other idols' events are left out.
        /// </summary>
        [Fact]
        public void History_GivesTheLatestScandals()
        {
            data_girls.girls aya = Idol("Aya");
            data_girls.girls rin = Idol("Rin");
            // An event's text names a random active idol through Unity, which only runs in the game
            aya.status = rin.status = data_girls._status.graduated;
            List<Event_Manager._activeEvent> log = new()
            {
                Scandal("old", Today.AddDays(-60), "An old scandal.", true, aya),
                Scandal("rant", Today.AddDays(-21), "Her stream rant.", true, aya),
                Scandal("good_deed", Today.AddDays(-5), "A good deed.", false, aya),
                Scandal("later", Today.AddDays(3), "Not yet.", true, aya),
                Scandal("rins", Today.AddDays(-2), "Rin's scandal.", true, rin),
                Scandal("lyrics", Today.AddDays(-40), "The group's lyrics.", true),
            };
            Moment moment = new("x");
            History.PolicyChange(moment, new List<data_girls.girls> { aya }, true, log, Today);
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("Aya's latest scandal, 21 days ago", "Her stream rant."),
                new KeyValuePair<string, string>("The group's latest scandal, 40 days ago", "The group's lyrics."),
            }, moment.Facts(H));
        }
    }
}
