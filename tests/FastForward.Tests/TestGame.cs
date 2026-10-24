using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

// Every test shares the game's static state (variables, time speed, hotkey blockers) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FastForward.Tests
{
    /// <summary>
    /// A game with the three time control buttons, running at normal speed, with nothing blocking hotkeys.
    /// </summary>
    public static class TestGame
    {
        public static mainScript Main;

        public static void Reset()
        {
            Seams.Reset();
            SetMultiplier(null);
            PopupManager.PopupCounter = 0;
            ActiveDialogueController.ShowingDialogue = false;
            DEBUG.ShowingPopup = false;

            Main = Seams.Component<mainScript>();
            Main.TimeControls_Pause = Seams.Component<GameObject>();
            Main.TimeControls_Normal = Seams.Component<GameObject>();
            Main.TimeControls_Fast = Seams.Component<GameObject>();
            Seams.MainCamera = Seams.Component<Camera>();
            Seams.SetComponent(Seams.MainCamera, Main);
            Seams.TimeSetState(Main, mainScript._time_state.normal);
        }

        /// <summary>
        /// Sets the speed setting; null removes it. variables.Set needs the game running, so this edits the list directly.
        /// </summary>
        public static void SetMultiplier(string value)
        {
            variables.variable.RemoveAll(v => v.name == FastForward.VARID);
            if (value != null)
                variables.variable.Add(new variables._variable { name = FastForward.VARID, value = value });
        }

        /// <summary>
        /// Clicks a time control button: the mod's prefix, TimeControlButton.OnClick's body, then the mod's postfix.
        /// runOriginal false is another mod's prefix blocking the click, so the body doesn't run.
        /// </summary>
        public static void Click(mainScript._time_state type, bool runOriginal = true)
        {
            TimeControlButton button = Seams.Component<TimeControlButton>();
            button.Type = type;
            Seams.OnClickPrefix(button, runOriginal, out bool state);
            if (runOriginal)
                Seams.GameTimeSetState(Main, type);
            Seams.OnClickPostfix(state, runOriginal);
        }

        /// <summary>
        /// Runs a frame of Controls.Update with this key pressed. Vanilla's Update has no '4' hotkey, so only the mod's postfix runs.
        /// runOriginal false is another mod's prefix skipping Update.
        /// </summary>
        public static void Press(KeyCode key, bool runOriginal = true)
        {
            Seams.KeysDown.Clear();
            Seams.KeysDown.Add(key);
            Seams.UpdatePostfix(runOriginal);
            Seams.KeysDown.Clear();
        }

        public static Color LabelColor(GameObject button) => Seams.ColorOf(Seams.ComponentOf<TextMeshProUGUI>(button));
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
    /// The time control patches reach Unity's native code (Camera.main, Input, GetComponent, text colours),
    /// which can't run outside the game. Harmony can't patch them in place either, because it compiles the
    /// originals first and Camera.main can't be compiled. So tests call copies of the patches, the mod's
    /// helpers and vanilla's Time_SetState, which call these stubs instead.
    /// </summary>
    public static class Seams
    {
        /// <summary>
        /// What Camera.main returns.
        /// </summary>
        public static Camera MainCamera;

        /// <summary>
        /// The keys Input.GetKeyDown reports as pressed.
        /// </summary>
        public static readonly HashSet<KeyCode> KeysDown = new();

        /// <summary>
        /// Another mod's prefix blocking mainScript.Time_SetState, so the game's time state doesn't change.
        /// </summary>
        public static bool TimeSetStateBlocked;

        private static readonly Dictionary<object, Dictionary<Type, object>> Components = new(new SameObject());
        private static readonly Dictionary<object, Color> Colors = new(new SameObject());

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main))] = Stub(nameof(StubMainCamera)),
            [AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })] = Stub(nameof(StubGetKeyDown)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.Method(typeof(Object), "op_Inequality")] = Stub(nameof(StubInequality)),
            [AccessTools.PropertySetter(typeof(Graphic), nameof(Graphic.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.Time_SetState))] = Stub(nameof(GameTimeSetState)),
            [AccessTools.Method(typeof(FastForward), nameof(FastForward.ApplySuperFast))] = Stub(nameof(ApplySuperFast)),
            [AccessTools.Method(typeof(FastForward), nameof(FastForward.SetSuperSpeed))] = Stub(nameof(SetSuperSpeed)),
            [AccessTools.Method(typeof(FastForward), nameof(FastForward.SetFastButtonColor))] = Stub(nameof(SetFastButtonColor)),
            [AccessTools.Method(typeof(ModMenusKeys), nameof(ModMenusKeys.Pressed))] = Stub(nameof(Pressed)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Copy(AccessTools.Method(typeof(mainScript), nameof(mainScript.Time_SetState)), nameof(TimeSetState));
            Copy(AccessTools.Method(typeof(FastForward), nameof(FastForward.SetFastButtonColor)), nameof(SetFastButtonColor));
            Copy(AccessTools.Method(typeof(FastForward), nameof(FastForward.ApplySuperFast)), nameof(ApplySuperFast));
            Copy(AccessTools.Method(typeof(FastForward), nameof(FastForward.SetSuperSpeed)), nameof(SetSuperSpeed));
            Copy(AccessTools.Method(typeof(TimeControlButton_OnClick), nameof(TimeControlButton_OnClick.Prefix)), nameof(OnClickPrefix));
            Copy(AccessTools.Method(typeof(TimeControlButton_OnClick), nameof(TimeControlButton_OnClick.Postfix)), nameof(OnClickPostfix));
            Copy(AccessTools.Method(typeof(Controls_Update), nameof(Controls_Update.Postfix)), nameof(UpdatePostfix));
            Copy(AccessTools.Method(typeof(ModMenusKeys), nameof(ModMenusKeys.Pressed)), nameof(Pressed));
            return true;
        });

        private static void Copy(MethodBase original, string copy) =>
            Harmony.ReversePatch(original, new HarmonyMethod(typeof(Seams), copy), AccessTools.Method(typeof(Seams), nameof(Redirect)), ilmanipulator: null);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TimeSetState(mainScript instance, mainScript._time_state state) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetFastButtonColor(Color32 color) => throw NotInstalled();

        /// <summary>
        /// The game's Time_SetState as the mod sees it: vanilla's, unless another mod blocks it.
        /// </summary>
        public static void GameTimeSetState(mainScript instance, mainScript._time_state state)
        {
            if (!TimeSetStateBlocked)
                TimeSetState(instance, state);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ApplySuperFast(mainScript main) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetSuperSpeed() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnClickPrefix(TimeControlButton __instance, bool __runOriginal, out bool __state) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnClickPostfix(bool __state, bool __runOriginal) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdatePostfix(bool __runOriginal) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Pressed(string varID, KeyCode key) => throw NotInstalled();

        private static Exception NotInstalled() => new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the stubs once per test run and clears the fake objects of earlier tests.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            MainCamera = null;
            KeysDown.Clear();
            TimeSetStateBlocked = false;
            Components.Clear();
            Colors.Clear();
            // No ModMenus: it isn't loaded in tests
            ModMenusKeys.Reset(looked: true);
        }

        /// <summary>
        /// The game's components are MonoBehaviours, which can't be constructed outside Unity.
        /// The methods under test only read the fields the tests set.
        /// </summary>
        public static T Component<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        /// <summary>
        /// The fake component GetComponent returns for this owner.
        /// </summary>
        public static T ComponentOf<T>(object owner)
        {
            if (!Components.TryGetValue(owner, out Dictionary<Type, object> components))
                Components[owner] = components = new Dictionary<Type, object>();
            if (!components.TryGetValue(typeof(T), out object component))
                components[typeof(T)] = component = Component<T>();
            return (T)component;
        }

        public static void SetComponent<T>(object owner, T component)
        {
            if (!Components.TryGetValue(owner, out Dictionary<Type, object> components))
                Components[owner] = components = new Dictionary<Type, object>();
            components[typeof(T)] = component;
        }

        public static Color ColorOf(Graphic text)
        {
            Assert.True(Colors.TryGetValue(text, out Color color), "The text's colour was never set");
            return color;
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

        private static Camera StubMainCamera() => MainCamera;
        private static bool StubGetKeyDown(KeyCode key) => KeysDown.Contains(key);
        private static bool StubEquality(Object a, Object b) => ReferenceEquals(a, b);
        private static bool StubInequality(Object a, Object b) => !ReferenceEquals(a, b);
        private static T StubGetComponent<T>(object owner) => ComponentOf<T>(owner);
        private static void StubSetColor(object text, Color color) => Colors[text] = color;
    }
}
