using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HarmonyChecker
{
    /// <summary>
    /// This mod's constants.json changes the game's Mods label (MM_MODS) to say IM-HI isn't installed, in every
    /// language. Once this DLL runs, IM-HI is installed, so the label goes back to what the game would show
    /// without this mod: the player's language, or a translation mod's label.
    /// </summary>
    internal static class ModsLabel
    {
        // Replaced in tests.
        internal static Func<IEnumerable<string>> ConstantsFiles = () => GameConstantsFiles();
        internal static Func<string, bool> IsOwnFile = path => IsUnder(path, OwnFolders());
        internal static Func<string, string> ReadFile = path => File.Exists(path) ? File.ReadAllText(path) : null;

        public static void Restore()
        {
            string label = WithoutThisMod(ConstantsFiles(), IsOwnFile, ReadFile);
            if (label != null)
            {
                Language.Data[HarmonyCheckerStatus.GAME_LABEL] = label;
            }
        }

        /// <summary>
        /// The Mods label from the files before this mod's, if this mod's file is the last one that sets it (as
        /// the game loads them, the last one wins); otherwise null, since a later mod's label is the one shown.
        /// A file that is missing (null text) is skipped, as the game skips it.
        /// </summary>
        public static string WithoutThisMod(IEnumerable<string> files, Func<string, bool> isOwn, Func<string, string> read)
        {
            string others = null;
            bool ownIsLast = false;
            foreach (string file in files)
            {
                string text = read(file);
                if (text == null)
                {
                    continue;
                }

                foreach (JSONNode constant in mainScript.ProcessInboundData(text).AsArray)
                {
                    if (constant["id"].Value != HarmonyCheckerStatus.GAME_LABEL)
                    {
                        continue;
                    }

                    ownIsLast = isOwn(file);
                    if (!ownIsLast)
                    {
                        others = constant["text"].Value;
                    }
                }
            }
            return ownIsLast ? others : null;
        }

        /// <summary>The constants files in the order Language._Load reads them: English first, then the language's and the mods'.</summary>
        private static IEnumerable<string> GameConstantsFiles()
        {
            string english = Path.Combine(Application.streamingAssetsPath, "Languages", "en", "JSON", "Constants", "constants.json");
            List<string> files = Mods.GetFilePaths(Path.Combine("JSON", "Constants", "constants.json"));
            if (!files.Contains(english))
            {
                files.Insert(0, english);
            }
            return files;
        }

        /// <summary>The folder of this DLL and of every mod with this mod's title.</summary>
        private static IEnumerable<string> OwnFolders() =>
            new[] { Path.GetDirectoryName(typeof(ModsLabel).Assembly.Location) }
                .Concat(Mods._Mods.Where(mod => mod != null && mod.Title == ModPicture.ModTitle).Select(mod => mod.Path));

        public static bool IsUnder(string path, IEnumerable<string> folders)
        {
            string file = Normalize(path);
            return folders.Where(folder => !string.IsNullOrEmpty(folder))
                .Any(folder => file.StartsWith(Normalize(folder).TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
