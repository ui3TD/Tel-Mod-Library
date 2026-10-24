using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace ModMenus
{
    /// <summary>
    /// The values of secret rows ("type": "secret"), such as API keys: one plain-text file each, outside the
    /// saves, the Mods folder and the folder Steam Cloud syncs:
    /// %USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\ModConfig\&lt;secretID&gt;.key. One secret
    /// serves every save. The files aren't encrypted.
    /// </summary>
    public static class SecretStore
    {
        public const string Extension = ".key";

        /// <summary>
        /// The folder the secrets are kept in. Tests point it somewhere else.
        /// </summary>
        internal static string FolderOverride = null;

        private sealed class Cached
        {
            public string Value;
            public DateTime Stamp;
        }

        /// <summary>
        /// Each secret as last read, by ID, read again when its file changes: a mod may write the file itself.
        /// </summary>
        private static readonly Dictionary<string, Cached> Cache = new(StringComparer.Ordinal);

        /// <summary>
        /// The stamp of a file that can't be checked, so the failure is cached like a read.
        /// </summary>
        private static readonly DateTime Unreadable = DateTime.MaxValue;

        public static string Folder => FolderOverride ?? GameFolder();

        // Apart, so that compiling Folder never reaches the engine's native code (which tests can't run)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameFolder() => Path.Combine(Application.persistentDataPath, "ModConfig");

        /// <summary>
        /// Whether a secretID can name a file: a name, or a folder and a name ("Tel AI Mods/anthropic"), so mods
        /// can share a folder. Each part has letters, digits, spaces, '-', '_' or '.', and doesn't start or end
        /// with a space or a dot, so it can't leave the folder or name a different file on another system.
        /// </summary>
        public static bool IsValidID(string secretID)
        {
            if (string.IsNullOrEmpty(secretID))
            {
                return false;
            }
            string[] parts = secretID.Split('/');
            if (parts.Length > 2)
            {
                return false;
            }
            foreach (string part in parts)
            {
                if (part.Length == 0 || part[0] == ' ' || part[0] == '.' || part[part.Length - 1] == ' ' || part[part.Length - 1] == '.')
                {
                    return false;
                }
                foreach (char c in part)
                {
                    if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_' && c != '.')
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// The secret's file. The ID must be valid (see IsValidID).
        /// </summary>
        public static string PathFor(string secretID)
        {
            string[] parts = secretID.Split('/');
            string folder = parts.Length == 2 ? Path.Combine(Folder, parts[0]) : Folder;
            return Path.Combine(folder, parts[parts.Length - 1] + Extension);
        }

        /// <summary>
        /// The saved secret, trimmed, or null when there is none, it can't be read, or the ID isn't valid.
        /// </summary>
        public static string Get(string secretID)
        {
            if (!IsValidID(secretID))
            {
                return null;
            }
            string file = null;
            DateTime stamp;
            try
            {
                file = PathFor(secretID);
                stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            }
            catch (Exception)
            {
                stamp = Unreadable;
            }
            if (Cache.TryGetValue(secretID, out Cached cached) && cached.Stamp == stamp)
            {
                return cached.Value;
            }
            string value;
            try
            {
                value = stamp != DateTime.MinValue ? File.ReadAllText(file, Encoding.UTF8).Trim() : "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] The secret " + secretID + " couldn't be read: " + e.Message);
                value = "";
            }
            value = value == "" ? null : value;
            Cache[secretID] = new Cached { Value = value, Stamp = stamp };
            return value;
        }

        /// <summary>
        /// Saves a secret, trimmed; an empty one removes it. Returns whether it was saved. The new file is
        /// written beside the old one first and then takes its place, so a crash can't leave half a key.
        /// </summary>
        public static bool Set(string secretID, string value)
        {
            value = (value ?? "").Trim();
            if (value == "")
            {
                return Delete(secretID);
            }
            if (!IsValidID(secretID))
            {
                return false;
            }
            Cache.Remove(secretID);
            string temp = null;
            try
            {
                string path = PathFor(secretID);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temp = path + ".tmp";
                File.WriteAllText(temp, value, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] The secret " + secretID + " couldn't be saved, so the old one is kept: " + e.Message);
                // The key isn't left in a second file
                try
                {
                    if (temp != null && File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch (Exception)
                {
                    // Nothing more can be done
                }
                return false;
            }
        }

        /// <summary>
        /// Removes a secret. Returns whether it's gone.
        /// </summary>
        public static bool Delete(string secretID)
        {
            if (!IsValidID(secretID))
            {
                return false;
            }
            Cache.Remove(secretID);
            try
            {
                string path = PathFor(secretID);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] The secret " + secretID + " couldn't be removed: " + e.Message);
                return false;
            }
        }

        internal static void ClearCache() => Cache.Clear();
    }
}
