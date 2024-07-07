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
using static ModMenus.ModMenusUtils;

namespace ModMenus
{
    /// <summary>
    /// Ensures the mod menu popup is available when the game starts.
    /// </summary>
    [HarmonyPatch(typeof(PopupManager), "Start")]
    public class PopupManager_Start
    {
        /// <summary>
        /// Postfix method that generates the mod menu popup.
        /// </summary>
        public static void Postfix()
        {
            GenerateMenuPopup();
        }
    }

    /// <summary>
    /// Integrates the mod menu access point into the game's existing UI.
    /// </summary>
    [HarmonyPatch(typeof(Tabs_Manager), "Awake")]
    public class Tabs_Manager_Awake
    {
        /// <summary>
        /// Postfix method that adds a mod menu button to the settings panel.
        /// </summary>
        public static void Postfix()
        {
            Transform settingsPanelTransform = Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().GetTab(Tabs_Manager._tab._type.settings).Tab.transform.Find("ScrollRect").Find("Container");
            GameObject mainMenuButton = settingsPanelTransform.Find("Main Menu").gameObject;
            GameObject modMenuButton = CloneButton(mainMenuButton, settingsPanelTransform, "ModMenuButton", "MODMENU__BUTTON_LABEL", false, false);
            modMenuButton.transform.SetSiblingIndex(settingsPanelTransform.childCount - 2);
            modMenuButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                PopupManager.OpenPopup((PopupManager._type)999);
            });

        }
    }

    //[HarmonyPatch(typeof(MainMenu_Buttons_Controller), "Start")]
    //public class MainMenu_Buttons_Controller_Start
    //{
    //    static void Postfix(MainMenu_Buttons_Controller __instance)
    //    {
    //        GameObject modMenuButton = GenerateMenuButton(__instance.ContinueButton);

    //        // Add the new onClick listener
    //        modMenuButton.GetComponent<Button>().onClick.AddListener(() =>
    //        {
    //            PopupManager.OpenPopup((PopupManager._type)999);
    //        });
    //    }
    //}

    /// <summary>
    /// Utility class containing methods for creating and managing mod menu elements.
    /// </summary>
    class ModMenusUtils
    {
        //public static GameObject GenerateMenuButton(GameObject continueButton)
        //{
        //    Transform ButtonsLayout = continueButton.transform.parent;

        //    // Create new button from clone
        //    GameObject modMenuButton = CloneButton(continueButton, ButtonsLayout, "ModMenuButton", "MODMENU__BUTTON_LABEL", new Color(0.8f, 0.8f, 0.2f, 1f), false, true);

        //    modMenuButton.AddComponent<LayoutElement>().ignoreLayout = true;

        //    RectTransform modMenuButtonRect = modMenuButton.GetComponent<RectTransform>();
        //    SetRectTransform(modMenuButtonRect, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero, Vector2.one);
        //    modMenuButtonRect.sizeDelta = new Vector2(240, 40);

        //    return modMenuButton;
        //}

        /// <summary>
        /// Generates the main mod menu popup.
        /// </summary>
        /// <returns>The GameObject representing the mod menu popup.</returns>
        public static GameObject GenerateMenuPopup()
        {
            PopupManager mainPopupManager = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>();
            PopupManager._popup existingPopup = mainPopupManager.GetByType((PopupManager._type)999);
            if (existingPopup != null) 
                return existingPopup.obj;

            // Adapt existing popup
            GameObject originalModMenuObj = mainPopupManager.GetByType(PopupManager._type.settings_difficulty).obj;
            GameObject modMenuObj = UnityEngine.Object.Instantiate(originalModMenuObj);
            modMenuObj.transform.SetParent(originalModMenuObj.transform.parent, false);
            modMenuObj.name = "ModMenu";

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
            GameObject applyButton = CloneButton(oldApplyButton, oldApplyButton.transform.parent, "modMenuApply", "APPLY", new Color(0.2f, 0.7f, 0.2f), true);
            GameObject cancelButton = CloneButton(oldCancelButton, oldCancelButton.transform.parent, "modMenuCancel", "CANCEL", true);

            // Attach custom component to Panel
            ModMenuManager panelModMenuManager = panelTransform.gameObject.AddComponent<ModMenuManager>();
            applyButton.GetComponent<Button>().onClick.AddListener(panelModMenuManager.OnApply);
            cancelButton.GetComponent<Button>().onClick.AddListener(panelModMenuManager.OnCancel);

            // Create Menu Scroll objects
            GameObject menuContentContainer = GenerateScrollArea(panelTransform);

            // Generate Menu Contents
            AddMenuItems(menuContentContainer.transform);

            // Incorporate popup into game
            PopupManager._popup newPopup = new()
            {
                type = (PopupManager._type)999,
                obj = modMenuObj,
                BGBlur = true,
                BGDarken = true
            };
            Array.Resize(ref mainPopupManager.popups, mainPopupManager.popups.Length + 1);
            mainPopupManager.popups[mainPopupManager.popups.Length - 1] = newPopup;

            return modMenuObj;
        }

        /// <summary>
        /// Adds menu items to the mod menu based on JSON configuration files.
        /// </summary>
        /// <param name="parentTransform">The parent transform to add menu items to.</param>
        public static void AddMenuItems(Transform parentTransform)
        {

            string relativePath = Path.Combine("JSON", "Mod Menu", "modmenu.json");
            string filepath;
            if (relativePath[0].ToString() == "/")
            {
                relativePath = relativePath.Substring(1);
            }

            int firstDropdown = -1;
            GameObject dropdownItem;

            // Reverse the for loops because the menu generates from the bottom up to ensure proper layering
            List<Mods._mod> reversedMods = new(Mods._Mods);
            reversedMods.Reverse();

            foreach (Mods._mod mod in reversedMods)
            {
                if (!mod.IsEnabled()) 
                    continue;

                filepath = Path.Combine(mod.Path, relativePath).Replace("\\", "/");
                if (!File.Exists(filepath)) 
                    continue;
                
                string data = File.ReadAllText(filepath);
                JSONArray jsonArray = JSON.Parse(data).AsArray;

                for (int i = jsonArray.Count - 1; i >= 0; i--)
                {
                    JSONNode item = jsonArray[i];
                    if (string.IsNullOrEmpty(item["type"]) || string.IsNullOrEmpty(item["labelID"]) || item["ignore"].AsBool) continue;

                    string type = item["type"];
                    string label = item["labelID"];
                    string id;

                    switch (type)
                    {
                        case "text":
                            AddMenuText(label, parentTransform, 15, mainScript.blue32, TextAlignmentOptions.Left);
                            break;

                        case "slider":
                            if (string.IsNullOrEmpty(item["varID"])) 
                                continue;

                            id = item["varID"];
                            float minValue = 0;
                            float maxValue = 100;
                            if (!string.IsNullOrEmpty(item["minValue"]) && !string.IsNullOrEmpty(item["maxValue"]))
                            {
                                minValue = item["minValue"].AsFloat;
                                maxValue = item["maxValue"].AsFloat;
                            }
                            float defaultFloat = maxValue + minValue / 2;
                            if (!string.IsNullOrEmpty(item["defaultValue"]))
                            {
                                defaultFloat = item["defaultValue"].AsFloat;
                            }

                            AddMenuSlider(id, label, minValue, maxValue, defaultFloat, parentTransform);
                            break;

                        case "checkbox":
                            if (string.IsNullOrEmpty(item["varID"])) 
                                continue;

                            id = item["varID"];
                            bool defaultBool = false;
                            if (!string.IsNullOrEmpty(item["defaultValue"]))
                            {
                                defaultBool = item["defaultValue"].AsBool;
                            }

                            AddMenuCheckbox(id, label, defaultBool, parentTransform);
                            break;

                        case "dropdown":
                            if (string.IsNullOrEmpty(item["varID"]) || string.IsNullOrEmpty(item["itemIDList"].ToString()) || item["itemIDList"].AsArray == null) 
                                continue;

                            id = item["varID"];
                            int defaultSelect = 0;
                            if (!string.IsNullOrEmpty(item["defaultValue"]))
                            {
                                defaultSelect = item["defaultValue"].AsInt;
                            }

                            JSONArray jsonArrayItems = item["itemIDList"].AsArray;
                            string[] itemList = new string[jsonArrayItems.Count];
                            for (int j = 0; j < jsonArrayItems.Count; j++)
                            {
                                itemList[j] = jsonArrayItems[j];
                            }

                            dropdownItem = AddMenuDropdown(id, label, itemList, defaultSelect, parentTransform);

                            if (firstDropdown < 0)
                            {
                                firstDropdown = dropdownItem.transform.GetSiblingIndex();
                            }
                            break;
                    }
                }

                AddMenuText(mod.ModName, parentTransform, 20, mainScript.black32, TextAlignmentOptions.Center);
                
            }

            // Add blank rows as padding if a dropdown list is too close to the bottom
            if(firstDropdown > -1)
            {
                for (int i = 0; i < 2 - firstDropdown; i++)
                {
                    AddMenuText("", parentTransform, 15, mainScript.blue32, TextAlignmentOptions.Left).transform.SetAsFirstSibling();
                }
            }

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
            GameObject menuTextObj = new("ModMenuText_" + textID, typeof(RectTransform), typeof(TextMeshProUGUI));
            menuTextObj.transform.SetParent(parentTransform, false);


            // Set text object alignment
            RectTransform contentRectTransform = menuTextObj.GetComponent<RectTransform>();
            SetRectTransform(contentRectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            contentRectTransform.sizeDelta = new Vector2(200, 20);

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
                return null;

            // Define default value
            float savedValue = float.TryParse(variables.Get(varID), out float savedFloat) ? savedFloat : def;

            // Set up main slider object
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Find("Tabs").Find("TabButton (3)").GetComponent<TabButton>().TabToOpen;
            GameObject existingSlider = existingUIObj.transform.Find("Autosave").gameObject;
            GameObject modSlider = UnityEngine.Object.Instantiate(existingSlider, parentTransform);
            RectTransform modSliderRect = modSlider.GetComponent<RectTransform>();
            SetRectTransform(modSliderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modSliderRect.sizeDelta = new Vector2(400f, 40f);
            modSlider.name = "ModMenuSlider_" + varID;

            // Configure Settings_Slider
            Settings_Slider modSliderSettingsSlider = modSlider.GetComponent<Settings_Slider>();
            modSliderSettingsSlider.Min_Value = min;
            modSliderSettingsSlider.Max_Value = max;
            modSliderSettingsSlider.Title_Text = labelID;

            // Configure ModMenuItem
            ModMenuItem modSliderMenuItem = modSlider.AddComponent<ModMenuItem>();
            modSliderMenuItem.varID = varID;
            modSliderMenuItem.defValue = savedValue;
            modSliderMenuItem.tempValue = savedValue;

            // Configure Slider events
            Slider modSliderSlider = modSlider.GetComponentInChildren<Slider>();
            modSliderSlider.onValueChanged = new Slider.SliderEvent();
            modSliderSlider.onValueChanged.AddListener(modSliderMenuItem.onUpdateSlider);

            // Set slider size and pos
            RectTransform SliderRect = modSlider.transform.Find("Slider").GetComponent<RectTransform>();
            //SliderRect.pivot = new Vector2(0.5f, 0.5f);
            SetRectTransform(SliderRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, -50));
            SliderRect.sizeDelta = new Vector2(20f, 400f);

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
            bool savedValue = float.TryParse(variables.Get(varID), out float savedFloat) ? savedFloat != 0 : def;

            // Set up main object
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Find("Tabs").Find("TabButton (3)").GetComponent<TabButton>().TabToOpen;
            GameObject existingCheckbox = existingUIObj.transform.Find("Run In Background").gameObject;
            GameObject modCheckbox = UnityEngine.Object.Instantiate(existingCheckbox, parentTransform);
            RectTransform modCheckboxRect = modCheckbox.GetComponent<RectTransform>();
            SetRectTransform(modCheckboxRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modCheckboxRect.sizeDelta = new Vector2(400f, 40f);
            modCheckbox.name = "ModMenuCheckbox_" + varID;

            // Configure Checkbox_Text
            Checkbox_Text modCheckboxCheck = modCheckbox.GetComponent<Checkbox_Text>();
            modCheckboxCheck.Title.GetComponent<Lang_Button>().Constant = labelID;
            modCheckboxCheck.Title.GetComponent<TextMeshProUGUI>().text = labelText;

            // Configure ModMenuItem
            ModMenuItem modCheckboxMenuItem = modCheckbox.AddComponent<ModMenuItem>();
            modCheckboxMenuItem.varID = varID;
            modCheckboxMenuItem.defValue = savedValue ? 1 : 0;
            modCheckboxMenuItem.tempValue = savedValue ? 1 : 0;

            // Configure checkbox button events
            Button modCheckBoxButton = modCheckbox.GetComponentInChildren<Button>();
            modCheckBoxButton.onClick = new Button.ButtonClickedEvent();
            modCheckBoxButton.onClick.AddListener(modCheckboxMenuItem.onClickCheck);

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
        /// <returns>The GameObject representing the dropdown element.</returns>
        public static GameObject AddMenuDropdown(string varID, string labelID, string[] itemLabelIDs, int def, Transform parentTransform)
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
            modDropdownRect.sizeDelta = new Vector2(400f, 60f);
            modDropdown.name = "ModMenuDropdown_" + varID;

            // Configure label text
            modDropdown.GetComponentInChildren<Lang_Button>().Constant = labelID;
            modDropdown.GetComponentInChildren<TextMeshProUGUI>().text = labelText;

            // Configure ModMenuItem
            ModMenuItem modDropdownMenuItem = modDropdown.AddComponent<ModMenuItem>();
            modDropdownMenuItem.varID = varID;
            modDropdownMenuItem.defValue = savedValue;
            modDropdownMenuItem.tempValue = savedValue;


            RectTransform modDropdownDropdownRect = modDropdown.transform.Find("Dropdown").GetComponent<RectTransform>();
            SetRectTransform(modDropdownDropdownRect, Vector2.up, Vector2.up, Vector2.zero, new Vector2(0, -20), Vector2.up);
            modDropdownDropdownRect.sizeDelta = new Vector2(300f, 20f);

            // Configure dropdown events
            CustomDropdown modDropdownCustomDropdown = modDropdown.GetComponentInChildren<CustomDropdown>();
            modDropdownCustomDropdown.dropdownItems.Clear();
            foreach(string itemLabelID in itemLabelIDs)
            {
                modDropdownCustomDropdown.CreateNewItemFast(Language.Data.TryGetValue(itemLabelID, out string idStr) ? idStr : itemLabelID, null);
            }
            modDropdownCustomDropdown.dropdownEvent = new CustomDropdown.DropdownEvent();
            modDropdownCustomDropdown.dropdownEvent.AddListener(modDropdown.GetComponent<ModMenuItem>().onUpdateDropdown);
            modDropdownCustomDropdown.SetupDropdown();

            return modDropdown;
        }

        /// <summary>
        /// Generates a scrollable area for the mod menu.
        /// </summary>
        /// <param name="parentTransform">The parent transform to add the scroll area to.</param>
        /// <returns>The GameObject representing the content container of the scroll area.</returns>
        public static GameObject GenerateScrollArea(Transform parentTransform)
        {

            // Create container to control layout
            GameObject menuContentContainer = new("MenuContainer", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));

            // Set container alignment
            RectTransform menuContentRect = menuContentContainer.GetComponent<RectTransform>();
            SetRectTransform(menuContentRect, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1));

            // Set layout parameters
            ContentSizeFitter menuContentFitter = menuContentContainer.GetComponent<ContentSizeFitter>();
            menuContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            menuContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            GridLayoutGroup menuContentLayoutGroup = menuContentContainer.GetComponent<GridLayoutGroup>();
            menuContentLayoutGroup.startCorner = GridLayoutGroup.Corner.LowerLeft;
            menuContentLayoutGroup.startAxis = GridLayoutGroup.Axis.Vertical;
            menuContentLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            menuContentLayoutGroup.constraintCount = 1;
            menuContentLayoutGroup.spacing = new Vector2(0, 10);

            // Create the handle for the scrollbar
            GameObject vHandle = new("VerticalHandle", typeof(RectTransform), typeof(Image));

            // Align handle
            RectTransform handleRect = vHandle.GetComponent<RectTransform>();
            SetRectTransform(handleRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Set handle color
            Image handleImage = vHandle.GetComponent<Image>();
            handleImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

            // Create the vertical scroll bar
            GameObject verticalScrollBar = new("VerticalScrollBar", typeof(RectTransform), typeof(Scrollbar));

            // Align vertical scroll bar
            RectTransform vScrollbarRect = verticalScrollBar.GetComponent<RectTransform>();
            SetRectTransform(vScrollbarRect, Vector2.right, Vector2.one, Vector2.zero, Vector2.zero);
            vScrollbarRect.sizeDelta = new Vector2(10f, 0);

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
            GameObject scrollContainer = new("ScrollContainer", typeof(RectTransform), typeof(ScrollRect));

            // Set scrollRect object alignment
            RectTransform scrollRectTransform = scrollContainer.GetComponent<RectTransform>();
            SetRectTransform(scrollRectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            scrollRectTransform.sizeDelta = new Vector2(-20, -20); // padding
            
            // Create view area and attach to scrollrect container
            GameObject viewport = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f); // Make the viewport transparent

            // Set view area alignment
            RectTransform viewportTransform = viewport.GetComponent<RectTransform>();
            SetRectTransform(viewportTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, 0));
            viewportTransform.sizeDelta = new Vector2(-10, -10); // padding

            // Configure the ScrollRect
            ScrollRect scrollRect = scrollContainer.GetComponent<ScrollRect>();
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

            menuContentLayoutGroup.cellSize = new Vector2(viewportTransform.rect.width, 40);

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

        //public static void ProbeGameObject(GameObject gameObject)
        //{
        //    Debug.Log("Probing Object: " + gameObject.name);

        //    Component[] comps = gameObject.GetComponentsInChildren<Component>();

        //    string compName;
        //    string prefix;
        //    string suffix;
        //    foreach (Component comp in comps)
        //    {
        //        prefix = "";
        //        suffix = "";
        //        compName = comp.GetType().Name;

        //        if (comp is RectTransform rt)
        //        {
        //            suffix = " (" + rt.rect.width + " x " + rt.rect.height + ")";
        //        }
        //        if (comp is CanvasGroup cg)
        //        {
        //            suffix = " (alpha: " + cg.alpha + ")";
        //        }
        //        if (comp is TextMeshProUGUI tmp)
        //        {
        //            suffix = " (" + tmp.text + ")";
        //        }
        //        if (comp is Text tx)
        //        {
        //            suffix = " (" + tx.text + ")";
        //        }
        //        if (comp is Lang_Button lb)
        //        {
        //            suffix = " (" + lb.Constant + ")";
        //        }
        //        if (comp is Image im)
        //        {
        //            Sprite sprite = im.sprite;
        //            suffix = " (";
        //            if (sprite != null)
        //            {
        //                suffix += "sprite: " + sprite.name + ", ";
        //            }
        //            suffix += im.color.ToString();
        //            suffix += ")";
        //        }
        //        if (comp is RawImage ri)
        //        {
        //            Texture texture = ri.texture;
        //            if (texture != null)
        //            {
        //                suffix = " (texture: " + texture.name + ")";
        //            }
        //        }
        //        if (comp is Button btn)
        //        {
        //            int eventCount = btn.onClick.GetPersistentEventCount();
        //            if (eventCount > 0)
        //            {
        //                suffix = " (" + btn.onClick.GetPersistentTarget(0).GetType().Name + "." + btn.onClick.GetPersistentMethodName(0);

        //                int i = 1;
        //                while (i < eventCount)
        //                {
        //                    suffix += ", " + btn.onClick.GetPersistentTarget(i).GetType().Name + "." + btn.onClick.GetPersistentMethodName(i);
        //                    i++;
        //                }
        //                suffix += ")";
        //            }
        //        }

        //        Transform trans = comp.gameObject.transform;
        //        while (trans != null)
        //        {
        //            prefix = trans.gameObject.name + ":" + prefix;
        //            trans = trans.parent;
        //        }
        //        Debug.Log(prefix + compName + suffix);

        //    }
        //}


        /// <summary>
        /// Manages the mod menu functionality.
        /// </summary>
        public class ModMenuManager : MonoBehaviour
        {
            /// <summary>
            /// Called when the mod menu is enabled.
            /// </summary>
            public void OnEnable()
            {
                Transform scrollContainer = gameObject.transform.Find("ScrollContainer");
                if(scrollContainer != null)
                {
                    scrollContainer.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
                }
            }

            /// <summary>
            /// Handles the cancel action for the mod menu.
            /// </summary>
            public void OnCancel()
            {
                PopupManager.Close_();
            }

            /// <summary>
            /// Handles the apply action for the mod menu, saving all changes.
            /// </summary>
            public void OnApply()
            {
                foreach(ModMenuItem item in GetComponentsInChildren<ModMenuItem>())
                {
                    variables.Set(item.varID, item.tempValue.ToString());
                }
                PopupManager.Close_();
            }

        }

        /// <summary>
        /// Represents a single item in the mod menu.
        /// </summary>
        public class ModMenuItem : MonoBehaviour
        {
            /// <summary>
            /// Initializes the ModMenuItem component.
            /// </summary>
            public void Awake()
            {
                settingsSlider = GetComponent<Settings_Slider>();
                if(settingsSlider != null)
                {
                    slider = settingsSlider.Slider_Obj.GetComponent<Slider>();
                    sliderText = settingsSlider.Title.GetComponent<TextMeshProUGUI>();
                }
                checkboxText = GetComponent<Checkbox_Text>();
                customDropdown = GetComponentInChildren<CustomDropdown>();
            }

            /// <summary>
            /// Called when the ModMenuItem is enabled.
            /// </summary>
            public void OnEnable()
            {
                if (settingsSlider != null)
                {
                    onUpdateSlider(slider.value);
                    RenderSlider();
                }
                if (checkboxText != null)
                {
                    tempValue = GetSavedFloat();
                    RenderCheckbox();
                }
                if (customDropdown != null)
                {
                    tempValue = GetSavedFloat();
                    RenderDropdown();
                }
            }

            /// <summary>
            /// Renders the current state of a slider item.
            /// </summary>
            public void RenderSlider()
            {
                float savedValue = GetSavedFloat();
                float normalizedValue = (savedValue - settingsSlider.Min_Value) / (settingsSlider.Max_Value - settingsSlider.Min_Value) * settingsSlider.Max_Value;
                settingsSlider.Render_100(normalizedValue);
                sliderText.text = $"{Language.Data[settingsSlider.Title_Text]}: {savedValue}";
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
            public void RenderDropdown()
            {
                int savedValue = (int)Mathf.Floor(GetSavedFloat());
                customDropdown.ChangeDropdownInfo(savedValue);
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
                }
            }

            /// <summary>
            /// Handles value changes for a slider item.
            /// </summary>
            /// <param name="val">The new slider value.</param>
            public void onUpdateSlider(float val)
            {
                if(settingsSlider != null)
                {
                    tempValue = Mathf.Round(settingsSlider.Min_Value + val * (settingsSlider.Max_Value - settingsSlider.Min_Value));
                    sliderText.text = Language.Data[settingsSlider.Title_Text] + ": " + tempValue;
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
                }
            }

            /// <summary>
            /// Retrieves the saved float value for the item.
            /// </summary>
            /// <returns>The saved float value or the default value if not found.</returns>
            private float GetSavedFloat()
            {
                return float.TryParse(variables.Get(varID), out float savedValue) ? savedValue : defValue;
            }

            public string varID;
            public float tempValue;
            public float defValue;

            private Settings_Slider settingsSlider;
            private Slider slider;
            private TextMeshProUGUI sliderText;
            private Checkbox_Text checkboxText;
            private CustomDropdown customDropdown;
        }
    }
}
