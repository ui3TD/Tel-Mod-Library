using UnityEngine;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// A tab's "Use in all saves" box (see AddScopeRow). While it's ticked, the note under it shows. Like a
        /// setting, a click is kept by Apply and thrown away by Cancel; unlike one, Reset leaves it alone and it's
        /// kept in SharedSettings, not in the save.
        /// </summary>
        public class ModMenuScopeRow : MonoBehaviour
        {
            /// <summary>The tab, as SharedSettings keeps it: its mod's title, the same on every computer.</summary>
            public string tabID;

            /// <summary>The tab's page, whose settings are shared while the box is ticked.</summary>
            public Transform page;

            public Checkbox_Text checkboxText;
            public GameObject note;

            /// <summary>Whether the box is ticked in the menu, applied or not.</summary>
            public bool ticked;

            public void Show(bool value)
            {
                ticked = value;
                checkboxText.SetCheck(value);
                note.SetActive(value);
            }

            public void OnClick()
            {
                Show(!ticked);
            }
        }
    }
}
