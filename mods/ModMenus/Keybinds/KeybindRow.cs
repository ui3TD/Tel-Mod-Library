using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        public partial class ModMenuItem
        {
            /// <summary>The key row waiting for a key, or null.</summary>
            internal static ModMenuItem listening;

            /// <summary>
            /// A key row's controls, set when it's built; null for every other item.
            /// </summary>
            public Button keybindBox;
            public TextMeshProUGUI keybindText;
            public Button keybindClear;
            public TextMeshProUGUI keybindClash;

            /// <summary>A key row's label, and the mod it's under, to name it when another row has its key.</summary>
            public string keybindLabel;
            public string keybindMod;

            /// <summary>The frame the row began waiting for a key: the click that began it isn't a click elsewhere.</summary>
            private int listenFrame;

            /// <summary>A modifier pressed while waiting, taken alone if it's let go before another key is pressed.</summary>
            private KeyCode pendingModifier;

            /// <summary>Whether the line naming the rows with the same key is shown.</summary>
            private bool clashShown;

            /// <summary>The row named in another row's clash line: its label, and its mod's name.</summary>
            public string KeybindTitle => string.IsNullOrEmpty(keybindMod) ? keybindLabel : keybindLabel + " (" + keybindMod + ")";

            /// <summary>
            /// The key saved for the row, in its saved form, or its default if none is saved or it isn't a key.
            /// </summary>
            public string SavedKeybind()
            {
                return NormalizeKeybind(variables.Get(varID)) ?? defText;
            }

            /// <summary>
            /// Shows a key and keeps it until it's changed; a row waiting for a key stops waiting.
            /// </summary>
            public void ShowKeybind(string value)
            {
                if (ReferenceEquals(listening, this))
                {
                    listening = null;
                }
                tempText = value;
                RenderKeybind();
            }

            /// <summary>
            /// Shows the row's key, or that it's waiting for one.
            /// </summary>
            public void RenderKeybind()
            {
                keybindText.text = ReferenceEquals(listening, this) ? ShownText(PRESS_A_KEY_LABEL) : KeybindName(tempText);
            }

            /// <summary>
            /// Clicking the box makes the row wait for a key; clicking it again stops it waiting.
            /// </summary>
            public void onClickKeybind()
            {
                Deselect();
                if (ReferenceEquals(listening, this))
                {
                    StopListening(false);
                    return;
                }
                if (listening is not null)
                {
                    listening.StopListening(false);
                }
                listening = this;
                listenFrame = Time.frameCount;
                pendingModifier = KeyCode.None;
                RenderKeybind();
            }

            /// <summary>
            /// The × beside the box: the row has no key.
            /// </summary>
            public void onClearKeybind()
            {
                Deselect();
                ShowKeybind(KEY_NONE);
                Changed();
            }

            /// <summary>
            /// Stops the row waiting for a key, if it is.
            /// </summary>
            /// <param name="suppress">Whether a key was just taken, which no row may react to until it's let go.</param>
            public void StopListening(bool suppress)
            {
                if (ReferenceEquals(listening, this))
                {
                    listening = null;
                }
                if (suppress)
                {
                    SuppressKeybindsUntilReleased();
                }
                RenderKeybind();
            }

            /// <summary>
            /// Stops any row waiting for a key: the menu is closing.
            /// </summary>
            public static void StopAnyListening()
            {
                if (listening is not null)
                {
                    listening.StopListening(false);
                }
            }

            /// <summary>
            /// While the row waits, takes the first key pressed, with the modifiers held; a modifier let go with no
            /// other key pressed is taken alone. A left click anywhere but the box stops it waiting.
            /// </summary>
            public void PollKeybind()
            {
                if (!ReferenceEquals(listening, this))
                {
                    return;
                }
                if (Input.GetKeyUp(KeyCode.Mouse0) && Time.frameCount != listenFrame)
                {
                    StopListening(false);
                    return;
                }
                foreach (KeyCode key in CaptureKeys)
                {
                    if (!Input.GetKeyDown(key))
                    {
                        continue;
                    }
                    if (IsModifierKey(key))
                    {
                        pendingModifier = key;
                        continue;
                    }
                    TakeKey(new Keybind(key, HeldModifiers()));
                    return;
                }
                if (pendingModifier != KeyCode.None && Input.GetKeyUp(pendingModifier))
                {
                    TakeKey(new Keybind(pendingModifier));
                }
            }

            private void TakeKey(Keybind keybind)
            {
                tempText = FormatKeybind(keybind);
                StopListening(true);
                Changed();
            }

            /// <summary>
            /// Shows the rows that have the same key under the box, or hides the line if there are none. The row
            /// grows by the line while it's shown, and by the lines it wraps onto.
            /// </summary>
            /// <param name="others">The other rows' names, or null.</param>
            public void ShowClash(string others)
            {
                if (keybindClash == null)
                {
                    return;
                }
                // A hidden line is empty, so its wrapping adds nothing to the row (see LabelWrap)
                string format = ShownText(ALSO_USED_BY_LABEL);
                keybindClash.text = others == null ? "" : format.Contains("{0}") ? format.Replace("{0}", others) : format + " " + others;
                if ((others != null) == clashShown)
                {
                    return;
                }
                clashShown = others != null;
                keybindClash.gameObject.SetActive(clashShown);
                GrowRow((RectTransform)transform, clashShown ? WARNING_HEIGHT : -WARNING_HEIGHT);
            }
        }
    }
}
