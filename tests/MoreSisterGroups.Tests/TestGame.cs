using HarmonyLib;
using SisterGroups;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

// Every test shares the game's static state (difficulty, groups, idols, singles).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MoreSisterGroups.Tests
{
    /// <summary>
    /// Builds the minimal game state the patched methods read: difficulty, groups, idols and singles.
    /// </summary>
    public static class TestGame
    {
        public const string ModNamespace = "SisterGroups";

        // Not "tests.SisterGroups": PatchTargetTests unpatches everything under that ID
        public const string HarmonyId = "tests.SisterGroups.Behaviour";

        private static readonly Lazy<bool> Patched = new(() =>
        {
            Harmony harmony = new(HarmonyId);
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Utility).Assembly, ModNamespace))
                harmony.CreateClassProcessor(patchClass).Patch();
            return true;
        });

        /// <summary>
        /// Resets the game state to a new save with just the main group. The mod is applied to the
        /// game's methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static Groups._group Reset(staticVars._playerData._difficulty difficulty = staticVars._playerData._difficulty.normal)
        {
            _ = Patched.Value;

            staticVars.PlayerData = new staticVars._playerData { Difficulty = difficulty };
            Groups.Groups_ = new List<Groups._group>();
            data_girls.girl = new List<data_girls.girls>();
            singles.Singles = new List<singles._single>();
            Utility.groupSales = null;
            return Group();
        }

        /// <summary>
        /// Adds a group (the first one added is the main group, ID 0) with this many active members.
        /// </summary>
        public static Groups._group Group(int members = 0, Groups._group._status status = Groups._group._status.normal)
        {
            Groups._group group = new() { ID = Groups.Groups_.Count, Status = status };
            Groups.Groups_.Add(group);
            for (int i = 0; i < members; i++)
                Idol(group);
            return group;
        }

        /// <summary>
        /// Adds an idol to the roster, and to the group if given.
        /// </summary>
        public static data_girls.girls Idol(Groups._group group = null, data_girls._status status = data_girls._status.normal)
        {
            data_girls.girls girl = new() { status = status };
            data_girls.girl.Add(girl);
            group?.Girls.Add(girl);
            return girl;
        }

        /// <summary>
        /// Adds a single to the release list (oldest first), owned by the group if given.
        /// A single that's in no group's list belongs to the main group, as in the game.
        /// </summary>
        public static singles._single Single(Groups._group group = null, singles._single._status status = singles._single._status.released)
        {
            singles._single single = new() { status = status };
            singles.Singles.Add(single);
            group?.Singles.Add(single);
            return single;
        }

        /// <summary>
        /// Gives the group fan points (as spent on its appeal screen) for each of these fan types.
        /// </summary>
        public static void FanPoints(Groups._group group, int points, params resources.fanType[] types)
        {
            foreach (resources.fanType type in types)
                group.Fans.Add(new Groups._group._fan { Type = type, Points = points });
        }

        /// <summary>
        /// Whether the game method's original IL calls the other method.
        /// </summary>
        public static bool Calls(MethodBase method, MethodInfo callee) =>
            PatchProcessor.GetOriginalInstructions(method).Any(i => i.Calls(callee));

        /// <summary>
        /// Compares by reference: game objects may override equality.
        /// </summary>
        public static IEqualityComparer<T> SameObject<T>() where T : class => new ReferenceComparer<T>();

        private class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "More Sister Groups", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
    }
}
