using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;
using Status = data_girls._status;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// An idol going on hiatus and coming back, with the reason fans hear by the status she had: an injury,
    /// health reasons for depression (never named), or none.
    /// </summary>
    public class HiatusTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string A = Moment.AgencySection;

        private readonly List<business.active_proposal> running = new();
        private readonly Func<IList<business.active_proposal>> runningBefore = ContractBreaks.Running;

        public HiatusTests()
        {
            TestEnv.Reset();
            ContractBreaks.Running = () => running;
        }

        public void Dispose() => ContractBreaks.Running = runningBefore;

        /// <summary>
        /// Rin in Stars with Aoi and Mio, center of two singles, friends with Aoi, bullied by Mio's clique
        /// (known), in a show on air and one in production, with an ad running.
        /// </summary>
        private data_girls.girls Rin(Status status = Status.normal)
        {
            data_girls.girls rin = Idol("Rin");
            data_girls.girls aoi = Idol("Aoi");
            data_girls.girls mio = Idol("Mio");
            data_girls.girls ema = Idol("Ema");
            Group("Stars", Today.AddYears(-2), rin, aoi, mio, ema);
            Single("Bloom", Today.AddDays(-40), 3000, rin, aoi).Styled("Pop", "Love", "Cute");
            Single("Ember", Today.AddDays(-200), 9000, rin, mio);
            Single("Not Hers", Today.AddDays(-20), 5000, aoi, rin);
            Clique(rin, aoi);
            Relationships._clique bullies = Clique(mio, ema);
            bullies.Bullied_Girls.Add(rin);
            bullies.KnownBulliedGirls.Add(rin);
            Show("Idol Kitchen", Shows._show._status.released, rin);
            Show("Secret Pilot", Shows._show._status.working, rin);
            running.Add(new business.active_proposal { Type = business._type.ad, Girl = rin, Agent_Name = "Bepis", EndDate = Today.AddMonths(2) });
            rin.status = status;
            rin.setParam(data_girls._paramType.mentalStamina, 8f);
            rin.setParam(data_girls._paramType.physicalStamina, 12f);
            return rin;
        }

        private static Shows._show Show(string title, Shows._show._status status, params data_girls.girls[] girls)
        {
            Shows._show show = new()
            {
                title = title,
                status = status,
                episodeCount = status == Shows._show._status.released ? 10 : 0,
                LaunchDate = Today.AddDays(-63),
                medium = new Shows._param { title = "Internet" },
                genre = new Shows._param { title = "Food" },
            };
            for (int i = 0; i < girls.Length; i++)
            {
                show.girls[i] = girls[i];
            }
            Shows.shows.Add(show);
            return show;
        }

        /// <summary>The game's SendOnHiatus around the patch: it breaks her contracts and refills her slots.</summary>
        private Moment Start(data_girls.girls girl, bool ran = true)
        {
            data_girls_girls_SendOnHiatus.Prefix(girl, out data_girls_girls_SendOnHiatus.Before before);
            if (ran)
            {
                running.RemoveAll(c => c.Girl == girl);
                foreach (Shows._show show in Shows.shows)
                {
                    for (int i = 0; i < show.girls.Length; i++)
                    {
                        if (show.girls[i] == girl)
                        {
                            show.girls[i] = null;
                        }
                    }
                }
                girl.HiatusEnd = Today.AddDays(90);
                girl.previous_status = girl.status;
                girl.status = Status.hiatus;
            }
            if (!data_girls_girls_SendOnHiatus.ShouldPost(girl, before, ran))
            {
                return null;
            }
            return MomentContext.HiatusStart(girl, before.Reason, before.LastEnd,
                ContractBreaks.Ended(before.Contracts).Where(c => c.Girl == girl).ToList(), before.Shows);
        }

        /// <summary>The game's FinishHiatus around the patch, on a date.</summary>
        private static Moment End(data_girls.girls girl, DateTime on, bool ran = true)
        {
            staticVars.dateTime = on;
            data_girls_girls_FinishHiatus.Prefix(girl, out data_girls_girls_FinishHiatus.Before before);
            if (ran)
            {
                girl.status = Status.normal;
            }
            if (!data_girls_girls_FinishHiatus.ShouldPost(girl, before, ran))
            {
                return null;
            }
            bool early = History.Known(before.PlannedEnd) && before.PlannedEnd.Date > on.Date;
            return MomentContext.HiatusEnd(girl, before.Reason, before.PlannedEnd, early);
        }

        [Theory]
        [InlineData(Status.injured, "an injury")]
        [InlineData(Status.depressed, "health reasons, no details given")]
        [InlineData(Status.normal, "no reason given")]
        // A status another mod adds, or one the game doesn't treat as a reason
        [InlineData((Status)99, "no reason given")]
        public void Reason_FollowsHerStatus(Status status, string reason)
        {
            data_girls.girls rin = Rin(status);
            Moment start = Start(rin);
            Assert.Equal(reason, start.Fact(F, "Reason announced"));
            Assert.Equal(reason, End(rin, Today.AddDays(90)).Fact(F, "Reason announced at the start"));
        }

        /// <summary>
        /// Mental health stays private: no prompt says depression or mental, and her line has no stamina,
        /// for any reason, so it never singles out a depressed idol.
        /// </summary>
        [Theory]
        [InlineData(Status.injured)]
        [InlineData(Status.depressed)]
        [InlineData(Status.normal)]
        public void Prompts_NeverHintAtMentalHealth(Status status)
        {
            data_girls.girls rin = Rin(status);
            foreach (Moment moment in new[] { Start(rin), End(rin, Today.AddDays(30)) })
            {
                string text = moment.Describe();
                Assert.DoesNotContain("depress", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("mental", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("stamina", moment.Fact(M, "Rin"));
            }
        }

        /// <summary>
        /// Her return date, the contracts and shows on air she leaves (read before the game removed her), her
        /// center singles, the bullying, and her friends; a show in production isn't public yet.
        /// </summary>
        [Fact]
        public void Start_GivesWhatSheLeavesAndHerPeople()
        {
            Moment moment = Start(Rin());
            Assert.Equal("an idol went on hiatus: a break from all idol activities", moment.Kind);
            Assert.Equal("Rin's hiatus", moment.Label);
            Assert.Equal("Rin", moment.Fact(F, "Idol"));
            Assert.Equal("Saturday 22 June 2024, 3 months from now", moment.Fact(F, "Expected back"));
            Assert.Contains("Bepis", moment.Fact(F, "Contracts cancelled because of this"));
            Assert.Equal("\n- \"Idol Kitchen\" (Internet, Food)", moment.Fact(F, "Shows she leaves"));
            string centers = moment.Fact(F, "Singles she was the center of");
            Assert.True(centers.IndexOf("Ember") < centers.IndexOf("Bloom"), centers);
            Assert.DoesNotContain("Not Hers", centers);
            Assert.Equal("Mio, Ema", moment.Fact(F, "Suspected to be bullied by"));
            Assert.Equal("Aoi", moment.Fact(A, "Friends in Stars"));
            Assert.NotNull(moment.Fact(A, "Most popular members"));
        }

        /// <summary>
        /// An injured idol lost her shows and contracts when she got hurt, so her hiatus lists neither.
        /// </summary>
        [Fact]
        public void Start_ForAnInjury_ListsNoShowsOrContracts()
        {
            data_girls.girls rin = Rin(Status.injured);
            // What Set_Injured did when she got hurt
            running.RemoveAll(c => c.Girl == rin);
            foreach (Shows._show show in Shows.shows)
            {
                show.girls[0] = null;
            }
            Moment moment = Start(rin);
            Assert.Null(moment.Fact(F, "Contracts cancelled because of this"));
            Assert.Null(moment.Fact(F, "Shows she leaves"));
        }

        [Fact]
        public void Start_NamesHerLastHiatus_OnlyForAKnownPastDate()
        {
            data_girls.girls rin = Rin();
            rin.HiatusEnd = Today.AddMonths(-8);
            Assert.Equal("8 months ago", Start(rin).Fact(F, "Her last hiatus ended"));

            data_girls.girls never = Rin();
            never.HiatusEnd = default;
            Assert.Null(Start(never).Fact(F, "Her last hiatus ended"));
        }

        /// <summary>A skipped original, or a call while she's already on hiatus: no post.</summary>
        [Fact]
        public void Start_OtherCalls_DoNotPost()
        {
            Assert.Null(Start(Rin(), ran: false));
            Assert.Null(Start(Rin(Status.hiatus)));
        }

        [Fact]
        public void End_OnItsDate_IsAsPlanned()
        {
            data_girls.girls rin = Rin(Status.injured);
            Start(rin);
            Moment moment = End(rin, Today.AddDays(90));
            Assert.Equal("an idol returned from hiatus", moment.Kind);
            Assert.Equal("Rin's return from hiatus", moment.Label);
            Assert.Equal("as planned", moment.Fact(F, "Returned"));
            Assert.Null(moment.Fact(F, "Shows she leaves"));
            Assert.Null(moment.Fact(F, "Contracts cancelled because of this"));
            Assert.Equal("Mio, Ema", moment.Fact(F, "Suspected to be bullied by"));
            Assert.Equal("Aoi", moment.Fact(A, "Friends in Stars"));
            Assert.NotNull(moment.Fact(F, "Singles she was the center of"));
        }

        [Fact]
        public void End_CalledBackEarly_GivesThePlannedDate()
        {
            data_girls.girls rin = Rin();
            Start(rin);
            Assert.Equal("earlier than planned (planned date: Saturday 22 June 2024)", End(rin, Today.AddDays(20)).Fact(F, "Returned"));
        }

        /// <summary>
        /// A loaded idol on hiatus: the reason comes from the status the game saved before it.
        /// </summary>
        [Fact]
        public void End_AfterALoad_ReadsTheSavedReason()
        {
            data_girls.girls rin = Rin(Status.hiatus);
            rin.previous_status = Status.depressed;
            rin.HiatusEnd = Today.AddDays(10);
            Assert.Equal("health reasons, no details given", End(rin, Today.AddDays(10)).Fact(F, "Reason announced at the start"));
        }

        /// <summary>A skipped original, or a mod that keeps her on hiatus, or one not on hiatus: no post.</summary>
        [Fact]
        public void End_OtherCalls_DoNotPost()
        {
            data_girls.girls rin = Rin();
            Start(rin);
            Assert.Null(End(rin, Today.AddDays(90), ran: false));
            Assert.Null(End(Rin(), Today.AddDays(90)));
        }

        /// <summary>The graduation's bullying, center singles and friends read as they did.</summary>
        [Fact]
        public void Graduation_IsUnchanged()
        {
            data_girls.girls rin = Rin();
            Moment moment = MomentContext.Graduation(rin, null);
            Assert.Equal("Mio, Ema", moment.Fact(F, "Suspected to be bullied by"));
            Assert.Equal("Aoi", moment.Fact(A, "Friends in Stars"));
            Assert.Null(moment.Fact(A, "Most popular members"));
            Assert.NotNull(moment.Fact(F, "Singles she was the center of"));
        }
    }
}
