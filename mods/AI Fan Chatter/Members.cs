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
        public const int SingleCap = 5;
        /// <summary>Stamina at or below this shows in an idol's line; above it, fans would see nothing to remark on.</summary>
        public const int LowStamina = 50;
        public const int ConcertCap = 5;
        public const int EventCap = 3;
        /// <summary>The most idols a policy change names, from the ones whose scandal points paid for it.</summary>
        public const int PolicyCap = 5;
        /// <summary>The most idols a tour names: every active idol goes, so the most popular ones.</summary>
        public const int TourCap = 5;
        /// <summary>The most idols an election names: the top places, and the biggest moves outside the top.</summary>
        public const int ElectionCap = 5;
        /// <summary>The election places named before the biggest moves outside the top.</summary>
        public const int ElectionTopFirst = 3;
        /// <summary>The most idols a show's start names: its cast, or the most popular for the whole group.</summary>
        public const int ShowCap = 5;
        /// <summary>The most idols an awards show names: winners, speakers, then nominees.</summary>
        public const int AwardsCap = 5;
        /// <summary>The most new idols an audition names.</summary>
        public const int AuditionCap = 5;
        /// <summary>Only the idols this far up the order get their trivia, to keep the prompt short.</summary>
        public const int TriviaIdols = 2;
        public const int TriviaLength = 120;
        public const int GraduationTriviaLength = 150;
        public const int MostPopular = 3;
        /// <summary>Groups smaller than this have no standouts: with two members, each would top or trail every stat.</summary>
        public const int StandoutGroupSize = 3;
        public const int AdultAge = 16;

        /// <summary>
        /// The stats a standout is named for, with how a top and a bottom value read.
        /// </summary>
        private static readonly Standout[] StandoutStats =
        {
            new(data_girls._paramType.vocal, skill: "vocal"),
            new(data_girls._paramType.dance, skill: "dance"),
            new(data_girls._paramType.cute, top: "cutest", bottom: "cute"),
            new(data_girls._paramType.cool, top: "coolest", bottom: "cool"),
            new(data_girls._paramType.pretty, top: "prettiest", bottom: "pretty"),
            new(data_girls._paramType.sexy, top: "sexiest", bottom: "sexy", adultsOnly: true),
            new(data_girls._paramType.funny, top: "funniest", bottom: "funny"),
            new(data_girls._paramType.smart, top: "smartest", bottom: "smart"),
        };

        /// <summary>
        /// A skill reads "top vocal skill" and "poorest vocal skill"; any other stat reads as its superlative
        /// ("cutest") and after "least" ("least cute").
        /// </summary>
        private sealed class Standout
        {
            public readonly data_girls._paramType Type;
            public readonly string Skill;
            public readonly string Top;
            public readonly string Bottom;
            public readonly bool AdultsOnly;

            public Standout(data_girls._paramType type, string skill = null, string top = null, string bottom = null, bool adultsOnly = false)
            {
                Type = type;
                Skill = skill;
                Top = top;
                Bottom = bottom;
                AdultsOnly = adultsOnly;
            }
        }

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
        /// A tour's idols: the game sends every active idol, from every group, so the ones with the most fans.
        /// </summary>
        public static List<data_girls.girls> ForTour()
        {
            return ByFans(data_girls.GetActiveGirls()).Take(TourCap).ToList();
        }

        /// <summary>
        /// An election's idols: the top places, then the biggest drop and rise outside the top, then the next
        /// places, up to ElectionCap. results is in place order.
        /// </summary>
        public static List<data_girls.girls> ForElection(IList<SEvent_SSK._SSK._result> results, data_girls.girls drop, data_girls.girls rise)
        {
            List<data_girls.girls> top = results.Where(r => r?.Girl != null).Select(r => r.Girl).ToList();
            return top.Take(ElectionTopFirst)
                .Concat(new[] { drop, rise }.Where(g => g != null))
                .Concat(top.Skip(ElectionTopFirst))
                .Distinct()
                .Take(ElectionCap)
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
        /// Without withElection the lines leave out her election place: an election's own moment gives it.
        /// A newcomer's line leaves out her time in the group, election, place by fans and singles: on her
        /// first day they're all empty.
        /// </summary>
        public static void Add(Moment moment, IList<data_girls.girls> idols, DateTime now, Groups._group eventGroup, IDictionary<data_girls.girls, Rank> knownRanks = null,
            bool withElection = true, bool newcomer = false)
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
                moment.AddTo(Moment.MembersSection, name, () => Line(girl, withTrivia, now, eventGroup, known, RankIn, withElection, newcomer));
            }
        }

        /// <summary>
        /// An idol's line, from who she is to her place in the group: trait, trivia, scandal record, age,
        /// how she's doing, her standout stats, then time in the group, election, rank by fans and singles.
        /// The scandal record shows only when she has one, stamina only when it's low, and stats only where
        /// she alone is highest or lowest in the group. The group's own totals (its age,
        /// members, singles) are in the agency section, so the line leaves them out.
        /// An idol from another group than the moment's (a sister group's member in a main single) has her
        /// time and rank given in her own group, by name and with its totals; her singles are still counted
        /// in the moment's group.
        /// </summary>
        public static string Line(data_girls.girls girl, bool withTrivia, DateTime now, Groups._group eventGroup, Rank knownRank,
            Func<data_girls.girls, Groups._group, string, Rank> rankIn, bool withElection = true, bool newcomer = false)
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
            Sentence(sentences, Part(() =>
            {
                string age = "Age " + girl.GetAge();
                string standing = Part(() => AgeStanding(girl, home, named));
                return standing == "" ? age : age + ", " + standing;
            }));
            Sentence(sentences, Join(
                Part(() => girl.GetMentalStamina() <= LowStamina ? "mental stamina " + OutOf100(girl.GetMentalStamina()) : ""),
                Part(() => girl.GetPhysicalStamina() <= LowStamina ? "stamina " + OutOf100(girl.GetPhysicalStamina()) : "")));
            Sentence(sentences, Part(() => Standouts(girl, home, named)));
            Sentence(sentences, newcomer ? Part(() => HighestPotential(girl)) : "");
            Sentence(sentences, newcomer ? "" : Join(
                Part(() => Tenure(girl, now, home, named)),
                Part(() => withElection ? Election(girl) : ""),
                Part(() => RankText(knownRank ?? rankIn?.Invoke(girl, home, named), knownRank != null)),
                Part(() => Singles(girl, eventGroup ?? own))));
            // Last, so it reads as a label: she isn't in the group the moment is about
            Sentence(sentences, guest ? Part(() => OtherGroup(own)) : "");
            return string.Join(" ", sentences.ToArray());
        }

        /// <summary>
        /// "Member of sister group Moons" for an idol from a sister group; "Member of main group Stars" for
        /// a main-group idol in a sister group's moment.
        /// </summary>
        public static string OtherGroup(Groups._group own)
        {
            string name = GroupName(own);
            return name == "" ? "" : "Member of " + (own.IsMain() ? "main" : "sister") + " group " + name;
        }

        /// <summary>
        /// "top vocal and dance skills, cutest and smartest in the group, but least funny": the stats where
        /// she alone has the highest or the lowest value among the group's members, herself included even
        /// after she graduates. A tie names no one. Group is the group's name when it isn't the moment's group.
        /// </summary>
        public static string Standouts(data_girls.girls girl, Groups._group group, string named)
        {
            if (girl == null || group == null)
            {
                return "";
            }
            List<data_girls.girls> members = group.GetGirls(true).Where(g => g != null).ToList();
            if (!members.Contains(girl))
            {
                members.Add(girl);
            }
            if (members.Count < StandoutGroupSize)
            {
                return "";
            }
            List<Standout> tops = new();
            List<Standout> bottoms = new();
            bool adult = Part(() => girl.GetAge() >= AdultAge ? "adult" : "") != "";
            foreach (Standout stat in StandoutStats.Where(s => adult || !s.AdultsOnly))
            {
                float? Value(data_girls.girls g)
                {
                    try
                    {
                        return g.getParam(stat.Type)?.val;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
                float? own = Value(girl);
                if (own == null)
                {
                    continue;
                }
                List<float> others = members.Where(g => g != girl).Select(Value).Where(v => v != null).Select(v => v.Value).ToList();
                if (others.Count == 0)
                {
                    continue;
                }
                if (own > others.Max())
                {
                    tops.Add(stat);
                }
                else if (own < others.Min())
                {
                    bottoms.Add(stat);
                }
            }
            // Skills share one "top ... skills", other stats one "least ...", and the group is named once
            string top = AndList(new[] { Skills("top", tops) }.Concat(tops.Where(t => t.Skill == null).Select(t => t.Top)).ToList());
            string bottom = AndList(new[] { Skills("poorest", bottoms), Least(bottoms) }.ToList());
            string where = "in " + (string.IsNullOrEmpty(named) ? "the group" : named);
            return top != "" && bottom != "" ? top + " " + where + ", but " + bottom
                : top != "" ? top + " " + where
                : bottom != "" ? bottom + " " + where
                : "";
        }

        /// <summary>
        /// "youngest in the group" or "oldest in Moons" when she alone is, by birthday, among the group's
        /// members, herself included even after she graduates; groups of 3 or more, as for the standouts. ""
        /// otherwise, or when a birthday is unknown.
        /// </summary>
        public static string AgeStanding(data_girls.girls girl, Groups._group group, string named)
        {
            if (girl == null || group == null || !History.Known(girl.birthday))
            {
                return "";
            }
            List<data_girls.girls> members = group.GetGirls(true).Where(g => g != null).ToList();
            if (!members.Contains(girl))
            {
                members.Add(girl);
            }
            List<DateTime> others = members.Where(g => g != girl).Select(g => g.birthday).ToList();
            if (members.Count < StandoutGroupSize || others.Any(b => !History.Known(b)))
            {
                return "";
            }
            string where = " in " + (string.IsNullOrEmpty(named) ? "the group" : named);
            // The latest birthday is the youngest
            return girl.birthday > others.Max() ? "youngest" + where
                : girl.birthday < others.Min() ? "oldest" + where
                : "";
        }

        /// <summary>
        /// "Highest potential: dance", or every stat tied for it ("vocal and dance"): how far she could grow,
        /// for a newcomer. The potential the game rolled when it made her; its GetPotential would roll one
        /// for a stat without, so it isn't called. Sexy counts only for adults, as in the standouts. "" when
        /// the game has none.
        /// </summary>
        public static string HighestPotential(data_girls.girls girl)
        {
            bool adult = Part(() => girl.GetAge() >= AdultAge ? "adult" : "") != "";
            List<KeyValuePair<string, int>> potentials = new();
            foreach (Standout stat in StandoutStats.Where(s => adult || !s.AdultsOnly))
            {
                int potential = 0;
                try
                {
                    potential = girl.getParam(stat.Type)?.potential ?? 0;
                }
                catch (Exception)
                {
                    // Left out
                }
                if (potential > 0)
                {
                    potentials.Add(new KeyValuePair<string, int>(stat.Skill ?? stat.Bottom, potential));
                }
            }
            if (potentials.Count == 0)
            {
                return "";
            }
            int best = potentials.Max(p => p.Value);
            return "Highest potential: " + AndList(potentials.Where(p => p.Value == best).Select(p => p.Key).ToList());
        }

        /// <summary>"top vocal skill", "poorest vocal and dance skills"; "" without skills.</summary>
        private static string Skills(string level, List<Standout> stats)
        {
            List<string> skills = stats.Where(s => s.Skill != null).Select(s => s.Skill).ToList();
            return skills.Count == 0 ? "" : level + " " + AndList(skills) + (skills.Count == 1 ? " skill" : " skills");
        }

        /// <summary>"least cute and funny"; "" without other stats.</summary>
        private static string Least(List<Standout> stats)
        {
            List<string> words = stats.Where(s => s.Skill == null).Select(s => s.Bottom).ToList();
            return words.Count == 0 ? "" : "least " + AndList(words);
        }

        /// <summary>"a", "a and b", "a, b and c"; empty items are left out.</summary>
        private static string AndList(List<string> items)
        {
            items = items.Where(i => !string.IsNullOrEmpty(i)).ToList();
            return items.Count == 0 ? "" : items.Count == 1 ? items[0]
                : string.Join(", ", items.Take(items.Count - 1).ToArray()) + " and " + items[items.Count - 1];
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
        /// An idol's scandal points in words, which the model can follow where the number means nothing; ""
        /// for none, as the agency's reputation already says when nobody has any. The bands follow the
        /// game's own cutoffs for an idol: business deals' liability rises above 2 points and doubles above 4
        /// (business.cs).
        /// </summary>
        public static string ScandalRecord(int points)
        {
            if (points <= 0)
            {
                return "";
            }
            if (points <= 2)
            {
                return "A minor scandal in her past";
            }
            if (points <= 4)
            {
                return "Several scandals behind her";
            }
            return "Involved in many public scandals";
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
        /// "a member for 2 years", or "a member of Moons for 8 of its 18 months" when the group is named: her
        /// time since she joined it (hired, moved over from another group, or when the group was formed,
        /// whichever is latest). The moment's group's age is in the agency section; a named group's is given
        /// here.
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
            if (string.IsNullOrEmpty(named))
            {
                return "a member for " + History.Span(hers);
            }
            string member = "a member of " + named;
            if (group == null || !History.Known(group.Date_Created))
            {
                return member + " for " + History.Span(hers);
            }
            int groups = (now.Date - group.Date_Created.Date).Days;
            int herCount = History.Count(hers, out string herUnit);
            History.Count(groups, out string groupUnit);
            string theirs = "its " + History.Span(groups);
            return herUnit == groupUnit
                ? member + " for " + herCount + " of " + theirs
                : member + " for " + History.Span(hers) + " of " + theirs;
        }

        /// <summary>
        /// Smallest field in which second and second-to-last places are named. In a smaller one they're the
        /// same middle places, so only first and last are.
        /// </summary>
        public const int SecondPlacesFrom = 5;

        public enum Standing
        {
            None,
            First,
            Second,
            SecondLast,
            Last
        }

        /// <summary>
        /// "1st", "2nd", "3rd", "4th", "11th", "12th", "13th", "21st".
        /// </summary>
        public static string Ordinal(int place)
        {
            int tens = place % 100;
            string suffix = tens >= 11 && tens <= 13 ? "th" : (place % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return place.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        /// <summary>
        /// Whether a place is one worth naming: first and last when there are 2 or more, second and second to
        /// last too from SecondPlacesFrom. A field of one has no standing.
        /// </summary>
        public static Standing StandingOf(int place, int of)
        {
            if (place <= 0 || of < 2 || place > of)
            {
                return Standing.None;
            }
            if (place == 1)
            {
                return Standing.First;
            }
            if (place == of)
            {
                return Standing.Last;
            }
            if (of >= SecondPlacesFrom && place == 2)
            {
                return Standing.Second;
            }
            if (of >= SecondPlacesFrom && place == of - 1)
            {
                return Standing.SecondLast;
            }
            return Standing.None;
        }

        /// <summary>
        /// Her place in the latest finished election when it's first, second, second to last or last, out of
        /// the idols it ranked. When she isn't in that election, her own latest
        /// result if first or second: how many it ranked isn't kept.
        /// </summary>
        public static string Election(data_girls.girls girl)
        {
            SEvent_SSK._SSK latest = LatestElection();
            SEvent_SSK._SSK._result result = latest?.Results?.FirstOrDefault(r => r != null && r.Girl == girl);
            if (result != null && result.Place > 0)
            {
                return StandingOf(result.Place, latest.Results.Count(r => r != null && r.Place > 0)) switch
                {
                    Standing.First => "1st in the last election",
                    Standing.Second => "2nd in the last election",
                    Standing.SecondLast => "second from bottom in the last election",
                    Standing.Last => "bottom in the last election",
                    _ => "",
                };
            }
            int? place = girl.SSK_GetLatestResult();
            return place == 1 ? "1st in her last election" : place == 2 ? "2nd in her last election" : "";
        }

        /// <summary>
        /// Her place by fans in a group when it's the most, second most, second fewest or fewest.
        /// </summary>
        public static string RankText(Rank rank, bool past)
        {
            if (rank == null)
            {
                return "";
            }
            string place = StandingOf(rank.Place, rank.Of) switch
            {
                Standing.First => "the most fans",
                Standing.Second => "the second most fans",
                Standing.SecondLast => "the second fewest fans",
                Standing.Last => "the fewest fans",
                _ => "",
            };
            if (place == "")
            {
                return "";
            }
            // The moment's group's size is in the agency section, but not a named group's, or the size before
            // a graduation
            string of = !string.IsNullOrEmpty(rank.Group) || past ? " of " + rank.Of : "";
            return (past ? "had " : "") + place + of + " in " + (string.IsNullOrEmpty(rank.Group) ? "the group" : rank.Group);
        }

        /// <summary>
        /// "in 5 of the group's singles (center of 2)": the group's released singles she's in. Their number is
        /// in the agency section.
        /// </summary>
        public static string Singles(data_girls.girls girl, Groups._group group)
        {
            List<singles._single> released = ReleasedSingles(group);
            if (released.Count == 0)
            {
                return "";
            }
            int hers = released.Count(s => s.girls.Contains(girl));
            int centers = released.Count(s => s.GetCenter() == girl);
            return "in " + hers + " of the group's singles" + (centers > 0 ? " (center of " + centers + ")" : "");
        }

        /// <summary>
        /// A group's released singles; every released single when there's no group.
        /// </summary>
        public static List<singles._single> ReleasedSingles(Groups._group group)
        {
            return singles.Singles
                .Where(s => s != null && s.status == singles._single._status.released)
                .Where(s => group == null || History.GroupOf(s) == group)
                .ToList();
        }

        /// <summary>
        /// The latest finished election, or null before the first one.
        /// </summary>
        public static SEvent_SSK._SSK LatestElection()
        {
            return History.Latest(
                SEvent_SSK.Elections.Where(e => e != null && e.Status == SEvent_Tour.tour._status.finished).ToList(), e => e.FinishDate);
        }

        /// <summary>
        /// Her place by fans in her group now; null when she isn't in a group.
        /// </summary>
        public static Rank RankOf(data_girls.girls girl) => RankIn(girl, girl?.GetGroup(), null);

        /// <summary>
        /// The other members of her clique; empty when she isn't in one or the player hasn't found it out yet.
        /// The game takes her out of it as she graduates.
        /// </summary>
        public static List<data_girls.girls> Friends(data_girls.girls girl)
        {
            Relationships._clique clique = girl?.GetClique();
            return clique == null || !clique.Known ? new List<data_girls.girls>()
                : clique.Members.Where(m => m != null && m != girl).ToList();
        }

        /// <summary>
        /// The idols bullying her, only where the player has found the bullying out; members who stopped are
        /// left out, as the game's tooltip does. The game ends the bullying as she graduates.
        /// </summary>
        public static List<data_girls.girls> BulliedBy(data_girls.girls girl)
        {
            return girl == null ? new List<data_girls.girls>()
                : Relationships.GetCliquesThatBully(girl)
                    .Where(c => c != null && c.KnownBulliedGirls.Contains(girl))
                    .SelectMany(c => c.Members.Where(m => m != null && m != girl && c.IsBulliedBy(m, girl)))
                    .Distinct().ToList();
        }

        /// <summary>
        /// The idols she is bullying, only where the player has found the bullying out. The game takes her out
        /// of her clique as she graduates.
        /// </summary>
        public static List<data_girls.girls> Bullied(data_girls.girls girl)
        {
            return girl == null ? new List<data_girls.girls>()
                : Relationships.GetBulliedGirls(girl, ShouldBeKnown: true).Where(g => g != null && g != girl).ToList();
        }

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
