using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    /// <summary>
    /// What happened before a moment, read from what the game already saves, so it works on old saves and
    /// the mod saves nothing new. Each builder adds its facts to the moment's history section.
    /// </summary>
    public static class History
    {
        private const string Section = Moment.HistorySection;

        /// <summary>
        /// False for the game's placeholder dates: 1900-01-01 for an unknown date, and the default DateTime.
        /// </summary>
        public static bool Known(DateTime date) => date.Year > 1900;

        /// <summary>
        /// A length of time in the largest unit that reads well: days under 60, months under 2 years, then years.
        /// </summary>
        public static string Span(int days)
        {
            int count = Count(days, out string unit);
            return count + " " + unit + (count == 1 ? "" : "s");
        }

        /// <summary>
        /// The number of whole units in a span of days, and the unit's name.
        /// </summary>
        public static int Count(int days, out string unit)
        {
            if (days < 60)
            {
                unit = "day";
                return Math.Max(0, days);
            }
            if (days < 730)
            {
                unit = "month";
                return days / 30;
            }
            unit = "year";
            return days / 365;
        }

        /// <summary>
        /// "today", "1 day ago", "73 days ago", "4 months ago", "3 years ago"; "" for an unknown or later date.
        /// </summary>
        public static string Ago(DateTime then, DateTime now)
        {
            if (!Known(then) || then.Date > now.Date)
            {
                return "";
            }
            int days = (now.Date - then.Date).Days;
            return days == 0 ? "today" : Span(days) + " ago";
        }

        /// <summary>
        /// The item with the latest date; of those on the same date, the one later in the list.
        /// </summary>
        internal static T Latest<T>(IList<T> items, Func<T, DateTime> date) where T : class
        {
            T latest = null;
            DateTime best = DateTime.MinValue;
            foreach (T item in items)
            {
                DateTime when = date(item);
                if (latest == null || when >= best)
                {
                    latest = item;
                    best = when;
                }
            }
            return latest;
        }

        /// <summary>
        /// The group a single belongs to in the game (the main group for one in no group's list); null
        /// when the game has no groups or can't say.
        /// </summary>
        internal static Groups._group GroupOf(singles._single single)
        {
            try
            {
                return single.GetGroup();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public const int PreviousSingles = 3;

        private static readonly string[] PreviousSingleKeys = { "Previous single", "2nd previous single", "3rd previous single" };

        /// <summary>
        /// The same group's last released singles, latest first, how each did, and the group's earlier singles.
        /// </summary>
        public static void Single(Moment moment, singles._single single, IEnumerable<singles._single> all, DateTime now)
        {
            try
            {
                Groups._group group = GroupOf(single);
                List<singles._single> earlier = (all ?? Enumerable.Empty<singles._single>())
                    .Where(s => s != null && s != single && s.status == singles._single._status.released && s.ReleaseData != null)
                    .Where(s => group == null || GroupOf(s) == group)
                    .ToList();
                if (earlier.Count == 0)
                {
                    return;
                }
                // Several singles can be in production at once, so release order isn't list order; of those
                // released on the same day, the one later in the list counts as later, as in Latest
                List<singles._single> latestFirst = earlier
                    .Select((s, index) => new { s, index })
                    .OrderByDescending(x => x.s.ReleaseData.ReleaseDate)
                    .ThenByDescending(x => x.index)
                    .Select(x => x.s)
                    .Take(PreviousSingles)
                    .ToList();
                for (int i = 0; i < latestFirst.Count; i++)
                {
                    singles._single previous = latestFirst[i];
                    moment.AddTo(Section, PreviousSingleKeys[i], () => Summary(previous, now));
                }
                singles._single best = earlier.OrderByDescending(s => s.ReleaseData.Sales).First();
                moment
                    .AddTo(Section, "Singles released before this one", () => earlier.Count)
                    .AddTo(Section, "Best-selling earlier single", () => MomentContext.TitleWithStyle(best) + ", " + best.ReleaseData.Sales + " sales");
            }
            catch (Exception)
            {
                // No history for this single
            }
        }

        /// <summary>
        /// "\"Title\" (Pop, Love lyrics, Cool choreography): 200000 sales, quality 40%, fan satisfaction 100%,
        /// released 7 months ago". The game's quality runs from 0 to 10 (stars, Single_Released_Button).
        /// </summary>
        public static string Summary(singles._single single, DateTime now)
        {
            singles._single._releaseData data = single.ReleaseData;
            string ago = Ago(data.ReleaseDate, now);
            return MomentContext.TitleWithStyle(single) + ": " + data.Sales + " sales, quality " + data.Quality * 10 + "%, fan satisfaction "
                + data.Fan_Satisfaction + "%" + (ago == "" ? "" : ", released " + ago);
        }

        /// <summary>
        /// The previous finished concert and the group's earlier concerts. Only finished ones count: the
        /// game sets an unfinished concert's FinishDate to today on every load.
        /// </summary>
        public static void Concert(Moment moment, SEvent_Concerts._concert concert, IEnumerable<SEvent_Concerts._concert> all, DateTime now)
        {
            try
            {
                List<SEvent_Concerts._concert> earlier = (all ?? Enumerable.Empty<SEvent_Concerts._concert>())
                    .Where(c => c != null && c != concert && c.Status == SEvent_Tour.tour._status.finished && c.ProjectedValues != null)
                    .ToList();
                if (earlier.Count == 0)
                {
                    return;
                }
                SEvent_Concerts._concert previous = Latest(earlier, c => c.FinishDate);
                moment
                    .AddTo(Section, "Previous concert", () => ConcertSummary(previous, now))
                    .AddTo(Section, "Concerts held before this one", () => earlier.Count)
                    .AddTo(Section, "Biggest audience at an earlier concert", () => earlier.Max(c => c.ProjectedValues.Actual_Audience));
            }
            catch (Exception)
            {
                // No history for this concert
            }
        }

        /// <summary>
        /// "Concert Hall, attendance 1500 out of 1500 seats, held 6 months ago"; without the venue when the game
        /// has no name for it.
        /// </summary>
        public static string ConcertSummary(SEvent_Concerts._concert concert, DateTime now)
        {
            string venue;
            try
            {
                venue = Moment.Clean(SEvent_Concerts.GetVenueTitle(concert.Venue));
            }
            catch (Exception)
            {
                venue = "";
            }
            string ago = Ago(concert.FinishDate, now);
            return (venue == "" ? "" : venue + ", ") + "attendance " + MomentContext.Attendance(concert) + (ago == "" ? "" : ", held " + ago);
        }

        /// <summary>
        /// The finished tours other than this one. The game removes a tour cancelled before launch from its list,
        /// and a launched tour always finishes.
        /// </summary>
        internal static List<SEvent_Tour.tour> EarlierTours(SEvent_Tour.tour tour, IEnumerable<SEvent_Tour.tour> all)
        {
            return (all ?? Enumerable.Empty<SEvent_Tour.tour>())
                .Where(t => t != null && t != tour && t.Status == SEvent_Tour.tour._status.finished)
                .ToList();
        }

        /// <summary>
        /// A tour's stops in the order played, without any the game can't place.
        /// </summary>
        internal static IEnumerable<SEvent_Tour.tour.selectedCountry> Stops(SEvent_Tour.tour tour)
        {
            return (tour?.SelectedCountries ?? new List<SEvent_Tour.tour.selectedCountry>()).Where(s => s?.Country != null);
        }

        /// <summary>
        /// Every place earlier tours went to, at its latest visit, newest first: "Osaka: 7600 out of 8000 seats,
        /// 4 months ago". Then how many tours there have been, this one included.
        /// </summary>
        public static void Tour(Moment moment, SEvent_Tour.tour tour, IEnumerable<SEvent_Tour.tour> all, DateTime now)
        {
            try
            {
                List<SEvent_Tour.tour> earlier = EarlierTours(tour, all);
                // Tours finish one at a time; of two on the same day, the one later in the list is later, as in Latest
                var visits = earlier
                    .Select((t, index) => new { t, index })
                    .SelectMany(x => Stops(x.t).Select(stop => new { stop, x.t.FinishDate, x.index }))
                    .GroupBy(v => v.stop.Country.Type)
                    .Select(g => g.OrderByDescending(v => v.FinishDate).ThenByDescending(v => v.index).First())
                    .OrderByDescending(v => v.FinishDate).ThenByDescending(v => v.index)
                    .ToList();
                moment.AddList(Section, "Places visited on earlier tours", visits.Select(v => (Func<object>)(() =>
                {
                    string stop = MomentContext.TourStop(v.stop);
                    string ago = Ago(v.FinishDate, now);
                    return stop == "" || ago == "" ? stop : stop + ", " + ago;
                })).ToList());
                moment.AddTo(Section, "Tours so far, this one included", () => earlier.Count + 1);
            }
            catch (Exception)
            {
                // No history for this tour
            }
        }

        /// <summary>
        /// An election's results in place order, without places the game left empty.
        /// </summary>
        internal static List<SEvent_SSK._SSK._result> Ranked(SEvent_SSK._SSK election)
        {
            return (election?.Results ?? new List<SEvent_SSK._SSK._result>())
                .Where(r => r?.Girl != null && r.Place > 0)
                .OrderBy(r => r.Place)
                .ToList();
        }

        /// <summary>
        /// The places in an election, by idol: her best one, should a mod have ranked her twice.
        /// </summary>
        internal static Dictionary<data_girls.girls, int> Places(SEvent_SSK._SSK election)
        {
            Dictionary<data_girls.girls, int> places = new();
            foreach (SEvent_SSK._SSK._result result in Ranked(election))
            {
                if (!places.ContainsKey(result.Girl))
                {
                    places[result.Girl] = result.Place;
                }
            }
            return places;
        }

        /// <summary>
        /// The finished election before this one, or null for the first.
        /// </summary>
        internal static SEvent_SSK._SSK PreviousElection(SEvent_SSK._SSK election, IEnumerable<SEvent_SSK._SSK> all)
        {
            List<SEvent_SSK._SSK> earlier = (all ?? Enumerable.Empty<SEvent_SSK._SSK>())
                .Where(e => e != null && e != election && e.Status == SEvent_Tour.tour._status.finished && e.FinishDate <= election.FinishDate)
                .ToList();
            return earlier.Count == 0 ? null : Latest(earlier, e => e.FinishDate);
        }

        /// <summary>
        /// "Election #2, won by Rin, 6 months ago", and how many elections there have been, this one included.
        /// Without counted, the count is left out: a cancelled election never happened.
        /// </summary>
        public static void Election(Moment moment, SEvent_SSK._SSK election, SEvent_SSK._SSK previous, IEnumerable<SEvent_SSK._SSK> all, DateTime now,
            bool counted = true)
        {
            try
            {
                if (previous != null)
                {
                    moment.AddTo(Section, "Previous election", () =>
                    {
                        data_girls.girls winner = Ranked(previous).FirstOrDefault()?.Girl;
                        string ago = Ago(previous.FinishDate, now);
                        return previous.GetTitle() + (winner == null ? "" : ", won by " + MomentContext.Name(winner)) + (ago == "" ? "" : ", " + ago);
                    });
                }
                if (counted)
                {
                    moment.AddTo(Section, "Elections so far, this one included", () =>
                        (all ?? Enumerable.Empty<SEvent_SSK._SSK>()).Count(e => e != null && e != election && e.Status == SEvent_Tour.tour._status.finished) + 1);
                }
            }
            catch (Exception)
            {
                // No history for this election
            }
        }

        /// <summary>The most cancelled shows and shows on air a show's start names.</summary>
        public const int ShowsListed = 3;

        /// <summary>
        /// For a show's start or end: shows cancelled in the past year, newest first, other shows on air and
        /// when they started, and, at a start, how many shows started before. The game keeps no end date:
        /// episodes air weekly from the launch date, so the last one aired 7 days times the episodes after the
        /// first later.
        /// </summary>
        public static void Shows(Moment moment, Shows._show show, IEnumerable<Shows._show> all, DateTime now, bool started = true)
        {
            try
            {
                List<Shows._show> others = (all ?? Enumerable.Empty<Shows._show>()).Where(s => s != null && s != show).ToList();
                moment.AddList(Section, "Recently cancelled shows", others
                    .Where(s => s.status == global::Shows._show._status.canceled && Known(s.LaunchDate) && s.episodeCount > 0)
                    .Select(s => new { s, last = LastAired(s) })
                    .Where(x => x.last <= now && x.last >= now.AddMonths(-12))
                    .OrderByDescending(x => x.last)
                    .Take(ShowsListed)
                    .Select(x => (Func<object>)(() =>
                    {
                        string ago = Ago(x.last, now);
                        return ShowName(x.s) + ", " + x.s.episodeCount + (x.s.episodeCount == 1 ? " episode" : " episodes") + (ago == "" ? "" : ", last aired " + ago);
                    })).ToList());
                moment.AddList(Section, "Other shows on air now", others
                    .Where(s => s.status == global::Shows._show._status.released || s.status == global::Shows._show._status.relaunching
                        || s.status == global::Shows._show._status.relaunching_working)
                    .Take(ShowsListed)
                    .Select(s => (Func<object>)(() =>
                    {
                        string ago = s.episodeCount > 0 ? Ago(s.LaunchDate, now) : "";
                        return ShowName(s) + (ago == "" ? "" : ", started " + ago);
                    })).ToList());
                if (started)
                {
                    moment.AddTo(Section, "Shows started before this one", () => others.Count(s => s.episodeCount > 0));
                }
            }
            catch (Exception)
            {
                // No history for this show
            }
        }

        /// <summary>The day a show's last episode aired: weekly from its launch.</summary>
        internal static DateTime LastAired(Shows._show show) => show.LaunchDate.AddDays(7 * (show.episodeCount - 1));

        /// <summary>The most cast members a listed show names before "and N more".</summary>
        public const int ShowCastNamed = 3;

        /// <summary>
        /// "\"Idol Kitchen\" (Internet, Food), with Rin and Aoi": its title, medium and genre as the game names
        /// them, and its cast: the idols in its cast slots, the whole group, or a rotating cast.
        /// </summary>
        private static string ShowName(Shows._show show)
        {
            string cast = show.castType == global::Shows._show._castType.entireGroup ? "the whole group"
                : show.castType == global::Shows._show._castType.rotatingCast ? "a rotating cast"
                : Part(() => MomentContext.JoinAnd(Cast(show)));
            return ShowTitle(show) + (cast == "" ? "" : ", with " + cast);
        }

        /// <summary>"\"Idol Kitchen\" (Internet, Food)": its title, medium and genre as the game names them.</summary>
        internal static string ShowTitle(Shows._show show)
        {
            List<string> kind = new[] { Part(() => show.medium?.GetTitle()), Part(() => show.genre?.GetTitle()) }.Where(t => t != "").ToList();
            return "\"" + Moment.Clean(show.title) + "\"" + (kind.Count == 0 ? "" : " (" + string.Join(", ", kind.ToArray()) + ")");
        }

        /// <summary>A fixed cast's names, up to ShowCastNamed, then "N more" as the last item.</summary>
        private static List<string> Cast(Shows._show show)
        {
            List<data_girls.girls> girls = (show.girls ?? new data_girls.girls[0]).Where(g => g != null).Distinct().ToList();
            List<string> names = girls.Take(ShowCastNamed).Select(MomentContext.Name).ToList();
            if (girls.Count > ShowCastNamed)
            {
                names.Add((girls.Count - ShowCastNamed) + " more");
            }
            return names;
        }

        private static string Part(Func<string> text)
        {
            try
            {
                return Moment.Clean(text() ?? "");
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// For a contract's start: her other running contracts, and how many deals of this kind she took before.
        /// </summary>
        public static void Contracts(Moment moment, business.active_proposal contract, IEnumerable<business.active_proposal> running,
            IEnumerable<business._history> history)
        {
            try
            {
                data_girls.girls girl = contract.Girl;
                moment.AddList(Section, "Her other running contracts", (running ?? Enumerable.Empty<business.active_proposal>())
                    .Where(c => c != null && c != contract && c.Girl == girl)
                    .Select(c => (Func<object>)(() => MomentContext.Contract(c))).ToList());
                bool tv = contract.Type == business._type.tv_drama;
                int earlier = (history ?? Enumerable.Empty<business._history>()).Count(h => h != null && h.Girl == girl && h.Type == contract.Type);
                moment.AddTo(Section, tv ? "Her earlier TV dramas" : "Her earlier advertisements", () => earlier);
            }
            catch (Exception)
            {
                // No history for this contract
            }
        }

        /// <summary>
        /// The agency's awards before this year's show: how many it won, last year's wins by title, and how many
        /// shows it has been in (a year with a win or a nomination), this one included.
        /// </summary>
        public static void Awards(Moment moment, AwardsShow show, IEnumerable<global::Awards._award> wins, IEnumerable<global::Awards._award> losses)
        {
            try
            {
                List<global::Awards._award> earlierWins = (wins ?? Enumerable.Empty<global::Awards._award>()).Where(a => a != null && a.Year < show.Year).ToList();
                List<string> lastYear = earlierWins.Where(a => a.Year == show.Year - 1)
                    .Select(a => Moment.Clean(global::Awards.GetAwardTitle(a.Type))).Where(t => t != "").ToList();
                int shows = earlierWins.Concat((losses ?? Enumerable.Empty<global::Awards._award>()).Where(a => a != null && a.Year < show.Year))
                    .Select(a => a.Year).Distinct().Count() + 1;
                moment
                    .AddTo(Section, "Awards won in earlier years", () => earlierWins.Count)
                    .AddTo(Section, "Last year's wins", () => MomentContext.JoinAnd(lastYear))
                    .AddTo(Section, "Awards shows the agency was in, this one included", () => shows);
            }
            catch (Exception)
            {
                // No history for this show
            }
        }

        /// <summary>
        /// The idols who joined last before these, all hired on the same day, and how long ago: "Ishikawa Yui
        /// and Ogawa Rena, 4 months ago". Graduates count: they joined too. Left out when no one joined before.
        /// </summary>
        public static void Joined(Moment moment, IList<data_girls.girls> hired, IEnumerable<data_girls.girls> all, DateTime now)
        {
            try
            {
                List<data_girls.girls> earlier = (all ?? Enumerable.Empty<data_girls.girls>())
                    .Where(g => g != null && !hired.Contains(g) && Known(g.Hiring_Date) && g.Hiring_Date.Date <= now.Date)
                    .ToList();
                if (earlier.Count == 0)
                {
                    return;
                }
                DateTime last = earlier.Max(g => g.Hiring_Date.Date);
                List<string> names = earlier.Where(g => g.Hiring_Date.Date == last).Select(MomentContext.Name).ToList();
                string ago = Ago(last, now);
                moment.AddTo(Section, names.Count == 1 ? "Previous new member" : "Previous new members", () => MomentContext.JoinAnd(names) + (ago == "" ? "" : ", " + ago));
            }
            catch (Exception)
            {
                // No history for these idols
            }
        }

        /// <summary>
        /// The previous graduation and how many idols graduated before her, from her own group: a graduate
        /// stays in her group's list. Without a group, every graduate counts. The first graduate is said to
        /// be. An unknown graduation date (old saves) counts towards the number but is never the previous one.
        /// </summary>
        public static void Graduation(Moment moment, data_girls.girls girl, IEnumerable<data_girls.girls> all, DateTime now)
        {
            try
            {
                Groups._group group = GroupOfIdol(girl);
                List<data_girls.girls> earlier = (all ?? Enumerable.Empty<data_girls.girls>())
                    .Where(g => g != null && g != girl && g.status == data_girls._status.graduated)
                    .Where(g => group == null || GroupOfIdol(g) == group)
                    .ToList();
                string countKey = group == null ? "Idols who graduated before her" : "Idols who graduated from the group before her";
                if (earlier.Count == 0)
                {
                    moment.AddTo(Section, countKey, () => "none, she is the first");
                    return;
                }
                List<data_girls.girls> dated = earlier.Where(g => Known(g.Graduation_Date)).ToList();
                data_girls.girls previous = dated.Count > 0 ? Latest(dated, g => g.Graduation_Date) : null;
                if (previous != null)
                {
                    moment
                        .AddTo(Section, "Previous graduate", () => MomentContext.Name(previous))
                        .AddTo(Section, "Previous graduation", () => Ago(previous.Graduation_Date, now));
                }
                moment.AddTo(Section, countKey, () => earlier.Count);
            }
            catch (Exception)
            {
                // No history for this graduation
            }
        }

        private static Groups._group GroupOfIdol(data_girls.girls girl)
        {
            try
            {
                return girl.GetGroup();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How often the same event happened before and when it last did, from the game's saved event log.
        /// </summary>
        public static void Event(Moment moment, Event_Manager._activeEvent active, IEnumerable<Event_Manager._activeEvent> log, DateTime now)
        {
            try
            {
                string id = active?.data?.id;
                if (string.IsNullOrEmpty(id) || log == null)
                {
                    return;
                }
                List<Event_Manager._activeEvent> before = log.Where(e => e != null && e != active && e.data?.id == id).ToList();
                if (before.Count > 0)
                {
                    moment
                        .AddTo(Section, "Times this event happened before", () => before.Count)
                        .AddTo(Section, "Last time it happened", () => Ago(before.Max(e => e.date), now));
                }
                Storyline(moment, active, log, now);
            }
            catch (Exception)
            {
                // No history for this event
            }
        }

        /// <summary>
        /// For a paid policy change or a firing, the latest scandal of each idol whose scandal points paid for it, and of
        /// the group when its own points did: her most recent event that can add scandal points (an idol in
        /// it), or the group's (no idol in it), from the game's event log. Dating scandals aren't in that
        /// log, and the game doesn't keep their outcome, so they're never named.
        /// </summary>
        public static void ScandalsPaidFor(Moment moment, IList<data_girls.girls> idols, bool group, IEnumerable<Event_Manager._activeEvent> log, DateTime now)
        {
            if (log == null)
            {
                return;
            }
            List<Event_Manager._activeEvent> scandals = log.Where(e => e != null && e.date <= now && EventOutcome.CanAddScandalPoints(e.data)).ToList();
            foreach (data_girls.girls girl in idols ?? new List<data_girls.girls>())
            {
                Latest(moment, MomentContext.Name(girl) + "'s latest scandal", scandals.Where(e => e.actors.Any(a => a?.girl == girl)), now);
            }
            if (group)
            {
                Latest(moment, "The group's latest scandal", scandals.Where(e => !e.actors.Any(a => a?.girl != null)), now);
            }
        }

        private static void Latest(Moment moment, string label, IEnumerable<Event_Manager._activeEvent> events, DateTime now)
        {
            Event_Manager._activeEvent latest = events.OrderByDescending(e => e.date).FirstOrDefault();
            if (latest != null)
            {
                string ago = Ago(latest.date, now);
                moment.AddTo(Section, label + (ago == "" ? "" : ", " + ago), () => latest.GetDescription());
            }
        }

        private const string TriggerEvent = "trigger_event";
        private const string TriggerEventSameActor = "trigger_event_same_actor";

        /// <summary>
        /// The text of the storyline's earlier events that happened and reached the public, oldest first: the
        /// events whose answers start this one (a trigger_event effect), and the ones that start those. The
        /// game doesn't save which answer the player gave, so an earlier event is left out unless every
        /// answer of it that leads on in the storyline has a public outcome (EventOutcome.IsPublic). The
        /// answers themselves aren't sent.
        /// </summary>
        private static void Storyline(Moment moment, Event_Manager._activeEvent active, IEnumerable<Event_Manager._activeEvent> log, DateTime now)
        {
            // Each earlier event, with the answers of it that lead on to a later event of the storyline
            Dictionary<string, List<Event_Manager._randomEvent._reply>> leading = new();
            Queue<string> next = new();
            next.Enqueue(active.data.id);
            while (next.Count > 0)
            {
                string id = next.Dequeue();
                foreach (Event_Manager._randomEvent data in Event_Manager.RandomEvents)
                {
                    List<Event_Manager._randomEvent._reply> starting = Starting(data, id);
                    if (starting.Count == 0 || string.IsNullOrEmpty(data.id) || data.id == active.data.id)
                    {
                        continue;
                    }
                    if (!leading.TryGetValue(data.id, out List<Event_Manager._randomEvent._reply> answers))
                    {
                        answers = new List<Event_Manager._randomEvent._reply>();
                        leading[data.id] = answers;
                        next.Enqueue(data.id);
                    }
                    answers.AddRange(starting.Where(r => !answers.Contains(r)));
                }
            }
            List<Event_Manager._activeEvent> chapters = leading
                .Where(chapter => chapter.Value.All(EventOutcome.IsPublic))
                .Select(chapter => log.Where(e => e != null && e != active && e.data?.id == chapter.Key && e.date <= active.date)
                    .OrderByDescending(e => e.date).FirstOrDefault())
                .Where(e => e != null)
                .OrderBy(e => e.date)
                .ToList();
            foreach (Event_Manager._activeEvent chapter in chapters)
            {
                string ago = Ago(chapter.date, now);
                moment.AddTo(Section, "Earlier in this storyline" + (ago == "" ? "" : ", " + ago), () => chapter.GetDescription());
            }
        }

        /// <summary>The event's answers that start the event with this id.</summary>
        private static List<Event_Manager._randomEvent._reply> Starting(Event_Manager._randomEvent data, string id)
        {
            if (data?.reply == null)
            {
                return new List<Event_Manager._randomEvent._reply>();
            }
            return data.reply.Where(r => r?.Effects != null && r.Effects.Any(effect =>
                effect != null && (effect.parameter == TriggerEvent || effect.parameter == TriggerEventSameActor)
                && (effect.formula ?? "").Split(' ')[0] == id)).ToList();
        }
    }
}
