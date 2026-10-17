using SimpleJSON;
using System;
using System.Globalization;
using System.Text;

namespace AIFanChatter
{
    /// <summary>
    /// Writes JSON text. The game's SimpleJSON can't: it quotes every number and doesn't escape control
    /// characters. SimpleJSON is only used to read.
    /// </summary>
    internal static class Json
    {
        public static string Quote(string text)
        {
            StringBuilder sb = new((text?.Length ?? 0) + 2);
            sb.Append('"');
            foreach (char c in text ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses with the game's SimpleJSON, which throws on some broken input; null then.
        /// </summary>
        public static JSONNode TryParse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            try
            {
                return JSON.Parse(text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A string member of an object, or "" when it's missing or not an object. SimpleJSON returns a
        /// placeholder for missing members, and its == null is overloaded, so this checks the real type.
        /// </summary>
        public static string Str(JSONNode node, string key)
        {
            if (node is not JSONClass obj)
            {
                return "";
            }
            JSONNode member = obj[key];
            return member is JSONData ? member.Value ?? "" : "";
        }

        public static JSONClass Obj(JSONNode node, string key) => node is JSONClass obj ? obj[key] as JSONClass : null;

        public static JSONArray Arr(JSONNode node, string key) => node is JSONClass obj ? obj[key] as JSONArray : null;
    }
}
