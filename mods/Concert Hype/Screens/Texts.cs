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
            ["CONCERTHYPE__TALK_BREAK"] = "Talk Break ({0} left):",
            ["CONCERTHYPE__TALK_NOT_YET"] = "Talk breaks can start after the first song.",
            ["CONCERTHYPE__TALK_NONE_LEFT"] = "No talk breaks left in this show.",
            ["CONCERTHYPE__TALK_DONE"] = "One talk break between songs; the next can come after this song.",
            ["CONCERTHYPE__START_TALK"] = "Start Talk Break",
            ["CONCERTHYPE__RESET_LINEUP"] = "Reset Lineup",
            ["CONCERTHYPE__AFTER_TALK"] = "This lineup plays after the talk break.",
            ["CONCERTHYPE__TIP_ORIGINAL"] = "Original: her own place in the single, where she gets the senbatsu bonus.",
            ["CONCERTHYPE__TIP_SUB"] = "Substitute: performs in another idol's place, without the senbatsu bonus.",
            ["CONCERTHYPE__TIP_HOSTING"] = "{0} hosts the talk break first and is back here for the song.",
            ["CONCERTHYPE__TIP_HURT"] = "Hurt: out for {0}. A talk break counts as one song.",
            ["CONCERTHYPE__TIP_RESTING"] = "Exhausted: resting until her energy is back to {0}%.",
            ["CONCERTHYPE__CANT_LEAVE"] = "{0} can't just leave: drag an idol from the bench onto her place.",
            ["CONCERTHYPE__CANT_PERFORM"] = "{0} can't perform right now.",
            ["CONCERTHYPE__NOT_ALLOWED"] = "That change isn't allowed in this song.",
            ["CONCERTHYPE__CLICK_DRAG"] = "Drag {0} onto a place, or onto the talk break.",
            ["CONCERTHYPE__EXPECTED_TITLE"] = "Expected Hype:",
            ["CONCERTHYPE__EXPECTED_TIP"] = "Likely {0} to {1}, depending on how the idols perform. Hype {2}%: {3}.",
            ["CONCERTHYPE__BAND_LOW"] = "a changed lineup surprises the crowd",
            ["CONCERTHYPE__BAND_MID"] = "the crowd is close to 100%",
            ["CONCERTHYPE__BAND_HIGH"] = "the crowd wants the hits",
            ["CONCERTHYPE__AFTER_TALK_TITLE"] = "After Talk Break:",
            ["CONCERTHYPE__AFTER_TALK_TIP"] = "The hype expected after the talk break. The song plays after it.",
            ["CONCERTHYPE__CHANGED_TITLE"] = "Changed Lineup:",
            ["CONCERTHYPE__CHANGED_TIP"] = "The share of places not held by their own idol from the single; the center counts most. The more changed, the more the crowd's mood raises or lowers the hype change.",
            ["CONCERTHYPE__LAST_SONG_TITLE"] = "Last Song:",
            ["CONCERTHYPE__LAST_TALK_TITLE"] = "Last Talk Break:",
            ["CONCERTHYPE__BADGE_ORIGINAL"] = "\u2605",
            ["CONCERTHYPE__BADGE_SUB"] = "\u21C4",
            ["CONCERTHYPE__OUT"] = "\u271A{0}",
            ["CONCERTHYPE__RESTING"] = "Zz",
            ["CONCERTHYPE__SONGS"] = "{0} songs",
            ["CONCERTHYPE__SONG"] = "1 song",
            ["CONCERTHYPE__SCORE_TITLE"] = "Show Score:",
            ["CONCERTHYPE__SCORE_TIP"] = "Average {0}%, best {1}%, finale {2}%.",
            ["CONCERTHYPE__CRIT_FAIL"] = "Critical Failure",
            ["CONCERTHYPE__FAIL"] = "Failure",
            ["CONCERTHYPE__SUCCESS"] = "Success",
            ["CONCERTHYPE__CRIT_SUCCESS"] = "Critical Success",
            ["CONCERTHYPE__ROLL_TIP"] = "{0}: {1}, with a roll of {2} (expected {3}). Her chances were {4}% critical failure, {5}% failure, {6}% success and {7}% critical success.",
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
