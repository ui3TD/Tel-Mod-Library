using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace MenuHotkeys
{

    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
    public class Controls_Update
    {
        public const KeyCode KEY_IDOLS = KeyCode.A;
        public const KeyCode KEY_STAFF = KeyCode.S;
        public const KeyCode KEY_ACTIVITIES = KeyCode.D;
        public const KeyCode KEY_SINGLES = KeyCode.F;
        public const KeyCode KEY_MEDIA = KeyCode.G;
        public const KeyCode KEY_SE = KeyCode.H;
        public const KeyCode KEY_RESEARCH = KeyCode.J;
        public const KeyCode KEY_POLICIES = KeyCode.K;

        /// <summary>
        /// A key: its key row in ModMenus (the player can change it there), its default, and the tab it opens.
        /// </summary>
        public class Hotkey
        {
            public string VarID;
            public KeyCode Key;
            public Tabs_Manager._tab._type Tab;

            public Hotkey(string varID, KeyCode key, Tabs_Manager._tab._type tab)
            {
                VarID = varID;
                Key = key;
                Tab = tab;
            }
        }

        // Checked in this order: one tab per frame, the first key down wins
        public static readonly Hotkey[] Hotkeys =
        {
            new("MenuHotkeys_Idols", KEY_IDOLS, Tabs_Manager._tab._type.idols),
            new("MenuHotkeys_Staff", KEY_STAFF, Tabs_Manager._tab._type.staff),
            new("MenuHotkeys_Activities", KEY_ACTIVITIES, Tabs_Manager._tab._type.activities),
            new("MenuHotkeys_Singles", KEY_SINGLES, Tabs_Manager._tab._type.singles),
            new("MenuHotkeys_Media", KEY_MEDIA, Tabs_Manager._tab._type.media),
            new("MenuHotkeys_SpecialEvents", KEY_SE, Tabs_Manager._tab._type.specialEvents),
            new("MenuHotkeys_Research", KEY_RESEARCH, Tabs_Manager._tab._type.research),
            new("MenuHotkeys_Policies", KEY_POLICIES, Tabs_Manager._tab._type.policies),
        };

        public static void Postfix()
        {
            if (mainScript.IsBlockingHotkeys())
            {
                return;
            }
            foreach (Hotkey hotkey in Hotkeys)
            {
                if (ModMenusKeys.Pressed(hotkey.VarID, hotkey.Key))
                {
                    Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(hotkey.Tab);
                    return;
                }
            }
        }
    }

}
