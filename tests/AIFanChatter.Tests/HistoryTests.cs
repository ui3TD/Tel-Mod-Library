using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AIFanChatter.Tests.TestGame;

namespace AIFanChatter.Tests
{
    public class HistoryTests
    {
        public HistoryTests() => TestEnv.Reset();

        private const string H = Moment.HistorySection;

        [Theory]
        [InlineData(0, "today")]
        [InlineData(1, "1 day ago")]
        [InlineData(59, "59 days ago")]
        [InlineData(60, "2 months ago")]
        [InlineData(729, "24 months ago")]
        [InlineData(730, "2 years ago")]
        [InlineData(1100, "3 years ago")]
        public void Ago_UsesTheLargestUnitThatReadsWell(int days, string expected)
        {
            Assert.Equal(expected, History.Ago(Today.AddDays(-days), Today));
        }

        [Fact]
        public void Ago_IsBlankForUnknownAndLaterDates()
        {
            Assert.Equal("", History.Ago(new DateTime(1900, 1, 1), Today));
            Assert.Equal("", History.Ago(default, Today));
            Assert.Equal("", History.Ago(Today.AddDays(3), Today));
        }

        [Fact]
        public void Single_ListsTheLastThreeReleased_LatestFirst()
        {
            Single("Oldest", Today.AddDays(-400), 10000);
            Single("First", Today.AddDays(-200), 50000).Styled("Pop", "Love", "Cool");
            Single("Second", Today.AddDays(-90), 40000);
            singles._single third = Single("Third", Today.AddDays(-40), 20000).Styled("Metal", "Regret", "Cute");
            third.ReleaseData.Quality = 6;
            third.ReleaseData.Fan_Satisfaction = 80;
            singles._single unreleased = Single("Unfinished", Today.AddDays(-1), 999999);
            unreleased.status = singles._single._status.working;
            singles._single current = Single("Now", Today, 30000);

            Moment moment = new("x");
            History.Single(moment, current, singles.Singles, Today);
            Assert.Equal("\"Third\" (Metal, Regret lyrics, Cute choreography): 20000 sales, quality 6 of 10, fan satisfaction 80 of 100, released 40 days ago",
                moment.Fact(H, "Previous single"));
            Assert.Equal("\"Second\": 40000 sales, quality 0 of 10, fan satisfaction 0 of 100, released 3 months ago", moment.Fact(H, "2nd previous single"));
            Assert.StartsWith("\"First\" (Pop, Love lyrics, Cool choreography): 50000 sales", moment.Fact(H, "3rd previous single"));
            Assert.DoesNotContain(moment.Facts(H), f => f.Value.Contains("Oldest"));
            Assert.Equal("4", moment.Fact(H, "Singles released before this one"));
            Assert.Equal("\"First\" (Pop, Love lyrics, Cool choreography), 50000 sales", moment.Fact(H, "Best-selling earlier single"));
        }

        [Fact]
        public void Single_FewerThanThreeEarlier_ListsThoseThereAre()
        {
            Single("Debut", Today.AddDays(-30), 1);
            singles._single current = Single("Now", Today, 1);
            Moment moment = new("x");
            History.Single(moment, current, singles.Singles, Today);
            Assert.StartsWith("\"Debut\": ", moment.Fact(H, "Previous single"));
            Assert.Null(moment.Fact(H, "2nd previous single"));
            Assert.Null(moment.Fact(H, "3rd previous single"));
        }

        /// <summary>
        /// A style part the single doesn't have is left out; titles keep their commas inside the quotes.
        /// </summary>
        [Theory]
        [InlineData("Rock", null, "Cool", "\"DJ, Go\" (Rock, Cool choreography)")]
        [InlineData(null, "Love", null, "\"DJ, Go\" (Love lyrics)")]
        [InlineData(null, null, null, "\"DJ, Go\"")]
        public void TitleWithStyle_LeavesOutWhatTheSingleLacks(string genre, string lyrics, string choreography, string expected)
        {
            Assert.Equal(expected, MomentContext.TitleWithStyle(Single("DJ, Go", Today, 1).Styled(genre, lyrics, choreography)));
        }

        /// <summary>
        /// Several singles can be in production at once, so the previous one is the latest released, not the
        /// latest in the list.
        /// </summary>
        [Fact]
        public void Single_PreviousIsTheLatestReleased_NotTheLatestInTheList()
        {
            Single("Released later", Today.AddDays(-10), 1);
            Single("Released earlier", Today.AddDays(-40), 1);
            singles._single current = Single("Now", Today, 1);
            Moment moment = new("x");
            History.Single(moment, current, singles.Singles, Today);
            Assert.StartsWith("\"Released later\"", moment.Fact(H, "Previous single"));
            Assert.StartsWith("\"Released earlier\"", moment.Fact(H, "2nd previous single"));
        }

