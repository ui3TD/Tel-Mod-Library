using HarmonyLib;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine;
using System.Reflection;
using TMPro;
using System.Collections.Generic;
using System;

namespace NationalTour
{

    // ovverride tab button click operation to render continue button
    [HarmonyPatch(typeof(SpecialEvents_Manager), "OpenTab_WorldTour")]
    public class SpecialEvents_Manager_OpenTab_WorldTour
    {
        public static bool Prefix()
        {
            GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;

            Image bgimg = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();

            bgimg.sprite = Utility.World_tour_def;

            return true;
        }
    }

    // override if opentab = 3 (national tour)
    //[HarmonyPatch(typeof(SpecialEvents_Manager), "ResetPopup_New")]
    //public class SpecialEvents_Manager_ResetPopup_New
    //{
    //    public static void Postfix(SpecialEvents_Manager __instance)
    //    {
    //        if (__instance.OpenTab == SEvent_Actions.nationalTourID)
    //        {
    //            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_tour_new).obj.GetComponent<Tour_New_Popup>().Reset();
    //        }
    //    }
    //}



    // Set create new image, if opened directly
    [HarmonyPatch(typeof(SpecialEvents_Manager), "OpenSpecialEventsPopup")]
    public class SpecialEvents_Manager_OpenSpecialEventsPopup
    {
        public static void Postfix(SpecialEvents_Manager __instance)
        {
            GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;

            if (__instance.OpenTab == SpecialEvents_Manager._type.WorldTour)
            {
                if (SEvent_Actions.nationalTab)
                {
                    Image bgimg = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();

                    bgimg.sprite = Utility.World_tour_def;
                }
                //else if (Utility.World_tour_def_orig != null)
                //{
                //    Image bgimg = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();

                //    bgimg.sprite = Utility.World_tour_def_orig;
                //}
            }

            //GameObject tabs = popupObj.transform.Find("Container").Find("Types").gameObject;

            //if (popupObj.transform.Find("Container").Find("Types").Find("National Tour") == null)
            //{
            //    GameObject tourButton = GameObject.Instantiate(popupObj.transform.Find("Container").Find("Types").Find("World Tour").gameObject);
            //    tourButton.name = "National Tour";
            //    tourButton.GetComponentInChildren<Lang_Button>().Constant = "SEVENT__NATIONAL_TOUR";
            //    tourButton.transform.SetParent(popupObj.transform.Find("Container").Find("Types"));
            //    tourButton.transform.position = popupObj.transform.Find("Container").Find("Types").Find("World Tour").transform.position;
            //    tourButton.transform.localScale = popupObj.transform.Find("Container").Find("Types").Find("World Tour").transform.localScale;

            //    UnityAction callOnClickNational = new(SEvent_Actions.OnClick_National);
            //    tourButton.GetComponent<Button>().onClick.AddListener(callOnClickNational);

            //    UnityAction callOnClickWorld = new(SEvent_Actions.OnClick_World);
            //    popupObj.transform.Find("Container").Find("Types").Find("World Tour").gameObject.GetComponent<Button>().onClick.AddListener(callOnClickWorld);
            //}

        }
    }

    // Load national tour event for cooldown and status tracking
    //[HarmonyPatch(typeof(SpecialEvents_Manager), "LoadDefaultEvents")]
    //public class SpecialEvents_Manager_LoadDefaultEvents
    //{
    //    public static void Postfix()
    //    {
    //        SpecialEvents_Manager._type type = SEvent_Actions.nationalTourID;
    //        SpecialEvents_Manager._specialEvent specialEvent = new()
    //        {
    //            Type = type,
    //            Status = SpecialEvents_Manager._specialEvent._status.normal
    //        };
    //        SpecialEvents_Manager.SpecialEvents.Add(specialEvent);
    //    }
    //}

    // Load national tour event for cooldown and status tracking
    //[HarmonyPatch(typeof(SpecialEvents_Manager._specialEvent), "SetCooldown")]
    //public class SpecialEvents_Manager__specialEvent_SetCooldown
    //{
    //    public static void Postfix(ref SpecialEvents_Manager._specialEvent __instance)
    //    {
    //        if(__instance.Type == SEvent_Actions.nationalTourID)
    //        {
    //            __instance.Cooldown_Stop = __instance.Cooldown_Start.AddMonths(3);
    //        }
    //    }
    //}

