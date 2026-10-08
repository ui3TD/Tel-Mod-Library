using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Xunit;

// Every test shares the game's static state (idols, date).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NeverGraduate.Tests
{
    /// <summary>
    /// Builds the minimal game state the weekly graduation check reads: the date and the idol list.
    /// </summary>
    public static class TestGame
    {
        public const string ModNamespace = "NeverGraduate";
        public static readonly DateTime Today = new(2023, 6, 5);

        // Not "tests.NeverGraduate": PatchTargetTests unpatches everything under that ID
        public const string HarmonyId = "tests.NeverGraduate.Behaviour";

        private static readonly Lazy<bool> Patched = new(() =>
        {
            Harmony harmony = new(HarmonyId);
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(data_girls_UpdateGraduationDates).Assembly, ModNamespace))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game state. With patched set, the mod is applied to the game's methods
        /// (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;

            staticVars.dateTime = Today;
            data_girls.girl = new List<data_girls.girls>();
        }

        /// <summary>
        /// Adds an idol with this status and graduation date (the game's 1900 "not set yet" by default).
        /// </summary>
        public static data_girls.girls Idol(data_girls._status status = data_girls._status.normal, DateTime? graduationDate = null)
        {
            data_girls.girls girl = new() { status = status };
            if (graduationDate != null)
                girl.Graduation_Date = graduationDate.Value;
            data_girls.girl.Add(girl);
            return girl;
        }

        /// <summary>
        /// The idol manager component; a MonoBehaviour, so it's created without running Unity's constructor.
        /// </summary>
        public static data_girls Manager() => (data_girls)FormatterServices.GetUninitializedObject(typeof(data_girls));

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Never Graduate", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
    }
}
