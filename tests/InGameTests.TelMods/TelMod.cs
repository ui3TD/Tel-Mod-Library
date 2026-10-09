using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace InGameTests.TelMods
{
    /// <summary>
    /// A mod project in this repo, read from its .csproj at run time so the checks always
    /// compare against the current checkout, plus where the game has it installed and loaded.
    /// </summary>
    internal sealed class TelMod
    {
        private static readonly Regex HarmonyIdInInfo = new("\"HarmonyID\"\\s*:\\s*\"([^\"]+)\"");

        private static List<TelMod> all;

        /// <summary>The deployed folder name and info.json title.</summary>
        public string Name;
        public string HarmonyId;
        public string Version;
        public string ProjectDir;
        public string ProjectFile;

        public static string RepoRoot => typeof(TelMod).Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RepoRoot").Value;

        public static string LocalModsDir => Path.GetFullPath(Path.Combine(
            UnityEngine.Application.persistentDataPath, "Mods"));

        /// <summary>Every mods\*\*.csproj that defines a HarmonyID.</summary>
        public static List<TelMod> All()
        {
            if (all != null)
                return all;

            string modsDir = Path.Combine(RepoRoot, "mods");
            if (!Directory.Exists(modsDir))
                throw new DirectoryNotFoundException("Tel-Mod-Library checkout not found at " + RepoRoot + " (built into InGameTests.TelMods.dll; rebuild it from the checkout)");

            all = new List<TelMod>();
            foreach (string project in Directory.GetFiles(modsDir, "*.csproj", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(project))
                    continue;
                string xml = File.ReadAllText(project);
                string id = Property(xml, "HarmonyID");
                if (id == null)
                    continue;

                TelMod mod = new()
                {
                    Name = Property(xml, "ModName"),
                    HarmonyId = id,
                    Version = Property(xml, "Version"),
                    ProjectDir = Path.GetDirectoryName(project),
                    ProjectFile = project,
                };
                all.Add(mod);
            }
            all.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return all;
        }

        /// <summary>Mods whose patches are live in this session (enabled, with patches applied by their HarmonyID).</summary>
        public static List<TelMod> Active() => All().Where(m => m.IsEnabled() && m.Assembly() != null).ToList();

        public static TelMod ByHarmonyId(string id) => All().FirstOrDefault(m => m.HarmonyId == id);

        /// <summary>Installed copies (LocalLow and Workshop) whose info.json names this HarmonyID.</summary>
        public List<Mods._mod> InstalledCopies()
        {
            return Mods._Mods.Where(m => m != null && HarmonyIdOf(m) == HarmonyId).ToList();
        }

        public bool IsEnabled() => InstalledCopies().Any(m => m.IsEnabled());

        /// <summary>
        /// The loaded mod assembly: the one whose methods are applied as patches under the
        /// HarmonyID, or else any loaded assembly with that name. Null when not loaded.
        /// </summary>
        public Assembly Assembly()
        {
            foreach (MethodBase original in Harmony.GetAllPatchedMethods())
            {
                Patches info = Harmony.GetPatchInfo(original);
                Patch patch = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                    .FirstOrDefault(p => p.owner == HarmonyId);
                if (patch != null)
                    return patch.PatchMethod.DeclaringType.Assembly;
            }
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == HarmonyId);
        }

        /// <summary>
        /// The mod's DLLs in this checkout's bin folder, one per configuration built. A Release
        /// build deploys a copy of its DLL, so the loaded DLL equals one of these if it came from here.
        /// </summary>
        public IEnumerable<string> BuildOutputs()
        {
            string bin = Path.Combine(ProjectDir, "bin");
            return Directory.Exists(bin)
                ? Directory.GetFiles(bin, HarmonyId + ".dll", SearchOption.AllDirectories)
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Files deployed from the mod's assets folder that the game reads, as paths relative to
        /// it. The Steam description is upload-only, so it is left out.
        /// </summary>
        public IEnumerable<string> AssetFiles()
        {
            string assets = Path.Combine(ProjectDir, "assets");
            if (!Directory.Exists(assets))
                yield break;
            foreach (string file in Directory.GetFiles(assets, "*", SearchOption.AllDirectories))
            {
                if (!string.Equals(Path.GetFileName(file), "steam description.txt", StringComparison.OrdinalIgnoreCase))
                    yield return file.Substring(assets.Length).TrimStart(Path.DirectorySeparatorChar);
            }
        }

        /// <summary>The command that rebuilds and deploys this mod.</summary>
        public string BuildCommand()
        {
            return $"dotnet build \"{ProjectFile.Substring(RepoRoot.Length).TrimStart(Path.DirectorySeparatorChar)}\" -c Release";
        }

        public override string ToString() => Name;

        internal static string HarmonyIdOf(Mods._mod mod)
        {
            try
            {
                string info = Path.Combine(mod.Path, "info.json");
                if (!File.Exists(info))
                    return null;
                Match m = HarmonyIdInInfo.Match(File.ReadAllText(info));
                return m.Success ? m.Groups[1].Value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Property(string xml, string name)
        {
            Match m = Regex.Match(xml, "<" + name + ">([^<]*)</" + name + ">");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static bool IsBuildOutput(string path)
        {
            string sep = Path.DirectorySeparatorChar.ToString();
            return path.Contains(sep + "bin" + sep) || path.Contains(sep + "obj" + sep);
        }
    }
}
