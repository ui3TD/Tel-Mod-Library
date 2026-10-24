using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HarmonyChecker
{
    internal static class HarmonyCheckerStatus
    {
        /// <summary>
        /// Shows on the main menu that IM-HI works: the Mods button gets back the label of the player's
        /// language (this mod's JSON changes it to say IM-HI isn't installed, which only shows when this DLL
        /// can't run), and a badge with a tick, or a "!" when IM-HI is out of date.
        /// </summary>
        public static void MarkInstalled(MainMenu_Buttons_Controller controller = null)
        {
            // The language table is the same everywhere, so this runs even outside the main menu.
            try
            {
                ModsLabel.Restore();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't restore the Mods label: " + e);
            }

            if (controller == null)
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                mainScript main = camera.GetComponent<mainScript>();
                if (main == null || main.Data == null)
                {
                    return;
                }

                controller = main.Data.GetComponent<MainMenu_Buttons_Controller>();
            }

            if (controller == null || controller.Main_Container == null)
            {
                return;
            }

            Version installed = IMHIUpdate.InstalledVersion();
            bool outdated = IMHIUpdate.IsOutdated(installed);
            Transform container = controller.Main_Container.transform;
            Transform modsTransform = container.Find("Mods");

            // Only the game's label is redrawn: another mod's label on the Mods button is kept
            Lang_Button modButton = modsTransform == null ? null : modsTransform.GetComponentInChildren<Lang_Button>();
            if (modButton != null && modButton.Constant == GAME_LABEL)
            {
                // MainMenu_Buttons_Controller.Start may already have run by the time IM-HI
                // finishes loading Workshop Harmony DLLs, so the button already shows the old text.
                modButton.ResetText();
            }

            if (modsTransform != null)
            {
                IMHIUpdate.ShowBadge(modsTransform, outdated, installed);
            }

            if (outdated)
            {
                IMHIUpdate.ShowButton(container, modsTransform, installed);
            }
        }

        public const string GAME_LABEL = "MM_MODS";
    }

    /// <summary>
    /// This mod's constants.json changes the game's Mods label (MM_MODS) to say IM-HI isn't installed, in every
    /// language. Once this DLL runs, IM-HI is installed, so the label goes back to what the game would show
    /// without this mod: the player's language, or a translation mod's label.
    /// </summary>
    internal static class ModsLabel
    {
        // Replaced in tests.
        internal static Func<IEnumerable<string>> ConstantsFiles = () => GameConstantsFiles();
        internal static Func<string, bool> IsOwnFile = path => IsUnder(path, OwnFolders());
        internal static Func<string, string> ReadFile = path => File.Exists(path) ? File.ReadAllText(path) : null;

        public static void Restore()
        {
            string label = WithoutThisMod(ConstantsFiles(), IsOwnFile, ReadFile);
            if (label != null)
            {
                Language.Data[HarmonyCheckerStatus.GAME_LABEL] = label;
            }
        }

        /// <summary>
        /// The Mods label from the files before this mod's, if this mod's file is the last one that sets it (as
        /// the game loads them, the last one wins); otherwise null, since a later mod's label is the one shown.
        /// A file that is missing (null text) is skipped, as the game skips it.
        /// </summary>
        public static string WithoutThisMod(IEnumerable<string> files, Func<string, bool> isOwn, Func<string, string> read)
        {
            string others = null;
            bool ownIsLast = false;
            foreach (string file in files)
            {
                string text = read(file);
                if (text == null)
                {
                    continue;
                }

                foreach (JSONNode constant in mainScript.ProcessInboundData(text).AsArray)
                {
                    if (constant["id"].Value != HarmonyCheckerStatus.GAME_LABEL)
                    {
                        continue;
                    }

                    ownIsLast = isOwn(file);
                    if (!ownIsLast)
                    {
                        others = constant["text"].Value;
                    }
                }
            }
            return ownIsLast ? others : null;
        }

        /// <summary>The constants files in the order Language._Load reads them: English first, then the language's and the mods'.</summary>
        private static IEnumerable<string> GameConstantsFiles()
        {
            string english = Path.Combine(Application.streamingAssetsPath, "Languages", "en", "JSON", "Constants", "constants.json");
            List<string> files = Mods.GetFilePaths(Path.Combine("JSON", "Constants", "constants.json"));
            if (!files.Contains(english))
            {
                files.Insert(0, english);
            }
            return files;
        }

        /// <summary>The folder of this DLL and of every mod with this mod's title.</summary>
        private static IEnumerable<string> OwnFolders() =>
            new[] { Path.GetDirectoryName(typeof(ModsLabel).Assembly.Location) }
                .Concat(Mods._Mods.Where(mod => mod != null && mod.Title == GoldButton.ModTitle).Select(mod => mod.Path));

        public static bool IsUnder(string path, IEnumerable<string> folders)
        {
            string file = Normalize(path);
            return folders.Where(folder => !string.IsNullOrEmpty(folder))
                .Any(folder => file.StartsWith(Normalize(folder).TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string path) => path.Replace('\\', '/');
    }

    /// <summary>
    /// A round badge on the right of the Mods button: a green tick when IM-HI works, an amber "!" when it is out
    /// of date. Hovering it shows what it means in the game's tooltip.
    /// </summary>
    internal static class StatusBadge
    {
        public const string NAME = "IMHI_Status";

        // The badge's size, and its gap to the right edge of the Mods button (which is 50 high).
        public const float SIZE = 28f;
        public const float INSET = 12f;

        public const string OK_TEXT = "IMHI_STATUS_OK";
        public const string OUTDATED_TEXT = "IMHI_STATUS_OUTDATED";
        public const string OK_FALLBACK = "IM-HarmonyIntegration is installed";
        public const string OUTDATED_FALLBACK = "Please update IM-HarmonyIntegration";

        public static readonly GoldButton.Picture Ok = new GoldButton.Picture("IMHI_Badge_OK.png", Vector4.zero);
        public static readonly GoldButton.Picture Outdated = new GoldButton.Picture("IMHI_Badge_Outdated.png", Vector4.zero);

        // Replaced in tests.
        internal static Func<bool, Sprite> Picture = outdated => (outdated ? Outdated : Ok).Get();

        /// <summary>
        /// Adds the badge to the Mods button, or updates the one already there; runs on every menu refresh.
        /// Without its picture there is no badge. If building it fails, the half-built badge is removed, so the
        /// next refresh tries again, and the error is passed on.
        /// </summary>
        public static void Show(Transform modsButton, bool outdated, Version installed)
        {
            Sprite sprite = Picture(outdated);
            if (sprite is null)
            {
                return;
            }

            Transform existing = modsButton.Find(NAME);
            GameObject badge = existing == null ? null : existing.gameObject;
            if (badge == null)
            {
                badge = new GameObject(NAME, typeof(RectTransform));
                try
                {
                    badge.transform.SetParent(modsButton, false);
                    badge.AddComponent<LayoutElement>().ignoreLayout = true;

                    // Centred on the button's right edge, moved in by INSET
                    RectTransform rect = (RectTransform)badge.transform;
                    rect.anchorMin = new Vector2(1f, 0.5f);
                    rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.offsetMin = new Vector2(-INSET - SIZE, -SIZE / 2f);
                    rect.offsetMax = new Vector2(-INSET, SIZE / 2f);

                    // The image takes the mouse so the badge can show its tooltip; a click still goes on to the
                    // Mods button, since the badge has no click handler of its own.
                    badge.AddComponent<Image>();
                    badge.AddComponent<BadgeTooltip>();
                }
                catch
                {
                    Object.Destroy(badge);
                    throw;
                }
            }

            badge.GetComponent<Image>().sprite = sprite;
            badge.GetComponent<BadgeTooltip>().Text = Text(outdated, installed);
        }

        /// <summary>The tooltip, from the language table (so a translation mod can change it), versions filled in.</summary>
        public static string Text(bool outdated, Version installed)
        {
            string key = outdated ? OUTDATED_TEXT : OK_TEXT;
            string text = Language.Data.TryGetValue(key, out string translated) ? translated : outdated ? OUTDATED_FALLBACK : OK_FALLBACK;

            // An unknown version is left out
            return text.Replace("@1", installed?.ToString() ?? "").Replace("@2", IMHIUpdate.MIN_VERSION.ToString()).Replace("  ", " ");
        }
    }

    /// <summary>
    /// Shows the badge's text in the game's tooltip while the mouse is over the badge. The game hides its
    /// tooltip each frame it isn't asked for, so this asks every frame, as the game's buttons do.
    /// </summary>
    internal class BadgeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // The light tooltip of the game's other screens (their TooltipManager's LightBG and LightText). The main
        // menu's tooltip is left in its dark prefab colours, since no menu button has a tooltip.
        public static readonly Color32 LIGHT_BG = new Color32(255, 255, 255, 229);
        public static readonly Color32 LIGHT_TEXT = new Color32(119, 123, 186, 255);

        public string Text;

        public bool Hovered { get; private set; }

        private TooltipManager tooltips;

        // The tooltip's background and text while this badge has recoloured them, with their colours before.
        private Image themedBackground;
        private TextMeshProUGUI themedText;
        private Color backgroundBefore, textBefore;

        public void OnPointerEnter(PointerEventData eventData) => Hovered = true;

        public void OnPointerExit(PointerEventData eventData) => Hovered = false;

        private void Update()
        {
            if (!Hovered || string.IsNullOrEmpty(Text))
            {
                RestoreTheme();
                return;
            }

            if (tooltips == null)
            {
                mainScript main = Camera.main == null ? null : Camera.main.GetComponent<mainScript>();
                tooltips = main == null || main.Data == null ? null : main.Data.GetComponent<TooltipManager>();
            }
            if (tooltips != null)
            {
                Theme();
                tooltips.ShowTooltip(Text);
            }

            // The Mods button only counts the mouse as over it when it is over the button itself, not the badge
            staticVars.setCursor(staticVars.CursorType.pointer);
        }

        /// <summary>Gives the tooltip the game's light colours, keeping its own to put back.</summary>
        private void Theme()
        {
            if (themedText != null || tooltips.TextObj == null)
            {
                return;
            }

            // The text sits in the background (TooltipManager.TextObj in "BG")
            TextMeshProUGUI text = tooltips.TextObj.GetComponent<TextMeshProUGUI>();
            Image background = text == null || text.transform.parent == null ? null : text.transform.parent.GetComponent<Image>();
            if (background == null)
            {
                return;
            }

            themedText = text;
            themedBackground = background;
            textBefore = text.color;
            backgroundBefore = background.color;
            text.color = LIGHT_TEXT;
            background.color = LIGHT_BG;
        }

        /// <summary>Puts the tooltip's own colours back, so other tooltips on the menu look as before.</summary>
        private void RestoreTheme()
        {
            if (themedText == null)
            {
                return;
            }

            themedText.color = textBefore;
            if (themedBackground != null)
            {
                themedBackground.color = backgroundBefore;
            }
            themedText = null;
            themedBackground = null;
        }

        // A hidden badge gets no mouse-leave, so it stops counting as hovered here.
        private void OnDisable()
        {
            Hovered = false;
            RestoreTheme();
        }
    }

    /// <summary>
    /// Offers a link to the IM-HI releases page when the installed plugin is older than
    /// the version this mod asks for. Bump MIN_VERSION with each IM-HI release.
    /// </summary>
    internal static class IMHIUpdate
    {
        public static readonly Version MIN_VERSION = new Version(1, 2, 0);

        public const string IMHI_GUID = "com.name.HarmonyIntegration";
        public const string CHAINLOADER_TYPE = "BepInEx.Bootstrap.Chainloader";
        public const string RELEASES_URL = "https://github.com/ui3TD/IM-HarmonyIntegration/releases/latest";

        public const string BUTTON_NAME = "IMHI_Update";
        public const string BUTTON_LABEL = "IMHI_UPDATE";
        public const string FALLBACK_LABEL = "Update IM-HI to @2";

        // The drawn label, versions filled in. The game clears the language table when the
        // language changes, so the button then keeps this text instead of showing @1 and @2.
        public const string DRAWN_LABEL = "IMHI_UPDATE_DRAWN";

        // Replaced in tests.
        internal static Func<Version> InstalledVersion = () => ReadInstalledVersion(AppDomain.CurrentDomain.GetAssemblies());
        internal static Action<string> OpenUrl = url => Application.OpenURL(url);
        internal static Func<Sprite> GoldSprite = () => GoldButton.Button.Get();
        internal static Action<GameObject> AddGlow = button => GoldButton.AddGlow(button);
        internal static Func<TMP_FontAsset> MenuFont = () => FindMenuFont();
        internal static Action<Transform, bool, Version> ShowBadge = (modsButton, outdated, installed) => StatusBadge.Show(modsButton, outdated, installed);

        // The game's menu buttons, as the game draws them: 50 high (the menu's column sets their width), the
        // picture at twice its pixel density, and white text in the menu font, centred and raised a little.
        public const float BUTTON_HEIGHT = 50f;
        public const float PIXELS_PER_UNIT_MULTIPLIER = 2f;
        public const string MENU_FONT = "Linotte-SemiBold SDF";
        public const float FONT_SIZE = 20f;
        public const float TEXT_RAISE = 1.3f;

        private static TMP_FontAsset menuFont;

        /// <summary>The menu buttons' font, found by name among the loaded fonts; null if it isn't loaded.</summary>
        private static TMP_FontAsset FindMenuFont()
        {
            if (menuFont == null)
            {
                menuFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(font => font.name == MENU_FONT);
            }
            return menuFont;
        }

        /// <summary>
        /// The IM-HI version BepInEx loaded, or null if it can't be read. Harmony Checker doesn't
        /// reference BepInEx, so this reads Chainloader.PluginInfos[guid].Metadata.Version by name.
        /// </summary>
        public static Version ReadInstalledVersion(IEnumerable<Assembly> assemblies)
        {
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type chainloader = assembly.GetType(CHAINLOADER_TYPE, false);
                    if (chainloader == null)
                    {
                        continue;
                    }

                    IDictionary plugins = chainloader.GetProperty("PluginInfos", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null) as IDictionary;
                    if (plugins == null || !plugins.Contains(IMHI_GUID))
                    {
                        return null;
                    }

                    object plugin = plugins[IMHI_GUID];
                    object metadata = plugin?.GetType().GetProperty("Metadata")?.GetValue(plugin, null);
                    object version = metadata?.GetType().GetProperty("Version")?.GetValue(metadata, null);
                    return version != null && Version.TryParse(version.ToString(), out Version parsed) ? parsed : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Unknown versions count as up to date, so nobody is told to update by mistake.
        /// </summary>
        public static bool IsOutdated(Version installed) => installed != null && installed < MIN_VERSION;

        /// <summary>
        /// Adds a gold button to the main menu's column of buttons, below the Mods button if there is one, that
        /// opens the IM-HI releases page. It's built from the game's own button parts, as the menu's buttons are,
        /// not copied from the Mods button, so whatever the game or another mod has on that button stays there.
        /// The label is kept about as short as the Mods label, which fills the button. Runs on every menu
        /// refresh, so it adds the button only once. If building it fails, the half-built button is removed, so
        /// the next refresh tries again, and the error is passed on.
        /// </summary>
        public static void ShowButton(Transform container, Transform modsTransform, Version installed)
        {
            if (container.Find(BUTTON_NAME) != null)
            {
                return;
            }

            GameObject button = new GameObject(BUTTON_NAME, typeof(RectTransform));
            try
            {
                button.transform.SetParent(container, false);
                if (modsTransform != null && modsTransform.parent == container)
                {
                    button.transform.SetSiblingIndex(modsTransform.GetSiblingIndex() + 1);
                }
                ((RectTransform)button.transform).sizeDelta = new Vector2(0f, BUTTON_HEIGHT);

                // The game's menu button picture in gold, with white text like the other menu buttons. Without
                // the picture it falls back to a plain gold background with the game's dark text colour for gold.
                Sprite gold = GoldSprite();
                Image background = button.AddComponent<Image>();
                if (gold is null)
                {
                    background.color = mainScript.gold32;
                }
                else
                {
                    background.sprite = gold;
                    background.type = Image.Type.Sliced;
                    background.pixelsPerUnitMultiplier = PIXELS_PER_UNIT_MULTIPLIER;
                }

                Button click = button.AddComponent<Button>();
                click.onClick = new Button.ButtonClickedEvent();
                click.onClick.AddListener(() => OpenUrl(RELEASES_URL));

                // The game's button behaviour: its click and hover sounds and pointer, and being switched off
                // with the menu's other buttons while the menu is hidden
                button.AddComponent<ButtonDefault>();

                GameObject label = new GameObject("Text", typeof(RectTransform));
                label.transform.SetParent(button.transform, false);
                RectTransform labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(0f, TEXT_RAISE);
                labelRect.offsetMax = new Vector2(0f, TEXT_RAISE);

                TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
                TMP_FontAsset font = MenuFont();
                if (font is null)
                {
                    Debug.LogWarning("[Harmony Checker] The menu font " + MENU_FONT + " isn't loaded, so the update button uses the default font.");
                }
                else
                {
                    text.font = font;
                }
                text.fontSize = FONT_SIZE;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.color = gold is null ? (Color)mainScript.blackGold32 : Color.white;

                string constant = Language.Data.TryGetValue(BUTTON_LABEL, out string translated) ? translated : FALLBACK_LABEL;
                Language.Data[DRAWN_LABEL] = constant.Replace("@1", installed.ToString()).Replace("@2", MIN_VERSION.ToString());
                Lang_Button langButton = label.AddComponent<Lang_Button>();
                langButton.Constant = DRAWN_LABEL;
                langButton.ResetText();

                if (!(gold is null))
                {
                    AddGlow(button);
                }
            }
            catch
            {
                Object.Destroy(button);
                throw;
            }
        }
    }

    /// <summary>
    /// A gold copy of the game's menu button picture, which the game only has in blue, green and red,
    /// and a soft halo around it that pulses to draw the eye and holds bright while the mouse is over it.
    /// </summary>
    internal static class GoldButton
    {
        public const string ModTitle = "IM-HarmonyIntegration Plugin";

        // The halo picture reaches this many pixels past the button on each side.
        public const float GLOW_PAD = 16f;

        // The game's button picture's 9-slice edges (left, bottom, right, top), so it stretches the same way.
        public static readonly Picture Button = new Picture("IMHI_Update.png", new Vector4(11f, 17f, 11f, 12f));
        public static readonly Picture Glow = new Picture("IMHI_Update_Glow.png",
            new Vector4(11f + GLOW_PAD, 17f + GLOW_PAD, 11f + GLOW_PAD, 12f + GLOW_PAD));

        /// <summary>
        /// Puts the halo over the button, reaching past its edges. The halo is clear over the button
        /// itself, so it doesn't cover the button or its text. Without the picture the button has no halo.
        /// </summary>
        public static void AddGlow(GameObject button)
        {
            Sprite sprite = Glow.Get();
            if (sprite == null)
            {
                return;
            }

            GameObject glow = new GameObject("Glow", typeof(RectTransform));
            glow.transform.SetParent(button.transform, false);
            glow.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform rect = (RectTransform)glow.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-GLOW_PAD, -GLOW_PAD);
            rect.offsetMax = new Vector2(GLOW_PAD, GLOW_PAD);

            Image image = glow.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            GlowPulse pulse = glow.AddComponent<GlowPulse>();
            pulse.Image = image;
            button.AddComponent<GlowHover>().Pulse = pulse;
        }

        /// <summary>The picture's path: next to the DLL, else in a mod folder with this mod's title; null if neither has it.</summary>
        public static string FindFile(string relativePath, string assemblyDir, IEnumerable<string> modDirs)
        {
            return new[] { assemblyDir }.Concat(modDirs)
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Select(dir => Path.Combine(dir, relativePath))
                .FirstOrDefault(File.Exists);
        }

        internal sealed class Picture
        {
            public readonly string RelativePath;
            public readonly Vector4 Border;
            private Sprite sprite;
            private bool loadFailed;

            public Picture(string fileName, Vector4 border)
            {
                RelativePath = Path.Combine("Images", fileName);
                Border = border;
            }

            /// <summary>The picture, loaded once; null (logged once) if it can't be loaded.</summary>
            public Sprite Get()
            {
                if (sprite != null || loadFailed)
                {
                    return sprite;
                }

                string path = FindFile(RelativePath, Path.GetDirectoryName(typeof(GoldButton).Assembly.Location),
                    Mods._Mods.Where(mod => mod.Title == ModTitle).Select(mod => mod.GetPath()));
                Texture2D texture = path != null ? new Texture2D(2, 2, TextureFormat.RGBA32, false) : null;
                if (texture != null && ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                {
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                        100f, 0, SpriteMeshType.FullRect, Border);
                    sprite.name = Path.GetFileNameWithoutExtension(RelativePath);
                }
                else
                {
                    loadFailed = true;
                    Debug.LogError("[Harmony Checker] couldn't load " + RelativePath + "; the menu is drawn without it.");
                }
                return sprite;
            }
        }
    }

    /// <summary>
    /// Fades the halo in and out, once every PERIOD seconds, on real time so a paused game still pulses.
    /// While the mouse is over the button the halo brightens to full and stays there; when the mouse
    /// leaves, the pulse carries on from the halo's brightness at that moment.
    /// </summary>
    internal class GlowPulse : MonoBehaviour
    {
        public const float PERIOD = 2f;
        public const float MIN_ALPHA = 0.1f;
        public const float MAX_ALPHA = 1f;

        // Seconds the halo takes to brighten from its dimmest to full when the mouse moves onto the button.
        public const float FADE_IN = 0.2f;

        public Image Image;

        private bool hovered;
        private bool restart;
        private float start;
        private float alpha = MIN_ALPHA;

        public bool Hovered
        {
            get => hovered;
            set
            {
                restart |= hovered && !value;
                hovered = value;
            }
        }

        public static float AlphaAt(float time) =>
            MIN_ALPHA + (MAX_ALPHA - MIN_ALPHA) * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * time / PERIOD));

        /// <summary>The time in the dimming half of the pulse at which AlphaAt gives this alpha.</summary>
        public static float FadingTimeAt(float alpha)
        {
            float bright = Mathf.Clamp01((alpha - MIN_ALPHA) / (MAX_ALPHA - MIN_ALPHA));
            return PERIOD - Mathf.Acos(1f - 2f * bright) * PERIOD / (2f * Mathf.PI);
        }

        /// <summary>The halo's alpha one frame later: brightening toward full while hovered, else the pulse.</summary>
        public static float Step(float alpha, bool hovered, float pulseTime, float deltaTime) => hovered
            ? Mathf.MoveTowards(alpha, MAX_ALPHA, (MAX_ALPHA - MIN_ALPHA) * deltaTime / FADE_IN)
            : AlphaAt(pulseTime);

        private void Update()
        {
            if (!hovered && restart)
            {
                start = Time.unscaledTime - FadingTimeAt(alpha);
                restart = false;
            }

            alpha = Step(alpha, hovered, Time.unscaledTime - start, Time.unscaledDeltaTime);
            if (Image != null)
            {
                Color color = Image.color;
                color.a = alpha;
                Image.color = color;
            }
        }

        // A hidden button gets no mouse-leave, so it stops counting as hovered here.
        private void OnDisable() => Hovered = false;
    }

    /// <summary>
    /// Tells the halo when the mouse is over the button. It sits on the button, since the halo takes no mouse events.
    /// </summary>
    internal class GlowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GlowPulse Pulse;

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (!(Pulse is null))
            {
                Pulse.Hovered = hovered;
            }
        }
    }

    // The refreshes below run whether or not another mod skipped the game's method: the menu's label
    // shows whether IM-HI works, which doesn't depend on what those methods do. An error stays in the
    // postfix, so the game's method and other mods' patches on it still finish.

    // Keep the original early path for cases where Harmony mods load before this Start.
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), nameof(MainMenu_Buttons_Controller.Start))]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix(MainMenu_Buttons_Controller __instance)
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }

    // A language change (or any reload of the language table) loads this mod's JSON label again, so it's
    // put back after the game's load. Last, so it also comes after other mods' changes to the table.
    [HarmonyPatch(typeof(Language), nameof(Language._Load))]
    public class Language__Load
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }

    // On Steam, Mods.LoadMods() can finish after MainMenu_Buttons_Controller.Start().
    // IM-HI patches Harmony Checker during Mods.LoadMods(); StopSpinner is called later
    // by the same loading coroutine, making this a reliable post-load refresh point.
    [HarmonyPatch(typeof(Mods), nameof(Mods.StopSpinner))]
    public class Mods_StopSpinner
    {
        public static void Postfix()
        {
            try
            {
                HarmonyCheckerStatus.MarkInstalled();
            }
            catch (Exception e)
            {
                Debug.LogError("[Harmony Checker] Couldn't update the main menu: " + e);
            }
        }
    }
}
