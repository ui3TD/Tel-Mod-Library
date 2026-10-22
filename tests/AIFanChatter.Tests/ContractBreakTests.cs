using System;
using System.Collections.Generic;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Business contracts an answer to an event ended: running before the game applied it, gone after.
    /// </summary>
    public class ContractBreakTests : IDisposable
    {
        private const string F = Moment.FactsSection;

        private readonly List<business.active_proposal> running = new();
        private readonly Func<IList<business.active_proposal>> runningBefore = ContractBreaks.Running;
        private readonly List<business._data> dataBefore = new(business.data);

        public ContractBreakTests()
        {
            TestEnv.Reset();
            ContractBreaks.Running = () => running;
            business.data.Clear();
            business.data.Add(new business._data { business_type = business._type.ad, title = "Advertisement", duration = 3 });
        }

        public void Dispose()
        {
            ContractBreaks.Running = runningBefore;
            business.data.Clear();
            business.data.AddRange(dataBefore);
        }

        private business.active_proposal Running(data_girls.girls girl, string client, business._type type = business._type.ad)
        {
            business.active_proposal contract = new() { Type = type, Girl = girl, Agent_Name = client, EndDate = Today.AddMonths(2) };
            running.Add(contract);
            return contract;
        }

        /// <summary>
        /// The contracts gone after the answer, however they went; ones still running, or added, aren't.
        /// </summary>
        [Fact]
        public void Ended_AreTheOnesGone()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi");
            business.active_proposal ad = Running(rin, "Bepis");
            business.active_proposal drama = Running(rin, "Webflix", business._type.tv_drama);
            Running(aoi, "Nintender");
            List<business.active_proposal> before = ContractBreaks.Snapshot();

            running.Remove(ad);
            running.Remove(drama);
            Running(aoi, "Burger Queen");
            Assert.Equal(new[] { ad, drama }, ContractBreaks.Ended(before));
        }

        [Fact]
        public void Ended_IsEmpty_WhenNothingChanged_OrTheGameCantSay()
        {
            Running(Idol("Rin"), "Bepis");
            Assert.Empty(ContractBreaks.Ended(ContractBreaks.Snapshot()));
            ContractBreaks.Running = () => throw new InvalidOperationException("no scene");
            Assert.Empty(ContractBreaks.Snapshot());
            Assert.Empty(ContractBreaks.Ended(null));
        }

        /// <summary>
        /// A random event names the contracts it cancelled, after its outcome; none, no line.
        /// </summary>
        [Fact]
        public void Event_NamesTheContractsItCancelled()
        {
            data_girls.girls rin = Idol("Rin");
            business.active_proposal ad = new() { Type = business._type.ad, Girl = rin, Agent_Name = "Bepis", EndDate = Today.AddMonths(2) };
            Event_Manager._activeEvent active = Event("scandal", Today, rin);
            Moment moment = MomentContext.Event(active, new Event_Manager._randomEvent._reply(), null, new[] { ad });
            Assert.Equal("\n- Advertisement from Bepis, with Rin", moment.Fact(F, "Contracts cancelled because of this"));
            Assert.Null(MomentContext.Event(active, new Event_Manager._randomEvent._reply()).Fact(F, "Contracts cancelled because of this"));
        }
    }
}
