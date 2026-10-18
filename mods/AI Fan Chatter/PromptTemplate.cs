using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    /// <summary>
    /// Fills the prompt's templates from constants.json. Each {name} is a module that gives its text; the
    /// template decides where each goes, so players can reorder, reword or drop the parts.
    /// </summary>
    public static class PromptTemplate
    {
        /// <summary>
        /// The start of a module's wrapper key: AIFANCHATTER__PROMPT__FEED wraps the feed module's lines.
        /// </summary>
        public const string WrapperPrefix = "AIFANCHATTER__PROMPT__";

        private static readonly Regex Placeholder = new(@"\{([a-z_]+)\}", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new(@"\n{3,}", RegexOptions.Compiled);

        /// <summary>
        /// The template with each known {name} replaced by its module's text. A placeholder without a module
        /// is left as typed. A module that fails gives "" and a log line. A line that held only placeholders
        /// that came out empty is dropped, and runs of blank lines collapse to one.
        /// </summary>
        public static string Fill(string template, IDictionary<string, Func<string>> modules)
        {
            if (string.IsNullOrEmpty(template))
            {
                return "";
            }
            Dictionary<string, string> values = new();
            StringBuilder sb = new();
            foreach (string line in template.Replace("\r\n", "\n").Split('\n'))
            {
                bool hadModule = false;
                string filled = Placeholder.Replace(line, match =>
                {
                    string name = match.Groups[1].Value;
                    if (modules == null || !modules.TryGetValue(name, out Func<string> module))
                    {
                        return match.Value;
                    }
                    hadModule = true;
                    if (!values.TryGetValue(name, out string value))
                    {
                        value = Run(name, module);
                        values[name] = value;
                    }
                    return value;
                });
                if (hadModule && filled.Trim() == "")
                {
                    continue;
                }
                sb.Append(filled).Append('\n');
            }
            return BlankLines.Replace(sb.ToString(), "\n\n").Trim();
        }

        private static string Run(string name, Func<string> module)
        {
            try
            {
                return module() ?? "";
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't build the " + name + " part of the prompt; it's left out: " + e.Message);
                return "";
            }
        }

        /// <summary>
        /// A module's lines in its wrapper from constants.json, at {lines}; just the lines when it has no
        /// wrapper, and "" when there are no lines, so a header never stands on its own.
        /// </summary>
        public static string Wrap(string module, string lines)
        {
            if (string.IsNullOrEmpty(lines) || lines.Trim() == "")
            {
                return "";
            }
            if (!Prompt.TryText(WrapperPrefix + module.ToUpperInvariant(), out string wrapper))
            {
                return lines;
            }
            return wrapper.Contains("{lines}") ? wrapper.Replace("{lines}", lines) : wrapper + "\n" + lines;
        }
    }
}
