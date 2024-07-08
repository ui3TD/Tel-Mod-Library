using HarmonyLib;
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

        public static void Postfix()
        {
            if (mainScript.IsBlockingHotkeys())
            {
                return;
            }
            if (Input.GetKeyDown(KEY_IDOLS))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.idols);
            }
            else if (Input.GetKeyDown(KEY_STAFF))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.staff);
            }
            else if (Input.GetKeyDown(KEY_ACTIVITIES))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.activities);
            }
            else if (Input.GetKeyDown(KEY_SINGLES))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.singles);
            }
            else if (Input.GetKeyDown(KEY_MEDIA))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.media);
            }
            else if (Input.GetKeyDown(KEY_SE))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.specialEvents);
            }
            else if (Input.GetKeyDown(KEY_RESEARCH))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.research);
            }
            else if (Input.GetKeyDown(KEY_POLICIES))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.policies);
            }
        }
    }

}
