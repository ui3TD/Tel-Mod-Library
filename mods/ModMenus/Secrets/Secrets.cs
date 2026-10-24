using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModMenus
{
    /// <summary>
    /// Secret rows ("type": "secret"), for API keys and other secrets. The field is write-only: it always
    /// opens empty, and the saved secret is never shown again. A line under it says whether one is saved, with
    /// its last 4 characters if it's long enough, and a Remove button removes it on Apply. Secrets are kept
    /// in their own files (see SecretStore), never in a save or in the settings used in all saves, and Reset
    /// leaves them alone. Mods read them with GetSecret.
    /// </summary>
    partial class ModMenusUtils
    {
        public const string JSON_TYPE_SECRET = "secret";
        public const string JSON_FIELD_SECRETID = "secretID";

        public const string MENU_SECRET_OBJ_NAME = "ModMenuSecret";
        public const string SECRET_FIELD_OBJ_NAME = "Field";
        public const string SECRET_HINT_OBJ_NAME = "Hint";
        public const string SECRET_BUTTON_OBJ_NAME = "Remove";
        public const string SECRET_STATUS_OBJ_NAME = "Status";

        public const string SECRET_HINT_NEW_LABEL = "MODMENU__SECRET_HINT_NEW";
        public const string SECRET_HINT_REPLACE_LABEL = "MODMENU__SECRET_HINT_REPLACE";
        public const string SECRET_REMOVE_LABEL = "MODMENU__SECRET_REMOVE";
        public const string SECRET_UNDO_LABEL = "MODMENU__SECRET_UNDO";
        public const string SECRET_NONE_LABEL = "MODMENU__SECRET_NONE";
        public const string SECRET_SAVED_LABEL = "MODMENU__SECRET_SAVED";
        public const string SECRET_SAVED_ENDING_LABEL = "MODMENU__SECRET_SAVED_ENDING";
        public const string SECRET_REMOVING_LABEL = "MODMENU__SECRET_REMOVING";

        /// <summary>
        /// The shortest secret whose last characters the row shows: 4 of 16 leaves 12 hidden. API keys are far
        /// longer; a short secret, such as a PIN, shows only that one is saved, since its end is most of it.
        /// </summary>
        public const int SECRET_ENDING_MIN_LENGTH = 16;
        public const int SECRET_ENDING_LENGTH = 4;

        // The Remove button beside the box
        public const float SECRET_BUTTON_WIDTH = 90;

        /// <summary>
        /// Raised after an Apply that changed any secret, with the IDs of those that changed.
        /// </summary>
        public static event Action<string[]> SecretsChanged;

        /// <summary>
        /// A secret row's saved value, or null when none is saved (or the ID isn't valid). Read from its file,
        /// which is read again only when it changes, so this can be called whenever the secret is needed.
        /// </summary>
        public static string GetSecret(string secretID) => SecretStore.Get(secretID);

        /// <summary>
        /// Whether a secret row has a saved value.
        /// </summary>
        public static bool HasSecret(string secretID) => SecretStore.Get(secretID) != null;

        /// <summary>
        /// What the line under a secret's field says: that it will be removed on Apply, that none is saved, or
        /// that one is saved, ending in its last 4 characters if it has at least SECRET_ENDING_MIN_LENGTH.
        /// </summary>
        public static string SecretStatus(string saved, bool removing)
        {
            if (removing)
            {
                return ShownText(SECRET_REMOVING_LABEL);
            }
            if (saved == null)
            {
                return ShownText(SECRET_NONE_LABEL);
            }
            if (saved.Length < SECRET_ENDING_MIN_LENGTH)
            {
                return ShownText(SECRET_SAVED_LABEL);
            }
            // The ending is shown as written, even if it looks like a text tag
            string ending = "<noparse>" + saved.Substring(saved.Length - SECRET_ENDING_LENGTH) + "</noparse>";
            string format = ShownText(SECRET_SAVED_ENDING_LABEL);
            return format.Contains("{0}") ? format.Replace("{0}", ending) : format + " " + ending;
        }

        /// <summary>
        /// Whether a menu has a secret row the menu shows, which "Use in all saves" never shares.
        /// </summary>
        public static bool HasSecretField(SimpleJSON.JSONArray items)
        {
            // Not TryGetDefault: reading a missing list adds an empty one to the item, and the row is built from it after
            for (int i = 0; i < items.Count; i++)
            {
                SimpleJSON.JSONNode item = items[i];
                if ((string)item[JSON_FIELD_TYPE] == JSON_TYPE_SECRET && !string.IsNullOrEmpty(item[JSON_FIELD_LABELID])
                    && SecretStore.IsValidID(item[JSON_FIELD_SECRETID]) && !item[JSON_FIELD_IGNORE].AsBool)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Calls every SecretsChanged handler; one that fails is logged and the others still run.
        /// </summary>
        internal static void RaiseSecretsChanged(IEnumerable<string> secretIDs)
        {
            string[] ids = secretIDs.Distinct(StringComparer.Ordinal).ToArray();
            if (ids.Length == 0 || SecretsChanged == null)
            {
                return;
            }
            foreach (Delegate handler in SecretsChanged.GetInvocationList())
            {
                try
                {
                    ((Action<string[]>)handler)(ids);
                }
                catch (Exception e)
                {
                    Debug.LogError("[ModMenus] A mod's SecretsChanged handler failed: " + e);
                }
            }
        }

        /// <summary>
        /// Adds a secret row: its label, a masked field that always opens empty with a hint in it, a Remove
        /// button beside it while a secret is saved, and a line under them saying what's saved.
        /// </summary>
        /// <param name="secretID">Where the secret is kept (see SecretStore.IsValidID).</param>
        /// <param name="labelID">The label's text ID.</param>
        /// <param name="parentTransform">Where the row goes.</param>
        public static GameObject AddMenuSecret(string secretID, string labelID, Transform parentTransform)
        {
            string labelText = ShownText(labelID);

            GameObject row = new(SecretRowName(secretID), typeof(RectTransform));
            row.transform.SetParent(parentTransform, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            SetRectTransform(rowRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rowRect.sizeDelta = new Vector2(400f, LABEL_HEIGHT + BOX_HEIGHT + WARNING_HEIGHT);

            GameObject field = UnityEngine.Object.Instantiate(VanillaTextField(), row.transform);
            field.name = SECRET_FIELD_OBJ_NAME;
            // Tab would jump to the rival's last name field
            UnityEngine.Object.DestroyImmediate(field.GetComponent<InputField_Tab>());
            TextMeshProUGUI title = PlaceTextBox(row, field, labelID, labelText);

            ModMenuItem item = row.AddComponent<ModMenuItem>();
            item.secretID = secretID;

            // Vanilla's would rename the rival
            TMP_InputField input = field.GetComponent<TMP_InputField>();
            input.characterLimit = 0;
            input.contentType = TMP_InputField.ContentType.Password;
            input.onValueChanged = new TMP_InputField.OnChangeEvent();
            input.onValueChanged.AddListener(item.onTypeSecret);
            item.secretField = input;

            // The hint shows while nothing is typed; it takes the place of any the game's field has
            Transform container = field.transform.Find("Container");
            GameObject hint = AddMenuText(SECRET_HINT_NEW_LABEL, container, TEXT_SIZE, HintColor(), TextAlignmentOptions.Left);
            hint.name = SECRET_HINT_OBJ_NAME;
            SetRectTransform((RectTransform)hint.transform, Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, 0));
            TextMeshProUGUI hintText = hint.GetComponent<TextMeshProUGUI>();
            hintText.raycastTarget = false;
            if (input.placeholder != null)
            {
                input.placeholder.gameObject.SetActive(false);
            }
            input.placeholder = hintText;
            item.secretHint = hintText;

            // Remove: a copy of the game's blue button, as tall as the box beside it. Without the game's button
            // (another mod changed the menu) the row has none, and the secret can still be replaced.
            RectTransform buttonRect = null;
            if (buttonTemplate != null)
            {
                GameObject button = AddRowButton(SECRET_BUTTON_OBJ_NAME, SECRET_REMOVE_LABEL, row.transform, out TextMeshProUGUI buttonText);
                buttonRect = (RectTransform)button.transform;
                SetRectTransform(buttonRect, Vector2.up, Vector2.up, new Vector2(BOX_WIDTH + CLEAR_GAP, -LABEL_HEIGHT - BOX_HEIGHT), new Vector2(BOX_WIDTH + CLEAR_GAP + SECRET_BUTTON_WIDTH, -LABEL_HEIGHT), Vector2.up);
                Button removeButton = button.GetComponent<Button>();
                removeButton.onClick.AddListener(item.onClickSecretButton);
                item.secretButton = removeButton;
                item.secretButtonText = buttonText;
            }
            else
            {
                Debug.LogWarning("[ModMenus] The menu has no Reset to Defaults button to copy, so the secret row " + secretID + " has no Remove button.");
            }

            // What's saved, under the box
            GameObject status = AddMenuText(SECRET_NONE_LABEL, row.transform, TEXT_SIZE, mainScript.blue32, TextAlignmentOptions.Left);
            status.name = SECRET_STATUS_OBJ_NAME;
            SetRectTransform((RectTransform)status.transform, Vector2.up, Vector2.one, new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT - WARNING_HEIGHT), new Vector2(0, -LABEL_HEIGHT - BOX_HEIGHT));
            TextMeshProUGUI statusText = status.GetComponent<TextMeshProUGUI>();
            statusText.raycastTarget = false;
            item.secretStatus = statusText;

            item.ShowSecret();
            AddLabelWrap(row, title, null, buttonRect == null
                ? new[] { (RectTransform)field.transform, (RectTransform)status.transform }
                : new[] { (RectTransform)field.transform, buttonRect, (RectTransform)status.transform });
            return row;
        }

        /// <summary>
        /// The game's button that rows copy for their own: the menu's Reset to Defaults, the game's blue button.
        /// Set when the menu is built, before its rows.
        /// </summary>
        internal static GameObject buttonTemplate;

        /// <summary>
        /// A button for a row: a copy of the game's blue button (see buttonTemplate), so it looks and acts like
        /// the game's, with its label at the menu's text size. Its label changes with what it does, so it doesn't
        /// follow the game's language changes. Its click does nothing until the caller adds what it does.
        /// </summary>
        private static GameObject AddRowButton(string name, string labelID, Transform parent, out TextMeshProUGUI label)
        {
            GameObject button = CloneButton(buttonTemplate, parent, name, labelID);
            Transform text = button.transform.Find("Text");
            UnityEngine.Object.DestroyImmediate(text.GetComponent<Lang_Button>());
            SetRectTransform((RectTransform)text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label = text.GetComponent<TextMeshProUGUI>();
            label.enableAutoSizing = false;
            label.fontSize = TEXT_SIZE;
            label.raycastTarget = false;
            return button;
        }

        /// <summary>
        /// A secret row's object name. Unity's Transform.Find reads '/' as a path, so a folder's '/' is
        /// written as '\'.
        /// </summary>
        public static string SecretRowName(string secretID) => MENU_SECRET_OBJ_NAME + "_" + secretID.Replace('/', '\\');

        /// <summary>
        /// The hint's colour: the typed text's, faded, so it doesn't read as typed.
        /// </summary>
        private static Color HintColor()
        {
            Color32 dark = mainScript.black32;
            return new Color32(dark.r, dark.g, dark.b, 120);
        }
    }
}
