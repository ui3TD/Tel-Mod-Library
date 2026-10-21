using System;
using System.Collections.Generic;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// An answer reaches the public when its outcome changes fans, fans' opinion, buzz, scandal points or
    /// fame; never judged by its text.
    /// </summary>
    public class EventOutcomeTests
    {
        public EventOutcomeTests() => TestEnv.Reset();

        private static Event_Manager._randomEvent._reply Answer(string target, string parameter)
        {
            Event_Manager._randomEvent._reply reply = new() { text = "Ignore it" };
            reply.Effects.Add(new data_dialogues._action { target = target, parameter = parameter, formula = "10" });
            return reply;
        }

        [Theory]
        [InlineData("resource", "buzz")]
        [InlineData("resource", "scandalPoints")]
        [InlineData("resource", "fans")]
        [InlineData("resource", "fame")]
        [InlineData("group", "add_fans")]
        [InlineData("group", "buzz")]
        [InlineData("@actor_0@", "add_fans")]
        [InlineData("@actor_1@", "add_opinion")]
        [InlineData("@actor_0@", "param famePoints")]
        [InlineData("@actor_0@", "param scandalPoints")]
        public void PublicOutcomes(string target, string parameter)
        {
            Assert.True(EventOutcome.IsPublic(Answer(target, parameter)));
        }

        [Theory]
        [InlineData("resource", "money")]
        [InlineData("@actor_0@", "param mentalStamina")]
        [InlineData("@actor_0@", "friendship")]
        [InlineData("@actor_0@", "influence")]
        [InlineData("@actor_0@", "romance")]
        [InlineData("group", "business")]
        [InlineData("meta", "start_dialogue")]
        [InlineData("meta", "trigger_event")]
        public void PrivateOutcomes(string target, string parameter)
        {
            Assert.False(EventOutcome.IsPublic(Answer(target, parameter)));
        }

        [Fact]
        public void AnAnswerWithoutEffects_IsPrivate_UnlessTheGamePostsAboutIt()
        {
            Event_Manager._randomEvent._reply reply = new() { text = "Issue a public statement" };
            Assert.False(EventOutcome.IsPublic(reply));
            reply.SNS.Add(new Event_Manager._randomEvent._reply._SNS());
            Assert.True(EventOutcome.IsPublic(reply));
            Assert.False(EventOutcome.IsPublic(null));
        }

        /// <summary>
        /// The measured state changes with each value the public sees, and not with money or stamina.
        /// </summary>
        [Fact]
        public void PublicState_ChangesWithWhatThePublicSees()
        {
            resources.resource.AddRange(new long[] { 1000, 0, 50, 2, 300 });
            data_girls.girls aya = Idol("Aya");
            List<double> start = EventOutcome.PublicState();

            resources.resource[(int)resources.type.money] += 5000;
            aya.With(data_girls._paramType.mentalStamina, 10);
            Assert.False(EventOutcome.Changed(start, EventOutcome.PublicState()));

            void Check(Action change)
            {
                List<double> before = EventOutcome.PublicState();
                change();
                Assert.True(EventOutcome.Changed(before, EventOutcome.PublicState()));
            }
            Check(() => resources.resource[(int)resources.type.buzz] += 20);
            Check(() => resources.resource[(int)resources.type.fame] += 1);
            Check(() => resources.resource[(int)resources.type.scandalPoints] += 1);
            Check(() => aya.Fans[0].people += 100);
            Check(() => aya.Fans[0].appeal += 0.1f);
            Check(() => aya.getParam(data_girls._paramType.scandalPoints)._val = 3);
            Check(() => aya.With(data_girls._paramType.famePoints, 40));
            Check(() => aya.status = data_girls._status.graduated);
        }
    }
}
