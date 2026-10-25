using System;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UiCopies;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// The popup type the mod menu is registered under: MOD_MENU_POPUP, or the next number no popup has if
        /// another mod registered a popup under it first.
        /// </summary>
        public static PopupManager._type MenuPopupType { get; private set; } = MOD_MENU_POPUP;

        /// <summary>
        /// Generates the main mod menu popup.
        /// </summary>
        /// <returns>The GameObject representing the mod menu popup, or null if it couldn't be built.</returns>
        public static GameObject GenerateMenuPopup()
        {
            PopupManager mainPopupManager = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>();
            PopupManager._popup existingPopup = mainPopupManager.GetByType(MenuPopupType);
            if (existingPopup != null && IsModMenu(existingPopup.obj))
                return existingPopup.obj;

            // Adapt existing popup
            GameObject modMenuObj = CopyDifficultyPopup(mainPopupManager.GetByType(PopupManager._type.settings_difficulty).obj);
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
            MenuPopupType = FreePopupType(mainPopupManager.popups);
            PopupManager._popup newPopup = new()
            {
                type = MenuPopupType,
                obj = modMenuObj,
                BGBlur = true,
                BGDarken = true
            };
            Array.Resize(ref mainPopupManager.popups, mainPopupManager.popups.Length + 1);
            mainPopupManager.popups[mainPopupManager.popups.Length - 1] = newPopup;

            return modMenuObj;
        }

        /// <summary>
        /// The game's difficulty settings popup, as read in the game with no mods loaded.
        /// </summary>
        public static readonly VanillaShape DifficultyPopupShape = new(
            new[] { typeof(RectTransform), typeof(global::Popup), typeof(CanvasGroup), typeof(Settings_Difficulty) },
            new[]
            {
                "Cancel", "Cancel/Text", "Apply", "Apply/Text", "Panel", "Panel/Settings_Container",
                "Panel/Settings_Container/Title", "Panel/Settings_Container/Title/Title",
                "Panel/Settings_Container/Relaxed", "Panel/Settings_Container/Relaxed/Checkbox",
                "Panel/Settings_Container/Relaxed/Title", "Panel/Settings_Container/Relaxed/Description",
                "Panel/Settings_Container/Normal", "Panel/Settings_Container/Normal/Checkbox",
                "Panel/Settings_Container/Normal/Title", "Panel/Settings_Container/Normal/Description",
                "Panel/Settings_Container/Unfair", "Panel/Settings_Container/Unfair/Checkbox",
                "Panel/Settings_Container/Unfair/Title", "Panel/Settings_Container/Unfair/Description",
            });

        /// <summary>
        /// A copy of the game's difficulty settings popup, next to it, without what other mods have added to it.
        /// </summary>
        public static GameObject CopyDifficultyPopup(GameObject original) =>
            UiCopy.CopyGameObject(original, original.transform.parent, DifficultyPopupShape, "[ModMenus]", "difficulty settings popup");

        /// <summary>
        /// MOD_MENU_POPUP, or, if a popup already has it, the first number after it that no popup has.
        /// </summary>
        public static PopupManager._type FreePopupType(PopupManager._popup[] popups)
        {
            PopupManager._type type = MOD_MENU_POPUP;
            while (popups != null && popups.Any(popup => popup != null && popup.type == type))
            {
                type++;
            }
            return type;
        }

        /// <summary>
        /// Whether a popup is the mod menu, not another mod's popup with the same type.
        /// </summary>
        private static bool IsModMenu(GameObject popup)
        {
            Transform panel = popup == null ? null : popup.transform.Find("Panel");
            return panel != null && panel.GetComponent<ModMenuManager>() != null;
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
            // Rows copy it for their own buttons (see AddMenuSecret)
            buttonTemplate = resetButton;

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
            image.color = DividerColor();
            image.raycastTarget = false;
            return divider;
        }

        /// <summary>
        /// The colour of the menu's lines: the game's blue, faint.
        /// </summary>
        public static Color32 DividerColor()
        {
            Color32 blue = mainScript.blue32;
            return new Color32(blue.r, blue.g, blue.b, 77);
        }
    }
}
