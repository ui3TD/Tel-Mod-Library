using HarmonyLib;
using UnityEngine;
using System;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace MenuHotkeys
{

    [HarmonyPatch(typeof(Controls), "Update")]
    public class Controls_Update
    {

        public static void Postfix()
        {
            if (mainScript.IsBlockingHotkeys())
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.A))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.idols);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.staff);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.activities);
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.singles);
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.media);
            }
            else if (Input.GetKeyDown(KeyCode.H))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.specialEvents);
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.research);
            }
            else if (Input.GetKeyDown(KeyCode.K))
            {
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().OpenTab(Tabs_Manager._tab._type.policies);
            }
        }
    }

}
