using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

// Every test shares the game's static state (idols, groups, theaters, policies, date) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Builds the game state the patches read, and applies the mod to the game's methods.
    /// </summary>
    public static class TestGame
    {
        public static readonly DateTime Today = new(2023, 6, 5);

        /// <summary>
        /// Patch classes that couldn't be applied outside the game (their targets call Unity native code).
        /// Those are tested by calling the patch directly.
        /// </summary>
        public static readonly List<Type> NotApplied = new();

        /// <summary>
        /// What the mod logged while it was being applied, e.g. a transpiler not finding its target.
        /// </summary>
        public static readonly List<string> PatchingLog = new();

        private static readonly Lazy<bool> Patched = new(() =>
        {
            Log.Install();
            Seams.Install();

            // Not "tests.UnofficialPatch": PatchTargetTests unpatches everything under that ID
            Harmony harmony = new("tests.UnofficialPatch.Behaviour");
            foreach (Type patchClass in TelModTests.Common.PatchTargetAssert.PatchClasses(typeof(Theaters_CompleteDay).Assembly, "UnofficialPatch"))
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }
                catch (HarmonyException e) when (e.InnerException is System.Security.SecurityException)
                {
                    NotApplied.Add(patchClass);
                }
            }
            PatchingLog.AddRange(Log.Messages.Select(m => m.type + ": " + m.message));
            return true;
        });

        /// <summary>
        /// Resets the game state, the seams and the log, with the mod applied to the game's methods.
        /// </summary>
        public static void Reset()
        {
            _ = Patched.Value;
            Seams.Reset();
            Log.Messages.Clear();

            staticVars.dateTime = Today;
            staticVars.PlayerData.Difficulty = staticVars._playerData._difficulty.normal;
            data_girls.girl = new List<data_girls.girls>();
            singles.Singles = new List<singles._single>();
            Groups.Groups_ = new List<Groups._group> { new() { ID = 0, Girls = new List<data_girls.girls>() } };
            Relationships.RelationshipsData = new List<Relationships._relationship>();
            Theaters.Theaters_ = new List<Theaters._theater>();
            Cafes.Cafes_ = new List<Cafes._cafe>();
            staff.Staff = new List<staff._staff>();
            resources.resource = new List<long>(new long[32]);
            resources.Fans = new List<resources._fan>();
            Awards._Awards = new List<Awards._award>();
            variables.variable = new List<variables._variable>();
            SetPolicies(policies._value.performances_neutral);
        }

        /// <summary>
        /// Selects this performance policy, and a vibe that boosts Sexy only.
        /// </summary>
        public static void SetPolicies(policies._value performances)
        {
            policies.Values = new List<policies.value>
            {
                new() { Type = policies._type.performances, Value = performances, Selected = true },
                new() { Type = policies._type.vibe, Value = policies._value.vibe_sexy, Selected = true },
            };
        }

        /// <summary>
        /// The main group, which every hired idol joins.
        /// </summary>
        public static Groups._group MainGroup => Groups.Groups_[0];

        /// <summary>
        /// An active idol aged 20. Every stat starts at statValue, stamina at 100, fame and scandal points at 0.
        /// </summary>
        public static data_girls.girls Idol(float statValue = 50f, string name = "Idol", int age = 20)
        {
            data_girls.girls girl = new() { nickname = name, birthday = Today.AddYears(-age).AddDays(-1) };
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

        /// <summary>
        /// Adds the idol to the agency and its main group.
        /// </summary>
        public static data_girls.girls Hire(data_girls.girls girl)
        {
            data_girls.girl.Add(girl);
            MainGroup.Girls.Add(girl);
            return girl;
        }

        public static float Stat(data_girls.girls girl, data_girls._paramType type) => girl.getParam(type).val;

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test don't read their other fields.
        /// </summary>
        public static T Component<T>() => (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));

        /// <summary>
        /// A component Unity's == operator treats as alive, for code that checks it against null.
        /// </summary>
        public static T LiveComponent<T>() where T : Object
        {
            T component = Component<T>();
            AccessTools.Field(typeof(Object), "m_CachedPtr").SetValue(component, new IntPtr(1));
            return component;
        }

        /// <summary>
        /// Makes a live component read as destroyed under Unity's == operator.
        /// </summary>
        public static void Kill(Object component) => AccessTools.Field(typeof(Object), "m_CachedPtr").SetValue(component, IntPtr.Zero);

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
    }

    public class HangException : Exception
    {
    }

    /// <summary>
    /// Compares Unity objects by reference. Objects made outside Unity all equal each other (and null) under
    /// Unity's own Equals, because none of them has a native object.
    /// </summary>
    public class SameObject<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Unity's logger writes through native code. The tests swap in a handler that records messages,
    /// so the mod's warnings (e.g. a transpiler not finding its target) can be checked.
    /// </summary>
    public class Log : ILogHandler
    {
        public static readonly List<(LogType type, string message)> Messages = new();

        public static void Install() => Debug.unityLogger.logHandler = new Log();

        public static IEnumerable<string> Of(LogType type) => Messages.Where(m => m.type == type).Select(m => m.message);

        public void LogFormat(LogType logType, Object context, string format, params object[] args) =>
            Messages.Add((logType, string.Format(format, args)));

        public void LogException(Exception exception, Object context) =>
            Messages.Add((LogType.Exception, exception.ToString()));
    }

    /// <summary>
    /// Some patched methods reach Unity's native code (the camera, UI components, notifications, Random).
    /// Those can't run outside the game, so the methods are patched to call these stubs instead.
    /// </summary>
    public static class Seams
    {
        public static Func<int, bool> Chance;

        /// <summary>
        /// Unity's Random.Range(int, int), and the points the stat roll asks Auditions.GetPointsByType for.
        /// </summary>
        public static Func<int, int, int> Range;
        public static Func<Auditions.data._girl._type, int> Points;

        /// <summary>
        /// Range calls since the last Reset. The game's stat roll spins forever when its points can't fit;
        /// past this limit the stub throws instead, so tests can detect the hang.
        /// </summary>
        public static int RangeCalls;
        public const int HangLimit = 1_000_000;

        public static readonly List<long> MoneyAdded = new();
        public static readonly List<Floats.type> FloatsShown = new();
        public static readonly List<(data_girls.girls girl, data_girls._paramType type, float val)> ParamsAdded = new();
        public static readonly List<string> Notifications = new();
        public static readonly List<(GameObject obj, Color32 color)> ColorsSet = new();

        /// <summary>
        /// Fill amounts of the stub Images the fan pie patch reads and writes, by the GameObject that holds them.
        /// </summary>
        public static readonly Dictionary<GameObject, Image> Images = new(new SameObject<GameObject>());
        public static readonly Dictionary<Image, float> Fills = new(new SameObject<Image>());

        /// <summary>
        /// Sprites of the stub Images, other components of GameObjects, and the components their children hold.
        /// </summary>
        public static readonly Dictionary<Image, Sprite> Sprites = new(new SameObject<Image>());
        public static readonly Dictionary<GameObject, List<object>> Components = new(new SameObject<GameObject>());
        public static readonly Dictionary<GameObject, List<object>> Children = new(new SameObject<GameObject>());

        /// <summary>
        /// Coroutines started and objects destroyed since the last Reset, and the real-time clock.
        /// </summary>
        public static readonly List<(MonoBehaviour host, IEnumerator routine)> Coroutines = new();
        public static readonly List<Object> Destroyed = new();
        public static float Now;

        private static readonly (MethodBase target, string stub)[] Redirects =
        {
            (AccessTools.Method(typeof(Theaters._theater), nameof(Theaters._theater.GetRoom)), nameof(StubGetRoom)),
            (AccessTools.Method(typeof(agency._room), nameof(agency._room.addFloat)), nameof(StubAddFloat)),
            (AccessTools.Method(typeof(resources), nameof(resources.Add), new[] { typeof(resources.type), typeof(long) }), nameof(StubAddResource)),
            (AccessTools.Method(typeof(mainScript), nameof(mainScript.chance), new[] { typeof(int) }), nameof(StubChance)),
            (AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.addParam)), nameof(StubAddParam)),
            (AccessTools.Method(typeof(NotificationManager), nameof(NotificationManager.AddNotification), new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) }), nameof(StubNotification)),
            (AccessTools.Method(typeof(ExtensionMethods), nameof(ExtensionMethods.SetColor), new[] { typeof(GameObject), typeof(Color32) }), nameof(StubSetColor)),
            (AccessTools.PropertyGetter(typeof(Image), nameof(Image.fillAmount)), nameof(StubGetFill)),
            (AccessTools.PropertySetter(typeof(Image), nameof(Image.fillAmount)), nameof(StubSetFill)),
            (AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) }), nameof(StubRange)),
            (AccessTools.Method(typeof(Auditions), nameof(Auditions.GetPointsByType)), nameof(StubPoints)),
            (AccessTools.PropertyGetter(typeof(Image), nameof(Image.sprite)), nameof(StubGetSprite)),
            (AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite)), nameof(StubSetSprite)),
            (AccessTools.Method(typeof(MonoBehaviour), nameof(MonoBehaviour.StartCoroutine), new[] { typeof(IEnumerator) }), nameof(StubStartCoroutine)),
            (AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) }), nameof(StubDestroy)),
        };

        private static readonly MethodBase[] Patched =
        {
            AccessTools.Method(typeof(Theaters), "CompleteDay"),
            AccessTools.Method(typeof(Theaters_CompleteDay), nameof(Theaters_CompleteDay.Postfix)),
            AccessTools.Method(typeof(Relationships._relationship), nameof(Relationships._relationship.BreakUp)),
            AccessTools.Method(typeof(Profile_Fans_Pies_Render_Pies), nameof(Profile_Fans_Pies_Render_Pies.Postfix)),
            AccessTools.Method(typeof(Tour_New_Popup_Render), nameof(Tour_New_Popup_Render.Postfix)),
            AccessTools.Method(typeof(data_girls), "GenerateParams"),
            AccessTools.Method(typeof(data_girls), "GeneratePotential"),
            AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.ShowOpenedPortrait)),
            AccessTools.Method(typeof(PortraitLoading), "SetSprite"),
            AccessTools.Method(typeof(PortraitLoading), nameof(PortraitLoading.DestroyRenderer)),
            AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(PortraitLoading), "FinishLateRender")),
            AccessTools.Method(typeof(Popup_Audition_PortraitsLoaded), nameof(Popup_Audition_PortraitsLoaded.Postfix)),
            AccessTools.Method(typeof(Popup_Audition_OpenCard), nameof(Popup_Audition_OpenCard.Postfix)),
        };

        /// <summary>
        /// Installs the stubs once per test run. They run after the mod's own transpilers, which look for the
        /// game's original calls.
        /// </summary>
        public static void Install()
        {
            Harmony harmony = new("tests.UnofficialPatch.Seams");

            HarmonyMethod transpiler = new(typeof(Seams), nameof(Redirect)) { priority = Priority.Last };
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

            // The game's stat roll as it is without the mod, for comparison
            Harmony.ReversePatch(AccessTools.Method(typeof(data_girls), "GenerateParams"),
                new HarmonyMethod(typeof(Seams), nameof(VanillaGenerateParams)), AccessTools.Method(typeof(Seams), nameof(Redirect)), ilmanipulator: null);
        }

        /// <summary>
        /// Clears what earlier tests recorded.
        /// </summary>
        public static void Reset()
        {
            Chance = _ => false;
            Range = (min, max) => throw new InvalidOperationException("Set Seams.Range first");
            Points = _ => throw new InvalidOperationException("Set Seams.Points first");
            RangeCalls = 0;
            MoneyAdded.Clear();
            FloatsShown.Clear();
            ParamsAdded.Clear();
            Notifications.Clear();
            ColorsSet.Clear();
            Images.Clear();
            Fills.Clear();
            Sprites.Clear();
            Components.Clear();
            Children.Clear();
            Coroutines.Clear();
            Destroyed.Clear();
            Now = 0f;
            PortraitLoading.Clock = () => Now;
        }

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
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo inChildren
                    && inChildren.Name == nameof(GameObject.GetComponentsInChildren) && inChildren.IsGenericMethod
                    && inChildren.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(bool) })
                    && inChildren.DeclaringType == typeof(GameObject))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Seams), nameof(StubGetComponentsInChildren)).MakeGenericMethod(inChildren.GetGenericArguments());
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo shuffle
                    && shuffle.DeclaringType == typeof(ExtensionMethods) && shuffle.Name == nameof(ExtensionMethods.Shuffle))
                {
                    // Shuffle draws from Random.Range inside the game's assembly
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Seams), nameof(StubShuffle)).MakeGenericMethod(shuffle.GetGenericArguments());
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodBase target)
                {
                    foreach ((MethodBase from, string stub) in Redirects)
                    {
                        if (SameMethod(target, from))
                        {
                            instruction.opcode = OpCodes.Call;
                            instruction.operand = AccessTools.Method(typeof(Seams), stub);
                            break;
                        }
                    }
                }
                yield return instruction;
            }
        }

        private static bool SameMethod(MethodBase a, MethodBase b)
        {
            if (a == b)
                return true;
            // Constructed generic methods (GetComponent<T>) aren't always the same MethodInfo instance
            return a.DeclaringType == b.DeclaringType && a.Name == b.Name
                && a.IsGenericMethod && b.IsGenericMethod
                && a.GetGenericArguments().SequenceEqual(b.GetGenericArguments());
        }

        private static agency._room StubGetRoom(Theaters._theater theater) => null;

        private static void StubAddFloat(agency._room room, Floats.type floatType, string str, bool up, GameObject targetObj, float paddingTop, float lifeSpan, float delay, Color32? color) =>
            FloatsShown.Add(floatType);

        private static void StubAddResource(resources.type type, long val)
        {
            if (type == resources.type.money)
                MoneyAdded.Add(val);
        }

        private static bool StubChance(int num) => Chance(num);

        /// <summary>
        /// The game's own data_girls.GenerateParams, without the mod's patch, calling the stubs.
        /// </summary>
        public static void VanillaGenerateParams(data_girls instance, data_girls.girls Girl, Auditions.data._girl._type Type) =>
            throw new NotImplementedException("Replaced by a reverse patch");

        /// <summary>
        /// Draws every Range roll, and every chance roll, from a seeded generator, the way the game does.
        /// </summary>
        public static void Seeded(int seed)
        {
            System.Random rng = new(seed);
            Range = (min, max) => rng.Next(min, max);
            Chance = num => num >= 100 || (num > 0 && StubRange(0, 100) < num);
            RangeCalls = 0;
        }

        private static int StubRange(int min, int max)
        {
            if (++RangeCalls > HangLimit)
                throw new HangException();
            return Range(min, max);
        }

        private static int StubPoints(Auditions.data._girl._type type) => Points(type);

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
        private static void StubAddParam(data_girls.girls girl, data_girls._paramType type, float val, bool ignorePotential) => ParamsAdded.Add((girl, type, val));
        private static void StubNotification(string text, Color32 color, NotificationManager._notification._type type) => Notifications.Add(text);
        private static void StubSetColor(GameObject obj, Color32 color) => ColorsSet.Add((obj, color));

        /// <summary>
        /// A GameObject holding a stub Image with this fill.
        /// </summary>
        public static GameObject ImageObject(float fill)
        {
            GameObject obj = TestGame.Component<GameObject>();
            Image image = TestGame.Component<Image>();
            Images[obj] = image;
            Fills[image] = fill;
            return obj;
        }

        public static float Fill(GameObject obj) => Fills[Images[obj]];

        /// <summary>
        /// The stub Image of a GameObject made by ImageObject; any other component is a blank one.
        /// </summary>
        private static T StubGetComponent<T>(Object owner)
        {
            if (typeof(T) == typeof(Image) && owner is GameObject obj && Images.TryGetValue(obj, out Image image))
                return (T)(object)image;
            if (owner is GameObject holder && Components.TryGetValue(holder, out List<object> components) && components.OfType<T>().Any())
                return components.OfType<T>().First();
            return TestGame.Component<T>();
        }

        private static T[] StubGetComponentsInChildren<T>(GameObject owner, bool includeInactive) =>
            Children.TryGetValue(owner, out List<object> components) ? components.OfType<T>().ToArray() : new T[0];

        private static float StubGetFill(Image image) => Fills[image];
        private static void StubSetFill(Image image, float value) => Fills[image] = value;

        private static Sprite StubGetSprite(Image image) => Sprites.TryGetValue(image, out Sprite sprite) ? sprite : null;
        private static void StubSetSprite(Image image, Sprite sprite) => Sprites[image] = sprite;

        private static Coroutine StubStartCoroutine(MonoBehaviour host, IEnumerator routine)
        {
            Coroutines.Add((host, routine));
            return null;
        }

        /// <summary>
        /// Records the object, and makes a live one read as destroyed.
        /// </summary>
        private static void StubDestroy(Object obj)
        {
            Destroyed.Add(obj);
            if (obj is not null)
                TestGame.Kill(obj);
        }

        /// <summary>
        /// A live GameObject holding a live stub Image with this sprite (null for none).
        /// </summary>
        public static GameObject ImageWithSprite(Sprite sprite = null)
        {
            GameObject obj = TestGame.LiveComponent<GameObject>();
            Image image = TestGame.LiveComponent<Image>();
            Images[obj] = image;
            Sprites[image] = sprite;
            return obj;
        }

        public static Sprite SpriteOf(GameObject obj) => Sprites.TryGetValue(Images[obj], out Sprite sprite) ? sprite : null;

        /// <summary>
        /// A live GameObject whose GetComponent returns these components.
        /// </summary>
        public static GameObject Holding(params object[] components)
        {
            GameObject obj = TestGame.LiveComponent<GameObject>();
            Components[obj] = components.ToList();
            return obj;
        }

    }
}
