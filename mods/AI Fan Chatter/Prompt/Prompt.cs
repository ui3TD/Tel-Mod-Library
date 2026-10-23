using AICore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// The feed's code for one of the game's fan types (resources._fan): M_HC_YA for hardcore young adult men.
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
            string hardcore = code.Contains("_HC_") ? "hardcore" : "casual";
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

        // The whole prompt is two templates in constants.json, so players can change it (or a mod can replace
        // it) without rebuilding. Each {name} in them is a module below. The fan codes and the reply format
        // are written here: the feed and PostParser depend on them.
        public const string SystemKey = PromptTemplate.WrapperPrefix + "SYSTEM";
        public const string UserKey = PromptTemplate.WrapperPrefix + "USER";

        public const string FormatLead = "Reply with only this JSON and nothing else:";
        public const string FormatExample = "{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"...\",\"replies\":[{\"user\":\"M_C_A\",\"text\":\"...\"}]}]}";

        /// <summary>
        /// The reply format with fan codes the agency has: FormatExample's when it has both, otherwise its
        /// first ones, and no reply when it has a single kind of fan.
        /// </summary>
        public static string FormatFor(IList<string> codes)
        {
            string post = codes.Contains("F_HC_T") ? "F_HC_T" : codes[0];
            string reply = codes.Contains("M_C_A") && post != "M_C_A" ? "M_C_A" : codes.FirstOrDefault(code => code != post);
            string replies = reply == null ? "" : "{\"user\":\"" + reply + "\",\"text\":\"...\"}";
            return "{\"posts\":[{\"user\":\"" + post + "\",\"text\":\"...\",\"replies\":[" + replies + "]}]}";
        }

        /// <summary>
        /// What's sent when constants.json has no system template: the parts the reply depends on.
        /// </summary>
        public const string FallbackSystem = "Fan accounts. Use these exact codes as \"user\":\n{fan_types}\n\n" + FormatLead + "\n{format}";

        /// <summary>
        /// What's sent when constants.json has no user template.
        /// </summary>
        public const string FallbackUser = "{event}\n{facts}\n{agency}\n\n{members}\n\n{history}\n\n{feed}";

        /// <summary>
        /// A text from the game's loaded constants; false when it's missing.
        /// </summary>
        internal static bool TryText(string key, out string text)
        {
            text = null;
            if (Language.Data != null && Language.Data.TryGetValue(key, out string found) && found != null && found.Trim() != "")
            {
                text = found.Trim();
                return true;
            }
            return false;
        }

        /// <summary>
        /// A template from the game's loaded constants, or the fallback with a log line when it's missing.
        /// </summary>
        internal static string Template(string key, string fallback)
        {
            if (TryText(key, out string text))
            {
                return text;
            }
            Log.Warn("No text " + key + " in constants.json; the prompt uses a plain one instead.");
            return fallback;
        }

        /// <summary>
        /// Whether the system template asks for several posts: it does through {min}, which is MinPosts. A
        /// template that says how many posts in its own words may ask for just one.
        /// </summary>
        public static bool AsksForSeveralPosts()
        {
            return MinPosts > 1 && TryText(SystemKey, out string template) && template.Contains("{min}");
        }

        /// <summary>
        /// One line per fan code given: who the fans are, the game's name for the account, and their opinion.
        /// </summary>
        public static string FanTypes(IEnumerable<string> codes, IDictionary<string, string> handles, IDictionary<string, int> opinions)
        {
            StringBuilder sb = new();
            foreach (string code in codes)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append("- ").Append(code).Append(": ").Append(Describe(code));
                if (handles != null && handles.TryGetValue(code, out string handle) && !string.IsNullOrEmpty(handle))
                {
                    sb.Append(" (shown as @").Append(handle).Append(')');
                }
                if (opinions != null && opinions.TryGetValue(code, out int opinion))
                {
                    sb.Append(", opinion of the group: ").Append(opinion).Append("%");
                }
            }
            return sb.ToString();
        }

        /// <param name="codes">The fan codes the agency has fans of; only these are listed.</param>
        /// <param name="handles">The game's display name for each fan code, where it has one.</param>
        /// <param name="opinions">Each fan code's opinion of the group in the game, 0 to 100, where the game has one.</param>
        public static string System(string language, IList<string> codes, IDictionary<string, string> handles, IDictionary<string, int> opinions)
        {
            string template = Template(SystemKey, FallbackSystem);
            if (!template.Contains("{format}"))
            {
                // PostParser needs the reply in this format, whatever the template says
                Log.Warn(SystemKey + " has no {format}; the reply format is added at the end.");
                template += "\n\n" + FormatLead + "\n{format}";
            }
            return PromptTemplate.Fill(template, new Dictionary<string, Func<string>>
            {
                ["fan_types"] = () => FanTypes(codes, handles, opinions),
                ["format"] = () => FormatFor(codes),
                ["min"] = () => MinPosts.ToString(),
                ["max"] = () => MaxPosts.ToString(),
                ["replies"] = () => MaxReplies.ToString(),
                ["length"] = () => MaxLength.ToString(),
                ["language"] = () => language
            });
        }

        /// <summary>
        /// The user message: the moment's sections, each in its wrapper where constants.json has one, and
        /// the recent posts on the feed.
        /// </summary>
        public static string User(Moment moment, IList<SNS_Manager._message> posts)
        {
            Dictionary<string, Func<string>> modules = new()
            {
                ["event"] = () => "Event: " + moment.Kind,
                ["feed"] = () => PromptTemplate.Wrap("feed", FeedHistory.Describe(posts, moment.FeedPosts))
            };
            foreach (string section in moment.SectionNames)
            {
                string name = section;
                modules[name] = () => PromptTemplate.Wrap(name, moment.Lines(name));
            }
            return PromptTemplate.Fill(Template(UserKey, FallbackUser), modules);
        }

        /// <summary>
        /// The reply's JSON schema, for services that hold the model to one. Every object lists all of its
        /// properties as required and allows no others, as OpenAI's strict mode and Anthropic both need; the
        /// replies are written out rather than referenced, for services that copy OpenAI's format. Lengths
        /// and counts can't be set in a schema, so PostParser still enforces them. The fan codes are the
        /// ones given: the kinds of fans the agency has.
        /// </summary>
        public static string ReplySchemaFor(IEnumerable<string> fanCodes)
        {
            string codes = string.Join(",", fanCodes.Select(Json.Quote).ToArray());
            string user = "\"user\":{\"type\":\"string\",\"enum\":[" + codes + "]}";
            string text = "\"text\":{\"type\":\"string\"}";
            string reply = "{\"type\":\"object\",\"properties\":{" + user + "," + text + "},\"required\":[\"user\",\"text\"],\"additionalProperties\":false}";
            string post = "{\"type\":\"object\",\"properties\":{" + user + "," + text + ",\"replies\":{\"type\":\"array\",\"items\":" + reply + "}},"
                + "\"required\":[\"user\",\"text\",\"replies\"],\"additionalProperties\":false}";
            return "{\"type\":\"object\",\"properties\":{\"posts\":{\"type\":\"array\",\"items\":" + post + "}},\"required\":[\"posts\"],\"additionalProperties\":false}";
        }

        public const string TestSystem = "Reply with the single word OK.";
        public const string TestUser = "Connection test.";
    }
}
