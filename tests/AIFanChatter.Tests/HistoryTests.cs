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
            Assert.Equal("\"Third\" (Metal, Regret lyrics, Cute choreography): 20000 sales, quality 60%, fan satisfaction 80%, released 40 days ago",
                moment.Fact(H, "Previous single"));
            Assert.Equal("\"Second\": 40000 sales, quality 0%, fan satisfaction 0%, released 3 months ago", moment.Fact(H, "2nd previous single"));
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
        public void Single_QualityAndSatisfactionArePercentages()
        {
            singles._single single = Single("Song", Today, 1);
            single.ReleaseData.Quality = 7;
            single.ReleaseData.Fan_Satisfaction = 82;
            Moment moment = MomentContext.Single(single);
            Assert.Equal("70%", moment.Fact(Moment.FactsSection, "Quality"));
            Assert.Equal("82%", moment.Fact(Moment.FactsSection, "Fan satisfaction"));
        }

        /// <summary>
        /// Only finished concerts count: the game sets an unfinished one's finish date to today on every load.
        /// </summary>
        [Fact]
        public void Concert_ComparesWithThePreviousFinishedConcert()
        {
            Language.Data["CONCERT__CONCERT_HALL"] = "Concert Hall";
            Concert("Spring Live", Today.AddDays(-90), 5000);
            SEvent_Concerts._concert summer = Concert("Summer Live", Today.AddDays(-30), 1350);
            summer.Venue = SEvent_Concerts._venue.concertHall;
            summer.ProjectedValues.Actual_Attendance = 0.9f;
            Concert("Planned", Today, 99999, done: false);
            SEvent_Concerts._concert current = Concert("Autumn Live", Today, 4000);

            Moment moment = new("x");
            History.Concert(moment, current, SEvent_Concerts.Concerts, Today);
            // One line, without the concert's name: the game's names are the venue's
            Assert.Equal("Concert Hall, attendance 1350 out of 1500 seats, held 30 days ago", moment.Fact(H, "Previous concert"));
            Assert.Equal(new[] { "Previous concert", "Concerts held before this one", "Biggest audience at an earlier concert" },
                moment.Facts(H).Select(f => f.Key));
            Assert.Equal("2", moment.Fact(H, "Concerts held before this one"));
            Assert.Equal("5000", moment.Fact(H, "Biggest audience at an earlier concert"));
        }

        [Fact]
        public void PreviousConcert_WithoutAVenueName_KeepsTheRest()
        {
            SEvent_Concerts._concert old = Concert("Old", Today.AddDays(-30), 500);
            old.Venue = SEvent_Concerts._venue.club;
            old.ProjectedValues.Actual_Attendance = 1f;
            Assert.Equal("attendance 500 out of 500 seats, held 30 days ago", History.ConcertSummary(old, Today));
        }

        /// <summary>
        /// A sister group's graduation compares only with its own graduates; the main group's don't count.
        /// </summary>
        [Fact]
        public void Graduation_ComparesOnlyWithHerOwnGroup()
        {
            data_girls.girls mainGraduate = Idol("Main graduate").Graduated(Today.AddDays(-20));
            data_girls.girls sisterGraduate = Idol("Sister graduate").Graduated(Today.AddDays(-200));
            data_girls.girls leaving = Idol("Leaving").Graduated(Today);
            Group("Stars", Today.AddYears(-3), Idol("Main"), mainGraduate);
            Group("Moons", Today.AddYears(-1), Idol("Sister"), sisterGraduate, leaving);

            Moment moment = new("x");
            History.Graduation(moment, leaving, data_girls.girl, Today);
            Assert.Equal("Sister graduate", moment.Fact(H, "Previous graduate"));
            Assert.Equal("6 months ago", moment.Fact(H, "Previous graduation"));
            Assert.Equal("1", moment.Fact(H, "Idols who graduated from the group before her"));
            Assert.Null(moment.Fact(H, "Idols who graduated before her"));
        }

        [Fact]
        public void Graduation_FirstInHerGroup_SaysSo()
        {
            data_girls.girls mainGraduate = Idol("Main graduate").Graduated(Today.AddDays(-20));
            data_girls.girls leaving = Idol("Leaving").Graduated(Today);
            Group("Stars", Today.AddYears(-3), Idol("Main"), mainGraduate);
            Group("Moons", Today.AddYears(-1), Idol("Sister"), leaving);

            Moment moment = new("x");
            History.Graduation(moment, leaving, data_girls.girl, Today);
            Assert.Equal(new[] { "Idols who graduated from the group before her" }, moment.Facts(H).Select(f => f.Key));
            Assert.Equal("none, she is the first", moment.Fact(H, "Idols who graduated from the group before her"));
        }

        /// <summary>
        /// Her released singles as center, best-selling first, with their style; none leaves the list out.
        /// </summary>
        [Fact]
        public void Graduation_ListsTheSinglesSheCentered()
        {
            data_girls.girls leaving = Idol("Leaving");
            data_girls.girls other = Idol("Other");
            Group("Stars", Today.AddYears(-3), leaving, other);
            Single("Second", Today.AddDays(-50), 900, leaving, other).Styled("Rock", "Friends", "Cool");
            Single("First", Today.AddDays(-300), 5000, leaving).Styled("Pop", "Love", "Cute");
            Single("Not hers", Today.AddDays(-100), 1, other, leaving);
            singles._single planned = Single("Planned", Today, 1, leaving);
            planned.status = singles._single._status.normal;
            leaving.Graduated(Today);

            string lines = MomentContext.Graduation(leaving, null).Lines(Moment.FactsSection);
            Assert.Contains("Singles she was the center of:\n"
                + "- \"First\" (Pop, Love lyrics, Cute choreography)\n"
                + "- \"Second\" (Rock, Friends lyrics, Cool choreography)", lines);
            Assert.DoesNotContain("Not hers", lines);
            Assert.DoesNotContain("Planned", lines);
            Assert.DoesNotContain("center of", MomentContext.Graduation(Idol("Nobody").Graduated(Today), null).Lines(Moment.FactsSection));
        }

        [Fact]
        public void Graduation_ListsOnlyHerFiveBestSellingCenterSingles()
        {
            data_girls.girls leaving = Idol("Leaving");
            Group("Stars", Today.AddYears(-3), leaving);
            for (int i = 1; i <= 7; i++)
            {
                Single("Song " + i, Today.AddDays(-10 * i), i * 1000, leaving);
            }
            leaving.Graduated(Today);

            string lines = MomentContext.Graduation(leaving, null).Lines(Moment.FactsSection);
            Assert.Contains("Singles she was the center of:\n- \"Song 7\"\n- \"Song 6\"\n- \"Song 5\"\n- \"Song 4\"\n- \"Song 3\"", lines);
            Assert.DoesNotContain("Song 2", lines);
            Assert.DoesNotContain("Song 1\"", lines);
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

        /// <summary>
        /// An event in the game's list whose answer starts the next one, as random_events.json writes it:
        /// "trigger_event politician_2 14". The answer also adds buzz, a public outcome, unless it's private.
        /// </summary>
        private static Event_Manager._randomEvent Starts(string id, string next, string effect = "trigger_event", bool isPublic = true)
        {
            Event_Manager._randomEvent data = Event_Manager.RandomEvents.FirstOrDefault(e => e.id == id);
            if (data == null)
            {
                data = new Event_Manager._randomEvent { id = id };
                Event_Manager.RandomEvents.Add(data);
            }
            Event_Manager._randomEvent._reply reply = new();
            reply.Effects.Add(new data_dialogues._action { target = "meta", parameter = effect, formula = next + " 14" });
            reply.Effects.Add(isPublic
                ? new data_dialogues._action { target = "resource", parameter = "buzz", formula = "20" }
                : new data_dialogues._action { target = "resource", parameter = "money", formula = "-100000" });
            data.reply.Add(reply);
            return data;
        }

        private static Event_Manager._activeEvent Chapter(string id, DateTime date, string text)
        {
            Event_Manager._activeEvent chapter = Event(id, date);
            chapter.data.description = text;
            return chapter;
        }

        private static List<KeyValuePair<string, string>> StoryLines(Moment moment) =>
            moment.Facts(H).Where(f => f.Key.StartsWith("Earlier in this storyline")).ToList();

        /// <summary>
        /// The event history with a player, as the game always has: an event's text names the player.
        /// </summary>
        private static void EventHistory(Moment moment, Event_Manager._activeEvent current, List<Event_Manager._activeEvent> log)
        {
            staticVars._playerData playerBefore = staticVars.PlayerData;
            List<staff._staff> staffBefore = staff.Staff.ToList();
            try
            {
                staticVars.PlayerData = new staticVars._playerData { FirstName = "Haruto", LastName = "Sato" };
                staff.Staff.Clear();
                staff.Staff.Add(new staff._staff { type = staff._type.player });
                History.Event(moment, current, log, Today);
            }
            finally
            {
                staticVars.PlayerData = playerBefore;
                staff.Staff.Clear();
                staff.Staff.AddRange(staffBefore);
            }
        }

        /// <summary>
        /// The storyline's earlier events that happened, oldest first, each with how long ago; events outside
        /// the storyline, and a later run of an earlier chapter, are left out.
        /// </summary>
        [Fact]
        public void Event_InAStoryline_GivesTheEarlierChapters()
        {
            Starts("politician", "politician_2");
            Starts("politician_2", "politician_3a", "trigger_event_same_actor");
            Starts("fake_tickets", "elsewhere");
            List<Event_Manager._activeEvent> log = new()
            {
                Chapter("politician_2", Today.AddDays(-40), "He doubles down."),
                Chapter("politician", Today.AddDays(-104), "A politician attacks idol groups."),
                Chapter("fake_tickets", Today.AddDays(-50), "Fake tickets."),
                Chapter("politician", Today.AddDays(5), "Not yet."),
            };
            Event_Manager._activeEvent current = Chapter("politician_3a", Today, "He resigns.");
            log.Add(current);

            Moment moment = new("x");
            EventHistory(moment, current, log);
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("Earlier in this storyline, 3 months ago", "A politician attacks idol groups."),
                new KeyValuePair<string, string>("Earlier in this storyline, 40 days ago", "He doubles down."),
            }, StoryLines(moment));
        }

        /// <summary>
        /// A chapter missing from the save is left out and the rest still show; an event that starts itself
        /// or a loop of events ends.
        /// </summary>
        [Fact]
        public void Event_Storyline_SkipsMissingChapters_AndEndsLoops()
        {
            Starts("onions_10", "onions_20");
            Starts("onions_20", "onions_30");
            Starts("onions_30", "onions_10");
            Starts("onions_30", "onions_30");
            Event_Manager._activeEvent current = Chapter("onions_30", Today, "Third.");
            List<Event_Manager._activeEvent> log = new() { Chapter("onions_10", Today.AddDays(-14), "First."), current };

            Moment moment = new("x");
            EventHistory(moment, current, log);
            Assert.Equal(new[] { new KeyValuePair<string, string>("Earlier in this storyline, 14 days ago", "First.") }, StoryLines(moment));
        }

        /// <summary>
        /// The game doesn't save the answer given, so an earlier event is left out when any answer of it that
        /// leads on could have been private; one whose leading answers were all public, or posted by the game
        /// itself, still shows.
        /// </summary>
        [Fact]
        public void Event_Storyline_LeavesOutChaptersThatMayHaveStayedPrivate()
        {
            Starts("blackmail", "fallout", isPublic: false);
            Starts("rumour", "fallout");
            Starts("rumour", "fallout", isPublic: false);
            Event_Manager._randomEvent posted = Starts("trolls", "fallout", isPublic: false);
            posted.reply[0].SNS.Add(new Event_Manager._randomEvent._reply._SNS());
            Starts("press", "fallout");
            Event_Manager._activeEvent current = Chapter("fallout", Today, "Now.");
            List<Event_Manager._activeEvent> log = new()
            {
                Chapter("blackmail", Today.AddDays(-4), "A private email."),
                Chapter("rumour", Today.AddDays(-3), "Maybe private."),
                Chapter("trolls", Today.AddDays(-2), "Trolls, posted by the game."),
                Chapter("press", Today.AddDays(-1), "In the papers."),
                current,
            };

            Moment moment = new("x");
            EventHistory(moment, current, log);
            Assert.Equal(new[] { "Trolls, posted by the game.", "In the papers." }, StoryLines(moment).Select(l => l.Value));
        }

        /// <summary>
        /// An event outside any storyline keeps only its count of earlier runs.
        /// </summary>
        [Fact]
        public void Event_OutsideAStoryline_HasNoStoryline()
        {
            Starts("politician", "politician_2");
            Event_Manager._activeEvent current = Chapter("scandal", Today, "Now.");
            List<Event_Manager._activeEvent> log = new() { Chapter("scandal", Today.AddDays(-20), "Before."), Chapter("politician", Today.AddDays(-5), "Other."), current };

            Moment moment = new("x");
            EventHistory(moment, current, log);
            Assert.Empty(StoryLines(moment));
            Assert.Equal("1", moment.Fact(H, "Times this event happened before"));
        }
    }
}
