using System;
using System.Collections.Generic;
using System.Text;

namespace AIFanChatter
{
    /// <summary>
    /// What the model is asked. The fan codes are the ones the game's feed draws: gender, casual or
    /// hardcore, and age group pick the avatar and the background colour (SNS_Manager._message).
    /// </summary>
    public static class Prompt
    {
        public const int MinPosts = 3;
        public const int MaxPosts = 6;
        public const int MaxReplies = 3;
        public const int MaxLength = 100;

        public static readonly string[] FanCodes =
        {
            "M_C_T", "M_C_YA", "M_C_A", "M_HC_T", "M_HC_YA", "M_HC_A",
            "F_C_T", "F_C_YA", "F_C_A", "F_HC_T", "F_HC_YA", "F_HC_A"
        };

        /// <summary>
        /// The feed's code for one of the game's fan types (resources._fan): M_HC_YA for devoted young adult men.
        /// </summary>
        public static string CodeFor(resources.fanType gender, resources.fanType hardcoreness, resources.fanType age)
        {
            string g = gender == resources.fanType.male ? "M" : "F";
            string h = hardcoreness == resources.fanType.hardcore ? "HC" : "C";
            string a = age == resources.fanType.teen ? "T" : age == resources.fanType.youngAdult ? "YA" : "A";
            return g + "_" + h + "_" + a;
        }

        /// <summary>
        /// The game's opinion (0 to 1, a half when it has none) as a whole number out of 100.
        /// </summary>
        public static int OpinionOutOf100(float opinion)
        {
            if (float.IsNaN(opinion))
            {
                return 50;
            }
            return (int)Math.Round(Math.Max(0f, Math.Min(1f, opinion)) * 100f);
        }

        public static string Describe(string code)
        {
            string hardcore = code.Contains("_HC_") ? "devoted" : "casual";
            string age = code.EndsWith("_T") ? "teen" : code.EndsWith("_YA") ? "young adult" : "adult";
            string gender = code.StartsWith("M") ? (age == "teen" ? "boy" : "man") : (age == "teen" ? "girl" : "woman");
            return hardcore + " " + age + " " + gender;
        }

        /// <summary>
        /// The language the game is set to, by its code in staticVars.Settings.Language. An unknown code
        /// (from a translation mod) is passed on as written.
        /// </summary>
        public static string LanguageName(string code)
        {
            switch (code)
            {
                case null:
                case "":
                case "en": return "English";
                case "jp": return "Japanese";
                case "cn": return "Simplified Chinese";
                case "ptbr": return "Brazilian Portuguese";
                case "ru": return "Russian";
                default: return code;
            }
        }

        // The prompt's wording is in constants.json, so players can change it (or a mod can replace it) without
        // rebuilding. The fan codes and the reply format stay here: the feed and PostParser depend on them.
        public const string IntroKey = "AIFANCHATTER__PROMPT__INTRO";
        public const string RulesKey = "AIFANCHATTER__PROMPT__RULES";

        /// <summary>
        /// A text of the prompt from the game's loaded constants; "" with a log line when it's missing.
        /// </summary>
        internal static string Text(string key)
        {
            if (Language.Data != null && Language.Data.TryGetValue(key, out string text) && text != null)
            {
                return text.Trim();
            }
            Log.Warn("No text " + key + " in constants.json; that part of the prompt is left out.");
            return "";
        }

        /// <summary>
        /// The rules text with its placeholders filled in: {min}, {max}, {replies}, {length}, {language}.
        /// </summary>
        public static string Rules(string language)
        {
            return Text(RulesKey)
                .Replace("{min}", MinPosts.ToString())
                .Replace("{max}", MaxPosts.ToString())
                .Replace("{replies}", MaxReplies.ToString())
                .Replace("{length}", MaxLength.ToString())
                .Replace("{language}", language);
        }

        /// <param name="handles">The game's display name for each fan code, where it has one.</param>
        /// <param name="opinions">Each fan code's opinion of the group in the game, 0 to 100, where the game has one.</param>
        public static string System(string language, IDictionary<string, string> handles, IDictionary<string, int> opinions)
        {
            StringBuilder sb = new();
            string intro = Text(IntroKey);
            if (intro != "")
            {
                sb.AppendLine(intro);
                sb.AppendLine();
            }
            sb.AppendLine("Fan accounts. Use these exact codes as \"user\":");
            foreach (string code in FanCodes)
            {
                sb.Append("- ").Append(code).Append(": ").Append(Describe(code));
                if (handles != null && handles.TryGetValue(code, out string handle) && !string.IsNullOrEmpty(handle))
                {
                    sb.Append(" (shown as @").Append(handle).Append(')');
                }
                if (opinions != null && opinions.TryGetValue(code, out int opinion))
                {
                    sb.Append(", opinion of the group: ").Append(opinion).Append("/100");
                }
                sb.AppendLine();
            }
            string rules = Rules(language);
            if (rules != "")
            {
                sb.AppendLine();
                sb.AppendLine(rules);
            }
            sb.AppendLine();
            sb.AppendLine("Reply with only this JSON and nothing else:");
            sb.Append("{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"...\",\"replies\":[{\"user\":\"M_C_A\",\"text\":\"...\"}]}]}");
            return sb.ToString();
        }

        public const string TestSystem = "Reply with the single word OK.";
        public const string TestUser = "Connection test.";
    }
}
