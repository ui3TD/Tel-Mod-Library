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
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Xunit;
using Object = UnityEngine.Object;

// Every test shares the game's static state (mods, language, tour lists, the mod's loaded pictures) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NationalTour.Tests
{
    /// <summary>
    /// The game state National Tour reads: the installed mods, the language table, the tour countries, and the
    /// popups it adds to (the Special Events tabs, the new tour map, the tour results).
    /// </summary>
    internal static class TestGame
    {
        public const string ModNamespace = "NationalTour";

        /// <summary>
        /// Clears the fake scene, the game's lists and the mod's state.
        /// </summary>
        public static void Reset()
        {
            Seams.Reset();
            Mods._Mods.Clear();
            Language.Data.Clear();
            Language.Data["ERROR"] = "ERROR";
            SEvent_Tour.Countries.Clear();
            SEvent_Tour.Tours.Clear();
            SEvent_Tour.Const_Area.Clear();
            Prefectures.All.Clear();
            Readiness.Reset();
            NationalRecord.Reset();
            NationalTab.Tab = null;
            NationalTab.Panel = null;
            NationalTab.Manager = null;
            NewTourMap.Reset();
            ResultsPictures.Reset();
            Pictures.TOUR_map_2 = null;
            Pictures.World_tour_def = null;
            Pictures.World_tour_def_tex = null;
            Pictures.World_tour_def_BG = null;
        }

        /// <summary>
        /// Sets the pictures mainScript.Start would have loaded.
        /// </summary>
        public static void LoadImages()
        {
            Pictures.TOUR_map_2 = Seams.Fake<Sprite>();
            Pictures.World_tour_def = Seams.Fake<Sprite>();
            Pictures.World_tour_def_tex = Seams.Fake<Texture2D>();
            Pictures.World_tour_def_BG = Seams.Fake<Sprite>();
        }

        /// <summary>
        /// The game's 18 countries with the numbers of its countries.json, and the prefectures built from them.
        /// </summary>
        public static void LoadCountries()
        {
            foreach (SEvent_Tour._areaType type in Enum.GetValues(typeof(SEvent_Tour._areaType)))
                SEvent_Tour.Const_Area.Add(new SEvent_Tour._area { Type = type, Saving = 0.2f });
            foreach (GameCountries.Row row in GameCountries.Rows())
            {
                SEvent_Tour.Countries.Add(new SEvent_Tour.country
                {
                    Type = row.Type,
                    Area = SEvent_Tour.Const_Area.Single(a => a.Type == row.Area),
                    Cost = row.Cost,
                    TicketPrice = row.TicketPrice,
                });
            }
            Prefectures.Build();
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
        public static Mods._mod AddThisMod() => AddMod(Pictures.MOD_TITLE, ModFile("assets"));

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

        /// <summary>
        /// A tour with one stop per destination given.
        /// </summary>
        public static SEvent_Tour.tour Tour(params SEvent_Tour.country[] stops)
        {
            SEvent_Tour.tour tour = new();
            foreach (SEvent_Tour.country stop in stops)
                tour.SelectedCountries.Add(new SEvent_Tour.tour.selectedCountry { Country = stop, Level = 1 });
            return tour;
        }

        public static SEvent_Tour.country Country(SEvent_Tour._country type) => SEvent_Tour.Countries.Single(c => c.Type == type);
    }

    /// <summary>
    /// The game's countries.json (the same in every language): each country's discount area, cost and ticket price.
    /// </summary>
    internal static class GameCountries
    {
        public sealed class Row
        {
            public readonly SEvent_Tour._country Type;
            public readonly SEvent_Tour._areaType Area;
            public readonly int Cost;
            public readonly int TicketPrice;

            public Row(SEvent_Tour._country type, SEvent_Tour._areaType area, int cost, int ticketPrice)
            {
                Type = type;
                Area = area;
                Cost = cost;
                TicketPrice = ticketPrice;
            }
        }

        public static IEnumerable<Row> Rows() => new Row[]
        {
            new(SEvent_Tour._country.china, SEvent_Tour._areaType.asia, 45300, 2245),
            new(SEvent_Tour._country.southKorea, SEvent_Tour._areaType.asia, 15100, 4339),
            new(SEvent_Tour._country.india, SEvent_Tour._areaType.asia, 75500, 1286),
            new(SEvent_Tour._country.thailand, SEvent_Tour._areaType.asia, 52850, 1925),
            new(SEvent_Tour._country.philippines, SEvent_Tour._areaType.asia, 46810, 1468),
            new(SEvent_Tour._country.indonesia, SEvent_Tour._areaType.asia, 52850, 1582),
            new(SEvent_Tour._country.australia, SEvent_Tour._areaType.asia, 105700, 4817),
            new(SEvent_Tour._country.southAfrica, SEvent_Tour._areaType.africa, 135900, 1828),
            new(SEvent_Tour._country.canada, SEvent_Tour._areaType.namerica, 196300, 5734),
            new(SEvent_Tour._country.US, SEvent_Tour._areaType.namerica, 151000, 7271),
            new(SEvent_Tour._country.brazil, SEvent_Tour._areaType.samerica, 151000, 2510),
            new(SEvent_Tour._country.argentina, SEvent_Tour._areaType.samerica, 196300, 2903),
            new(SEvent_Tour._country.UK, SEvent_Tour._areaType.europe, 135900, 5174),
            new(SEvent_Tour._country.france, SEvent_Tour._areaType.europe, 90600, 5123),
            new(SEvent_Tour._country.germany, SEvent_Tour._areaType.europe, 120800, 5500),
            new(SEvent_Tour._country.italy, SEvent_Tour._areaType.europe, 120800, 4400),
            new(SEvent_Tour._country.spain, SEvent_Tour._areaType.europe, 75500, 4094),
            new(SEvent_Tour._country.russia, SEvent_Tour._areaType.europe, 108720, 2577),
        };
    }

    /// <summary>
    /// The objects of a game scene the mod reaches, built in the fake scene, named as in the game (checked in game
    /// with a layout dump). Every pin on the new tour map starts somewhere off the map, so a test can tell whether it moved.
    /// </summary>
    internal class Scene
    {
        public static readonly Vector3 VanillaPosition = new(100f, 100f, 0f);
        public static readonly Vector2 BackgroundSize = new(1024f, 0f);
        public static readonly Color BlurColor = new(0.132f, 0.172f, 0.802f, 0.365f);

        public mainScript Main;
        public GameObject Data;
        public PopupManager Popups;

        // The full-screen background the results popup fades in
        public GameObject BGImage;
        public RawImage BGRaw;
        public CanvasGroup BGFade;
        public RectTransform BGRect;
        public Texture GameBackground;

        public Image ResultsBG;
        public Sprite GameBlur;

        // The Special Events popup
        public SpecialEvents_Manager SpecialEvents;
        public GameObject SpecialEventsPopup;
        public GameObject Container;
        public GameObject Types;
        public GameObject WorldTourTab;
        public GameObject WorldTourPanel;
        public Image WorldTourPicture;
        public Sprite GameTourPicture;

        public Tour_New_Popup NewTour;
        public Image NewTourMap;
        public Sprite GameMap;
        public ButtonDefault NewTourContinue;
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

            BGImage = Seams.NewObject("BGImage_RenderTexture");
            BGRaw = Seams.Add<RawImage>(BGImage);
            BGFade = Seams.Add<CanvasGroup>(BGImage);
            BGRect = (RectTransform)Seams.TransformOf(BGImage);
            Seams.Sizes[BGRect] = BackgroundSize;
            GameBackground = Seams.Fake<RenderTexture>();
            Seams.SetTexture(BGRaw, GameBackground);
            Popups.BGImage = BGImage;

            GameObject results = Seams.NewObject("SEvents_Tour");
            ResultsBG = Seams.Add<Image>(Seams.NewObject("BG", results));
            Seams.SetColor(ResultsBG, BlurColor);

            BuildSpecialEvents();
            GameObject newTour = BuildNewTour();

            Popups.popups = new[]
            {
                new PopupManager._popup { type = PopupManager._type.sevent_tour, obj = results },
                new PopupManager._popup { type = PopupManager._type.special_events, obj = SpecialEventsPopup },
                new PopupManager._popup { type = PopupManager._type.sevent_tour_new, obj = newTour },
            };
        }

        private void BuildSpecialEvents()
        {
            SpecialEventsPopup = Seams.NewObject("Special_Events");
            Container = Seams.NewObject("Container", SpecialEventsPopup);
            foreach (string panel in new[] { "Concert", "Election", "World Tour" })
            {
                GameObject obj = Seams.NewObject(panel, Container);
                Image picture = Seams.Add<Image>(Seams.NewObject("BG", obj));
                Label(Seams.NewObject("Text", Seams.NewObject("Panel", obj)), panel == "World Tour" ? "SEVENT__WORLD_TOUR_DESC" : "OTHER_DESC");
                if (panel == "World Tour")
                {
                    WorldTourPanel = obj;
                    WorldTourPicture = picture;
                    GameTourPicture = Seams.Fake<Sprite>();
                    Seams.SetSprite(picture, GameTourPicture);
                }
            }

            Types = Seams.NewObject("Types", Container);
            foreach (string tab in new[] { "Concert", "Election", "World Tour" })
            {
                GameObject obj = Seams.NewObject(tab, Types);
                Seams.Add<Image>(obj);
                Seams.Add<Button>(obj);
                Seams.Add<ButtonDefault>(obj);
                TabButton tabButton = Seams.Add<TabButton>(obj);
                Label(Seams.NewObject("Text", obj), tab == "World Tour" ? "SEVENT__WORLD_TOUR" : "OTHER");
                if (tab == "World Tour")
                {
                    WorldTourTab = obj;
                    tabButton.TabToOpen = WorldTourPanel;
                    Seams.OnClick(Seams.GetComponent<Button>(obj)).Add(SpecialEvents != null ? SpecialEvents.OpenTab_WorldTour : null);
                }
            }
        }

        private static void Label(GameObject obj, string constant)
        {
            Seams.Add<Lang_Button>(obj).Constant = constant;
            Seams.Add<TextMeshProUGUI>(obj);
        }

        private GameObject BuildNewTour()
        {
            GameObject newTour = Seams.NewObject("SEvents_Tour_New");
            NewTour = Seams.Add<Tour_New_Popup>(newTour);
            NewTour.Tour = new SEvent_Tour.tour();
            GameObject continueButton = Seams.NewObject("Continue", newTour);
            NewTourContinue = Seams.Add<ButtonDefault>(continueButton);
            NewTour.Continue = continueButton;
            GameObject panel = Seams.NewObject("Panel", newTour);
            NewTourMap = Seams.Add<Image>(Seams.NewObject("BG", panel));
            GameMap = Seams.Fake<Sprite>();
            Seams.SetSprite(NewTourMap, GameMap);
            NewTour.CountriesContainer = Seams.NewObject("Countries", panel);
            foreach (SEvent_Tour._country type in Enum.GetValues(typeof(SEvent_Tour._country)))
            {
                Tour_Country country = Seams.Add<Tour_Country>(Seams.NewObject(type == 0 ? "Country" : $"Country ({(int)type})", NewTour.CountriesContainer));
                country.CountryType = type;
                Seams.Positions[Seams.TransformOf(country)] = VanillaPosition;
                Countries[type] = country;
            }
            return newTour;
        }

        public Vector3 PositionOf(Tour_Country pin) => Seams.Positions[Seams.TransformOf(pin)];

        public Vector3 PositionOf(SEvent_Tour._country type) => PositionOf(Countries[type]);

        /// <summary>
        /// Every pin on the new tour map, in the order the game lists them.
        /// </summary>
        public Tour_Country[] Pins() => Seams.ChildrenOf(NewTour.CountriesContainer).Select(c => Seams.GetComponent<Tour_Country>(c)).Where(p => p is not null).ToArray();

        public Tour_Country[] PrefecturePins() => Pins().Where(p => Prefectures.IsPrefecture(p.CountryType)).ToArray();

        public Tour_Country[] GamePins() => Pins().Where(p => !Prefectures.IsPrefecture(p.CountryType)).ToArray();
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
    /// A fake GameObject: its name, place in the hierarchy, whether it's active, and components. Its transform is
    /// a RectTransform, as in the UI.
    /// </summary>
    internal class Node
    {
        public string Name;
        public bool Active = true;
        public Node Parent;
        public readonly List<Node> Children = new();
        public readonly List<Component> Components = new();
        public GameObject GameObject;
        public RectTransform Transform;

        /// <summary>
        /// This object then its descendants, depth first, the order GetComponentsInChildren searches. Without
        /// includeInactive, an inactive object and everything under it is left out, as in Unity.
        /// </summary>
        public IEnumerable<Node> SelfAndDescendants(bool includeInactive = true) =>
            !includeInactive && !Active ? Enumerable.Empty<Node>()
            : new[] { this }.Concat(Children.SelectMany(c => c.SelfAndDescendants(includeInactive)));
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
    /// The mod's code reaches the scene through Unity calls (Camera.main, components, transforms, images, tweens,
    /// image loading) and game methods that use them, which can't run outside the game. Harmony can't patch them
    /// in place either, because it compiles the originals first and the engine getters can't be compiled. So
    /// tests call copies of the mod's methods (and of mainScript.IsMainMenu), which call these stubs instead.
    /// The stubs keep a small fake scene and record what the game methods were asked to do.
    /// </summary>
    internal static class Seams
    {
        public static Camera MainCamera;

        public static readonly Dictionary<object, Vector3> Positions = new(new SameObject());

        /// <summary>
        /// Each transform's scale, as the popup's open and close animations set it; 1 when not set. The fake popup
        /// sits at the world's origin, so its TransformPoint only scales.
        /// </summary>
        public static readonly Dictionary<object, float> Scales = new(new SameObject());
        public static readonly Dictionary<object, float> Alphas = new(new SameObject());
        public static readonly Dictionary<object, Vector2> Sizes = new(new SameObject());
        public static readonly Dictionary<object, string> Texts = new(new SameObject());
        public static readonly Dictionary<object, bool> Activated = new(new SameObject());
        public static readonly List<StartedTween> Tweens = new();
        public static readonly List<object> Killed = new();

        /// <summary>
        /// Game methods the copies called, in order: (method, the object it was called on).
        /// </summary>
        public static readonly List<(string Method, object Target)> Calls = new();

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
        private static readonly Dictionary<object, Button.ButtonClickedEvent> ClickEvents = new(new SameObject());
        private static readonly Dictionary<object, List<UnityAction>> Listeners = new(new SameObject());
        private static IMG2Sprite Loader;

        /// <summary>
        /// The mod's methods and the game method they call, each with the stand-in that becomes its copy.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Copies = new()
        {
            [AccessTools.Method(typeof(mainScript_Start), nameof(mainScript_Start.Postfix))] = Stub(nameof(GameStart)),
            [AccessTools.Method(typeof(mainScript), nameof(mainScript.IsMainMenu))] = Stub(nameof(IsMainMenu)),

            [AccessTools.Method(typeof(NewTourMap), nameof(NewTourMap.OnReset))] = Stub(nameof(NewTourReset)),
            [AccessTools.Method(typeof(NewTourMap), nameof(NewTourMap.OpenNational))] = Stub(nameof(OpenNationalMap)),
            [AccessTools.Method(typeof(NewTourMap), "ShowNational")] = Stub(nameof(ShowNational)),
            [AccessTools.Method(typeof(NewTourMap), "ShowWorld")] = Stub(nameof(ShowWorld)),
            [AccessTools.Method(typeof(NewTourMap), "AddPins")] = Stub(nameof(AddPins)),
            [AccessTools.Method(typeof(Tour_New_Popup_Render), nameof(Tour_New_Popup_Render.Postfix))] = Stub(nameof(NewTourRender)),

            [AccessTools.Method(typeof(ResultsPictures), nameof(ResultsPictures.OnReset))] = Stub(nameof(TourResultsReset)),
            [AccessTools.Method(typeof(ResultsPictures), nameof(ResultsPictures.RestoreSize))] = Stub(nameof(TourResultsFinish)),

            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.Build))] = Stub(nameof(BuildTab)),
            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.CanOpen))] = Stub(nameof(CanOpenTab)),
            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.Open))] = Stub(nameof(OpenTab)),
            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.OnTabClicked))] = Stub(nameof(ClickTab)),
            [AccessTools.Method(typeof(NationalTab), "SetText")] = Stub(nameof(SetTabText)),
            [AccessTools.Method(typeof(SpecialEvents_Manager_ResetPopup_New), nameof(SpecialEvents_Manager_ResetPopup_New.Postfix))] = Stub(nameof(SpecialEventsContinue)),
        };

        /// <summary>
        /// What the copies call instead: each other, and stubs for the engine and the game.
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
            [AccessTools.PropertySetter(typeof(Object), nameof(Object.name))] = Stub(nameof(StubSetName)),
            [AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive))] = Stub(nameof(StubSetActive)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.activeSelf))] = Stub(nameof(StubActiveSelf)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })] = Stub(nameof(StubFind)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.GetSiblingIndex))] = Stub(nameof(StubGetSiblingIndex)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.SetSiblingIndex))] = Stub(nameof(StubSetSiblingIndex)),
            [AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position))] = Stub(nameof(StubGetPosition)),
            [AccessTools.PropertySetter(typeof(Transform), nameof(Transform.position))] = Stub(nameof(StubSetPosition)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.TransformPoint), new[] { typeof(Vector3) })] = Stub(nameof(StubTransformPoint)),

            // Looks
            [AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite))] = Stub(nameof(StubSetSprite)),
            [AccessTools.PropertySetter(typeof(Graphic), nameof(Graphic.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertySetter(typeof(RawImage), nameof(RawImage.texture))] = Stub(nameof(StubSetTexture)),
            [AccessTools.PropertySetter(typeof(CanvasGroup), nameof(CanvasGroup.alpha))] = Stub(nameof(StubSetAlpha)),
            [AccessTools.PropertyGetter(typeof(RectTransform), nameof(RectTransform.sizeDelta))] = Stub(nameof(StubGetSize)),
            [AccessTools.PropertySetter(typeof(RectTransform), nameof(RectTransform.sizeDelta))] = Stub(nameof(StubSetSize)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.Method(typeof(DOTweenModuleUI), nameof(DOTweenModuleUI.DOFade), new[] { typeof(CanvasGroup), typeof(float), typeof(float) })] = Stub(nameof(StubFade)),
            [AccessTools.Method(typeof(DOTweenModuleUI), nameof(DOTweenModuleUI.DOSizeDelta))] = Stub(nameof(StubTweenSize)),
            [AccessTools.Method(typeof(ShortcutExtensions), nameof(ShortcutExtensions.DOKill), new[] { typeof(Component), typeof(bool) })] = Stub(nameof(StubKill)),

            // Buttons
            [AccessTools.PropertyGetter(typeof(Button), nameof(Button.onClick))] = Stub(nameof(StubGetOnClick)),
            [AccessTools.PropertySetter(typeof(Button), nameof(Button.onClick))] = Stub(nameof(StubSetOnClick)),
            [AccessTools.Method(typeof(UnityEvent), nameof(UnityEvent.AddListener), new[] { typeof(UnityAction) })] = Stub(nameof(StubAddListener)),

            // Game methods that reach the engine: what they were asked to do is recorded
            [AccessTools.Method(typeof(ButtonDefault), nameof(ButtonDefault.Activate))] = Stub(nameof(StubActivate)),
            [AccessTools.Method(typeof(TabButton), nameof(TabButton.HideTab))] = Stub(nameof(StubHideTab)),
            [AccessTools.Method(typeof(TabButton), nameof(TabButton.OnClick))] = Stub(nameof(StubTabClick)),
            [AccessTools.Method(typeof(Lang_Button), nameof(Lang_Button.ResetText))] = Stub(nameof(StubResetText)),
            [AccessTools.Method(typeof(Tour_Country), nameof(Tour_Country.UpdateData))] = Stub(nameof(StubUpdateData)),
            [AccessTools.Method(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Reset))] = Stub(nameof(StubNewTourPopupReset)),
            [AccessTools.Method(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Render))] = Stub(nameof(StubNewTourPopupRender)),
            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.CallRenderContinueButton))] = Stub(nameof(StubRenderContinueButton)),
            [AccessTools.Method(typeof(NationalTab), nameof(NationalTab.CallRenderTab))] = Stub(nameof(StubRenderTab)),

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
            [typeof(Component).GetMethods().Single(m => m.Name == nameof(Component.GetComponentsInChildren) && m.IsGenericMethodDefinition
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(bool) }))] = Stub(nameof(StubGetComponentsInChildrenWithInactive)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponentsInChildren), 0)] = Stub(nameof(StubGetComponentsInChildren)),
            [typeof(GameObject).GetMethods().Single(m => m.Name == nameof(GameObject.GetComponentsInChildren) && m.IsGenericMethodDefinition
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(bool) }))] = Stub(nameof(StubGetComponentsInChildrenWithInactive)),
            [typeof(Object).GetMethods().Single(m => m.Name == nameof(Object.Instantiate) && m.IsGenericMethodDefinition
                && m.GetParameters().Select(p => p.ParameterType.IsGenericParameter ? "T" : p.ParameterType.Name).SequenceEqual(new[] { "T", "Transform", "Boolean" }))] = Stub(nameof(StubInstantiate)),
            [typeof(TweenSettingsExtensions).GetMethods().Single(m => m.Name == nameof(TweenSettingsExtensions.SetEase)
                && m.GetParameters().Select(p => p.ParameterType).Skip(1).SequenceEqual(new[] { typeof(Ease) }))] = Stub(nameof(StubSetEase)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            foreach (KeyValuePair<MethodBase, MethodInfo> copy in Copies)
                Harmony.ReversePatch(copy.Key, new HarmonyMethod(copy.Value), Stub(nameof(Redirect)), ilmanipulator: null);
            return true;
        });

        // The copies

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GameStart() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsMainMenu() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NewTourReset(Tour_New_Popup popup) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OpenNationalMap(Tour_New_Popup popup) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShowNational(Tour_New_Popup popup, Image map) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShowWorld(Tour_New_Popup popup, Image map) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static List<Tour_Country> AddPins(Tour_New_Popup popup) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NewTourRender(Tour_New_Popup __instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TourResultsReset() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TourResultsFinish() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void BuildTab(SpecialEvents_Manager manager) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool CanOpenTab() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OpenTab(SpecialEvents_Manager manager, TabButton tabButton) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ClickTab() => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetTabText(Transform label, string key, string fallback) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SpecialEventsContinue(SpecialEvents_Manager __instance) => throw NotInstalled();

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
            Scales.Clear();
            Alphas.Clear();
            Sizes.Clear();
            Texts.Clear();
            Activated.Clear();
            Tweens.Clear();
            Killed.Clear();
            Calls.Clear();
            TweenHandles.Clear();
            ClickEvents.Clear();
            Listeners.Clear();
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

        public static GameObject GameObjectOf(object owner) => NodeOf(owner).GameObject;

        public static string NameOf(object owner) => NodeOf(owner).Name;

        public static void Rename(GameObject obj, string name) => NodeOf(obj).Name = name;

        public static bool IsActive(object owner) => NodeOf(owner).Active;

        public static void SetActive(object owner, bool active) => NodeOf(owner).Active = active;

        public static GameObject ParentOf(object owner) => NodeOf(owner).Parent?.GameObject;

        public static GameObject[] ChildrenOf(object owner) => NodeOf(owner).Children.Select(c => c.GameObject).ToArray();

        public static GameObject Child(object owner, string name) => NodeOf(owner).Children.Single(c => c.Name == name).GameObject;

        public static T GetComponent<T>(object owner) => NodeOf(owner).Components.OfType<T>().FirstOrDefault();

        public static string TextOf(object owner) => Texts.TryGetValue(GetComponent<TextMeshProUGUI>(owner), out string text) ? text : null;

        /// <summary>
        /// The click listeners a button has, as the copies left them (the game's own are set by the scene).
        /// </summary>
        public static List<UnityAction> OnClick(Button button)
        {
            if (!ClickEvents.TryGetValue(button, out Button.ButtonClickedEvent click))
                StubSetOnClick(button, click = Fake<Button.ButtonClickedEvent>());
            return Listeners[click];
        }

        public static int CountCalls(string method, object target) => Calls.Count(c => c.Method == method && ReferenceEquals(c.Target, target));

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
        private static void StubSetName(Object obj, string name) => NodeOf(obj).Name = name;
        private static void StubSetActive(GameObject obj, bool active) => NodeOf(obj).Active = active;
        private static bool StubActiveSelf(GameObject obj) => NodeOf(obj).Active;

        private static Transform StubFind(Transform parent, string name)
        {
            Finds++;
            return NodeOf(parent).Children.FirstOrDefault(c => c.Name == name)?.Transform;
        }

        private static int StubGetSiblingIndex(Transform transform)
        {
            Node node = NodeOf(transform);
            return node.Parent.Children.IndexOf(node);
        }

        private static void StubSetSiblingIndex(Transform transform, int index)
        {
            Node node = NodeOf(transform);
            node.Parent.Children.Remove(node);
            node.Parent.Children.Insert(index, node);
        }

        private static Vector3 StubGetPosition(Transform transform) => Positions.TryGetValue(transform, out Vector3 position) ? position : Vector3.zero;
        private static void StubSetPosition(Transform transform, Vector3 value) => Positions[transform] = value;
        private static Vector3 StubTransformPoint(Transform transform, Vector3 point) => point * (Scales.TryGetValue(transform, out float scale) ? scale : 1f);

        private static T[] StubGetComponentsInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants(includeInactive: false).SelectMany(n => n.Components).OfType<T>().ToArray();

        private static T[] StubGetComponentsInChildrenWithInactive<T>(object owner, bool includeInactive) =>
            NodeOf(owner).SelfAndDescendants(includeInactive).SelectMany(n => n.Components).OfType<T>().ToArray();

        /// <summary>
        /// A copy of the object and everything under it, with copies of its components' fields. References
        /// between the copied components still point at the original's, which the mod doesn't rely on.
        /// </summary>
        private static T StubInstantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object
        {
            Node copy = CopyNode(NodeOf(original), NodeOf(parent));
            return (T)(object)copy.GameObject;
        }

        private static readonly MethodInfo MemberwiseClone = typeof(object).GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic);

        private static Node CopyNode(Node original, Node parent)
        {
            GameObject obj = NewObject(original.Name, parent.GameObject);
            Node copy = NodeOf(obj);
            copy.Active = original.Active;
            foreach (Component component in original.Components.Where(c => c is not RectTransform))
            {
                Component clone = (Component)MemberwiseClone.Invoke(component, null);
                copy.Components.Add(clone);
                Nodes[clone] = copy;
                if (component is Button button && ClickEvents.TryGetValue(button, out Button.ButtonClickedEvent click))
                {
                    // A copy keeps the original's saved listeners
                    Button.ButtonClickedEvent copied = Fake<Button.ButtonClickedEvent>();
                    StubSetOnClick((Button)clone, copied);
                    Listeners[copied].AddRange(Listeners[click]);
                }
            }
            if (Positions.TryGetValue(original.Transform, out Vector3 position))
                Positions[copy.Transform] = position;
            foreach (Node child in original.Children)
                CopyNode(child, copy);
            return copy;
        }

        public static void SetSprite(Image image, Sprite value) => StubSetSprite(image, value);
        public static void SetColor(Graphic graphic, Color value) => StubSetColor(graphic, value);
        public static void SetTexture(RawImage image, Texture value) => StubSetTexture(image, value);

        // Image setters store their value in the field the getter reads, so tests can read it back
        private static void StubSetSprite(Image image, Sprite value) => AccessTools.FieldRefAccess<Image, Sprite>("m_Sprite")(image) = value;
        private static void StubSetColor(Graphic graphic, Color value) => AccessTools.FieldRefAccess<Graphic, Color>("m_Color")(graphic) = value;
        private static void StubSetTexture(RawImage image, Texture value) => AccessTools.FieldRefAccess<RawImage, Texture>("m_Texture")(image) = value;
        private static void StubSetAlpha(CanvasGroup group, float value) => Alphas[group] = value;
        private static Vector2 StubGetSize(RectTransform rt) => Sizes.TryGetValue(rt, out Vector2 size) ? size : Vector2.zero;
        private static void StubSetSize(RectTransform rt, Vector2 value) => Sizes[rt] = value;
        private static void StubSetText(TMP_Text text, string value) => Texts[text] = value;

        private static TweenerCore<float, float, FloatOptions> StubFade(CanvasGroup target, float endValue, float duration) =>
            StartTween<TweenerCore<float, float, FloatOptions>>(nameof(DOTweenModuleUI.DOFade), target, endValue, duration);

        private static TweenerCore<Vector2, Vector2, VectorOptions> StubTweenSize(RectTransform target, Vector2 endValue, float duration, bool snapping) =>
            StartTween<TweenerCore<Vector2, Vector2, VectorOptions>>(nameof(DOTweenModuleUI.DOSizeDelta), target, endValue, duration);

        private static int StubKill(Component target, bool complete)
        {
            Killed.Add(target);
            return 0;
        }

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

        private static Button.ButtonClickedEvent StubGetOnClick(Button button) =>
            ClickEvents.TryGetValue(button, out Button.ButtonClickedEvent click) ? click : null;

        private static void StubSetOnClick(Button button, Button.ButtonClickedEvent click)
        {
            ClickEvents[button] = click;
            if (!Listeners.ContainsKey(click))
                Listeners[click] = new List<UnityAction>();
        }

        private static void StubAddListener(UnityEvent click, UnityAction listener)
        {
            if (!Listeners.ContainsKey(click))
                Listeners[click] = new List<UnityAction>();
            Listeners[click].Add(listener);
        }

        private static void StubActivate(ButtonDefault button, bool val, bool showTooltip)
        {
            Activated[button] = val;
            Calls.Add((nameof(ButtonDefault.Activate), button));
        }

        private static void StubHideTab(TabButton tab)
        {
            Calls.Add((nameof(TabButton.HideTab), tab));
            if (tab.TabToOpen is not null)
                NodeOf(tab.TabToOpen).Active = false;
        }

        private static void StubTabClick(TabButton tab)
        {
            Calls.Add((nameof(TabButton.OnClick), tab));
            foreach (Node sibling in NodeOf(tab).Parent.Children)
            {
                TabButton other = sibling.Components.OfType<TabButton>().FirstOrDefault();
                if (other?.TabToOpen is not null)
                    NodeOf(other.TabToOpen).Active = false;
            }
            if (tab.TabToOpen is not null)
                NodeOf(tab.TabToOpen).Active = true;
        }

        private static void StubResetText(Lang_Button lang)
        {
            Calls.Add((nameof(Lang_Button.ResetText), lang));
            if (Language.Data.TryGetValue(lang.Constant, out string text))
                Texts[GetComponent<TextMeshProUGUI>(lang)] = text;
        }

        private static void StubUpdateData(Tour_Country pin) => Calls.Add((nameof(Tour_Country.UpdateData), pin));

        /// <summary>
        /// The game's reset: a new empty tour, every active pin updated, then the mod's postfix.
        /// </summary>
        private static void StubNewTourPopupReset(Tour_New_Popup popup)
        {
            Calls.Add((nameof(Tour_New_Popup.Reset), popup));
            popup.Tour = new SEvent_Tour.tour();
            foreach (Tour_Country pin in StubGetComponentsInChildren<Tour_Country>(popup.CountriesContainer))
                StubUpdateData(pin);
            NewTourReset(popup);
        }

        /// <summary>
        /// The game's render ends with Continue on (the tests' tours have stops), then the mod's postfix.
        /// </summary>
        private static void StubNewTourPopupRender(Tour_New_Popup popup)
        {
            Calls.Add((nameof(Tour_New_Popup.Render), popup));
            StubActivate(GetComponent<ButtonDefault>(popup.Continue), true, false);
            NewTourRender(popup);
        }

        private static void StubRenderContinueButton(SpecialEvents_Manager manager) => Calls.Add((nameof(NationalTab.CallRenderContinueButton), manager));

        private static void StubRenderTab(TabButton tab) => Calls.Add((nameof(NationalTab.CallRenderTab), tab));

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

    /// <summary>
    /// Records what the mod logs, instead of the game's log.
    /// </summary>
    internal sealed class LogRecorder : ILogHandler, IDisposable
    {
        public readonly List<string> Errors = new();
        private readonly ILogHandler game;

        public LogRecorder()
        {
            game = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (logType == LogType.Error)
                Errors.Add(string.Format(format, args));
        }

        public void LogException(Exception exception, Object context) => Errors.Add(exception.ToString());

        public void Dispose() => Debug.unityLogger.logHandler = game;
    }
}
