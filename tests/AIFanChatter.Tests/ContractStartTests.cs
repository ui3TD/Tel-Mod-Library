using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// A lasting business contract (an ad or a TV drama), posted as the player accepts it.
    /// </summary>
    public class ContractStartTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string H = Moment.HistorySection;

        private readonly List<business._data> dataBefore = business.data.ToList();
        private readonly List<business._history> historyBefore = business.History.ToList();

        public ContractStartTests()
        {
            TestEnv.Reset();
            business.data.Clear();
            business.data.Add(new business._data { business_type = business._type.ad, title = "Advertisement", duration = 3 });
            business.data.Add(new business._data { business_type = business._type.tv_drama, title = "TV Drama", duration = 3 });
            business.History.Clear();
            Language.Data["COOL"] = "Cool";
            Language.Data["CUTE"] = "Cute";
        }

        public void Dispose()
        {
            business.data.Clear();
            business.data.AddRange(dataBefore);
            business.History.Clear();
            business.History.AddRange(historyBefore);
            Language.Data.Remove("COOL");
            Language.Data.Remove("CUTE");
        }

        private static business.active_proposal Contract(business._type type, data_girls.girls girl, string client, string product = null,
            data_girls._paramType skill = data_girls._paramType.cute) =>
            new() { Type = type, Girl = girl, Agent_Name = client, Product_Name = product, Skill = skill, Payment_per_week = 500000, Fans_per_week = 4321, EndDate = Today.AddMonths(3) };

        /// <summary>
        /// The game's AddActiveProposal adds one contract: the patch finds it. A skipped method, or nothing
        /// added, finds none.
        /// </summary>
        [Fact]
        public void NewContract_IsFound_OnlyWhenAdded()
        {
            data_girls.girls rin = Idol("Rin");
            List<business.active_proposal> running = new() { Contract(business._type.ad, Idol("Aoi"), "Bepis") };
            business.active_proposal added = Contract(business._type.ad, rin, "Burger Queen");
            running.Add(added);
            Assert.Same(added, business_AddActiveProposal.Added(running, 1, true));
            Assert.Null(business_AddActiveProposal.Added(running, 1, false));
            Assert.Null(business_AddActiveProposal.Added(running, 2, true));
            Assert.Null(business_AddActiveProposal.Added(null, -1, true));
        }

        /// <summary>
        /// An ad: the idol, the game's kind, the brand, the image it uses and its length; what it pays and
        /// brings in isn't public.
        /// </summary>
        [Fact]
        public void Ad_GivesItsFacts()
        {
            data_girls.girls rin = Idol("Rin");
            business.active_proposal ad = Contract(business._type.ad, rin, "Burger Queen", skill: data_girls._paramType.cool);
            Moment moment = MomentContext.ContractStart(ad, new[] { ad }, business.History);
            Assert.Equal("an idol landed an advertising contract", moment.Kind);
            Assert.Equal("Rin's advertising contract", moment.Label);
            Assert.Equal("Rin", moment.Fact(F, "Idol"));
            Assert.Equal("Advertisement", moment.Fact(F, "Contract"));
            Assert.Equal("Burger Queen", moment.Fact(F, "Brand"));
            Assert.Null(moment.Fact(F, "Drama"));
            Assert.Equal("Cool", moment.Fact(F, "Image used"));
            Assert.Equal("3 months", moment.Fact(F, "Length"));
            string text = moment.Describe();
            Assert.DoesNotContain("500000", text);
            Assert.DoesNotContain("4321", text);
        }

        [Fact]
        public void TvDrama_NamesTheStudioAndDrama()
        {
            data_girls.girls rin = Idol("Rin");
            business.active_proposal drama = Contract(business._type.tv_drama, rin, "Webflix", "Zombies Ate My Idols");
            Moment moment = MomentContext.ContractStart(drama, new[] { drama }, business.History);
            Assert.Equal("an idol was cast in a TV drama", moment.Kind);
            Assert.Equal("Rin's TV drama", moment.Label);
            Assert.Equal("Webflix", moment.Fact(F, "Studio or channel"));
            Assert.Equal("\"Zombies Ate My Idols\"", moment.Fact(F, "Drama"));
            Assert.Null(moment.Fact(F, "Brand"));
        }

        /// <summary>
        /// Her other running contracts, and how many deals of this kind she took before; others' don't count.
        /// The moment is about her group.
        /// </summary>
        [Fact]
        public void History_GivesHerOtherContracts_AndEarlierDeals()
        {
            data_girls.girls rin = Idol("Rin"), aoi = Idol("Aoi");
            Group("Stars", Today.AddYears(-2), Idol("Main"));
            Group("Moons", Today.AddYears(-1), rin, aoi);
            business.active_proposal drama = Contract(business._type.tv_drama, rin, "Webflix", "Zombies Ate My Idols");
            business.active_proposal hers = Contract(business._type.ad, rin, "Bepis");
            business.active_proposal other = Contract(business._type.ad, aoi, "Nintender");
            business.History.Add(new business._history { Type = business._type.tv_drama, Girl = rin, Date = Today.AddMonths(-8) });
            business.History.Add(new business._history { Type = business._type.tv_drama, Girl = aoi, Date = Today.AddMonths(-3) });
            business.History.Add(new business._history { Type = business._type.ad, Girl = rin, Date = Today.AddMonths(-2) });

            Moment moment = MomentContext.ContractStart(drama, new[] { hers, other, drama }, business.History);
            Assert.Equal("\n- " + MomentContext.Contract(hers), moment.Fact(H, "Her other running contracts"));
            Assert.Equal("1, the latest 8 months ago", moment.Fact(H, "Her earlier TV dramas"));
            Assert.Equal("Moons", moment.Fact(Moment.AgencySection, "Group"));
        }
    }
}
