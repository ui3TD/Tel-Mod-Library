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

            Transform modsTransform = controller.Main_Container.transform.Find("Mods");
            if (modsTransform == null)
            {
                return;
            }

            Lang_Button modButton = modsTransform.GetComponentInChildren<Lang_Button>();
            if (modButton == null)
            {
                return;
            }

            Version installed = IMHIUpdate.InstalledVersion();
            bool outdated = IMHIUpdate.IsOutdated(installed);
            modButton.Constant = outdated ? IMHIUpdate.MODS_LABEL : BUTTON_LABEL;

            // MainMenu_Buttons_Controller.Start may already have run by the time IM-HI
            // finishes loading Workshop Harmony DLLs. Updating Constant alone does not
            // redraw an already-started Lang_Button, so force the text refresh now.
            modButton.ResetText();

            if (outdated)
            {
                IMHIUpdate.ShowButton(modsTransform, installed);
            }
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
        /// Adds a yellow copy of the Mods button below it that opens the IM-HI releases page.
        /// The label is kept about as short as the Mods label, which fills the button.
        /// Runs on every menu refresh, so it adds the button only once.
        /// </summary>
        public static void ShowButton(Transform modsTransform, Version installed)
        {
            Transform container = modsTransform.parent;
            if (container == null || container.Find(BUTTON_NAME) != null)
            {
                return;
            }

            GameObject button = Object.Instantiate(modsTransform.gameObject, container);
            button.name = BUTTON_NAME;
            button.transform.SetSiblingIndex(modsTransform.GetSiblingIndex() + 1);

            Button click = button.GetComponentInChildren<Button>();
            Lang_Button label = button.GetComponentInChildren<Lang_Button>();
            if (click == null || label == null)
            {
                Object.Destroy(button);
                return;
            }

            // The copy has the Mods button's handlers. ButtonDefault adds its click sound
            // in Start, which runs after this, so it keeps the sound.
            click.onClick = new Button.ButtonClickedEvent();
            click.onClick.AddListener(() => OpenUrl(RELEASES_URL));

            string text = Language.Data.TryGetValue(BUTTON_LABEL, out string constant) ? constant : FALLBACK_LABEL;
            Language.Data[DRAWN_LABEL] = text.Replace("@1", installed.ToString()).Replace("@2", MIN_VERSION.ToString());
            label.Constant = DRAWN_LABEL;
            label.ResetText();

            click.colors = TintFromWhite(click.colors);

            // The button's image is purple, and tinting it only darkens it, so the copy draws a
            // gold version of it instead, with white text like the other menu buttons. Without the
            // picture it falls back to a plain gold background with the game's dark text colour for gold.
            Sprite gold = GoldSprite();
            Image buttonImage = button.GetComponent<Image>();
            Color textColor = gold is null ? (Color)mainScript.blackGold32 : Color.white;
            foreach (Image background in button.GetComponentsInChildren<Image>())
            {
                if (gold is null)
                {
                    background.sprite = null;
                    background.color = mainScript.gold32;
                }
                else if (ReferenceEquals(background, buttonImage))
                {
                    background.sprite = gold;
                    background.color = Color.white;
                }
            }
            foreach (TMP_Text tmp in button.GetComponentsInChildren<TMP_Text>())
            {
                tmp.color = textColor;
            }
            foreach (Text uiText in button.GetComponentsInChildren<Text>())
            {
                uiText.color = textColor;
            }

            if (!(gold is null))
            {
                AddGlow(button);
            }
        }

        /// <summary>
        /// The Mods button's colour tint is purple, which would darken the yellow. This tints
        /// from white instead, keeping each state's change from the normal colour. The game's
        /// ColorBlock has no setters for most colours, so its fields are set directly.
        /// </summary>
        public static ColorBlock TintFromWhite(ColorBlock colors)
        {
            object block = colors;
            FieldInfo normalField = AccessTools.Field(typeof(ColorBlock), "m_NormalColor");
            if (normalField == null)
            {
                return colors;
            }

            Color normal = (Color)normalField.GetValue(block);
            foreach (string name in TINT_FIELDS)
            {
                FieldInfo field = AccessTools.Field(typeof(ColorBlock), name);
                field?.SetValue(block, Relative((Color)field.GetValue(block), normal));
            }
            normalField.SetValue(block, Color.white);
            return (ColorBlock)block;
        }

        private static readonly string[] TINT_FIELDS = { "m_HighlightedColor", "m_PressedColor", "m_SelectedColor", "m_DisabledColor" };

        /// <summary>
        /// A state's tint divided by the normal tint, so it changes white as it changed normal.
        /// </summary>
        public static Color Relative(Color state, Color normal) => new Color(
            Ratio(state.r, normal.r), Ratio(state.g, normal.g), Ratio(state.b, normal.b), Ratio(state.a, normal.a));

        private static float Ratio(float state, float normal) => normal <= 0f ? 1f : Mathf.Clamp01(state / normal);
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

    // Keep the original early path for cases where Harmony mods load before this Start.
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), nameof(MainMenu_Buttons_Controller.Start))]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix(MainMenu_Buttons_Controller __instance)
        {
            HarmonyCheckerStatus.MarkInstalled(__instance);
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
            HarmonyCheckerStatus.MarkInstalled();
        }
    }
}
