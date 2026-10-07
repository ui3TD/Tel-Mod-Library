using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;

// Every test shares the game's static state (policies, girls, staticVars) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PoliciesThatMatter.Tests
{
    /// <summary>
    /// The weekly effects call game methods that reach Unity's native code (Random.Range,
    /// Camera.main) or need a running game. Those can't run or be patched outside the game,
    /// so the mod's own methods are patched to call these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<int, bool> Chance;
        public static Func<int, int, int> Range;
        public static Func<data_girls.girls, int> FameLevel;

        public static readonly List<int> ChancesRolled = new();
        public static readonly List<(int min, int max)> RangesRolled = new();
        public static readonly List<(data_girls.girls girl, data_girls._paramType type, float val)> ParamsAdded = new();
        public static readonly List<(data_girls.girls girl, long val)> FansAdded = new();
        public static readonly List<(resources.type type, long val)> ResourcesAdded = new();
        public static readonly List<string> Notifications = new();

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(int) })] = Stub(nameof(StubChance)),
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })] = Stub(nameof(StubRange)),
            [AccessTools.Method(typeof(NotificationManager), nameof(NotificationManager.AddNotification), new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) })] = Stub(nameof(StubNotification)),
            [AccessTools.Method(typeof(resources), nameof(resources.Add), new[] { typeof(resources.type), typeof(long) })] = Stub(nameof(StubAddResource)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam))] = Stub(nameof(StubAddParam)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(long), typeof(resources.fanType?) })] = Stub(nameof(StubAddFans)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetFameLevel))] = Stub(nameof(StubFameLevel)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetName))] = Stub(nameof(StubName)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(data_girls_PoliciesStamina), nameof(data_girls_PoliciesStamina.Prefix)),
            AccessTools.Method(typeof(data_girls_PoliciesStamina), "AddStaminaNotification"),
            AccessTools.Method(typeof(data_girls_PoliciesResources), nameof(data_girls_PoliciesResources.Prefix)),
            AccessTools.Method(typeof(data_girls_PoliciesResources), "AddBonusNotification"),
            AccessTools.Method(typeof(traits_GetRandomTraitType), nameof(traits_GetRandomTraitType.Postfix)),
            AccessTools.Method(typeof(agency__room_DoGirlTraining), nameof(agency__room_DoGirlTraining.Infix)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony harmony = new("tests.PoliciesThatMatter.Seams");
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            foreach (MethodBase method in Patched)
                harmony.Patch(method, transpiler: transpiler);
            return true;
        });

        /// <summary>
        /// Installs the stubs once per test run and clears what earlier tests recorded.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            Chance = _ => false;
            Range = (min, _) => min;
            FameLevel = _ => 0;
            ChancesRolled.Clear();
            RangesRolled.Clear();
            ParamsAdded.Clear();
            FansAdded.Clear();
            ResourcesAdded.Clear();
            Notifications.Clear();

            foreach (string key in new[] { "MENTAL_STAMINA", "PT", "FANS", "MONEY", "IDOL__POLICY_SNS", "IDOL__POLICY_STREAMING", PoliciesThatMatter.DATING_NOTIF_LABEL })
                Language.Data[key] = key;
        }

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodBase target && Redirects.TryGetValue(target, out MethodInfo stub))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = stub;
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static bool StubChance(int num)
        {
            ChancesRolled.Add(num);
            return Chance(num);
        }

        private static int StubRange(int min, int max)
        {
            RangesRolled.Add((min, max));
            int roll = Range(min, max);
            Assert.InRange(roll, min, max - 1);
            return roll;
        }

        private static void StubNotification(string text, Color32 color, NotificationManager._notification._type type) => Notifications.Add(text);
        private static void StubAddResource(resources.type type, long val) => ResourcesAdded.Add((type, val));
        private static void StubAddParam(data_girls.girls girl, data_girls._paramType type, float val, bool ignorePotential) => ParamsAdded.Add((girl, type, val));
        private static void StubAddFans(data_girls.girls girl, long val, resources.fanType? fanType) => FansAdded.Add((girl, val));
        private static int StubFameLevel(data_girls.girls girl) => FameLevel(girl);
        private static string StubName(data_girls.girls girl, bool full) => "Idol";
    }

    /// <summary>
    /// Selects policies from the policy list the mod ships.
    /// </summary>
    public static class TestPolicies
    {
        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModAsset(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Policies That Matter", "assets", relativePath);

        public static string PolicyFile => ModAsset(Path.Combine("JSON", "Policies", "policies.json"));

        /// <summary>
        /// Parses a policies.json the way policies.Load does.
        /// </summary>
        public static List<policies.value> Parse(string path)
        {
            SimpleJSON.JSONNode node = mainScript.ProcessInboundData(File.ReadAllText(path));
            List<policies.value> values = new();
            for (int i = 0; i < node.Count; i++)
            {
                policies.value value = new();
                value.SetData(node[i]);
                values.Add(value);
            }
            return values;
        }

        /// <summary>
        /// Loads the mod's defaults, then selects the given options.
        /// </summary>
        public static void Use(params policies._value[] selected)
        {
            policies.Values = Parse(PolicyFile);
            foreach (policies._value option in selected)
            {
                policies.value chosen = policies.Values.Single(v => v.Value == option);
                foreach (policies.value value in policies.Values.Where(v => v.Type == chosen.Type))
                    value.Selected = false;
                chosen.Selected = true;
            }
        }
    }
}
