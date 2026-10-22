using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    /// <summary>
    /// This year's awards show as it ended: the game clears its nominations right after (Reset_After_Awards),
    /// so they're copied first. The rival winners are the names the show announced for awards the agency
    /// lost, from the game's variables (Awards.SetVariables).
    /// </summary>
    public sealed class AwardsShow
    {
        public sealed class Nomination
        {
            public Awards._type Type;
            public data_girls.girls Girl;
            public singles._single Single;
            public bool Won;
            /// <summary>The winner the show announced when the agency lost: a person or a single's title; "" for none.</summary>
            public string RivalName = "";
            /// <summary>The rival winner's group; "" for none.</summary>
            public string RivalGroup = "";
        }

        public int Year;
        public readonly List<Nomination> Nominations = new();
        public readonly List<Awards._speech> Speeches = new();
        /// <summary>Each speech as the show gave it, where it was heard.</summary>
        public readonly Dictionary<Awards._speech, string> SpeechTexts = new();

        // The speeches' parts as the show builds them (vn_actions.DoCustom), until the show ends
        private static readonly Dictionary<Awards._speech, List<string>> heard = new();

        /// <summary>The game's dialogue text with its variables filled in; tests replace it.</summary>
        internal static Func<string, string> Parse = text => ActiveDialogueController.ParseText(text, richText: false);

        /// <summary>The show's custom steps that build a speech: an intro, then the thanks.</summary>
        public static bool IsSpeechPart(string formula) =>
            formula == "awards_solo_intro" || formula == "awards_solo_thanks" || formula == "awards_group_intro" || formula == "awards_group_thanks";

        /// <summary>
        /// A part of a speech as the show just built it, with the game's variables filled in as the dialogue
        /// shows them ([groupname], [player:polite]).
        /// </summary>
        public static void Heard(Awards._speech speech, string text)
        {
            if (speech == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            string line = Moment.Clean(Parse(text));
            if (line == "")
            {
                return;
            }
            if (!heard.TryGetValue(speech, out List<string> parts))
            {
                parts = new List<string>();
                heard[speech] = parts;
            }
            parts.Add(line);
        }

        public static void ClearHeard() => heard.Clear();

        // The game's variables for each award's announced winner: the name (or the single's title) and its group.
        // The agency's awards have only a group, in the first key.
        private static readonly Dictionary<Awards._type, string[]> RivalKeys = new()
        {
            [Awards._type.most_performances] = new[] { null, "most_performances" },
            [Awards._type.most_promotions] = new[] { null, "most_promotions" },
            [Awards._type.best_concert] = new[] { null, "best_concert" },
            [Awards._type.most_prolific_group] = new[] { null, "most_prolific_group" },
            [Awards._type.best_employer] = new[] { null, "best_employer" },
            [Awards._type.gravure_queen] = new[] { "gravure_queen", "gravure_queen_group" },
            [Awards._type.fashion_icon] = new[] { "fashion_icon", "fashion_icon_group" },
            [Awards._type.variety_queen] = new[] { "top_entertainer", "top_entertainer_group" },
            [Awards._type.best_debut_idol] = new[] { "best_debut_idol", "best_debut_idol_group" },
            [Awards._type.best_single] = new[] { "best_single", "best_single_group" },
        };

        /// <summary>
        /// The show from the game's lists, read before they're cleared; null without nominations.
        /// </summary>
        public static AwardsShow Read()
        {
            List<Awards._award> nominations = (Awards.TempNominations ?? new List<Awards._award>()).Where(n => n != null).ToList();
            if (nominations.Count == 0)
            {
                return null;
            }
            AwardsShow show = new() { Year = nominations[0].Year };
            foreach (Awards._award award in nominations)
            {
                Nomination nomination = new()
                {
                    Type = award.Type,
                    Girl = award.Girl,
                    Single = award.Single,
                    Won = Awards._Awards.Contains(award),
                };
                if (!nomination.Won && RivalKeys.TryGetValue(award.Type, out string[] keys))
                {
                    nomination.RivalName = keys[0] == null ? "" : Variable(keys[0]);
                    nomination.RivalGroup = Variable(keys[1]);
                }
                show.Nominations.Add(nomination);
            }
            show.Speeches.AddRange((Awards.Speeches ?? new List<Awards._speech>()).Where(s => s != null));
            foreach (Awards._speech speech in show.Speeches)
            {
                if (heard.TryGetValue(speech, out List<string> parts))
                {
                    show.SpeechTexts[speech] = string.Join(" ", parts.ToArray());
                }
            }
            heard.Clear();
            return show;
        }

        private static string Variable(string name)
        {
            try
            {
                return Moment.Clean(FeedStore.GetVariable(variables.variable, name) ?? "");
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
