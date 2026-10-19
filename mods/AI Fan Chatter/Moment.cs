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

        public Moment(string kind)
        {
            Kind = kind;
            foreach (string section in Sections)
            {
                sections[section] = new List<KeyValuePair<string, string>>();
            }
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
        /// <param name="graduation">The moment is a graduation, so the member count is after she left.</param>
        /// <param name="friends">A graduate's clique, named by group in place of the most popular members.</param>
        /// <param name="rules">Adds the agency rules that matter to this kind of moment, after the concept.</param>
        private static Moment Agency(Moment moment, Groups._group group, bool graduation = false,
            List<data_girls.girls> friends = null, Func<Moment, Moment> rules = null)
        {
            const string section = Moment.AgencySection;
            Groups._group main = MainGroup();
            group ??= main;
            bool sister = group != null && !group.IsMain();
            moment
                .AddTo(section, "Group", () => group != null ? Members.GroupName(group) : staticVars.PlayerData.GetGroupName())
                .AddTo(section, "Sister group of", () => !sister ? "" : main != null ? Members.GroupName(main) : staticVars.PlayerData.GetGroupName())
                .AddTo(section, graduation ? "Members left in the group" : "Members in the group", () => group?.GetNumberOfNonGraduatedGirls())
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
                .AddTo(section, "Group reputation", () => Members.AgencyReputation(resources.GetScandalPointsTotal()))
                .AddTo(section, "Group concept", AgencyRules.Concept);
            return (rules?.Invoke(moment) ?? moment)
                .AddTo(section, "Date", () => staticVars.dateTime.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture));
        }

        public static Moment Single(singles._single single)
        {
            Moment moment = new Moment("the group released a new single")
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
                .Add("Marketing", () => Marketing(single));
            Groups._group group = History.GroupOf(single);
            Members.Add(moment, Members.ForSingle(single), Now, group);
            History.Single(moment, single, singles.Singles, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group handshake event security", () => AgencyRules.HandshakeSecurity(single)));
        }

        public static Moment Concert(SEvent_Concerts._concert concert)
        {
            Moment moment = new Moment("the group held a concert")
                .Add("Venue", () => SEvent_Concerts.GetVenueTitle(concert.Venue))
                .Add("Attendance", () => Attendance(concert))
                // The game's hype runs from 0 to 200 (Concert_Popup.AddHype) and shows as a percentage
                .Add("Hype", () => concert.ProjectedValues.Actual_Hype + "%")
                .Add("Ticket price", () => concert.ProjectedValues.TicketPrice + " yen");
            // The set list names who performed. Unnumbered, so the songs are named by title rather than by their place in the list
            moment.AddList(Moment.FactsSection, "Set list, in order",
                (concert.SetListItems ?? new List<SEvent_Concerts._concert.ISetlistItem>())
                .Select(item => (Func<object>)(() => SetListItem(item))).ToList());
            // The game's concerts are the main group's
            Groups._group group = MainGroup();
            Members.Add(moment, Members.ForConcert(concert), Now, group);
            History.Concert(moment, concert, SEvent_Concerts.Concerts, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Stage costumes", AgencyRules.Costumes)
                .AddTo(Moment.AgencySection, "Performance style", AgencyRules.PerformanceStyle));
        }

        /// <param name="rankBefore">Her place by fans in her group before she left, taken before the game moved her fans.</param>
        /// <param name="friends">Her clique before she left, taken before the game took her out of it; null reads it now.</param>
        public static Moment Graduation(data_girls.girls girl, Members.Rank rankBefore, List<data_girls.girls> friends = null)
        {
            Moment moment = new Moment("an idol graduated (left the group)")
                .Add("Idol", () => Name(girl))
                .Add("After graduating", () => Members.Cut(Moment.Clean(girl.Graduation_Trivia_Text), Members.GraduationTriviaLength));
            // Her best-selling released singles as center, best first; left out when she centered none
            moment.AddList(Moment.FactsSection, "Singles she was the center of, best-selling first",
                singles.Singles
                    .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null && CenterOf(s) == girl)
                    .OrderByDescending(s => s.ReleaseData.Sales)
                    .Take(CenterSingles)
                    .Select(s => (Func<object>)(() => TitleWithStyle(s))).ToList());
            Dictionary<data_girls.girls, Members.Rank> ranks = new();
            if (rankBefore != null)
            {
                ranks[girl] = rankBefore;
            }
            // A graduated idol stays in her group's list, so the group is still hers
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group, ranks);
            History.Graduation(moment, girl, data_girls.girl, Now);
            return Agency(moment, group, graduation: true, friends: friends ?? Members.Friends(girl));
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
        /// The share of seats filled, then the people who came out of the venue's capacity: "92% (4600/5000)".
        /// Without the capacity when the game can't give it.
        /// </summary>
        public static string Attendance(SEvent_Concerts._concert concert)
        {
            string capacity = Part(() => concert.GetCapacity().ToString(CultureInfo.InvariantCulture));
            return Math.Round(concert.ProjectedValues.Actual_Attendance * 100) + "% (" + concert.ProjectedValues.Actual_Audience
                + (capacity == "" ? "" : "/" + capacity) + ")";
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
        public static Moment Event(Event_Manager._activeEvent active, Event_Manager._randomEvent._reply reply, IEnumerable<Event_Manager._activeEvent> log = null)
        {
            Moment moment = new Moment("something happened at the agency")
                .Add("What happened", () => Translate(active.data?.title))
                // The game's event texts speak to the player, who is the producer
                .Add("You", () => SpeaksToYou(Part(() => active.GetDescription()), Part(() => Translate(reply.text)), Part(() => Translate(reply.description)))
                    ? Producer() : "")
                .Add("Details", () => active.GetDescription())
                .Add("The agency's response", () => active.SwitchVariables(Translate(reply.text)))
                .Add("Outcome", () => reply.description == "-" ? "" : active.SwitchVariables(Translate(reply.description)))
                .Add("People involved", () => string.Join(", ", active.actors.Select(ActorName).Where(n => n != "").ToArray()));
            // An event is about its main subject's group
            List<data_girls.girls> idols = Members.ForEvent(active);
            Groups._group group = idols.Count > 0 ? GroupOf(() => idols[0].GetGroup()) : null;
            Members.Add(moment, idols, Now, group ?? MainGroup());
            History.Event(moment, active, log, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group dating rule", AgencyRules.Dating));
        }
    }
}
