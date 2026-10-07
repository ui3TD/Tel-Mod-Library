using CustomAuditions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Xunit;
using static CustomAuditions.CustomAuditions;

// Every test shares the game's static state (variables, texture assets, girls) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// The mod calls Unity's Random (directly and through mainScript.chance, Shuffle and
    /// GetPointsByType), which is native code that can't run outside the game. The mod's methods,
    /// and the vanilla GenerateParams they replace, are patched to call these stubs instead.
    /// Both sides draw from the same stub, so a seeded run gives identical rolls to each.
    /// </summary>
    public static class Seams
    {
        public static Func<int, int, int> Range;
        public static Func<Auditions.data._girl._type, int> Points;
        public static readonly List<string> Errors = new();

        /// <summary>
        /// Range calls since the last Reset. Vanilla stat generation spins forever on an impossible
        /// budget; past this limit the stub throws instead, so tests can detect the hang.
        /// </summary>
        public static int RangeCalls;
        public const int HangLimit = 1_000_000;

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })] = Stub(nameof(StubRange)),
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(int) })] = Stub(nameof(StubChance)),
            [AccessTools.Method(typeof(Auditions), nameof(Auditions.GetPointsByType))] = Stub(nameof(StubPoints)),
            [AccessTools.Method(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogError), new[] { typeof(object) })] = Stub(nameof(StubLogError)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(data_girls_GenerateParams), "GenerateParamsSafely"),
            AccessTools.Method(typeof(data_girls_GenerateParams), "SpendVanillaBudget"),
            AccessTools.Method(typeof(data_girls_GenerateParams), "GeneratePotential"),
            AccessTools.Method(typeof(data_girls_GenerateParams), nameof(data_girls_GenerateParams.Infix)),
            AccessTools.Method(typeof(data_girls_GenerateGirl), nameof(data_girls_GenerateGirl.Postfix)),
            AccessTools.Method(typeof(Auditions_GenerateGirls), nameof(Auditions_GenerateGirls.Finalizer)),
            AccessTools.Method(typeof(CustomAuditions.CustomAuditions), nameof(ApplyRandomBirthdayInConfiguredRange)),
            AccessTools.Method(typeof(data_girls), "GenerateParams"),
            AccessTools.Method(typeof(data_girls), "GeneratePotential"),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony harmony = new("tests.TargetedAuditions.Seams");
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            foreach (MethodBase method in Patched)
                harmony.Patch(method, transpiler: transpiler);
            return true;
        });

        /// <summary>
        /// Installs the stubs once per test run and resets the mod's and game's shared state.
        /// </summary>
        public static void Reset(int seed = 1)
        {
            _ = Installed.Value;
            Seeded(seed);
            Points = _ => throw new InvalidOperationException("Set Seams.Points first");
            Errors.Clear();

            while (IsGeneratingAudition)
                EndAuditionGeneration();

            variables.variable.Clear();
            Auditions.UsedBodyIDs.Clear();
            data_girls.girl.Clear();
            TextureAssets.Clear();

            minAge = 12;
            maxAge = 23;
            defaultMinAge = 12;
            defaultMaxAge = 23;
            chanceLesbian = 7;
            chanceBi = 14;
            priorityDict.Clear();
            foreach (data_girls._paramType param in paramTypes)
                priorityDict[param] = 50;
        }

        /// <summary>
        /// Draws every roll from a seeded generator, with Unity's exclusive upper bound.
        /// </summary>
        public static void Seeded(int seed)
        {
            System.Random rng = new(seed);
            Range = (min, max) => rng.Next(min, max);
            RangeCalls = 0;
        }

        /// <summary>
        /// Returns the given rolls in order, then fails if more are asked for.
        /// </summary>
        public static void Rolls(params int[] rolls)
        {
            Queue<int> queue = new(rolls);
            Range = (min, max) =>
            {
                Assert.True(queue.Count > 0, $"Unexpected roll Range({min}, {max})");
                return queue.Dequeue();
            };
        }

        /// <summary>
        /// The game's private list of every loaded sprite asset.
        /// </summary>
        public static List<data_girls_textures._textureAsset> TextureAssets =>
            (List<data_girls_textures._textureAsset>)AccessTools.Field(typeof(data_girls_textures), "textureAssets").GetValue(null);

        /// <summary>
        /// Sets a game variable without going through variables.Set, whose first write of a name logs
        /// through Unity and raises the task system's event.
        /// </summary>
        public static void SetVariable(string name, string value)
        {
            variables._variable existing = variables.variable.FirstOrDefault(v => v.name == name);
            if (existing != null)
                existing.value = value;
            else
                variables.variable.Add(new variables._variable { name = name, value = value });
        }

        /// <summary>
        /// A girl with the eight audition stats, so setParam has somewhere to write.
        /// </summary>
        public static data_girls.girls NewGirl()
        {
            data_girls.girls girl = new();
            girl.parameters.Clear();
            foreach (data_girls._paramType type in paramTypes)
                girl.parameters.Add(new data_girls.girls.param { type = type });
            return girl;
        }

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their fields.
        /// </summary>
        public static T Component<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModAsset(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Targeted Auditions", "assets", relativePath);

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo target)
                {
                    if (Redirects.TryGetValue(target, out MethodInfo stub))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = stub;
                    }
                    else if (target.DeclaringType == typeof(ExtensionMethods) && target.Name == nameof(ExtensionMethods.Shuffle))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = Stub(nameof(StubShuffle)).MakeGenericMethod(target.GetGenericArguments());
                    }
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static int StubRange(int min, int max)
        {
            if (++RangeCalls > HangLimit)
                throw new HangException();
            int roll = Range(min, max);
            Assert.InRange(roll, min, Math.Max(min, max - 1));
            return roll;
        }

        /// <summary>
        /// Same logic as mainScript.chance(int), so it draws the same rolls.
        /// </summary>
        private static bool StubChance(int num)
        {
            if (num >= 100)
                return true;
            if (num <= 0)
                return false;
            return StubRange(0, 100) < num;
        }

        /// <summary>
        /// Same logic as ExtensionMethods.Shuffle.
        /// </summary>
        private static void StubShuffle<T>(IList<T> ts)
        {
            int count = ts.Count;
            for (int i = 0; i < count - 1; i++)
            {
                int index = StubRange(i, count);
                (ts[i], ts[index]) = (ts[index], ts[i]);
            }
        }

        private static int StubPoints(Auditions.data._girl._type type) => Points(type);
        private static void StubLogError(object message) => Errors.Add(message?.ToString());
    }

    public class HangException : Exception
    {
    }
}
