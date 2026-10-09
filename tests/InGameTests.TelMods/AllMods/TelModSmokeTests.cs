using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Tier 0: checks of every enabled Tel mod that need no game time, run with the smoke
    /// suite right after the save loads. They catch what a passive run can't: a stale build,
    /// a patch that never applied, and a transpiler whose IL search silently failed.
    /// Mods that aren't installed or enabled are noted and skipped.
    /// </summary>
    internal static class TelModSmokeTests
    {
        private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Transpiler", "Finalizer", "ILManipulator" };

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

        /// <summary>
        /// Every patch method a mod declares is applied under its HarmonyID. Harmony skips the
        /// patch classes in types that fail to load, and the mod loader only logs that.
        /// </summary>
        [InGameTest(Order = 0)]
        private static IEnumerator EveryPatchMethodIsApplied(TestContext ctx)
        {
            var applied = new HashSet<string>();
            foreach (MethodBase original in Harmony.GetAllPatchedMethods())
            {
                foreach (Patch patch in AllPatches(Harmony.GetPatchInfo(original)))
                    applied.Add(patch.owner + " " + Key(patch.PatchMethod));
            }

            int expected = 0;
            foreach (TelMod mod in TelMod.Active())
            {
                Type[] types;
                try
                {
                    types = mod.Assembly().GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (string message in ex.LoaderExceptions.Where(e => e != null).Select(e => e.Message).Distinct())
                        ctx.Fail($"{mod.Name}: some types failed to load, so their patches were skipped: {message}");
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in types)
                {
                    // PatchAll only processes classes with a class-level Harmony attribute.
                    bool classAnnotated = type.IsDefined(typeof(HarmonyAttribute), false);
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (PatchKind(method) == null || method.IsDefined(typeof(HarmonyReversePatch), false))
                            continue;
                        if (!classAnnotated && !method.IsDefined(typeof(HarmonyPatch), false))
                            continue;
                        expected++;
                        if (!applied.Contains(mod.HarmonyId + " " + Key(method)))
                            ctx.Fail($"{mod.Name}: {type.FullName}.{method.Name} is not applied"
                                + (classAnnotated ? "" : " (its class has no [HarmonyPatch], so PatchAll skips it)"));
                    }
                }
            }
            ctx.Record("patchMethodsChecked", expected);
            yield break;
        }

        /// <summary>
        /// Each Tel transpiler changes its method's IL. A transpiler whose IL search finds nothing
        /// usually returns the instructions untouched, and the mod then does nothing without an error.
        /// Re-runs the patched methods' transpilers one at a time and compares the IL before and after each.
        /// </summary>
        [InGameTest(Order = 0)]
        private static IEnumerator EveryTranspilerChangesIL(TestContext ctx)
        {
            var telIds = new HashSet<string>(TelMod.Active().Select(m => m.HarmonyId));
            int checkedTranspilers = 0;
            foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToList())
            {
                Patches info = Harmony.GetPatchInfo(original);
                if (!info.Transpilers.Any(p => telIds.Contains(p.owner)))
                    continue;

                string target = original.DeclaringType.FullName + "." + original.Name;
                try
                {
                    // Sorted the way Harmony applies them; GetCurrentInstructions applies the first N.
                    List<MethodInfo> sorted = PatchProcessor.GetSortedPatchMethods(original, info.Transpilers.ToArray());
                    string before = Dump(PatchProcessor.GetCurrentInstructions(original, 0));
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        string after = Dump(PatchProcessor.GetCurrentInstructions(original, i + 1));
                        Patch patch = info.Transpilers.First(p => Key(p.PatchMethod) == Key(sorted[i]));
                        if (telIds.Contains(patch.owner))
                        {
                            checkedTranspilers++;
                            if (after == before)
                                ctx.Fail($"{TelMod.ByHarmonyId(patch.owner).Name}: {sorted[i].DeclaringType.FullName}.{sorted[i].Name}"
                                    + $" leaves {target} unchanged; its IL search probably found nothing");
                        }
                        before = after;
                    }
                }
                catch (Exception ex)
                {
                    ctx.Fail($"Re-running the transpilers of {target} threw: {ex}");
                }
            }
            ctx.Record("transpilersChecked", checkedTranspilers);
            yield break;
        }

        private static IEnumerable<Patch> AllPatches(Patches info)
        {
            return info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.ILManipulators);
        }

        /// <summary>The patch type a method declares by attribute or by name, or null.</summary>
        private static string PatchKind(MethodInfo method)
        {
            foreach (object attribute in method.GetCustomAttributes(false))
            {
                string name = attribute.GetType().Name;
                if (name.StartsWith("Harmony", StringComparison.Ordinal) && PatchKinds.Contains(name.Substring("Harmony".Length)))
                    return name.Substring("Harmony".Length);
            }
            return PatchKinds.Contains(method.Name) ? method.Name : null;
        }

        private static string Key(MethodBase method) => method.Module.ModuleVersionId + ":" + method.MetadataToken;

        /// <summary>Instructions as text. Label and local numbers are stable between calls for the same transpilers.</summary>
        private static string Dump(List<CodeInstruction> code)
        {
            var sb = new StringBuilder();
            foreach (CodeInstruction ci in code)
            {
                sb.Append(ci.opcode.Name).Append(' ').Append(Operand(ci.operand));
                foreach (Label label in ci.labels)
                    sb.Append(" L").Append(label.GetHashCode());
                foreach (ExceptionBlock block in ci.blocks)
                    sb.Append(" B").Append(block.blockType).Append(block.catchType);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string Operand(object operand)
        {
            switch (operand)
            {
                case null:
                    return "";
                case Label label:
                    return "L" + label.GetHashCode();
                case Label[] labels:
                    return string.Join(",", labels.Select(l => "L" + l.GetHashCode()).ToArray());
                case LocalBuilder local:
                    return "V" + local.LocalIndex + ":" + local.LocalType;
                case MemberInfo member:
                    return member.DeclaringType + "::" + member;
                default:
                    return Convert.ToString(operand, CultureInfo.InvariantCulture);
            }
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
