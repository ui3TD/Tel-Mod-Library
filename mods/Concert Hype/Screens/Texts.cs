using System.Collections.Generic;

namespace ConcertHype
{
    /// <summary>
    /// The show's on-screen text: the game's language entry when there is one (constants.json), else the English here.
    /// </summary>
    public static class Texts
    {
        public static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["CONCERTHYPE__MODMENU__ENABLED"] = "New concert minigame (off: original concerts)",
            ["CONCERTHYPE__TALK_BREAK"] = "Talk Break ({0} left)",
            ["CONCERTHYPE__RESET_LINEUP"] = "Reset Lineup",
            ["CONCERTHYPE__HINT_PLACE"] = "Click a place to change who performs it, then an idol on the right. One change per song; empty places must be filled.\n\u2605 original  \u21C4 substitute  ! fill  \u2716 empty",
            ["CONCERTHYPE__HINT_PICK"] = "Now click an idol on the right to put her in that place, or the place again to cancel.",
            ["CONCERTHYPE__HINT_HOST"] = "Click an idol on the right to host the talk break, or the button again to cancel.",
            ["CONCERTHYPE__HYPE"] = "Hype {0}% · {1}",
            ["CONCERTHYPE__BAND_LOW"] = "a changed lineup surprises the crowd",
            ["CONCERTHYPE__BAND_MID"] = "the crowd is close to 100%",
            ["CONCERTHYPE__BAND_HIGH"] = "the crowd wants the hits",
            ["CONCERTHYPE__EXPECTED"] = "Changed lineup {0}% · expected hype {1} ({2} to {3})",
            ["CONCERTHYPE__LAST_SONG"] = "Last song: {0} hype",
            ["CONCERTHYPE__HURT"] = "{0} is hurt and out for {1}",
            ["CONCERTHYPE__EXHAUSTED"] = "{0} is exhausted",
            ["CONCERTHYPE__TALK_RESULT"] = "Talk break by {0}: hype {1}% to {2}%",
            ["CONCERTHYPE__BADGE_ORIGINAL"] = "\u2605",
            ["CONCERTHYPE__BADGE_SUB"] = "\u21C4",
            ["CONCERTHYPE__BADGE_FILL"] = "!",
            ["CONCERTHYPE__BADGE_EMPTY"] = "\u2716",
            ["CONCERTHYPE__OUT"] = "\u271A{0}",
            ["CONCERTHYPE__RESTING"] = "Zz",
            ["CONCERTHYPE__SONGS"] = "{0} songs",
            ["CONCERTHYPE__SONG"] = "1 song",
            ["CONCERTHYPE__SCORE"] = "Show score: {0}% (average {1}%, best {2}%, finale {3}%)",
        };

        public static string Get(string key)
        {
            if (Language.Data != null && Language.Data.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
                return text;
            return English.TryGetValue(key, out string english) ? english : key;
        }

        public static string Format(string key, params object[] values)
        {
            try
            {
                return string.Format(Get(key), values);
            }
            catch (System.FormatException)
            {
                return string.Format(English[key], values);
            }
        }
    }
}
