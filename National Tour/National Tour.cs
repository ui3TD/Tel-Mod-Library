using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEngine.Events;
using SimpleJSON;
using static CM_Theater_Performance;
using System;

namespace NationalTour
{

    // Set locations
    [HarmonyPatch(typeof(Tour_New_Popup), "Reset")]
    public class Tour_New_Popup_Reset
    {
        public static void Postfix(ref Tour_New_Popup __instance)
        {
            if(SEvent_Actions.nationalTab)
            {
                Image image = __instance.transform.Find("Panel").Find("BG").GetComponent<Image>();
                image.sprite = Utility.TOUR_map_2;

                Tour_Country[] componentsInChildren = __instance.CountriesContainer.transform.GetComponentsInChildren<Tour_Country>();

                for (int i = 0; i < Utility.tourLocations.Count; i++)
                {
                    componentsInChildren[i].CountryType = (SEvent_Tour._country)Utility.tourLocations[componentsInChildren[i].CountryType];
                    componentsInChildren[i].UpdateData();


                    switch ((Utility.Prefectures)componentsInChildren[i].CountryType)
                    {
                        case Utility.Prefectures.ishikawa: // ishikawa
                            componentsInChildren[i].transform.position = new Vector3(0.1f, 0f, 0f);
                            break;
                        case Utility.Prefectures.hokkaido: // hokkaido
                            componentsInChildren[i].transform.position = new Vector3(4.8f, 2.5f, 0f);
                            break;
                        case Utility.Prefectures.miyagi: // miyagi
                            componentsInChildren[i].transform.position = new Vector3(3.2f, 0.5f, 0f);
                            break;
                        case Utility.Prefectures.okinawa: // okinawa
                            componentsInChildren[i].transform.position = new Vector3(-5.9f, 2.8f, 0f);
                            break;
                        case Utility.Prefectures.chiba: // chiba
                            componentsInChildren[i].transform.position = new Vector3(1.6f, -1.9f, 0f);
                            break;
                        case Utility.Prefectures.saitama: // saitama
                            componentsInChildren[i].transform.position = new Vector3(1.1f, -0.9f, 0f);
                            break;
                        case Utility.Prefectures.niigata: // niigata
                            componentsInChildren[i].transform.position = new Vector3(1.4f, 0f, 0f);
                            break;
                        case Utility.Prefectures.aichi: // aichi
                            componentsInChildren[i].transform.position = new Vector3(-0.2f, -1.2f, 0f);
                            break;
                        case Utility.Prefectures.shizuoka: // shizuoka
                            componentsInChildren[i].transform.position = new Vector3(0.3f, -1.9f, 0f);
                            break;
                        case Utility.Prefectures.kanagawa: // kanagawa
                            componentsInChildren[i].transform.position = new Vector3(1f, -1.4f, 0f);
                            break;
                        case Utility.Prefectures.fukuoka: // fukuoka
                            componentsInChildren[i].transform.position = new Vector3(-4f, -0.4f, 0f);
                            break;
                        case Utility.Prefectures.kumamoto: // kumamoto
                            componentsInChildren[i].transform.position = new Vector3(-4.5f, -1.2f, 0f);
                            break;
                        case Utility.Prefectures.ehime: // ehime
                            componentsInChildren[i].transform.position = new Vector3(-2.8f, -1.3f, 0f);
                            break;
                        case Utility.Prefectures.hiroshima: // hiroshima
                            componentsInChildren[i].transform.position = new Vector3(-2.8f, -0.8f, 0f);
                            break;
                        case Utility.Prefectures.okayama: // okayama
                            componentsInChildren[i].transform.position = new Vector3(-1.9f, -0.4f, 0f);
                            break;
                        case Utility.Prefectures.hyogo: // hyogo
                            componentsInChildren[i].transform.position = new Vector3(-1.6f, -0.9f, 0f);
                            break;
                        case Utility.Prefectures.osaka: // osaka
                            componentsInChildren[i].transform.position = new Vector3(-1.3f, -1.4f, 0f);
                            break;
                        case Utility.Prefectures.kyoto: // kyoto
                            componentsInChildren[i].transform.position = new Vector3(-0.7f, -0.7f, 0f);
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {

            }
        }
    }


    // Set tour finish images
    [HarmonyPatch(typeof(Tour_Popup), "Reset")]
    public class Tour_Popup_Reset
    {
        public static void Postfix()
        {
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);

            // Set the BGImage transition, and do a slight fade-in and zoom-in effect
            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().BGImage.GetComponent<RawImage>().texture = Utility.World_tour_def_tex;
            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().BGImage.GetComponent<CanvasGroup>().alpha = 0f;
            Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().BGImage.GetComponent<RectTransform>().sizeDelta = new Vector2(1024, 0);
            TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOFade(Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().BGImage.GetComponent<CanvasGroup>(), 1f, 0.5f), Ease.OutQuint);
            TweenSettingsExtensions.SetEase(DOTweenModuleUI.DOSizeDelta(Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().BGImage.GetComponent<RectTransform>(), new Vector2(1074,20), 4.5f), Ease.OutQuint);

            // Set the blurred BG image for the results
            GameObject imgobj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_tour).obj.transform.Find("BG").gameObject;
            Image image = imgobj.GetComponent<Image>();
            image.color = new Color(1,1,1,1f);
            image.sprite = Utility.World_tour_def_BG;
        }
    }

