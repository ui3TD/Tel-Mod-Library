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
        /// "\"Title\" (Pop, Love lyrics, Cool choreography): 200000 sales, quality 4 of 10, fan satisfaction
        /// 100 of 100, released 7 months ago". The game shows quality as stars out of 10 (Single_Released_Button).
        /// </summary>
        public static string Summary(singles._single single, DateTime now)
        {
            singles._single._releaseData data = single.ReleaseData;
            string ago = Ago(data.ReleaseDate, now);
            return MomentContext.TitleWithStyle(single) + ": " + data.Sales + " sales, quality " + data.Quality + " of 10, fan satisfaction "
                + data.Fan_Satisfaction + " of 100" + (ago == "" ? "" : ", released " + ago);
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
                    .AddTo(Section, "Previous concert", () => previous.GetTitle())
                    .AddTo(Section, "Previous concert venue", () => SEvent_Concerts.GetVenueTitle(previous.Venue))
                    .AddTo(Section, "Previous concert audience", () => previous.ProjectedValues.Actual_Audience)
                    .AddTo(Section, "Previous concert attendance", () => Math.Round(previous.ProjectedValues.Actual_Attendance * 100) + "%")
                    .AddTo(Section, "Previous concert held", () => Ago(previous.FinishDate, now))
                    .AddTo(Section, "Concerts held before this one", () => earlier.Count)
                    .AddTo(Section, "Biggest audience at an earlier concert", () => earlier.Max(c => c.ProjectedValues.Actual_Audience));
            }
            catch (Exception)
            {
                // No history for this concert
            }
        }

        /// <summary>
        /// The previous graduation and how many idols graduated before her. An unknown graduation date
        /// (old saves) counts towards the number but is never the previous one.
        /// </summary>
        public static void Graduation(Moment moment, data_girls.girls girl, IEnumerable<data_girls.girls> all, DateTime now)
        {
            try
            {
                List<data_girls.girls> earlier = (all ?? Enumerable.Empty<data_girls.girls>())
                    .Where(g => g != null && g != girl && g.status == data_girls._status.graduated)
                    .ToList();
                if (earlier.Count == 0)
                {
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
                moment.AddTo(Section, "Idols who graduated before her", () => earlier.Count);
            }
            catch (Exception)
            {
                // No history for this graduation
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
                if (before.Count == 0)
                {
                    return;
                }
                moment
                    .AddTo(Section, "Times this event happened before", () => before.Count)
                    .AddTo(Section, "Last time it happened", () => Ago(before.Max(e => e.date), now));
            }
            catch (Exception)
            {
                // No history for this event
            }
        }
    }
}
