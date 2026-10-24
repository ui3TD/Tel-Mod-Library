using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.ModernUIPack;
using System.Linq;

namespace ModMenus
{
    partial class ModMenusUtils
    {
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

            GameObject modCheckbox = CopyCheckbox(MENU_CHECKBOX_OBJ_NAME + "_" + varID, labelID, labelText, parentTransform);
            Checkbox_Text modCheckboxCheck = modCheckbox.GetComponent<Checkbox_Text>();

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
        /// A copy of the game's Run In Background checkbox with this label, as wide as a row. Its click does
        /// nothing until the caller adds what it does.
        /// </summary>
        public static GameObject CopyCheckbox(string name, string labelID, string labelText, Transform parentTransform)
        {
            GameObject existingUIObj = PopupManager.GetObject(PopupManager._type.main_menu_settings).transform.Find("Panel").Find("Tabs").Find("TabButton (3)").GetComponent<TabButton>().TabToOpen;
            GameObject existingCheckbox = existingUIObj.transform.Find("Run In Background").gameObject;
            GameObject modCheckbox = UnityEngine.Object.Instantiate(existingCheckbox, parentTransform);
            RectTransform modCheckboxRect = modCheckbox.GetComponent<RectTransform>();
            SetRectTransform(modCheckboxRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modCheckboxRect.sizeDelta = new Vector2(400f, CHECKBOX_HEIGHT);
            modCheckbox.name = name;

            Checkbox_Text modCheckboxCheck = modCheckbox.GetComponent<Checkbox_Text>();
            modCheckboxCheck.Title.GetComponent<Lang_Button>().Constant = labelID;
            modCheckboxCheck.Title.GetComponent<TextMeshProUGUI>().text = labelText;

            // Vanilla's would turn Run In Background on or off
            Button button = modCheckbox.GetComponentInChildren<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            return modCheckbox;
        }

        /// <summary>
        /// Adds a tab's "Use in all saves" box, under the tab's title, the note under it that shows while it's
        /// ticked, a line saying secrets are in every save on a tab that has one, and a line under them that
        /// separates them from the mod's rows. It isn't a setting: Reset leaves it alone, and Apply keeps it in
        /// SharedSettings, not in the save.
        /// </summary>
        /// <param name="tabID">The tab, as SharedSettings keeps it.</param>
        /// <param name="hasSecret">Whether the tab has a secret row, which is the same in every save whether the box is ticked or not.</param>
        /// <param name="parentTransform">Where the row goes: the tab's page.</param>
        public static GameObject AddScopeRow(string tabID, bool hasSecret, Transform parentTransform)
        {
            GameObject row = CopyCheckbox(SCOPE_ROW_OBJ_NAME, EVERY_SAVE_LABEL, ShownText(EVERY_SAVE_LABEL), parentTransform);
            Checkbox_Text check = row.GetComponent<Checkbox_Text>();

            GameObject note = AddMenuText(EVERY_SAVE_NOTE, parentTransform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);
            note.name = SCOPE_NOTE_OBJ_NAME;
            note.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (hasSecret)
            {
                GameObject secretNote = AddMenuText(EVERY_SAVE_SECRET_NOTE, parentTransform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);
                secretNote.name = SCOPE_SECRET_NOTE_OBJ_NAME;
                secretNote.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // As thick as the line between the tabs and the settings
            GameObject line = new(SCOPE_LINE_OBJ_NAME, typeof(RectTransform), typeof(Image));
            line.transform.SetParent(parentTransform, false);
            ((RectTransform)line.transform).sizeDelta = new Vector2(200, 1);
            Image lineImage = line.GetComponent<Image>();
            lineImage.color = DividerColor();
            lineImage.raycastTarget = false;

            ModMenuScopeRow scope = row.AddComponent<ModMenuScopeRow>();
            scope.tabID = tabID;
            scope.page = parentTransform;
            scope.checkboxText = check;
            scope.note = note;
            scope.Show(false);

            Button button = row.GetComponentInChildren<Button>();
            button.onClick.AddListener(scope.OnClick);

            AddLabelWrap(row, check.Title.GetComponent<TextMeshProUGUI>(), (RectTransform)row.transform.Find("Checkbox"));
            return row;
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
        /// <param name="parentTransform">The parent transform to add the text field to.</param>
        /// <returns>The GameObject representing the text field element.</returns>
        public static GameObject AddMenuInput(string varID, string labelID, string def, int maxLength, Transform parentTransform)
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

            TextMeshProUGUI title = PlaceTextBox(modInput, modField, labelID, labelText);

            // Configure ModMenuItem
            ModMenuItem modInputMenuItem = modInput.AddComponent<ModMenuItem>();
            modInputMenuItem.varID = varID;
            modInputMenuItem.defText = def;
            modInputMenuItem.tempText = savedValue;

            // Configure field events; vanilla's would rename the rival
            TMP_InputField modInputField = modField.GetComponent<TMP_InputField>();
            modInputField.characterLimit = maxLength;
            modInputField.onValueChanged = new TMP_InputField.OnChangeEvent();
            modInputField.onValueChanged.AddListener(modInputMenuItem.onUpdateInput);
            modInputMenuItem.inputField = modInputField;

            AddLabelWrap(modInput, title, null, (RectTransform)modField.transform);

            return modInput;
        }

        /// <summary>
        /// Lays out a copy of the game's text field in its row: the field's label moves to the top of the row,
        /// and the field is a white box under it with dark text.
        /// </summary>
        /// <returns>The label.</returns>
        public static TextMeshProUGUI PlaceTextBox(GameObject row, GameObject field, string labelID, string labelText)
        {
            // The field's label moves out of the field to the top of the row, so only the field takes clicks
            Transform title = field.transform.Find("Title");
            title.SetParent(row.transform, false);
            SetRectTransform((RectTransform)title, Vector2.up, Vector2.one, new Vector2(0, -LABEL_HEIGHT), Vector2.zero);
            title.GetComponent<Lang_Button>().Constant = labelID;
            title.GetComponent<TextMeshProUGUI>().text = labelText;

            // The field is a white box under the label, the width of a dropdown's box, with the blue underline
            // along its bottom. What's typed is dark, so it doesn't read as another label.
            SetRectTransform((RectTransform)field.transform, Vector2.up, Vector2.up, new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT), new Vector2(BOX_WIDTH, -LABEL_HEIGHT), Vector2.up);
            field.GetComponent<Image>().color = Color.white;
            Transform textArea = field.transform.Find("Container");
            SetRectTransform((RectTransform)textArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Transform text = textArea.Find("Text");
            SetRectTransform((RectTransform)text, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, 0));
            TextMeshProUGUI textTMP = text.GetComponent<TextMeshProUGUI>();
            textTMP.fontSize = TEXT_SIZE;
            textTMP.color = mainScript.black32;
            textTMP.alignment = TextAlignmentOptions.Left;
            SetRectTransform((RectTransform)textArea.Find("Image"), Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 2));
            return title.GetComponent<TextMeshProUGUI>();
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
    }
}
