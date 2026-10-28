using ConcertHype.Model;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;

namespace ConcertHype
{
    /// <summary>
    /// The game's concert popup, as the session sees it.
    /// </summary>
    internal sealed class PopupStage : IConcertStage
    {
        private static readonly AccessTools.FieldRef<Concert_Popup, int> SongCounterField =
            AccessTools.FieldRefAccess<Concert_Popup, int>("songCounter");

        private readonly Concert_Popup popup;

        public PopupStage(Concert_Popup popup) => this.popup = popup;

        public SEvent_Concerts._concert Concert => popup.Concert;

        public int SongCounter => SongCounterField(popup);

        public void AddHype(float value, bool animate) => popup.AddHype(value, animate);
    }

    // Start the new show for a qualifying concert, after the game opened its popup.
    [HarmonyPatch(typeof(SEvent_Concerts), nameof(SEvent_Concerts.StartConcert))]
    public class SEvent_Concerts_StartConcert
    {
        public static void Postfix(SEvent_Concerts __instance, bool __runOriginal)
        {
            try
            {
                SEvent_Concerts._concert concert = __instance.Concert;
                if (!__runOriginal || concert == null || !ReferenceEquals(__instance.ConcertPopup?.Concert, concert))
                    return;
                if (!ShowSession.Qualifies(concert))
                {
                    if (Switch.Enabled && concert.SetListItems.Count > 0)
                        Log.Info("this concert's setlist has talk breaks, so it plays as in the original game");
                    return;
                }
                string folder = ModFolder.Find();
                Config config = folder == null ? new Config() : ConfigFile.Load(Path.Combine(folder, ConfigFile.FileName), Log.Warn);
                ShowSession.LogPath = folder == null ? null : Path.Combine(folder, "last_show.txt");
                ShowSession.Begin(concert, config);
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "starting the show");
            }
        }
    }

    // No action cards: the new show replaces them.
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.GenerateCards))]
    public class SEvent_Concerts__concert_GenerateCards
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            try
            {
                if (ShowSession.Qualifies(__instance))
                    __instance.Cards.Clear();
            }
            catch (Exception ex)
            {
                Log.WarnOnce("cards", "couldn't remove the action cards: " + ex.Message);
            }
        }
    }

    // Talk breaks are called during the show instead of planned in the setlist.
    [HarmonyPatch(typeof(Concert_New_Popup), nameof(Concert_New_Popup.OnAddMC))]
    public class Concert_New_Popup_OnAddMC
    {
        public static bool Prefix() => !Switch.Enabled;
    }

    // The show starts at least at the starting hype: the game's own start (20, or an election's minimum) is raised.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.AddHype))]
    public class Concert_Popup_AddHype
    {
        public static void Prefix(Concert_Popup __instance, ref float val)
        {
            try
            {
                ShowSession session = ShowSession.Current;
                if (session == null || session.StartHypeApplied || !ReferenceEquals(__instance.Concert, session.Concert))
                    return;
                session.StartHypeApplied = true;
                val = Mathf.Max(val, (float)session.State.Config.StartingHype - __instance.Concert.Hype);
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "setting the starting hype");
            }
        }
    }

    // A new show never inherits an old one.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.Reset))]
    public class Concert_Popup_Reset
    {
        public static void Prefix()
        {
            if (ShowSession.Current != null)
                ShowSession.End();
        }
    }

    // The player starts the next song: the model plays it.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.OnNextSongClick))]
    public class Concert_Popup_OnNextSongClick
    {
        public static void Prefix(Concert_Popup __instance)
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !ReferenceEquals(__instance.Concert, session.Concert))
                return;
            try
            {
                session.BeforeSong(new PopupStage(__instance));
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "playing the song");
            }
        }
    }

    // No accident wheel: the new show has its own accidents.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.AnimateAccident))]
    public class Concert_Popup_AnimateAccident
    {
        public static bool Prefix(Concert_Popup __instance) => !ShowsNewShow(__instance);

        internal static bool ShowsNewShow(Concert_Popup popup) =>
            ShowSession.Current != null && ReferenceEquals(popup.Concert, ShowSession.Current.Concert);
    }

    // Keep the wheel hidden while a song plays.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.ShowObject))]
    public class Concert_Popup_ShowObject
    {
        public static bool Prefix(Concert_Popup __instance, GameObject obj) =>
            !(Concert_Popup_AnimateAccident.ShowsNewShow(__instance) && ReferenceEquals(obj, __instance.Accident_Top_Container));
    }

    // The song's hype is the model's: the popup animates it over the song.
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetHype))]
    public class SEvent_Concerts__concert__song_GetHype
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts._concert._song __instance, ref float __result)
        {
            try
            {
                if (ShowSession.Current != null && ShowSession.Current.HypeFor(__instance, out float change))
                    __result = change;
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "the song's hype");
            }
        }
    }

    // The show's energy is its own: songs cost no real stamina.
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetStaminaCost))]
    public class SEvent_Concerts__concert__song_GetStaminaCost
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (ShowSession.Current != null)
                __result = 0f;
        }
    }

    // The song ended: land the hype on the model's value.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.OnSongFinished))]
    public class Concert_Popup_OnSongFinished
    {
        public static void Prefix(Concert_Popup __instance)
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !ReferenceEquals(__instance.Concert, session.Concert))
                return;
            try
            {
                session.AfterSong(new PopupStage(__instance));
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "ending the song");
            }
        }
    }

    // The show is over: the score becomes the concert's hype before the game finishes the concert.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.ShowFinalScreen))]
    public class Concert_Popup_ShowFinalScreen
    {
        public static void Prefix(Concert_Popup __instance)
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !ReferenceEquals(__instance.Concert, session.Concert))
                return;
            try
            {
                session.Finish(new PopupStage(__instance));
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "scoring the show");
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            if (ReferenceEquals(ShowSession.Current?.Concert, __instance))
                ShowSession.End();
        }
    }
}
