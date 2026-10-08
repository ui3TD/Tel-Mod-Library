using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;
using TabType = Tabs_Manager._tab._type;

// Every test shares the game's static state (hotkey blockers) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MenuHotkeys.Tests
{
    /// <summary>
    /// A game on the main screen with nothing blocking hotkeys.
    /// </summary>
    public static class TestGame
    {
        /// <summary>
        /// The hotkeys the Steam description promises: key, the menu's name there, and the tab it opens.
        /// </summary>
        public static readonly (KeyCode Key, string Name, TabType Tab)[] Hotkeys =
        {
            (KeyCode.A, "Idols", TabType.idols),
            (KeyCode.S, "Staff", TabType.staff),
            (KeyCode.D, "Activities", TabType.activities),
            (KeyCode.F, "Singles", TabType.singles),
            (KeyCode.G, "Media", TabType.media),
            (KeyCode.H, "Special Events", TabType.specialEvents),
            (KeyCode.J, "Research", TabType.research),
            (KeyCode.K, "Policies", TabType.policies),
        };

        public static IEnumerable<object[]> HotkeyData()
        {
            foreach ((KeyCode key, _, TabType tab) in Hotkeys)
                yield return new object[] { key, tab };
        }

        public static mainScript Main;

        /// <summary>
        /// The tab manager on the game's Data object, which the hotkeys must use.
        /// </summary>
        public static Tabs_Manager Tabs;

        public static void Reset()
        {
            Seams.Reset();
            PopupManager.PopupCounter = 0;
            ActiveDialogueController.ShowingDialogue = false;
            DEBUG.ShowingPopup = false;

            Main = Seams.Component<mainScript>();
            Main.Data = Seams.Component<GameObject>();
            Tabs = Seams.ComponentOf<Tabs_Manager>(Main.Data);
            Seams.MainCamera = Seams.Component<Camera>();
            Seams.SetComponent(Seams.MainCamera, Main);
        }

        /// <summary>
        /// Runs the mod's Controls.Update postfix for a frame with these keys pressed.
        /// </summary>
        public static void Press(params KeyCode[] keys)
        {
            Seams.KeysDown.Clear();
            Seams.KeysDown.UnionWith(keys);
            Seams.UpdatePostfix();
            Seams.KeysDown.Clear();
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
            Path.Combine(RepoRoot(), "mods", "Menu Hotkeys", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
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
    /// The hotkey patch reaches Unity's native code (Camera.main, Input, GetComponent), which can't run
    /// outside the game. Harmony can't patch it in place either, because it compiles the original first
    /// and Camera.main can't be compiled. So tests call a copy of the postfix, which calls these stubs
    /// instead. Tabs_Manager.OpenTab animates the UI, so the copy records which tab it was asked to open.
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
        /// Every OpenTab call: the tab manager it was called on and the tab.
        /// </summary>
        public static readonly List<(Tabs_Manager Manager, TabType Tab)> OpenedTabs = new();

        private static readonly Dictionary<object, Dictionary<Type, object>> Components = new(new SameObject());

        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new()
        {
            [AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main))] = Stub(nameof(StubMainCamera)),
            [AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })] = Stub(nameof(StubGetKeyDown)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.Method(typeof(Object), "op_Inequality")] = Stub(nameof(StubInequality)),
            [AccessTools.Method(typeof(Tabs_Manager), nameof(Tabs_Manager.OpenTab))] = Stub(nameof(StubOpenTab)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            Harmony.ReversePatch(AccessTools.Method(typeof(Controls_Update), nameof(Controls_Update.Postfix)),
                new HarmonyMethod(typeof(Seams), nameof(UpdatePostfix)), AccessTools.Method(typeof(Seams), nameof(Redirect)), ilmanipulator: null);
            return true;
        });

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdatePostfix() => throw new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the stubs once per test run and clears the fake objects of earlier tests.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            MainCamera = null;
            KeysDown.Clear();
            OpenedTabs.Clear();
            Components.Clear();
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
        private static void StubOpenTab(Tabs_Manager manager, TabType tab) => OpenedTabs.Add((manager, tab));
    }
}
