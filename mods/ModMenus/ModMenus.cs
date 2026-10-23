using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleJSON;
using System.IO;
using Michsky.UI.ModernUIPack;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.EventSystems;
using static ModMenus.ModMenusUtils;

namespace ModMenus
{
    /// <summary>
    /// Lazily installs ModMenus only when the player opens the in-game Settings tab.
    /// This intentionally does no work on the main menu or while a game is loading.
    /// </summary>
    [HarmonyPatch(typeof(Tabs_Manager), nameof(Tabs_Manager.OpenTab))]
    public class Tabs_Manager_OpenTab
    {
        public static void Postfix(Tabs_Manager._tab._type Type)
        {
            if (Type != Tabs_Manager._tab._type.settings || !ModMenusUtils.IsGameplayReady())
            {
                return;
            }

            // The Settings hierarchy is active by the time this postfix runs, so install
            // the button once without any background retry loop. The heavier mod-menu
            // popup is generated only if the player actually clicks Mod Settings.
            if (!ModMenusUtils.TryInstallSettingsButton())
            {
                Debug.LogWarning("[ModMenus] Could not install the Mod Settings button in the active in-game Settings tab.");
            }
        }
    }

    /// <summary>
    /// Every new game and every loaded scene starts with no settings (variables.Awake clears them).
    /// The defaults are saved straight away, so mods read the menu's default even if the player
    /// never opens Mod Settings.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Awake))]
    public class variables_Awake
    {
        public static void Postfix()
        {
            ModMenusUtils.SaveMissingDefaults();
        }
    }

    /// <summary>
    /// Loading a save replaces the settings with the saved ones, which may be from before a mod was
    /// installed or gained a setting.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.LoadFunction))]
    public class variables_LoadFunction
    {
        public static void Postfix()
        {
            ModMenusUtils.SaveMissingDefaults();
        }
    }

    /// <summary>
    /// Utility class containing methods for creating and managing mod menu elements.
    /// </summary>
    class ModMenusUtils
    {
        /// <summary>
        /// The popup type the mod menu is registered under. It's past the game's own popup types;
        /// other mods adding popups should use a different number.
        /// </summary>
        public const PopupManager._type MOD_MENU_POPUP = (PopupManager._type)999;

        public const string BUTTON_LABEL = "MODMENU__BUTTON_LABEL";
        public const string APPLY_LABEL = "APPLY";
        public const string CANCEL_LABEL = "CANCEL";
        public const string RESET_LABEL = "MODMENU__RESET";

        public const string NO_SETTINGS_LABEL = "MODMENU__NO_SETTINGS";

        // Each row is as tall as what it draws, so the gaps between rows are all MENUITEM_SPACING.
        // A blank text row is MENUITEM_HEIGHT; a text row is as tall as its lines.
        public const int MENUITEM_HEIGHT = 40;
        public const int MENUITEM_SPACING = 10;
        // A mod's title is the first row of its page, as tall as its lines
        public const float TITLE_HEIGHT = 26;
        public const float LABEL_HEIGHT = 20;
        public const float CHECKBOX_HEIGHT = 22;
        public const float SLIDER_HEIGHT = 32;
        public const float SLIDER_LENGTH = 400;
        // A dropdown's or text field's box, under its label
        public const float BOX_WIDTH = 300;
        public const float BOX_HEIGHT = 28;

        public const float TEXT_SIZE = 15;
        public const float TITLE_SIZE = 20;

        // The menu fits the popup, which covers the screen: the panel is as big as fits, up to the maximum,
        // with Apply and Cancel under it, and the two are centred together (see LayoutFor).
        public const float PANEL_MAX_WIDTH = 840;
        public const float PANEL_MAX_HEIGHT = 640;
        public const float POPUP_MARGIN_MIN = 8;
        public const float POPUP_MARGIN_MAX = 24;
        public const float BUTTON_GAP = 8;
        public const float BUTTON_WIDTH = 184;
        // Cancel, Reset and Apply sit in a row under the panel, this far apart
        public const float BUTTON_ROW_GAP = 24;

        // Inside the panel: the same margin on every side, and twice it between the tabs and the settings,
        // with a line in the middle. Each column's scroll bar sits inside it, on its right.
        public const float PANEL_MARGIN = 14;
        public const float SCROLLBAR_WIDTH = 6;
        public const float SCROLLBAR_GAP = 8;

        // The tab column on the left, one tab per mod. A long name wraps onto TAB_MAX_LINES, then ends in "…".
        public const float TAB_WIDTH = 200;
        public const float TAB_MIN_WIDTH = 140;
        public const int TAB_MAX_LINES = 2;
        public const float TAB_PADDING = 6;
        // Tabs touch, so a click anywhere down the column reaches one; their padding keeps the names apart
        public const float TAB_SPACING = 0;

        public const string JSON_FILE = "modmenu.json";
        public const string JSON_DIR = "Mod Menu";
        public const string JSON_FIELD_VARID = "varID";
        public const string JSON_FIELD_LABELID = "labelID";
        public const string JSON_FIELD_TYPE = "type";
        public const string JSON_FIELD_DEF = "defaultValue";
        public const string JSON_FIELD_MIN = "minValue";
        public const string JSON_FIELD_MAX = "maxValue";
        public const string JSON_FIELD_IGNORE = "ignore";
        public const string JSON_FIELD_LIST = "itemIDList";
        public const string JSON_FIELD_MAXLEN = "maxLength";
        public const string JSON_FIELD_PASSWORD = "password";
        public const string JSON_FIELD_STEP = "step";
        public const string JSON_FIELD_ENABLEDIF = "enabledIf";
        public const string JSON_FIELD_INPUTTYPE = "inputType";
        public const string JSON_FIELD_RESET = "reset";
        public const string JSON_INPUT_INTEGER = "integer";
        public const string JSON_INPUT_DECIMAL = "decimal";
        public const string JSON_TYPE_TEXT = "text";
        public const string JSON_TYPE_DROPDOWN = "dropdown";
        public const string JSON_TYPE_SLIDER = "slider";
        public const string JSON_TYPE_CHECKBOX = "checkbox";
        public const string JSON_TYPE_INPUT = "input";


        public const string BUTTON_OBJ_NAME = "ModMenuButton";
        public const string SETTINGS_BUTTON_OBJ_NAME = "Settings";
        public const string POPUP_OBJ_NAME = "ModMenu";
        public const string APPLYBUTTON_OBJ_NAME = "ModMenuApply";
        public const string CANCELBUTTON_OBJ_NAME = "ModMenuCancel";
        public const string RESETBUTTON_OBJ_NAME = "ModMenuReset";
        public const string BLUE_BUTTON_SPRITE = "blue";
        public const string MENU_TEXT_OBJ_NAME = "ModMenuText";
        public const string MENU_SLIDER_OBJ_NAME = "ModMenuSlider";
        public const string MENU_CHECKBOX_OBJ_NAME = "ModMenuCheckbox";
        public const string MENU_DROPDOWN_OBJ_NAME = "ModMenuDropdown";
        public const string MENU_INPUT_OBJ_NAME = "ModMenuInput";
        public const string MENUCONTENT_OBJ_NAME = "MenuContainer";
        public const string SCROLLHANDLE_OBJ_NAME = "VerticalHandle";
        public const string SCROLLBAR_OBJ_NAME = "VerticalScrollBar";
        public const string SCROLLRECT_OBJ_NAME = "ScrollContainer";
        public const string VIEWPORT_OBJ_NAME = "Viewport";
        public const string DROPDOWN_LAYER_OBJ_NAME = "DropdownLayer";
        public const string TABS_SCROLLRECT_OBJ_NAME = "ModMenuTabs";
        public const string TAB_CONTAINER_OBJ_NAME = "TabContainer";
        public const string TAB_OBJ_NAME = "ModMenuTab";
        public const string PAGE_OBJ_NAME = "ModMenuPage";
        public const string DIVIDER_OBJ_NAME = "ModMenuDivider";

        /// <summary>
        /// Returns true only after a gameplay scene is active and its game-only data exists.
        /// The title screen intentionally fails this check.
        /// </summary>
        public static bool IsGameplayReady()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            mainScript main = camera.GetComponent<mainScript>();
            if (main == null || main.Data == null || !main.IsGameScene)
            {
                return false;
            }

            // mainScript.IsMainMenu() uses this same component as the distinction between
            // the title screen and a loaded game. Checking it directly avoids calling the
            // static helper while scene objects are still being constructed.
            return main.Data.GetComponent<SpecialEvents_Manager>() != null;
        }

        /// <summary>
        /// Installs or repairs the Mod Settings button on the active in-game settings tab.
        /// </summary>
        public static bool TryInstallSettingsButton()
        {
            if (!IsGameplayReady())
            {
                return false;
            }

            mainScript main = Camera.main.GetComponent<mainScript>();

            Tabs_Manager tabsManager = main.Data.GetComponent<Tabs_Manager>();
            if (tabsManager == null)
            {
                return false;
            }

            Tabs_Manager._tab settingsTab = tabsManager.GetTab(Tabs_Manager._tab._type.settings);
            if (settingsTab == null || settingsTab.Tab == null || !settingsTab.Tab.activeInHierarchy)
            {
                return false;
            }

            Transform settingsContainer = FindSettingsContainer(settingsTab.Tab.transform);
            if (settingsContainer == null)
            {
                return false;
            }

            // Mod Settings has one ordering anchor only: the vanilla Settings button.
            // Do not derive its location from childCount and do not anchor it to
            // Save & Quit/Main Menu, so later mods remain free to occupy that region.
            Transform settingsButton = FindSettingsButton(settingsContainer);
            if (settingsButton == null || settingsButton.parent == null)
            {
                return false;
            }

            settingsContainer = settingsButton.parent;

            Transform existingButton = FindNamedChild(settingsTab.Tab.transform, BUTTON_OBJ_NAME);
            if (existingButton != null)
            {
                ConfigureModMenuButton(existingButton.gameObject);
                PositionModMenuButton(existingButton, settingsButton);
                return true;
            }

            // Clone the exact vanilla Settings button that also serves as our anchor.
            GameObject modMenuButton = CloneButton(
                settingsButton.gameObject,
                settingsContainer,
                BUTTON_OBJ_NAME,
                BUTTON_LABEL,
                false,
                false);

            if (modMenuButton == null)
            {
                return false;
            }

            ConfigureModMenuButton(modMenuButton);
            PositionModMenuButton(modMenuButton.transform, settingsButton);
            return true;
        }

        /// <summary>
        /// Finds the settings list container using strict path first, then robust fallback.
        /// </summary>
        private static Transform FindSettingsContainer(Transform settingsRoot)
        {
            if (settingsRoot == null)
            {
                return null;
            }

            Transform container = settingsRoot.Find("ScrollRect/Container");
            if (container != null)
            {
                return container;
            }

            Transform[] descendants = settingsRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!string.Equals(candidate.name, "Container", StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.GetComponent<VerticalLayoutGroup>() != null || candidate.GetComponent<GridLayoutGroup>() != null)
                {
                    return candidate;
                }
            }