    // Load countries
    [HarmonyPatch(typeof(SEvent_Tour.country), "Set")]
    public class SEvent_Tour_country_Set
    {
        public static bool Prefix(SEvent_Tour.country __instance, JSONNode data)
        {
            if(Enum.TryParse(data["type"], true, out Utility.Prefectures result))
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
            Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);

            if (mainScript.IsMainMenu())
            {
                return;
            }

            Mods._mod thisMod = null;
            foreach (Mods._mod mod in Mods._Mods)
            {
                if (mod.Title == "National Tour")
                {
                    thisMod = mod;
                    break;
                }
            }

            string imageDir = Path.Combine(thisMod.GetPath(), "Textures", "Patch");
            string path_World_tour_def = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, "World_tour_def.jpg"));
            string path_World_tour_def_BG = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, "World_tour_def_BG.jpg"));
            string path_TOUR_map_2 = System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDir, "TOUR_map_2.jpg"));

            Utility.World_tour_def = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def, 100f);
            Utility.World_tour_def_tex = IMG2Sprite.instance.LoadTexture(path_World_tour_def);
            Utility.World_tour_def_BG = IMG2Sprite.instance.LoadNewSprite(path_World_tour_def_BG, 100f);
            Utility.TOUR_map_2 = IMG2Sprite.instance.LoadNewSprite(path_TOUR_map_2, 100f);

            //GameObject popupObj = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;
            //Image bgimg = popupObj.transform.Find("Container").Find("World Tour").Find("BG").gameObject.GetComponent<Image>();
            //Utility.World_tour_def_orig = bgimg.sprite;

        }
    }

    public class Utility
    {
        public static Sprite TOUR_map_2;
        public static Sprite World_tour_def;
        public static Texture World_tour_def_tex;
        public static Sprite World_tour_def_BG;
        //public static Sprite TOUR_map_2_orig;
        //public static Sprite World_tour_def_orig;
        //public static Texture World_tour_def_tex_orig;
        //public static Sprite World_tour_def_BG_orig;

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

        public static void ProbeGameObject(GameObject gameObject)
        {
            Debug.Log("Probing Object: " + gameObject.name);

            Component[] comps = gameObject.GetComponentsInChildren<Component>();

            string compName;
            string prefix;
            string suffix;
            foreach (Component comp in comps)
            {
                prefix = "";
                suffix = "";
                compName = comp.GetType().Name;

                if (comp.GetType() == typeof(RectTransform))
                {
                    suffix = " (" + (comp as RectTransform).rect.width + " x " + (comp as RectTransform).rect.height + ")";
                }
                if (comp.GetType() == typeof(CanvasGroup))
                {
                    suffix = " (alpha: " + (comp as CanvasGroup).alpha + ")";
                }
                if (comp.GetType() == typeof(TextMeshProUGUI))
                {
                    suffix = " (" + (comp as TextMeshProUGUI).text + ")";
                }
                if (comp.GetType() == typeof(Text))
                {
                    suffix = " (" + (comp as Text).text + ")";
                }
                if (comp.GetType() == typeof(Image))
                {
                    Sprite sprite = (comp as Image).sprite;
                    suffix = " (";
                    if (sprite != null)
                    {
                        suffix += "sprite: " + sprite.name + ", ";
                    }
                    suffix += (comp as Image).color.ToString();
                    suffix += ")";
                }
                if (comp.GetType() == typeof(RawImage))
                {
                    Texture texture = (comp as RawImage).texture;
                    if (texture != null)
                    {
                        suffix = " (texture: " + texture.name + ")";
                    }
                }
                if (comp.GetType() == typeof(Lang_Button))
                {
                    suffix = " (" + (comp as Lang_Button).Constant + ")";
                }
                if (comp.GetType() == typeof(Button))
                {
                    int eventCount = (comp as Button).onClick.GetPersistentEventCount();
                    if(eventCount > 0)
                    {
                        suffix = " (" + (comp as Button).onClick.GetPersistentTarget(0).GetType().Name + "." + (comp as Button).onClick.GetPersistentMethodName(0);

                        int i = 1;
                        while (i < eventCount)
                        {
                            suffix += ", " + (comp as Button).onClick.GetPersistentTarget(i).GetType().Name + "." + (comp as Button).onClick.GetPersistentMethodName(i);
                            i++;
                        }
                        suffix += ")";
                    }
                }

                Transform trans = comp.gameObject.transform;
                string tag;
                while (trans != null)
                {
                    tag = "";
                    if(trans.gameObject.tag != "Untagged")
                    {
                        tag = $" #{trans.gameObject.tag}";
                    }
                    prefix = trans.gameObject.name + tag + ":" + prefix;
                    trans = trans.parent;
                }
                Debug.Log(prefix + compName + suffix);

            }
        }

    }

}
