using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace MenuHotkeys
{

    [HarmonyPatch(typeof(Controls), "Update")]
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

        // Each key and the tab it opens, checked in this order: one tab per frame, the first key down wins
        private static readonly KeyValuePair<KeyCode, Tabs_Manager._tab._type>[] Hotkeys =
        {
            new(KEY_IDOLS, Tabs_Manager._tab._type.idols),
            new(KEY_STAFF, Tabs_Manager._tab._type.staff),
            new(KEY_ACTIVITIES, Tabs_Manager._tab._type.activities),
            new(KEY_SINGLES, Tabs_Manager._tab._type.singles),
            new(KEY_MEDIA, Tabs_Manager._tab._type.media),
            new(KEY_SE, Tabs_Manager._tab._type.specialEvents),
            new(KEY_RESEARCH, Tabs_Manager._tab._type.research),
            new(KEY_POLICIES, Tabs_Manager._tab._type.policies),
        };

        public static void Postfix()
        {
            if (mainScript.IsBlockingHotkeys())
            {
                return;
            }
            foreach (KeyValuePair<KeyCode, Tabs_Manager._tab._type> hotkey in Hotkeys)
            {
                if (Input.GetKeyDown(hotkey.Key))
                {
                    Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(hotkey.Value);
                    return;
                }
            }
        }
    }

}
