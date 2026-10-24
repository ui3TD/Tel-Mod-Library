using UnityEngine;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// A row with enabledIf: it's shown only while that setting is on (see ModMenuManager.RefreshConditions).
        /// </summary>
        public class ModMenuCondition : MonoBehaviour
        {
            /// <summary>The setting the row depends on.</summary>
            public string varID;
        }
    }
}
