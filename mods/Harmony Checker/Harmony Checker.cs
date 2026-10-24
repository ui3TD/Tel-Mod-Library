using HarmonyLib;
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
        public const string BUTTON_LABEL = "IMHI_INSTALLED";

        public static void MarkInstalled(MainMenu_Buttons_Controller controller = null)
        {
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

            // Only the game's label or one of ours is changed: another mod's label on the Mods button is kept
            Lang_Button modButton = modsTransform == null ? null : modsTransform.GetComponentInChildren<Lang_Button>();
            if (modButton != null && Array.IndexOf(LABELS_TO_REPLACE, modButton.Constant) >= 0)
            {
                modButton.Constant = outdated ? IMHIUpdate.MODS_LABEL : BUTTON_LABEL;

                // MainMenu_Buttons_Controller.Start may already have run by the time IM-HI
                // finishes loading Workshop Harmony DLLs. Updating Constant alone does not
                // redraw an already-started Lang_Button, so force the text refresh now.
                modButton.ResetText();
            }

            if (outdated)
            {
                IMHIUpdate.ShowButton(container, modsTransform, installed);
            }
        }

        /// <summary>The Mods button's label in the game, and the two this mod gives it.</summary>
        public static readonly string[] LABELS_TO_REPLACE = { GAME_LABEL, BUTTON_LABEL, IMHIUpdate.MODS_LABEL };

        public const string GAME_LABEL = "MM_MODS";
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

        public const string MODS_LABEL = "IMHI_OUT_OF_DATE";
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
                    Debug.LogError("[Harmony Checker] couldn't load " + RelativePath + "; the update button is drawn without it.");
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

    // The two refreshes below run whether or not another mod skipped the game's method: the menu's label
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
