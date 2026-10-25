using HarmonyLib;
using System.Reflection;
using TMPro;
using UiCopies;
using UnityEngine;
using UnityEngine.UI;
using Module = NationalTour.Readiness.Module;

namespace NationalTour
{
    /// <summary>
    /// The Special Events popup's fourth tab, National Tour: a copy of the World Tour tab and its panel. The
    /// World Tour tab keeps the game's picture and text.
    /// </summary>
    public static class NationalTab
    {
        public const string NAME = "National Tour";
        public const string TITLE_KEY = "NATIONAL_TOUR__TITLE";
        public const string DESC_KEY = "NATIONAL_TOUR__DESC";

        // Shown if the language files don't have the mod's text
        public const string TITLE_FALLBACK = "National Tour";
        public const string DESC_FALLBACK = "Select the prefectures you want to perform in and choose a type of venue.";

        /// <summary>
        /// The game's World Tour panel, as read in the game with no mods loaded.
        /// </summary>
        public static readonly VanillaShape WorldTourPanelShape = new(
            new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(CanvasGroup) },
            new[] { "BG", "Panel", "Panel/Text", "Panel/Cooldown" });

        public static GameObject Tab;
        public static GameObject Panel;
        public static SpecialEvents_Manager Manager;

        private static readonly MethodInfo RenderContinueButton = AccessTools.Method(typeof(SpecialEvents_Manager), "RenderContinueButton");
        private static readonly MethodInfo RenderTab = AccessTools.Method(typeof(TabButton), "Render");

        /// <summary>
        /// The tab is only added when a national tour can be planned: the prefectures, the national record and
        /// the map of Japan are all there.
        /// </summary>
        public static bool CanOpen() =>
            Readiness.AreReady(Module.Prefectures, Module.NationalRecord, Module.NationalTab, Module.NewTourMap)
            && Prefectures.All.Count > 0 && Pictures.TOUR_map_2 != null;

        /// <summary>
        /// Adds the tab and its panel once per game scene (Unity's == is true for a tab the engine destroyed with
        /// the last scene).
        /// </summary>
        public static void Build(SpecialEvents_Manager manager)
        {
            if (Tab != null || !CanOpen())
                return;

            GameObject popup = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.special_events).obj;
            Transform container = popup.transform.Find("Container");
            Transform types = container?.Find("Types");
            Transform worldTab = types?.Find("World Tour");
            Transform worldPanel = container?.Find("World Tour");
            TabButton worldTabButton = worldTab?.GetComponent<TabButton>();
            if (worldPanel == null || worldTabButton == null || worldTab.GetComponent<Button>() == null)
            {
                Readiness.Fail(Module.NationalTab, "the Special Events popup has no World Tour tab to copy");
                return;
            }

            // The panel goes right after the World Tour panel, so the tab bar stays drawn on top
            Panel = UiCopy.CopyGameObject(worldPanel.gameObject, container, WorldTourPanelShape, "[National Tour]", "World Tour panel");
            Panel.name = NAME;
            Panel.transform.SetSiblingIndex(worldPanel.GetSiblingIndex() + 1);
            SetText(Panel.transform.Find("Panel")?.Find("Text"), DESC_KEY, DESC_FALLBACK);
            Image picture = Panel.transform.Find("BG")?.GetComponent<Image>();
            if (picture != null && Pictures.World_tour_def != null)
                picture.sprite = Pictures.World_tour_def;

            Tab = Object.Instantiate(worldTab.gameObject, types, false);
            Tab.name = NAME;
            SetText(Tab.transform.Find("Text"), TITLE_KEY, TITLE_FALLBACK);
            TabButton tabButton = Tab.GetComponent<TabButton>();
            tabButton.TabToOpen = Panel;

            // The copy's listeners would open the World Tour tab
            Button button = Tab.GetComponent<Button>();
            Manager = manager;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnTabClicked);

            tabButton.HideTab();
            // The World Tour tab is no longer the last one, so each tab redraws its ends
            foreach (TabButton each in types.GetComponentsInChildren<TabButton>())
                CallRenderTab(each);
        }

        /// <summary>
        /// The tab's click.
        /// </summary>
        public static void OnTabClicked()
        {
            if (Tab != null)
                Open(Manager, Tab.GetComponent<TabButton>());
        }

        /// <summary>
        /// What the World Tour tab does, for the national tour: show the panel, then the Continue button for
        /// the national record.
        /// </summary>
        public static void Open(SpecialEvents_Manager manager, TabButton tabButton)
        {
            tabButton.OnClick();
            manager.OpenTab = NationalRecord.Tab;
            CallRenderContinueButton(manager);
        }

        public static void CallRenderContinueButton(SpecialEvents_Manager manager) => RenderContinueButton.Invoke(manager, null);

        public static void CallRenderTab(TabButton tab) => RenderTab.Invoke(tab, null);

        /// <summary>
        /// Points the copied label at the mod's text, which the game then also uses when the language changes.
        /// </summary>
        public static void SetText(Transform label, string key, string fallback)
        {
            if (label == null)
                return;
            Lang_Button lang = label.GetComponent<Lang_Button>();
            if (lang != null)
                lang.Constant = key;
            if (Language.Data.ContainsKey(key))
                lang?.ResetText();
            else
            {
                TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                if (text != null)
                    text.text = fallback;
            }
        }
    }


    // Add the National Tour tab the first time the popup opens
    [HarmonyPatch(typeof(SpecialEvents_Manager), nameof(SpecialEvents_Manager.OpenSpecialEventsPopup))]
    public class SpecialEvents_Manager_OpenSpecialEventsPopup
    {
        public static void Postfix(SpecialEvents_Manager __instance) => NationalTab.Build(__instance);
    }


    // Continue on the National Tour tab: the game opens the new tour popup for any tab it doesn't know, but only
    // resets it for the World Tour tab
    [HarmonyPatch(typeof(SpecialEvents_Manager), "ResetPopup_New")]
    public class SpecialEvents_Manager_ResetPopup_New
    {
        public static void Postfix(SpecialEvents_Manager __instance)
        {
            if (__instance.OpenTab != NationalRecord.Tab)
                return;
            Tour_New_Popup popup = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.sevent_tour_new).obj.GetComponent<Tour_New_Popup>();
            NewTourMap.OpenNational(popup);
        }
    }
}
