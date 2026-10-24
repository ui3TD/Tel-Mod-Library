using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ModMenus
{
    partial class ModMenusUtils
    {
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
