using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AIFanChatter
{
    /// <summary>
    /// A short line about each idol a moment is about, so fans have something to say about her. Only the
    /// idols in the moment get one, most involved first, so large groups don't make the prompt grow.
    /// </summary>
    public static class Members
    {
        public const int SingleCap = 7;
        public const int ConcertCap = 5;
        public const int EventCap = 3;
        /// <summary>Only the idols this far up the order get their trivia, to keep the prompt short.</summary>
        public const int TriviaIdols = 2;
        public const int TriviaLength = 120;
        public const int GraduationTriviaLength = 150;
        public const int MostPopular = 3;

        /// <summary>
        /// An idol's place by fans among a group's members who haven't graduated. Group is that group's name
        /// when it isn't the moment's group, and null when it is ("the group").
        /// </summary>
        public sealed class Rank
        {
            public int Place;
            public int Of;
            public string Group;
        }

        /// <summary>
        /// A single's members in its formation: the center, then row by row from the front, by slot. The
        /// game's rows follow the slots (singles.GetGirlsRowInSenbatsu: slot 0 is the center's row, slots 1
        /// and 2 the next, and so on).
        /// </summary>
        public static List<data_girls.girls> ForSingle(singles._single single)
        {
            return single.girls.Where(g => g != null).Distinct().Take(SingleCap).ToList();
        }

        /// <summary>
        /// A concert's performers, most featured first: songs they center, then MC segments they're in, then
        /// their first appearance in the set list, then fans.
        /// </summary>
        public static List<data_girls.girls> ForConcert(SEvent_Concerts._concert concert)
        {
            Dictionary<data_girls.girls, int[]> seen = new();
            List<data_girls.girls> order = new();
            void Count(data_girls.girls girl, int kind, int position)
            {
                if (girl == null)
                {
                    return;
                }
                if (!seen.TryGetValue(girl, out int[] counts))
                {
                    counts = new[] { 0, 0, position };
                    seen[girl] = counts;
                    order.Add(girl);
                }
                counts[kind]++;
            }
            for (int i = 0; i < concert.SetListItems.Count; i++)
            {
                if (concert.SetListItems[i] is SEvent_Concerts._concert._song song)
                {
                    Count(song.Center, 0, i);
                }
                else if (concert.SetListItems[i] is SEvent_Concerts._concert._mc mc && mc.Girls != null)
                {
                    foreach (data_girls.girls girl in mc.Girls)
                    {
                        Count(girl, 1, i);
                    }
                }
            }
            return order
                .OrderByDescending(g => seen[g][0])
                .ThenByDescending(g => seen[g][1])
                .ThenBy(g => seen[g][2])
                .ThenByDescending(Fans)
                .Take(ConcertCap)
                .ToList();
        }

        /// <summary>
        /// The idols in an event, in the event's own actor order: the first is its main subject.
        /// </summary>
        public static List<data_girls.girls> ForEvent(Event_Manager._activeEvent active)
        {
            return active.actors.Where(a => a?.girl != null).Select(a => a.girl).Distinct().Take(EventCap).ToList();
        }

        /// <summary>
        /// Adds a line for each idol, in the order given. "The group" in the lines is the moment's group
        /// (eventGroup): the single's group, the main group for a concert, the graduating idol's group. Known
        /// ranks replace the ones worked out now: a graduating idol's is taken before the game moves her fans.
        /// </summary>
        public static void Add(Moment moment, IList<data_girls.girls> idols, DateTime now, Groups._group eventGroup, IDictionary<data_girls.girls, Rank> knownRanks = null)
        {
            if (idols == null)
            {
                return;
            }
            Dictionary<Groups._group, List<data_girls.girls>> byFans = new();
            Rank RankIn(data_girls.girls girl, Groups._group group, string named)
            {
                if (group == null)
                {
                    return null;
                }
                if (!byFans.TryGetValue(group, out List<data_girls.girls> sorted))
                {
                    sorted = ByFans(group.GetGirls(true));
                    byFans[group] = sorted;
                }
                int place = sorted.IndexOf(girl) + 1;
                return place > 0 ? new Rank { Place = place, Of = sorted.Count, Group = named } : null;
            }

            for (int i = 0; i < idols.Count && i < SingleCap; i++)
            {
                data_girls.girls girl = idols[i];
                if (girl == null)
                {
                    continue;
                }
                string name;
                try
                {
                    name = MomentContext.Name(girl);
                }
                catch (Exception)
                {
                    continue;
                }
                bool withTrivia = i < TriviaIdols;
                Rank known = knownRanks != null && knownRanks.TryGetValue(girl, out Rank rank) ? rank : null;
                moment.AddTo(Moment.MembersSection, name, () => Line(girl, withTrivia, now, eventGroup, known, RankIn));
            }
        }

        /// <summary>
        /// An idol's line, from who she is to her place in the group: trait, trivia, scandal record, age,
        /// how she's doing, then time in the group, election, rank by fans and singles. An idol from another
        /// group than the moment's (a sister group's member in a main single) has her time and rank given in
        /// her own group, by name; her singles are still counted in the moment's group.
        /// </summary>
        public static string Line(data_girls.girls girl, bool withTrivia, DateTime now, Groups._group eventGroup, Rank knownRank,
            Func<data_girls.girls, Groups._group, string, Rank> rankIn)
        {
            Groups._group own = OwnGroup(girl);
            bool guest = eventGroup != null && own != null && own != eventGroup;
            Groups._group home = guest || eventGroup == null ? own : eventGroup;
            string named = guest ? GroupName(own) : null;

            List<string> sentences = new();
            Sentence(sentences, Part(() => Trait(girl)));
            if (withTrivia && girl.Trivia != null)
            {
                foreach (string trivium in girl.Trivia.Take(2))
                {
                    Sentence(sentences, Cut(Moment.Clean(trivium), TriviaLength));
                }
            }
            Sentence(sentences, Part(() => ScandalRecord(girl.GetScandalPoints())));
            Sentence(sentences, Part(() => "Age " + girl.GetAge()));
            Sentence(sentences, Join(
                Part(() => "mental stamina " + OutOf100(girl.GetMentalStamina())),
                Part(() => "stamina " + OutOf100(girl.GetPhysicalStamina()))));
            Sentence(sentences, Join(
                Part(() => Tenure(girl, now, home, named)),
                Part(() => Election(girl)),
                Part(() => RankText(knownRank ?? rankIn?.Invoke(girl, home, named), knownRank != null)),
                Part(() => Singles(girl, eventGroup ?? own))));
            return string.Join(" ", sentences.ToArray());
        }

        private static Groups._group OwnGroup(data_girls.girls girl)
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
        /// A group's name as the game shows it: the player's name for the main group, the title for others.
        /// </summary>
        public static string GroupName(Groups._group group)
        {
            if (group == null)
            {
                return "";
            }
            string name = "";
            try
            {
                name = Moment.Clean(group.IsMain() ? staticVars.PlayerData.GetGroupName() : group.Title);
            }
            catch (Exception)
            {
                // Unnamed
            }
            return name == "" ? Moment.Clean(group.Title) : name;
        }

        /// <summary>
        /// "Trait: Thick Skin". The game's trait titles are nouns, adjectives, conditions or phrases (Prodigy,
        /// Clumsy, Anxiety, Live fast), and other mods add more, so the title is given as a label.
        /// </summary>
        public static string Trait(data_girls.girls girl)
        {
            string title = Moment.Clean(girl.GetTrait()?.Title);
            return title == "" ? "" : "Trait: " + title;
        }

        /// <summary>
        /// An idol's scandal points in words, which the model can follow where the number means nothing. The
        /// bands follow the game's own cutoffs for an idol: business deals' liability rises above 2 points
        /// and doubles above 4 (business.cs).
        /// </summary>
        public static string ScandalRecord(int points)
        {
            if (points <= 0)
            {
                return "No scandals";
            }
            if (points <= 2)
            {
                return "A minor scandal in her past";
            }
            if (points <= 4)
            {
                return "Several scandals behind her";
            }
            return "A serious scandal record";
        }

        /// <summary>
        /// The agency's total scandal points in words, banded where the game's penalties start: platinum
        /// auditions go at 6, gold ones at 12, bonus fans stop at 15 (ScandalPoints.cs), and 20 ends the
        /// game (Bankruptcy.cs).
        /// </summary>
        public static string AgencyReputation(long points)
        {
            if (points <= 0)
            {
                return "no scandals";
            }
            if (points <= 5)
            {
                return "a few scandals";
            }
            if (points <= 11)
            {
                return "a troubled reputation";
            }
            if (points <= 19)
            {
                return "a badly damaged reputation";
            }
            return "on the brink of collapse from scandals";
        }

        /// <summary>
        /// "a member for 2 of the group's 6 years", or "a member of Moons for 8 of its 18 months" when the
        /// group is named: her time since she joined it (hired, moved over from another group, or when the
        /// group was formed, whichever is latest), out of the group's age.
        /// </summary>
        public static string Tenure(data_girls.girls girl, DateTime now, Groups._group group, string named = null)
        {
            DateTime start = girl.Hiring_Date;
            if (girl.TransferDate > start)
            {
                start = girl.TransferDate;
            }
            if (group != null && group.Date_Created > start)
            {
                start = group.Date_Created;
            }
            if (!History.Known(start) || start.Date > now.Date)
            {
                return "";
            }
            int hers = (now.Date - start.Date).Days;
            string member = string.IsNullOrEmpty(named) ? "a member" : "a member of " + named;
            if (group == null || !History.Known(group.Date_Created))
            {
                return member + " for " + History.Span(hers);
            }
            int groups = (now.Date - group.Date_Created.Date).Days;
            int herCount = History.Count(hers, out string herUnit);
            History.Count(groups, out string groupUnit);
            string theirs = (string.IsNullOrEmpty(named) ? "the group's " : "its ") + History.Span(groups);
            return herUnit == groupUnit
                ? member + " for " + herCount + " of " + theirs
                : member + " for " + History.Span(hers) + " of " + theirs;
        }

        /// <summary>
        /// Her place in the latest finished election, out of its number of results; her own latest result
        /// alone when she isn't in that election.
        /// </summary>
        public static string Election(data_girls.girls girl)
        {
            SEvent_SSK._SSK latest = History.Latest(
                SEvent_SSK.Elections.Where(e => e != null && e.Status == SEvent_Tour.tour._status.finished).ToList(), e => e.FinishDate);
            SEvent_SSK._SSK._result result = latest?.Results?.FirstOrDefault(r => r != null && r.Girl == girl);
            if (result != null && result.Place > 0)
            {
                return Ordinal(result.Place) + " of " + latest.Results.Count + " in the last election";
            }
            int? place = girl.SSK_GetLatestResult();
            return place.HasValue && place.Value > 0 ? Ordinal(place.Value) + " in her last election" : "";
        }

        public static string RankText(Rank rank, bool past)
        {
            if (rank == null || rank.Place <= 0)
            {
                return "";
            }
            string place = rank.Place == 1 ? "the most fans" : (past ? "the " : "") + Ordinal(rank.Place) + " most fans";
            return (past ? "had " : "") + place + " of " + rank.Of + " in " + (string.IsNullOrEmpty(rank.Group) ? "the group" : rank.Group);
        }

        /// <summary>
        /// "in 5 of the group's 12 singles (center of 2)": the group's released singles she's in.
        /// </summary>
        public static string Singles(data_girls.girls girl, Groups._group group)
        {
            List<singles._single> released = singles.Singles
                .Where(s => s != null && s.status == singles._single._status.released)
                .Where(s => group == null || History.GroupOf(s) == group)
                .ToList();
            if (released.Count == 0)
            {
                return "";
            }
            int hers = released.Count(s => s.girls.Contains(girl));
            int centers = released.Count(s => s.GetCenter() == girl);
            return "in " + hers + " of the group's " + released.Count + " singles" + (centers > 0 ? " (center of " + centers + ")" : "");
        }

        /// <summary>
        /// Her place by fans in her group now; null when she isn't in a group.
        /// </summary>
        public static Rank RankOf(data_girls.girls girl) => RankIn(girl, girl?.GetGroup(), null);

        /// <summary>
        /// Her place by fans in a group, named when it isn't the moment's group; null when she isn't in it.
        /// </summary>
        public static Rank RankIn(data_girls.girls girl, Groups._group group, string named)
        {
            if (group == null)
            {
                return null;
            }
            List<data_girls.girls> sorted = ByFans(group.GetGirls(true));
            int place = sorted.IndexOf(girl) + 1;
            return place > 0 ? new Rank { Place = place, Of = sorted.Count, Group = named } : null;
        }

        /// <summary>
        /// Idols by fans, most first; ties keep their order.
        /// </summary>
        public static List<data_girls.girls> ByFans(IEnumerable<data_girls.girls> girls)
        {
            return girls.Where(g => g != null).OrderByDescending(Fans).ToList();
        }

        private static long Fans(data_girls.girls girl)
        {
            try
            {
                return girl.GetFans_Total();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static string Ordinal(int number)
        {
            int lastTwo = number % 100;
            string suffix = lastTwo >= 11 && lastTwo <= 13 ? "th"
                : (number % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return number.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        private static string OutOf100(float value)
        {
            return Math.Round(Math.Max(0f, Math.Min(100f, value))).ToString(CultureInfo.InvariantCulture) + "/100";
        }

        /// <summary>
        /// Text cut to a length, with "..." when it's cut.
        /// </summary>
        public static string Cut(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }
            return text.Substring(0, max - 3).TrimEnd() + "...";
        }

        private static string Part(Func<string> part)
        {
            try
            {
                return part() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Join(params string[] parts) => string.Join(", ", parts.Where(p => p != "").ToArray());

        /// <summary>
        /// Adds a sentence, capitalised and ending in a full stop, unless it's empty.
        /// </summary>
        private static void Sentence(List<string> sentences, string text)
        {
            text = text?.Trim() ?? "";
            if (text == "")
            {
                return;
            }
            text = char.ToUpperInvariant(text[0]) + text.Substring(1);
            char last = text[text.Length - 1];
            sentences.Add(last == '.' || last == '!' || last == '?' ? text : text + ".");
        }
    }
}
