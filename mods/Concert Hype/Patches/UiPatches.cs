using HarmonyLib;
using System;
using UnityEngine.UI;

namespace ConcertHype
{
    // Before each song: the lineup panel for the new show, the game's readouts for its own.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.SetNextSongScreen))]
    public class Concert_Popup_SetNextSongScreen
    {
        public static void Postfix(Concert_Popup __instance)
        {
            try
            {
                ShowSession session = ShowSession.Current;
                if (session != null && ReferenceEquals(__instance.Concert, session.Concert))
                    LineupPanel.Show(__instance, session);
                else
                    LineupPanel.Hide(__instance);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("panel", "couldn't show the lineup panel; each song plays its default lineup: " + ex);
            }
        }
    }

    // The results screen shows the show's score.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.ShowFinalScreen))]
    public class Concert_Popup_ShowFinalScreen_Score
    {
        public static void Postfix(Concert_Popup __instance)
        {
            try
            {
                bool ours = ShowSession.LastScore != null && ReferenceEquals(ShowSession.LastScoreConcert, __instance.Concert);
                ScoreLine.Show(__instance, ours ? PanelView.ScoreLine(ShowSession.LastScore) : null);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("score", "couldn't show the score: " + ex.Message);
            }
        }
    }

    // While the mod is on, the setup has no Add Talk Break button: talk breaks are called during the show.
    [HarmonyPatch(typeof(Concert_New_Popup), nameof(Concert_New_Popup.Reset))]
    public class Concert_New_Popup_Reset
    {
        public static void Postfix(Concert_New_Popup __instance)
        {
            try
            {
                foreach (Button button in __instance.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentMethodName(i) == nameof(Concert_New_Popup.OnAddMC))
                            button.gameObject.SetActive(!Switch.Enabled);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WarnOnce("setup", "couldn't hide the Add Talk Break button: " + ex.Message);
            }
        }
    }
}
