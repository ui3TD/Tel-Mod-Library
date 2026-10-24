using HarmonyLib;
using UnityEngine;

namespace NationalTour
{
    /// <summary>
    /// The national tour lives in a second, disabled SEvent_Tour next to the game's own. Its Tour field holds the
    /// national tour in production, so the game's own SetTour, Assign, StartTour, FinishTour and CancelTour run
    /// unchanged on it, and other mods' patches on them see the national tour as __instance.Tour.
    /// </summary>
    public static class NationalHandler
    {
        /// <summary>
        /// The national handler of the current game scene, or null before it's added.
        /// </summary>
        public static SEvent_Tour Instance;

        private static bool adding;

        private static readonly AccessTools.FieldRef<SEvent_Tour, PopupManager> PopupManager =
            AccessTools.FieldRefAccess<SEvent_Tour, PopupManager>("popupManager");

        public static bool IsNationalHandler(SEvent_Tour handler) => handler is not null && ReferenceEquals(handler, Instance);

        /// <summary>
        /// The handler a tour belongs to: the national one for a tour to prefectures, the game's otherwise.
        /// </summary>
        public static SEvent_Tour For(SEvent_Tour game, SEvent_Tour.tour tour) =>
            Instance is not null && Prefectures.IsNational(tour) ? Instance : game;

        /// <summary>
        /// Adds the national handler next to the game's, once per game scene. Its own Awake runs as it's added:
        /// it empties the shared tour list (the game's Awake has just done the same) and subscribes to the save
        /// and load events, which only the game's handler answers. Disabled, its Start never runs.
        /// </summary>
        public static void Add(SEvent_Tour game)
        {
            // The national handler's own Awake, or a scene that already has a second handler
            if (adding || game.GetComponents<SEvent_Tour>().Length > 1)
                return;

            SEvent_Tour national;
            adding = true;
            try
            {
                national = game.gameObject.AddComponent<SEvent_Tour>();
            }
            finally
            {
                adding = false;
            }
            national.enabled = false;
            SaveManager.SaveEvent -= national.SaveFunction;
            SaveManager.LoadEvent -= national.LoadFunction;
            Instance = national;
        }

        /// <summary>
        /// The popups the game's Start found, which the national handler opens too.
        /// </summary>
        public static void CopyPopups(SEvent_Tour game)
        {
            if (Instance is null || ReferenceEquals(game, Instance))
                return;
            PopupManager(Instance) = PopupManager(game);
            Instance.TourPopup = game.TourPopup;
        }
    }


    [HarmonyPatch(typeof(SEvent_Tour), "Awake")]
    public class SEvent_Tour_Awake
    {
        public static void Postfix(SEvent_Tour __instance) => NationalHandler.Add(__instance);
    }


    [HarmonyPatch(typeof(SEvent_Tour), "Start")]
    public class SEvent_Tour_Start
    {
        public static void Postfix(SEvent_Tour __instance) => NationalHandler.CopyPopups(__instance);
    }
}
