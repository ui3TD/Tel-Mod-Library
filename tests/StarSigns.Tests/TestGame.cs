using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;
using static StarSigns.StarSigns;
using Object = UnityEngine.Object;

// Every test shares the game's static state (idols, relationships, cliques, pushes, date) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StarSigns.Tests
{
    /// <summary>
    /// Builds the game state the star sign code reads, and applies the mod to the game's methods.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);

        /// <summary>
        /// A birthday well inside each sign.
        /// </summary>
        private static readonly Dictionary<Zodiac, (int month, int day)> MidSign = new()
        {
            [Zodiac.Capricorn] = (1, 5),
            [Zodiac.Aquarius] = (2, 5),
            [Zodiac.Pisces] = (3, 5),
            [Zodiac.Aries] = (4, 5),
            [Zodiac.Taurus] = (5, 5),
            [Zodiac.Gemini] = (6, 5),
            [Zodiac.Cancer] = (7, 5),
            [Zodiac.Leo] = (8, 5),
            [Zodiac.Virgo] = (9, 5),
            [Zodiac.Libra] = (10, 5),
            [Zodiac.Scorpio] = (11, 5),
            [Zodiac.Sagittarius] = (12, 5),
        };

        public static IEnumerable<Zodiac> Signs => MidSign.Keys;

        private static readonly Lazy<bool> Patched = new(() =>
        {
            // Not "tests.StarSigns": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.StarSigns.Behaviour");
            // Patch the methods the other patched methods call first. The tests' JIT inlines small methods
            // (AddRelationship is 20 bytes of IL) into replacements compiled before them, which skips their
            // patches. The game's Mono JIT only inlines methods under 20 bytes.
            Type[] called = { typeof(data_girls_girls_AddRelationship), typeof(Relationships__relationship_Add), typeof(data_girls_girls_param_GetVal) };
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Relationships_Do_Dynamic).Assembly, "StarSigns")
                .OrderBy(t => !called.Contains(t)))
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }
                catch (HarmonyException e) when (e.InnerException is System.Security.SecurityException)
                {
                    // The target calls Unity native methods, which can't be compiled outside the game
                    // (e.g. Instantiate in Profile_Popup.RenderTab_Extras). Those patches are tested directly.
                }
            }
            return true;
        });

        /// <summary>
        /// Resets the game state and the mod's state. With patched set, the mod is applied to the game's
        /// methods (once per test run), so tests can call them as the game would.
        /// </summary>
        public static void Reset(bool patched = false)
        {
            if (patched)
                _ = Patched.Value;
            Seams.Reset();

            staticVars.dateTime = Today;
            data_girls.girl = new List<data_girls.girls>();
            Relationships.RelationshipsData = new List<Relationships._relationship>();
            Relationships.Cliques = new List<Relationships._clique>();
            Pushes.Girls = new List<data_girls.girls> { null, null, null };
            Pushes.Days = new List<int> { 0, 0, 0 };
            AccessTools.Field(typeof(Pushes), "GirlsLastDay").SetValue(null, new List<data_girls.girls> { null, null, null });

            ZodiacTextureReferenceList.Clear();
            patchGetVal = false;
            patchAddRelationship = false;
        }

        /// <summary>
        /// A birthday in this sign that makes an idol this old today.
        /// </summary>
        public static DateTime Birthday(Zodiac sign, int age = 20)
        {
            (int month, int day) = MidSign[sign];
            DateTime birthday = new(Today.Year - age, month, day);
            return birthday > Today.AddYears(-age) ? birthday.AddYears(-1) : birthday;
        }

        /// <summary>
        /// An active idol of this sign, aged 20. Every stat starts at statValue, stamina at 100,
        /// and fame and scandal points at 0.
        /// </summary>
        public static data_girls.girls Idol(Zodiac sign, float statValue = 40f, string name = "Idol")
        {
            data_girls.girls girl = new() { nickname = name, birthday = Birthday(sign) };
            foreach (data_girls._paramType type in Enum.GetValues(typeof(data_girls._paramType)))
            {
                float value = type switch
                {
                    data_girls._paramType.physicalStamina or data_girls._paramType.mentalStamina => 100f,
                    data_girls._paramType.famePoints or data_girls._paramType.scandalPoints => 0f,
                    _ => statValue,
                };
                girl.parameters.Add(new data_girls.girls.param { type = type, _val = value, Parent = girl });
            }
            return girl;
        }

        public static void SetStat(data_girls.girls girl, data_girls._paramType type, float value) => girl.getParam(type)._val = value;

        /// <summary>
        /// A neutral relationship between two idols, registered with the game.
        /// </summary>
        public static Relationships._relationship Pair(data_girls.girls a, data_girls.girls b)
        {
            Relationships._relationship relationship = new();
            relationship.Girls.Add(a);
            relationship.Girls.Add(b);
            Relationships.RelationshipsData.Add(relationship);
            return relationship;
        }

        /// <summary>
        /// A clique of these idols, registered with the game.
        /// </summary>
        public static Relationships._clique Clique(params data_girls.girls[] members)
        {
            Relationships._clique clique = new() { Members = new List<data_girls.girls>(members) };
            Relationships.Cliques.Add(clique);
            return clique;
        }

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their other fields.
        /// </summary>
        public static T Component<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

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

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "Star Signs", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));

        /// <summary>
        /// Parses one of the mod's JSON files the way the game does.
        /// </summary>
        public static SimpleJSON.JSONNode LoadJson(string relativePath) =>
            mainScript.ProcessInboundData(File.ReadAllText(ModAsset(relativePath)));
    }

    /// <summary>
    /// Unity's == treats every fake object as destroyed, and so equal; fakes are told apart by reference.
    /// </summary>
    public class SameObject : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Unity's logger writes through native code; the clique leader code logs the new leader.
    /// </summary>
    public class Log : ILogHandler
    {
        public static readonly List<string> Messages = new();

        public void LogFormat(LogType logType, Object context, string format, params object[] args) =>
            Messages.Add(string.Format(format, args));

        public void LogException(Exception exception, Object context) =>
            Messages.Add(exception.ToString());
    }

    /// <summary>
    /// Some of the mod's patches call game methods that reach Unity's native code (Random, transforms,
    /// UI components). Those can't run outside the game, so the mod's own methods are patched to call
    /// these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<int, bool> Chance;
        public static Func<int, int, int> Range;

        public static readonly List<int> ChancesRolled = new();

        /// <summary>
        /// Range calls since the last Reset. Past this limit the stub throws, so tests can
        /// detect a birthday search that never ends.
        /// </summary>
        public static int RangeCalls;
        public const int HangLimit = 100_000;

        /// <summary>
        /// The fake transforms, children, components and texts the UI patches read and write.
        /// </summary>
        private static readonly Dictionary<object, Transform> Transforms = new(new SameObject());
        private static readonly Dictionary<object, Dictionary<string, Transform>> Children = new(new SameObject());
        private static readonly Dictionary<object, Dictionary<Type, object>> Components = new(new SameObject());
        private static readonly Dictionary<object, string> Texts = new(new SameObject());
        public static readonly List<string> FoundChildren = new();
        public static readonly List<RectTransform> LayoutsRebuilt = new();

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(int) })] = Stub(nameof(StubChance)),
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })] = Stub(nameof(StubRange)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })] = Stub(nameof(StubFind)),
            [AccessTools.PropertyGetter(typeof(TMP_Text), nameof(TMP_Text.text))] = Stub(nameof(StubGetText)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.Method(typeof(LayoutRebuilder), nameof(LayoutRebuilder.ForceRebuildLayoutImmediate))] = Stub(nameof(StubRebuild)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(Relationships_Do_Dynamic), "CheckZodiacBonus"),
            AccessTools.Method(typeof(Relationships__clique_AddBulliedGirl), nameof(Relationships__clique_AddBulliedGirl.Prefix)),
            AccessTools.Method(typeof(data_girls_GenerateGirl), nameof(data_girls_GenerateGirl.Postfix)),
            AccessTools.Method(typeof(Audition_Data_Card_Show), nameof(Audition_Data_Card_Show.Postfix)),
            AccessTools.Method(typeof(Audition_Data_Card_Show_Fast), nameof(Audition_Data_Card_Show_Fast.Postfix)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony harmony = new("tests.StarSigns.Seams");
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            List<string> failed = new();
            foreach (MethodBase method in Patched)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                }
                catch (HarmonyException e)
                {
                    failed.Add(method.DeclaringType.Name + "." + method.Name + ": " + e.InnerException?.Message);
                }
            }
            if (failed.Count > 0)
                throw new InvalidOperationException(string.Join("; ", failed));

            Harmony.ReversePatch(
                AccessTools.Method(typeof(Profile_Popup_RenderTab_Extras), nameof(Profile_Popup_RenderTab_Extras.Postfix)),
                new HarmonyMethod(typeof(Seams), nameof(ProfileExtrasPostfix)),
                AccessTools.Method(typeof(Seams), nameof(Redirect)),
                ilmanipulator: null);

            Debug.unityLogger.logHandler = new Log();
            return true;
        });

        /// <summary>
        /// The profile postfix reads GameObject.transform, an engine call the tests can't even compile, so
        /// Harmony can't patch it in place (it compiles the original first). This is a copy of the postfix
        /// with the same stubs; tests call it instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ProfileExtrasPostfix(Profile_Popup __instance) =>
            throw new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the stubs once per test run, clears what earlier tests recorded, and loads the mod's
        /// sign names and descriptions into the game's language table.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            Chance = _ => false;
            Seeded(1);
            ChancesRolled.Clear();
            Transforms.Clear();
            Children.Clear();
            Components.Clear();
            Texts.Clear();
            FoundChildren.Clear();
            LayoutsRebuilt.Clear();
            Log.Messages.Clear();

            SimpleJSON.JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
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
        /// The fake transform of a GameObject or component.
        /// </summary>
        public static Transform TransformOf(object owner)
        {
            if (!Transforms.TryGetValue(owner, out Transform transform))
                Transforms[owner] = transform = TestGame.Component<Transform>();
            return transform;
        }

        /// <summary>
        /// The fake child Transform.Find returns for this name.
        /// </summary>
        public static Transform Child(Transform parent, string name)
        {
            if (!Children.TryGetValue(parent, out Dictionary<string, Transform> children))
                Children[parent] = children = new Dictionary<string, Transform>();
            if (!children.TryGetValue(name, out Transform child))
                children[name] = child = TestGame.Component<Transform>();
            return child;
        }

        /// <summary>
        /// The fake component GetComponent returns for this owner.
        /// </summary>
        public static T ComponentOf<T>(object owner)
        {
            if (!Components.TryGetValue(owner, out Dictionary<Type, object> components))
                Components[owner] = components = new Dictionary<Type, object>();
            if (!components.TryGetValue(typeof(T), out object component))
                components[typeof(T)] = component = TestGame.Component<T>();
            return (T)component;
        }

        public static string TextOf(TMP_Text text) => Texts.TryGetValue(text, out string value) ? value : "";

        public static void SetText(TMP_Text text, string value) => Texts[text] = value;

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo getComponent
                    && getComponent.Name == nameof(GameObject.GetComponent) && getComponent.IsGenericMethod && getComponent.GetParameters().Length == 0
                    && (getComponent.DeclaringType == typeof(GameObject) || getComponent.DeclaringType == typeof(Component)))
                {
                    // GetComponent<T>() calls native code; the JIT can inline it, so every one is redirected
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Seams), nameof(StubGetComponent)).MakeGenericMethod(getComponent.GetGenericArguments());
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
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
            if (++RangeCalls > HangLimit)
                throw new TimeoutException($"Range called more than {HangLimit} times");
            int roll = Range(min, max);
            Assert.InRange(roll, min, max - 1);
            return roll;
        }

        private static Transform StubTransform(object owner) => TransformOf(owner);

        private static Transform StubFind(Transform parent, string name)
        {
            FoundChildren.Add(name);
            return Child(parent, name);
        }

        private static T StubGetComponent<T>(object owner) => ComponentOf<T>(owner);
        private static string StubGetText(TMP_Text text) => TextOf(text);
        private static void StubSetText(TMP_Text text, string value) => SetText(text, value);
        private static void StubRebuild(RectTransform layoutRoot) => LayoutsRebuilt.Add(layoutRoot);
    }
}
