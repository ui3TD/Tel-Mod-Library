using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HarmonyChecker
{
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
}
