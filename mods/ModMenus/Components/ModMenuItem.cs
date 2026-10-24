using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.ModernUIPack;
using System.Collections.Generic;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Represents a single item in the mod menu. Its controls are set when it's built, as its page may be
        /// hidden; nothing happens when its page shows, so changes not yet applied stay as they are.
        /// </summary>
        public partial class ModMenuItem : MonoBehaviour
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
                if (keybindBox != null)
                {
                    ShowKeybind(SavedKeybind());
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
                if (keybindBox != null)
                {
                    ShowKeybind(defText);
                }
            }

            /// <summary>
            /// What Apply saves: a text field's text, a key's name (see FormatKeybind), or the other items' number.
            /// </summary>
            public string CurrentValue => tempText ?? FormatSetting(tempValue);

            /// <summary>
            /// A dropdown's list that opens past the bottom of the screen moves up (see ListShift), and back when it closes.
            /// It's moved every frame while open: the game's dropdown animates its list back into place each frame.
            /// </summary>
            public void LateUpdate()
            {
                if (keybindBox != null)
                {
                    PollKeybind();
                    return;
                }
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
            /// A text field's text or a key's name, which Apply saves, and its default. Null for every other item.
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

            /// <summary>Whether the row has "password": true, so it's never in the settings used in all saves.</summary>
            public bool password;

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
    }
}
