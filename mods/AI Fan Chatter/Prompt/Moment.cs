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
}
