using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConcertHype
{
    /// <summary>
    /// The game's idol profile card moves to a portrait only when the idol changes, so the same idol hovered on
    /// another of the show's portraits (bench, place, host place) kept the card where she was first hovered.
    /// </summary>
    public static class ProfileCard
    {
        public static bool NeedsMove(bool sameGirl, bool sameButton, bool newIsOurs, bool cachedGone) =>
            sameGirl && !sameButton && (newIsOurs || cachedGone);
    }

    // The profile card goes to the show's portrait that's hovered, with the game's own placement.
    [HarmonyPatch(typeof(TooltipStatusButton), nameof(TooltipStatusButton.Set), new[] { typeof(GameObject), typeof(data_girls.girls), typeof(bool) })]
    public class TooltipStatusButton_Set
    {
        public static void Postfix(TooltipStatusButton __instance, GameObject _StatusButton, data_girls.girls _girl, ref GameObject ___StatusButton, data_girls.girls ___Girl)
        {
            try
            {
                bool cachedGone = !ReferenceEquals(___StatusButton, null) && ___StatusButton == null;
                bool ours = _StatusButton != null && _StatusButton.GetComponent<DragSource>() != null;
                if (!ProfileCard.NeedsMove(ReferenceEquals(___Girl, _girl), ReferenceEquals(___StatusButton, _StatusButton), ours, cachedGone))
                    return;
                ___StatusButton = _StatusButton;
                __instance.Render();
            }
            catch (Exception ex)
            {
                Log.WarnOnce("profile card", "couldn't move the idol's profile card to the hovered portrait: " + ex.Message);
            }
        }
    }

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