        [Fact]
        public void Single_FirstSingle_HasNoHistory()
        {
            singles._single current = Single("Debut", Today, 1);
            Moment moment = new("x");
            History.Single(moment, current, singles.Singles, Today);
            Assert.Empty(moment.Facts(H));
        }

        /// <summary>
        /// Through the game's own group lookup: a sister group's single is compared with that group's singles.
        /// Without groups every single counts.
        /// </summary>
        [Fact]
        public void Single_StaysWithinItsGroup()
        {
            Groups._group main = Group("Stars", Today.AddYears(-3));
            Groups._group sister = Group("Moons", Today.AddYears(-1));
            Single("Main single", Today.AddDays(-5), 1).In(main);
            Single("Sister single", Today.AddDays(-30), 1).In(sister);
            singles._single current = Single("Sister now", Today, 1).In(sister);

            Moment moment = new("x");
            History.Single(moment, current, singles.Singles, Today);
            Assert.StartsWith("\"Sister single\"", moment.Fact(H, "Previous single"));
            Assert.Equal("1", moment.Fact(H, "Singles released before this one"));

            Groups.Groups_.Clear();
            Moment ungrouped = new("x");
            History.Single(ungrouped, current, singles.Singles, Today);
            Assert.StartsWith("\"Main single\"", ungrouped.Fact(H, "Previous single"));
        }

        [Fact]
        public void Single_QualityIsOutOf10()
        {
            singles._single single = Single("Song", Today, 1);
            single.ReleaseData.Quality = 7;
            Moment moment = MomentContext.Single(single);
            Assert.Equal("7", moment.Fact(Moment.FactsSection, "Quality (0 to 10)"));
        }

        /// <summary>
        /// Only finished concerts count: the game sets an unfinished one's finish date to today on every load.
        /// </summary>
        [Fact]
        public void Concert_ComparesWithThePreviousFinishedConcert()
        {
            Concert("Spring Live", Today.AddDays(-90), 5000);
            Concert("Summer Live", Today.AddDays(-30), 3000);
            Concert("Planned", Today, 99999, done: false);
            SEvent_Concerts._concert current = Concert("Autumn Live", Today, 4000);

            Moment moment = new("x");
            History.Concert(moment, current, SEvent_Concerts.Concerts, Today);
            Assert.Equal("Summer Live", moment.Fact(H, "Previous concert"));
            Assert.Equal("3000", moment.Fact(H, "Previous concert audience"));
            Assert.Equal("30 days ago", moment.Fact(H, "Previous concert held"));
            Assert.Equal("2", moment.Fact(H, "Concerts held before this one"));
            Assert.Equal("5000", moment.Fact(H, "Biggest audience at an earlier concert"));
        }

        [Fact]
        public void Graduation_UnknownDatesCount_ButAreNeverThePrevious()
        {
            Idol("Old").Graduated(new DateTime(1900, 1, 1));
            Idol("Recent").Graduated(Today.AddDays(-100));
            Idol("Earlier").Graduated(Today.AddDays(-400));
            Idol("Still here");
            data_girls.girls leaving = Idol("Leaving").Graduated(Today);

            Moment moment = new("x");
            History.Graduation(moment, leaving, data_girls.girl, Today);
            Assert.Equal("Recent", moment.Fact(H, "Previous graduate"));
            Assert.Equal("3 months ago", moment.Fact(H, "Previous graduation"));
            Assert.Equal("3", moment.Fact(H, "Idols who graduated before her"));
        }

        [Fact]
        public void Graduation_OnlyOldSaveDates_GiveJustTheCount()
        {
            Idol("Old").Graduated(new DateTime(1900, 1, 1));
            data_girls.girls leaving = Idol("Leaving").Graduated(Today);
            Moment moment = new("x");
            History.Graduation(moment, leaving, data_girls.girl, Today);
            Assert.Equal(new[] { "Idols who graduated before her" }, moment.Facts(H).Select(f => f.Key));
        }

        [Fact]
        public void Event_CountsTheSameEventOnly()
        {
            List<Event_Manager._activeEvent> log = new()
            {
                Event("scandal", Today.AddDays(-200)),
                Event("scandal", Today.AddDays(-20)),
                Event("birthday", Today.AddDays(-2))
            };
            Event_Manager._activeEvent current = Event("scandal", Today);
            log.Add(current);

            Moment moment = new("x");
            History.Event(moment, current, log, Today);
            Assert.Equal("2", moment.Fact(H, "Times this event happened before"));
            Assert.Equal("20 days ago", moment.Fact(H, "Last time it happened"));
        }

        [Fact]
        public void Event_FirstTime_HasNoHistory()
        {
            Event_Manager._activeEvent current = Event("scandal", Today);
            Moment moment = new("x");
            History.Event(moment, current, new List<Event_Manager._activeEvent> { current }, Today);
            Assert.Empty(moment.Facts(H));
        }
    }
}
