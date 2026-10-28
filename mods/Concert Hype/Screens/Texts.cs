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
            ["CONCERTHYPE__TALK_DROP"] = "Drag an idol here to host a talk break before this song.",
            ["CONCERTHYPE__TALK_PULL"] = "{0} pulls hype to about {1}%. Drag her away to cancel.",
            ["CONCERTHYPE__TALK_NOT_YET"] = "Talk breaks can start after the first song.",
            ["CONCERTHYPE__TALK_NONE_LEFT"] = "No talk breaks left in this show.",
            ["CONCERTHYPE__TALK_DONE"] = "One talk break between songs; the next can come after this song.",
            ["CONCERTHYPE__START_TALK"] = "Start Talk Break",
            ["CONCERTHYPE__RESET_LINEUP"] = "Reset Lineup",
            ["CONCERTHYPE__HINT_DRAG"] = "Drag an idol from the bench on the right onto a place. One change per song; filling the place of an idol who can't perform is free.\n\u2605 original  \u21C4 substitute  ! fill  \u2716 empty",
            ["CONCERTHYPE__NO_SWAP"] = "Idols can't swap places: drag an idol from the bench instead.",
            ["CONCERTHYPE__CANT_LEAVE"] = "{0} can't just leave: drag an idol from the bench onto her place.",
            ["CONCERTHYPE__OWN_PLACE"] = "{0} has her own place in this song.",
            ["CONCERTHYPE__CANT_PERFORM"] = "{0} can't perform right now.",
            ["CONCERTHYPE__NOT_ALLOWED"] = "That change isn't allowed in this song.",
            ["CONCERTHYPE__CLICK_DRAG"] = "Drag {0} onto a place, or onto the talk break.",
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
