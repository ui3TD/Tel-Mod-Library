using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// The new tour popup in two modes. World mode is the game's own map and pins. National mode, opened from the
    /// National Tour tab, shows the map of Japan and one pin per prefecture, copied from the game's pins.
    /// </summary>
    public static class NewTourMap
    {
        public const string PIN_NAME_PREFIX = "Prefecture ";

        /// <summary>
        /// The mode the popup was last reset in.
        /// </summary>
        public static bool NationalMode;

        private static bool openingNational;

        // What the mod changed, so world mode puts back only that
        private static Sprite gameMap;
        private static bool mapSwapped;
        private static readonly List<GameObject> hiddenGamePins = new();

        /// <summary>
        /// Resets the popup in national mode. Every other reset (the World Tour tab, the event list) is world mode.
        /// </summary>
        public static void OpenNational(Tour_New_Popup popup)
        {
            openingNational = true;
            try
            {
                popup.Reset();
            }
            finally
            {
                openingNational = false;
            }
        }

        /// <summary>
        /// After the game's reset (a new empty tour, and its pins updated): show the pins and map for the mode.
        /// </summary>
        public static void OnReset(Tour_New_Popup popup)
        {
            bool national = openingNational;
            NationalMode = national;

            Image map = popup.transform.Find("Panel")?.Find("BG")?.GetComponent<Image>();
            if (national && (map == null || popup.CountriesContainer == null))
            {
                Readiness.Fail(Module.NewTourMap, "the new tour popup has no map or pins");
                NationalMode = false;
                return;
            }

            if (national)
                ShowNational(popup, map);
            else
                ShowWorld(popup, map);

            // Continue follows the mode
            popup.Render();
        }

        private static void ShowNational(Tour_New_Popup popup, Image map)
        {
            if (!mapSwapped)
                gameMap = map.sprite;
            map.sprite = Pictures.TOUR_map_2;
            mapSwapped = true;

            List<Tour_Country> nationalPins = new();
            foreach (Tour_Country pin in popup.CountriesContainer.GetComponentsInChildren<Tour_Country>(true))
            {
                if (Prefectures.IsPrefecture(pin.CountryType))
                    nationalPins.Add(pin);
                else if (pin.gameObject.activeSelf)
                {
                    pin.gameObject.SetActive(false);
                    hiddenGamePins.Add(pin.gameObject);
                }
            }
            if (nationalPins.Count == 0)
                nationalPins = AddPins(popup);

            foreach (Tour_Country pin in nationalPins)
            {
                Prefectures.Prefecture prefecture = (Prefectures.Prefecture)(int)pin.CountryType;
                pin.gameObject.SetActive(true);
                // In the popup's own units, so the pins land the same at any popup scale: the National Tour tab's
                // Continue resets this popup while it can still be at 0.5 from closing
                pin.transform.position = popup.transform.TransformPoint(Prefectures.MapPosition[prefecture] * Prefectures.POPUP_UNITS_PER_MAP_UNIT);
                pin.UpdateData();
            }
        }

        /// <summary>
        /// One pin per prefecture, copied from the game's first pin.
        /// </summary>
        private static List<Tour_Country> AddPins(Tour_New_Popup popup)
        {
            List<Tour_Country> pins = new();
            Tour_Country template = null;
            foreach (Tour_Country pin in popup.CountriesContainer.GetComponentsInChildren<Tour_Country>(true))
            {
                if (!Prefectures.IsPrefecture(pin.CountryType))
                {
                    template = pin;
                    break;
                }
            }
            if (template == null)
                return pins;

            foreach (SEvent_Tour.country prefecture in Prefectures.All)
            {
                GameObject copy = Object.Instantiate(template.gameObject, popup.CountriesContainer.transform, false);
                copy.name = PIN_NAME_PREFIX + prefecture.Type;
                Tour_Country pin = copy.GetComponent<Tour_Country>();
                pin.CountryType = prefecture.Type;
                pins.Add(pin);
            }
            return pins;
        }

        private static void ShowWorld(Tour_New_Popup popup, Image map)
        {
            if (mapSwapped && map != null)
                map.sprite = gameMap;
            mapSwapped = false;

            foreach (GameObject pin in hiddenGamePins)
            {
                if (pin == null)
                    continue;
                pin.SetActive(true);
                // The game's reset skipped it while it was hidden
                pin.GetComponent<Tour_Country>().UpdateData();
            }
            hiddenGamePins.Clear();

            if (popup.CountriesContainer == null)
                return;
            foreach (Tour_Country pin in popup.CountriesContainer.GetComponentsInChildren<Tour_Country>(true))
            {
                if (Prefectures.IsPrefecture(pin.CountryType))
                    pin.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// For tests: a new scene.
        /// </summary>
        internal static void Reset()
        {
            NationalMode = false;
            openingNational = false;
            gameMap = null;
            mapSwapped = false;
            hiddenGamePins.Clear();
        }
    }


    [HarmonyPatch(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Reset))]
    public class Tour_New_Popup_Reset
    {
        public static void Postfix(Tour_New_Popup __instance) => NewTourMap.OnReset(__instance);
    }


    // A national tour can be planned but not created until national tours have their own slot: Continue stays greyed
    [HarmonyPatch(typeof(Tour_New_Popup), nameof(Tour_New_Popup.Render))]
    public class Tour_New_Popup_Render
    {
        public static void Postfix(Tour_New_Popup __instance)
        {
            if (NewTourMap.NationalMode && !Readiness.IsReady(Module.Routing))
                __instance.Continue.GetComponent<ButtonDefault>().Activate(false);
        }
    }
}
