using System;
using System.Collections.Generic;
using Xunit;
using static AIFanChatter.Tests.TestGame;
using Reaction = Event_Templates._dating_reactions._type;

namespace AIFanChatter.Tests
{
    public class DatingScandalTests
    {
        public DatingScandalTests() => TestEnv.Reset();

        private const string F = Moment.FactsSection;

        /// <summary>
        /// The game's dating scandal (event_templates.json) cut down: two parts, then the question to the
        /// player after the last part. Her partner is unidentified, so the game names them "her partner".
        /// </summary>
        private static Event_Templates._active_template Scandal(data_girls.girls girl, string id = DatingScandal.TemplateId)
        {
            Event_Templates._template template = new() { ID = id, Formula = "[how_spotted] getting into a car with [fling].\n\n[admits]What will you do?" };
            Event_Templates._template._part spotted = new() { ID = "how_spotted" };
            Event_Templates._template._part fling = new() { ID = "fling" };
            Event_Templates._template._part admits = new() { ID = "admits" };
            template.Parts.AddRange(new[] { spotted, fling, admits });
            Event_Templates._active_template active = new() { Template = template };
            active.Parts.Add(new Event_Templates._template._part._value { Parent = spotted, Text = "Photos have surfaced of Aya" });
            active.Parts.Add(new Event_Templates._template._part._value { Parent = fling, Text = "an office worker" });
            // "admits" wasn't rolled, so it's left out
            active.Actors.Add(new Event_Manager._activeEvent._actor { girl = girl, name = "" });
            active.Actors.Add(new Event_Manager._activeEvent._actor { name = "her partner" });
            return active;
        }

        private static Event_Templates._template._reply Reply(string text) => new() { Text = text };

        private static void ReactionText(Reaction type, string text) =>
            Event_Templates.Dating_Reactions.Add(new Event_Templates._dating_reactions { Type = type, Text = text });

        [Fact]
        public void PublicOutcome_Posts()
        {
            Event_Templates._active_template active = Scandal(Idol("Aya"));
            Assert.True(Event_Templates_ConcludeEvent.ShouldPost(active, Reply("Apologize"), new[] { Reaction.Apologize__normal }, ran: true));
        }

