using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace ModMenus
{
    /// <summary>
    /// The settings of the tabs whose "Use in all saves" box is ticked, kept in one file beside the saves:
    /// %USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\data\modmenus_settings.txt. Steam Cloud syncs
    /// everything in that folder, so the settings follow the player to other computers. Rows with "password": true
    /// are never written here.
    /// </summary>
    /// <remarks>
    /// Plain text rather than JSON: the game's JSON reader drops empty strings, so an empty text field would read
    /// back as missing. Each tab is a "[tab]" line, then a "varID=value" line for each setting; a backslash, a
    /// line break or a carriage return in a name or value is written as \\, \n or \r.
    /// </remarks>
    public static class SharedSettings
    {
        public const string FileName = "modmenus_settings.txt";

        private const string Heading = "# Mod Settings: the settings of the tabs whose \"Use in all saves\" box is ticked. Steam Cloud syncs this file.";

        /// <summary>
        /// The folder the file is kept in. Tests point it somewhere else.
        /// </summary>
        internal static string FolderOverride = null;

        /// <summary>
        /// The last problem logged, so a file that keeps failing the same way is logged once.
        /// </summary>
        private static string lastProblem;

        public static string Folder => FolderOverride ?? GameFolder();

        public static string FilePath => Path.Combine(Folder, FileName);

        // Apart, so that compiling Folder never reaches the engine's native code (which tests can't run)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameFolder() => Path.Combine(Application.persistentDataPath, "data");

        /// <summary>
        /// Each ticked tab's settings, by tab (see ModMenuScopeRow.tabID) and then by varID. Read again each
        /// time, so a file Steam Cloud synced while the game was closed is used. A missing file means no tab is
        /// ticked; so does one that can't be read, which is logged.
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> Load()
        {
            try
            {
                string path = FilePath;
                return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new Dictionary<string, Dictionary<string, string>>();
            }
            catch (Exception e)
            {
                LogOnce("[ModMenus] The settings used in all saves couldn't be read, so each save uses its own: " + e.Message);
                return new Dictionary<string, Dictionary<string, string>>();
            }
        }

        /// <summary>
        /// Every shared setting, whichever tab it's on.
        /// </summary>
        public static Dictionary<string, string> Values(Dictionary<string, Dictionary<string, string>> tabs)
        {
            Dictionary<string, string> values = new();
            foreach (KeyValuePair<string, Dictionary<string, string>> tab in tabs.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                foreach (KeyValuePair<string, string> setting in tab.Value)
                {
                    values[setting.Key] = setting.Value;
                }
            }
            return values;
        }

        /// <summary>
        /// Writes the ticked tabs' settings, if they differ from the file's. The new file is written beside the
        /// old one first and then takes its place, so a crash can't leave half a file. A failure is logged.
        /// </summary>
        public static void Save(Dictionary<string, Dictionary<string, string>> tabs)
        {
            try
            {
                string path = FilePath;
                bool exists = File.Exists(path);
                string text = Write(tabs);
                if (exists ? File.ReadAllText(path, Encoding.UTF8) == text : tabs.Count == 0)
                {
                    return;
                }

                Directory.CreateDirectory(Folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (exists)
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
                lastProblem = null;
            }
            catch (Exception e)
            {
                LogOnce("[ModMenus] The settings used in all saves couldn't be saved, so other saves won't get the changes: " + e.Message);
            }
        }

        internal static string Write(Dictionary<string, Dictionary<string, string>> tabs)
        {
            StringBuilder text = new();
            text.Append(Heading).Append('\n');
            foreach (KeyValuePair<string, Dictionary<string, string>> tab in tabs.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                text.Append('[').Append(Escape(tab.Key)).Append("]\n");
                foreach (KeyValuePair<string, string> setting in tab.Value.OrderBy(s => s.Key, StringComparer.Ordinal))
                {
                    text.Append(Escape(setting.Key)).Append('=').Append(Escape(setting.Value ?? "")).Append('\n');
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// Reads the file's text. Blank lines, lines starting with # and settings before the first tab are skipped.
        /// </summary>
        internal static Dictionary<string, Dictionary<string, string>> Parse(string text)
        {
            Dictionary<string, Dictionary<string, string>> tabs = new();
            Dictionary<string, string> current = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string tab = Unescape(line.Substring(1, line.Length - 2));
                    if (!tabs.TryGetValue(tab, out current))
                    {
                        tabs[tab] = current = new Dictionary<string, string>();
                    }
                    continue;
                }
                int equals = line.IndexOf('=');
                if (current == null || equals <= 0)
                {
                    continue;
                }
                current[Unescape(line.Substring(0, equals))] = Unescape(line.Substring(equals + 1));
            }
            return tabs;
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0)
            {
                return text;
            }
            StringBuilder result = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[++i];
                    result.Append(next == 'n' ? '\n' : next == 'r' ? '\r' : next);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        private static void LogOnce(string message)
        {
            if (message == lastProblem)
            {
                return;
            }
            lastProblem = message;
            Debug.LogWarning(message);
        }

        internal static void ForgetLogged() => lastProblem = null;
    }
}
