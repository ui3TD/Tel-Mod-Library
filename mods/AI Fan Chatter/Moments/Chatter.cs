using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <summary>The idols a Refresh's fans hear about: one of the most popular, the rest from anyone.</summary>
        public const int ChatterIdols = 5;
        /// <summary>How many of the group's releases and shows a Refresh's fans are reminded of.</summary>
        public const int ChatterMemories = 3;
        /// <summary>The feed's newest posts a Refresh gives, so batch after batch doesn't repeat itself.</summary>
        public const int ChatterFeedPosts = 20;

        /// <summary>
        /// Everyday chatter, as the player's Refresh asks for it with nothing queued. Each one picks a different
        /// mix, so batch after batch doesn't come back to the same facts: one of the most popular members and
        /// four others, and three of the latest single, an older single, the latest concert and a show on air.
        /// The prompt gives more of the feed than other moments.
        /// </summary>
        public static Moment Chatter(Random random)
        {
            Moment moment = new Moment("everyday fan chatter about the group") { FeedPosts = ChatterFeedPosts }
                .Labelled(() => "a refresh of the feed")
                .Add("What fans talk about", () => "each post picks its own topic: a favourite member, a single or concert, a show on air, "
                    + "fan life (merch, handshake events, meeting other fans at shows), or what they hope comes next");
            Groups._group group = MainGroup();
            Members.Add(moment, ChatterIdolsOf(group, random), Now, group);

            List<KeyValuePair<string, Func<object>>> memories = new();
            List<singles._single> released = Members.ReleasedSingles(group).Where(s => s.ReleaseData != null)
                .Select((s, index) => new { s, index })
                .OrderByDescending(x => x.s.ReleaseData.ReleaseDate).ThenByDescending(x => x.index)
                .Select(x => x.s).ToList();
            if (released.Count > 0)
            {
                singles._single latest = released[0];
                memories.Add(new("Latest single", () => History.Summary(latest, Now)));
            }
            if (released.Count > 1)
            {
                singles._single older = released[1 + random.Next(released.Count - 1)];
                memories.Add(new("An earlier single", () => History.Summary(older, Now)));
            }
            SEvent_Concerts._concert concert = SEvent_Concerts.Concerts
                .Where(c => c != null && c.Status == SEvent_Tour.tour._status.finished && c.ProjectedValues != null)
                .OrderByDescending(c => c.FinishDate).FirstOrDefault();
            if (concert != null)
            {
                memories.Add(new("Latest concert", () => History.ConcertSummary(concert, Now)));
            }
            List<Shows._show> onAir = Shows.shows.Where(s => s != null && s.status == Shows._show._status.released).ToList();
            if (onAir.Count > 0)
            {
                Shows._show show = onAir[random.Next(onAir.Count)];
                memories.Add(new("A show on air", () => History.ShowName(show)));
            }
            foreach (KeyValuePair<string, Func<object>> memory in memories.OrderBy(_ => random.Next()).Take(ChatterMemories))
            {
                moment.AddTo(Moment.HistorySection, memory.Key, memory.Value);
            }
            return Agency(moment, group);
        }

        /// <summary>One of the group's most popular members and others from the whole group, in fan order.</summary>
        private static List<data_girls.girls> ChatterIdolsOf(Groups._group group, Random random)
        {
            List<data_girls.girls> byFans = Members.ByFans(group?.GetGirls(true) ?? new List<data_girls.girls>());
            if (byFans.Count <= ChatterIdols)
            {
                return byFans;
            }
            data_girls.girls popular = byFans[random.Next(Math.Min(Members.MostPopular, byFans.Count))];
            List<data_girls.girls> picked = byFans.Where(g => g != popular).OrderBy(_ => random.Next()).Take(ChatterIdols - 1).ToList();
            picked.Add(popular);
            return byFans.Where(picked.Contains).ToList();
        }
    }
}