        /// <summary>
        /// The endings that keep the scandal from the public get no posts, even next to a public one.
        /// </summary>
        [Theory]
        [InlineData(Reaction.Pay_blackmail)]
        [InlineData(Reaction.Pay_to_publication)]
        [InlineData(Reaction.Report_to_police__success)]
        [InlineData(Reaction.Report_to_police__success_2)]
        [InlineData(Reaction.Do_nothing__blackmail__no_damage)]
        [InlineData(Reaction.Deny_identity__success__unpublished)]
        public void HiddenOutcome_DoesNotPost(Reaction hidden)
        {
            Event_Templates._active_template active = Scandal(Idol("Aya"));
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(active, Reply("Pay"), new[] { hidden }, ran: true));
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(active, Reply("Pay"), new[] { Reaction.Apologize__normal, hidden }, ran: true));
        }

        /// <summary>
        /// Every ending the game has that isn't one of the six hidden ones reaches the public.
        /// </summary>
        [Fact]
        public void EveryOtherDatingOutcome_IsPublic()
        {
            int hidden = 0;
            foreach (Reaction type in Enum.GetValues(typeof(Reaction)))
            {
                if (type.ToString().StartsWith("conflict_"))
                {
                    continue;
                }
                if (DatingScandal.IsHidden(type))
                {
                    hidden++;
                }
            }
            Assert.Equal(6, hidden);
            Assert.False(DatingScandal.IsHidden(Reaction.Do_nothing__publication__high_damage));
            Assert.False(DatingScandal.IsHidden(Reaction.Deny_identity__success__unpublished_2));
        }

        [Fact]
        public void NoPost_WhenTheGameDidNotResolveIt()
        {
            data_girls.girls aya = Idol("Aya");
            Reaction[] outcome = { Reaction.Apologize__normal };
            // Another mod skipped the game's method
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(Scandal(aya), Reply("Apologize"), outcome, ran: false));
            // Another mod resolved it its own way: whether it went public is unknown
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(Scandal(aya), Reply("Apologize"), new Reaction[0], ran: true));
            // A backstage conflict between idols, which fans never see
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(Scandal(aya, "conflict"), Reply("Apologize"), outcome, ran: true));
            // No idol, or no scandal at all
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(Scandal(null), Reply("Apologize"), outcome, ran: true));
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(null, Reply("Apologize"), outcome, ran: true));
            Assert.False(Event_Templates_ConcludeEvent.ShouldPost(Scandal(aya), null, outcome, ran: true));
        }

        /// <summary>
        /// Outcomes are noted only while a scandal is being resolved, and the noting stops even when the
        /// game's method throws.
        /// </summary>
        [Fact]
        public void Outcomes_AreNotedOnlyWhileResolving()
        {
            Event_Templates_GetDatingReaction.Postfix(Reaction.Apologize__normal);
            Assert.Empty(DatingScandal.Reactions);

            Event_Templates.Active_Template = Scandal(Idol("Aya"));
            Event_Templates_ConcludeEvent.Prefix(out Event_Templates._active_template state);
            Assert.Same(Event_Templates.Active_Template, state);
            Event_Templates_GetDatingReaction.Postfix(Reaction.Apologize__normal);
            Assert.Equal(new[] { Reaction.Apologize__normal }, DatingScandal.Reactions);

            Exception thrown = new InvalidOperationException();
            Assert.Same(thrown, Event_Templates_ConcludeEvent.Finalizer(thrown));
            Assert.False(DatingScandal.Capturing);
            Event_Templates_GetDatingReaction.Postfix(Reaction.Pay_blackmail);
            Assert.Equal(new[] { Reaction.Apologize__normal }, DatingScandal.Reactions);

            // A new scandal starts with no outcomes
            Event_Templates_ConcludeEvent.Prefix(out _);
            Assert.Empty(DatingScandal.Reactions);
            Event_Templates_ConcludeEvent.Finalizer(null);
        }

        /// <summary>
        /// The scandal as the game describes it, without its question to the player; the agency's answer,
        /// the game's outcome text without its colour, the people in it, and the agency's dating rule.
        /// </summary>
        [Fact]
        public void Moment_GivesTheScandal()
        {
            ReactionText(Reaction.Apologize__normal, "Few fans seem to change their opinion after the apology.");
            Policy(policies._type.dating, policies._value.dating_forbidden);
            Moment moment = MomentContext.DatingScandal(Scandal(Idol("Aya")), Reply("Have Aya publicly apologize and put her on probation"),
                new[] { Reaction.Apologize__normal });
            Assert.Equal("Photos have surfaced of Aya getting into a car with an office worker.", moment.Fact(F, "What happened"));
            Assert.Equal("Have Aya publicly apologize and put her on probation", moment.Fact(F, "The agency's response"));
            Assert.Equal("Few fans seem to change their opinion after the apology.", moment.Fact(F, "Outcome"));
            Assert.Equal("Aya, her partner", moment.Fact(F, "People involved"));
            Assert.Null(moment.Fact(F, "You"));
            Assert.Equal("dating is forbidden", moment.Fact(Moment.AgencySection, "Group dating rule"));
            Assert.Contains("Aya:", moment.Lines(Moment.MembersSection));
            Assert.Equal("Aya's dating scandal", moment.Label);
        }

        /// <summary>
        /// The game writes some outcomes to the player; then the model is told who "you" is.
        /// </summary>
        [Fact]
        public void Moment_SayingYou_NamesTheProducer()
        {
            ReactionText(Reaction.Family__no_damage, "Fans sympathise once you explain.");
            Moment moment = MomentContext.DatingScandal(Scandal(Idol("Aya")), Reply("Say it was family"), new[] { Reaction.Family__no_damage });
            Assert.StartsWith("the group's producer", moment.Fact(F, "You"));
        }

        /// <summary>
        /// The contracts the scandal cost her, without the weeks they had left; none, no line.
        /// </summary>
        [Fact]
        public void Moment_NamesTheContractsItCancelled()
        {
            data_girls.girls aya = Idol("Aya");
            business.active_proposal ad = new() { Type = business._type.ad, Girl = aya, Agent_Name = "Bepis", EndDate = Today.AddMonths(2) };
            List<business._data> dataBefore = new(business.data);
            business.data.Clear();
            business.data.Add(new business._data { business_type = business._type.ad, title = "Advertisement", duration = 3 });
            try
            {
                Moment moment = MomentContext.DatingScandal(Scandal(aya), Reply("Deny it"), new[] { Reaction.Apologize__normal }, new[] { ad });
                Assert.Equal("\n- Advertisement from Bepis, with Aya", moment.Fact(F, "Contracts cancelled because of this"));
            }
            finally
            {
                business.data.Clear();
                business.data.AddRange(dataBefore);
            }
            Assert.Null(MomentContext.DatingScandal(Scandal(aya), Reply("Deny it"), new[] { Reaction.Apologize__normal })
                .Fact(F, "Contracts cancelled because of this"));
        }
    }
}
