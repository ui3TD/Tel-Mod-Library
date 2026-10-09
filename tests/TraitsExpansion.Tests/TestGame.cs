using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using TraitsExpansion;
using UnityEngine;
using Xunit;
using static TraitsExpansion.TraitsExpansion;

// Every test shares the game's static state (idols, cliques, policies, date) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TraitsExpansionTests
{
    /// <summary>
    /// Builds the game state the trait code reads, and applies the mod to the game's methods.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.TraitsExpansion": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.TraitsExpansion.Behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(traits_GetTraitType).Assembly, "TraitsExpansion"))
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }
                catch (HarmonyException e) when (e.InnerException is System.Security.SecurityException)
                {
                    // The target calls Unity native methods, which can't be compiled outside the game
                    // (e.g. Random.Range in GetNewFansByAttendance). Those patches are tested directly.
                }
            }
            return true;
        });

        /// <summary>
        /// Resets the game state and the mod's flags. With patched set, the mod is applied to the
        /// game's methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;
            Seams.Reset();

            staticVars.dateTime = Today;
            staticVars.PlayerData.Difficulty = staticVars._playerData._difficulty.normal;
            data_girls.girl = new List<data_girls.girls>();
            Relationships.Cliques = new List<Relationships._clique>();
            Awards._Awards = new List<Awards._award>();
            Awards._Nominations = new List<Awards._award>();
            // A vibe that boosts none of the stats the tests read, so senbatsu scores aren't scaled
            policies.Values = new List<policies.value>
            {
                new() { Type = policies._type.vibe, Value = policies._value.vibe_cute, Selected = true }
            };

            patchGetVal = false;
            patchAddParam = false;
            patchGetFan_Count = false;
        }

        /// <summary>
        /// An active idol with this trait. Every stat starts at statValue, and stamina at 100.
        /// </summary>
        public static data_girls.girls Idol(NewTraits trait = NewTraits.none, float statValue = 40f, string name = "Idol")
        {
            data_girls.girls girl = new() { nickname = name, trait = (traits._trait._type)trait };
            foreach (data_girls._paramType type in Enum.GetValues(typeof(data_girls._paramType)))
            {
                float value = type is data_girls._paramType.physicalStamina or data_girls._paramType.mentalStamina ? 100f : statValue;
                girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            }
            return girl;
        }

        /// <summary>
        /// Adds the idol to the agency, so the game counts her among its idols.
        /// </summary>
        public static data_girls.girls Hire(data_girls.girls girl)
        {
            data_girls.girl.Add(girl);
            return girl;
        }

        public static float Stat(data_girls.girls girl, data_girls._paramType type) => girl.getParam(type).val;

        public static void SetStat(data_girls.girls girl, data_girls._paramType type, float value) => girl.getParam(type)._val = value;

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their fields.
        /// </summary>
        public static T Component<T>() => (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));

        /// <summary>
        /// Calls a private game method, unwrapping exceptions it throws.
        /// </summary>
        public static object CallPrivate(object instance, Type type, string method, params object[] args)
        {
            try
            {
                return AccessTools.Method(type, method).Invoke(instance, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModAsset(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Traits Expansion", "assets", relativePath);

        /// <summary>
        /// Parses one of the mod's JSON files the way the game does.
        /// </summary>
        public static SimpleJSON.JSONNode LoadJson(string relativePath) =>
            mainScript.ProcessInboundData(File.ReadAllText(ModAsset(relativePath)));
    }

    /// <summary>
    /// Some of the mod's patches call game methods that reach Unity's native code (Random.Range,
    /// notifications, injuries, logging). Those can't run outside the game, so the mod's own methods are
    /// patched to call these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<float, bool> Chance;
        public static Func<int, int, int> Range;

        public static readonly List<float> ChancesRolled = new();
        public static readonly List<(int min, int max)> RangesRolled = new();
        public static readonly List<(data_girls.girls girl, data_girls._paramType type, float val)> ParamsAdded = new();
        public static readonly List<data_girls.girls> Injured = new();
        public static readonly List<string> Notifications = new();
        public static readonly List<string> Warnings = new();

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(float) })] = Stub(nameof(StubChance)),
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })] = Stub(nameof(StubRange)),
            [AccessTools.Method(typeof(NotificationManager), nameof(NotificationManager.AddNotification), new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) })] = Stub(nameof(StubNotification)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam))] = Stub(nameof(StubAddParam)),
            [AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Set_Injured))] = Stub(nameof(StubSetInjured)),
            [AccessTools.Method(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object) })] = Stub(nameof(StubWarning)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(Relationships_Do_Bullying), nameof(Relationships_Do_Bullying.Postfix)),
            AccessTools.Method(typeof(data_girls_girls_Graduation_Set_Default_Date), nameof(data_girls_girls_Graduation_Set_Default_Date.Postfix)),
            AccessTools.Method(typeof(SEvent_Tour_UseStamina), nameof(SEvent_Tour_UseStamina.Postfix)),
            AccessTools.Method(typeof(SEvent_Concerts__concert_Finish), nameof(SEvent_Concerts__concert_Finish.Postfix)),
            AccessTools.Method(typeof(data_girls_girls_Try_Injury), nameof(data_girls_girls_Try_Injury.Postfix)),
            AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.WarnUnlessModTrait)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony harmony = new("tests.TraitsExpansion.Seams");
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
            ChancesRolled.Clear();
            RangesRolled.Clear();
            ParamsAdded.Clear();
            Injured.Clear();
            Notifications.Clear();
            Warnings.Clear();

            Language.Data["IDOL__BULLIED_UNKNOWN"] = "An idol lost @ mental stamina to bullying.";
            Language.Data["REL__BULLYING_LOST"] = " lost @ mental stamina to bullying.";
            // The mod's own text, so the notification test checks what players read
            SimpleJSON.JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
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

        private static bool StubChance(float num)
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
        private static void StubAddParam(data_girls.girls girl, data_girls._paramType type, float val, bool ignorePotential) => ParamsAdded.Add((girl, type, val));
        private static void StubSetInjured(data_girls.girls girl) => Injured.Add(girl);
        private static void StubWarning(object message) => Warnings.Add(message?.ToString());
    }
}
