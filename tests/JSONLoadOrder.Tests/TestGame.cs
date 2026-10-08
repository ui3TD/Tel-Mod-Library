using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

// Every test shares the game's static mod list.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JSONLoadOrder.Tests
{
    /// <summary>
    /// Builds the game's loaded mod list from real mod folders with an info.json each, in a temp directory.
    /// </summary>
    public static class TestGame
    {
        public const string ModNamespace = "JSONLoadOrder";

        private static string modsDir;

        /// <summary>
        /// Empties the game's mod list, the mod's remembered orders and the enabled/disabled settings,
        /// and gives the test a fresh Mods folder.
        /// </summary>
        public static void Reset()
        {
            Mods._Mods = new List<Mods._mod>();
            ModLoadOrder.modOrders.Clear();
            staticVars.Settings = new staticVars._settings();

            if (modsDir != null && Directory.Exists(modsDir))
                Directory.Delete(modsDir, true);
            modsDir = Path.Combine(Path.GetTempPath(), "JSONLoadOrder.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(modsDir);
        }

        /// <summary>
        /// Adds a mod to the end of the game's list (as Mods.LoadMod does), with this exact info.json text.
        /// </summary>
        public static Mods._mod ModWithInfo(string name, string infoJson)
        {
            string dir = Path.Combine(modsDir, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "info.json"), infoJson);

            Mods._mod mod = new() { ModName = name, Path = dir };
            Mods._Mods.Add(mod);
            return mod;
        }

        /// <summary>
        /// Adds a mod whose info.json has this JSONLoadOrder value, written as raw JSON
        /// (e.g. "100", "\"100\"", "1.5"), or no JSONLoadOrder line when null.
        /// </summary>
        public static Mods._mod Mod(string name, string order = null)
        {
            string orderLine = order == null ? "" : $"\t\"JSONLoadOrder\": {order},\n";
            return ModWithInfo(name, "{\n\t\"Title\": \"" + name + "\",\n" + orderLine + "\t\"HarmonyID\": \"test." + name + "\"\n}");
        }

        public static void Disable(Mods._mod mod) => staticVars.Settings.SwitchModStatus(mod.ModName);

        /// <summary>
        /// Runs the mod's prefix, as the game does at the start of every Language._Load.
        /// </summary>
        public static void LoadLanguage() => Language__Load.Prefix();

        public static List<string> LoadedNames() => Mods._Mods.Select(m => m.ModName).ToList();

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "JSON Load Order", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));

        public static string Guide() => File.ReadAllText(Path.Combine(RepoRoot(), "docs", "JSONLoadOrder.md"));
    }
}
