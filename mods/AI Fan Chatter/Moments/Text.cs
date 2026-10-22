using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    /// <summary>
    /// The facts for each kind of moment, read from the game.
    /// </summary>
    public static partial class MomentContext
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

        private static DateTime Now => staticVars.dateTime;

        /// <summary>
        /// "Sunday 21 January 2027, 3 months from now"; the date alone when it isn't ahead; "" when unknown.
        /// </summary>
        private static string DateAhead(DateTime date)
        {
            if (!History.Known(date))
            {
                return "";
            }
            string day = LongDate(date);
            int days = (date.Date - Now.Date).Days;
            return days > 0 ? day + ", " + History.Span(days) + " from now" : day;
        }

        /// <summary>
        /// Whether any of these texts says "you" or "your", as the game's event texts do to the player.
        /// </summary>
        public static bool SpeaksToYou(params string[] texts)
        {
            return texts.Any(t => t != null && Regex.IsMatch(t, @"\byou(r|rs|rself|'re|'ll|'ve|'d)?\b", RegexOptions.IgnoreCase));
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

        /// <summary>A text the game gives; "" when it can't, so one failed read leaves out only that part.</summary>
        internal static string Part(Func<string> text)
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

        /// <summary>"Sunday 14 September 2025": a date as the prompts write it.</summary>
        internal static string LongDate(DateTime date) => date.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
    }
}
