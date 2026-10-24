namespace ModMenus
{
    /// <summary>
    /// Utility class containing methods for creating and managing mod menu elements.
    /// </summary>
    partial class ModMenusUtils
    {
        /// <summary>
        /// The popup type the mod menu is registered under, past the game's own popup types, unless another mod
        /// already uses it (see MenuPopupType). Other mods adding popups should use a different number.
        /// </summary>
        public const PopupManager._type MOD_MENU_POPUP = (PopupManager._type)999;

        public const string BUTTON_LABEL = "MODMENU__BUTTON_LABEL";
        public const string APPLY_LABEL = "APPLY";
        public const string CANCEL_LABEL = "CANCEL";
        public const string RESET_LABEL = "MODMENU__RESET";

        public const string NO_SETTINGS_LABEL = "MODMENU__NO_SETTINGS";

        // A tab's "Use in all saves" box, the note under it while it's ticked, and the line about secrets
        // on a tab that has one, whether the box is ticked or not
        public const string EVERY_SAVE_LABEL = "MODMENU__EVERY_SAVE";
        public const string EVERY_SAVE_NOTE = "MODMENU__EVERY_SAVE_NOTE";
        public const string EVERY_SAVE_SECRET_NOTE = "MODMENU__EVERY_SAVE_SECRET_NOTE";

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
        public const string SCOPE_ROW_OBJ_NAME = "ModMenuEverySave";
        public const string SCOPE_NOTE_OBJ_NAME = "ModMenuEverySaveNote";
        public const string SCOPE_SECRET_NOTE_OBJ_NAME = "ModMenuEverySaveSecretNote";
        public const string SCOPE_LINE_OBJ_NAME = "ModMenuEverySaveLine";
    }
}
