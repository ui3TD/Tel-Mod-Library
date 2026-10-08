using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
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
            // plain gold background instead, with the game's dark text colour for gold.
            foreach (Image background in button.GetComponentsInChildren<Image>())
            {
                background.sprite = null;
                background.color = mainScript.gold32;
            }
            foreach (TMP_Text tmp in button.GetComponentsInChildren<TMP_Text>())
            {
                tmp.color = mainScript.blackGold32;
            }
            foreach (Text uiText in button.GetComponentsInChildren<Text>())
            {
                uiText.color = mainScript.blackGold32;
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

    // Keep the original early path for cases where Harmony mods load before this Start.
    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), "Start")]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix(ref MainMenu_Buttons_Controller __instance)
        {
            HarmonyCheckerStatus.MarkInstalled(__instance);
        }
    }

    // On Steam, Mods.LoadMods() can finish after MainMenu_Buttons_Controller.Start().
    // IM-HI patches Harmony Checker during Mods.LoadMods(); StopSpinner is called later
    // by the same loading coroutine, making this a reliable post-load refresh point.
    [HarmonyPatch(typeof(Mods), "StopSpinner")]
    public class Mods_StopSpinner
    {
        public static void Postfix()
        {
            HarmonyCheckerStatus.MarkInstalled();
        }
    }
}
