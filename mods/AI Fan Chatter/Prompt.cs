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
        public const int MaxLength = 200;

        public static readonly string[] FanCodes =
        {
            "M_C_T", "M_C_YA", "M_C_A", "M_HC_T", "M_HC_YA", "M_HC_A",
            "F_C_T", "F_C_YA", "F_C_A", "F_HC_T", "F_HC_YA", "F_HC_A"
        };

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

        public static string StyleLine(Style style)
        {
            switch (style)
            {
                case Style.Supportive:
                    return "The fans are warm and encouraging.";
                case Style.Spicy:
                    return "The fans are opinionated: hot takes, rivalries and mild drama are welcome, but no harassment or insults aimed at the idols.";
                default:
                    return "Mix excitement and opinions with the odd mild complaint, as real fans would.";
            }
        }

        /// <param name="handles">The game's display name for each fan code, where it has one.</param>
        public static string System(string language, Style style, IDictionary<string, string> handles)
        {
            StringBuilder sb = new();
            sb.AppendLine("You write fan posts for the social media feed of an idol management game. The agency, its idols and its fans are fictional.");
            sb.AppendLine();
            sb.AppendLine("Fan accounts. Use these exact codes as \"user\":");
            foreach (string code in FanCodes)
            {
                sb.Append("- ").Append(code).Append(": ").Append(Describe(code));
                if (handles != null && handles.TryGetValue(code, out string handle) && !string.IsNullOrEmpty(handle))
                {
                    sb.Append(" (shown as @").Append(handle).Append(')');
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Write " + MinPosts + " to " + MaxPosts + " posts reacting to the event the user describes. Each post can have 0 to " + MaxReplies + " replies from other fans.");
            sb.AppendLine("- Keep every post and reply under " + MaxLength + " characters, in the voice of a fan online.");
            sb.AppendLine("- Mix the kinds of fans, and let them react to each other in the replies.");
            sb.AppendLine("- Stay consistent with the facts given and don't invent numbers.");
            sb.AppendLine("- Don't mention real people or brands. Nothing sexual or hateful.");
            sb.AppendLine("- Use plain text without emoji: the game can't show them.");
            sb.AppendLine("- Write in " + language + ".");
            sb.AppendLine("- " + StyleLine(style));
            sb.AppendLine();
            sb.AppendLine("Reply with only this JSON and nothing else:");
            sb.Append("{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"...\",\"replies\":[{\"user\":\"M_C_A\",\"text\":\"...\"}]}]}");
            return sb.ToString();
        }

        public const string TestSystem = "Reply with the single word OK.";
        public const string TestUser = "Connection test.";
    }
}
