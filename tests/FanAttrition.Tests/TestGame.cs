using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

// Every test shares the game's static state (fans, shows, cafes, difficulty, the mod's fan counts) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FanAttrition.Tests
{
    /// <summary>
    /// Builds the game state the mod reads.
    /// </summary>
    public static class TestGame
    {
        /// <summary>
        /// Resets the game state and the mod's state, and loads the mod's text into the game's language table.
        /// </summary>
        public static void Reset()
        {
            Seams.Reset();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            staticVars.PlayerData = new staticVars._playerData { Difficulty = staticVars._playerData._difficulty.normal };
            resources.FansChange = 0L;
            resources.resource = new List<long> { 0L, 0L, 0L, 0L, 0L };
            resources.Fans = new List<resources._fan>();
            data_girls.girl = new List<data_girls.girls>();
            variables.variable = new List<variables._variable>();
            Shows.shows = new List<Shows._show>();
            Cafes.Cafes_ = new List<Cafes._cafe>();

            Utility.adFans = 0;
            Utility.dramaFans = 0;
            Utility.tvFans = 0;
            Utility.radioFans = 0;
            Utility.netFans = 0;
            Utility.cafeFans = 0;
            Utility.RenderFanChangeDelegate = null;

            SimpleJSON.JSONNode constants = LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
            // The game's own text the mod uses, as in its English constants.json
            Language.Data["PER_WEEK"] = "/w";
            Language.Data["TOTAL"] = "Total";
            Language.Data["TIP__CAFE"] = "Cafe";
            Language.Data["TIP__APPEAL"] = "Appeal";
            Language.Data["FAN_M"] = "Male";
            Language.Data["FAN_F"] = "Female";
            Language.Data["FAN_C"] = "Casual";
            Language.Data["FAN_HC"] = "Hardcore";
            Language.Data["FAN_T"] = "Teen";
            Language.Data["FAN_YA"] = "Young Adult";
            Language.Data["FAN_A"] = "Adult";
        }

        public static void SetDifficulty(staticVars._playerData._difficulty difficulty) => staticVars.PlayerData.Difficulty = difficulty;

        /// <summary>
        /// An active idol with these fans, registered with the game. Her appeal to every fan type is appeal.
        /// </summary>
        public static data_girls.girls Idol(float appeal = 0.5f, params resources._fan[] fans)
        {
            data_girls.girls girl = new();
            girl.Fans.AddRange(fans);
            foreach (resources.fanType type in Enum.GetValues(typeof(resources.fanType)))
                girl.FanAppeal.Add(new singles._fanAppeal { type = type, ratio = appeal });
            data_girls.girl.Add(girl);
            return girl;
        }

        public static resources._fan Fans(resources.fanType gender, resources.fanType hardcoreness, resources.fanType age, long people) =>
            new() { gender = gender, hardcoreness = hardcoreness, age = age, people = people };

        /// <summary>
        /// An idol holding this many fans, all of one kind.
        /// </summary>
        public static data_girls.girls FanBase(long people) =>
            Idol(0.5f, Fans(resources.fanType.male, resources.fanType.casual, resources.fanType.teen, people));

        /// <summary>
        /// The business component UpdateFanCount reads through Camera.main.
        /// </summary>
        public static business Business => Seams.ComponentOf<business>(Seams.ComponentOf<mainScript>(Seams.MainCamera).Data);

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
            Path.Combine(RepoRoot(), "mods", "Fan Attrition", relativePath);

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
    /// Some of the mod's code calls game methods that reach Unity's native code (Camera.main, Random,
    /// GameObjects and UI components). Those can't run outside the game, so the mod's methods, and the
    /// game's SetSales, are patched to call these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<int, int, int> Range;

        /// <summary>
        /// The formation stats a single's GetSenbatsuParamValue returns.
        /// </summary>
        public static readonly Dictionary<data_girls._paramType, float> SenbatsuStats = new();

        public static readonly List<(resources.type type, long val)> ResourcesAdded = new();
        public static readonly List<(resources._fan fan, long fans)> FansAddedEqually = new();
        public static readonly List<(object original, GameObject copy)> Instantiated = new();
        public static readonly List<(Transform child, Transform parent, bool worldPositionStays)> Parented = new();
        public static readonly List<(tooltip_fans_line line, string text)> LinesSet = new();
        public static readonly List<RectTransform> LayoutsRebuilt = new();

        public static Camera MainCamera { get; private set; }

        /// <summary>
        /// The fake transforms, components and texts the mod reads and writes.
        /// </summary>
        private static readonly Dictionary<object, Transform> Transforms = new(new SameObject());
        private static readonly Dictionary<object, GameObject> GameObjects = new(new SameObject());
        private static readonly Dictionary<object, Dictionary<Type, object>> Components = new(new SameObject());
        private static readonly Dictionary<object, string> Texts = new(new SameObject());

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main))] = Stub(nameof(StubMainCamera)),
            [AccessTools.Method(typeof(resources), nameof(resources.Add), new[] { typeof(resources.type), typeof(long) })] = Stub(nameof(StubAddResource)),
            [AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) })] = Stub(nameof(StubRange)),
            [AccessTools.Method(typeof(data_girls), nameof(data_girls.AddFans_Equally), new[] { typeof(long), typeof(resources._fan), typeof(List<data_girls.girls>) })] = Stub(nameof(StubAddFansEqually)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))] = Stub(nameof(StubGameObject)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) })] = Stub(nameof(StubSetParent)),
            [AccessTools.PropertySetter(typeof(Text), nameof(Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.Method(typeof(ExtensionMethods), nameof(ExtensionMethods.SetText), new[] { typeof(GameObject), typeof(string) })] = Stub(nameof(StubSetObjectText)),
            [AccessTools.Method(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Set), new[] { typeof(string) })] = Stub(nameof(StubLineSet)),
            [AccessTools.Method(typeof(LayoutRebuilder), nameof(LayoutRebuilder.ForceRebuildLayoutImmediate), new[] { typeof(RectTransform) })] = Stub(nameof(StubRebuild)),
            [AccessTools.Method(typeof(Utility), nameof(Utility.UpdateFanCount))] = Stub(nameof(UpdateFanCount)),
            [AccessTools.Method(typeof(tooltip_fans_Start), "AddLine")] = Stub(nameof(AddLine)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            // Not "tests.FanAttrition": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.FanAttrition.Seams");
            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect));
            MethodInfo redirect = AccessTools.Method(typeof(Seams), nameof(Redirect));

            // These call Camera.main or a GameObject's transform directly, which the tests can't even compile,
            // so Harmony can't patch them in place (it compiles the original first). Tests call copies instead.
            Harmony.ReversePatch(AccessTools.Method(typeof(Utility), nameof(Utility.UpdateFanCount)),
                new HarmonyMethod(typeof(Seams), nameof(UpdateFanCount)), redirect, ilmanipulator: null);
            Harmony.ReversePatch(AccessTools.Method(typeof(tooltip_fans_Render), nameof(tooltip_fans_Render.Prefix)),
                new HarmonyMethod(typeof(Seams), nameof(RenderPrefix)), redirect, ilmanipulator: null);
            Harmony.ReversePatch(AccessTools.Method(typeof(tooltip_fans_Start), "AddLine"),
                new HarmonyMethod(typeof(Seams), nameof(AddLine)), redirect, ilmanipulator: null);
            Harmony.ReversePatch(AccessTools.Method(typeof(tooltip_fans_Start), nameof(tooltip_fans_Start.Prefix)),
                new HarmonyMethod(typeof(Seams), nameof(StartPrefix)), redirect, ilmanipulator: null);

            harmony.Patch(AccessTools.Method(typeof(Utility), nameof(Utility.DailyFanChurn)), transpiler: transpiler);
            harmony.Patch(AccessTools.Method(typeof(resources_OnNewDay), nameof(resources_OnNewDay.Postfix)), transpiler: transpiler);
            harmony.Patch(AccessTools.Method(typeof(tooltip_fans_RenderFanChange), nameof(tooltip_fans_RenderFanChange.Postfix)), transpiler: transpiler);

            // The mod's show patches go on the game's SetSales, as in the game, and the stubs keep its
            // random rolls and fan handouts out of Unity
            harmony.CreateClassProcessor(typeof(Shows__show_SetSales_MC)).Patch();
            harmony.CreateClassProcessor(typeof(Shows__show_SetSales_Fatigue)).Patch();
            harmony.Patch(AccessTools.Method(typeof(Shows._show), "SetSales"), transpiler: transpiler);

            // Unofficial Patch fixes the game's GetSenbatsuParamValue, which ignores the stat asked for. The tests'
            // JIT inlines it (it's tiny) into GetSuccessChance if PatchTargetTests compiled that first, which skips
            // this prefix, so GetSuccessChance is recompiled after it.
            harmony.Patch(AccessTools.Method(typeof(singles._single), nameof(singles._single.GetSenbatsuParamValue)),
                prefix: new HarmonyMethod(typeof(Seams), nameof(SenbatsuParamValue)));
            harmony.Patch(AccessTools.Method(typeof(singles._param), nameof(singles._param.GetSuccessChance), new[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) }),
                transpiler: transpiler);
            return true;
        });

        /// <summary>
        /// Installs the stubs once per test run and clears what earlier tests recorded.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            Range = (min, _) => min;
            SenbatsuStats.Clear();
            ResourcesAdded.Clear();
            FansAddedEqually.Clear();
            Instantiated.Clear();
            Parented.Clear();
            LinesSet.Clear();
            LayoutsRebuilt.Clear();
            Transforms.Clear();
            GameObjects.Clear();
            Components.Clear();
            Texts.Clear();

            MainCamera = TestGame.Component<Camera>();
            ComponentOf<mainScript>(MainCamera).Data = TestGame.Component<GameObject>();
            ComponentOf<business>(ComponentOf<mainScript>(MainCamera).Data).ActiveProposals = new List<business.active_proposal>();
        }

        /// <summary>
        /// Copies of the mod's methods that reach Unity directly, with the stubs swapped in.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateFanCount() => throw new InvalidOperationException("Call Seams.Reset first");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool RenderPrefix(tooltip_fans __instance) => throw new InvalidOperationException("Call Seams.Reset first");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void StartPrefix(tooltip_fans __instance) => throw new InvalidOperationException("Call Seams.Reset first");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddLine(tooltip_fans instance, string txt) => throw new InvalidOperationException("Call Seams.Reset first");

        public static Transform TransformOf(object owner)
        {
            if (!Transforms.TryGetValue(owner, out Transform transform))
                Transforms[owner] = transform = TestGame.Component<Transform>();
            return transform;
        }

        public static GameObject GameObjectOf(object component)
        {
            if (!GameObjects.TryGetValue(component, out GameObject gameObject))
                GameObjects[component] = gameObject = TestGame.Component<GameObject>();
            return gameObject;
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

        public static string TextOf(object owner) => Texts.TryGetValue(owner, out string value) ? value : null;

        /// <summary>
        /// The Text of every line added under this GameObject, in the order they were added.
        /// </summary>
        public static Text[] LineTexts(GameObject parent)
        {
            Transform parentTransform = TransformOf(parent);
            return Parented
                .Where(p => ReferenceEquals(p.parent, parentTransform))
                .Select(p => Instantiated.Single(i => ReferenceEquals(TransformOf(i.copy), p.child)).copy)
                .Select(line => ComponentOf<Text>(line))
                .ToArray();
        }

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo generic && generic.IsGenericMethod)
                {
                    // Native generic calls; the JIT can inline them, so every one is redirected
                    string stub = generic.Name switch
                    {
                        nameof(GameObject.GetComponent) when generic.GetParameters().Length == 0 => nameof(StubGetComponent),
                        nameof(GameObject.GetComponentsInChildren) when generic.GetParameters().Length == 0 => nameof(StubGetComponentsInChildren),
                        nameof(Object.Instantiate) when generic.GetParameters().Length == 1 => nameof(StubInstantiate),
                        _ => null,
                    };
                    if (stub != null && (generic.DeclaringType == typeof(GameObject) || generic.DeclaringType == typeof(Component) || generic.DeclaringType == typeof(Object)))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = AccessTools.Method(typeof(Seams), stub).MakeGenericMethod(generic.GetGenericArguments());
                    }
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

        private static bool SenbatsuParamValue(data_girls._paramType Type, ref float __result)
        {
            __result = SenbatsuStats.TryGetValue(Type, out float value) ? value : 0f;
            return false;
        }

        private static Camera StubMainCamera() => MainCamera;

        /// <summary>
        /// Adds fans the way the game does, to the first idol's first fan group, never below zero.
        /// </summary>
        private static void StubAddResource(resources.type type, long val)
        {
            ResourcesAdded.Add((type, val));
            if (type == resources.type.fans && val != 0)
            {
                resources._fan fans = data_girls.girl.First().Fans.First();
                fans.people = Math.Max(0, fans.people + val);
            }
        }

        private static int StubRange(int min, int max)
        {
            int roll = Range(min, max);
            Assert.InRange(roll, min, max - 1);
            return roll;
        }

        private static void StubAddFansEqually(long total_fans, resources._fan _Fan, List<data_girls.girls> Girls) => FansAddedEqually.Add((_Fan, total_fans));
        private static bool StubEquality(Object x, Object y) => ReferenceEquals(x, y);
        private static GameObject StubGameObject(Component component) => GameObjectOf(component);
        private static Transform StubTransform(object owner) => TransformOf(owner);
        private static void StubSetParent(Transform child, Transform parent, bool worldPositionStays) => Parented.Add((child, parent, worldPositionStays));
        private static void StubSetText(Text text, string value) => Texts[text] = value;
        private static void StubSetObjectText(GameObject obj, string text) => Texts[obj] = text;
        private static void StubLineSet(tooltip_fans_line line, string txt) => LinesSet.Add((line, txt));
        private static void StubRebuild(RectTransform layoutRoot) => LayoutsRebuilt.Add(layoutRoot);
        private static T StubGetComponent<T>(object owner) => ComponentOf<T>(owner);
        private static T[] StubGetComponentsInChildren<T>(GameObject owner) => LineTexts(owner).Cast<T>().ToArray();

        private static T StubInstantiate<T>(T original)
        {
            GameObject copy = TestGame.Component<GameObject>();
            Instantiated.Add((original, copy));
            return (T)(object)copy;
        }
    }
}
