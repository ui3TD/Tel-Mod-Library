using TMPro;
using UnityEngine.UI;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        public partial class ModMenuItem
        {
            /// <summary>
            /// A secret row's ID (see SecretStore) and its controls, set when it's built; null for every other item.
            /// A secret row has no varID: nothing of it goes through the game's variables.
            /// </summary>
            public string secretID;
            public TMP_InputField secretField;
            public TextMeshProUGUI secretHint;
            public Button secretButton;
            public TextMeshProUGUI secretButtonText;
            public TextMeshProUGUI secretStatus;

            /// <summary>What's typed in a secret's field, which Apply saves if it isn't blank.</summary>
            public string secretTyped = "";

            /// <summary>Whether Remove was pressed, so Apply removes the saved secret.</summary>
            public bool secretRemoving;

            /// <summary>
            /// Empties the field and forgets a pending removal: the menu opened.
            /// </summary>
            public void ShowSecret()
            {
                secretTyped = "";
                secretRemoving = false;
                secretField.SetTextWithoutNotify("");
                RenderSecret();
            }

            /// <summary>
            /// Shows what's saved, the hint that fits it, and Remove or Undo while a secret is saved.
            /// </summary>
            public void RenderSecret()
            {
                string saved = SecretStore.Get(secretID);
                secretStatus.text = SecretStatus(saved, secretRemoving);
                secretHint.text = ShownText(saved != null && !secretRemoving ? SECRET_HINT_REPLACE_LABEL : SECRET_HINT_NEW_LABEL);
                if (secretButton != null)
                {
                    secretButtonText.text = ShownText(secretRemoving ? SECRET_UNDO_LABEL : SECRET_REMOVE_LABEL);
                    secretButton.gameObject.SetActive(saved != null);
                }
            }

            /// <summary>
            /// Typing in a secret's field. Typing a new secret takes back a pending removal: Apply saves the new one.
            /// </summary>
            public void onTypeSecret(string val)
            {
                secretTyped = val ?? "";
                if (secretRemoving && secretTyped.Trim() != "")
                {
                    secretRemoving = false;
                    RenderSecret();
                }
            }

            /// <summary>
            /// Remove: Apply removes the saved secret, and the field is emptied. Undo: it's kept after all.
            /// </summary>
            public void onClickSecretButton()
            {
                Deselect();
                secretRemoving = !secretRemoving;
                if (secretRemoving)
                {
                    secretTyped = "";
                    secretField.SetTextWithoutNotify("");
                }
                RenderSecret();
            }

            /// <summary>
            /// Saves what Apply should: a typed secret replaces the saved one; with nothing typed, a pending
            /// removal removes it; otherwise nothing changes. Returns whether the saved secret changed.
            /// </summary>
            public bool ApplySecret()
            {
                string typed = (secretTyped ?? "").Trim();
                string saved = SecretStore.Get(secretID);
                if (typed != "")
                {
                    return typed != saved && SecretStore.Set(secretID, typed);
                }
                if (secretRemoving && saved != null)
                {
                    return SecretStore.Delete(secretID);
                }
                return false;
            }
        }
    }
}
