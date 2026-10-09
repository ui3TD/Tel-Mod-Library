using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using System;
using System.Collections.Generic;
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

// Every test shares the game's static state (mods, language, the mod's loaded images) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NationalTour.Tests
{
    /// <summary>
    /// The game state National Tour reads: the installed mods, the language table, and the popups whose
    /// images it swaps (the new tour map, the tour results, the Special Events tab).
    /// </summary>
    internal static class TestGame
    {
        public const string ModNamespace = "NationalTour";

        /// <summary>
        /// Clears the fake scene and the mod's loaded images, and gives the mod stand-in images to show.
        /// </summary>
        public static void Reset()
        {
            Seams.Reset();
            Mods._Mods.Clear();
            Language.Data.Clear();
            Utility.tourPopupBGImage = null;
            Utility.TOUR_map_2 = null;
            Utility.World_tour_def = null;
            Utility.World_tour_def_tex = null;
            Utility.World_tour_def_BG = null;
        }

        /// <summary>
        /// Sets the images mainScript.Start would have loaded.
        /// </summary>
        public static void LoadImages()
        {
            Utility.TOUR_map_2 = Seams.Fake<Sprite>();
            Utility.World_tour_def = Seams.Fake<Sprite>();
            Utility.World_tour_def_tex = Seams.Fake<Texture2D>();
            Utility.World_tour_def_BG = Seams.Fake<Sprite>();
        }

        public static Mods._mod AddMod(string title, string path)
        {
            Mods._mod mod = new() { Title = title, ModName = title, Path = path };
            Mods._Mods.Add(mod);
            return mod;
        }

        /// <summary>
        /// Lists this mod the way the game does once it's installed: its folder holds the contents of assets.
        /// </summary>
        public static Mods._mod AddThisMod() => AddMod(Utility.MOD_TITLE, ModFile("assets"));

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        public static string ModFile(string relativePath) =>
            Path.Combine(RepoRoot(), "mods", "National Tour", relativePath);

        public static string ModAsset(string relativePath) => ModFile(Path.Combine("assets", relativePath));
    }

    /// <summary>
    /// The objects of a game scene the mod reaches, built in the fake scene. Every country on the new tour map
    /// starts somewhere off the map, so a test can tell whether it moved.
    /// </summary>
    internal class Scene
    {
        public static readonly Vector3 VanillaPosition = new(100f, 100f, 0f);

        public mainScript Main;
        public GameObject Data;
        public PopupManager Popups;

        // The full-screen background the results popup fades in
        public GameObject BGImage;
        public RawImage BGRaw;
        public CanvasGroup BGFade;
        public RectTransform BGRect;

        public Image ResultsBG;
        public Image SpecialEventsTourBG;
        public SpecialEvents_Manager SpecialEvents;

        public Tour_New_Popup NewTour;
        public Image NewTourMap;
        public readonly Dictionary<SEvent_Tour._country, Tour_Country> Countries = new();

        /// <summary>
        /// A save that's loaded has a SpecialEvents_Manager on the Data object; the main menu doesn't.
        /// </summary>
        public Scene(bool inGame = true)
        {
            GameObject camera = Seams.NewObject("Main Camera");
            Seams.MainCamera = Seams.Add<Camera>(camera);
            Main = Seams.Add<mainScript>(camera);
            Data = Seams.NewObject("Data");
            Main.Data = Data;
            Popups = Seams.Add<PopupManager>(Data);
            if (inGame)
                SpecialEvents = Seams.Add<SpecialEvents_Manager>(Data);

            BGImage = Seams.NewObject("BGImage");
            BGRaw = Seams.Add<RawImage>(BGImage);
            BGFade = Seams.Add<CanvasGroup>(BGImage);
            BGRect = (RectTransform)Seams.TransformOf(BGImage);
            Popups.BGImage = BGImage;

            GameObject results = Seams.NewObject("Tour Popup");
            ResultsBG = Seams.Add<Image>(Seams.NewObject("BG", results));

            GameObject specialEvents = Seams.NewObject("Special Events");
            GameObject container = Seams.NewObject("Container", specialEvents);
            Seams.NewObject("Concert", container);
            GameObject worldTour = Seams.NewObject("World Tour", container);
            SpecialEventsTourBG = Seams.Add<Image>(Seams.NewObject("BG", worldTour));

            GameObject newTour = Seams.NewObject("Tour New Popup");
            NewTour = Seams.Add<Tour_New_Popup>(newTour);
            NewTourMap = Seams.Add<Image>(Seams.NewObject("BG", Seams.NewObject("Panel", newTour)));
            NewTour.CountriesContainer = Seams.NewObject("Countries", newTour);
            foreach (SEvent_Tour._country type in Enum.GetValues(typeof(SEvent_Tour._country)))
            {
                Tour_Country country = Seams.Add<Tour_Country>(Seams.NewObject(type.ToString(), NewTour.CountriesContainer));
                country.CountryType = type;
                Seams.Positions[Seams.TransformOf(country)] = VanillaPosition;
                Countries[type] = country;
            }

            Popups.popups = new[]
            {
                new PopupManager._popup { type = PopupManager._type.sevent_tour, obj = results },
                new PopupManager._popup { type = PopupManager._type.special_events, obj = specialEvents },
                new PopupManager._popup { type = PopupManager._type.sevent_tour_new, obj = newTour },
            };
        }

        public Vector3 PositionOf(SEvent_Tour._country type) => Seams.Positions[Seams.TransformOf(Countries[type])];
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
    /// A fake GameObject: its name, place in the hierarchy, and components. Its transform is a RectTransform, as in the UI.
    /// </summary>
    internal class Node
    {
        public string Name;
        public Node Parent;
        public readonly List<Node> Children = new();
        public readonly List<Component> Components = new();
        public GameObject GameObject;
        public RectTransform Transform;

        /// <summary>
        /// This object then its descendants, depth first, the order GetComponentsInChildren searches.
        /// </summary>
        public IEnumerable<Node> SelfAndDescendants() => new[] { this }.Concat(Children.SelectMany(c => c.SelfAndDescendants()));
    }

    /// <summary>
    /// A tween the mod started: what it animates, to what, and how.
    /// </summary>
    internal class StartedTween
    {
        public string Method;
        public object Target;
        public object EndValue;
        public float Duration;
        public Ease? Ease;
    }

    /// <summary>
    /// The mod's patches reach the scene through Unity calls (Camera.main, components, transforms, images,
    /// tweens, image loading), which are native code that can't run outside the game. Harmony can't patch the
    /// patches in place either, because it compiles the originals first and the engine getters can't be
    /// compiled. So tests call copies of the patches (and of mainScript.IsMainMenu, which they call), which
    /// call these stubs instead. The stubs keep a small fake scene.
    /// </summary>
    internal static class Seams
    {
        public static Camera MainCamera;

        public static readonly Dictionary<object, Vector3> Positions = new(new SameObject());
        public static readonly Dictionary<object, float> Alphas = new(new SameObject());
        public static readonly Dictionary<object, Vector2> Sizes = new(new SameObject());
        public static readonly List<StartedTween> Tweens = new();

        /// <summary>
        /// Each image IMG2Sprite was asked to load, as (method, path, pixels per unit), and what it returned.
        /// </summary>
        public static readonly List<(string Method, string Path, float PixelsPerUnit, Object Result)> Loads = new();

        /// <summary>
        /// Each fake sprite's texture, made the first time it's read.
        /// </summary>
        public static readonly Dictionary<object, Texture2D> SpriteTextures = new(new SameObject());

        /// <summary>
        /// How many times the copies looked up a child by name.
        /// </summary>
        public static int Finds;

        private static readonly Dictionary<object, Node> Nodes = new(new SameObject());
        private static readonly Dictionary<object, StartedTween> TweenHandles = new(new SameObject());
        private static IMG2Sprite Loader;

        /// <summary>
        /// The mod's patches and the game method they call, each with the stand-in that becomes its copy.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Copies = new()
        {
            [AccessTools.Method(typeof(Tour_New_Popup_Reset), nameof(Tour_New_Popup_Reset.Postfix))] = Stub(nameof(NewTourReset)),
            [AccessTools.Method(typeof(Tour_Popup_Reset), nameof(Tour_Popup_Reset.Postfix))] = Stub(nameof(TourResultsReset)),
            [AccessTools.Method(typeof(mainScript_Start), nameof(mainScript_Start.Postfix))] = Stub(nameof(GameStart)),
            [AccessTools.Method(typeof(SpecialEvents_Manager_OpenTab_WorldTour), nameof(SpecialEvents_Manager_OpenTab_WorldTour.Prefix))] = Stub(nameof(OpenWorldTourTab)),
            [AccessTools.Method(typeof(SpecialEvents_Manager_OpenSpecialEventsPopup), nameof(SpecialEvents_Manager_OpenSpecialEventsPopup.Postfix))] = Stub(nameof(OpenSpecialEvents)),
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.IsMainMenu))] = Stub(nameof(IsMainMenu)),
        };

        /// <summary>
        /// What the copies call instead: each other, and stubs for the engine.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new(Copies)
        {
            // The fake scene
            [AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main))] = Stub(nameof(StubMainCamera)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))] = Stub(nameof(StubGameObject)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.Method(typeof(Object), "op_Inequality")] = Stub(nameof(StubInequality)),
            [AccessTools.Method(typeof(Object), "op_Implicit")] = Stub(nameof(StubExists)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })] = Stub(nameof(StubFind)),
            [AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position))] = Stub(nameof(StubGetPosition)),
            [AccessTools.PropertySetter(typeof(Transform), nameof(Transform.position))] = Stub(nameof(StubSetPosition)),

            // Looks
            [AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite))] = Stub(nameof(StubSetSprite)),
            [AccessTools.PropertySetter(typeof(Graphic), nameof(Graphic.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertySetter(typeof(RawImage), nameof(RawImage.texture))] = Stub(nameof(StubSetTexture)),
            [AccessTools.PropertySetter(typeof(CanvasGroup), nameof(CanvasGroup.alpha))] = Stub(nameof(StubSetAlpha)),
            [AccessTools.PropertySetter(typeof(RectTransform), nameof(RectTransform.sizeDelta))] = Stub(nameof(StubSetSize)),
            [AccessTools.Method(typeof(DOTweenModuleUI), nameof(DOTweenModuleUI.DOFade), new[] { typeof(CanvasGroup), typeof(float), typeof(float) })] = Stub(nameof(StubFade)),
            [AccessTools.Method(typeof(DOTweenModuleUI), nameof(DOTweenModuleUI.DOSizeDelta))] = Stub(nameof(StubTweenSize)),

            // Image loading
            [AccessTools.PropertyGetter(typeof(IMG2Sprite), nameof(IMG2Sprite.instance))] = Stub(nameof(StubLoader)),
            [AccessTools.Method(typeof(IMG2Sprite), nameof(IMG2Sprite.LoadNewSprite))] = Stub(nameof(StubLoadSprite)),
            [AccessTools.Method(typeof(IMG2Sprite), nameof(IMG2Sprite.LoadTexture))] = Stub(nameof(StubLoadTexture)),
            [AccessTools.PropertyGetter(typeof(Sprite), nameof(Sprite.texture))] = Stub(nameof(StubSpriteTexture)),
        };

        /// <summary>
        /// Generic engine methods, redirected whatever their type argument. They reach native code and the JIT
        /// can inline them, so every call goes to a stub.
        /// </summary>
        private static readonly Dictionary<MethodInfo, MethodInfo> GenericRedirects = new()
        {
            [Generic(typeof(Component), nameof(Component.GetComponent), 0)] = Stub(nameof(GetComponent)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponent), 0)] = Stub(nameof(GetComponent)),
            [Generic(typeof(Component), nameof(Component.GetComponentsInChildren), 0)] = Stub(nameof(StubGetComponentsInChildren)),
            [typeof(TweenSettingsExtensions).GetMethods().Single(m => m.Name == nameof(TweenSettingsExtensions.SetEase)
                && m.GetParameters().Select(p => p.ParameterType).Skip(1).SequenceEqual(new[] { typeof(Ease) }))] = Stub(nameof(StubSetEase)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            foreach (KeyValuePair<MethodBase, MethodInfo> copy in Copies)
                Harmony.ReversePatch(copy.Key, new HarmonyMethod(copy.Value), Stub(nameof(Redirect)), ilmanipulator: null);
            return true;
        });

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NewTourReset(Tour_New_Popup __instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TourResultsReset() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GameStart() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OpenWorldTourTab() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OpenSpecialEvents(SpecialEvents_Manager __instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsMainMenu() => throw NotInstalled();

        private static Exception NotInstalled() => new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the copies once per test run and clears the fake scene of earlier tests.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            MainCamera = null;
            Loader = Fake<IMG2Sprite>();
            Finds = 0;
            Nodes.Clear();
            Positions.Clear();
            Alphas.Clear();
            Sizes.Clear();
            Tweens.Clear();
            TweenHandles.Clear();
            Loads.Clear();
            SpriteTextures.Clear();
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

        public static T Add<T>(GameObject owner) where T : Component
        {
            T component = Fake<T>();
            NodeOf(owner).Components.Add(component);
            Nodes[component] = NodeOf(owner);
            return component;
        }

        public static Transform TransformOf(object owner) => NodeOf(owner).Transform;

        public static void Rename(GameObject obj, string name) => NodeOf(obj).Name = name;

        public static T GetComponent<T>(object owner) => NodeOf(owner).Components.OfType<T>().FirstOrDefault();

        private static Node NodeOf(object owner)
        {
            Assert.True(owner is not null && Nodes.ContainsKey(owner), $"Not a fake scene object: {owner?.GetType().Name ?? "null"}");
            return Nodes[owner];
        }

        /// <summary>
        /// The generic overload with this many parameters (each is the only one).
        /// </summary>
        private static MethodInfo Generic(Type type, string name, int parameters) =>
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
        private static GameObject StubGameObject(Component component) => NodeOf(component).GameObject;
        private static bool StubEquality(Object a, Object b) => ReferenceEquals(a, b);
        private static bool StubInequality(Object a, Object b) => !ReferenceEquals(a, b);
        private static bool StubExists(Object obj) => obj is not null;

        private static Transform StubFind(Transform parent, string name)
        {
            Finds++;
            return NodeOf(parent).Children.FirstOrDefault(c => c.Name == name)?.Transform;
        }

        private static Vector3 StubGetPosition(Transform transform) => Positions.TryGetValue(transform, out Vector3 position) ? position : Vector3.zero;
        private static void StubSetPosition(Transform transform, Vector3 value) => Positions[transform] = value;

        private static T[] StubGetComponentsInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants().SelectMany(n => n.Components).OfType<T>().ToArray();

        // Image setters store their value in the field the getter reads, so tests can read it back
        private static void StubSetSprite(Image image, Sprite value) => AccessTools.FieldRefAccess<Image, Sprite>("m_Sprite")(image) = value;
        private static void StubSetColor(Graphic graphic, Color value) => AccessTools.FieldRefAccess<Graphic, Color>("m_Color")(graphic) = value;
        private static void StubSetTexture(RawImage image, Texture value) => AccessTools.FieldRefAccess<RawImage, Texture>("m_Texture")(image) = value;
        private static void StubSetAlpha(CanvasGroup group, float value) => Alphas[group] = value;
        private static void StubSetSize(RectTransform rt, Vector2 value) => Sizes[rt] = value;

        private static TweenerCore<float, float, FloatOptions> StubFade(CanvasGroup target, float endValue, float duration) =>
            StartTween<TweenerCore<float, float, FloatOptions>>(nameof(DOTweenModuleUI.DOFade), target, endValue, duration);

        private static TweenerCore<Vector2, Vector2, VectorOptions> StubTweenSize(RectTransform target, Vector2 endValue, float duration, bool snapping) =>
            StartTween<TweenerCore<Vector2, Vector2, VectorOptions>>(nameof(DOTweenModuleUI.DOSizeDelta), target, endValue, duration);

        private static T StartTween<T>(string method, object target, object endValue, float duration)
        {
            StartedTween tween = new() { Method = method, Target = target, EndValue = endValue, Duration = duration };
            T handle = Fake<T>();
            Tweens.Add(tween);
            TweenHandles[handle] = tween;
            return handle;
        }

        private static T StubSetEase<T>(T handle, Ease ease)
        {
            Assert.True(TweenHandles.TryGetValue(handle, out StartedTween tween), "Eased something that isn't a tween");
            tween.Ease = ease;
            return handle;
        }

        private static IMG2Sprite StubLoader() => Loader;

        private static Sprite StubLoadSprite(IMG2Sprite loader, string path, float pixelsPerUnit)
        {
            Sprite sprite = Fake<Sprite>();
            Loads.Add((nameof(IMG2Sprite.LoadNewSprite), path, pixelsPerUnit, sprite));
            return sprite;
        }

        private static Texture2D StubSpriteTexture(Sprite sprite)
        {
            if (!SpriteTextures.TryGetValue(sprite, out Texture2D texture))
                SpriteTextures[sprite] = texture = Fake<Texture2D>();
            return texture;
        }

        private static Texture2D StubLoadTexture(IMG2Sprite loader, string path)
        {
            Texture2D texture = Fake<Texture2D>();
            Loads.Add((nameof(IMG2Sprite.LoadTexture), path, 0f, texture));
            return texture;
        }
    }
}
