using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    /// <summary>
    /// Something fans react to, as "key: value" lines for the model, in sections the prompt's template
    /// places: the moment's own facts, the idols in it, its history and the agency.
    /// </summary>
    public sealed class Moment
    {
        public const string FactsSection = "facts";
        public const string MembersSection = "members";
        public const string HistorySection = "history";
        public const string AgencySection = "agency";

        /// <summary>
        /// The sections every moment has, so the template's placeholders for them are filled even when empty.
        /// </summary>
        public static readonly string[] Sections = { FactsSection, MembersSection, HistorySection, AgencySection };

        public readonly string Kind;
        private readonly Dictionary<string, List<KeyValuePair<string, string>>> sections = new();
        private string label;

        public Moment(string kind)
        {
            Kind = kind;
            foreach (string section in Sections)
            {
                sections[section] = new List<KeyValuePair<string, string>>();
            }
        }

        /// <summary>
        /// The moment as the player knows it, for notices: "the single Midnight Platform", "Kaho's
        /// graduation". The kind when no name was given or the game couldn't give one.
        /// </summary>
        public string Label => string.IsNullOrEmpty(label) ? Kind : label;

        /// <summary>
        /// Names the moment for the player; a name the game can't give leaves the kind.
        /// </summary>
        public Moment Labelled(Func<string> name)
        {
            try
            {
                label = Clean(name());
            }
            catch (Exception)
            {
                label = null;
            }
            return this;
        }

        /// <summary>The moment's own facts.</summary>
        public List<KeyValuePair<string, string>> Facts => sections[FactsSection];

        public IEnumerable<string> SectionNames => sections.Keys;

        public List<KeyValuePair<string, string>> Section(string section)
        {
            return sections.TryGetValue(section, out List<KeyValuePair<string, string>> facts) ? facts : new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Adds a fact about the moment itself.
        /// </summary>
        public Moment Add(string name, Func<object> value) => AddTo(FactsSection, name, value);

        /// <summary>
        /// Adds a fact to a section. Each is read on its own, so one the game can't give (an unloaded part,
        /// another mod's change) only leaves that line out.
        /// </summary>
        public Moment AddTo(string section, string name, Func<object> value)
        {
            try
            {
                string text = Clean(Convert.ToString(value(), CultureInfo.InvariantCulture));
                if (text != "")
                {
                    if (!sections.TryGetValue(section, out List<KeyValuePair<string, string>> facts))
                    {
                        facts = new List<KeyValuePair<string, string>>();
                        sections[section] = facts;
                    }
                    facts.Add(new KeyValuePair<string, string>(name, text));
                }
            }
            catch (Exception)
            {
                // Left out
            }
            return this;
        }

        /// <summary>
        /// Adds a list to a section: a header line, then one "- " line per item, in order. Each item is read
        /// on its own, so one the game can't give only leaves that item out; a list with no items is left out.
        /// </summary>
        public Moment AddList(string section, string header, IEnumerable<Func<object>> items)
        {
            List<string> lines = new();
            foreach (Func<object> item in items)
            {
                try
                {
                    string text = Clean(Convert.ToString(item(), CultureInfo.InvariantCulture));
                    if (text != "")
                    {
                        lines.Add("- " + text);
                    }
                }
                catch (Exception)
                {
                    // Left out
                }
            }
            if (lines.Count > 0)
            {
                if (!sections.TryGetValue(section, out List<KeyValuePair<string, string>> facts))
                {
                    facts = new List<KeyValuePair<string, string>>();
                    sections[section] = facts;
                }
                facts.Add(new KeyValuePair<string, string>(header, ListStart + string.Join("\n", lines.ToArray())));
            }
            return this;
        }

        // A list's value starts on the line after its header, as AddList stores it
        private const string ListStart = "\n";

        /// <summary>
        /// A section's facts as "key: value" lines, and lists as their header and items; "" when it has none.
        /// </summary>
        public string Lines(string section)
        {
            return string.Join("\n", Section(section).Select(fact => fact.Value.StartsWith(ListStart, StringComparison.Ordinal)
                ? fact.Key + ":" + fact.Value
                : fact.Key + ": " + fact.Value).ToArray());
        }

        /// <summary>
        /// The user message for this moment, without the feed.
        /// </summary>
        public string Describe() => Prompt.User(this, null);

        private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// The game's text without its colour and style tags, on one line.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return Tags.Replace(text, "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    /// <summary>
    /// Whether fans knew an idol was leaving before her graduation.
    /// </summary>
    public enum GraduationNotice
    {
        /// <summary>She left at once, without announcing it.</summary>
        Sudden,
        /// <summary>She announced it 3 months before.</summary>
        Announced,
        /// <summary>The producer fired her at once.</summary>
        Fired,
    }

    /// <summary>
    /// The facts for each kind of moment, read from the game.
    /// </summary>
    public static class MomentContext
    {
        public static string Translate(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "";
            }
            return Language.Data != null && Language.Data.TryGetValue(key, out string text) ? text : key;
        }

        /// <summary>
        /// Up to max names, then "and N more" for the rest, so the model still knows how many there are.
        /// </summary>
        public static string Names(IEnumerable<data_girls.girls> girls, int max)
        {
            List<data_girls.girls> all = girls.Where(g => g != null).ToList();
            string names = string.Join(", ", all.Take(max).Select(Name).ToArray());
            return all.Count > max ? names + " and " + (all.Count - max) + " more" : names;
        }

        /// <summary>
        /// An idol's name for the model: the name the game shows, and her full name after a nickname, so fans
        /// can use either. The game's GetName gives only the nickname once one is set.
        /// </summary>
        public static string Name(data_girls.girls girl)
        {
            if (string.IsNullOrEmpty(girl.nickname))
            {
                return girl.GetName(true);
            }
            // The full name as the game builds it when there's no nickname (girls.girlName). Its IsFemale is
            // private and always true for idols.
            return WithNickname(girl.nickname, Language.GetVariableName(girl.firstName, girl.lastName, true, "default", IgnoreColor: true));
        }

        /// <summary>
        /// "Momo (Yasuda Momone)"; just the nickname when the full name is missing or the same.
        /// </summary>
        public static string WithNickname(string nickname, string fullName)
        {
            fullName = Moment.Clean(fullName);
            return fullName == "" || fullName == nickname ? nickname : nickname + " (" + fullName + ")";
        }

        /// <summary>
        /// A person in an event as the game names them (Event_Manager._activeEvent._actor.GetName), with an
        /// idol's full name after her nickname.
        /// </summary>
        public static string ActorName(Event_Manager._activeEvent._actor actor)
        {
            return actor.name == "" && actor.girl != null ? Name(actor.girl) : actor.GetName(true);
        }

        /// <summary>
        /// "A, B and C".
        /// </summary>
        public static string JoinAnd(IList<string> items)
        {
            if (items.Count <= 1)
            {
                return items.Count == 1 ? items[0] : "";
            }
            return string.Join(", ", items.Take(items.Count - 1).ToArray()) + " and " + items[items.Count - 1];
        }

        /// <summary>
        /// A single's genre, lyrics and choreography as the game titles them: "Metal, Regret lyrics, Cute
        /// choreography". One the single doesn't have is left out.
        /// </summary>
        public static string Style(singles._single single)
        {
            List<string> parts = new();
            void Part(Func<string> title, string suffix)
            {
                try
                {
                    string text = Moment.Clean(title());
                    if (text != "")
                    {
                        parts.Add(text + suffix);
                    }
                }
                catch (Exception)
                {
                    // Left out
                }
            }
            Part(() => single.genre?.GetTitle(), "");
            Part(() => single.lyrics?.GetTitle(), " lyrics");
            Part(() => single.choreography?.GetTitle(), " choreography");
            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// A single's title in quotes, as titles can hold commas, with its style: "\"Title\" (Metal, Regret
        /// lyrics, Cute choreography)".
        /// </summary>
        public static string TitleWithStyle(singles._single single)
        {
            string style = Style(single);
            return "\"" + Moment.Clean(single.title) + "\"" + (style == "" ? "" : " (" + style + ")");
        }

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

        private static DateTime Now => staticVars.dateTime;

        /// <summary>
        /// The main group, or null when the game has no groups yet.
        /// </summary>
        internal static Groups._group MainGroup()
        {
            try
            {
                return Groups.Groups_.FirstOrDefault(g => g != null && g.IsMain());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The group a moment is about; null when the game can't say.
        /// </summary>
        private static Groups._group GroupOf(Func<Groups._group> group)
        {
            try
            {
                return group();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The agency's facts, with the moment's group as "the group": a sister group's moment names that
        /// group and the main group it belongs to. Null is the main group.
        /// </summary>
        /// <param name="graduation">The moment is about one idol leaving: her graduation or its announcement.</param>
        /// <param name="friends">The leaving idol's clique, named by group in place of the most popular members.</param>
        /// <param name="rules">Adds the agency rules that matter to this kind of moment, after the concept.</param>
        /// <param name="changes">How many members each group gained (more than 0) or lost (less than 0) in the moment.
        /// Only these groups' sizes are given, one line each; a moment that changes no group gives none.</param>
        private static Moment Agency(Moment moment, Groups._group group, bool graduation = false,
            List<data_girls.girls> friends = null, Func<Moment, Moment> rules = null, IDictionary<Groups._group, int> changes = null)
        {
            const string section = Moment.AgencySection;
            Groups._group main = MainGroup();
            group ??= main;
            bool sister = group != null && !group.IsMain();
            moment
                .AddTo(section, "Group", () => group != null ? Members.GroupName(group) : staticVars.PlayerData.GetGroupName())
                .AddTo(section, "Sister group of", () => !sister ? "" : main != null ? Members.GroupName(main) : staticVars.PlayerData.GetGroupName());
            // Group sizes only where the moment changed them, before and after: "Members in Moons: 4 now, down from 5"
            foreach (KeyValuePair<Groups._group, int> change in (changes ?? new Dictionary<Groups._group, int>())
                .Where(c => c.Key != null && c.Value != 0).OrderBy(c => c.Key == group ? -1 : Groups.Groups_.IndexOf(c.Key)))
            {
                moment.AddTo(section, "Members in " + Members.GroupName(change.Key), () =>
                {
                    int now = change.Key.GetNumberOfNonGraduatedGirls();
                    return now + " now, " + (change.Value > 0 ? "up" : "down") + " from " + (now - change.Value);
                });
            }
            moment
                // The group's totals, once here rather than in every idol's line
                .AddTo(section, "Group formed", () => group != null ? History.Ago(group.Date_Created, Now) : "")
                .AddTo(section, "Singles released by the group", () => Members.ReleasedSingles(group).Count)
                .AddTo(section, "Most popular members", () => graduation || group == null ? ""
                    : string.Join(", ", Members.ByFans(group.GetGirls(true)).Take(Members.MostPopular).Select(Name).ToArray()));
            if (graduation)
            {
                // A clique can span groups: one line per group, hers first
                foreach (IGrouping<Groups._group, data_girls.girls> mates in (friends ?? new List<data_girls.girls>())
                    .GroupBy(f => GroupOf(() => f.GetGroup())).Where(g => g.Key != null)
                    .OrderBy(g => g.Key == group ? -1 : Groups.Groups_.IndexOf(g.Key)))
                {
                    moment.AddTo(section, "Friends in " + Members.GroupName(mates.Key), () => string.Join(", ", mates.Select(Name).ToArray()));
                }
            }
            moment
                // A graduation is about one idol, so the group's scandals stay out of it
                .AddTo(section, "Group reputation", () => graduation ? "" : Members.AgencyReputation(resources.GetScandalPointsTotal()))
                .AddTo(section, "Group concept", AgencyRules.Concept);
            return (rules?.Invoke(moment) ?? moment)
                .AddTo(section, "Date", () => staticVars.dateTime.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture));
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

        /// <summary>The most new idols an audition's list names before "and N more".</summary>
        public const int AuditionNames = 10;

        /// <summary>
        /// The idols hired from an audition the player launched, once its popup closes. Hires join the main
        /// group. Their audition rarity is the player's card colour, not something fans see, so it isn't sent.
        /// </summary>
        public static Moment Audition(IList<data_girls.girls> hired, Auditions.type scale)
        {
            bool one = hired.Count == 1;
            Moment moment = new Moment(one ? "a new idol joined the group" : "new idols joined the group")
                .Labelled(() => one ? Name(hired[0]) + " joining" : "the new members")
                .Add("New members", () => Names(hired, AuditionNames))
                .Add("Audition", () => Moment.Clean(Translate(ScaleKey(scale))));
            Groups._group group = MainGroup();
            Members.Add(moment, hired.Take(Members.AuditionCap).ToList(), Now, group, newcomer: true);
            History.Joined(moment, hired, data_girls.girl, Now);
            Dictionary<Groups._group, int> changes = new();
            foreach (data_girls.girls girl in hired)
            {
                Change(changes, GroupOf(() => girl.GetGroup()), 1);
            }
            return Agency(moment, group, changes: changes);
        }

        private static string ScaleKey(Auditions.type scale) => scale switch
        {
            Auditions.type.local => "AUDITION__LOCAL",
            Auditions.type.regional => "AUDITION__REGIONAL",
            Auditions.type.nationwide => "AUDITION__NATIONWIDE",
            _ => "",
        };

        /// <summary>
        /// The yearly awards show, once it's over: what the agency won, what it was nominated for and lost
        /// (with the winner the show announced), and the winners' acceptance speeches. The awards' gameplay
        /// bonuses aren't public, so they aren't sent.
        /// </summary>
        public static Moment Awards(AwardsShow show)
        {
            Moment moment = new Moment("the yearly idol awards show was held")
                .Labelled(() => "the awards show");
            moment.AddList(Moment.FactsSection, "Won",
                show.Nominations.Where(n => n.Won).Select(n => (Func<object>)(() => Award(n))).ToList());
            moment.AddList(Moment.FactsSection, "Nominated but lost",
                show.Nominations.Where(n => !n.Won).Select(n => (Func<object>)(() => Award(n) + LostTo(n))).ToList());
            moment.AddList(Moment.FactsSection, "Acceptance speeches",
                show.Speeches.Where(s => s.Won && s.Girl != null).Select(s => (Func<object>)(() =>
                    show.SpeechTexts.TryGetValue(s, out string said) ? SpeechSaid(s, said) : Speech(s))).ToList());
            // Winners first, then the idols who spoke for the agency's awards, then the nominees who lost
            List<data_girls.girls> idols = show.Nominations.Where(n => n.Won).Select(n => n.Girl)
                .Concat(show.Speeches.Where(s => s.Won).Select(s => s.Girl))
                .Concat(show.Nominations.Where(n => !n.Won).Select(n => n.Girl))
                .Where(g => g != null).Distinct().Take(Members.AwardsCap).ToList();
            Groups._group group = MainGroup();
            Members.Add(moment, idols, Now, group);
            History.Awards(moment, show, global::Awards._Awards, global::Awards._Nominations);
            return Agency(moment, group);
        }

        /// <summary>
        /// "Best Debut Idol: Kondo Tsubasa", "Best Single: \"Title\" (style)", "Most Performances": the
        /// award's title, and the idol or single it's for.
        /// </summary>
        private static string Award(AwardsShow.Nomination nomination)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(nomination.Type)));
            string subject = nomination.Girl != null ? Name(nomination.Girl)
                : nomination.Single != null ? Part(() => TitleWithStyle(nomination.Single))
                : "";
            return subject == "" ? title : title + ": " + subject;
        }

        /// <summary>
        /// ", lost to Saito Yuna of Starlight Ribbon", ", lost to \"Title\" by Starlight Ribbon", ", lost to
        /// Starlight Ribbon": the winner the show announced; ", lost" when it named none.
        /// </summary>
        private static string LostTo(AwardsShow.Nomination nomination)
        {
            string name = nomination.RivalName ?? "";
            string group = nomination.RivalGroup ?? "";
            if (name == "")
            {
                return group == "" ? ", lost" : ", lost to " + group;
            }
            if (nomination.Type == global::Awards._type.best_single)
            {
                return ", lost to \"" + name + "\"" + (group == "" ? "" : " by " + group);
            }
            return ", lost to " + name + (group == "" ? "" : " of " + group);
        }

        /// <summary>
        /// "Best Debut Idol, Kondo Tsubasa: \"Thank you so much...\"": the speech in the words the show gave.
        /// </summary>
        private static string SpeechSaid(global::Awards._speech speech, string said)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(speech.Type)));
            return title + ", " + Name(speech.Girl) + ": \"" + said + "\"";
        }

        /// <summary>
        /// "Best Debut Idol: Kondo Tsubasa thanked the group's producer (Haruto Sato)": who spoke for the award
        /// and whom she thanked, as the player chose in the speeches popup.
        /// </summary>
        private static string Speech(global::Awards._speech speech)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(speech.Type)));
            string thanked = speech.Thanks switch
            {
                global::Awards._speech._thanks.you => Producer(),
                // The game's name for him, as the speeches popup shows it
                global::Awards._speech._thanks.fujimoto => Language.Data != null && Language.Data.TryGetValue("AWARDS__THANK_FUJIMOTO", out string fujimoto)
                    ? Moment.Clean(fujimoto) : "Fujimoto",
                global::Awards._speech._thanks.staff => speech.Target_Staff == null ? "the staff"
                    : Moment.Clean(Part(() => speech.Target_Staff.GetName())) is string staffer && staffer != ""
                        ? staffer + " (" + Moment.Clean(Part(() => speech.Target_Staff.GetJobTitle())) + ")"
                        : "the staff",
                _ => speech.Target_Girl != null ? Name(speech.Target_Girl) : "",
            };
            return title + ": " + Name(speech.Girl) + (thanked == "" ? " gave an acceptance speech" : " thanked " + thanked);
        }

        /// <summary>
        /// A show's first episode, aired as the player launched it. Its budget, revenue and new fans are the
        /// agency's own numbers, so only the audience, a ratings-style figure, is sent.
        /// </summary>
        public static Moment ShowStart(Shows._show show)
        {
            List<data_girls.girls> cast = CastOf(show);
            Moment moment = ShowFacts(new Moment("a new show starring the agency's idols started airing")
                    .Labelled(() => "the show \"" + Moment.Clean(show.title) + "\""), show, cast, "first")
                .Add("First episode audience", () => show.audience.Count > 0 ? show.audience[0] + " viewers" : "");
            return ShowPeople(moment, show, cast, started: true);
        }

        /// <summary>
        /// A show that aired for a year or more, as the player's cancellation ends it. The year runs from its
        /// first episode, through any concept updates. Like its start, only audience figures are sent.
        /// </summary>
        public static Moment ShowEnd(Shows._show show)
        {
            List<data_girls.girls> cast = CastOf(show);
            Moment moment = ShowFacts(new Moment("a long-running show starring the agency's idols was cancelled")
                    .Labelled(() => "the end of the show \"" + Moment.Clean(show.title) + "\""), show, cast, "last")
                .Add("Ran for", () => History.Known(show.LaunchDate)
                    ? History.Span((Now.Date - show.LaunchDate.Date).Days) + ", " + show.episodeCount + (show.episodeCount == 1 ? " episode" : " episodes")
                    : "")
                .Add("Last episode audience", () => show.audience.Count > 0 ? show.audience[show.audience.Count - 1] + " viewers" : "")
                .Add("Best episode audience", () => show.peakAudience > 0 ? show.peakAudience + " viewers" : "")
                .Add("Concept updated", () => show.NumberOfRelaunches <= 0 ? ""
                    : show.NumberOfRelaunches + (show.NumberOfRelaunches == 1 ? " time" : " times"));
            return ShowPeople(moment, show, cast, started: false);
        }

        /// <summary>The show by the game's names, and its cast, as of its first or last episode.</summary>
        private static Moment ShowFacts(Moment moment, Shows._show show, List<data_girls.girls> cast, string episode)
        {
            return moment
                .Add("Title", () => "\"" + Moment.Clean(show.title) + "\"")
                .Add("Medium", () => show.medium?.GetTitle())
                .Add("Genre", () => show.genre?.GetTitle())
                // The host's tier as the game names it ("Seasoned"); tier 0 is the game's "No Host"
                .Add("Host", () => show.mc == null ? "" : show.mc.id == 0 ? show.mc.GetTitle() : Moment.Clean(show.mc.GetTitle()) + " host")
                .Add("Cast", () => show.castType == Shows._show._castType.entireGroup ? "the whole group"
                    : show.castType == Shows._show._castType.rotatingCast ? "a rotating cast, in the " + episode + " episode " + Names(cast, 7)
                    : Names(cast, 7));
        }

        /// <summary>The cast's lines, the agency's other shows, and the agency, for the cast's group.</summary>
        private static Moment ShowPeople(Moment moment, Shows._show show, List<data_girls.girls> cast, bool started)
        {
            bool wholeGroup = show.castType == Shows._show._castType.entireGroup;
            List<data_girls.girls> idols = (wholeGroup ? Members.ByFans(cast) : cast).Take(Members.ShowCap).ToList();
            Groups._group group = (wholeGroup || cast.Count == 0 ? null : GroupOf(() => cast[0].GetGroup())) ?? MainGroup();
            Members.Add(moment, idols, Now, group);
            History.Shows(moment, show, Shows.shows, Now, started);
            return Agency(moment, group);
        }

        private static List<data_girls.girls> CastOf(Shows._show show)
        {
            try
            {
                return show.GetCast().Where(g => g != null).Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<data_girls.girls>();
            }
        }

        /// <summary>
        /// A lasting contract, as the player accepts it: an ad or a TV drama, always one idol's. What it pays
        /// and costs her isn't public, so only who, what, for whom and how long is sent.
        /// </summary>
        /// <param name="running">The agency's running contracts, this one included.</param>
        /// <param name="history">Every deal the agency accepted before (business.History); this one isn't in it yet.</param>
        public static Moment ContractStart(business.active_proposal contract, IEnumerable<business.active_proposal> running,
            IEnumerable<business._history> history)
        {
            data_girls.girls girl = contract.Girl;
            bool tv = contract.Type == business._type.tv_drama;
            string kind = Moment.Clean(Part(() => business.GetData(contract.Type)?.GetTitle()));
            Moment moment = new Moment(tv ? "an idol was cast in a TV drama" : "an idol landed an advertising contract")
                .Labelled(() => Name(girl) + (tv ? "'s TV drama" : "'s advertising contract"))
                .Add("Idol", () => Name(girl))
                .Add("Contract", () => kind)
                .Add(tv ? "Studio or channel" : "Brand", () => Moment.Clean(contract.Agent_Name))
                .Add("Drama", () => tv && !string.IsNullOrEmpty(Moment.Clean(contract.Product_Name)) ? "\"" + Moment.Clean(contract.Product_Name) + "\"" : "")
                .Add("Image used", () => data_girls.GetParamName(contract.Skill))
                .Add("Length", () => business.GetData(contract.Type) is business._data data && data.duration > 0
                    ? data.duration + (data.duration == 1 ? " month" : " months") : "");
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group ?? MainGroup());
            History.Contracts(moment, contract, running, history);
            return Agency(moment, group);
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

        /// <param name="rankBefore">Her place by fans in her group before she left, taken before the game moved her fans.</param>
        /// <param name="friends">Her clique before she left, taken before the game took her out of it; null reads it now.</param>
        /// <param name="bulliedBy">Who was bullying her before she left, taken before the game ended it; null reads it now.</param>
        /// <param name="bullied">Who she was bullying before she left, taken before the game took her out of her clique; null reads it now.</param>
        /// <param name="successor">The idol who joined in her place from her goodbye scene, if any.</param>
        /// <param name="sister">The successor is her younger sister.</param>
        /// <param name="staffer">The staff job she took at the agency from her goodbye scene, if any.</param>
        /// <param name="notice">Whether fans knew she was leaving: she announced it, or she left at once.</param>
        /// <param name="announcedOn">The day she announced it, for an announced graduation; unknown leaves the day out.</param>
        public static Moment Graduation(data_girls.girls girl, Members.Rank rankBefore, List<data_girls.girls> friends = null,
            List<data_girls.girls> bulliedBy = null, List<data_girls.girls> bullied = null,
            data_girls.girls successor = null, bool sister = false, staff._staff staffer = null,
            GraduationNotice notice = GraduationNotice.Sudden, DateTime announcedOn = default)
        {
            Moment moment = new Moment("an idol graduated (left the group)")
                .Labelled(() => Name(girl) + "'s graduation");
            GraduateFacts(moment, girl, bulliedBy, bullied, m => m
                // How she left first, so fans write a farewell they saw coming or react to a shock
                .Add("Announced her graduation", () => notice != GraduationNotice.Announced ? ""
                    : History.Ago(announcedOn, Now) is string ago && ago != "" ? ago : "beforehand")
                .Add("How she left", () => notice switch
                {
                    GraduationNotice.Fired => "dismissed by the agency with immediate effect, with no announcement beforehand",
                    GraduationNotice.Sudden => "with immediate effect, with no announcement beforehand",
                    _ => "",
                })
                .Add("After graduating", () => Members.Cut(Moment.Clean(girl.Graduation_Trivia_Text), Members.GraduationTriviaLength))
                // Her staff job, by title and what the staffer does, as for a firing
                .Add("Stays at the agency as", () => staffer == null ? "" : Role(staffer))
                .Add("Joined in her place", () => successor == null ? "" : Successor(girl, successor, sister)));
            Dictionary<data_girls.girls, Members.Rank> ranks = new();
            if (rankBefore != null)
            {
                ranks[girl] = rankBefore;
            }
            // A graduated idol stays in her group's list, so the group is still hers
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group, ranks);
            if (successor != null)
            {
                Members.Add(moment, new List<data_girls.girls> { successor }, Now, group, newcomer: true);
            }
            History.Graduation(moment, girl, data_girls.girl, Now);
            // She left her group; her successor joined the group the game put her in, often another one
            Dictionary<Groups._group, int> changes = new();
            Change(changes, group, -1);
            if (successor != null)
            {
                Change(changes, GroupOf(() => successor.GetGroup()), 1);
            }
            return Agency(moment, group, graduation: true, friends: friends ?? Members.Friends(girl), changes: changes);
        }

        /// <summary>
        /// An idol's graduation announcement, as the game confirms it: she leaves in 3 months and is a member
        /// until then. The graduation's own facts about her, without what only exists once she has left: what
        /// she does after, her successor, her staff job, and the group's smaller size.
        /// </summary>
        public static Moment GraduationAnnouncement(data_girls.girls girl)
        {
            Moment moment = new Moment("an idol announced her graduation: she leaves the group in 3 months and stays a member until then")
                .Labelled(() => Name(girl) + "'s graduation announcement");
            GraduateFacts(moment, girl, null, null, m => m
                .Add("Graduation date", () => GraduationDate(girl.Graduation_Date)));
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group);
            History.Graduation(moment, girl, data_girls.girl, Now);
            return Agency(moment, group, graduation: true, friends: Members.Friends(girl));
        }

        /// <summary>
        /// "Sunday 21 January 2027, 3 months from now"; the date alone when it isn't ahead; "" when unknown.
        /// </summary>
        private static string GraduationDate(DateTime date)
        {
            if (!History.Known(date))
            {
                return "";
            }
            string day = date.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
            int days = (date.Date - Now.Date).Days;
            return days > 0 ? day + ", " + History.Span(days) + " from now" : day;
        }

        /// <summary>
        /// A leaving idol's facts, shared by her graduation and its announcement: her name, then the moment's
        /// own lines about how she leaves, then the bullying fans suspect and the singles she was the center of.
        /// </summary>
        private static void GraduateFacts(Moment moment, data_girls.girls girl, List<data_girls.girls> bulliedBy,
            List<data_girls.girls> bullied, Func<Moment, Moment> leaving)
        {
            bulliedBy ??= Members.BulliedBy(girl);
            bullied ??= Members.Bullied(girl);
            leaving(moment.Add("Idol", () => Name(girl)))
                // Only bullying the player has found out; fans can only suspect it
                .Add("Suspected to be bullied by", () => string.Join(", ", bulliedBy.Select(Name).ToArray()))
                .Add("Suspected to have bullied", () => string.Join(", ", bullied.Select(Name).ToArray()));
            // Her best-selling released singles as center, best first; left out when she centered none
            moment.AddList(Moment.FactsSection, "Singles she was the center of",
                singles.Singles
                    .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null && CenterOf(s) == girl)
                    .OrderByDescending(s => s.ReleaseData.Sales)
                    .Take(CenterSingles)
                    .Select(s => (Func<object>)(() => TitleWithStyle(s))).ToList());
        }

        /// <summary>
        /// Adds a gain or loss of members to a group's change; a null group is left out.
        /// </summary>
        private static void Change(Dictionary<Groups._group, int> changes, Groups._group group, int members)
        {
            if (group != null)
            {
                changes[group] = (changes.TryGetValue(group, out int before) ? before : 0) + members;
            }
        }

        /// <summary>
        /// "Uchida Hana, her younger sister" or "Sato Mio, the successor she chose", with the group she joined
        /// when it isn't the graduate's: the game puts every new idol in the main group.
        /// </summary>
        private static string Successor(data_girls.girls girl, data_girls.girls successor, bool sister)
        {
            string text = Name(successor) + (sister ? ", her younger sister" : ", the successor she chose");
            Groups._group hers = GroupOf(() => girl.GetGroup());
            Groups._group joined = GroupOf(() => successor.GetGroup());
            return joined != null && hers != null && joined != hers
                ? text + ", in " + (joined.IsMain() ? "the main group " : "") + Members.GroupName(joined)
                : text;
        }

        /// <summary>
        /// Whether any of these texts says "you" or "your", as the game's event texts do to the player.
        /// </summary>
        public static bool SpeaksToYou(params string[] texts)
        {
            return texts.Any(t => t != null && Regex.IsMatch(t, @"\byou(r|rs|rself|'re|'ll|'ve|'d)?\b", RegexOptions.IgnoreCase));
        }

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

        /// <summary>The most singles a graduate's list of center singles holds.</summary>
        public const int CenterSingles = 5;

        private static data_girls.girls CenterOf(singles._single single)
        {
            try
            {
                return single.GetCenter();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// "the group's producer (Haruto Sato)": the player, by the name the game shows; without the name when
        /// the game has none.
        /// </summary>
        public static string Producer()
        {
            string name = Moment.Clean(Part(() => staticVars.PlayerData.GetPlayerName(staticVars._playerData.name_type.full_name)));
            return "the group's producer" + (name == "" ? "" : " (" + name + ")");
        }

        private static string Part(Func<string> text)
        {
            try
            {
                return text() ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for how often this one happened before.</param>
        /// <param name="ended">The business contracts the answer ended.</param>
        public static Moment Event(Event_Manager._activeEvent active, Event_Manager._randomEvent._reply reply, IEnumerable<Event_Manager._activeEvent> log = null,
            IList<business.active_proposal> ended = null)
        {
            Moment moment = new Moment("something happened at the agency")
                // Some of the game's events have no title
                .Labelled(() => Translate(active.data?.title) is string title && title.Trim() != "" ? "the event " + title : "an event at the agency")
                .Add("What happened", () => Translate(active.data?.title))
                // The game's event texts speak to the player, who is the producer
                .Add("You", () => SpeaksToYou(Part(() => active.GetDescription()), Part(() => Translate(reply.text)), Part(() => Translate(reply.description)))
                    ? Producer() : "")
                .Add("Details", () => active.GetDescription())
                .Add("The agency's response", () => active.SwitchVariables(Translate(reply.text)))
                .Add("Outcome", () => reply.description == "-" ? "" : active.SwitchVariables(Translate(reply.description)))
                .Add("People involved", () => string.Join(", ", active.actors.Select(ActorName).Where(n => n != "").ToArray()));
            ContractsCancelled(moment, ended);
            // An event is about its main subject's group
            List<data_girls.girls> idols = Members.ForEvent(active);
            Groups._group group = idols.Count > 0 ? GroupOf(() => idols[0].GetGroup()) : null;
            Members.Add(moment, idols, Now, group ?? MainGroup());
            History.Event(moment, active, log, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group dating rule", AgencyRules.Dating));
        }

        /// <param name="reactions">The outcomes the game picked for the answer, in order.</param>
        /// <param name="ended">The business contracts the answer ended.</param>
        public static Moment DatingScandal(Event_Templates._active_template active, Event_Templates._template._reply reply,
            IList<Event_Templates._dating_reactions._type> reactions, IList<business.active_proposal> ended = null)
        {
            data_girls.girls idol = active.Actors[0].girl;
            string details = Part(() => ScandalDetails(active));
            string response = Part(() => active.SwitchVariables(reply.Text));
            string outcome = Part(() => active.SwitchVariables(string.Join(" ", reactions.Select(ReactionText).Where(t => t != "").ToArray())));
            Moment moment = new Moment("an idol's dating life became a scandal")
                .Labelled(() => Name(idol) + "'s dating scandal")
                .Add("You", () => SpeaksToYou(details, response, outcome) ? Producer() : "")
                .Add("What happened", () => details)
                .Add("The agency's response", () => response)
                .Add("Outcome", () => outcome)
                .Add("People involved", () => string.Join(", ", active.Actors.Where(a => a != null).Select(ActorName).Where(n => n != "").ToArray()));
            ContractsCancelled(moment, ended);
            Groups._group group = GroupOf(() => idol.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { idol }, Now, group ?? MainGroup());
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group dating rule", AgencyRules.Dating));
        }

        /// <param name="cleared">The scandal points the player spent on the change.</param>
        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for each one's latest scandal.</param>
        public static Moment PolicyChange(policies.value previous, policies.value value, IList<PolicyChange.Cleared> cleared,
            IEnumerable<Event_Manager._activeEvent> log = null)
        {
            string policy = Part(() => value.GetParent()?.Title);
            Payers payers = new(cleared);
            Moment moment = new Moment("the agency changed one of its policies for all groups")
                .Labelled(() => "the " + Moment.Clean(policy) + " policy change")
                .Add("Policy", () => policy)
                .Add("Changed from", () => previous?.Title)
                .Add("Changed to", () => value.Title)
                .Add("Reason for change", () => payers.Whose.Count == 0 ? "" : "scandals involving " + JoinAnd(payers.Whose));
            // Policies are agency-wide; the moment is about the first named idol's group, as an event is
            return payers.AddTo(moment, log);
        }

        /// <param name="budget">Whether the player accepted the staffer's deal to announce budget problems.</param>
        /// <param name="cleared">The scandal points the player spent on the firing.</param>
        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for each one's latest scandal.</param>
        /// <param name="contracts">The agency's running business contracts (business.ActiveProposals).</param>
        public static Moment StaffFiring(staff._staff staffer, bool budget, IList<PolicyChange.Cleared> cleared,
            IEnumerable<Event_Manager._activeEvent> log = null, IEnumerable<business.active_proposal> contracts = null)
        {
            Payers payers = new(cleared);
            string name = Moment.Clean(Part(() => staffer.GetName()));
            Moment moment = new Moment("the agency fired a staff member")
                .Labelled(() => name + "'s firing")
                .Add("Staff member", () => name)
                .Add("Role", () => Role(staffer))
                .Add("Time at the agency", () => History.Known(staffer.HireDate) && staffer.HireDate <= Now ? History.Span((Now.Date - staffer.HireDate.Date).Days) : "")
                // The staffer's deal: the agency announces budget problems instead of blaming them. The scandal
                // history below stays, so fans can doubt it.
                .Add("Announced reason", () => budget ? "budget problems (ostensibly)"
                    : payers.Whose.Count == 0 ? "blamed for the agency's scandals" : "blamed for scandals involving " + JoinAnd(payers.Whose));
            // Production staff: the agency's latest singles released while they worked there
            if (WorkOf(staffer) == staff._skill_type.production)
            {
                moment.AddList(Moment.FactsSection, "Latest singles released while they worked there",
                    singles.Singles
                        .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null
                            && s.ReleaseData.ReleaseDate >= staffer.HireDate && s.ReleaseData.ReleaseDate <= Now)
                        .OrderByDescending(s => s.ReleaseData.ReleaseDate)
                        .Take(StaffSingles)
                        .Select(s => (Func<object>)(() => StaffSingle(s))).ToList());
            }
            // Dealmakers: the agency's biggest running contracts, by weekly pay, which isn't sent
            if (WorkOf(staffer) == staff._skill_type.deals)
            {
                moment.AddList(Moment.FactsSection, "Biggest business contracts running now",
                    (contracts ?? Enumerable.Empty<business.active_proposal>())
                        .Where(c => c != null)
                        .OrderByDescending(c => c.Payment_per_week)
                        .Take(StaffContracts)
                        .Select(c => (Func<object>)(() => Contract(c))).ToList());
            }
            return payers.AddTo(moment, log);
        }

        /// <summary>
        /// "Choreographer (production staff)", "Dance Coach (trainer)": the job title, and what the staffer
        /// mostly did by their skills; the title alone for other work.
        /// </summary>
        private static string Role(staff._staff staffer)
        {
            string title = Part(() => staffer.GetJobTitle());
            string work = WorkOf(staffer) switch
            {
                staff._skill_type.production => "production staff",
                staff._skill_type.coaching => "trainer",
                staff._skill_type.deals => "dealmaker",
                staff._skill_type.physical_health => "physical health care",
                staff._skill_type.mental_health => "mental health care",
                staff._skill_type.cute_pretty => "cute and pretty styling",
                staff._skill_type.cool_sexy => "cool and sexy styling",
                _ => "",
            };
            return work == "" ? title : title + " (" + work + ")";
        }

        /// <summary>
        /// The skill a staffer is strongest in, as the game levels them: a dance coach can grow into
        /// production work, a production manager into deals. On a tie, the role's main skill (primary). none
        /// without skills.
        /// </summary>
        internal static staff._skill_type WorkOf(staff._staff staffer)
        {
            try
            {
                staff._staff._skill top = staffer.skills?.Where(k => k != null)
                    .OrderByDescending(k => k.level).ThenByDescending(k => k.primary).FirstOrDefault();
                return top?.skill_type ?? staff._skill_type.none;
            }
            catch (Exception)
            {
                return staff._skill_type.none;
            }
        }

        /// <summary>The most singles a fired staffer's list holds.</summary>
        public const int StaffSingles = 3;

        /// <summary>The most business contracts a fired dealmaker's list holds.</summary>
        public const int StaffContracts = 3;

        /// <summary>
        /// "Advertisement for Sparkle Soda from Hoshi Beverages, with Takeda Rin, 8 weeks left": the kind's full
        /// title from the game's business data, or its short one.
        /// </summary>
        /// <summary>
        /// The business contracts an answer ended, each by kind, client and idol, as a list; left out when it
        /// ended none.
        /// </summary>
        private static void ContractsCancelled(Moment moment, IList<business.active_proposal> ended)
        {
            moment.AddList(Moment.FactsSection, "Contracts cancelled because of this",
                (ended ?? new List<business.active_proposal>()).Select(c => (Func<object>)(() => Contract(c, weeksLeft: false))).ToList());
        }

        internal static string Contract(business.active_proposal contract, bool weeksLeft = true)
        {
            string kind = Moment.Clean(Part(() => business.GetData(contract.Type)?.GetTitle()));
            if (kind == "")
            {
                kind = Moment.Clean(Part(() => business.GetTypeTitle(contract.Type)));
            }
            string product = Moment.Clean(contract.Product_Name);
            string client = Moment.Clean(contract.Agent_Name);
            string who = contract.isGroup ? "the whole group" : contract.Girl != null ? Name(contract.Girl) : "";
            int weeks = !weeksLeft ? -1 : Part(() => contract.GetWeeksLeft().ToString(CultureInfo.InvariantCulture)) is string w && int.TryParse(w, out int n) ? n : -1;
            return kind + (product == "" ? "" : " for " + product) + (client == "" ? "" : " from " + client)
                + (who == "" ? "" : ", with " + who) + (weeks < 0 ? "" : ", " + weeks + (weeks == 1 ? " week" : " weeks") + " left");
        }

        /// <summary>"\"Title\" (Pop, Love lyrics, Cool choreography) by Aozora Chapter, released 2 months ago".</summary>
        private static string StaffSingle(singles._single single)
        {
            Groups._group group = History.GroupOf(single);
            string ago = History.Ago(single.ReleaseData.ReleaseDate, Now);
            return TitleWithStyle(single) + (group == null ? "" : " by " + Members.GroupName(group)) + (ago == "" ? "" : ", released " + ago);
        }

        /// <summary>
        /// The idols and the group whose scandal points the player spent, for a paid policy change or a
        /// firing: their friends and known bullying (as for a graduation), their lines, their latest scandals,
        /// and the moment's group, the first idol's.
        /// </summary>
        private sealed class Payers
        {
            public readonly List<data_girls.girls> Idols;
            public readonly bool Group;
            /// <summary>"Takeda Rin", ..., "the group itself", for the reason line.</summary>
            public readonly List<string> Whose;

            public Payers(IList<PolicyChange.Cleared> cleared)
            {
                Idols = (cleared ?? new List<PolicyChange.Cleared>()).Where(c => !c.Group && c.Girl != null)
                    .Select(c => c.Girl).Distinct().Take(Members.PolicyCap).ToList();
                Group = cleared != null && cleared.Any(c => c.Group);
                Whose = Idols.Select(Name).ToList();
                if (Group)
                {
                    Whose.Add("the group itself");
                }
            }

            public Moment AddTo(Moment moment, IEnumerable<Event_Manager._activeEvent> log)
            {
                // Known friends and bullying, as for a graduation; named per idol when there are several
                foreach (data_girls.girls girl in Idols)
                {
                    string who = Idols.Count == 1 ? "" : Name(girl) + ": ";
                    string Label(string text) => who == "" ? char.ToUpperInvariant(text[0]) + text.Substring(1) : who + text;
                    // A clique can span groups: one line per group, hers first
                    Groups._group own = GroupOf(() => girl.GetGroup());
                    foreach (IGrouping<Groups._group, data_girls.girls> mates in Members.Friends(girl)
                        .GroupBy(f => GroupOf(() => f.GetGroup())).Where(g => g.Key != null)
                        .OrderBy(g => g.Key == own ? -1 : Groups.Groups_.IndexOf(g.Key)))
                    {
                        moment.Add(Label("friends in " + Members.GroupName(mates.Key)), () => string.Join(", ", mates.Select(Name).ToArray()));
                    }
                    moment
                        .Add(Label("suspected to be bullied by"), () => string.Join(", ", Members.BulliedBy(girl).Select(Name).ToArray()))
                        .Add(Label("suspected to have bullied"), () => string.Join(", ", Members.Bullied(girl).Select(Name).ToArray()));
                }
                Groups._group about = Idols.Count > 0 ? GroupOf(() => Idols[0].GetGroup()) : null;
                Members.Add(moment, Idols, Now, about ?? MainGroup());
                History.ScandalsPaidFor(moment, Idols, Group, log, Now);
                return Agency(moment, about);
            }
        }

        /// <summary>
        /// The scandal as the game describes it (_active_template.GetDescription), without the question to the
        /// player that follows the last part: the formula is cut after its last [part].
        /// </summary>
        public static string ScandalDetails(Event_Templates._active_template active)
        {
            string text = active.Template.Formula ?? "";
            int end = text.LastIndexOf(']');
            if (end >= 0)
            {
                text = text.Substring(0, end + 1);
            }
            foreach (Event_Templates._template._part part in active.Template.Parts)
            {
                Event_Templates._template._part._value chosen = active.Parts.FirstOrDefault(v => v != null && v.Parent == part);
                text = text.Replace("[" + part.ID + "]", chosen?.Text ?? "");
            }
            return active.SwitchVariables(text);
        }

        /// <summary>The game's text for an outcome (template_reactions.json), without its colour.</summary>
        private static string ReactionText(Event_Templates._dating_reactions._type reaction)
        {
            return Event_Templates.Dating_Reactions.FirstOrDefault(r => r != null && r.Type == reaction)?.Text ?? "";
        }
    }
}