            Button[] buttons = settingsRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null || candidate.gameObject == null || candidate.transform.parent == null)
                {
                    continue;
                }

                if (candidate.GetComponentInChildren<TextMeshProUGUI>(true) == null)
                {
                    continue;
                }

                Transform parent = candidate.transform.parent;
                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    return parent;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a named descendant in a hierarchy using depth-first traversal.
        /// </summary>
        private static Transform FindNamedChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null && string.Equals(candidate.name, childName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the vanilla Settings button by its stable GameObject name.
        /// This intentionally does not use Save & Quit/Main Menu or any modded button
        /// as a fallback ordering anchor.
        /// </summary>
        private static Transform FindSettingsButton(Transform settingsContainer)
        {
            if (settingsContainer == null)
            {
                return null;
            }

            Transform direct = settingsContainer.Find(SETTINGS_BUTTON_OBJ_NAME);
            if (direct != null && direct.GetComponent<Button>() != null)
            {
                return direct;
            }

            Button[] buttons = settingsContainer.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                if (string.Equals(candidate.gameObject.name, SETTINGS_BUTTON_OBJ_NAME, StringComparison.Ordinal))
                {
                    return candidate.transform;
                }
            }

            return null;
        }

        /// <summary>
        /// Repairs Mod Settings so it is immediately after the vanilla Settings button.
        /// Called both when creating the button and on every Settings-tab activation
        /// when an existing ModMenuButton is found.
        /// </summary>
        private static void PositionModMenuButton(Transform modMenuButton, Transform settingsButton)
        {
            if (modMenuButton == null || settingsButton == null || settingsButton.parent == null)
            {
                return;
            }

            Transform parent = settingsButton.parent;
            if (modMenuButton.parent != parent)
            {
                modMenuButton.SetParent(parent, false);
            }

            MoveImmediatelyAfter(modMenuButton, settingsButton);

            RectTransform settingsRect = parent as RectTransform;
            if (settingsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(settingsRect);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void MoveImmediatelyAfter(Transform item, Transform anchor)
        {
            if (item == null || anchor == null || item == anchor || anchor.parent == null)
            {
                return;
            }

            Transform parent = anchor.parent;
            if (item.parent != parent)
            {
                item.SetParent(parent, false);
            }

            item.SetSiblingIndex(SiblingIndexAfter(item.GetSiblingIndex(), anchor.GetSiblingIndex(), parent.childCount));
        }

        /// <summary>
        /// Returns the sibling index that places an item immediately after its anchor.
        /// </summary>
        public static int SiblingIndexAfter(int itemIndex, int anchorIndex, int childCount)
        {
            int targetIndex = anchorIndex + 1;

            // Removing an item that started before the anchor shifts the anchor left.
            if (itemIndex < targetIndex)
            {
                targetIndex--;
            }

            return Mathf.Clamp(targetIndex, 0, childCount - 1);
        }

        /// <summary>
        /// Configures localized text and click action for the Mod Settings button.
        /// </summary>
        private static void ConfigureModMenuButton(GameObject modMenuButton)
        {
            if (modMenuButton == null)
            {
                return;
            }

            Transform textTransform = modMenuButton.transform.Find("Text");
            if (textTransform != null)
            {
                Lang_Button languageBinding = textTransform.GetComponent<Lang_Button>();
                if (languageBinding != null)
                {
                    languageBinding.Constant = BUTTON_LABEL;
                }

                TextMeshProUGUI label = textTransform.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = Language.Data.TryGetValue(BUTTON_LABEL, out string localized) ? localized : BUTTON_LABEL;
                }
            }

            Button button = modMenuButton.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (!IsGameplayReady())
                {
                    return;
                }

                if (GenerateMenuPopup() != null)
                {
                    PopupManager.OpenPopup(MOD_MENU_POPUP);
                }
            });
        }

        /// <summary>
        /// Generates the main mod menu popup.
        /// </summary>
        /// <returns>The GameObject representing the mod menu popup, or null if it couldn't be built.</returns>
        public static GameObject GenerateMenuPopup()
        {
            PopupManager mainPopupManager = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>();
            PopupManager._popup existingPopup = mainPopupManager.GetByType(MOD_MENU_POPUP);
            if (existingPopup != null)
                return existingPopup.obj;

            // Adapt existing popup
            GameObject originalModMenuObj = mainPopupManager.GetByType(PopupManager._type.settings_difficulty).obj;
            GameObject modMenuObj = UnityEngine.Object.Instantiate(originalModMenuObj);
            modMenuObj.transform.SetParent(originalModMenuObj.transform.parent, false);
            modMenuObj.name = POPUP_OBJ_NAME;

            // The popup is only registered once it's fully built. If building fails (the game's own settings
            // popup has changed, say), the half-built copy is removed, so the next click tries again cleanly
            // instead of leaving another copy behind each time.
            try
            {
                BuildMenuPopup(modMenuObj);
            }
            catch (Exception e)
            {
                UnityEngine.Object.Destroy(modMenuObj);
                Debug.LogError("[ModMenus] Couldn't build the Mod Settings menu: " + e);
                return null;
            }

            // Incorporate popup into game
            PopupManager._popup newPopup = new()
            {
                type = MOD_MENU_POPUP,
                obj = modMenuObj,
                BGBlur = true,
                BGDarken = true
            };
            Array.Resize(ref mainPopupManager.popups, mainPopupManager.popups.Length + 1);
            mainPopupManager.popups[mainPopupManager.popups.Length - 1] = newPopup;

            return modMenuObj;
        }

        /// <summary>
        /// Turns a copy of the game's difficulty settings popup into the mod menu.
        /// </summary>
        private static void BuildMenuPopup(GameObject modMenuObj)
        {
            // Remove old child objects
            Transform panelTransform = modMenuObj.transform.Find("Panel");
            Transform panelSettingsContainerTransform = panelTransform.Find("Settings_Container");
            if (panelSettingsContainerTransform != null)
            {
                UnityEngine.Object.DestroyImmediate(modMenuObj.GetComponent<Settings_Difficulty>());
                UnityEngine.Object.DestroyImmediate(panelSettingsContainerTransform.gameObject);
            }

            // Replace buttons
            GameObject oldApplyButton = modMenuObj.transform.Find("Apply").gameObject;
            GameObject oldCancelButton = modMenuObj.transform.Find("Cancel").gameObject;
            GameObject applyButton = CloneButton(oldApplyButton, oldApplyButton.transform.parent, APPLYBUTTON_OBJ_NAME, APPLY_LABEL, new Color(0.2f, 0.7f, 0.2f), true);
            GameObject cancelButton = CloneButton(oldCancelButton, oldCancelButton.transform.parent, CANCELBUTTON_OBJ_NAME, CANCEL_LABEL, true);
            // The game's blue button, so it isn't taken for Cancel or Apply
            GameObject resetButton = CloneButton(applyButton, applyButton.transform.parent, RESETBUTTON_OBJ_NAME, RESET_LABEL);
            Image resetImage = resetButton.GetComponent<Image>();
            Sprite blueButton = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == BLUE_BUTTON_SPRITE && resetImage.sprite != null && s.rect.size == resetImage.sprite.rect.size);
            if (blueButton != null)
            {
                resetImage.sprite = blueButton;
            }

            // Attach custom component to Panel
            ModMenuManager panelModMenuManager = panelTransform.gameObject.AddComponent<ModMenuManager>();
            applyButton.GetComponent<Button>().onClick.AddListener(panelModMenuManager.OnApply);
            cancelButton.GetComponent<Button>().onClick.AddListener(panelModMenuManager.OnCancel);
            resetButton.GetComponent<Button>().onClick.AddListener(panelModMenuManager.OnReset);

            // The settings first, so they're still the panel's first scroll area; then the tabs on the left, and
            // a line between them. Each page of settings is a child of the settings' container.
            GameObject menuContentContainer = GenerateScrollArea(panelTransform, SCROLLRECT_OBJ_NAME, MENUCONTENT_OBJ_NAME, 0, true, out ScrollRect settingsScroll);
            GameObject tabContainer = GenerateScrollArea(panelTransform, TABS_SCROLLRECT_OBJ_NAME, TAB_CONTAINER_OBJ_NAME, TAB_SPACING, false, out ScrollRect tabScroll);
            GameObject divider = AddDivider(panelTransform);
            panelModMenuManager.scroll = settingsScroll;
            panelModMenuManager.tabScroll = tabScroll;

            // An open list stays where it is while the rows scroll under it, so scrolling closes it first
            settingsScroll.onValueChanged.AddListener(_ => panelModMenuManager.CloseDropdownLists());

            // An open dropdown's list moves here, the popup's last child, so it shows above every row and the
            // buttons and isn't cut off by the scroll area. It moves back into its row when it closes.
            GameObject dropdownLayer = new(DROPDOWN_LAYER_OBJ_NAME, typeof(RectTransform));
            dropdownLayer.transform.SetParent(modMenuObj.transform, false);
            SetRectTransform(dropdownLayer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dropdownLayer.transform.SetAsLastSibling();

            // The panel and buttons fit the screen whenever the menu opens or the screen changes size
            ModMenuLayout layout = modMenuObj.AddComponent<ModMenuLayout>();
            layout.panel = (RectTransform)panelTransform;
            layout.apply = (RectTransform)applyButton.transform;
            layout.cancel = (RectTransform)cancelButton.transform;
            layout.reset = (RectTransform)resetButton.transform;
            layout.buttonHeight = layout.apply.rect.height;
            layout.tabColumn = (RectTransform)tabScroll.transform;
            layout.settingsArea = (RectTransform)settingsScroll.transform;
            layout.divider = (RectTransform)divider.transform;
            layout.manager = panelModMenuManager;

            // Generate Menu Contents
            AddMenuItems(panelModMenuManager, menuContentContainer.transform, tabContainer.transform, dropdownLayer.transform);

            // With no mod's settings shown, there's nothing to reset
            resetButton.GetComponent<Button>().interactable = panelModMenuManager.selected != null;
        }

        /// <summary>
        /// The line between the tabs and the settings: the menu's blue, faded. ModMenuLayout places it.
        /// </summary>
        private static GameObject AddDivider(Transform panel)
        {
            GameObject divider = new(DIVIDER_OBJ_NAME, typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(panel, false);
            Image image = divider.GetComponent<Image>();
            Color32 blue = mainScript.blue32;
            image.color = new Color32(blue.r, blue.g, blue.b, 77);
            image.raycastTarget = false;
            return divider;
        }

        /// <summary>
        /// Reads a slider's range and default from its JSON item. The range is 0-100 unless
        /// both minValue and maxValue are set; the default is the midpoint unless defaultValue is set.
        /// </summary>
        public static void GetSliderRange(JSONNode item, out float minValue, out float maxValue, out float defaultValue)
        {
            minValue = 0;
            maxValue = 100;
            if (!string.IsNullOrEmpty(item[JSON_FIELD_MIN]) && !string.IsNullOrEmpty(item[JSON_FIELD_MAX]))
            {
                minValue = item[JSON_FIELD_MIN].AsFloat;
                maxValue = item[JSON_FIELD_MAX].AsFloat;
            }
            defaultValue = (maxValue + minValue) / 2f;
            if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
            {
                defaultValue = item[JSON_FIELD_DEF].AsFloat;
            }
        }

        /// <summary>
        /// Reads a slider's step from its JSON item: 1 unless step is set. A step that isn't a whole number of
        /// at least 1 is 1.
        /// </summary>
        /// <returns>False if step is set but isn't a whole number of at least 1.</returns>
        public static bool TryGetSliderStep(JSONNode item, out int step)
        {
            step = 1;
            if (string.IsNullOrEmpty(item[JSON_FIELD_STEP]))
            {
                return true;
            }
            float written = item[JSON_FIELD_STEP].AsFloat;
            if (written < 1 || written != Mathf.Floor(written))
            {
                return false;
            }
            step = (int)written;
            return true;
        }

        /// <summary>
        /// The value a slider with a step saves: the nearest of min, min + step, min + 2 × step and so on up to
        /// max, or max itself, so max can be picked even when the range isn't a whole number of steps. Halfway
        /// goes up. With step 1 it's the whole number nearest the value, as sliders have always saved.
        /// </summary>
        public static float SnapToStep(float value, float min, float max, int step)
        {
            float clamped = Mathf.Clamp(value, min, max);
            if (step <= 1)
            {
                return Mathf.Round(clamped);
            }
            float below = min + Mathf.Floor((clamped - min) / step) * step;
            float above = Mathf.Min(below + step, max);
            return Mathf.Round(clamped - below < above - clamped ? below : above);
        }

        /// <summary>
        /// Reads a text field's default from its JSON item: defaultValue as written, or empty if it isn't set.
        /// </summary>
        public static string GetInputDefault(JSONNode item)
        {
            return string.IsNullOrEmpty(item[JSON_FIELD_DEF]) ? "" : (string)item[JSON_FIELD_DEF];
        }

        /// <summary>
        /// Reads what a text field takes from its JSON item: any text, or, with inputType "integer" or
        /// "decimal", a number, between minValue and maxValue where they're set. Each limit works on its own.
        /// </summary>
        /// <param name="inputType">"integer" or "decimal"; null for any text.</param>
        /// <returns>Whether the field takes a number.</returns>
        public static bool GetNumberField(JSONNode item, out string inputType, out float? min, out float? max)
        {
            string type = item[JSON_FIELD_INPUTTYPE];
            inputType = type == JSON_INPUT_INTEGER || type == JSON_INPUT_DECIMAL ? type : null;
            min = string.IsNullOrEmpty(item[JSON_FIELD_MIN]) ? null : item[JSON_FIELD_MIN].AsFloat;
            max = string.IsNullOrEmpty(item[JSON_FIELD_MAX]) ? null : item[JSON_FIELD_MAX].AsFloat;
            return inputType != null;
        }

        /// <summary>
        /// What a number field keeps of what was typed: the number, rounded to a whole number in an integer
        /// field, and moved inside its limits; written as settings are saved ("7", "-2", "0.5"). A comma counts
        /// as the decimal point. Text that isn't a number gives the fallback.
        /// </summary>
        public static string ValidNumber(string typed, bool integer, float? min, float? max, string fallback)
        {
            string text = (typed ?? "").Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            double low = min ?? double.MinValue;
            double high = max ?? double.MaxValue;
            if (integer)
            {
                // The limits move in to whole numbers, so the result is both whole and inside them
                value = Math.Round(value);
                low = Math.Ceiling(low);
                high = Math.Floor(high);
            }
            if (value < low)
            {
                value = low;
            }
            else if (value > high)
            {
                value = high;
            }
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A number field's default: its defaultValue, checked as typing is; or, if that isn't a number,
        /// 0 moved inside its limits.
        /// </summary>
        public static string NumberDefault(string def, bool integer, float? min, float? max)
        {
            return ValidNumber(def, integer, min, max, ValidNumber("0", integer, min, max, "0"));
        }

        /// <summary>
        /// Whether a setting is on, for enabledIf: its value isn't missing, empty or a number equal to 0. So a
        /// ticked checkbox, a slider above 0, a dropdown on any item but the first, or a text field with text.
        /// </summary>
        public static bool IsSettingOn(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            return !TryParseSetting(value, out float number) || number != 0;
        }

        /// <summary>
        /// Whether a row with enabledIf: varID is shown: the setting is on, and its own row, if it has an
        /// enabledIf too, is shown. Settings that lead back to one already being checked form a loop, and the
        /// loop's link counts as met.
        /// </summary>
        /// <param name="valueOf">A setting's value: its row's, if the menu has one, or the saved value.</param>
        /// <param name="conditionOf">The enabledIf of a setting's row, or null if it has none.</param>
        /// <param name="checking">The settings already being checked, which this adds to and takes from.</param>
        /// <param name="loop">Set to true if a loop was found.</param>
        public static bool ConditionMet(string varID, Func<string, string> valueOf, Func<string, string> conditionOf, HashSet<string> checking, ref bool loop)
        {
            if (!checking.Add(varID))
            {
                loop = true;
                return true;
            }
            try
            {
                string parent = conditionOf(varID);
                if (!string.IsNullOrEmpty(parent) && !ConditionMet(parent, valueOf, conditionOf, checking, ref loop))
                {
                    return false;
                }
                return IsSettingOn(valueOf(varID));
            }
            finally
            {
                checking.Remove(varID);
            }
        }

        /// <summary>
        /// What the open mod menu shows for a variable, applied or not, on any tab: the text Apply would save,
        /// so a checkbox's 1 or 0 and a dropdown's item counted from 0. Null while the menu isn't open, or if no
        /// row has the variable. A mod can read the player's choices before Apply with it, for example to work
        /// out a variable that a row's enabledIf names.
        /// </summary>
        public static string MenuValue(string varID)
        {
            ModMenuManager menu = ModMenuManager.open;
            if (menu == null || varID == null)
            {
                return null;
            }
            foreach (ModMenuItem item in menu.GetComponentsInChildren<ModMenuItem>(true))
            {
                if (item.varID == varID)
                {
                    return item.CurrentValue;
                }
            }
            return null;
        }

        /// <summary>
        /// A mod with a menu: its settings, and the name its tab shows.
        /// </summary>
        public class MenuSource
        {
            public Mods._mod Mod;
            public JSONArray Items;
            public string Title;
        }

        /// <summary>
        /// The enabled mods with a menu file that can be read, in tab order: by the name each tab shows,
        /// ignoring case. Mods with the same name keep their load order.
        /// </summary>
        public static List<MenuSource> FindMenus()
        {
            string relativePath = Path.Combine("JSON", JSON_DIR, JSON_FILE);
            List<MenuSource> menus = new();
            foreach (Mods._mod mod in Mods._Mods)
            {
                if (mod == null || !mod.IsEnabled())
                    continue;

                string filepath = Path.Combine(mod.Path, relativePath).Replace("\\", "/");
                if (!File.Exists(filepath))
                    continue;

                JSONArray items = ReadMenuFile(mod.Title, filepath);
                if (items == null)
                    continue;

                menus.Add(new MenuSource { Mod = mod, Items = items, Title = ShownText(mod.Title).Trim() });
            }

            // OrderBy keeps equal names in load order. Ordinal, so it sorts the same in every language.
            return menus.OrderBy(menu => menu.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// A text ID's text in the player's language, or the ID as written if it isn't a constant.
        /// </summary>
        public static string ShownText(string textID)
        {
            return textID != null && Language.Data.TryGetValue(textID, out string text) ? text : textID ?? "";
        }

        /// <summary>
        /// Builds the menu: a page of settings and a tab for each mod with a menu, then shows the tab the
        /// player last used, or the first one.
        /// </summary>
        /// <param name="manager">The menu's manager, which switches pages when a tab is clicked.</param>
        /// <param name="pageParent">Where the pages go: the settings' scroll area.</param>
        /// <param name="tabParent">Where the tabs go: the tab column's scroll area.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes, above every row (see AddMenuDropdown).</param>
        public static void AddMenuItems(ModMenuManager manager, Transform pageParent, Transform tabParent, Transform dropdownLayer)
        {
            manager.tabs = new List<ModMenuTab>();
            manager.selected = null;

            List<MenuSource> menus = FindMenus();
            if (menus.Count == 0)
            {
                GameObject message = AddMenuText(NO_SETTINGS_LABEL, pageParent, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Center);
                message.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                return;
            }

            List<string> modNames = new();
            foreach (MenuSource menu in menus)
            {
                GameObject page = AddMenuPage(menu, pageParent, dropdownLayer);
                manager.tabs.Add(AddMenuTab(menu, page, manager, tabParent));
                modNames.Add(menu.Mod.ModName);
            }

            // A changed setting may show or hide rows with enabledIf
            foreach (ModMenuItem item in pageParent.GetComponentsInChildren<ModMenuItem>(true))
            {
                item.manager = manager;
            }

            // Every page is built shown, as the menu's rows always were; only now are the others hidden
            manager.ShowPage(manager.tabs[RememberedTab(modNames, ModMenuManager.lastModName)]);
        }

        /// <summary>
        /// The tab to show: the one the player last used, or the first if that mod has no tab now.
        /// </summary>
        public static int RememberedTab(IList<string> modNames, string remembered)
        {
            for (int i = 0; i < modNames.Count; i++)
            {
                if (modNames[i] == remembered)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Adds a mod's page: its name, then its rows in file order.
        /// </summary>
        /// <param name="menu">The mod and its settings.</param>
        /// <param name="pageParent">Where the page goes: the settings' scroll area.</param>
        /// <param name="dropdownLayer">Where an open dropdown's list goes, above every row (see AddMenuDropdown).</param>
        /// <returns>The page.</returns>
        public static GameObject AddMenuPage(MenuSource menu, Transform pageParent, Transform dropdownLayer)
        {
            GameObject page = new(PAGE_OBJ_NAME + "_" + menu.Mod.ModName, typeof(RectTransform), typeof(VerticalLayoutGroup));
            page.transform.SetParent(pageParent, false);
            SetColumnLayout(page.GetComponent<VerticalLayoutGroup>(), MENUITEM_SPACING, false);
            Transform parentTransform = page.transform;

            // The whole name, however long: a tab may cut it short
            GameObject titleRow = AddMenuText(menu.Mod.Title, parentTransform, TITLE_SIZE, mainScript.black32, TextAlignmentOptions.Bottom);
            ((RectTransform)titleRow.transform).sizeDelta = new Vector2(200, TITLE_HEIGHT);
            titleRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            JSONArray jsonArray = menu.Items;
            for (int i = 0; i < jsonArray.Count; i++)
            {
                JSONNode item = jsonArray[i];
                string type = item[JSON_FIELD_TYPE];
                string label = item[JSON_FIELD_LABELID];

                // Text without a label is a blank row, the documented way to add spacing.
                // (The game's JSON parser drops empty strings, so an empty labelID reads as missing.)
                if (string.IsNullOrEmpty(type) || (string.IsNullOrEmpty(label) && type != JSON_TYPE_TEXT) || item[JSON_FIELD_IGNORE].AsBool) continue;
                string id;
                GameObject row = null;

                switch (type)
                {
                    case JSON_TYPE_TEXT:
                        row = AddMenuText(label ?? "", parentTransform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);

                        // As tall as its lines, so long text doesn't run into the next row. A blank one keeps its height: it's a spacer.
                        if (!string.IsNullOrEmpty(label))
                            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        break;

                    case JSON_TYPE_SLIDER:
                        if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                            continue;

                        id = item[JSON_FIELD_VARID];
                        GetSliderRange(item, out float minValue, out float maxValue, out float defaultFloat);

                        row = AddMenuSlider(id, label, minValue, maxValue, defaultFloat, parentTransform);
                        break;

                    case JSON_TYPE_CHECKBOX:
                        if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                            continue;

                        id = item[JSON_FIELD_VARID];
                        bool defaultBool = false;
                        if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
                        {
                            defaultBool = item[JSON_FIELD_DEF].AsBool;
                        }

                        row = AddMenuCheckbox(id, label, defaultBool, parentTransform);
                        break;

                    case JSON_TYPE_DROPDOWN:
                        if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]) || string.IsNullOrEmpty(item[JSON_FIELD_LIST].ToString()) || item[JSON_FIELD_LIST].AsArray == null)
                            continue;

                        id = item[JSON_FIELD_VARID];
                        int defaultSelect = 0;
                        if (!string.IsNullOrEmpty(item[JSON_FIELD_DEF]))
                        {
                            defaultSelect = item[JSON_FIELD_DEF].AsInt;
                        }

                        JSONArray jsonArrayItems = item[JSON_FIELD_LIST].AsArray;
                        string[] itemList = new string[jsonArrayItems.Count];
                        for (int j = 0; j < jsonArrayItems.Count; j++)
                        {
                            itemList[j] = jsonArrayItems[j];
                        }

                        row = AddMenuDropdown(id, label, itemList, defaultSelect, parentTransform, dropdownLayer);
                        break;

                    case JSON_TYPE_INPUT:
                        if (string.IsNullOrEmpty(item[JSON_FIELD_VARID]))
                            continue;

                        id = item[JSON_FIELD_VARID];
                        row = AddMenuInput(id, label, GetInputDefault(item), item[JSON_FIELD_MAXLEN].AsInt, item[JSON_FIELD_PASSWORD].AsBool, parentTransform);
                        break;
                }

                if (row != null)
                {
                    AddRowOptions(item, row);
                }
            }

            return page;
        }

        /// <summary>
        /// Gives a row the options any item can have, which the row builders don't take: a slider's step, a
        /// number field's limits, "reset": false, and enabledIf.
        /// </summary>
        public static void AddRowOptions(JSONNode item, GameObject row)
        {
            ModMenuItem menuItem = row.GetComponent<ModMenuItem>();
            if (menuItem != null)
            {
                // Only an explicit false: a missing field reads as false too
                menuItem.noReset = !string.IsNullOrEmpty(item[JSON_FIELD_RESET]) && !item[JSON_FIELD_RESET].AsBool;

                if (menuItem.settingsSlider != null)
                {
                    if (!TryGetSliderStep(item, out menuItem.step))
                    {
                        Debug.LogWarning("[ModMenus] The slider " + menuItem.varID + "'s step isn't a whole number of at least 1, so it moves in steps of 1.");
                    }
                }

                if (menuItem.inputField != null && GetNumberField(item, out string inputType, out float? min, out float? max))
                {
                    MakeNumberField(menuItem, inputType, min, max);
                    if (item[JSON_FIELD_PASSWORD].AsBool)
                    {
                        Debug.LogWarning("[ModMenus] The text field " + menuItem.varID + " takes a number, so its password option is ignored.");
                    }
                }
            }

            string condition = item[JSON_FIELD_ENABLEDIF];
            if (!string.IsNullOrEmpty(condition))
            {
                row.AddComponent<ModMenuCondition>().varID = condition;
            }
        }

        /// <summary>
        /// Makes a text field take only a number: the game's field lets only a number be typed, and leaving the
        /// field moves it inside its limits (see ValidNumber). This replaces the password option.
        /// </summary>
        public static void MakeNumberField(ModMenuItem menuItem, string inputType, float? min, float? max)
        {
            menuItem.inputType = inputType;
            menuItem.inputMin = min;
            menuItem.inputMax = max;

            TMP_InputField field = menuItem.inputField;
            field.contentType = inputType == JSON_INPUT_INTEGER ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
            field.onEndEdit = new TMP_InputField.SubmitEvent();
            field.onEndEdit.AddListener(menuItem.onEndInput);
        }

        /// <summary>
        /// Adds a mod's tab: its name, which wraps onto TAB_MAX_LINES and then ends in "…". Clicking it shows its page.
        /// </summary>
        /// <param name="menu">The mod.</param>
        /// <param name="page">The mod's page, which the tab shows.</param>
        /// <param name="manager">The menu's manager, which switches pages.</param>
        /// <param name="tabParent">Where the tab goes: the tab column's scroll area.</param>
        /// <returns>The tab.</returns>
        public static ModMenuTab AddMenuTab(MenuSource menu, GameObject page, ModMenuManager manager, Transform tabParent)
        {
            GameObject tabObj = new(TAB_OBJ_NAME + "_" + menu.Mod.ModName, typeof(RectTransform), typeof(Image), typeof(Button));
            tabObj.transform.SetParent(tabParent, false);
            ((RectTransform)tabObj.transform).sizeDelta = new Vector2(TAB_WIDTH, LABEL_HEIGHT + 2 * TAB_PADDING);

            // The name sits in the tab's padding and grows downwards as it wraps, making the tab taller
            GameObject labelObj = AddMenuText(menu.Mod.Title, tabObj.transform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);
            SetRectTransform((RectTransform)labelObj.transform, Vector2.up, Vector2.one, new Vector2(TAB_PADDING, -TAB_PADDING - LABEL_HEIGHT), new Vector2(-TAB_PADDING, -TAB_PADDING), new Vector2(0.5f, 1));
            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            AddLabelWrap(tabObj, label, null);
            tabObj.GetComponent<LabelWrap>().maxLines = TAB_MAX_LINES;

            ModMenuTab tab = tabObj.AddComponent<ModMenuTab>();
            tab.modName = menu.Mod.ModName;
            tab.page = page;
            tab.manager = manager;
            tab.image = tabObj.GetComponent<Image>();
            tab.label = label;
            tab.button = tabObj.GetComponent<Button>();
            SetTabLooks(tab.button, label);

            tab.button.onClick = new Button.ButtonClickedEvent();
            tab.button.onClick.AddListener(tab.OnClick);
            return tab;
        }

        /// <summary>
        /// A tab takes clicks on its whole area, not on its name. It draws its own fill (see ModMenuTab.Render),
        /// so the button doesn't tint it.
        /// </summary>
        private static void SetTabLooks(Button button, TextMeshProUGUI label)
        {
            label.raycastTarget = false;
            button.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// Lays out a column: each child is the full width; its height is its own, or, if the column controls
        /// it, its preferred height.
        /// </summary>
        private static void SetColumnLayout(VerticalLayoutGroup column, float spacing, bool controlChildHeight)
        {
            column.childControlWidth = true;
            column.childForceExpandWidth = true;
            column.childControlHeight = controlChildHeight;
            column.childForceExpandHeight = false;
            column.spacing = spacing;
        }

        /// <summary>
        /// Saves the default of every setting in the enabled mods' menus that has no saved value yet.
        /// Saved values are never changed.
        /// </summary>
        public static void SaveMissingDefaults()
        {
            // This runs while a game is starting or loading, so nothing may escape into the game's own code
            try
            {
                foreach (MenuSource menu in FindMenus())
                {
                    JSONArray settings = menu.Items;
                    for (int i = 0; i < settings.Count; i++)
                    {
                        if (!TryGetDefault(settings[i], out string varID, out string value) || variables.Get(varID) != null)
                            continue;

                        // Not variables.Set: it also logs every setting and raises the game's task events,
                        // which may not be ready while the scene is starting.
                        variables.variable.Add(new variables._variable { name = varID, value = value });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ModMenus] Couldn't save the settings' defaults: " + e);
            }
        }

        /// <summary>
        /// The value a setting starts at, written as Apply would save it. Only settings the menu shows have one:
        /// text, ignored items, items missing their varID or labelID, sliders whose default is outside their
        /// range and dropdowns without items are left out, as they are from the menu. A slider's default moves
        /// to its nearest step, and a number field's is checked as typing is.
        /// </summary>
        public static bool TryGetDefault(JSONNode item, out string varID, out string value)
        {
            varID = item?[JSON_FIELD_VARID];
            value = null;
            if (item == null
                || string.IsNullOrEmpty(varID)
                || string.IsNullOrEmpty(item[JSON_FIELD_LABELID])
                || item[JSON_FIELD_IGNORE].AsBool)
            {
                return false;
            }

            bool hasDefault = !string.IsNullOrEmpty(item[JSON_FIELD_DEF]);
            switch ((string)item[JSON_FIELD_TYPE])
            {
                case JSON_TYPE_SLIDER:
                    GetSliderRange(item, out float min, out float max, out float def);
                    if (max < min || def < min || def > max)
                        return false;
                    TryGetSliderStep(item, out int step);
                    value = FormatSetting(SnapToStep(def, min, max, step));
                    return true;

                case JSON_TYPE_CHECKBOX:
                    value = hasDefault && item[JSON_FIELD_DEF].AsBool ? "1" : "0";
                    return true;

                case JSON_TYPE_DROPDOWN:
                    JSONArray items = item[JSON_FIELD_LIST].AsArray;
                    if (items == null || items.Count == 0)
                        return false;
                    int choice = hasDefault ? item[JSON_FIELD_DEF].AsInt : 0;
                    value = FormatSetting(Mathf.Clamp(choice, 0, items.Count - 1));
                    return true;

                case JSON_TYPE_INPUT:
                    value = GetNumberField(item, out string inputType, out float? low, out float? high)
                        ? NumberDefault(GetInputDefault(item), inputType == JSON_INPUT_INTEGER, low, high)
                        : GetInputDefault(item);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads a saved setting. Settings are saved the same way whatever the player's language, so a
        /// comma-decimal language doesn't misread them.
        /// </summary>
        public static bool TryParseSetting(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Writes a setting as plain digits ("7", "-2", "10000000"), never as "1E+07" or with a comma.
        /// </summary>
        public static string FormatSetting(float value)
        {
            return ((double)value).ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a mod's modmenu.json. A file that can't be read or isn't a list of settings is left out
        /// with a warning, so one mod's broken file doesn't break the menu for every mod.
        /// </summary>
        /// <returns>The file's settings, or null if it's left out.</returns>
        public static JSONArray ReadMenuFile(string modTitle, string filepath)
        {
            JSONNode root;
            try
            {
                root = JSON.Parse(File.ReadAllText(filepath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ModMenus] " + modTitle + "'s " + JSON_FILE + " couldn't be read, so its settings are left out: " + e.Message);
                return null;
            }

            JSONArray settings = root?.AsArray;
            if (settings == null)
            {
                Debug.LogWarning("[ModMenus] " + modTitle + "'s " + JSON_FILE + " isn't a list of settings ([ ... ]), so its settings are left out.");
            }
            return settings;
        }

        /// <summary>
        /// Adds a text element to the mod menu.
        /// </summary>
        /// <param name="textID">The ID of the text to display.</param>
        /// <param name="parentTransform">The parent transform to add the text to.</param>
        /// <param name="fontSize">The font size of the text.</param>
        /// <param name="col">The color of the text.</param>
        /// <param name="alignment">The alignment of the text.</param>
        /// <returns>The GameObject representing the text element.</returns>
        public static GameObject AddMenuText(string textID, Transform parentTransform, float fontSize, Color col, TextAlignmentOptions alignment)
        {
            string text = Language.Data.TryGetValue(textID, out string tx) ? tx : textID;

            // Create contents to fill container
            GameObject menuTextObj = new(MENU_TEXT_OBJ_NAME + "_" + textID, typeof(RectTransform), typeof(TextMeshProUGUI));
            menuTextObj.transform.SetParent(parentTransform, false);


            // Set text object alignment
            RectTransform contentRectTransform = menuTextObj.GetComponent<RectTransform>();
            SetRectTransform(contentRectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            contentRectTransform.sizeDelta = new Vector2(200, MENUITEM_HEIGHT);

            // Format Text
            TextMeshProUGUI tmpText = menuTextObj.GetComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = fontSize;
            tmpText.color = col;
            tmpText.alignment = alignment;

            return menuTextObj;
        }

        /// <summary>
        /// Adds a slider element to the mod menu.
        /// </summary>
        /// <param name="varID">The variable ID associated with the slider.</param>
        /// <param name="labelID">The label ID for the slider.</param>
        /// <param name="min">The minimum value of the slider.</param>
        /// <param name="max">The maximum value of the slider.</param>
        /// <param name="def">The default value of the slider.</param>
        /// <param name="parentTransform">The parent transform to add the slider to.</param>
        /// <returns>The GameObject representing the slider element.</returns>
        public static GameObject AddMenuSlider(string varID, string labelID, float min, float max, float def, Transform parentTransform)
        {
            string labelText = Language.Data.TryGetValue(labelID, out string tx) ? tx : labelID;

            if (max < min || def < min || def > max)
            {
                Debug.LogWarning("[ModMenus] The slider " + varID + " is left out: its range is " + min + " to " + max + " and its default is " + def + ".");
                return null;
            }

            // Define default value
            float savedValue = TryParseSetting(variables.Get(varID), out float savedFloat) ? savedFloat : def;

            // Set up main slider object
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Find("Tabs").Find("TabButton (3)").GetComponent<TabButton>().TabToOpen;
            GameObject existingSlider = existingUIObj.transform.Find("Autosave").gameObject;
            GameObject modSlider = UnityEngine.Object.Instantiate(existingSlider, parentTransform);
            RectTransform modSliderRect = modSlider.GetComponent<RectTransform>();
            SetRectTransform(modSliderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modSliderRect.sizeDelta = new Vector2(400f, SLIDER_HEIGHT);
            modSlider.name = MENU_SLIDER_OBJ_NAME + "_" + varID;

            // Configure Settings_Slider
            Settings_Slider modSliderSettingsSlider = modSlider.GetComponent<Settings_Slider>();
            modSliderSettingsSlider.Min_Value = min;
            modSliderSettingsSlider.Max_Value = max;
            modSliderSettingsSlider.Title_Text = labelID;

            // Configure ModMenuItem
            ModMenuItem modSliderMenuItem = modSlider.AddComponent<ModMenuItem>();
            modSliderMenuItem.varID = varID;
            modSliderMenuItem.defValue = def;
            modSliderMenuItem.tempValue = savedValue;

            // Configure Slider events
            Slider modSliderSlider = modSlider.GetComponentInChildren<Slider>();
            modSliderSlider.onValueChanged = new Slider.SliderEvent();
            modSliderSlider.onValueChanged.AddListener(modSliderMenuItem.onUpdateSlider);
            modSliderMenuItem.settingsSlider = modSliderSettingsSlider;
            modSliderMenuItem.slider = modSliderSlider;
            modSliderMenuItem.sliderText = modSliderSettingsSlider.Title.GetComponent<TextMeshProUGUI>();

            // Set slider size and pos. The game's slider is turned on its side, so its height runs across the row;
            // it starts at the row's left edge, under the label, like the other controls.
            RectTransform SliderRect = modSlider.transform.Find("Slider").GetComponent<RectTransform>();
            SetRectTransform(SliderRect, Vector2.up, Vector2.up, Vector2.zero, Vector2.zero);
            SliderRect.sizeDelta = new Vector2(20f, SLIDER_LENGTH);
            SliderRect.anchoredPosition = new Vector2(SLIDER_LENGTH / 2, -25f);

            AddLabelWrap(modSlider, modSliderSettingsSlider.Title.GetComponent<TextMeshProUGUI>(), null, SliderRect);

            return modSlider;
        }

        /// <summary>
        /// Adds a checkbox element to the mod menu.
        /// </summary>
        /// <param name="varID">The variable ID associated with the checkbox.</param>
        /// <param name="labelID">The label ID for the checkbox.</param>
        /// <param name="def">The default state of the checkbox.</param>
        /// <param name="parentTransform">The parent transform to add the checkbox to.</param>
        /// <returns>The GameObject representing the checkbox element.</returns>
        public static GameObject AddMenuCheckbox(string varID, string labelID, bool def, Transform parentTransform)
        {
            string labelText = Language.Data.TryGetValue(labelID, out string tx) ? tx : labelID;

            // Define default value
            bool savedValue = TryParseSetting(variables.Get(varID), out float savedFloat) ? savedFloat != 0 : def;

            // Set up main object
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Find("Tabs").Find("TabButton (3)").GetComponent<TabButton>().TabToOpen;
            GameObject existingCheckbox = existingUIObj.transform.Find("Run In Background").gameObject;
            GameObject modCheckbox = UnityEngine.Object.Instantiate(existingCheckbox, parentTransform);
            RectTransform modCheckboxRect = modCheckbox.GetComponent<RectTransform>();
            SetRectTransform(modCheckboxRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modCheckboxRect.sizeDelta = new Vector2(400f, CHECKBOX_HEIGHT);
            modCheckbox.name = MENU_CHECKBOX_OBJ_NAME + "_" + varID;

            // Configure Checkbox_Text
            Checkbox_Text modCheckboxCheck = modCheckbox.GetComponent<Checkbox_Text>();
            modCheckboxCheck.Title.GetComponent<Lang_Button>().Constant = labelID;
            modCheckboxCheck.Title.GetComponent<TextMeshProUGUI>().text = labelText;

            // Configure ModMenuItem
            ModMenuItem modCheckboxMenuItem = modCheckbox.AddComponent<ModMenuItem>();
            modCheckboxMenuItem.varID = varID;
            modCheckboxMenuItem.defValue = def ? 1 : 0;
            modCheckboxMenuItem.tempValue = savedValue ? 1 : 0;
            modCheckboxMenuItem.checkboxText = modCheckboxCheck;

            // Configure checkbox button events
            Button modCheckBoxButton = modCheckbox.GetComponentInChildren<Button>();
            modCheckBoxButton.onClick = new Button.ButtonClickedEvent();
            modCheckBoxButton.onClick.AddListener(modCheckboxMenuItem.onClickCheck);

            AddLabelWrap(modCheckbox, modCheckboxCheck.Title.GetComponent<TextMeshProUGUI>(), (RectTransform)modCheckbox.transform.Find("Checkbox"));

            return modCheckbox;
        }

        /// <summary>
        /// Adds a dropdown element to the mod menu.
        /// </summary>
        /// <param name="varID">The variable ID associated with the dropdown.</param>
        /// <param name="labelID">The label ID for the dropdown.</param>
        /// <param name="itemLabelIDs">An array of item label IDs for the dropdown options.</param>
        /// <param name="def">The default selected index of the dropdown.</param>
        /// <param name="parentTransform">The parent transform to add the dropdown to.</param>
        /// <param name="dropdownLayer">Where the dropdown moves while its list is open, so the list shows above every row.</param>
        /// <returns>The GameObject representing the dropdown element.</returns>
        public static GameObject AddMenuDropdown(string varID, string labelID, string[] itemLabelIDs, int def, Transform parentTransform, Transform dropdownLayer)
        {
            string labelText = Language.Data.TryGetValue(labelID, out string tx) ? tx : labelID;

            // Define default value
            int savedValue = int.TryParse(variables.Get(varID), out int savedInt) ? savedInt : def;

            // Set up main object
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Cast<Transform>().FirstOrDefault(t => t.name.Contains("Screen")).gameObject;
            GameObject existingDropdown = existingUIObj.transform.Find("Quality").gameObject;
            GameObject modDropdown = UnityEngine.Object.Instantiate(existingDropdown, parentTransform);
            RectTransform modDropdownRect = modDropdown.GetComponent<RectTransform>();
            SetRectTransform(modDropdownRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modDropdownRect.sizeDelta = new Vector2(400f, LABEL_HEIGHT + BOX_HEIGHT);
            modDropdown.name = MENU_DROPDOWN_OBJ_NAME + "_" + varID;

            // Configure label text
            modDropdown.GetComponentInChildren<Lang_Button>().Constant = labelID;
            modDropdown.GetComponentInChildren<TextMeshProUGUI>().text = labelText;

            // Configure ModMenuItem
            ModMenuItem modDropdownMenuItem = modDropdown.AddComponent<ModMenuItem>();
            modDropdownMenuItem.varID = varID;
            modDropdownMenuItem.defValue = def;
            modDropdownMenuItem.tempValue = savedValue;


            RectTransform modDropdownDropdownRect = modDropdown.transform.Find("Dropdown").GetComponent<RectTransform>();
            PlaceDropdownInRow(modDropdownDropdownRect);

            // Configure dropdown events
            CustomDropdown modDropdownCustomDropdown = modDropdown.GetComponentInChildren<CustomDropdown>();
            modDropdownCustomDropdown.dropdownItems.Clear();
            foreach(string itemLabelID in itemLabelIDs)
            {
                modDropdownCustomDropdown.CreateNewItemFast(Language.Data.TryGetValue(itemLabelID, out string idStr) ? idStr : itemLabelID, null);
            }
            modDropdownCustomDropdown.dropdownEvent = new CustomDropdown.DropdownEvent();
            modDropdownCustomDropdown.dropdownEvent.AddListener(modDropdown.GetComponent<ModMenuItem>().onUpdateDropdown);
            modDropdownMenuItem.customDropdown = modDropdownCustomDropdown;

            // The game's dropdown sets itself up again the first time its page shows, which may be long after the
            // menu opened; it must keep the choice shown then, not load one of its own or run its choice's action
            modDropdownCustomDropdown.saveSelected = false;
            modDropdownCustomDropdown.invokeAtStart = false;
            modDropdownCustomDropdown.SetupDropdown();

            // The game's dropdown moves itself to listParent while open and back to its row when closed
            modDropdownCustomDropdown.isListItem = dropdownLayer != null;
            modDropdownCustomDropdown.listParent = dropdownLayer;

            AddLabelWrap(modDropdown, modDropdown.GetComponentInChildren<TextMeshProUGUI>(), null, modDropdownDropdownRect);

            return modDropdown;
        }

        /// <summary>
        /// Lets a row's label wrap onto more lines, making the row taller (see LabelWrap).
        /// </summary>
        /// <param name="box">A checkbox's box, beside its label; null for other rows.</param>
        /// <param name="below">The controls under the label.</param>
        public static void AddLabelWrap(GameObject row, TextMeshProUGUI label, RectTransform box, params RectTransform[] below)
        {
            LabelWrap wrap = row.AddComponent<LabelWrap>();
            wrap.label = label;
            wrap.box = box;
            wrap.below = below;
        }

        /// <summary>
        /// Puts a dropdown's box under its label, in the top-left of its row. The game's dropdown is drawn at
        /// half size, so its box is BOX_WIDTH wide, like a text field's, and BOX_HEIGHT tall.
        /// </summary>
        /// <param name="labelHeight">How tall the label is: LABEL_HEIGHT, plus any lines it wraps onto.</param>
        public static void PlaceDropdownInRow(RectTransform dropdown, float labelHeight = LABEL_HEIGHT)
        {
            SetRectTransform(dropdown, Vector2.up, Vector2.up, Vector2.zero, new Vector2(0, -labelHeight), Vector2.up);
            dropdown.sizeDelta = new Vector2(2 * BOX_WIDTH, 20f);
        }

        /// <summary>
        /// The game's own text field to copy: the one story mode names the rival with. It uses the same
        /// TextMeshPro text as the rest of the menu, unlike most of the game's text fields; the new game
        /// popup's name fields look the same but aren't in the game scene.
        /// </summary>
        public static GameObject VanillaTextField()
        {
            GameObject vnPopups = Camera.main.GetComponent<mainScript>().Data.GetComponent<ActiveDialogueController>().VN_Popups;
            return vnPopups.GetComponent<vn_popups>().Popup_RivalName.GetComponent<RivalName_Popup>().FirstName;
        }

        /// <summary>
        /// Adds a text field element to the mod menu.
        /// </summary>
        /// <param name="varID">The variable ID associated with the text field.</param>
        /// <param name="labelID">The label ID for the text field.</param>
        /// <param name="def">The default text.</param>
        /// <param name="maxLength">The most characters the player can type, or 0 for no limit.</param>
        /// <param name="password">Whether the field shows asterisks instead of what's typed, for secrets such as keys.</param>
        /// <param name="parentTransform">The parent transform to add the text field to.</param>
        /// <returns>The GameObject representing the text field element.</returns>
        public static GameObject AddMenuInput(string varID, string labelID, string def, int maxLength, bool password, Transform parentTransform)
        {
            string labelText = Language.Data.TryGetValue(labelID, out string tx) ? tx : labelID;

            // Define default value
            string savedValue = variables.Get(varID) ?? def;

            // Set up the row, which holds the label and, under it, the field's box
            GameObject modInput = new(MENU_INPUT_OBJ_NAME + "_" + varID, typeof(RectTransform));
            modInput.transform.SetParent(parentTransform, false);
            RectTransform modInputRect = modInput.GetComponent<RectTransform>();
            SetRectTransform(modInputRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modInputRect.sizeDelta = new Vector2(400f, LABEL_HEIGHT + BOX_HEIGHT);

            GameObject modField = UnityEngine.Object.Instantiate(VanillaTextField(), modInput.transform);
            modField.name = "Field";

            // Tab would jump to the rival's last name field
            UnityEngine.Object.DestroyImmediate(modField.GetComponent<InputField_Tab>());

            // The field's label moves out of the field to the top of the row, so only the field takes clicks
            Transform title = modField.transform.Find("Title");
            title.SetParent(modInput.transform, false);
            SetRectTransform((RectTransform)title, Vector2.up, Vector2.one, new Vector2(0, -LABEL_HEIGHT), Vector2.zero);
            title.GetComponent<Lang_Button>().Constant = labelID;
            title.GetComponent<TextMeshProUGUI>().text = labelText;

            // The field is a white box under the label, the width of a dropdown's box, with the blue underline
            // along its bottom. What's typed is dark, so it doesn't read as another label.
            SetRectTransform((RectTransform)modField.transform, Vector2.up, Vector2.up, new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT), new Vector2(BOX_WIDTH, -LABEL_HEIGHT), Vector2.up);
            modField.GetComponent<Image>().color = Color.white;
            Transform textArea = modField.transform.Find("Container");
            SetRectTransform((RectTransform)textArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Transform text = textArea.Find("Text");
            SetRectTransform((RectTransform)text, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, 0));
            TextMeshProUGUI textTMP = text.GetComponent<TextMeshProUGUI>();
            textTMP.fontSize = TEXT_SIZE;
            textTMP.color = mainScript.black32;
            textTMP.alignment = TextAlignmentOptions.Left;
            SetRectTransform((RectTransform)textArea.Find("Image"), Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 2));

            // Configure ModMenuItem
            ModMenuItem modInputMenuItem = modInput.AddComponent<ModMenuItem>();
            modInputMenuItem.varID = varID;
            modInputMenuItem.defText = def;
            modInputMenuItem.tempText = savedValue;

            // Configure field events; vanilla's would rename the rival
            TMP_InputField modInputField = modField.GetComponent<TMP_InputField>();
            modInputField.characterLimit = maxLength;
            if (password)
            {
                // Only the display changes: Apply still saves the text as typed
                modInputField.contentType = TMP_InputField.ContentType.Password;
            }
            modInputField.onValueChanged = new TMP_InputField.OnChangeEvent();
            modInputField.onValueChanged.AddListener(modInputMenuItem.onUpdateInput);
            modInputMenuItem.inputField = modInputField;

            AddLabelWrap(modInput, title.GetComponent<TextMeshProUGUI>(), null, (RectTransform)modField.transform);

            return modInput;
        }

        /// <summary>
        /// Generates a scrollable column for the mod menu: the settings, or the tabs. ModMenuLayout places it in the panel.
        /// </summary>
        /// <param name="parentTransform">The parent transform to add the scroll area to.</param>
        /// <param name="name">The scroll area's name.</param>
        /// <param name="contentName">The name of the container it scrolls.</param>
        /// <param name="spacing">The gap between the container's children.</param>
        /// <param name="controlChildHeight">Whether the container makes each child its preferred height, rather than leaving it its own.</param>
        /// <param name="scrollRect">The scroll area.</param>
        /// <returns>The GameObject representing the content container of the scroll area.</returns>
        public static GameObject GenerateScrollArea(Transform parentTransform, string name, string contentName, float spacing, bool controlChildHeight, out ScrollRect scrollRect)
        {
            // Create container to control layout
            GameObject menuContentContainer = new(contentName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));

            // Set container alignment
            RectTransform menuContentRect = menuContentContainer.GetComponent<RectTransform>();
            SetRectTransform(menuContentRect, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1));

            // Set layout parameters
            ContentSizeFitter menuContentFitter = menuContentContainer.GetComponent<ContentSizeFitter>();
            menuContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            menuContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            // One column, top to bottom
            SetColumnLayout(menuContentContainer.GetComponent<VerticalLayoutGroup>(), spacing, controlChildHeight);

            // Create the handle for the scrollbar
            GameObject vHandle = new(SCROLLHANDLE_OBJ_NAME, typeof(RectTransform), typeof(Image));

            // Align handle
            RectTransform handleRect = vHandle.GetComponent<RectTransform>();
            SetRectTransform(handleRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Set handle color: the menu's blue, faded
            Image handleImage = vHandle.GetComponent<Image>();
            Color32 blue = mainScript.blue32;
            handleImage.color = new Color32(blue.r, blue.g, blue.b, 128);

            // Create the vertical scroll bar
            GameObject verticalScrollBar = new(SCROLLBAR_OBJ_NAME, typeof(RectTransform), typeof(Scrollbar));

            // Align vertical scroll bar: it sits inside the column, on its right
            RectTransform vScrollbarRect = verticalScrollBar.GetComponent<RectTransform>();
            SetRectTransform(vScrollbarRect, Vector2.right, Vector2.one, new Vector2(-SCROLLBAR_WIDTH, 0), Vector2.zero);

            // Set the Scrollbar properties
            Scrollbar vScrollbar = verticalScrollBar.GetComponent<Scrollbar>();
            vScrollbar.interactable = true;
            vScrollbar.direction = Scrollbar.Direction.BottomToTop;
            vScrollbar.value = 1.0f; // Initial position at the bottom
            vScrollbar.size = 0.1f; // Size of the handle
            vScrollbar.numberOfSteps = 0; // Continuous scrolling
            vScrollbar.targetGraphic = handleImage;
            vScrollbar.handleRect = handleRect;

            // Create ScrollRect container and attach to panel
            GameObject scrollContainer = new(name, typeof(RectTransform), typeof(ScrollRect));

            // Set scrollRect object alignment
            RectTransform scrollRectTransform = scrollContainer.GetComponent<RectTransform>();
            SetRectTransform(scrollRectTransform, Vector2.zero, Vector2.one, new Vector2(PANEL_MARGIN, PANEL_MARGIN), new Vector2(-PANEL_MARGIN, -PANEL_MARGIN));

            // Create view area and attach to scrollrect container
            GameObject viewport = new(VIEWPORT_OBJ_NAME, typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f); // Make the viewport transparent

            // Set view area alignment
            RectTransform viewportTransform = viewport.GetComponent<RectTransform>();
            SetRectTransform(viewportTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-SCROLLBAR_WIDTH - SCROLLBAR_GAP, 0));

            // Configure the ScrollRect
            scrollRect = scrollContainer.GetComponent<ScrollRect>();
            scrollRect.content = menuContentRect; // attach content
            scrollRect.viewport = viewportTransform; // attach viewport
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = false;
            scrollRect.scrollSensitivity = 20;
            scrollRect.verticalNormalizedPosition = 1;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalScrollbar = vScrollbar; // attach scroll bar
            
            // Configure hierarchy
            scrollContainer.transform.SetParent(parentTransform, false); // Panel > Scroll Container
            viewport.transform.SetParent(scrollContainer.transform, false); // Scroll container > viewport
            menuContentContainer.transform.SetParent(viewport.transform, false); // viewport > content container
            verticalScrollBar.transform.SetParent(scrollContainer.transform, false);  // Scroll container > scroll bar
            vHandle.transform.SetParent(verticalScrollBar.transform, false); // scroll bar > handle

            return menuContentContainer;
        }

        /// <summary>
        /// Clones a button and modifies its properties.
        /// </summary>
        /// <param name="oldButton">The original button to clone.</param>
        /// <param name="parent">The parent transform for the new button.</param>
        /// <param name="buttonName">The name for the new button.</param>
        /// <param name="label">The label for the new button.</param>
        /// <param name="removeOrig">Whether to remove the original button.</param>
        /// <param name="removeSprite">Whether to remove the sprite from the new button.</param>
        /// <returns>The GameObject representing the cloned button.</returns>
        public static GameObject CloneButton(GameObject oldButton, Transform parent, string buttonName, string label, bool removeOrig = false, bool removeSprite = false)
        {
            return CloneButton(oldButton, parent, buttonName, label, Color.white, removeOrig, removeSprite);
        }

        /// <summary>
        /// Clones a button and modifies its properties, including color.
        /// </summary>
        /// <param name="oldButton">The original button to clone.</param>
        /// <param name="parent">The parent transform for the new button.</param>
        /// <param name="buttonName">The name for the new button.</param>
        /// <param name="label">The label for the new button.</param>
        /// <param name="col">The color for the new button.</param>
        /// <param name="removeOrig">Whether to remove the original button.</param>
        /// <param name="removeSprite">Whether to remove the sprite from the new button.</param>
        /// <returns>The GameObject representing the cloned button.</returns>
        public static GameObject CloneButton(GameObject oldButton, Transform parent, string buttonName, string label, Color col, bool removeOrig = false, bool removeSprite = false)
        {
            // Create a new button by cloning
            GameObject newButton = UnityEngine.Object.Instantiate(oldButton, parent);
            newButton.name = buttonName;
            Transform newButtonTextTransform = newButton.transform.Find("Text");
            newButtonTextTransform.GetComponent<Lang_Button>().Constant = label;
            newButtonTextTransform.GetComponent<TextMeshProUGUI>().text = Language.Data.TryGetValue(label, out string tx) ? tx : label;

            Image newButtonImage = newButton.GetComponent<Image>();
            newButtonImage.color = col;
            if (removeSprite)
            {
                newButtonImage.sprite = null;
            }

            // Remove the old onClick listeners
            Button buttonComponent = newButton.GetComponent<Button>();
            buttonComponent.onClick = new Button.ButtonClickedEvent();

            // Remove the old Apply button
            if(removeOrig)
            {
                UnityEngine.Object.DestroyImmediate(oldButton);
            }

            return newButton;
        }

        /// <summary>
        /// Sets the properties of a RectTransform with a default pivot.
        /// </summary>
        /// <param name="rt">The RectTransform to modify.</param>
        /// <param name="anchorMin">The minimum anchor point.</param>
        /// <param name="anchorMax">The maximum anchor point.</param>
        /// <param name="offsetMin">The minimum offset.</param>
        /// <param name="offsetMax">The maximum offset.</param>
        private static void SetRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            SetRectTransform(rt, anchorMin, anchorMax, offsetMin, offsetMax, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Sets the properties of a RectTransform.
        /// </summary>
        /// <param name="rt">The RectTransform to modify.</param>
        /// <param name="anchorMin">The minimum anchor point.</param>
        /// <param name="anchorMax">The maximum anchor point.</param>
        /// <param name="offsetMin">The minimum offset.</param>
        /// <param name="offsetMax">The maximum offset.</param>
        /// <param name="pivot">The pivot point of the RectTransform.</param>
        private static void SetRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }


        /// <summary>
        /// Where the panel and the Apply, Cancel and Reset buttons go, in the popup's units, measured from its centre.
        /// </summary>
        public struct MenuLayout
        {
            public Rect Panel;
            public Rect Apply;
            public Rect Cancel;
            public float TabWidth;
            public Rect Reset;
        }

        /// <summary>
        /// Fits the menu to the popup, which covers the screen. The game's popups keep the screen 1024 wide, so
        /// a wider screen gives a shorter popup. The panel is as big as fits, up to PANEL_MAX_WIDTH by
        /// PANEL_MAX_HEIGHT, inside a margin that shrinks on short screens; Cancel, Reset and Apply sit in a row
        /// under it, and the two are centred together. The tab column is a quarter of the panel, between
        /// TAB_MIN_WIDTH and TAB_WIDTH.
        /// </summary>
        /// <param name="popupSize">The popup's size.</param>
        /// <param name="buttonHeight">How tall the buttons are.</param>
        public static MenuLayout LayoutFor(Vector2 popupSize, float buttonHeight)
        {
            float margin = Mathf.Clamp(0.04f * popupSize.y, POPUP_MARGIN_MIN, POPUP_MARGIN_MAX);
            float width = Mathf.Max(0, Mathf.Min(PANEL_MAX_WIDTH, popupSize.x - 2 * margin));
            float height = Mathf.Clamp(popupSize.y - 2 * margin - BUTTON_GAP - buttonHeight, 0, PANEL_MAX_HEIGHT);
            float top = (height + BUTTON_GAP + buttonHeight) / 2;
            float buttonsBottom = top - height - BUTTON_GAP - buttonHeight;

            // On a panel too narrow for them, the buttons and the gaps between them get narrower, staying under it
            float gap = Mathf.Min(BUTTON_ROW_GAP, width / 20);
            float buttonWidth = Mathf.Max(0, Mathf.Min(BUTTON_WIDTH, (width - 2 * gap) / 3));
            float spread = buttonWidth + gap;

            return new MenuLayout
            {
                Panel = new Rect(-width / 2, top - height, width, height),
                Apply = new Rect(spread - buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                Cancel = new Rect(-spread - buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                Reset = new Rect(-buttonWidth / 2, buttonsBottom, buttonWidth, buttonHeight),
                TabWidth = Mathf.Clamp(0.24f * width, TAB_MIN_WIDTH, TAB_WIDTH),
            };
        }

        /// <summary>
        /// How tall a label may be: as tall as its wrapped lines, or, with a limit, up to that many lines. A label
        /// past the limit gets half a line more, so its last line fits whole and the next one doesn't; TextMeshPro
        /// then ends the last line in "…".
        /// </summary>
        /// <param name="wrapped">The label's height, wrapped at its width.</param>
        /// <param name="oneLine">The label's height on one line.</param>
        /// <param name="maxLines">The most lines it shows, or 0 for no limit.</param>
        public static float CappedLabelHeight(float wrapped, float oneLine, int maxLines)
        {
            if (maxLines <= 0)
            {
                return wrapped;
            }
            return Mathf.Min(wrapped, (maxLines + 0.5f) * oneLine);
        }

        /// <summary>
        /// Where a scroll area must scroll to show a child whole, moving as little as it can.
        /// </summary>
        /// <param name="contentHeight">How tall the scrolled content is.</param>
        /// <param name="viewHeight">How tall the visible area is.</param>
        /// <param name="top">The child's top, down from the content's top.</param>
        /// <param name="bottom">The child's bottom, down from the content's top.</param>
        /// <param name="position">Where it's scrolled to now: 1 at the top, 0 at the bottom.</param>
        /// <returns>Where to scroll to, in the same terms.</returns>
        public static float ScrollPositionShowing(float contentHeight, float viewHeight, float top, float bottom, float position)
        {
            float scrollable = contentHeight - viewHeight;
            if (scrollable <= 0)
            {
                return 1;
            }
            float offset = (1 - Mathf.Clamp01(position)) * scrollable;
            if (top < offset)
            {
                offset = top;
            }
            else if (bottom > offset + viewHeight)
            {
                offset = bottom - viewHeight;
            }
            return 1 - Mathf.Clamp01(offset / scrollable);
        }

        /// <summary>
        /// How far an open dropdown list must move up to stay on screen. The game's lists open below their box;
        /// one that would run past the bottom goes above its box instead, the same gap away, if it fits there, and
        /// otherwise moves up until its bottom is on screen. All values are heights on screen, bottom to top.
        /// </summary>
        /// <returns>How far to move the list up, or 0 if it fits where it is.</returns>
        public static float ListShift(float listBottom, float listTop, float boxBottom, float boxTop, float screenBottom, float screenTop)
        {
            if (listBottom >= screenBottom)
            {
                return 0;
            }
            float above = boxTop + Mathf.Max(0, boxBottom - listTop) - listBottom;
            return listTop + above <= screenTop ? above : screenBottom - listBottom;
        }

        /// <summary>
        /// Manages the mod menu functionality: the tabs, the page shown, and Apply and Cancel.
        /// </summary>
        public class ModMenuManager : MonoBehaviour
        {
            /// <summary>
            /// The mod whose tab the player last showed, so the menu reopens on it, even after the menu is
            /// built again for another game.
            /// </summary>
            public static string lastModName;

            /// <summary>The menu while it's open, for MenuValue; null while it's closed.</summary>
            internal static ModMenuManager open;

            public ScrollRect scroll;
            public ScrollRect tabScroll;
            public List<ModMenuTab> tabs;
            public ModMenuTab selected;

            /// <summary>
            /// Frames to wait before scrolling the tabs to the one shown: tab names wrap in the first frames.
            /// </summary>
            private int framesToShowTab;

            /// <summary>
            /// While every row is being set at once, rows with enabledIf are shown or hidden once at the end
            /// instead of after each one.
            /// </summary>
            private bool settingAll;

            /// <summary>The settings whose enabledIf loop has been logged, so each loop is logged once.</summary>
            private HashSet<string> loopsLogged;

            /// <summary>
            /// Called when the mod menu is enabled. Every page shows the saved settings, whether or not
            /// its tab is shown; what was changed and not applied is gone.
            /// </summary>
            public void OnEnable()
            {
                // Set first: the rows' enabledIf variables are read below, and a mod may answer one from MenuValue
                open = this;
                // A list left open when the menu closed would sit where it was, apart from its row,
                // after the menu reopens scrolled to the top
                CloseDropdownLists();
                settingAll = true;
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.ShowSaved();
                }
                settingAll = false;
                RefreshConditions();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1;
                }
                RenderTabs();
                framesToShowTab = 2;
            }

            /// <summary>
            /// Called when the menu closes: MenuValue has nothing to read until it opens again.
            /// </summary>
            public void OnDisable()
            {
                if (open == this)
                {
                    open = null;
                }
            }

            public void LateUpdate()
            {
                if (framesToShowTab <= 0 || --framesToShowTab > 0)
                {
                    return;
                }
                ScrollToTab(selected);
            }

            /// <summary>
            /// Shows a tab's page and hides the others. Lists are closed first: an open list isn't in its page.
            /// </summary>
            public void ShowPage(ModMenuTab tab)
            {
                if (tab == null || tab == selected)
                {
                    return;
                }
                CloseDropdownLists();
                foreach (ModMenuTab other in tabs)
                {
                    other.page.SetActive(other == tab);
                }
                selected = tab;
                lastModName = tab.modName;
                RenderTabs();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1;
                    scroll.StopMovement();
                }
            }

            /// <summary>
            /// Draws each tab as shown or not.
            /// </summary>
            public void RenderTabs()
            {
                if (tabs == null)
                {
                    return;
                }
                foreach (ModMenuTab tab in tabs)
                {
                    tab.Render(tab == selected);
                }
            }

            /// <summary>
            /// Scrolls the tab column so a tab shows whole.
            /// </summary>
            public void ScrollToTab(ModMenuTab tab)
            {
                if (tab == null || tabScroll == null || tabScroll.content == null || tabScroll.viewport == null)
                {
                    return;
                }
                RectTransform tabRect = (RectTransform)tab.transform;
                float height = tabRect.rect.height;

                // The column puts each tab's top-left corner at its place down the column
                float top = -tabRect.anchoredPosition.y - height * (1 - tabRect.pivot.y);
                tabScroll.verticalNormalizedPosition = ScrollPositionShowing(tabScroll.content.rect.height, tabScroll.viewport.rect.height, top, top + height, tabScroll.verticalNormalizedPosition);
            }

            /// <summary>
            /// Closes every open dropdown list.
            /// </summary>
            public void CloseDropdownLists()
            {
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.CloseList();
                }
            }

            /// <summary>
            /// Sets the shown tab's settings back to their defaults, except rows with "reset": false, including
            /// rows hidden by enabledIf. Nothing is saved: Apply saves the defaults, Cancel throws them away.
            /// </summary>
            public void OnReset()
            {
                if (selected == null || selected.page == null)
                {
                    return;
                }
                CloseDropdownLists();
                settingAll = true;
                foreach (ModMenuItem item in selected.page.transform.GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.ShowDefault();
                }
                settingAll = false;
                RefreshConditions();
            }

            /// <summary>
            /// Called when the player changes a setting, which may show or hide rows with enabledIf.
            /// </summary>
            public void OnSettingChanged()
            {
                if (!settingAll)
                {
                    RefreshConditions();
                }
            }

            /// <summary>
            /// Shows each row with enabledIf while its setting is on, on every page (see ConditionMet), and hides
            /// it otherwise; the rows under it move up. A setting's value is its row's, changed or not, on any
            /// tab; or, if no row has it, its saved value. Hidden rows keep their value, and Apply saves it.
            /// </summary>
            public void RefreshConditions()
            {
                ModMenuCondition[] conditions = GetComponentsInChildren<ModMenuCondition>(true);
                if (conditions.Length == 0)
                {
                    return;
                }

                Dictionary<string, string> values = new();
                Dictionary<string, string> rowConditions = new();
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    if (item.varID == null || values.ContainsKey(item.varID))
                    {
                        continue;
                    }
                    values[item.varID] = item.CurrentValue;
                    ModMenuCondition own = item.GetComponent<ModMenuCondition>();
                    rowConditions[item.varID] = own != null ? own.varID : null;
                }
                string ValueOf(string varID) => values.TryGetValue(varID, out string value) ? value : variables.Get(varID);
                string ConditionOf(string varID) => rowConditions.TryGetValue(varID, out string condition) ? condition : null;

                foreach (ModMenuCondition condition in conditions)
                {
                    bool loop = false;
                    bool shown = ConditionMet(condition.varID, ValueOf, ConditionOf, new HashSet<string>(), ref loop);
                    if (loop)
                    {
                        // No row is lost for good: a row in a loop could never be shown again once hidden
                        shown = true;
                        if ((loopsLogged ??= new HashSet<string>()).Add(condition.varID))
                        {
                            Debug.LogWarning("[ModMenus] The enabledIf settings leading from " + condition.varID + " lead back to themselves, so their rows are always shown.");
                        }
                    }
                    if (!shown)
                    {
                        // An open list is in the dropdown layer, not in its row, and would stay there
                        ModMenuItem item = condition.GetComponent<ModMenuItem>();
                        if (item != null)
                        {
                            item.CloseList();
                        }
                    }
                    condition.gameObject.SetActive(shown);
                }
            }

            /// <summary>
            /// Handles the cancel action for the mod menu.
            /// </summary>
            public void OnCancel()
            {
                CloseDropdownLists();
                PopupManager.Close_();
            }

            /// <summary>
            /// Handles the apply action for the mod menu, saving every setting on every page, shown or not.
            /// </summary>
            public void OnApply()
            {
                CloseDropdownLists();
                foreach(ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    // A number field still being typed in hasn't been checked yet
                    item.Validate();
                    variables.Set(item.varID, item.CurrentValue);
                }
                PopupManager.Close_();
            }

        }

        /// <summary>
        /// A mod's tab, in the column on the left. Clicking it shows the mod's page.
        /// </summary>
        public class ModMenuTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public string modName;
            public GameObject page;
            public ModMenuManager manager;
            public Image image;
            public TextMeshProUGUI label;
            public Button button;

            /// <summary>Whether the pointer is over the tab.</summary>
            public bool hovered;

            /// <summary>Whether the tab's page is shown.</summary>
            public bool shown;

            public void OnClick()
            {
                manager.ShowPage(this);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                hovered = true;
                Render(shown);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                hovered = false;
                Render(shown);
            }

            /// <summary>
            /// The shown tab is green with white text, as the game's own tabs are. The others are blue text with
            /// no fill, and a faint blue one while the pointer is over them.
            /// </summary>
            public void Render(bool selected)
            {
                shown = selected;
                Color32 blue = mainScript.blue32;
                image.color = selected ? mainScript.green32 : new Color32(blue.r, blue.g, blue.b, (byte)(hovered ? 38 : 0));
                label.color = selected ? Color.white : (Color)blue;
            }
        }

        /// <summary>
        /// Fits the menu to the popup (see LayoutFor) when it opens and whenever the screen changes size.
        /// </summary>
        public class ModMenuLayout : MonoBehaviour
        {
            public RectTransform panel;
            public RectTransform apply;
            public RectTransform cancel;
            public RectTransform reset;
            public RectTransform tabColumn;
            public RectTransform settingsArea;
            public RectTransform divider;
            public ModMenuManager manager;
            public float buttonHeight;

            private bool sizeChanged;

            public void OnEnable()
            {
                Fit();
            }

            /// <summary>
            /// Called while the canvas is being laid out, so the menu is fitted after it, in LateUpdate.
            /// </summary>
            public void OnRectTransformDimensionsChange()
            {
                sizeChanged = true;
            }

            public void LateUpdate()
            {
                if (!sizeChanged)
                {
                    return;
                }
                sizeChanged = false;
                Fit();
            }

            /// <summary>
            /// Places the panel, the buttons, and the tab column, line and settings inside the panel.
            /// </summary>
            public void Fit()
            {
                Vector2 size = ((RectTransform)transform).rect.size;
                if (size.x <= 0 || size.y <= 0)
                {
                    return;
                }
                MenuLayout layout = LayoutFor(size, buttonHeight);
                PlaceFromCentre(panel, layout.Panel);
                PlaceFromCentre(apply, layout.Apply);
                PlaceFromCentre(cancel, layout.Cancel);
                PlaceFromCentre(reset, layout.Reset);

                float tabsRight = PANEL_MARGIN + layout.TabWidth;
                float line = tabsRight + PANEL_MARGIN;
                SetRectTransform(tabColumn, Vector2.zero, Vector2.up, new Vector2(PANEL_MARGIN, PANEL_MARGIN), new Vector2(tabsRight, -PANEL_MARGIN));
                SetRectTransform(divider, Vector2.zero, Vector2.up, new Vector2(line, PANEL_MARGIN), new Vector2(line + 1, -PANEL_MARGIN));
                SetRectTransform(settingsArea, Vector2.zero, Vector2.one, new Vector2(line + 1 + PANEL_MARGIN, PANEL_MARGIN), new Vector2(-PANEL_MARGIN, -PANEL_MARGIN));

                // An open list would stay where its row was
                if (manager != null)
                {
                    manager.CloseDropdownLists();
                }
            }

            /// <summary>
            /// Puts a rect where the layout says, measured from the popup's centre.
            /// </summary>
            private static void PlaceFromCentre(RectTransform rt, Rect place)
            {
                Vector2 centre = new(0.5f, 0.5f);
                SetRectTransform(rt, centre, centre, Vector2.zero, Vector2.zero, centre);
                rt.sizeDelta = place.size;
                rt.anchoredPosition = place.center;
            }
        }

        /// <summary>
        /// Represents a single item in the mod menu. Its controls are set when it's built, as its page may be
        /// hidden; nothing happens when its page shows, so changes not yet applied stay as they are.
        /// </summary>
        public class ModMenuItem : MonoBehaviour
        {
            /// <summary>
            /// Shows the saved setting, or the default if none is saved, and keeps it until it's changed.
            /// Called for every item when the menu opens.
            /// </summary>
            public void ShowSaved()
            {
                if (settingsSlider != null)
                {
                    RenderSlider();
                }
                if (checkboxText != null)
                {
                    tempValue = GetSavedFloat();
                    RenderCheckbox();
                }
                if (customDropdown != null)
                {
                    RenderDropdown();
                }
                if (inputField != null)
                {
                    RenderInput();
                }
            }

            /// <summary>
            /// Shows the setting's default, unless its row has "reset": false, and keeps it until it's changed.
            /// Called by Reset for every item on the shown tab.
            /// </summary>
            public void ShowDefault()
            {
                if (noReset)
                {
                    return;
                }
                if (settingsSlider != null)
                {
                    ShowSliderAt(defValue);
                }
                if (checkboxText != null)
                {
                    tempValue = defValue;
                    checkboxText.SetCheck(tempValue != 0);
                }
                if (customDropdown != null)
                {
                    ShowDropdownAt(defValue);
                }
                if (inputField != null)
                {
                    ShowInputText(defText);
                }
            }

            /// <summary>
            /// What Apply saves: a text field's text, or the other items' number.
            /// </summary>
            public string CurrentValue => tempText ?? FormatSetting(tempValue);

            /// <summary>
            /// A dropdown's list that opens past the bottom of the screen moves up (see ListShift), and back when it closes.
            /// It's moved every frame while open: the game's dropdown animates its list back into place each frame.
            /// </summary>
            public void LateUpdate()
            {
                if (customDropdown == null || (!customDropdown.isOn && !listMoved))
                {
                    return;
                }
                RectTransform list = ListOf(customDropdown);
                RectTransform box = BoxOf(customDropdown);
                if (list == null)
                {
                    return;
                }

                // What moves is what holds the list beside the box, so the list's shadow moves with it
                RectTransform holder = list;
                while (holder.parent != null && holder.parent != box.parent && holder.parent != customDropdown.transform)
                {
                    holder = (RectTransform)holder.parent;
                }
                if (holder.parent != box.parent)
                {
                    holder = list;
                }

                if (!customDropdown.isOn)
                {
                    holder.anchoredPosition = listPosition;
                    PlaceBeside(false);
                    listMoved = false;
                    return;
                }
                if (!listMoved)
                {
                    listPosition = holder.anchoredPosition;
                    beside = new List<RectTransform>();
                    besidePositions = new List<Vector2>();
                    foreach (Transform child in holder)
                    {
                        if (child != list && !list.IsChildOf(child) && child is RectTransform rect)
                        {
                            beside.Add(rect);
                            besidePositions.Add(rect.anchoredPosition);
                        }
                    }
                    listMoved = true;
                }
                RectTransform screen = customDropdown.listParent as RectTransform;
                if (screen == null || holder.parent.lossyScale.y <= 0)
                {
                    return;
                }
                Rect listRect = WorldRect(list), boxRect = WorldRect(box), screenRect = WorldRect(screen);
                float shift = ListShift(listRect.yMin, listRect.yMax, boxRect.yMin, boxRect.yMax, screenRect.yMin, screenRect.yMax);
                if (shift > 0.5f)
                {
                    holder.anchoredPosition += new Vector2(0, shift / holder.parent.lossyScale.y);
                }
                PlaceBeside(listRect.center.y + Mathf.Max(0, shift) > boxRect.center.y);
            }

            /// <summary>
            /// What's drawn beside the list, its shadow, is offset towards the box, which hides most of it. Above the
            /// box the offset is turned round, so the shadow is hidden behind the box there too.
            /// </summary>
            private void PlaceBeside(bool above)
            {
                if (beside == null)
                {
                    return;
                }
                for (int i = 0; i < beside.Count; i++)
                {
                    if (beside[i] != null)
                    {
                        beside[i].anchoredPosition = above ? new Vector2(besidePositions[i].x, -besidePositions[i].y) : besidePositions[i];
                    }
                }
            }

            /// <summary>
            /// The dropdown's list as drawn: the area its items scroll in.
            /// </summary>
            private static RectTransform ListOf(CustomDropdown dropdown)
            {
                if (dropdown.itemParent == null)
                {
                    return null;
                }
                return (dropdown.itemParent.parent as RectTransform) ?? (RectTransform)dropdown.itemParent;
            }

            /// <summary>
            /// The dropdown's box as drawn: the part showing the choice, which is taller than the dropdown's own rect.
            /// </summary>
            private static RectTransform BoxOf(CustomDropdown dropdown)
            {
                return (dropdown.selectedText != null ? dropdown.selectedText.transform.parent as RectTransform : null) ?? (RectTransform)dropdown.transform;
            }

            private static Rect WorldRect(RectTransform rt)
            {
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }

            /// <summary>
            /// Closes this dropdown's list if it's open. The game's dropdown moves back into its row keeping its
            /// place on screen, which is wrong if the row has moved since it opened (the menu scrolled), so the
            /// box is put back in its place in the row.
            /// </summary>
            public void CloseList()
            {
                if (customDropdown == null || !customDropdown.isOn)
                {
                    return;
                }
                customDropdown.Animate();
                LabelWrap wrap = GetComponent<LabelWrap>();
                PlaceDropdownInRow((RectTransform)customDropdown.transform, LABEL_HEIGHT + (wrap != null ? wrap.Extra : 0));
            }

            /// <summary>
            /// Renders the current state of a slider item.
            /// </summary>
            public void RenderSlider()
            {
                ShowSliderAt(GetSavedFloat());
            }

            /// <summary>
            /// Moves a slider to a value. Vanilla's Settings_Slider.Render_100 isn't used: it needs the label to be
            /// a constant and divides by the maximum, which can be 0.
            /// </summary>
            private void ShowSliderAt(float value)
            {
                float range = settingsSlider.Max_Value - settingsSlider.Min_Value;
                float position = range > 0 ? Mathf.Clamp01((value - settingsSlider.Min_Value) / range) : 0;
                slider.value = position;

                // Shows and keeps the value the slider saves, even if the slider didn't move
                onUpdateSlider(position);
            }

            /// <summary>
            /// Renders the current state of a checkbox item.
            /// </summary>
            public void RenderCheckbox()
            {
                float savedValue = GetSavedFloat();
                checkboxText.SetCheck(savedValue != 0);
            }

            /// <summary>
            /// Renders the current state of a dropdown item.
            /// </summary>
            /// <remarks>
            /// A saved choice past the end of the list (the mod's list got shorter, or its default is past the end)
            /// shows and keeps the last item; the game's dropdown would throw on it every time the menu opened.
            /// </remarks>
            public void RenderDropdown()
            {
                ShowDropdownAt(GetSavedFloat());
            }

            /// <summary>
            /// Shows a dropdown's item, or its last item for a choice past the end of the list.
            /// </summary>
            private void ShowDropdownAt(float value)
            {
                int last = Mathf.Max(customDropdown.dropdownItems.Count - 1, 0);
                int choice = Mathf.Clamp((int)Mathf.Floor(value), 0, last);
                tempValue = choice;

                // Without a click for every dropdown each time the menu opens
                bool sounds = customDropdown.enableDropdownSounds;
                customDropdown.enableDropdownSounds = false;
                customDropdown.ChangeDropdownInfo(choice);
                customDropdown.enableDropdownSounds = sounds;
            }

            /// <summary>
            /// Renders the current state of a text field item.
            /// </summary>
            public void RenderInput()
            {
                ShowInputText(variables.Get(varID) ?? defText);
            }

            /// <summary>
            /// Shows a text field's text. A number field shows the number it keeps of it, or its default if
            /// the text isn't a number (a save edited by hand, say).
            /// </summary>
            private void ShowInputText(string text)
            {
                tempText = text;
                if (inputType != null)
                {
                    validText = NumberDefault(defText, inputType == JSON_INPUT_INTEGER, inputMin, inputMax);
                    Validate();
                    return;
                }
                inputField.SetTextWithoutNotify(tempText);
            }

            /// <summary>
            /// Checks a number field's text (see ValidNumber): anything that isn't a number goes back to the last
            /// number it held. Does nothing for other items.
            /// </summary>
            public void Validate()
            {
                if (inputType == null || inputField == null)
                {
                    return;
                }
                tempText = ValidNumber(tempText, inputType == JSON_INPUT_INTEGER, inputMin, inputMax, validText);
                validText = tempText;
                inputField.SetTextWithoutNotify(tempText);
            }

            /// <summary>
            /// Handles the click event for a checkbox item.
            /// </summary>
            public void onClickCheck()
            {
                if(checkboxText != null)
                {
                    tempValue = 1 - tempValue;
                    checkboxText.SetCheck(tempValue != 0);
                    Changed();
                }
            }

            /// <summary>
            /// Handles value changes for a slider item: it keeps the nearest value on its step (see SnapToStep).
            /// A slider with a step bigger than 1 moves its handle there too, so it moves in steps.
            /// </summary>
            /// <param name="val">The new slider value.</param>
            public void onUpdateSlider(float val)
            {
                if(settingsSlider != null)
                {
                    float min = settingsSlider.Min_Value;
                    float range = settingsSlider.Max_Value - min;
                    tempValue = SnapToStep(min + val * range, min, settingsSlider.Max_Value, step);
                    if (step > 1 && range > 0)
                    {
                        slider.SetValueWithoutNotify((tempValue - min) / range);
                    }
                    sliderText.text = SliderLabel() + ": " + tempValue;
                    Changed();
                }
            }

            /// <summary>
            /// Handles value changes for a dropdown item.
            /// </summary>
            /// <param name="val">The new selected index.</param>
            public void onUpdateDropdown(int val)
            {
                if (customDropdown != null)
                {
                    tempValue = val;
                    Changed();
                }
            }

            /// <summary>
            /// Handles typing in a text field item.
            /// </summary>
            /// <param name="val">The field's text.</param>
            public void onUpdateInput(string val)
            {
                tempText = val;
                Changed();
            }

            /// <summary>
            /// Handles the player leaving a number field: its number is checked (see Validate).
            /// </summary>
            /// <param name="val">The field's text.</param>
            public void onEndInput(string val)
            {
                tempText = val;
                Validate();
                Changed();
            }

            /// <summary>
            /// Tells the menu a setting changed, so rows with enabledIf can show or hide.
            /// </summary>
            private void Changed()
            {
                if (manager != null)
                {
                    manager.OnSettingChanged();
                }
            }

            /// <summary>
            /// Retrieves the saved float value for the item.
            /// </summary>
            /// <returns>The saved float value or the default value if not found.</returns>
            private float GetSavedFloat()
            {
                return TryParseSetting(variables.Get(varID), out float savedValue) ? savedValue : defValue;
            }

            /// <summary>
            /// The slider's label, or its labelID as written if it isn't a constant.
            /// </summary>
            private string SliderLabel()
            {
                return Language.Data.TryGetValue(settingsSlider.Title_Text, out string text) ? text : settingsSlider.Title_Text;
            }

            public string varID;
            public float tempValue;

            /// <summary>
            /// The setting's default from the mod's menu file, which Reset shows. defText for a text field.
            /// </summary>
            public float defValue;

            /// <summary>
            /// A text field's text, which Apply saves, and its default. Null for every other item.
            /// </summary>
            public string tempText;
            public string defText;

            /// <summary>
            /// The item's controls, set when it's built; null for the controls it doesn't have.
            /// </summary>
            public Settings_Slider settingsSlider;
            public Slider slider;
            public TextMeshProUGUI sliderText;
            public Checkbox_Text checkboxText;
            public CustomDropdown customDropdown;
            public TMP_InputField inputField;

            /// <summary>The menu's manager, told when the setting changes.</summary>
            public ModMenuManager manager;

            /// <summary>A slider's step (see SnapToStep).</summary>
            public int step = 1;

            /// <summary>Whether Reset leaves the setting as it is: "reset": false.</summary>
            public bool noReset;

            /// <summary>
            /// A number field's type, "integer" or "decimal", and its limits; null for every other item.
            /// </summary>
            public string inputType;
            public float? inputMin;
            public float? inputMax;

            /// <summary>The number a number field last held, which text that isn't a number goes back to.</summary>
            private string validText;

            /// <summary>Whether the open list was moved up, and where it was before.</summary>
            private bool listMoved;
            private Vector2 listPosition;
            private List<RectTransform> beside;
            private List<Vector2> besidePositions;
        }

        /// <summary>
        /// A row with enabledIf: it's shown only while that setting is on (see ModMenuManager.RefreshConditions).
        /// </summary>
        public class ModMenuCondition : MonoBehaviour
        {
            /// <summary>The setting the row depends on.</summary>
            public string varID;
        }

        /// <summary>
        /// Wraps a row's label onto more lines when it's wider than the row, and makes the row taller by the
        /// extra lines, so a long label isn't cut off. The controls under the label move down with it; a
        /// checkbox's box stays beside its label, centred on it. A label that fits on one line looks as built.
        /// The label is measured again whenever its text or width changes (a slider's label shows its value).
        /// </summary>
        public class LabelWrap : MonoBehaviour
        {
            public TextMeshProUGUI label;

            /// <summary>The controls under the label, which move down by the extra lines. Empty for a checkbox.</summary>
            public RectTransform[] below;

            /// <summary>The checkbox, beside the label; null for every other row.</summary>
            public RectTransform box;

            /// <summary>The most lines the label shows, ending in "…", or 0 for no limit (see CappedLabelHeight).</summary>
            public int maxLines;

            /// <summary>How much taller the row is than built, for the extra lines.</summary>
            public float Extra => extra;

            private float extra;
            private bool ready;
            private string measuredText;
            private float measuredWidth = -1;

            public void LateUpdate()
            {
                if (label == null)
                {
                    return;
                }
                if (!ready)
                {
                    Prepare();
                }
                RectTransform labelRect = label.rectTransform;
                float width = labelRect.rect.width;
                if (width <= 0 || (ReferenceEquals(label.text, measuredText) && width == measuredWidth))
                {
                    return;
                }
                measuredText = label.text;
                measuredWidth = width;

                // Measured on the label's own text: the game's TextMeshPro shows whatever text it last measured.
                // Unwrapped, the label is as tall as the lines it was written with.
                float unwrapped = label.GetPreferredValues(float.PositiveInfinity, 0).y;
                float wrapped = CappedLabelHeight(label.GetPreferredValues(width, 0).y, unwrapped, maxLines);
                float delta = Mathf.Max(0, wrapped - unwrapped) - extra;
                if (Mathf.Abs(delta) < 0.5f)
                {
                    return;
                }
                extra += delta;

                RectTransform row = (RectTransform)transform;
                row.sizeDelta += new Vector2(0, delta);
                if (box == null)
                {
                    // The label grows downwards from its top; a checkbox's label stretches with its row
                    labelRect.sizeDelta += new Vector2(0, delta);
                    labelRect.anchoredPosition -= new Vector2(0, (1 - labelRect.pivot.y) * delta);
                }
                foreach (RectTransform control in below)
                {
                    // An open dropdown's box is in the menu's dropdown layer; it's put back with Extra when its list closes
                    if (control != null && control.parent == transform)
                    {
                        control.anchoredPosition -= new Vector2(0, delta);
                    }
                }
                if (row.parent != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)row.parent);
                }
            }

            /// <summary>
            /// Makes the label wrap at the row's width instead of running past it, shrinking or being cut off.
            /// </summary>
            private void Prepare()
            {
                ready = true;
                if (label.enableAutoSizing)
                {
                    label.enableAutoSizing = false;
                    label.fontSize = TEXT_SIZE;
                }
                label.enableWordWrapping = true;
                label.overflowMode = maxLines > 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;

                // The game's dropdown label is narrower than the row
                RectTransform labelRect = label.rectTransform;
                if (box == null && labelRect.anchorMax.x < 1)
                {
                    labelRect.anchorMin = new Vector2(0, labelRect.anchorMin.y);
                    labelRect.anchorMax = new Vector2(1, labelRect.anchorMax.y);
                    labelRect.sizeDelta = new Vector2(-labelRect.anchoredPosition.x, labelRect.sizeDelta.y);
                }

                // The checkbox stretches to the row's height; keep it its own size, centred on the label
                if (box != null && box.anchorMin.y != box.anchorMax.y)
                {
                    float height = box.rect.height;
                    float centre = box.anchoredPosition.y;
                    box.anchorMin = new Vector2(box.anchorMin.x, 0.5f);
                    box.anchorMax = new Vector2(box.anchorMax.x, 0.5f);
                    box.sizeDelta = new Vector2(box.sizeDelta.x, height);
                    box.anchoredPosition = new Vector2(box.anchoredPosition.x, centre);
                }
            }
        }
    }
}
