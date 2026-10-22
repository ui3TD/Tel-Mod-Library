using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;
using Status = Shows._show._status;
using Cast = Shows._show._castType;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// A show's first episode, posted as the player launches it, with the shows cancelled in the past year.
    /// </summary>
    public class ShowStartTests
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        public ShowStartTests() => TestEnv.Reset();

        private static Shows._param Param(string title) => new() { title = title };

        /// <summary>A show as the game holds it, in its list: launched episodes ago, or still in production.</summary>
        private static Shows._show Show(string title, Status status, int episodes, DateTime launched, string medium = "TV", string genre = "Drama",
            Cast cast = Cast.permanentCast, params data_girls.girls[] girls)
        {
            Shows._show show = new()
            {
                title = title,
                status = status,
                episodeCount = episodes,
                LaunchDate = launched,
                medium = Param(medium),
                genre = Param(genre),
                mc = new Shows._param { id = 2, title = "Seasoned" },
                castType = cast,
            };
            for (int i = 0; i < girls.Length && i < show.girls.Length; i++)
            {
                show.girls[i] = girls[i];
            }
            Shows.shows.Add(show);
            return show;
        }

        private static Shows._show Launched(Cast cast = Cast.permanentCast, params data_girls.girls[] girls)
        {
            Shows._show show = Show("Idol Detectives", Status.released, 1, Today, cast: cast, girls: girls);
            show.audience.Add(480000);
            show.revenue.Add(12345678);
            show.fans.Add(9876);
            return show;
        }

        [Fact]
        public void LaunchedShow_PostsOnce()
        {
            Shows._show show = Launched(Cast.permanentCast, Idol("Rin"));
            Assert.True(Shows_ReleaseShow.ShouldPost(show, true));
            Shows_ReleaseShow.Postfix(show, true);
            Assert.False(Shows_ReleaseShow.ShouldPost(show, true));
        }

        /// <summary>
        /// A skipped launch, a show still in production, or a later episode: no posts.
        /// </summary>
        [Fact]
        public void OtherCalls_DoNotPost()
        {
            Shows._show show = Launched(Cast.permanentCast, Idol("Rin"));
            Assert.False(Shows_ReleaseShow.ShouldPost(show, false));
            Assert.False(Shows_ReleaseShow.ShouldPost(Show("Draft", Status.normal, 0, new DateTime(1900, 1, 1)), true));
            show.episodeCount = 2;
            Assert.False(Shows_ReleaseShow.ShouldPost(show, true));
            Assert.False(Shows_ReleaseShow.ShouldPost(null, true));
        }

        /// <summary>
        /// The show by the game's names, its cast and its first audience; its budget, revenue and new fans
        /// aren't public.
        /// </summary>
        [Fact]
        public void Show_GivesItsFacts()
        {
            Shows._show show = Launched(Cast.permanentCast, Idol("Rin"), Idol("Aoi"));
            Moment moment = MomentContext.ShowStart(show);
            Assert.Equal("a new show starring the agency's idols started airing", moment.Kind);
            Assert.Equal("the show \"Idol Detectives\"", moment.Label);
            Assert.Equal("\"Idol Detectives\"", moment.Fact(F, "Title"));
            Assert.Equal("TV", moment.Fact(F, "Medium"));
            Assert.Equal("Drama", moment.Fact(F, "Genre"));
            Assert.Equal("Seasoned host", moment.Fact(F, "Host"));
            show.mc = new Shows._param { id = 0, title = "No Host" };
            Assert.Equal("No Host", MomentContext.ShowStart(show).Fact(F, "Host"));
            Assert.Equal("Rin, Aoi", moment.Fact(F, "Cast"));
            Assert.Equal("480000 viewers", moment.Fact(F, "First episode audience"));
            string text = moment.Describe();
            Assert.DoesNotContain("12345678", text);
            Assert.DoesNotContain("9876", text);
            Assert.Equal(new[] { "Rin", "Aoi" }, moment.Facts(M).Select(f => f.Key));
        }

        /// <summary>
        /// The whole group as the cast, its most popular in the lines; a rotating cast names who was in the
        /// first episode.
        /// </summary>
        [Fact]
        public void CastTypes_AreDescribed()
        {
            List<data_girls.girls> idols = Enumerable.Range(1, 7).Select(i => Idol("I" + i, fans: i * 100)).ToList();
            Moment whole = MomentContext.ShowStart(Launched(Cast.entireGroup));
            Assert.Equal("the whole group", whole.Fact(F, "Cast"));
            Assert.Equal(new[] { "I7", "I6", "I5", "I4", "I3" }, whole.Facts(M).Select(f => f.Key));

            Moment rotating = MomentContext.ShowStart(Launched(Cast.rotatingCast, idols[0], idols[1]));
            Assert.Equal("a rotating cast, in the first episode I1, I2", rotating.Fact(F, "Cast"));
        }

        /// <summary>
        /// Shows cancelled in the past year by their last episode (weekly from the launch), newest first, at
        /// most 3; other shows on air; how many started before.
        /// </summary>
        [Fact]
        public void History_ListsRecentlyCancelledShows()
        {
            // Last aired 2 months ago: launched 14 weeks before that
            Show("Idol Kitchen", Status.canceled, 14, Today.AddMonths(-2).AddDays(-7 * 13), "Internet", "Food", girls: new[] { Idol("Mio"), Idol("Sora") });
            Show("Old News", Status.canceled, 4, Today.AddMonths(-20), "Radio", "News");
            Show("Quiz Night", Status.canceled, 12, Today.AddMonths(-9), "TV", "Game Show");
            Show("Road Trip", Status.canceled, 2, Today.AddMonths(-5), "Internet", "Travel", Cast.rotatingCast);
            Show("Morning Talk", Status.canceled, 1, Today.AddMonths(-4), "Radio", "Interviews");
            Show("Live Now", Status.released, 20, Today.AddMonths(-6), "TV", "Music", Cast.entireGroup);
            Show("In Production", Status.working, 0, new DateTime(1900, 1, 1));
            Shows._show show = Launched(Cast.permanentCast, Idol("Rin"));

            Moment moment = MomentContext.ShowStart(show);
            Assert.Equal("\n- \"Idol Kitchen\" (Internet, Food), with Mio and Sora, 14 episodes, last aired 2 months ago"
                + "\n- \"Morning Talk\" (Radio, Interviews), 1 episode, last aired 4 months ago"
                + "\n- \"Road Trip\" (Internet, Travel), with a rotating cast, 2 episodes, last aired 4 months ago",
                moment.Fact(H, "Recently cancelled shows"));
            Assert.Equal("\n- \"Live Now\" (TV, Music), with the whole group", moment.Fact(H, "Other shows on air now"));
            Assert.Equal("6", moment.Fact(H, "Shows started before this one"));
        }

        /// <summary>
        /// A listed show's fixed cast names up to 3, then how many more.
        /// </summary>
        [Fact]
        public void History_NamesUpToThreeOfACast()
        {
            Shows._show big = Show("Big Cast", Status.released, 5, Today.AddMonths(-1));
            big.girls = new[] { Idol("A1"), Idol("B1"), Idol("C1"), Idol("D1") };
            Moment moment = MomentContext.ShowStart(Launched(Cast.permanentCast, Idol("Rin")));
            Assert.Equal("\n- \"Big Cast\" (TV, Drama), with A1, B1, C1 and 1 more", moment.Fact(H, "Other shows on air now"));
        }

        [Fact]
        public void History_LeavesOutAnEmptyList()
        {
            Moment moment = MomentContext.ShowStart(Launched(Cast.permanentCast, Idol("Rin")));
            Assert.Null(moment.Fact(H, "Recently cancelled shows"));
            Assert.Null(moment.Fact(H, "Other shows on air now"));
            Assert.Equal("0", moment.Fact(H, "Shows started before this one"));
        }
    }
}
