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
    /// A show cancelled after a year or more on air, posted when the game really ends it.
    /// </summary>
    public class ShowEndTests
    {
        private const string F = Moment.FactsSection;
        private const string M = Moment.MembersSection;
        private const string H = Moment.HistorySection;

        public ShowEndTests() => TestEnv.Reset();

        private static Shows._param Param(string title, Shows._param._media_type type = Shows._param._media_type.internet)
            => new() { title = title, media_type = type };

        /// <summary>A show as the game holds it, launched on a date and aired weekly since.</summary>
        private static Shows._show Show(string title, Status status, int episodes, DateTime launched, Shows._param._media_type medium = Shows._param._media_type.internet,
            Cast cast = Cast.permanentCast, params data_girls.girls[] girls)
        {
            Shows._show show = new()
            {
                title = title,
                status = status,
                episodeCount = episodes,
                LaunchDate = launched,
                medium = Param(medium == Shows._param._media_type.internet ? "Internet" : "TV", medium),
                genre = Param("Music"),
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

        /// <summary>An internet show on air for 60 weeks, about 14 months.</summary>
        private static Shows._show LongRunning(Cast cast = Cast.permanentCast, params data_girls.girls[] girls)
        {
            Shows._show show = Show("Backstage Diaries", Status.released, 61, Today.AddDays(-7 * 60), cast: cast, girls: girls);
            show.audience.AddRange(new long[] { 200000, 350000, 260000 });
            show.peakAudience = 350000;
            show.revenue.Add(12345678);
            show.fans.Add(9876);
            return show;
        }

        /// <summary>
        /// The patch around the call, with the status the game's Cancel leaves. The game's Cancel itself can't
        /// run here: it refreshes the show list through Camera.main, which needs the engine.
        /// </summary>
        private static bool Ends(Shows._show show, Status after, bool ran = true)
        {
            Shows__show_Cancel.Prefix(show, out bool wasOn);
            show.status = after;
            return Shows__show_Cancel.ShouldPost(show, wasOn, ran, Today);
        }

        [Fact]
        public void ShowOnAirAYearOrMore_Posts()
        {
            Assert.True(Ends(LongRunning(Cast.permanentCast, Idol("Rin")), Status.canceled));
            Shows._show year = Show("A Year", Status.released, 53, Today.AddYears(-1), girls: Idol("Aoi"));
            Assert.True(Ends(year, Status.canceled));
        }

        /// <summary>
        /// A concept update keeps the first episode's date, so its time counts: the year runs from the first
        /// episode, and a show can end while an update is still in progress.
        /// </summary>
        [Fact]
        public void ConceptUpdates_CountAsTimeOnAir()
        {
            Shows._show updated = Show("Updated", Status.released, 57, Today.AddMonths(-13), girls: Idol("Rin"));
            updated.NumberOfRelaunches = 1;
            Assert.True(Ends(updated, Status.canceled));
            Shows._show updating = Show("Updating", Status.relaunching, 57, Today.AddMonths(-13), girls: Idol("Aoi"));
            Assert.True(Ends(updating, Status.canceled));
        }

        /// <summary>
        /// Under a year, a call that only marks a TV show to end at its 12th episode, a show already ended, or
        /// a call another mod skipped: no posts.
        /// </summary>
        [Fact]
        public void OtherCalls_DoNotPost()
        {
            Assert.False(Ends(Show("Short", Status.released, 48, Today.AddMonths(-11), girls: Idol("Rin")), Status.canceled));
            Assert.False(Ends(Show("Marked", Status.released, 58, Today.AddMonths(-14), Shows._param._media_type.tv, girls: Idol("Rin")), Status.released));
            Assert.False(Ends(Show("Ended", Status.canceled, 60, Today.AddMonths(-14), girls: Idol("Rin")), Status.canceled));
            Assert.False(Ends(LongRunning(Cast.permanentCast, Idol("Rin")), Status.canceled, ran: false));
            Assert.False(Ends(Show("Unknown", Status.released, 60, new DateTime(1900, 1, 1), girls: Idol("Rin")), Status.canceled));
            Assert.False(Shows__show_Cancel.ShouldPost(null, true, true, Today));
        }

        /// <summary>A marked TV show ends at its 12th episode; that later call posts.</summary>
        [Fact]
        public void MarkedShow_PostsWhenItEnds()
        {
            Shows._show show = Show("Marked", Status.released, 59, Today.AddDays(-7 * 59), Shows._param._media_type.tv, girls: Idol("Rin"));
            Assert.False(Ends(show, Status.released));
            show.episodeCount = 60;
            Assert.True(Ends(show, Status.canceled));
        }

        /// <summary>The game ends a show whose whole cast left; no one is left to post about.</summary>
        [Fact]
        public void EmptyCast_DoesNotPost()
        {
            Shows._show show = LongRunning(Cast.permanentCast);
            show.castType = Cast.rotatingCast;
            Assert.False(Ends(show, Status.canceled));
        }

        /// <summary>
        /// The show by the game's names, its cast and how long it ran. Its viewers, budget, revenue and new
        /// fans aren't public, and concept updates say little, so none of them is sent.
        /// </summary>
        [Fact]
        public void Show_GivesItsFacts()
        {
            Shows._show show = LongRunning(Cast.permanentCast, Idol("Rin"), Idol("Aoi"));
            show.status = Status.canceled;
            Moment moment = MomentContext.ShowEnd(show);
            Assert.Equal("a long-running show starring the agency's idols was cancelled", moment.Kind);
            Assert.Equal("the end of the show \"Backstage Diaries\"", moment.Label);
            Assert.Equal("\"Backstage Diaries\"", moment.Fact(F, "Title"));
            Assert.Equal("Internet", moment.Fact(F, "Medium"));
            Assert.Equal("Music", moment.Fact(F, "Genre"));
            Assert.Equal("Seasoned host", moment.Fact(F, "Host"));
            Assert.Equal("Rin, Aoi", moment.Fact(F, "Cast"));
            Assert.Equal("14 months, 61 episodes", moment.Fact(F, "Ran for"));
            show.NumberOfRelaunches = 2;
            string text = MomentContext.ShowEnd(show).Describe();
            Assert.DoesNotContain("audience", text);
            Assert.DoesNotContain("Concept", text);
            Assert.DoesNotContain("350000", text);
            Assert.DoesNotContain("260000", text);
            Assert.DoesNotContain("viewers", text);
            Assert.DoesNotContain("12345678", text);
            Assert.DoesNotContain("9876", text);
            Assert.Equal(new[] { "Rin", "Aoi" }, moment.Facts(M).Select(f => f.Key));
        }

        [Fact]
        public void RotatingCast_NamesTheLastEpisodes()
        {
            Moment moment = MomentContext.ShowEnd(LongRunning(Cast.rotatingCast, Idol("Rin"), Idol("Aoi")));
            Assert.Equal("a rotating cast, in the last episode Rin, Aoi", moment.Fact(F, "Cast"));
        }

        /// <summary>
        /// The agency's other shows as at a start, without the show itself and without the count of shows
        /// started before, which only reads right at a start.
        /// </summary>
        [Fact]
        public void History_ListsOtherShows()
        {
            Show("Idol Kitchen", Status.canceled, 14, Today.AddMonths(-2).AddDays(-7 * 13), girls: Idol("Mio"));
            Show("Live Now", Status.released, 20, Today.AddMonths(-6), cast: Cast.entireGroup);
            Shows._show show = LongRunning(Cast.permanentCast, Idol("Rin"));
            show.status = Status.canceled;

            Moment moment = MomentContext.ShowEnd(show);
            Assert.Equal("\n- \"Idol Kitchen\" (Internet, Music), with Mio, 14 episodes, last aired 2 months ago", moment.Fact(H, "Recently cancelled shows"));
            Assert.Equal("\n- \"Live Now\" (Internet, Music), with the whole group, started 6 months ago", moment.Fact(H, "Other shows on air now"));
            Assert.Null(moment.Fact(H, "Shows started before this one"));
        }
    }
}
