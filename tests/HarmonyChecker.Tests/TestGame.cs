using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

// Every test shares the game's static state (language table) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HarmonyChecker.Tests
{
    /// <summary>
    /// The main menu Harmony Checker relabels: the camera's mainScript, whose Data object holds the
    /// MainMenu_Buttons_Controller, whose Main_Container holds the "Mods" button.
    /// </summary>
    internal class Menu
    {
        public GameObject Camera, Data, Container, ModsButton, Label;
        public mainScript Main;
        public MainMenu_Buttons_Controller Controller;
        public Lang_Button ModsLangButton, SettingsLangButton;
        public Component Text;
        public Button ModsClick;
        public Image ModsBackground;

        /// <summary>
        /// The game's own click handler on the Mods button.
        /// </summary>
        public static void OpenModsList() { }
    }

    internal static class TestGame
    {
        /// <summary>
        /// The button's constant in the game. The mod's constants.json changes its text to say IM-HI isn't
        /// installed, which shows if the mod's JSON loads but its patches don't.
        /// </summary>
        public const string VanillaConstant = "MM_MODS";
        public const string VanillaText = "Mods";
        public const string NotInstalledText = "Mods [IM-HI not installed]";
        public const string PurpleSprite = "purple button";

        /// <summary>
        /// A purple tint like the game's, a little lighter on hover.
        /// </summary>
        public static readonly ColorBlock ModsTints = Tints(
            normal: new Color(0.5f, 0.5f, 0.8f),
            highlighted: new Color(0.6f, 0.6f, 0.8f),
            pressed: new Color(0.25f, 0.25f, 0.4f),
            selected: new Color(0.5f, 0.5f, 0.8f),
            disabled: new Color(0.5f, 0.5f, 0.8f, 0.4f));

        /// <summary>
        /// The game's ColorBlock has no setters for most colours, so this sets its fields.
        /// </summary>
        public static ColorBlock Tints(Color normal, Color highlighted, Color pressed, Color selected, Color disabled)
        {
            object block = ColorBlock.defaultColorBlock;
            foreach ((string field, Color color) in new[] { ("m_NormalColor", normal), ("m_HighlightedColor", highlighted), ("m_PressedColor", pressed), ("m_SelectedColor", selected), ("m_DisabledColor", disabled) })
                AccessTools.Field(typeof(ColorBlock), field).SetValue(block, color);
            return (ColorBlock)block;
        }

        public static void Reset()
        {
            Seams.Reset();
            Language.Data.Clear();
            Language.Data[VanillaConstant] = VanillaText;
            Language.Data["SETTINGS"] = "Settings";
        }

        /// <summary>
        /// Loads the mod's constants into the language table, as the game does once the mod is loaded.
        /// </summary>
        public static void LoadModConstants()
        {
            JSONNode constants = LoadJson("JSON/Constants/constants.json");
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
        }

        /// <summary>
        /// Builds the main menu with the Mods button's label already drawn by the game, from the language
        /// table as it is now. The label's text
        /// component is a TextMeshProUGUI unless another type is given.
        /// </summary>
        public static Menu BuildMenu(Type textType = null)
        {
            Menu menu = new();
            menu.Camera = Seams.NewObject("Main Camera");
            Seams.Add<Camera>(menu.Camera);
            menu.Main = Seams.Add<mainScript>(menu.Camera);
            menu.Data = Seams.NewObject("Data");
            menu.Main.Data = menu.Data;
            menu.Controller = Seams.Add<MainMenu_Buttons_Controller>(menu.Data);
            menu.Container = Seams.NewObject("Main_Container", menu.Data);
            menu.Controller.Main_Container = menu.Container;

            // Other buttons the mod must leave alone, one before the Mods button
            menu.SettingsLangButton = AddButton(menu.Container, "Settings", "SETTINGS", typeof(TextMeshProUGUI));

            menu.ModsButton = Seams.NewObject("Mods", menu.Container);
            menu.Label = Seams.NewObject("Text", menu.ModsButton);
            menu.Text = Seams.AddComponent(menu.Label, textType ?? typeof(TextMeshProUGUI));
            menu.ModsLangButton = Seams.Add<Lang_Button>(menu.Label);
            menu.ModsLangButton.Constant = VanillaConstant;
            Seams.Texts[menu.Text] = Language.Data[VanillaConstant];
            menu.ModsClick = Seams.Add<Button>(menu.ModsButton);
            menu.ModsBackground = Seams.Add<Image>(menu.ModsButton);
            Seams.Tints[menu.ModsClick] = ModsTints;
            Seams.Sprites[menu.ModsBackground] = PurpleSprite;
            menu.ModsClick.onClick = new Button.ButtonClickedEvent();
            menu.ModsClick.onClick.AddListener(Menu.OpenModsList);

            // Another button after Mods, so the update button's place is checked
            AddButton(menu.Container, "Quit", "SETTINGS", typeof(TextMeshProUGUI));
            return menu;
        }

        public static Lang_Button AddButton(GameObject container, string name, string constant, Type textType)
        {
            GameObject button = Seams.NewObject(name, container);
            GameObject label = Seams.NewObject("Text", button);
            Component text = Seams.AddComponent(label, textType);
            Lang_Button langButton = Seams.Add<Lang_Button>(label);
            langButton.Constant = constant;
            Seams.Texts[text] = Language.Data[constant];
            return langButton;
        }

        public static string TextOf(Component text) => Seams.Texts.TryGetValue(text, out string value) ? value : null;

        /// <summary>
        /// The update button Harmony Checker added to the menu, or null.
        /// </summary>
        public static GameObject UpdateButton(Menu menu) => Seams.ChildNamed(menu.Container, IMHIUpdate.BUTTON_NAME);

        public static JSONNode LoadJson(string relativePath) =>
            mainScript.ProcessInboundData(File.ReadAllText(ModAsset(relativePath)));

        public static string ModFile(string name) => Path.Combine(RepoRoot(), "mods", "Harmony Checker", name);

        public static string ModAsset(string name) => Path.Combine(ModFile("assets"), name);

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }
    }

    public class SameObject : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// A fake GameObject: its name, place in the hierarchy, and components.
    /// </summary>
    internal class Node
    {
        public string Name;
        public Node Parent;
        public readonly List<Node> Children = new();
        public readonly List<Component> Components = new();
        public GameObject GameObject;
        public Transform Transform;

        /// <summary>
        /// This object then its descendants, depth first, the order GetComponentInChildren searches.
        /// </summary>
        public IEnumerable<Node> SelfAndDescendants() => new[] { this }.Concat(Children.SelectMany(c => c.SelfAndDescendants()));
    }

    /// <summary>
    /// Harmony Checker finds the Mods button through Unity's scene calls (Camera.main, GetComponent,
    /// transforms) and draws its text, all native code that can't run outside the game. So tests call
    /// copies of the mod's methods and of the game's Lang_Button.ResetText, which call these stubs
    /// instead. The stubs keep a small fake scene.
    /// </summary>
    internal static class Seams
    {
        /// <summary>
        /// What Camera.main returns; null when no camera is tagged MainCamera.
        /// </summary>
        public static Camera MainCamera;

        /// <summary>
        /// The mod's own version lookup, before Reset replaces it with an up-to-date IM-HI.
        /// </summary>
        public static readonly Func<Version> DefaultInstalledVersion = IMHIUpdate.InstalledVersion;

        public static readonly List<string> OpenedUrls = new();
        public static readonly List<GameObject> Instantiated = new();
        public static readonly List<GameObject> Destroyed = new();

        /// <summary>
        /// The colour set on each text and image component.
        /// </summary>
        public static readonly Dictionary<object, Color> Colors = new(new SameObject());

        /// <summary>
        /// Each button's colour tints.
        /// </summary>
        public static readonly Dictionary<object, ColorBlock> Tints = new(new SameObject());

        /// <summary>
        /// Each image's sprite; images start with the game's purple button sprite.
        /// </summary>
        public static readonly Dictionary<object, string> Sprites = new(new SameObject());

        /// <summary>
        /// The text each Text, TextMeshPro or TextMesh component shows.
        /// </summary>
        public static readonly Dictionary<object, string> Texts = new(new SameObject());

        private static readonly Dictionary<object, Node> Nodes = new(new SameObject());

        /// <summary>
        /// The mod's methods under test and the game method they call, each with the stand-in that becomes its copy.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Copies = new()
        {
            [AccessTools.Method(typeof(HarmonyCheckerStatus), nameof(HarmonyCheckerStatus.MarkInstalled))] = Stub(nameof(MarkInstalled)),
            [AccessTools.Method(typeof(MainMenu_Buttons_Controller_Start), nameof(MainMenu_Buttons_Controller_Start.Postfix))] = Stub(nameof(StartPostfix)),
            [AccessTools.Method(typeof(Mods_StopSpinner), nameof(Mods_StopSpinner.Postfix))] = Stub(nameof(StopSpinnerPostfix)),
            [AccessTools.Method(typeof(Lang_Button), nameof(Lang_Button.ResetText))] = Stub(nameof(ResetText)),
            [AccessTools.Method(typeof(IMHIUpdate), nameof(IMHIUpdate.ShowButton))] = Stub(nameof(ShowButton)),
        };

        /// <summary>
        /// What the copies call instead: each other, and stubs for the engine.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new(Copies)
        {
            [AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main))] = Stub(nameof(StubMainCamera)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.Method(typeof(Object), "op_Inequality")] = Stub(nameof(StubInequality)),
            [AccessTools.Method(typeof(Object), "op_Implicit")] = Stub(nameof(StubExists)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })] = Stub(nameof(StubFind)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.PropertySetter(typeof(Text), nameof(Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text))] = Stub(nameof(StubSetText)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))] = Stub(nameof(StubGameObject)),
            [AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.parent))] = Stub(nameof(StubParent)),
            [AccessTools.PropertySetter(typeof(Object), nameof(Object.name))] = Stub(nameof(StubSetName)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.GetSiblingIndex))] = Stub(nameof(StubGetSiblingIndex)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.SetSiblingIndex))] = Stub(nameof(StubSetSiblingIndex)),
            [AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) })] = Stub(nameof(StubDestroy)),
            [AccessTools.PropertySetter(typeof(Graphic), nameof(Graphic.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.DeclaredPropertySetter(typeof(TMP_Text), nameof(TMP_Text.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertyGetter(typeof(Selectable), nameof(Selectable.colors))] = Stub(nameof(StubGetTints)),
            [AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite))] = Stub(nameof(StubSetSprite)),
            [AccessTools.PropertySetter(typeof(Selectable), nameof(Selectable.colors))] = Stub(nameof(StubSetTints)),
        };

        /// <summary>
        /// Generic engine methods, redirected whatever their type argument. They reach native code and the JIT
        /// can inline them, so every call goes to a stub.
        /// </summary>
        private static readonly Dictionary<MethodInfo, MethodInfo> GenericRedirects = new()
        {
            [Generic(typeof(Component), nameof(Component.GetComponent))] = Stub(nameof(GetComponent)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponent))] = Stub(nameof(GetComponent)),
            [Generic(typeof(Component), nameof(Component.GetComponentInChildren))] = Stub(nameof(GetComponentInChildren)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponentInChildren))] = Stub(nameof(GetComponentInChildren)),
            [Generic(typeof(Component), nameof(Component.GetComponentsInChildren))] = Stub(nameof(StubGetComponentsInChildren)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponentsInChildren))] = Stub(nameof(StubGetComponentsInChildren)),
            [Generic(typeof(Object), nameof(Object.Instantiate), 2)] = Stub(nameof(StubInstantiate)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            foreach (KeyValuePair<MethodBase, MethodInfo> copy in Copies)
                Harmony.ReversePatch(copy.Key, new HarmonyMethod(copy.Value), Stub(nameof(Redirect)), ilmanipulator: null);
            return true;
        });

        public static void MarkInstalled(MainMenu_Buttons_Controller controller) => throw NotInstalled();

        public static void StartPostfix(ref MainMenu_Buttons_Controller __instance) => throw NotInstalled();

        public static void StopSpinnerPostfix() => throw NotInstalled();

        public static void ResetText(Lang_Button instance) => throw NotInstalled();

        public static void ShowButton(Transform modsTransform, Version installed) => throw NotInstalled();

        private static Exception NotInstalled() => new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the copies once per test run and clears the fake scene of earlier tests.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            MainCamera = null;
            Texts.Clear();
            Nodes.Clear();
            Colors.Clear();
            Tints.Clear();
            Sprites.Clear();
            OpenedUrls.Clear();
            Instantiated.Clear();
            Destroyed.Clear();
            IMHIUpdate.InstalledVersion = () => IMHIUpdate.MIN_VERSION;
            IMHIUpdate.OpenUrl = OpenedUrls.Add;
        }

        /// <summary>
        /// Unity objects can't be constructed outside the game. The code under test only reads the fields the tests set.
        /// </summary>
        public static T Fake<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        public static GameObject NewObject(string name, GameObject parent = null)
        {
            Node node = new() { Name = name, GameObject = Fake<GameObject>(), Transform = Fake<RectTransform>() };
            Nodes[node.GameObject] = node;
            Nodes[node.Transform] = node;
            node.Components.Add(node.Transform);
            if (parent is not null)
            {
                node.Parent = NodeOf(parent);
                node.Parent.Children.Add(node);
            }
            return node.GameObject;
        }

        public static T Add<T>(GameObject owner) where T : Component => (T)AddComponent(owner, typeof(T));

        public static Component AddComponent(GameObject owner, Type type)
        {
            Component component = (Component)FormatterServices.GetUninitializedObject(type);
            Node node = NodeOf(owner);
            node.Components.Add(component);
            Nodes[component] = node;
            if (component is Camera camera && node.Name == "Main Camera")
                MainCamera = camera;
            return component;
        }

        /// <summary>
        /// Takes an object out of the scene, as if the game had destroyed it.
        /// </summary>
        public static void Remove(GameObject obj)
        {
            Node node = NodeOf(obj);
            node.Parent?.Children.Remove(node);
            node.Parent = null;
        }

        public static void RemoveComponent(Component component) => NodeOf(component).Components.Remove(component);

        public static T GetComponent<T>(object owner) => NodeOf(owner).Components.OfType<T>().FirstOrDefault();

        public static GameObject ChildNamed(GameObject parent, string name) =>
            NodeOf(parent).Children.FirstOrDefault(c => c.Name == name)?.GameObject;

        /// <summary>
        /// The names of an object's children, in order.
        /// </summary>
        public static List<string> ChildNames(GameObject parent) => NodeOf(parent).Children.Select(c => c.Name).ToList();

        public static bool InScene(GameObject obj) => NodeOf(obj).Parent != null;

        /// <summary>
        /// The delegates added to an event at runtime.
        /// </summary>
        public static List<Delegate> Listeners(UnityEventBase unityEvent)
        {
            object calls = Traverse.Create(unityEvent).Field("m_Calls").GetValue();
            IList runtimeCalls = Traverse.Create(calls).Field("m_RuntimeCalls").GetValue<IList>();
            return runtimeCalls.Cast<object>().Select(call => Traverse.Create(call).Field("Delegate").GetValue<Delegate>()).ToList();
        }

        /// <summary>
        /// Clicks a button. UnityEvent.Invoke skips listeners on fakes, so this calls them itself.
        /// </summary>
        public static void Click(Button button)
        {
            foreach (Delegate listener in Listeners(button.onClick))
                listener.DynamicInvoke();
        }

        public static T GetComponentInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants().SelectMany(n => n.Components).OfType<T>().FirstOrDefault();

        private static Node NodeOf(object owner)
        {
            Assert.True(owner is not null && Nodes.ContainsKey(owner), $"Not a fake scene object: {owner?.GetType().Name ?? "null"}");
            return Nodes[owner];
        }

        /// <summary>
        /// The generic overload with this many parameters (none unless given).
        /// </summary>
        private static MethodInfo Generic(Type type, string name, int parameters = 0) =>
            type.GetMethods().Single(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == parameters);

        private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
                {
                    if (!Redirects.TryGetValue(method, out MethodInfo stub) && method.IsGenericMethod
                        && GenericRedirects.TryGetValue(method.GetGenericMethodDefinition(), out MethodInfo generic))
                        stub = generic.MakeGenericMethod(method.GetGenericArguments());

                    if (stub != null)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = stub;
                    }
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static Camera StubMainCamera() => MainCamera;
        private static Transform StubTransform(object owner) => NodeOf(owner).Transform;
        private static bool StubEquality(Object a, Object b) => ReferenceEquals(a, b);
        private static bool StubInequality(Object a, Object b) => !ReferenceEquals(a, b);
        private static bool StubExists(Object obj) => obj is not null;
        private static void StubSetText(object text, string value) => Texts[text] = value;
        private static void StubSetColor(object text, Color value) => Colors[text] = value;
        private static ColorBlock StubGetTints(Selectable button) => Tints.TryGetValue(button, out ColorBlock tints) ? tints : ColorBlock.defaultColorBlock;
        private static void StubSetTints(Selectable button, ColorBlock value) => Tints[button] = value;
        private static void StubSetSprite(Image image, Sprite sprite) => Sprites[image] = sprite is null ? null : "sprite";
        private static GameObject StubGameObject(Component component) => NodeOf(component).GameObject;
        private static Transform StubParent(Transform transform) => NodeOf(transform).Parent?.Transform;
        private static void StubSetName(Object obj, string name) => NodeOf(obj).Name = name;
        private static int StubGetSiblingIndex(Transform transform) => NodeOf(transform).Parent.Children.IndexOf(NodeOf(transform));

        private static void StubSetSiblingIndex(Transform transform, int index)
        {
            Node node = NodeOf(transform);
            node.Parent.Children.Remove(node);
            node.Parent.Children.Insert(Math.Min(index, node.Parent.Children.Count), node);
        }

        private static void StubDestroy(Object obj)
        {
            Destroyed.Add((GameObject)obj);
            Remove((GameObject)obj);
        }

        private static T[] StubGetComponentsInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants().SelectMany(n => n.Components).OfType<T>().ToArray();

        private static readonly MethodInfo ShallowCopy = AccessTools.Method(typeof(object), "MemberwiseClone");

        /// <summary>
        /// Copies a GameObject and everything under it to the end of the parent's children, named as Unity
        /// names copies. Components are shallow copies, so they share fields (events too) with the original
        /// until the code under test replaces them.
        /// </summary>
        private static T StubInstantiate<T>(T original, Transform parent)
        {
            GameObject copy = CopyNode(NodeOf(original), NodeOf(parent).GameObject);
            NodeOf(copy).Name += "(Clone)";
            Instantiated.Add(copy);
            return (T)(object)copy;
        }

        private static GameObject CopyNode(Node original, GameObject parent)
        {
            GameObject copy = NewObject(original.Name, parent);
            Node node = NodeOf(copy);
            foreach (Component component in original.Components.Where(c => c is not Transform))
            {
                Component clone = (Component)ShallowCopy.Invoke(component, null);
                node.Components.Add(clone);
                Nodes[clone] = node;
                if (Texts.TryGetValue(component, out string text))
                    Texts[clone] = text;
                if (Tints.TryGetValue(component, out ColorBlock tints))
                    Tints[clone] = tints;
                if (Sprites.TryGetValue(component, out string sprite))
                    Sprites[clone] = sprite;
            }
            foreach (Node child in original.Children)
                CopyNode(child, copy);
            return copy;
        }

        private static Transform StubFind(Transform parent, string path)
        {
            Node node = NodeOf(parent);
            foreach (string name in path.Split('/'))
            {
                node = node.Children.FirstOrDefault(c => c.Name == name);
                if (node == null)
                    return null;
            }
            return node.Transform;
        }
    }
}
