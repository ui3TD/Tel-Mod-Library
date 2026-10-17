using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace AIFanChatter
{
    /// <summary>
    /// The API keys, one per provider, in plain text files outside the saves and the Mods folder:
    /// %USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\ModConfig\AI Fan Chatter\. Saves can be
    /// shared or synced and the Mods folder is uploaded and replaced by updates; this folder is neither,
    /// and one key works for every save.
    /// </summary>
    public static class KeyStore
    {
        /// <summary>
        /// The folder the keys are kept in. Tests point it somewhere else.
        /// </summary>
        internal static string FolderOverride = null;

        private static readonly Dictionary<Provider, string> Cache = new();

        public static string Folder => FolderOverride ?? GameFolder();

        // Apart, so that compiling Folder never reaches the engine's native code (which tests can't run)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameFolder() => FolderUnder(Application.persistentDataPath);

        /// <summary>
        /// The key folder in the game's data folder, which also holds the saves (data) and the mods (Mods).
        /// </summary>
        internal static string FolderUnder(string gameDataFolder) => Path.Combine(Path.Combine(gameDataFolder, "ModConfig"), "AI Fan Chatter");

        internal static string FileFor(Provider provider) => Path.Combine(Folder, provider.ToString().ToLowerInvariant() + ".key");

        /// <summary>
        /// The provider's saved key, or null when there is none or it can't be read.
        /// </summary>
        public static string Get(Provider provider)
        {
            if (Cache.TryGetValue(provider, out string key))
            {
                return key;
            }
            string error = null;
            try
            {
                string file = FileFor(provider);
                key = File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8).Trim() : "";
            }
            catch (Exception e)
            {
                error = e.Message;
                key = "";
            }
            key = key == "" ? null : key;
            // Cached before logging: the log redacts keys, which reads them again
            Cache[provider] = key;
            if (error != null)
            {
                Log.Warn("Couldn't read the saved API key: " + error);
            }
            return key;
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

        public static void Delete(Provider provider)
        {
            Cache.Remove(provider);
            try
            {
                string file = FileFor(provider);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't remove the saved API key: " + e.Message);
            }
        }

        internal static void ClearCache() => Cache.Clear();

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
            foreach (Provider provider in new[] { Provider.Anthropic, Provider.OpenAI, Provider.Compatible })
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
                if (variable != null && variable.name == Settings.ApiKeyVar && !string.IsNullOrEmpty(variable.value))
                {
                    Apply(provider, variable.value);
                    variable.value = "";
                    found = true;
                }
            }
            return found;
        }
    }

    /// <summary>
    /// The mod's log lines. Tests replace the sink, since Unity's logger needs the engine.
    /// </summary>
    internal static class Log
    {
        internal static Action<string> Sink = message => Debug.LogWarning(message);

        public static void Warn(string message)
        {
            try
            {
                Sink("[AI Fan Chatter] " + KeyStore.Redact(message));
            }
            catch
            {
                // Logging must never break the game
            }
        }
    }
}
