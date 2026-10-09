using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Tier 0: the loaded Tel mods are this checkout's builds, checked with the smoke suite right
    /// after the save loads. The runner's own smoke checks cover the rest of tier 0 for every
    /// Harmony mod: patches applied and transpilers changing IL.
    /// Mods that aren't installed or enabled are noted and skipped.
    /// </summary>
    internal static class TelModSmokeTests
    {
        /// <summary>
        /// The copy the game loaded is this checkout's build: the same version, the same DLL as
        /// its last build here, and the same assets. Otherwise the other results are about other
        /// code. Edits made since that build aren't detected: build changed mods first (run.py --build-mods).
        /// </summary>
        [InGameTest(Order = 0)]
        private static IEnumerator LoadedBuildsMatchCheckout(TestContext ctx)
        {
            var notInstalled = new List<string>();
            var notEnabled = new List<string>();
            int checkedMods = 0;
            foreach (TelMod mod in TelMod.All())
            {
                if (mod.InstalledCopies().Count == 0)
                {
                    notInstalled.Add(mod.Name);
                    continue;
                }
                if (!mod.IsEnabled())
                {
                    notEnabled.Add(mod.Name);
                    continue;
                }
                Assembly assembly = mod.Assembly();
                if (assembly == null)
                {
                    ctx.Fail($"{mod.Name} is enabled but {mod.HarmonyId}.dll is not loaded");
                    continue;
                }
                checkedMods++;

                string deployedDir = Path.GetDirectoryName(assembly.Location);
                string copy = IsUnder(deployedDir, TelMod.LocalModsDir) ? "local copy" : "Workshop copy";
                string rebuild = "Rebuild it in Release to deploy it: " + mod.BuildCommand();

                string loaded = ThreePart(assembly.GetName().Version);
                string repo = ThreePart(new Version(mod.Version));
                if (loaded != repo)
                {
                    ctx.Fail($"{mod.Name}: the game loaded the {copy} of version {loaded}; this checkout is {repo}. {rebuild}");
                    continue;
                }

                // File times can't tell: git rewrites unchanged files, and builds aren't byte-identical across line-ending changes.
                string build = mod.BuildOutputs().FirstOrDefault(b => SameBytes(b, assembly.Location));
                if (build == null)
                    ctx.Fail($"{mod.Name}: the loaded {copy} ({assembly.Location}) isn't the DLL of the last build in this checkout. {rebuild}");
                else if (!build.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar))
                    ctx.Note($"{mod.Name}: the loaded DLL is a non-Release build ({Relative(build)})");

                List<string> differing = mod.AssetFiles()
                    .Where(rel => !SameContent(Path.Combine(Path.Combine(mod.ProjectDir, "assets"), rel), Path.Combine(deployedDir, rel)))
                    .ToList();
                if (differing.Count > 0)
                    ctx.Fail($"{mod.Name}: the loaded {copy} has a missing or different assets\\{differing[0]}{More(differing)}. {rebuild}");
            }

            if (notInstalled.Count > 0)
                ctx.Note("Not installed, not tested: " + string.Join(", ", notInstalled.ToArray()));
            if (notEnabled.Count > 0)
                ctx.Note("Not enabled, not tested: " + string.Join(", ", notEnabled.ToArray()));
            ctx.Record("modsChecked", checkedMods);
            yield break;
        }

        private static string ThreePart(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";

        private static string More(List<string> items) => items.Count > 1 ? $" and {items.Count - 1} more" : "";

        private static string Relative(string path) => path.Substring(TelMod.RepoRoot.Length).TrimStart(Path.DirectorySeparatorChar);

        private static bool IsUnder(string path, string root)
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameBytes(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b) || new FileInfo(a).Length != new FileInfo(b).Length)
                return false;
            return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
        }

        /// <summary>Equal bytes, or for text assets equal text apart from line endings (git may convert them).</summary>
        private static bool SameContent(string a, string b)
        {
            if (SameBytes(a, b))
                return true;
            string ext = Path.GetExtension(a).ToLowerInvariant();
            if (!File.Exists(b) || (ext != ".json" && ext != ".txt"))
                return false;
            return File.ReadAllText(a).Replace("\r\n", "\n") == File.ReadAllText(b).Replace("\r\n", "\n");
        }
    }
}
