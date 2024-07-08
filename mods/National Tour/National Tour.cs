using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using SimpleJSON;
using System;
using static NationalTour.Utility;

namespace NationalTour
{

    // Set locations
    [HarmonyPatch(typeof(Tour_New_Popup), "Reset")]
    public class Tour_New_Popup_Reset
    {
        public static void Postfix(ref Tour_New_Popup __instance)
        {
            Image image = __instance.transform.Find("Panel").Find("BG").GetComponent<Image>();
            image.sprite = TOUR_map_2;

            Tour_Country[] componentsInChildren = __instance.CountriesContainer.transform.GetComponentsInChildren<Tour_Country>();

            for (int i = 0; i < tourLocations.Count; i++)
            {
                componentsInChildren[i].CountryType = (SEvent_Tour._country)tourLocations[componentsInChildren[i].CountryType];
                componentsInChildren[i].UpdateData();

                componentsInChildren[i].transform.position = locationDict.TryGetValue((Prefectures)componentsInChildren[i].CountryType, out Vector3 v) ? v : componentsInChildren[i].transform.position;
            }
        }
    }


    // Set tour finish images
    [HarmonyPatch(typeof(Tour_Popup), "Reset")]
    public class Tour_Popup_Reset
    {
        public static void Postfix()
        {
            PopupManager popupManager = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>();
            GameObject BGImage = popupManager.BGImage;

            // Set the BGImage transition, and do a slight fade-in and zoom-in effect
            BGImage.GetComponent<RawImage>().texture = World_tour_def_tex;
            BGImage.GetComponent<CanvasGroup>().alpha = 0f;
            BGImage.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 0);
            TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOFade(BGImage.GetComponent<CanvasGroup>(), 1f, 0.5f), Ease.OutQuint);
            TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOSizeDelta(BGImage.GetComponent<RectTransform>(), new Vector2(1074,20), 4.5f), Ease.OutQuint);

            // Set the blurred BG image for the results
            GameObject imgobj = popupManager.GetByType(PopupManager._type.sevent_tour).obj.transform.Find("BG").gameObject;
            Image image = imgobj.GetComponent<Image>();
            image.color = new Color(1,1,1,1f);
            image.sprite = World_tour_def_BG;
        }
    }

    // Load countries
    [HarmonyPatch(typeof(SEvent_Tour.country), "Set")]
    public class SEvent_Tour_country_Set
    {
        public static bool Prefix(SEvent_Tour.country __instance, JSONNode data)
        {
            if(Enum.TryParse(data["type"], true, out Prefectures result))
            {
                __instance.Type = (SEvent_Tour._country)result;

                var GetArea = typeof(SEvent_Tour).GetMethod("GetArea", BindingFlags.NonPublic | BindingFlags.Static);
                __instance.Area = (SEvent_Tour._area)GetArea.Invoke(null, new object[] { (SEvent_Tour._areaType)Enum.Parse(typeof(SEvent_Tour._areaType), data["area"]) });
                __instance.Cost = data["cost"].AsInt;
                __instance.TicketPrice = data["ticketPrice"].AsInt;
                if (data["level"] == null)
                {
                    __instance.Level = 1;
                    return false;
                }
                __instance.Level = data["level"].AsInt;
                return false;
            }
            return true;
        }
    }


    // Load assets
    [HarmonyPatch(typeof(mainScript), "Start")]
    public class mainScript_Start
    {
        public static void Postfix()
        {
            if (mainScript.IsMainMenu())
                return;

            Mods._mod thisMod = null;
            foreach (Mods._mod mod in Mods._Mods)
            {
                if (mod.Title == MOD_TITLE)
                {
                    thisMod = mod;
                    break;
                }
            }

            string imageDir = Path.Combine(thisMod.GetPath(), "Textures", MOD_TEXTURE_DIR);
            string path_World_tour_def = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, TOUR_POPUP_BG_FILE));
            string path_World_tour_def_BG = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, TOUR_POPUP_BG_BLURRED_FILE));
            string path_TOUR_map_2 = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, TOUR_MAP_FILE));

            World_tour_def = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def, 100f);
            World_tour_def_tex = IMG2Sprite.instance.LoadTexture(path_World_tour_def);
            World_tour_def_BG = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def_BG, 100f);
            TOUR_map_2 = IMG2Sprite.instance.LoadNewSprite(path_TOUR_map_2, 100f);


        }
    }

    public class Utility
    {
        public const string MOD_TITLE = "National Tour";
        public const string MOD_TEXTURE_DIR = "Patch";
        public const string TOUR_POPUP_BG_FILE = "World_tour_def.jpg";
        public const string TOUR_POPUP_BG_BLURRED_FILE = "World_tour_def_BG.jpg";
        public const string TOUR_MAP_FILE = "TOUR_map_2.jpg";

        public static Sprite TOUR_map_2;
        public static Sprite World_tour_def;
        public static Texture World_tour_def_tex;
        public static Sprite World_tour_def_BG;

        public static Image tourPopupBGImage;

        public static Dictionary<SEvent_Tour._country, Prefectures> tourLocations = new()
                {
                    { SEvent_Tour._country.china, Prefectures.saitama },
                    { SEvent_Tour._country.southKorea, Prefectures.kanagawa },
                    { SEvent_Tour._country.india, Prefectures.niigata },
                    { SEvent_Tour._country.thailand, Prefectures.aichi },
                    { SEvent_Tour._country.philippines, Prefectures.chiba },
                    { SEvent_Tour._country.indonesia, Prefectures.shizuoka },
                    { SEvent_Tour._country.australia, Prefectures.ishikawa },
                    { SEvent_Tour._country.southAfrica, Prefectures.okinawa },
                    { SEvent_Tour._country.canada, Prefectures.hokkaido },
                    { SEvent_Tour._country.US, Prefectures.miyagi },
                    { SEvent_Tour._country.brazil, Prefectures.fukuoka },
                    { SEvent_Tour._country.argentina, Prefectures.kumamoto },
                    { SEvent_Tour._country.UK, Prefectures.ehime },
                    { SEvent_Tour._country.france, Prefectures.osaka },
                    { SEvent_Tour._country.germany, Prefectures.okayama },
                    { SEvent_Tour._country.italy, Prefectures.hiroshima },
                    { SEvent_Tour._country.spain, Prefectures.hyogo },
                    { SEvent_Tour._country.russia, Prefectures.kyoto }
                };


        public static Dictionary<Prefectures, Vector3> locationDict = new()
                {
                    { Prefectures.ishikawa, new Vector3(0.1f, 0f, 0f) },
                    { Prefectures.hokkaido, new Vector3(4.8f, 2.5f, 0f) },
                    { Prefectures.miyagi, new Vector3(3.2f, 0.5f, 0f) },
                    { Prefectures.okinawa, new Vector3(-5.9f, 2.8f, 0f) },
                    { Prefectures.chiba, new Vector3(1.6f, -1.9f, 0f) },
                    { Prefectures.saitama, new Vector3(1.1f, -0.9f, 0f) },
                    { Prefectures.niigata, new Vector3(1.4f, 0f, 0f) },
                    { Prefectures.aichi, new Vector3(-0.2f, -1.2f, 0f) },
                    { Prefectures.shizuoka, new Vector3(0.3f, -1.9f, 0f) },
                    { Prefectures.kanagawa, new Vector3(1f, -1.4f, 0f) },
                    { Prefectures.fukuoka, new Vector3(-4f, -0.4f, 0f) },
                    { Prefectures.kumamoto, new Vector3(-4.5f, -1.2f, 0f) },
                    { Prefectures.ehime, new Vector3(-2.8f, -1.3f, 0f) },
                    { Prefectures.hiroshima, new Vector3(-2.8f, -0.8f, 0f) },
                    { Prefectures.okayama, new Vector3(-1.9f, -0.4f, 0f) },
                    { Prefectures.hyogo, new Vector3(-1.6f, -0.9f, 0f) },
                    { Prefectures.osaka, new Vector3(-1.3f, -1.4f, 0f) },
                    { Prefectures.kyoto, new Vector3(-0.7f, -0.7f, 0f) }
                };


        public enum Prefectures
        {
            saitama = 1200,
            kanagawa = 1201,
            niigata = 1202,
            aichi = 1203,
            chiba = 1204,
            shizuoka = 1205,
            ishikawa = 1206,
            okinawa = 1207,
            hokkaido = 1208,
            miyagi = 1209,
            fukuoka = 1210,
            kumamoto = 1211,
            ehime = 1212,
            osaka = 1213,
            okayama = 1214,
            hiroshima = 1215,
            hyogo = 1216,
            kyoto = 1217
        };


    }

}
