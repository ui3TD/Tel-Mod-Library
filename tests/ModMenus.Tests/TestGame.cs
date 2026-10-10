using HarmonyLib;
using Michsky.UI.ModernUIPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Xunit;
using static ModMenus.ModMenusUtils;
using Object = UnityEngine.Object;

// Every test shares the game's static state (mods, variables, language) and the seams below.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ModMenus.Tests
{
    /// <summary>
    /// The game state ModMenus reads: the installed mods, the saved settings, the language table, the
    /// vanilla settings popup whose slider, checkbox and dropdown it copies, and the rival name field it copies
    /// for text fields.
    /// </summary>
    internal static class TestGame
    {
        public static readonly Dictionary<string, string> Labels = new()
        {
            ["TEST__INTRO"] = "Some settings",
            ["TEST__VOLUME"] = "Volume",
            ["TEST__LOUD"] = "Loud mode",
            ["TEST__PICK"] = "Pick one",
            ["TEST__FIRST"] = "First",
            ["TEST__SECOND"] = "Second",
            ["TEST__THIRD"] = "Third",
            ["TEST__MOTTO"] = "Agency motto",
        };

        /// <summary>
        /// The sprites of the vanilla checkbox when ticked and when empty.
        /// </summary>
        public static Sprite Checked, Empty;

        private static readonly string ModsDir = Path.Combine(Path.GetTempPath(), "ModMenus.Tests");

        public static void Reset()
        {
            Seams.Reset();
            Mods._Mods.Clear();
            staticVars.Settings._Mods.Clear();
            variables.variable.Clear();
            Language.Data.Clear();
            foreach (KeyValuePair<string, string> label in Labels)
                Language.Data[label.Key] = label.Value;
            if (Directory.Exists(ModsDir))
                Directory.Delete(ModsDir, recursive: true);

            Checked = Seams.Fake<Sprite>();
            Empty = Seams.Fake<Sprite>();
            Seams.SettingsPopup = VanillaSettings();
            Seams.TextField = Seams.Prefab(RivalFirstNameField);
        }

        /// <summary>
        /// Installs a mod after the ones already installed. Its folder has this modmenu.json, or none if null.
        /// </summary>
        public static Mods._mod AddMod(string title, string menuJson, bool enabled = true)
        {
            string path = Path.Combine(ModsDir, title);
            Directory.CreateDirectory(path);
            if (menuJson != null)
            {
                string menuDir = Path.Combine(path, "JSON", JSON_DIR);
                Directory.CreateDirectory(menuDir);
                File.WriteAllText(Path.Combine(menuDir, JSON_FILE), menuJson);
            }
            return AddModAt(title, path, enabled);
        }

        /// <summary>
        /// Installs one of our mods from the repo, with its constants loaded into the language table as the game does.
        /// </summary>
        public static Mods._mod AddShippedMod(string folder)
        {
            string path = Path.Combine(RepoRoot(), "mods", folder, "assets");
            SimpleJSON.JSONNode constants = mainScript.ProcessInboundData(File.ReadAllText(Path.Combine(path, "JSON", "Constants", "constants.json")));
            for (int i = 0; i < constants.Count; i++)
                Language.Data[constants[i]["id"]] = constants[i]["text"];
            return AddModAt(folder, path, enabled: true);
        }

        private static Mods._mod AddModAt(string title, string path, bool enabled)
        {
            string modName = "com.test." + title.Replace(" ", "");
            Mods._mod mod = new() { ModName = modName, Title = title, Path = path };
            Mods._Mods.Add(mod);
            if (!enabled)
                staticVars.Settings._Mods.Add(new Mods._mod { ModName = modName, Enabled = false });
            return mod;
        }

        /// <summary>
        /// Builds the menu as GenerateMenuPopup does: a panel with the Apply and Cancel handler, holding a
        /// scroll area whose content AddMenuItems fills.
        /// </summary>
        public static Menu BuildMenu()
        {
            GameObject panel = Seams.NewObject("Panel");
            Seams.Add<ModMenuManager>(panel);
            GameObject scroll = Seams.NewObject(SCROLLRECT_OBJ_NAME, panel);
            Seams.Add<ScrollRect>(scroll);
            GameObject viewport = Seams.NewObject(VIEWPORT_OBJ_NAME, scroll);
            GameObject content = Seams.NewObject(MENUCONTENT_OBJ_NAME, viewport);
            GameObject dropdownLayer = Seams.NewObject(DROPDOWN_LAYER_OBJ_NAME, panel);

            Seams.AddMenuItems(Seams.TransformOf(content), Seams.TransformOf(dropdownLayer));
            return new Menu(panel, content, dropdownLayer);
        }

        public static string Saved(string varID) => variables.Get(varID);

        /// <summary>
        /// Saves a setting directly; variables.Set's first write of a name raises the task system's event.
        /// </summary>
        public static void Save(string varID, string value) =>
            variables.variable.Add(new variables._variable { name = varID, value = value });

        public static string RepoRoot()
        {
            DirectoryInfo dir = new(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tel Mod Library.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        /// <summary>
        /// The parts of the main menu's settings popup the mod copies: the Autosave slider and Run In
        /// Background checkbox from the fourth tab, and the Quality dropdown from the Screen tab.
        /// </summary>
        private static GameObject VanillaSettings()
        {
            GameObject popup = Seams.NewObject("Settings");
            GameObject panel = Seams.NewObject("Panel", popup);
            GameObject tabs = Seams.NewObject("Tabs", panel);
            GameObject gameTab = Seams.NewObject("Game", panel);
            GameObject screenTab = Seams.NewObject("Screen Settings", panel);
            Seams.Add<TabButton>(Seams.NewObject("TabButton (3)", tabs)).TabToOpen = gameTab;

            Seams.AddPrefab(gameTab, AutosaveSlider);
            Seams.AddPrefab(gameTab, RunInBackgroundCheckbox);
            Seams.AddPrefab(screenTab, QualityDropdown);
            return popup;
        }

        private static GameObject AutosaveSlider()
        {
            GameObject root = Seams.NewObject("Autosave");
            GameObject title = Seams.NewObject("Title", root);
            Seams.Add<TextMeshProUGUI>(title);
            Seams.Add<Lang_Button>(title).Constant = "AUTOSAVE";
            GameObject track = Seams.NewObject("Slider", root);
            Slider slider = Seams.Add<Slider>(track);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.onValueChanged.AddListener(_ => throw new InvalidOperationException("Vanilla's autosave listener ran"));
            Seams.SetSliderValue(slider, 0.3f, notify: false);

            Settings_Slider settings = Seams.Add<Settings_Slider>(root);
            settings.Title = title;
            settings.Slider_Obj = track;
            settings.Min_Value = 0;
            settings.Max_Value = 100;
            settings.Title_Text = "AUTOSAVE";
            return root;
        }

        private static GameObject RunInBackgroundCheckbox()
        {
            GameObject root = Seams.NewObject("Run In Background");
            GameObject box = Seams.NewObject("Checkbox", root);
            Seams.Add<Image>(box);
            Button button = Seams.Add<Button>(box);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => throw new InvalidOperationException("Vanilla's run in background listener ran"));
            GameObject title = Seams.NewObject("Title", root);
            Seams.Add<TextMeshProUGUI>(title);
            Seams.Add<Lang_Button>(title).Constant = "RUN_IN_BACKGROUND";

            Checkbox_Text checkbox = Seams.Add<Checkbox_Text>(root);
            checkbox.Checkbox = box;
            checkbox.Title = title;
            checkbox.Checked = Checked;
            checkbox.Empty = Empty;
            return root;
        }

        private static GameObject QualityDropdown()
        {
            GameObject root = Seams.NewObject("Quality");
            GameObject title = Seams.NewObject("Title", root);
            Seams.Add<TextMeshProUGUI>(title);
            Seams.Add<Lang_Button>(title).Constant = "QUALITY";
            CustomDropdown dropdown = Seams.Add<CustomDropdown>(Seams.NewObject("Dropdown", root));
            dropdown.dropdownItems = new List<CustomDropdown.Item>
            {
                new() { itemName = "Low" },
                new() { itemName = "High" },
            };
            dropdown.dropdownEvent = new CustomDropdown.DropdownEvent();
            dropdown.dropdownEvent.AddListener(_ => throw new InvalidOperationException("Vanilla's quality listener ran"));
            return root;
        }

        /// <summary>
        /// The rival name popup's first name field, which the mod copies for text fields: its label, and its
        /// text and underline in a masked container. Tab moves to the last name field, and typing renames the rival.
        /// </summary>
        private static GameObject RivalFirstNameField()
        {
            GameObject root = Seams.NewObject("Rival First Name");
            Seams.Add<Image>(root);
            Seams.Add<ButtonDefault>(root);
            Seams.Add<InputField_Tab>(root);
            GameObject title = Seams.NewObject("Title", root);
            Seams.Add<Lang_Button>(title).Constant = "STORY__RIVAL_FIRST_NAME";
            Seams.Add<TextMeshProUGUI>(title);
            GameObject container = Seams.NewObject("Container", root);
            Seams.Add<RectMask2D>(container);
            Seams.Add<TextMeshProUGUI>(Seams.NewObject("Text", container));
            Seams.Add<Image>(Seams.NewObject("Image", container));

            TMP_InputField field = Seams.Add<TMP_InputField>(root);
            Seams.SetInputText(field, "");
            AccessTools.FieldRefAccess<TMP_InputField, int>("m_CharacterLimit")(field) = 10;
            field.onValueChanged = new TMP_InputField.OnChangeEvent();
            field.onValueChanged.AddListener(_ => throw new InvalidOperationException("Vanilla's rival name listener ran"));
            return root;
        }
    }

    /// <summary>
    /// A built mod menu, and what the player does with it.
    /// </summary>
    internal class Menu
    {
        public readonly GameObject Panel;
        public readonly GameObject Content;
        public readonly GameObject DropdownLayer;
        private bool awake;

        public Menu(GameObject panel, GameObject content, GameObject dropdownLayer)
        {
            Panel = panel;
            Content = content;
            DropdownLayer = dropdownLayer;
        }

        public ModMenuManager Manager => Seams.GetComponent<ModMenuManager>(Panel);

        /// <summary>
        /// The rows from the top of the menu down.
        /// </summary>
        public List<GameObject> Rows => Seams.ChildrenOf(Content).ToList();

        public List<string> RowNames => Rows.Select(Seams.NameOf).ToList();

        public List<ModMenuItem> Items => Rows.Select(Seams.GetComponent<ModMenuItem>).Where(item => item is not null).ToList();

        public ModMenuItem Item(string varID) => Assert.Single(Items, item => item.varID == varID);

        public GameObject Row(string varID) => Seams.OwnerOf(Item(varID));

        /// <summary>
        /// Opens the popup. Unity wakes each item the first time it's shown, then enables the panel and items.
        /// </summary>
        public void Open()
        {
            if (!awake)
            {
                Items.ForEach(Seams.ItemAwake);
                awake = true;
            }
            Seams.ManagerOnEnable(Manager);
            Items.ForEach(Seams.ItemOnEnable);
        }

        public void Apply() => Seams.ManagerOnApply(Manager);

        public void Cancel() => Seams.ManagerOnCancel(Manager);

        public Slider Slider(string varID) => Seams.GetComponentInChildren<Slider>(Row(varID));

        public Settings_Slider SliderSettings(string varID) => Seams.GetComponent<Settings_Slider>(Row(varID));

        public Checkbox_Text Checkbox(string varID) => Seams.GetComponent<Checkbox_Text>(Row(varID));

        public CustomDropdown Dropdown(string varID) => Seams.GetComponentInChildren<CustomDropdown>(Row(varID));

        /// <summary>
        /// Drags a slider to this point along its track, from 0 at the left to 1 at the right.
        /// </summary>
        public void Drag(string varID, float position) => Seams.SetSliderValue(Slider(varID), position);

        public float SliderPosition(string varID) => Seams.SliderValueOf(Slider(varID));

        public string SliderText(string varID) => Seams.GetComponent<TextMeshProUGUI>(SliderSettings(varID).Title).text;

        public void Click(string varID) => Seams.Fire(Seams.GetComponentInChildren<Button>(Row(varID)).onClick);

        /// <summary>
        /// Whether the checkbox shows a tick.
        /// </summary>
        public bool IsTicked(string varID)
        {
            Sprite shown = Seams.GetComponent<Image>(Checkbox(varID).Checkbox).sprite;
            Assert.True(ReferenceEquals(shown, TestGame.Checked) || ReferenceEquals(shown, TestGame.Empty), "The checkbox was never drawn");
            return ReferenceEquals(shown, TestGame.Checked);
        }

        /// <summary>
        /// Picks a dropdown entry, as the entry's button does.
        /// </summary>
        public void Select(string varID, int index)
        {
            CustomDropdown dropdown = Dropdown(varID);
            Seams.ChangeDropdownInfo(dropdown, index);
            Seams.Fire(dropdown.dropdownEvent, index);
        }

        public int Selected(string varID) => Dropdown(varID).selectedItemIndex;

        public TMP_InputField Field(string varID) => Seams.GetComponentInChildren<TMP_InputField>(Row(varID));

        /// <summary>
        /// The text a text field shows.
        /// </summary>
        public string FieldText(string varID) => Field(varID).text;

        /// <summary>
        /// Types into a text field: its text changes and, as each key press does, it tells its listeners.
        /// </summary>
        public void Type(string varID, string text)
        {
            TMP_InputField field = Field(varID);
            Seams.SetInputText(field, text);
            Seams.Fire(field.onValueChanged, text);
        }
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
    /// Unity's logger writes through native code; variables.Set logs each new setting.
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
        /// This object then its descendants, depth first, the order GetComponentInChildren searches.
        /// </summary>
        public IEnumerable<Node> SelfAndDescendants() => new[] { this }.Concat(Children.SelectMany(c => c.SelfAndDescendants()));
    }

    /// <summary>
    /// ModMenus builds and runs its menu with Unity's scene and UI calls (GameObjects, transforms, components,
    /// text, sliders), which are native code that can't run outside the game. Harmony can't patch the mod's
    /// methods in place either, because it compiles the originals first and the engine getters can't be
    /// compiled. So tests call copies of the mod's methods (and the vanilla UI methods they call), which call
    /// these stubs instead. The stubs keep a small fake scene.
    /// </summary>
    internal static class Seams
    {
        /// <summary>
        /// What PopupManager.GetObject returns for the main menu's settings popup.
        /// </summary>
        public static GameObject SettingsPopup;

        /// <summary>
        /// What ModMenusUtils.VanillaTextField returns: the rival name popup's first name field.
        /// </summary>
        public static GameObject TextField;

        public static int PopupsClosed;

        /// <summary>
        /// The entries each dropdown was set up with, when SetupDropdown last built its list.
        /// </summary>
        public static readonly Dictionary<object, List<string>> DropdownSetups = new(new SameObject());

        public static readonly Dictionary<object, float> ScrollPositions = new(new SameObject());

        /// <summary>
        /// The alignment each text was given. TextMeshPro stores it split in two, so it's kept here instead.
        /// </summary>
        public static readonly Dictionary<object, TextAlignmentOptions> Alignments = new(new SameObject());

        private static readonly Dictionary<object, Node> Nodes = new(new SameObject());
        private static readonly Dictionary<object, Func<GameObject>> Prefabs = new(new SameObject());
        private static readonly Dictionary<object, float> SliderValues = new(new SameObject());

        /// <summary>
        /// The mod's methods under test and the vanilla UI methods they call, each with the stand-in that becomes its copy.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Copies = new()
        {
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuItems))] = Stub(nameof(AddMenuItems)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuText))] = Stub(nameof(AddMenuText)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuSlider))] = Stub(nameof(AddMenuSlider)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuCheckbox))] = Stub(nameof(AddMenuCheckbox)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuDropdown))] = Stub(nameof(AddMenuDropdown)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.PlaceDropdownInRow))] = Stub(nameof(PlaceDropdownInRow)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.AddMenuInput))] = Stub(nameof(AddMenuInput)),
            [AccessTools.FirstMethod(AccessTools.Inner(typeof(ModMenusUtils), "<>c"), m => m.Name.Contains(nameof(ModMenusUtils.AddMenuDropdown)))] = Stub(nameof(IsScreenTab)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.Awake))] = Stub(nameof(ItemAwake)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.OnEnable))] = Stub(nameof(ItemOnEnable)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.CloseList))] = Stub(nameof(ItemCloseList)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.RenderSlider))] = Stub(nameof(ItemRenderSlider)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.RenderCheckbox))] = Stub(nameof(ItemRenderCheckbox)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.RenderDropdown))] = Stub(nameof(ItemRenderDropdown)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.RenderInput))] = Stub(nameof(ItemRenderInput)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.onClickCheck))] = Stub(nameof(ItemOnClickCheck)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.onUpdateSlider))] = Stub(nameof(ItemOnUpdateSlider)),
            [AccessTools.Method(typeof(ModMenuItem), nameof(ModMenuItem.onUpdateDropdown))] = Stub(nameof(ItemOnUpdateDropdown)),
            [AccessTools.Method(typeof(ModMenuManager), nameof(ModMenuManager.OnEnable))] = Stub(nameof(ManagerOnEnable)),
            [AccessTools.Method(typeof(ModMenuManager), nameof(ModMenuManager.OnApply))] = Stub(nameof(ManagerOnApply)),
            [AccessTools.Method(typeof(ModMenuManager), nameof(ModMenuManager.OnCancel))] = Stub(nameof(ManagerOnCancel)),
            [AccessTools.Method(typeof(Checkbox_Text), nameof(Checkbox_Text.SetCheck))] = Stub(nameof(SetCheck)),
            [AccessTools.Method(typeof(ExtensionMethods), nameof(ExtensionMethods.SetColor), new[] { typeof(GameObject), typeof(Color32) })] = Stub(nameof(SetColor)),
        };

        /// <summary>
        /// What the copies call instead: each other, and stubs for the engine.
        /// </summary>
        private static readonly Dictionary<MethodBase, MethodInfo> Redirects = new(Copies)
        {
            // The fake scene
            [AccessTools.Constructor(typeof(GameObject), new[] { typeof(string), typeof(Type[]) })] = Stub(nameof(StubNewGameObject)),
            [AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))] = Stub(nameof(StubTransform)),
            [AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))] = Stub(nameof(StubGameObject)),
            [AccessTools.PropertyGetter(typeof(Object), nameof(Object.name))] = Stub(nameof(StubGetName)),
            [AccessTools.PropertySetter(typeof(Object), nameof(Object.name))] = Stub(nameof(StubSetName)),
            [AccessTools.Method(typeof(Object), "op_Equality")] = Stub(nameof(StubEquality)),
            [AccessTools.Method(typeof(Object), "op_Inequality")] = Stub(nameof(StubInequality)),
            [AccessTools.Method(typeof(Object), "op_Implicit")] = Stub(nameof(StubExists)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })] = Stub(nameof(StubFind)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) })] = Stub(nameof(StubSetParent)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.GetSiblingIndex))] = Stub(nameof(StubGetSiblingIndex)),
            [AccessTools.Method(typeof(Transform), nameof(Transform.SetAsFirstSibling))] = Stub(nameof(StubSetAsFirstSibling)),
            [AccessTools.Method(typeof(Object), nameof(Object.DestroyImmediate), new[] { typeof(Object) })] = Stub(nameof(StubDestroyImmediate)),

            // Layout and looks
            [AccessTools.Method(typeof(ModMenusUtils), "SetRectTransform", new[] { typeof(RectTransform), typeof(Vector2), typeof(Vector2), typeof(Vector2), typeof(Vector2) })] = Stub(nameof(StubSetRect)),
            [AccessTools.Method(typeof(ModMenusUtils), "SetRectTransform", new[] { typeof(RectTransform), typeof(Vector2), typeof(Vector2), typeof(Vector2), typeof(Vector2), typeof(Vector2) })] = Stub(nameof(StubSetRectPivot)),
            [AccessTools.PropertySetter(typeof(RectTransform), nameof(RectTransform.sizeDelta))] = Stub(nameof(StubSetSize)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text))] = Stub(nameof(StubSetText)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.fontSize))] = Stub(nameof(StubSetFontSize)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.alignment))] = Stub(nameof(StubSetAlignment)),
            [AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertySetter(typeof(Graphic), nameof(Graphic.color))] = Stub(nameof(StubSetColor)),
            [AccessTools.PropertySetter(typeof(Image), nameof(Image.sprite))] = Stub(nameof(StubSetSprite)),

            // UI controls
            [AccessTools.PropertyGetter(typeof(Slider), nameof(Slider.value))] = Stub(nameof(SliderValueOf)),
            [AccessTools.PropertySetter(typeof(Slider), nameof(Slider.value))] = Stub(nameof(StubSetSliderValue)),
            [AccessTools.PropertySetter(typeof(ScrollRect), nameof(ScrollRect.verticalNormalizedPosition))] = Stub(nameof(StubSetScroll)),
            [AccessTools.Method(typeof(CustomDropdown), nameof(CustomDropdown.SetupDropdown))] = Stub(nameof(StubSetupDropdown)),
            [AccessTools.Method(typeof(CustomDropdown), nameof(CustomDropdown.ChangeDropdownInfo))] = Stub(nameof(ChangeDropdownInfo)),
            [AccessTools.PropertySetter(typeof(TMP_InputField), nameof(TMP_InputField.characterLimit))] = Stub(nameof(StubSetCharacterLimit)),
            [AccessTools.Method(typeof(TMP_InputField), nameof(TMP_InputField.SetTextWithoutNotify))] = Stub(nameof(SetInputText)),
            [AccessTools.Method(typeof(PopupManager), nameof(PopupManager.GetObject))] = Stub(nameof(StubGetPopup)),
            [AccessTools.Method(typeof(ModMenusUtils), nameof(ModMenusUtils.VanillaTextField))] = Stub(nameof(StubTextField)),
            [AccessTools.Method(typeof(PopupManager), nameof(PopupManager.Close_))] = Stub(nameof(StubClosePopup)),
        };

        /// <summary>
        /// Generic engine methods, redirected whatever their type argument. They reach native code and the JIT
        /// can inline them, so every call goes to a stub.
        /// </summary>
        private static readonly Dictionary<MethodInfo, MethodInfo> GenericRedirects = new()
        {
            [Generic(typeof(Component), nameof(Component.GetComponent), 0)] = Stub(nameof(GetComponent)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponent), 0)] = Stub(nameof(GetComponent)),
            [Generic(typeof(Component), nameof(Component.GetComponentInChildren), 0)] = Stub(nameof(GetComponentInChildren)),
            [Generic(typeof(GameObject), nameof(GameObject.GetComponentInChildren), 0)] = Stub(nameof(GetComponentInChildren)),
            [Generic(typeof(Component), nameof(Component.GetComponentsInChildren), 0)] = Stub(nameof(StubGetComponentsInChildren)),
            [Generic(typeof(GameObject), nameof(GameObject.AddComponent), 0)] = Stub(nameof(StubAddComponent)),
            [Generic(typeof(Object), nameof(Object.Instantiate), 2)] = Stub(nameof(StubInstantiate)),
            [Generic(typeof(Enumerable), nameof(Enumerable.Cast), 1)] = Stub(nameof(StubCast)),
            [Generic(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), 2)] = Stub(nameof(StubFirstOrDefault)),
        };

        private static readonly Lazy<bool> Installed = new(() =>
        {
            foreach (KeyValuePair<MethodBase, MethodInfo> copy in Copies)
                Harmony.ReversePatch(copy.Key, new HarmonyMethod(copy.Value), Stub(nameof(Redirect)), ilmanipulator: null);
            Debug.unityLogger.logHandler = new Log();
            return true;
        });

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddMenuItems(Transform parentTransform, Transform dropdownLayer) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject AddMenuText(string textID, Transform parentTransform, float fontSize, Color col, TextAlignmentOptions alignment) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject AddMenuSlider(string varID, string labelID, float min, float max, float def, Transform parentTransform) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject AddMenuCheckbox(string varID, string labelID, bool def, Transform parentTransform) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject AddMenuDropdown(string varID, string labelID, string[] itemLabelIDs, int def, Transform parentTransform, Transform dropdownLayer) => throw NotInstalled();
        public static void PlaceDropdownInRow(RectTransform dropdown) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject AddMenuInput(string varID, string labelID, string def, int maxLength, Transform parentTransform) => throw NotInstalled();

        /// <summary>
        /// AddMenuDropdown's lambda that finds the settings popup's Screen tab by name.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsScreenTab(object closure, Transform t) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemAwake(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemOnEnable(ModMenuItem instance) => throw NotInstalled();
        public static void ItemCloseList(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemRenderSlider(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemRenderCheckbox(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemRenderDropdown(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemRenderInput(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemOnClickCheck(ModMenuItem instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemOnUpdateSlider(ModMenuItem instance, float val) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ItemOnUpdateDropdown(ModMenuItem instance, int val) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ManagerOnEnable(ModMenuManager instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ManagerOnApply(ModMenuManager instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ManagerOnCancel(ModMenuManager instance) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetCheck(Checkbox_Text instance, bool val) => throw NotInstalled();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetColor(GameObject obj, Color32 clr) => throw NotInstalled();

        private static Exception NotInstalled() => new InvalidOperationException("Call Seams.Reset first");

        /// <summary>
        /// Installs the copies once per test run and clears the fake scene of earlier tests.
        /// </summary>
        public static void Reset()
        {
            _ = Installed.Value;
            SettingsPopup = null;
            TextField = null;
            PopupsClosed = 0;
            DropdownSetups.Clear();
            ScrollPositions.Clear();
            Alignments.Clear();
            Nodes.Clear();
            Prefabs.Clear();
            SliderValues.Clear();
            Log.Messages.Clear();
        }

        /// <summary>
        /// Unity objects can't be constructed outside the game. The code under test only reads the fields the tests set.
        /// </summary>
        public static T Fake<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        public static GameObject NewObject(string name, GameObject parent = null) => NewNode(name, parent is null ? null : NodeOf(parent)).GameObject;

        public static T Add<T>(GameObject owner) where T : Component => (T)AddComponent(NodeOf(owner), typeof(T));

        /// <summary>
        /// Adds an object built by this function under the parent. Instantiating it builds a fresh one, the
        /// way Unity copies the object with its own components and children.
        /// </summary>
        public static void AddPrefab(GameObject parent, Func<GameObject> build) => SetParent(NodeOf(Prefab(build)), NodeOf(parent));

        /// <summary>
        /// An object built by this function, which instantiating copies (see <see cref="AddPrefab"/>).
        /// </summary>
        public static GameObject Prefab(Func<GameObject> build)
        {
            GameObject prefab = build();
            Prefabs[prefab] = build;
            return prefab;
        }

        /// <summary>
        /// Sets the text a text field holds, without telling its listeners, as SetTextWithoutNotify does.
        /// </summary>
        public static void SetInputText(TMP_InputField field, string text) => AccessTools.FieldRefAccess<TMP_InputField, string>("m_Text")(field) = text;

        public static Transform TransformOf(object owner) => NodeOf(owner).Transform;

        public static GameObject OwnerOf(Component component) => NodeOf(component).GameObject;

        public static string NameOf(GameObject obj) => NodeOf(obj).Name;

        public static List<GameObject> ChildrenOf(GameObject obj) => NodeOf(obj).Children.Select(c => c.GameObject).ToList();

        public static T GetComponent<T>(object owner) => NodeOf(owner).Components.OfType<T>().FirstOrDefault();

        public static T GetComponentInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants().SelectMany(n => n.Components).OfType<T>().FirstOrDefault();

        public static float SliderValueOf(Slider slider) => SliderValues.TryGetValue(slider, out float value) ? value : 0f;

        /// <summary>
        /// Moves a slider and, as Slider.Set does, tells its listeners if the value changed.
        /// </summary>
        public static void SetSliderValue(Slider slider, float value, bool notify = true)
        {
            value = Mathf.Clamp01(value);
            if (SliderValues.TryGetValue(slider, out float current) && current == value)
                return;
            SliderValues[slider] = value;
            if (notify)
                Fire(slider.onValueChanged, value);
        }

        /// <summary>
        /// The real method also shows the entry's name and icon, which nothing here reads.
        /// It reads the entry first, so an index past the list fails as it would in game.
        /// </summary>
        public static void ChangeDropdownInfo(CustomDropdown dropdown, int itemIndex)
        {
            _ = dropdown.dropdownItems[itemIndex];
            dropdown.selectedItemIndex = itemIndex;
        }

        /// <summary>
        /// The listeners added to an event at runtime.
        /// </summary>
        public static List<Delegate> Listeners(UnityEventBase unityEvent)
        {
            object calls = Traverse.Create(unityEvent).Field("m_Calls").GetValue();
            IList runtimeCalls = Traverse.Create(calls).Field("m_RuntimeCalls").GetValue<IList>();
            return runtimeCalls.Cast<object>().Select(call => Traverse.Create(call).Field("Delegate").GetValue<Delegate>()).ToList();
        }

        /// <summary>
        /// Calls an event's listeners. UnityEvent.Invoke skips listeners on destroyed objects, and every fake
        /// counts as destroyed, so this calls them itself, through the copies for the mod's own methods.
        /// </summary>
        public static void Fire(UnityEventBase unityEvent, params object[] args)
        {
            foreach (Delegate listener in Listeners(unityEvent))
            {
                try
                {
                    if (Redirects.TryGetValue(listener.Method, out MethodInfo copy))
                        copy.Invoke(null, new[] { listener.Target }.Concat(args).ToArray());
                    else
                        listener.DynamicInvoke(args);
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }
            }
        }

        private static Node NodeOf(object owner)
        {
            Assert.True(owner is not null && Nodes.ContainsKey(owner), $"Not a fake scene object: {owner?.GetType().Name ?? "null"}");
            return Nodes[owner];
        }

        private static Node NewNode(string name, Node parent)
        {
            Node node = new() { Name = name, GameObject = Fake<GameObject>(), Transform = Fake<RectTransform>() };
            Nodes[node.GameObject] = node;
            Nodes[node.Transform] = node;
            node.Components.Add(node.Transform);
            if (parent != null)
                SetParent(node, parent);
            return node;
        }

        private static Component AddComponent(Node node, Type type)
        {
            Component component = (Component)FormatterServices.GetUninitializedObject(type);
            node.Components.Add(component);
            Nodes[component] = node;
            return component;
        }

        private static void SetParent(Node node, Node parent)
        {
            node.Parent?.Children.Remove(node);
            node.Parent = parent;
            parent?.Children.Add(node);
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
                MethodInfo stub = null;
                if (instruction.operand is ConstructorInfo constructor && instruction.opcode == OpCodes.Newobj)
                    Redirects.TryGetValue(constructor, out stub);
                else if (instruction.operand is MethodInfo method && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
                {
                    if (!Redirects.TryGetValue(method, out stub) && method.IsGenericMethod
                        && GenericRedirects.TryGetValue(method.GetGenericMethodDefinition(), out MethodInfo generic))
                        stub = generic.MakeGenericMethod(method.GetGenericArguments());
                }

                if (stub != null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = stub;
                }
                yield return instruction;
            }
        }

        private static MethodInfo Stub(string name) => AccessTools.Method(typeof(Seams), name);

        private static GameObject StubNewGameObject(string name, Type[] components)
        {
            Node node = NewNode(name, null);
            foreach (Type type in components.Where(t => t != typeof(RectTransform)))
                AddComponent(node, type);
            return node.GameObject;
        }

        private static Transform StubTransform(object owner) => NodeOf(owner).Transform;
        private static GameObject StubGameObject(Component component) => NodeOf(component).GameObject;
        private static string StubGetName(Object obj) => NodeOf(obj).Name;
        private static void StubSetName(Object obj, string name) => NodeOf(obj).Name = name;
        private static bool StubEquality(Object a, Object b) => ReferenceEquals(a, b);
        private static bool StubInequality(Object a, Object b) => !ReferenceEquals(a, b);
        private static bool StubExists(Object obj) => obj is not null;

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

        private static void StubSetParent(Transform transform, Transform parent, bool worldPositionStays) =>
            SetParent(NodeOf(transform), parent is null ? null : NodeOf(parent));

        private static int StubGetSiblingIndex(Transform transform)
        {
            Node node = NodeOf(transform);
            return node.Parent?.Children.IndexOf(node) ?? 0;
        }

        private static void StubSetAsFirstSibling(Transform transform)
        {
            Node node = NodeOf(transform);
            node.Parent.Children.Remove(node);
            node.Parent.Children.Insert(0, node);
        }

        /// <summary>
        /// Destroying a component removes it from its object.
        /// </summary>
        private static void StubDestroyImmediate(Object obj)
        {
            Assert.True(obj is Component, "Destroyed something that isn't a component");
            NodeOf(obj).Components.Remove((Component)obj);
            Nodes.Remove(obj);
        }

        private static T[] StubGetComponentsInChildren<T>(object owner) =>
            NodeOf(owner).SelfAndDescendants().SelectMany(n => n.Components).OfType<T>().ToArray();

        private static T StubAddComponent<T>(GameObject owner) => (T)(object)AddComponent(NodeOf(owner), typeof(T));

        private static T StubInstantiate<T>(T original, Transform parent) where T : Object
        {
            Assert.True(Prefabs.TryGetValue(original, out Func<GameObject> build), "Instantiated an object that isn't a vanilla prefab");
            GameObject copy = build();
            SetParent(NodeOf(copy), NodeOf(parent));
            return (T)(object)copy;
        }

        /// <summary>
        /// Enumerating a Transform lists its children.
        /// </summary>
        private static IEnumerable<T> StubCast<T>(IEnumerable source) =>
            source is Transform transform ? NodeOf(transform).Children.Select(c => c.Transform).Cast<T>() : source.Cast<T>();

        /// <summary>
        /// The predicate is one of the mod's lambdas, which reads Unity names, so its copy is called instead.
        /// </summary>
        private static T StubFirstOrDefault<T>(IEnumerable<T> source, Func<T, bool> predicate)
        {
            Assert.True(Redirects.TryGetValue(predicate.Method, out MethodInfo copy), $"No copy of {predicate.Method.Name}");
            return source.FirstOrDefault(item => (bool)copy.Invoke(null, new object[] { predicate.Target, item }));
        }

        private static void StubSetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) { }
        private static void StubSetRectPivot(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot) { }
        private static void StubSetSize(RectTransform rt, Vector2 size) { }

        // Text and image setters store their value in the field the getter reads, so tests can read it back
        private static void StubSetText(TMP_Text text, string value) => AccessTools.FieldRefAccess<TMP_Text, string>("m_text")(text) = value;
        private static void StubSetFontSize(TMP_Text text, float value) => AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSize")(text) = value;
        private static void StubSetAlignment(TMP_Text text, TextAlignmentOptions value) => Alignments[text] = value;
        private static void StubSetSprite(Image image, Sprite value) => AccessTools.FieldRefAccess<Image, Sprite>("m_Sprite")(image) = value;

        private static void StubSetColor(Graphic graphic, Color value)
        {
            if (graphic is TMP_Text text)
                AccessTools.FieldRefAccess<TMP_Text, Color>("m_fontColor")(text) = value;
            else
                AccessTools.FieldRefAccess<Graphic, Color>("m_Color")(graphic) = value;
        }

        private static void StubSetSliderValue(Slider slider, float value) => SetSliderValue(slider, value);
        private static void StubSetScroll(ScrollRect scrollRect, float value) => ScrollPositions[scrollRect] = value;

        private static void StubSetupDropdown(CustomDropdown dropdown) =>
            DropdownSetups[dropdown] = dropdown.dropdownItems.Select(i => i.itemName).ToList();

        private static GameObject StubGetPopup(PopupManager._type type)
        {
            Assert.Equal(PopupManager._type.main_menu_settings, type);
            return SettingsPopup;
        }

        private static void StubClosePopup(Action onComplete) => PopupsClosed++;

        private static GameObject StubTextField() => TextField;

        private static void StubSetCharacterLimit(TMP_InputField field, int value) => AccessTools.FieldRefAccess<TMP_InputField, int>("m_CharacterLimit")(field) = value;
    }
}