    // Load national tour event for cooldown and status tracking
    //[HarmonyPatch(typeof(SpecialEvents_Manager._specialEvent), "UpdateStatus")]
    //public class SpecialEvents_Manager__specialEvent_UpdateStatus
    //{
    //    public static List<int> listOfNationalTours = new(){ 0 };
    //    public static void Postfix(ref SpecialEvents_Manager._specialEvent __instance)
    //    {
    //        if (__instance.Type == SpecialEvents_Manager._type.WorldTour)
    //        {
    //            bool flag = false;
    //            foreach (SEvent_Tour.tour _tour in SEvent_Tour.Tours)
    //            {
    //                if (_tour.Status != SEvent_Tour.tour._status.finished && !listOfNationalTours.Contains(_tour.ID))
    //                {
    //                    flag = true;
    //                }
    //            }
    //            if (flag)
    //            {
    //                __instance.Status = SpecialEvents_Manager._specialEvent._status.production;
    //            }
    //            else
    //            {
    //                __instance.Status = SpecialEvents_Manager._specialEvent._status.normal;
    //            }
    //        }
    //        if (__instance.Type == SEvent_Actions.nationalTourID)
    //        {
    //            bool flag = false;
    //            foreach (SEvent_Tour.tour _tour in SEvent_Tour.Tours)
    //            {
    //                if (_tour.Status != SEvent_Tour.tour._status.finished && listOfNationalTours.Contains(_tour.ID))
    //                {
    //                    flag = true;
    //                }
    //            }
    //            if (flag)
    //            {
    //                __instance.Status = SpecialEvents_Manager._specialEvent._status.production;
    //            }
    //            else
    //            {
    //                __instance.Status = SpecialEvents_Manager._specialEvent._status.normal;
    //            }
    //        }
    //    }
    //}

    // ovverride tab button click operation to render continue button
    //[HarmonyPatch(typeof(SpecialEvents_Manager), "GetEvent")]
    //public class SpecialEvents_Manager_GetEvent
    //{
    //    public static void Postfix(ref SpecialEvents_Manager._specialEvent __result, SpecialEvents_Manager._type type)
    //    {
    //        if(__result == null && type == SEvent_Actions.nationalTourID)
    //        {
    //            SpecialEvents_Manager._specialEvent specialEvent = new()
    //            {
    //                Type = type,
    //                Status = SpecialEvents_Manager._specialEvent._status.normal
    //            };
    //            SpecialEvents_Manager.SpecialEvents.Add(specialEvent);

    //            __result = SpecialEvents_Manager.SpecialEvents[SpecialEvents_Manager.SpecialEvents.Count - 1];
    //        }
    //    }
    //}


    public class SEvent_Actions
    {

        public static bool nationalTab = true;

        //public static SpecialEvents_Manager._type nationalTourID = (SpecialEvents_Manager._type)30;

        //public static void OnClick_National()
        //{
        //    nationalTab = true;

        //    GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;

        //    Image image = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();
        //    image.sprite = Utility.World_tour_def;

        //    GameObject desc = popupObj.transform.Find("Container").Find("World Tour").Find("Panel").Find("Text").gameObject;
        //    desc.GetComponent<Lang_Button>().Constant = Language.Data["SEVENT__NATIONAL_TOUR_DESC"];
        //    desc.GetComponent<TextMeshProUGUI>().text = Language.Data["SEVENT__NATIONAL_TOUR_DESC"];

        //    SpecialEvents_Manager specialEvents_Manager = Camera.main.GetComponent<mainScript>().Data.GetComponent<SpecialEvents_Manager>();
        //    specialEvents_Manager.OpenTab = SEvent_Actions.nationalTourID;

        //    var renderContinueButton = specialEvents_Manager.GetType().GetMethod("RenderContinueButton", BindingFlags.NonPublic | BindingFlags.Instance);
        //    renderContinueButton.Invoke(specialEvents_Manager, null);
        //}
        //public static void OnClick_World()
        //{
        //    nationalTab = false;

        //    GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;

        //    Image image = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();

        //    image.sprite = Utility.World_tour_def_orig;

        //    GameObject desc = popupObj.transform.Find("Container").Find("World Tour").Find("Panel").Find("Text").gameObject;
        //    desc.GetComponent<Lang_Button>().Constant = Language.Data["SEVENT__WORLD_TOUR_DESC"];
        //    desc.GetComponent<TextMeshProUGUI>().text = Language.Data["SEVENT__WORLD_TOUR_DESC"];

        //    SpecialEvents_Manager specialEvents_Manager = Camera.main.GetComponent<mainScript>().Data.GetComponent<SpecialEvents_Manager>();
        //    specialEvents_Manager.OpenTab = SpecialEvents_Manager._type.WorldTour;

        //    var renderContinueButton = specialEvents_Manager.GetType().GetMethod("RenderContinueButton", BindingFlags.NonPublic | BindingFlags.Instance);
        //    renderContinueButton.Invoke(specialEvents_Manager, null);
        //}
    }
}
