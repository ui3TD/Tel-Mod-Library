using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// Tours, posted once the player closes the results: every stop, where the agency went before, and the
    /// most popular idols, since every active idol goes.
    /// </summary>
    public class TourTests : IDisposable
    {
        private const string F = Moment.FactsSection;
        private const string H = Moment.HistorySection;

        private static readonly Dictionary<string, string> Texts = new()
        {
            ["SEVENT__WORLD_TOUR"] = "World Tour",
            ["CHINA"] = "China",
            ["FRANCE"] = "France",
            ["USA"] = "USA",
            ["SPAIN"] = "Spain",
        };

        private readonly List<int> capacityBefore = SEvent_Tour.Const_Capacity.ToList();

        public TourTests()
        {
            TestEnv.Reset();
            foreach (KeyValuePair<string, string> text in Texts)
            {
                Language.Data[text.Key] = text.Value;
            }
            // The game's venue sizes (JSON/SpecialEvents/WorldTour/constants.json)
            SEvent_Tour.Const_Capacity.Clear();
            SEvent_Tour.Const_Capacity.AddRange(new[] { 500, 2500, 8000, 15000, 30000 });
        }

        public void Dispose()
        {
            SEvent_Tour.Const_Capacity.Clear();
            SEvent_Tour.Const_Capacity.AddRange(capacityBefore);
            foreach (string key in Texts.Keys)
            {
                Language.Data.Remove(key);
            }
        }

        /// <summary>The game's FinishTour: the tour is finished, then cleared from SEvent_Tour.Tour.</summary>
        private static bool Finish(SEvent_Tour.tour tour, bool ran = true)
        {
            SEvent_Tour game = (SEvent_Tour)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SEvent_Tour));
            game.Tour = tour;
            SEvent_Tour_FinishTour.Prefix(game, out SEvent_Tour.tour captured);
            if (ran && tour != null)
            {
                tour.Status = SEvent_Tour.tour._status.finished;
                game.Tour = null;
            }
            return SEvent_Tour_FinishTour.ShouldPost(captured, ran);
        }

        [Fact]
        public void FinishedTour_PostsOnce()
        {
            SEvent_Tour.tour tour = Tour(Today, false, (SEvent_Tour._country.china, 3, 7600));
            Assert.True(Finish(tour));
            // As the postfix does when it posts
            SEvent_Tour_FinishTour.Postfix(tour, true);
            Assert.False(SEvent_Tour_FinishTour.ShouldPost(tour, true));
        }

        /// <summary>
        /// A skipped FinishTour, no current tour to read, or a tour the game didn't finish: no posts.
        /// </summary>
        [Fact]
        public void UnfinishedTours_DoNotPost()
        {
            Assert.False(Finish(Tour(Today, false, (SEvent_Tour._country.china, 3, 7600)), ran: false));
            Assert.False(Finish(null));
            SEvent_Tour_FinishTour.Postfix(null, true);
            Assert.Contains(TestEnv.Logs, l => l.Contains("no current tour"));
            Assert.False(SEvent_Tour_FinishTour.ShouldPost(Tour(Today, false), true));
        }

        /// <summary>
        /// Each stop in order, by the game's name for the place, with its audience out of the venue's seats.
        /// Hidden values (new fans, revenue, the total audience) aren't sent.
        /// </summary>
        [Fact]
        public void Tour_GivesItsStops_InOrder_WithSeats()
        {
            Idol("Rin");
            SEvent_Tour.tour tour = Tour(Today, true, (SEvent_Tour._country.france, 3, 7600), (SEvent_Tour._country.china, 5, 30000));
            tour.NewFans = 12345;
            tour.Revenue = 999999;

            Moment moment = MomentContext.Tour(tour);
            Assert.Equal("the agency's idols came back from their World Tour", moment.Kind);
            Assert.Equal("the World Tour", moment.Label);
            Assert.Equal("\n- France: 7600 out of 8000 seats, first visit\n- China: 30000 out of 30000 seats, first visit",
                moment.Fact(F, "Stops, in order"));
            string text = moment.Describe();
            Assert.DoesNotContain("12345", text);
            Assert.DoesNotContain("999999", text);
            Assert.DoesNotContain("Total audience", text);
            Assert.DoesNotContain("Idols on tour", text);
        }

        /// <summary>
        /// The groups with an idol on the tour, main group first; a group whose idols all stayed home, or a
        /// disbanded one, isn't on it.
        /// </summary>
        [Fact]
        public void GroupsOnTour_AreTheGroupsWithAnIdolThere()
        {
            data_girls.girls resting = Idol("Resting");
            resting.status = data_girls._status.hiatus;
            Group("Stars", Today.AddYears(-2), Idol("Rin"));
            Group("Moons", Today.AddYears(-1), Idol("Aoi"));
            Group("Suns", Today.AddYears(-1), resting);
            Group("Gone", Today.AddYears(-1), Idol("Mei")).Status = Groups._group._status.disbanded;
            staticVars.PlayerData.GroupName = "Stars";

            Moment moment = MomentContext.Tour(Tour(Today, true, (SEvent_Tour._country.china, 3, 7600)));
            Assert.Equal("Stars and Moons", moment.Fact(F, "Groups on tour"));
        }

        /// <summary>
        /// Idols the game didn't send stay home, each with the reason the game's status gives; graduates and
        /// idols at work aren't listed.
        /// </summary>
        [Fact]
        public void StayedHome_ListsIdolsWhoDidntGo_WithWhy()
        {
            Idol("Rin");
            Idol("Kaede").status = data_girls._status.hiatus;
            Idol("Noa").status = data_girls._status.announced_graduation;
            Idol("Mei").status = data_girls._status.injured;
            Idol("Asuka").Graduated(Today.AddYears(-1));

            Moment moment = MomentContext.Tour(Tour(Today, true, (SEvent_Tour._country.china, 3, 7600)));
            Assert.Equal("\n- Kaede: on hiatus\n- Noa: announced her graduation\n- Mei: injured", moment.Fact(F, "Stayed home"));
        }

        [Fact]
        public void StayedHome_IsLeftOut_WhenEveryoneWent()
        {
            Idol("Rin");
            Moment moment = MomentContext.Tour(Tour(Today, true, (SEvent_Tour._country.china, 3, 7600)));
            Assert.Null(moment.Fact(F, "Stayed home"));
        }

        /// <summary>
        /// A location mod renames the game's places and tours through the game's text, which the moment uses.
        /// </summary>
        [Fact]
        public void Tour_UsesTheGamesNames()
        {
            Language.Data["SEVENT__WORLD_TOUR"] = "National Tour";
            Language.Data["FRANCE"] = "Osaka";
            SEvent_Tour.tour tour = Tour(Today, true, (SEvent_Tour._country.france, 3, 7600));
            Moment moment = MomentContext.Tour(tour);
            Assert.Equal("the agency's idols came back from their National Tour", moment.Kind);
            Assert.Equal("\n- Osaka: 7600 out of 8000 seats, first visit", moment.Fact(F, "Stops, in order"));
        }

        /// <summary>
        /// "first visit" only on places no earlier finished tour went to. A tour that never finished went nowhere.
        /// </summary>
        [Fact]
        public void FirstVisit_OnlyWhereNoEarlierTourWent()
        {
            Tour(Today.AddMonths(-8), true, (SEvent_Tour._country.france, 2, 2000));
            Tour(Today.AddMonths(-1), false, (SEvent_Tour._country.spain, 2, 0));
            SEvent_Tour.tour tour = Tour(Today, true, (SEvent_Tour._country.france, 3, 7600), (SEvent_Tour._country.spain, 1, 400));

            Assert.Equal("\n- France: 7600 out of 8000 seats\n- Spain: 400 out of 500 seats, first visit",
                MomentContext.Tour(tour).Fact(F, "Stops, in order"));
        }

        /// <summary>
        /// Every active idol goes, from every group, so the lines are the 5 with the most fans.
        /// </summary>
        [Fact]
        public void Members_AreTheMostPopularActiveIdols_FromAnyGroup()
        {
            List<data_girls.girls> main = Enumerable.Range(1, 5).Select(i => Idol("Main" + i, fans: i * 100)).ToList();
            data_girls.girls star = Idol("Star", fans: 100000);
            Idol("Resting", fans: 900000).status = data_girls._status.hiatus;
            Group("Stars", Today.AddYears(-2), main.ToArray());
            Group("Moons", Today.AddYears(-1), star);

            Moment moment = MomentContext.Tour(Tour(Today, true, (SEvent_Tour._country.china, 3, 7600)));
            Assert.Equal(new[] { "Star", "Main5", "Main4", "Main3", "Main2" }, moment.Facts(Moment.MembersSection).Select(f => f.Key));
            Assert.Contains("Member of sister group Moons", moment.Fact(Moment.MembersSection, "Star"));
        }

        [Fact]
        public void FirstTour_HasNoPlacesVisited()
        {
            Moment moment = MomentContext.Tour(Tour(Today, true, (SEvent_Tour._country.china, 3, 7600)));
            Assert.Null(moment.Fact(H, "Places visited on earlier tours"));
            Assert.Equal("1", moment.Fact(H, "Tours so far, this one included"));
        }

        /// <summary>
        /// Every place earlier tours went to, once, at its latest visit, newest first; not this tour's stops.
        /// </summary>
        [Fact]
        public void History_ListsEachPlace_AtItsLatestVisit()
        {
            Tour(Today.AddMonths(-12), true, (SEvent_Tour._country.france, 2, 2000), (SEvent_Tour._country.china, 1, 450));
            Tour(Today.AddMonths(-4), true, (SEvent_Tour._country.france, 3, 7600));
            Tour(Today.AddMonths(-1), false, (SEvent_Tour._country.spain, 2, 0));
            SEvent_Tour.tour tour = Tour(Today, true, (SEvent_Tour._country.US, 4, 15000));

            Moment moment = MomentContext.Tour(tour);
            Assert.Equal("\n- France: 7600 out of 8000 seats, 4 months ago\n- China: 450 out of 500 seats, 12 months ago",
                moment.Fact(H, "Places visited on earlier tours"));
            Assert.Equal("3", moment.Fact(H, "Tours so far, this one included"));
        }
    }
}
