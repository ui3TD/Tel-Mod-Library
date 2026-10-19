using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace AICore
{
    /// <summary>
    /// The API keys, one per provider, in plain text files outside the saves and the Mods folder:
    /// %USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\ModConfig\Tel AI Mods\. Saves can be
    /// shared or synced and the Mods folder is uploaded and replaced by updates; this folder is neither.
    /// One key works for every save and every one of Tel's AI mods.
    /// </summary>
    public static class KeyStore
    {
        public const string SharedFolderName = "Tel AI Mods";

        /// <summary>
        /// The folder the keys are kept in. Tests point it somewhere else.
        /// </summary>
        internal static string FolderOverride = null;

        /// <summary>
        /// The mod's old key folder while FolderOverride is set; null for none, so tests never reach the player's keys.
        /// </summary>
        internal static string LegacyFolderOverride = null;

        private sealed class CachedKey
        {
            public string Key;
            public DateTime Stamp;
        }

        /// <summary>
        /// Each mod has its own copy of this class, so a key is read again when its file changes: another
        /// AI mod may have saved a new one.
        /// </summary>
        private static readonly Dictionary<Provider, CachedKey> Cache = new();

        /// <summary>
        /// The providers whose key was looked for in the old folder already, so a failed move is tried and
        /// logged once.
        /// </summary>
        private static readonly HashSet<Provider> LegacyChecked = new();

        /// <summary>
        /// The stamp of a key file that can't be checked, so the failure is cached like a read.
        /// </summary>
        private static readonly DateTime Unreadable = DateTime.MaxValue;

        public static string Folder => FolderOverride ?? GameFolder();

        private static string LegacyFolder => FolderOverride != null ? LegacyFolderOverride : GameLegacyFolder();

        // Apart, so that compiling Folder never reaches the engine's native code (which tests can't run)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameFolder() => FolderUnder(Application.persistentDataPath);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameLegacyFolder() => LegacyFolderUnder(Application.persistentDataPath);

        /// <summary>
        /// The key folder in the game's data folder, which also holds the saves (data) and the mods (Mods).
        /// </summary>
        internal static string FolderUnder(string gameDataFolder) => Path.Combine(Path.Combine(gameDataFolder, "ModConfig"), SharedFolderName);

        /// <summary>
        /// The mod's own key folder from before the shared one; null when it never had one.
        /// </summary>
        internal static string LegacyFolderUnder(string gameDataFolder)
        {
            string name = AIMod.LegacyKeyFolder;
            return name == null ? null : Path.Combine(Path.Combine(gameDataFolder, "ModConfig"), name);
        }

        internal static string FileFor(Provider provider) => Path.Combine(Folder, FileName(provider));

        private static string FileName(Provider provider) => provider.ToString().ToLowerInvariant() + ".key";

        /// <summary>
        /// The provider's saved key, or null when there is none or it can't be read.
        /// </summary>
        public static string Get(Provider provider)
        {
            string file = null;
            DateTime stamp;
            try
            {
                file = FileFor(provider);
                MoveLegacyKey(provider, file);
                stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
            }
            catch (Exception)
            {
                stamp = Unreadable;
            }
            if (Cache.TryGetValue(provider, out CachedKey cached) && cached.Stamp == stamp)
            {
                return cached.Key;
            }
            string key;
            string error = null;
            try
            {
                key = stamp != DateTime.MinValue ? File.ReadAllText(file, Encoding.UTF8).Trim() : "";
            }
            catch (Exception e)
            {
                error = e.Message;
                key = "";
            }
            key = key == "" ? null : key;
            // Cached before logging: the log redacts keys, which reads them again
            Cache[provider] = new CachedKey { Key = key, Stamp = stamp };
            if (error != null)
            {
                Log.Warn("Couldn't read the saved API key: " + error);
            }
            return key;
        }

        /// <summary>
        /// Moves every key from the mod's old folder to the shared one now, so other AI mods find them even
        /// while this mod is off. Called once the game starts; reading a key does it too.
        /// </summary>
        public static void MoveLegacyKeys()
        {
            foreach (Provider provider in (Provider[])Enum.GetValues(typeof(Provider)))
            {
                try
                {
                    MoveLegacyKey(provider, FileFor(provider));
                }
                catch (Exception e)
                {
                    Log.Warn("Couldn't move the saved API key to the shared key folder: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Moves a key from the mod's old folder to the shared one, when the shared one has none for the
        /// provider. The old file is deleted, so the key isn't kept in two places, and so is the old folder
        /// once it's empty. A key already in the shared folder wins and the old one is left alone.
        /// </summary>
        private static void MoveLegacyKey(Provider provider, string file)
        {
            if (!LegacyChecked.Add(provider))
            {
                return;
            }
            string legacyFolder = LegacyFolder;
            if (legacyFolder == null || File.Exists(file))
            {
                return;
            }
            string legacyFile = Path.Combine(legacyFolder, FileName(provider));
            try
            {
                if (!File.Exists(legacyFile))
                {
                    return;
                }
                string key = File.ReadAllText(legacyFile, Encoding.UTF8).Trim();
                if (key != "")
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, key, new UTF8Encoding(false));
                }
                File.Delete(legacyFile);
                if (Directory.GetFileSystemEntries(legacyFolder).Length == 0)
                {
                    Directory.Delete(legacyFolder);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't move the saved API key to the shared key folder: " + e.Message);
            }
        }

        public static void Set(Provider provider, string key)
        {
            key = (key ?? "").Trim();
            if (key == "")
            {
                Delete(provider);
                return;
            }
            Cache.Remove(provider);
            LegacyChecked.Add(provider);
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FileFor(provider), key, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't save the API key: " + e.Message);
            }
        }

        /// <summary>
        /// Removes the provider's key, also from the mod's old folder, where it would otherwise be moved back.
        /// </summary>
        public static void Delete(Provider provider)
        {
            Cache.Remove(provider);
            LegacyChecked.Add(provider);
            try
            {
                string file = FileFor(provider);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                string legacyFolder = LegacyFolder;
                string legacyFile = legacyFolder == null ? null : Path.Combine(legacyFolder, FileName(provider));
                if (legacyFile != null && File.Exists(legacyFile))
                {
                    File.Delete(legacyFile);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't remove the saved API key: " + e.Message);
            }
        }

        internal static void ClearCache()
        {
            Cache.Clear();
            LegacyChecked.Clear();
        }

        /// <summary>
        /// Text with every saved key replaced by ***, for logs and notifications. A service may echo the key
        /// back in an error message.
        /// </summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            foreach (Provider provider in (Provider[])Enum.GetValues(typeof(Provider)))
            {
                string key = Get(provider);
                if (key != null && key.Length >= 4)
                {
                    text = text.Replace(key, "***");
                }
            }
            return text;
        }

        /// <summary>
        /// Handles a value typed into the key field, as ModMenus' Apply saves it: a key is saved for the
        /// provider, "-" removes that provider's key, and blank keeps it. Returns whether anything changed.
        /// The caller saves "" in the key's variable, so the key never reaches the save.
        /// </summary>
        public static bool Apply(Provider provider, string typed)
        {
            string value = (typed ?? "").Trim();
            if (value == "")
            {
                return false;
            }
            if (value == "-")
            {
                bool had = Get(provider) != null;
                Delete(provider);
                return had;
            }
            if (value == Get(provider))
            {
                return false;
            }
            Set(provider, value);
            return true;
        }

        /// <summary>
        /// Moves a key that reached the variables list some other way than variables.Set into the key file,
        /// and blanks it. Returns whether one was found.
        /// </summary>
        public static bool Scrub(List<variables._variable> list, Provider provider)
        {
            bool found = false;
            if (list == null)
            {
                return false;
            }
            foreach (variables._variable variable in list)
            {
                if (variable != null && variable.name == ConnectionSettings.ApiKeyVar && !string.IsNullOrEmpty(variable.value))
                {
                    Apply(provider, variable.value);
                    variable.value = "";
                    found = true;
                }
            }
            return found;
        }
    }
}
