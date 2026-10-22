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
        /// "Voting single for Election #3: each CD comes with a ballot, and the results are announced after the
        /// concert at Open Air Stage" for a voting single before its election; "Election single: its lineup is
        /// the top places of Election #2, won by Rin" for the single the game makes from an election's results.
        /// </summary>
        private static string SingleElection(singles._single single)
        {
            SEvent_SSK._SSK parent = ParentElection(single);
            if (parent != null)
            {
                data_girls.girls winner = History.Ranked(parent).FirstOrDefault()?.Girl;
                return "Election single: its lineup is the top places of " + Part(() => parent.GetTitle()) + (winner == null ? "" : ", won by " + Name(winner));
            }
            SEvent_SSK._SSK voting = single.SSK;
            if (voting == null || voting.Status == SEvent_Tour.tour._status.finished)
            {
                return "";
            }
            string venue = voting.Concert == null ? "" : Part(() => SEvent_Concerts.GetVenueTitle(voting.Concert.Venue));
            // The game's own description: each CD comes packaged with a ballot (SSK__SINGLE_TIP)
            return "Voting single for " + Part(() => voting.GetTitle()) + ": each CD comes with a ballot"
                + (venue == "" ? "" : ", and the results are announced after the concert at " + venue);
        }

        /// <summary>
        /// An election cancelled after its voting single came out, so its ballots no longer count: the single,
        /// what fans spent on it, and its results concert. The game lowers hardcore and casual fans' opinion as
        /// it cancels, before this moment's prompt reads the opinions.
        /// </summary>
        public static Moment ElectionCancelled(SEvent_SSK_CancelSSK.Before cancelled)
        {
            singles._single single = cancelled.Single;
            Moment moment = new Moment("an election was cancelled after its voting single came out: the ballot that came with each CD no longer counts")
                .Labelled(() => cancelled.Title + "'s cancellation")
                .Add("Election", () => cancelled.Title)
                .Add("Was to be broadcast on", () => SEvent_SSK.GetBroadcastTitle(cancelled.Broadcast))
                .Add("Voting single", () => "\"" + Moment.Clean(single.title) + "\""
                    + (History.Ago(single.ReleaseData.ReleaseDate, Now) is string ago && ago != "" ? ", released " + ago : ""))
                .Add("Voting single sales", () => single.ReleaseData.Sales + (single.ReleaseData.Sales == 1 ? " CD" : " CDs"))
                // What fans paid: CDs sold times the price per CD, as the game counts it at release
                .Add("Money fans spent on it", () => single.GetMoney() + " yen")
                .Add("Results concert", () => cancelled.Concert == null ? ""
                    : Part(() => SEvent_Concerts.GetVenueTitle(cancelled.Concert.Venue)) is string venue && venue != ""
                        ? "at " + venue + (cancelled.ConcertCancelled ? ", also cancelled" : ", still on without the results")
                        : cancelled.ConcertCancelled ? "also cancelled" : "still on without the results");
            Groups._group group = History.GroupOf(single);
            Members.Add(moment, Members.ForSingle(single), Now, group);
            History.Election(moment, cancelled.Election, Members.LatestElection(), SEvent_SSK.Elections, Now, counted: false);
            return Agency(moment, group);
        }

        /// <summary>The election whose results set this single's lineup; null for any other single.</summary>
        private static SEvent_SSK._SSK ParentElection(singles._single single)
        {
            try
            {
                return single.IsElectionSingle ? single.GetParentSSK() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The most places an election's results list.</summary>
        public const int ElectionPlaces = 10;

        /// <summary>
        /// An election's results, once the player has revealed every place. Every idol of every group takes
        /// part, so the moment is the main group's; idols of other groups are named with their group.
        /// </summary>
        public static Moment Election(SEvent_SSK._SSK election)
        {
            Moment moment = new Moment("the results of the agency's idol election were announced")
                .Labelled(() => election.GetTitle());
            List<data_girls.girls> idols = ElectionFacts(moment, election, withConcert: true, out SEvent_SSK._SSK previous);
            Groups._group group = MainGroup();
            Members.Add(moment, idols, Now, group, withElection: false);
            History.Election(moment, election, previous, SEvent_SSK.Elections, Now);
            return Agency(moment, group);
        }

        /// <summary>
        /// An election's own facts: its number, broadcast, voting single, results and biggest moves. Gives the
        /// idols its moment names and the election before it.
        /// </summary>
        /// <param name="withConcert">Names the concert it followed; a moment that is about that concert already does.</param>
        private static List<data_girls.girls> ElectionFacts(Moment moment, SEvent_SSK._SSK election, bool withConcert, out SEvent_SSK._SSK previous)
        {
            List<SEvent_SSK._SSK._result> results = History.Ranked(election);
            previous = History.PreviousElection(election, SEvent_SSK.Elections);
            Dictionary<data_girls.girls, int> before = previous == null ? null : History.Places(previous);
            List<SEvent_SSK._SSK._result> top = results.Take(ElectionPlaces).ToList();
            Move drop = before == null ? null : BiggestDrop(results, top, before);
            Move rise = before == null ? null : BiggestRise(results, top, before);
            moment
                .Add("Election", () => election.GetTitle())
                .Add("Broadcast on", () => SEvent_SSK.GetBroadcastTitle(election.Broadcast))
                .Add("Voting single", () => election.Single == null ? "" : "\"" + Moment.Clean(election.Single.title) + "\"")
                .Add("Held after the concert at", () => !withConcert || election.Concert == null ? "" : SEvent_Concerts.GetVenueTitle(election.Concert.Venue))
                .Add("Idols ranked", () => results.Count);
            moment.AddList(Moment.FactsSection, "Results",
                top.Select(r => (Func<object>)(() => Members.Ordinal(r.Place) + ": " + Ranked(r) + Movement(r, before) + Leaving(r.Girl))).ToList());
            moment
                .Add("Biggest drop outside the top " + ElectionPlaces, () => drop == null ? "" : drop.Describe())
                .Add("Biggest rise outside the top " + ElectionPlaces, () => rise == null ? "" : rise.Describe());
            return Members.ForElection(top, drop?.Girl, rise?.Girl);
        }

        /// <summary>
        /// "Rin, 48200 votes"; "Mio of Moons, 900 votes" for an idol of a sister group.
        /// </summary>
        private static string Ranked(SEvent_SSK._SSK._result result)
        {
            return Named(result.Girl) + ", " + result.Votes + (result.Votes == 1 ? " vote" : " votes");
        }

        /// <summary>
        /// Her name, with her group's after it when it isn't the main group: "Momo (Yasuda Momone) of Moons".
        /// </summary>
        private static string Named(data_girls.girls girl)
        {
            Groups._group own = GroupOf(() => girl.GetGroup());
            string group = own == null || own.IsMain() ? "" : Members.GroupName(own);
            return Name(girl) + (group == "" ? "" : " of " + group);
        }

        /// <summary>
        /// ", announced her graduation" for an idol in her last months: she still takes part in elections.
        /// </summary>
        private static string Leaving(data_girls.girls girl)
        {
            return girl.status == data_girls._status.announced_graduation ? ", announced her graduation" : "";
        }

        /// <summary>
        /// " (up from 3rd)", " (down from 1st)", " (same place as last time)", " (not ranked last time)"; ""
        /// for the first election.
        /// </summary>
        private static string Movement(SEvent_SSK._SSK._result result, Dictionary<data_girls.girls, int> before)
        {
            if (before == null)
            {
                return "";
            }
            if (!before.TryGetValue(result.Girl, out int was))
            {
                return " (not ranked last time)";
            }
            return was == result.Place ? " (same place as last time)"
                : " (" + (result.Place < was ? "up" : "down") + " from " + Members.Ordinal(was) + ")";
        }

        /// <summary>
        /// An idol's move between two elections, outside the listed places: her result now, or none when the
        /// game didn't keep her place (it keeps the top 10).
        /// </summary>
        private sealed class Move
        {
            public data_girls.girls Girl;
            public SEvent_SSK._SSK._result Now;
            public int Was;

            public string Describe()
            {
                return Now == null
                    ? Named(Girl) + ", no longer ranked (was " + Members.Ordinal(Was) + ")" + Leaving(Girl)
                    : Members.Ordinal(Now.Place) + ": " + Ranked(Now) + " (" + (Now.Place < Was ? "up" : "down") + " from " + Members.Ordinal(Was) + ")" + Leaving(Girl);
            }
        }

        /// <summary>
        /// The idol ranked last time who fell furthest and isn't in the listed places. An idol the game no
        /// longer ranks counts as one place below the last one ranked. Graduates are left out: they didn't
        /// take part. Of equal falls, the one who was higher.
        /// </summary>
        private static Move BiggestDrop(List<SEvent_SSK._SSK._result> results, List<SEvent_SSK._SSK._result> top, Dictionary<data_girls.girls, int> before)
        {
            Move best = null;
            int bestFall = 0;
            foreach (KeyValuePair<data_girls.girls, int> was in before.OrderBy(p => p.Value))
            {
                data_girls.girls girl = was.Key;
                if (top.Any(r => r.Girl == girl) || girl.status == data_girls._status.graduated)
                {
                    continue;
                }
                SEvent_SSK._SSK._result now = results.FirstOrDefault(r => r.Girl == girl);
                int fall = (now?.Place ?? results.Count + 1) - was.Value;
                if (fall > bestFall)
                {
                    best = new Move { Girl = girl, Now = now, Was = was.Value };
                    bestFall = fall;
                }
            }
            return best;
        }

        /// <summary>
        /// The idol below the listed places who climbed furthest since last time. Only places past the top 10
        /// can show this, which the game keeps only with a mod. An idol ranked for the first time has no
        /// earlier place to climb from. Of equal climbs, the one placed higher now.
        /// </summary>
        private static Move BiggestRise(List<SEvent_SSK._SSK._result> results, List<SEvent_SSK._SSK._result> top, Dictionary<data_girls.girls, int> before)
        {
            Move best = null;
            int bestClimb = 0;
            foreach (SEvent_SSK._SSK._result now in results.Skip(top.Count))
            {
                if (before.TryGetValue(now.Girl, out int was) && was - now.Place > bestClimb)
                {
                    best = new Move { Girl = now.Girl, Now = now, Was = was };
                    bestClimb = was - now.Place;
                }
            }
            return best;
        }
    }
}
