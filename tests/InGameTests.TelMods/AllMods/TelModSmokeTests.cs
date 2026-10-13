using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Tier 0: the loaded Tel mods are this checkout's builds, and every test class here names its
    /// mod, checked with the smoke suite right after the save loads. The runner's own smoke checks
    /// cover the rest of tier 0 for every Harmony mod: patches applied and transpilers changing IL.
    /// These checks judge every Tel mod in scope, so they also run in the affected suite. Mods that
    /// aren't installed, enabled or in scope are noted and skipped.
    /// </summary>
    [ModUnderTest(ModUnderTestAttribute.EveryMod)]
    internal static class TelModSmokeTests
    {
        /// <summary>
        /// The copy the game loaded is this checkout's build: the same version, the same code as
        /// its last build here, and the same assets. Otherwise the other results are about other
        /// code. Edits made since that build aren't detected: build changed mods first (run.py --build-mods).
        /// </summary>
        [InGameTest(Order = 0)]
        private static IEnumerator LoadedBuildsMatchCheckout(TestContext ctx)
        {
            var notInstalled = new List<string>();
            var notEnabled = new List<string>();
            int checkedMods = 0;
            List<TelMod> mods = TelMod.All();
            ModScope.NoteSkipped(ctx, mods.Where(m => !ModScope.Includes(m.HarmonyId)).Select(m => m.Name));
            foreach (TelMod mod in mods.Where(m => ModScope.Includes(m.HarmonyId)))
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
                // Rebuilds of the same code (make_release.py, Release test runs) still match: see SameBuild.
                string build = mod.BuildOutputs().FirstOrDefault(b => SameBuild(b, assembly.Location));
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

        /// <summary>
        /// Every class here with tests names the one mod they exercise: "*" or a HarmonyID from a mod
        /// project in this checkout. An untagged class would run in every scope and never in the
        /// affected suite, and a misspelt ID would never be in scope; neither shows any other way.
        /// </summary>
        [InGameTest(Order = 0)]
        private static IEnumerator EveryTestClassNamesATelMod(TestContext ctx)
        {
            int classes = 0;
            foreach (Type type in typeof(TelModSmokeTests).Assembly.GetTypes())
            {
                bool hasTests = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(m => m.IsDefined(typeof(InGameTestAttribute), false));
                if (!hasTests)
                    continue;
                classes++;
                var tag = (ModUnderTestAttribute)type.GetCustomAttributes(typeof(ModUnderTestAttribute), false).FirstOrDefault();
                if (tag == null)
                    ctx.Fail($"{type.Name} has tests but no [ModUnderTest]");
                else if (tag.HarmonyId != ModUnderTestAttribute.EveryMod && TelMod.ByHarmonyId(tag.HarmonyId) == null)
                    ctx.Fail($"{type.Name}: [ModUnderTest(\"{tag.HarmonyId}\")] matches no mod project in this checkout");
            }
            ctx.Record("classesChecked", classes);
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

        /// <summary>
        /// The same DLL apart from what every build of the same code changes: the timestamps, the
        /// module and PDB IDs, the PDB checksum, and the commit the SDK appends to the informational
        /// version (HEAD at build time, not the code that was built).
        /// </summary>
        private static bool SameBuild(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b) || new FileInfo(a).Length != new FileInfo(b).Length)
                return false;
            byte[] x = File.ReadAllBytes(a), y = File.ReadAllBytes(b);
            if (x.SequenceEqual(y))
                return true;
            try
            {
                ClearBuildIdentity(x);
                ClearBuildIdentity(y);
            }
            catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException)
            {
                return false; // not a PE file this reads
            }
            return x.SequenceEqual(y);
        }

        private static void ClearBuildIdentity(byte[] dll)
        {
            int coff = BitConverter.ToInt32(dll, 0x3C) + 4;
            int sectionCount = BitConverter.ToUInt16(dll, coff + 2);
            int optional = coff + 20;
            int sections = optional + BitConverter.ToUInt16(dll, coff + 16);
            int directories = optional + (BitConverter.ToUInt16(dll, optional) == 0x20B ? 112 : 96);
            Array.Clear(dll, coff + 4, 4); // TimeDateStamp
            Array.Clear(dll, optional + 64, 4); // CheckSum

            // Debug directory: each entry's timestamp, the CodeView GUID and age, the PDB checksum
            int debug = FileOffset(dll, sections, sectionCount, BitConverter.ToInt32(dll, directories + 6 * 8));
            int debugSize = BitConverter.ToInt32(dll, directories + 6 * 8 + 4);
            for (int entry = debug; debug >= 0 && entry + 28 <= debug + debugSize; entry += 28)
            {
                Array.Clear(dll, entry + 4, 4);
                int type = BitConverter.ToInt32(dll, entry + 12);
                int size = BitConverter.ToInt32(dll, entry + 16);
                int data = BitConverter.ToInt32(dll, entry + 24);
                if (type == 2)
                    Array.Clear(dll, data + 4, 20);
                else if (type == 19)
                    Array.Clear(dll, data, size);
            }

            // Metadata #GUID heap: the module version ID
            int cli = FileOffset(dll, sections, sectionCount, BitConverter.ToInt32(dll, directories + 14 * 8));
            int root = FileOffset(dll, sections, sectionCount, BitConverter.ToInt32(dll, cli + 8));
            int versionLength = BitConverter.ToInt32(dll, root + 12);
            int streamCount = BitConverter.ToUInt16(dll, root + 18 + versionLength);
            int header = root + 20 + versionLength;
            for (int i = 0; i < streamCount; i++)
            {
                int name = header + 8;
                int nameLength = Array.IndexOf(dll, (byte)0, name) - name;
                if (System.Text.Encoding.ASCII.GetString(dll, name, nameLength) == "#GUID")
                    Array.Clear(dll, root + BitConverter.ToInt32(dll, header), BitConverter.ToInt32(dll, header + 4));
                header = name + (nameLength / 4 + 1) * 4;
            }

            // "+<commit>": in the metadata as ASCII, in the version resource as UTF-16
            ClearCommitHashes(dll, 1);
            ClearCommitHashes(dll, 2);
        }

        private static int FileOffset(byte[] dll, int sections, int sectionCount, int rva)
        {
            if (rva == 0)
                return -1;
            for (int s = sections; s < sections + sectionCount * 40; s += 40)
            {
                int start = BitConverter.ToInt32(dll, s + 12);
                if (rva >= start && rva < start + Math.Max(BitConverter.ToInt32(dll, s + 8), BitConverter.ToInt32(dll, s + 16)))
                    return rva - start + BitConverter.ToInt32(dll, s + 20);
            }
            throw new ArgumentException("RVA outside every section");
        }

        private static void ClearCommitHashes(byte[] dll, int charSize)
        {
            const int length = 40;
            for (int i = 0; i + (length + 1) * charSize <= dll.Length; i++)
            {
                if (!IsChar(dll, i, charSize, c => c == '+'))
                    continue;
                int hash = i + charSize;
                if (Enumerable.Range(0, length).All(k => IsChar(dll, hash + k * charSize, charSize, IsHexDigit))
                    && !IsChar(dll, hash + length * charSize, charSize, IsHexDigit))
                    Array.Clear(dll, hash, length * charSize);
            }
        }

        private static bool IsChar(byte[] dll, int at, int charSize, Func<char, bool> test) =>
            at + charSize <= dll.Length && (charSize == 1 || dll[at + 1] == 0) && test((char)dll[at]);

        private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');

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
