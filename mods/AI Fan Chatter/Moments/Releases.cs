using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <summary>
        /// One item of a concert's set list: a song with its single's style and the idol at its center (a
        /// song has one), or a talk break with its idols. The game's talk breaks have no title of their own,
        /// only a number it shows on screen and doesn't save.
        /// </summary>
        public static string SetListItem(SEvent_Concerts._concert.ISetlistItem item)
        {
            if (item is SEvent_Concerts._concert._song song)
            {
                if (song.Single == null)
                {
                    return "";
                }
                string text = TitleWithStyle(song.Single);
                return song.Center != null ? text + ", center " + Name(song.Center) : text;
            }
            if (item is SEvent_Concerts._concert._mc mc)
            {
                List<string> names = mc.GetGirls(true).Select(Name).ToList();
                return names.Count == 0 ? "Talk break" : "Talk break with " + JoinAnd(names);
            }
            return "";
        }

        public static Moment Single(singles._single single)
        {
            Moment moment = new Moment("the group released a new single")
                .Labelled(() => "the single " + single.title)
                .Add("Title", () => single.title)
                .Add("Center", () => single.GetCenter() is data_girls.girls center ? Name(center) : "")
                .Add("Members", () => Names(single.girls, 7))
                .Add("Genre", () => single.genre?.GetTitle())
                .Add("Lyrics", () => single.lyrics?.GetTitle())
                .Add("Choreography", () => single.choreography?.GetTitle())
                .Add("Sales", () => single.ReleaseData.Sales)
                // The game rounds quality to 0 to 10 and shows it as stars out of 10 (Single_Released_Button)
                .Add("Quality", () => single.ReleaseData.Quality * 10 + "%")
                .Add("Fan satisfaction", () => single.ReleaseData.Fan_Satisfaction + "%")
                .Add("Marketing", () => Marketing(single))
                .Add("Election", () => SingleElection(single));
            // An election single's lineup is its election's top places; a graduate since leaves her slot empty
            SEvent_SSK._SSK parent = ParentElection(single);
            moment.AddList(Moment.FactsSection, "Ranked but not in it", parent == null ? new List<Func<object>>()
                : History.Ranked(parent).Take(ElectionPlaces).Where(r => r.Girl.status == data_girls._status.graduated)
                    .Select(r => (Func<object>)(() => Name(r.Girl) + ", " + Members.Ordinal(r.Place) + ", graduated since")).ToList());
            Groups._group group = History.GroupOf(single);
            Members.Add(moment, Members.ForSingle(single), Now, group);
            History.Single(moment, single, singles.Singles, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group handshake event security", () => AgencyRules.HandshakeSecurity(single)));
        }

        public static Moment Concert(SEvent_Concerts._concert concert)
        {
            Moment moment = new Moment("the group held a concert")
                .Labelled(() => "the concert at " + SEvent_Concerts.GetVenueTitle(concert.Venue));
            ConcertFacts(moment, concert);
            // The game's concerts are the main group's
            Groups._group group = MainGroup();
            Members.Add(moment, Members.ForConcert(concert), Now, group);
            History.Concert(moment, concert, SEvent_Concerts.Concerts, Now);
            return Agency(moment, group, rules: ConcertRules);
        }

        private static Moment ConcertRules(Moment moment) => moment
            .AddTo(Moment.AgencySection, "Stage costumes", AgencyRules.Costumes)
            .AddTo(Moment.AgencySection, "Performance style", AgencyRules.PerformanceStyle);

        /// <summary>
        /// A concert's own facts: venue, attendance, hype, ticket price and set list.
        /// </summary>
        private static void ConcertFacts(Moment moment, SEvent_Concerts._concert concert)
        {
            moment
                .Add("Venue", () => SEvent_Concerts.GetVenueTitle(concert.Venue))
                .Add("Attendance", () => Attendance(concert))
                // The game's hype runs from 0 to 200 (Concert_Popup.AddHype) and shows as a percentage
                .Add("Hype", () => concert.ProjectedValues.Actual_Hype + "%")
                .Add("Ticket price", () => concert.ProjectedValues.TicketPrice + " yen");
            // The set list names who performed. Unnumbered, so the songs are named by title rather than by their place in the list
            moment.AddList(Moment.FactsSection, "Set list, in order",
                (concert.SetListItems ?? new List<SEvent_Concerts._concert.ISetlistItem>())
                .Select(item => (Func<object>)(() => SetListItem(item))).ToList());
        }

        /// <summary>
        /// A concert and the election whose results were announced right after it, as one moment: the game
        /// starts the election as the concert's final screen closes.
        /// </summary>
        public static Moment ConcertElection(SEvent_Concerts._concert concert, SEvent_SSK._SSK election)
        {
            string venue = Part(() => SEvent_Concerts.GetVenueTitle(concert.Venue));
            string title = Part(() => election.GetTitle());
            Moment moment = new Moment("the group held a concert, and the results of " + title + " were announced after it")
                .Labelled(() => "the concert at " + venue + " and " + title);
            ConcertFacts(moment, concert);
            List<data_girls.girls> idols = ElectionFacts(moment, election, withConcert: false, out SEvent_SSK._SSK previous);
            Groups._group group = MainGroup();
            Members.Add(moment, idols, Now, group, withElection: false);
            History.Concert(moment, concert, SEvent_Concerts.Concerts, Now);
            History.Election(moment, election, previous, SEvent_SSK.Elections, Now);
            return Agency(moment, group, rules: ConcertRules);
        }

        /// <summary>
        /// A tour, once it's over: the game plays every stop in one popup and finishes the tour when the
        /// player closes it. It has no group or cast of its own: every active idol goes, from every group.
        /// </summary>
        public static Moment Tour(SEvent_Tour.tour tour)
        {
            // The game's name for its tours (a location mod can rename it), as the special events tab shows it
            string title = Moment.Clean(Translate("SEVENT__WORLD_TOUR"));
            HashSet<SEvent_Tour._country> visited = new(History.EarlierTours(tour, SEvent_Tour.Tours)
                .SelectMany(History.Stops).Select(stop => stop.Country.Type));
            Moment moment = new Moment("the agency's idols came back from their " + title)
                .Labelled(() => "the " + title)
                .Add("Groups on tour", () => JoinAnd(ToursGroups()));
            moment.AddList(Moment.FactsSection, "Stops, in order",
                History.Stops(tour).Select(stop => (Func<object>)(() =>
                {
                    string text = TourStop(stop);
                    return text == "" || visited.Contains(stop.Country.Type) ? text : text + ", first visit";
                })).ToList());
            // The game sends every active idol; the others stay home, which fans notice
            moment.AddList(Moment.FactsSection, "Stayed home",
                data_girls.girl.Where(g => g != null && g.status != data_girls._status.graduated && !g.IsActive())
                    .Select(g => (Func<object>)(() => Name(g) + (Absence(g) is string why && why != "" ? ": " + why : ""))).ToList());
            // Tours aren't a group's, so the main group, as for concerts
            Groups._group group = MainGroup();
            Members.Add(moment, Members.ForTour(), Now, group);
            History.Tour(moment, tour, SEvent_Tour.Tours, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Stage costumes", AgencyRules.Costumes)
                .AddTo(Moment.AgencySection, "Performance style", AgencyRules.PerformanceStyle));
        }

        /// <summary>
        /// The groups with an idol on the tour, main group first, by the names the game shows; the agency's
        /// group name when the game has no groups.
        /// </summary>
        private static List<string> ToursGroups()
        {
            List<string> names = Groups.Groups_
                .Where(g => g != null && g.Status != Groups._group._status.disbanded && g.GetGirls(true).Any(girl => girl != null && girl.IsActive()))
                .OrderBy(g => g.IsMain() ? 0 : 1)
                .Select(Members.GroupName).Where(n => n != "").ToList();
            return names.Count > 0 ? names : new List<string> { Moment.Clean(staticVars.PlayerData.GetGroupName()) };
        }

        /// <summary>
        /// Why an idol who isn't graduated stays out of the agency's work, as the game's status says: "on
        /// hiatus", "injured", "announced her graduation"; "" for a status the game doesn't name here.
        /// </summary>
        private static string Absence(data_girls.girls girl) => girl.status switch
        {
            data_girls._status.hiatus => "on hiatus",
            data_girls._status.injured => "injured",
            data_girls._status.announced_graduation => "announced her graduation",
            _ => "",
        };

        /// <summary>
        /// A single's promotion as the game titles it: "Group Handshake Event, Ad Campaign (success)". Only
        /// the risky kind (campaigns, a fake scandal, a daring PV) is rolled, so only it has an outcome.
        /// </summary>
        public static string Marketing(singles._single single)
        {
            List<string> parts = new();
            foreach (singles._param item in single.marketing ?? new List<singles._param>())
            {
                string title = item == null ? "" : Moment.Clean(Part(() => item.GetTitle()));
                if (title == "")
                {
                    continue;
                }
                string outcome = Part(() => item.IsRiskyMarketing() ? Outcome(single.Marketing_Result_Status) : "");
                parts.Add(outcome == "" ? title : title + " (" + outcome + ")");
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string Outcome(Single_Marketing_Roll._result result) => result switch
        {
            Single_Marketing_Roll._result.success => "success",
            Single_Marketing_Roll._result.success_crit => "great success",
            Single_Marketing_Roll._result.fail => "failure",
            Single_Marketing_Roll._result.fail_crit => "major failure",
            _ => "",
        };

        /// <summary>
        /// The people who came out of the venue's seats, without a percentage: "4600 out of 5000 seats".
        /// "4600 people" when the game can't give the capacity.
        /// </summary>
        public static string Attendance(SEvent_Concerts._concert concert)
        {
            return Seats(concert.ProjectedValues.Actual_Audience, Part(() => concert.GetCapacity().ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// "4600 out of 5000 seats"; "4600 people" without a capacity.
        /// </summary>
        public static string Seats(long audience, string capacity)
        {
            return audience + (capacity == "" || capacity == "0" ? " people" : " out of " + capacity + " seats");
        }

        /// <summary>
        /// A tour stop's place as the game names it (a location mod's names come through) and its audience:
        /// "Osaka: 7600 out of 8000 seats". "" when the game can't name the place.
        /// </summary>
        public static string TourStop(SEvent_Tour.tour.selectedCountry stop)
        {
            string place = Moment.Clean(Part(() => stop.Country.GetTitle()));
            return place == "" ? "" : place + ": " + Seats(stop.Audience, Part(() => SEvent_Tour.GetCapacity(stop.Level).ToString(CultureInfo.InvariantCulture)));
        }
    }
}
