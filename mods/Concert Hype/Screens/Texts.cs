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
            ["CONCERTHYPE__TAB_SONG"] = "Song",
            ["CONCERTHYPE__ALL_GROUPS"] = "All Groups",
            ["CONCERTHYPE__BACKSTAGE"] = "Backstage",
            ["CONCERTHYPE__TALK_BREAK"] = "Talk Break ({0} left)",
            ["CONCERTHYPE__TALK_NOT_YET"] = "Talk breaks can start after the first song.",
            ["CONCERTHYPE__TALK_NONE_LEFT"] = "No talk breaks left in this show.",
            ["CONCERTHYPE__TALK_DONE"] = "One talk break between songs; the next can come after this song.",
            ["CONCERTHYPE__START_TALK"] = "Start Talk Break",
            ["CONCERTHYPE__RESET_LINEUP"] = "Reset Lineup",
            ["CONCERTHYPE__TIP_ORIGINAL"] = "Original: her own place in the single, where she gets the senbatsu bonus.",
            ["CONCERTHYPE__TIP_SUB"] = "Substitute: performs in another idol's place, without the senbatsu bonus.",
            ["CONCERTHYPE__TIP_HURT"] = "Hurt: out for {0}. A talk break counts as one song.",
            ["CONCERTHYPE__TIP_RESTING"] = "Exhausted: resting until her energy is back to {0}%.",
            ["CONCERTHYPE__TIP_ENERGY_PLACE"] = "Energy {0}%. This song uses about {1}.",
            ["CONCERTHYPE__TIP_ENERGY_HOST"] = "Energy {0}%. Hosts don't recover.",
            ["CONCERTHYPE__TIP_ENERGY_BENCH"] = "Energy {0}%. Recovers {1} per song sat out.",
            ["CONCERTHYPE__TIP_SUCCESS"] = "Success {0}% (critical {1}%). Critical failure {2}%.",
            ["CONCERTHYPE__CANT_LEAVE"] = "{0} can't just leave: drag an idol from the bench onto her place.",
            ["CONCERTHYPE__CANT_PERFORM"] = "{0} can't perform right now.",
            ["CONCERTHYPE__NOT_ALLOWED"] = "That change isn't allowed in this song.",
            ["CONCERTHYPE__CLICK_DRAG"] = "Drag {0} onto a place.",
            ["CONCERTHYPE__CLICK_DRAG_HOST"] = "Drag {0} onto a talk break place.",
            ["CONCERTHYPE__EXPECTED_TITLE"] = "Expected Hype:",
            ["CONCERTHYPE__GAP"] = "-{0}%",
            ["CONCERTHYPE__GAP_TIP"] = "Empty place: costs about {0}% hype.",
            ["CONCERTHYPE__BAND_LOW"] = "a changed lineup surprises the crowd",
            ["CONCERTHYPE__BAND_MID"] = "the crowd is close to 100%",
            ["CONCERTHYPE__BAND_HIGH"] = "the crowd wants the hits",
            ["CONCERTHYPE__AFTER_TALK_TITLE"] = "After Talk Break:",
            ["CONCERTHYPE__VENUE_TITLE"] = "Venue:",
            ["CONCERTHYPE__PERFORMANCE_TITLE"] = "Performance:",
            ["CONCERTHYPE__PRODUCTION_TITLE"] = "Production:",
            ["CONCERTHYPE__BARS_LOST"] = "lost",
            ["CONCERTHYPE__BARS_ADDED"] = "added",
            ["CONCERTHYPE__LIKELY"] = "likely {0} to {1}",
            ["CONCERTHYPE__TOTAL_TIP"] = "Likely {0} to {1}, depending on how the idols perform.",
            ["CONCERTHYPE__LAST_SONG_TIP"] = "Last song: {0}.",
            ["CONCERTHYPE__LAST_TALK_TIP"] = "Last talk break: {0}.",
            ["CONCERTHYPE__VENUE_TIP"] = "{0} of {1} seats filled ({2}%).",
            ["CONCERTHYPE__VENUE_TIP_SHARE"] = "The venue is {0}% full.",
            ["CONCERTHYPE__VENUE_SOLD_OUT"] = "Sold out: every song gains {0} hype.",
            ["CONCERTHYPE__VENUE_ALMOST"] = "Sell the last seats for +{0} hype every song.",
            ["CONCERTHYPE__VENUE_EMPTY"] = "Empty seats cost every song {0} hype.",
            ["CONCERTHYPE__PERF_CENTER"] = "Center: {0}% success",
            ["CONCERTHYPE__PERF_ROW"] = "Row {0}: {1}% success",
            ["CONCERTHYPE__PERF_EMPTY_ONE"] = "1 empty place",
            ["CONCERTHYPE__PERF_EMPTY"] = "{0} empty places",
            ["CONCERTHYPE__PERF_WEIGHTS"] = "Front rows and idols with more fans count most.",
            ["CONCERTHYPE__PROD_QUALITY"] = "Production quality {0}% (this venue needs {1}%): {2}",
            ["CONCERTHYPE__PROD_APPEAL"] = "Song's appeal to these idols' fans: {0}",
            ["CONCERTHYPE__PROD_CHANGED"] = "Changed lineup {0}%: {1}",
            ["CONCERTHYPE__PROD_MOOD"] = "Hype {0}%: {1}.",
            ["CONCERTHYPE__PROD_FLOW"] = "Same {0} as the last song: {1}",
            ["CONCERTHYPE__PROD_NO_FLOW"] = "Nothing in common with the last song: {0}",
            ["CONCERTHYPE__FLOW_GENRE"] = "genre",
            ["CONCERTHYPE__FLOW_LYRICS"] = "lyrics",
            ["CONCERTHYPE__FLOW_CHOREO"] = "choreography",
            ["CONCERTHYPE__AND"] = "and",
            ["CONCERTHYPE__TALK_TOTAL_TIP"] = "Hosts' talk {0}%: about {1}% hype after.",
            ["CONCERTHYPE__BAR_SONG"] = "Hype {0}%. After this song: about {1}%.",
            ["CONCERTHYPE__BAR_TALK"] = "Hype {0}%. After the talk break: about {1}%.",
            ["CONCERTHYPE__BAR_NOW"] = "Hype {0}%.",
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
            ["CONCERTHYPE__OUTCOME_TIP"] = "{0}: {1}",
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
