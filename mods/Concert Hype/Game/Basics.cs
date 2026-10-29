using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConcertHype
{
    internal static class Log
    {
        private const string Prefix = "[Concert Hype] ";
        private static readonly HashSet<string> Logged = new HashSet<string>();

        public static void Info(string message) => Debug.Log(Prefix + message);

        public static void Warn(string message) => WarnSink(Prefix + message);

        /// <summary>
        /// Where warnings go: the game's log; the unit tests, which run without Unity, catch them instead.
        /// </summary>
        internal static Action<string> WarnSink = message => Debug.LogWarning(message);

        public static void Error(string message) => Debug.LogError(Prefix + message);

        public static void WarnOnce(string key, string message)
        {
            if (Logged.Add(key))
                Warn(message);
        }
    }

    /// <summary>
    /// The mod menu's switch. On when ModMenus isn't installed.
    /// </summary>
    public static class Switch
    {
        public const string VarId = "ConcertHype_Enabled";

        public static bool Enabled
        {
            get
            {
                try
                {
                    return variables.Get(VarId) != "0";
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }
    }

    public static class ModFolder
    {
        public const string Title = "Concert Hype";

        private const string InfoFile = "info.json";

        /// <summary>
        /// The mod's folder (its info.json beside it): the DLL's own folder, or the loaded mod with this title. Null if neither.
        /// </summary>
        public static string Find()
        {
            try
            {
                string own = Path.GetDirectoryName(typeof(ModFolder).Assembly.Location);
                if (!string.IsNullOrEmpty(own) && File.Exists(Path.Combine(own, InfoFile)))
                    return own;
                foreach (Mods._mod mod in Mods._Mods)
                {
                    if (mod.Title == Title && File.Exists(Path.Combine(mod.GetPath(), InfoFile)))
                        return mod.GetPath();
                }
            }
            catch (Exception ex)
            {
                Log.WarnOnce("folder", "couldn't look for the mod's folder: " + ex.Message);
            }
            return null;
        }
    }
}
