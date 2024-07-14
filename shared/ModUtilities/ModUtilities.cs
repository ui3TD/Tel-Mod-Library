using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using HarmonyLib;

#if DEBUG
namespace IMModUtilities
{
    [HarmonyPatch(typeof(PopupManager), "Start")]
    public class PopupManager_Start
    {
        public static void Postfix()
        {
            ModUtilities.AttachToPopupManager();
        }
    }

    [HarmonyPatch(typeof(TooltipManager), "Start")]
    public class TooltipManager_Start
    {
        public static void Postfix()
        {
            ModUtilities.AttachToToolTips();
        }
    }

    [HarmonyPatch(typeof(MainMenu_Buttons_Controller), "Start")]
    public class MainMenu_Buttons_Controller_Start
    {
        public static void Postfix()
        {
            ModUtilities.AttachToMainMenu();
        }
    }

    public static class ComponentExtensions
    {
        public static T AddOrGetComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out var outComponent)
                ? outComponent
                : gameObject.AddComponent<T>();
        }

    }

    /// <summary>
    /// Upon clicking "`", outputs game UI objects, their hierarchy and their components to the console.
    /// Upon right click, outputs game UI object that the mouse is hovering over to the console.
    /// </summary>
    public class ModUtilities
    {
        public static void AttachToPopupManager()
        {
            GameObject data = Camera.main.GetComponent<mainScript>().Data;
            GameObject agencyPopups = data.GetComponent<PopupManager>().GetByType(PopupManager._type.main_menu_settings).obj.transform.parent.gameObject;
            agencyPopups.AddOrGetComponent<GraphicRaycaster>();
            agencyPopups.AddOrGetComponent<ClickDetector>();

            GameObject agencyBuilding = data.GetComponent<PopupManager>().AgencyBuilding;
            if(agencyBuilding != null)
            {
                agencyBuilding.AddOrGetComponent<GraphicRaycaster>();
                agencyBuilding.AddOrGetComponent<ClickDetector>();
            }
            GameObject agencyGUI = data.GetComponent<PopupManager>().AgencyGUI;
            if (agencyGUI != null)
            {
                agencyGUI.AddOrGetComponent<GraphicRaycaster>();
                agencyGUI.AddOrGetComponent<ClickDetector>();
            }

        }
        public static void AttachToToolTips()
        {
            GameObject data = Camera.main.GetComponent<mainScript>().Data;
            GameObject Tooltips = data.GetComponent<TooltipManager>().TooltipObject.transform.parent.gameObject;
            Tooltips.AddOrGetComponent<GraphicRaycaster>();
            Tooltips.AddOrGetComponent<ClickDetector>();
        }
        public static void AttachToMainMenu()
        {
            GameObject data = Camera.main.GetComponent<mainScript>().Data;
            GameObject GUI_Buttons = data.GetComponent<MainMenu_Buttons_Controller>().Main_Container.transform.parent.gameObject;
            GUI_Buttons.AddOrGetComponent<GraphicRaycaster>();
            GUI_Buttons.AddOrGetComponent<ClickDetector>();
        }

        public class ClickDetector : MonoBehaviour
        {
            PointerEventData clickData;
            List<RaycastResult> clickResults;
            GraphicRaycaster raycaster;
            private void Start()
            {
                raycaster = GetComponent<GraphicRaycaster>();
                clickData = new(EventSystem.current);
                clickResults = new();
            }

            private void Update()
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Vector2 mousePosition = Input.mousePosition;

                    clickData.position = mousePosition;
                    clickResults.Clear();
                    raycaster.Raycast(clickData, clickResults);

                    if (clickResults.Count > 0)
                    {
                        Debug.Log($"Mouse clicked at: {mousePosition}");
                        ProbeGameObject(clickResults[0].gameObject);
                    }

                }
                else if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    if(gameObject.activeInHierarchy)
                    {
                        ProbeGameObject(gameObject);
                    }
                }
            }

        }

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

                if (comp is RectTransform rt)
                {
                    suffix = " (" + rt.rect.width + " x " + rt.rect.height + ")";
                }
                if (comp is CanvasGroup cg)
                {
                    suffix = " (alpha: " + cg.alpha + ")";
                }
                if (comp is TextMeshProUGUI tmp)
                {
                    suffix = " (" + tmp.text + ")";
                }
                if (comp is Text tx)
                {
                    suffix = " (" + tx.text + ")";
                }
                if (comp is Lang_Button lb)
                {
                    suffix = " (" + lb.Constant + ")";
                }
                if (comp is Image im)
                {
                    Sprite sprite = im.sprite;
                    suffix = " (";
                    if (sprite != null)
                    {
                        suffix += "sprite: " + sprite.name + ", ";
                    }
                    suffix += im.color.ToString();
                    suffix += ")";
                }
                if (comp is RawImage ri)
                {
                    Texture texture = ri.texture;
                    if (texture != null)
                    {
                        suffix = " (texture: " + texture.name + ")";
                    }
                }
                if (comp is Button btn)
                {
                    int eventCount = btn.onClick.GetPersistentEventCount();
                    if (eventCount > 0)
                    {
                        suffix = " (" + btn.onClick.GetPersistentTarget(0).GetType().Name + "." + btn.onClick.GetPersistentMethodName(0);

                        int i = 1;
                        while (i < eventCount)
                        {
                            suffix += ", ";
                            if(btn.onClick.GetPersistentTarget(i) != null)
                            {
                                suffix += btn.onClick.GetPersistentTarget(i).GetType().Name + ".";
                            }
                            suffix += btn.onClick.GetPersistentMethodName(i);
                            i++;
                        }
                        suffix += ")";
                    }
                }

                Transform trans = comp.gameObject.transform;
                while (trans != null)
                {
                    prefix = trans.gameObject.name + ":" + prefix;
                    trans = trans.parent;
                }
                Debug.Log(prefix + compName + suffix);

            }
        }
    }

}
#endif