using ConcertHype.Model;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        public void NextFrame(Action action) => popup.StartCoroutine(AfterAFrame(action));

        private static IEnumerator AfterAFrame(Action action)
        {
            yield return null;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.WarnOnce("next frame", "couldn't land the hype after the song: " + ex.Message);
            }
        }
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
                ShowSession session = ShowSession.Begin(concert, config);
                try
                {
                    BenchColumn.Build(__instance.ConcertPopup, session);
                }
                catch (Exception ex)
                {
                    Log.Error("couldn't show the cast; the show goes on with each song's default lineup: " + ex);
                }
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

        // Between songs, the panel shows the hype the crowd is at now.
        public static void Postfix(Concert_Popup __instance)
        {
            try
            {
                ShowSession session = ShowSession.Current;
                if (session != null && !session.Playing && ReferenceEquals(__instance.Concert, session.Concert))
                    LineupPanel.Render();
            }
            catch (Exception ex)
            {
                Log.WarnOnce("render", "couldn't redraw the lineup panel: " + ex.Message);
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
            BenchColumn.Restore();
        }
    }

    // The player starts the next song, or the talk break before it: the model plays it. A talk break goes into
    // the setlist where the popup is, before the popup reads its next item.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.OnNextSongClick))]
    public class Concert_Popup_OnNextSongClick
    {
        public static void Prefix(Concert_Popup __instance)
        {
            ShowSession session = ShowSession.Current;
            if (session == null || !ReferenceEquals(__instance.Concert, session.Concert))
                return;
            bool talk = session.Host >= 0;
            try
            {
                if (talk)
                    session.StartTalkBreak(new PopupStage(__instance));
                else
                    session.BeforeSong(new PopupStage(__instance));
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, talk ? "starting the talk break" : "playing the song");
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

    // The new show has no action cards: the opening goes straight from the stage to the first song, without the
    // card draw. And the accident wheel stays hidden while a song plays.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.ShowObject))]
    public class Concert_Popup_ShowObject
    {
        public static bool Prefix(Concert_Popup __instance, GameObject obj)
        {
            if (!Concert_Popup_AnimateAccident.ShowsNewShow(__instance))
                return true;
            try
            {
                if (ReferenceEquals(obj, __instance.DrawCardsTitle))
                {
                    // Where the game would show the deck, it goes on as if the drawing were done
                    __instance.OnDrawingComplete();
                    return false;
                }
                return !ReferenceEquals(obj, __instance.DrawCardsButton) && !ReferenceEquals(obj, __instance.Accident_Top_Container);
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "skipping the card draw");
                return true;
            }
        }
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

    // The show's talk break pulls the hype to the model's value: the popup animates it over the talk break.
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetHype))]
    public class SEvent_Concerts__concert__mc_GetHype
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts._concert._mc __instance, ref float __result)
        {
            try
            {
                if (ShowSession.Current != null && ShowSession.Current.HypeForTalk(__instance, out float change))
                    __result = change;
            }
            catch (Exception ex)
            {
                ShowSession.Fail(ex, "the talk break's hype");
            }
        }
    }

    // Talk breaks cost no real stamina either.
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetStaminaCost))]
    public class SEvent_Concerts__concert__mc_GetStaminaCost
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (ShowSession.Current != null)
                __result = 0f;
        }
    }

    // ...and restore none: the show's talk break rests the show's energy instead.
    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.StaminaToRestore))]
    public class SEvent_Concerts__concert_StaminaToRestore
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SEvent_Concerts._concert __instance, ref float __result)
        {
            if (ReferenceEquals(ShowSession.Current?.Concert, __instance))
                __result = 0f;
        }
    }

    /// <summary>
    /// How long the popup plays a song or talk break.
    /// </summary>
    public static class Pace
    {
        public const float GameSeconds = 10f;

        /// <summary>
        /// Set when the duration transpiler found its anchor.
        /// </summary>
        internal static bool TranspilerApplied;

        /// <summary>
        /// The configured length during the show; the game's 10 s otherwise.
        /// </summary>
        public static float SongSeconds()
        {
            double seconds = ShowSession.Current?.State.Config.SongSeconds ?? GameSeconds;
            return seconds > 0 ? (float)seconds : GameSeconds;
        }
    }

    // Songs and talk breaks last Pace.SongSeconds(): the popup's PlaySong coroutine stores the game's 10 s into its
    // duration once, which every tween of the song (progress bar, hype, stamina) uses.
    [HarmonyPatch]
    public class Concert_Popup_PlaySong
    {
        public static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Concert_Popup), "PlaySong"));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> code = instructions.ToList();
            try
            {
                MethodInfo pace = AccessTools.Method(typeof(Pace), nameof(Pace.SongSeconds));
                if (code.Any(c => c.Calls(pace)))
                {
                    // Already changed (the patch applied twice)
                    Pace.TranspilerApplied = true;
                    return code;
                }
                FieldInfo duration = AccessTools.GetDeclaredFields(original.DeclaringType).FirstOrDefault(f => f.Name.Contains("songDuration"));
                List<int> anchors = Enumerable.Range(0, code.Count - 1)
                    .Where(i => code[i].opcode == OpCodes.Ldc_R4 && code[i].operand is float value && value == Pace.GameSeconds
                        && duration != null && code[i + 1].StoresField(duration))
                    .ToList();
                if (anchors.Count != 1)
                {
                    Log.Warn($"the concert's song length is set {anchors.Count} times, not once (another mod changed it?); songs keep their length");
                    return code;
                }
                code[anchors[0]].opcode = OpCodes.Call;
                code[anchors[0]].operand = pace;
                Pace.TranspilerApplied = true;
            }
            catch (Exception ex)
            {
                Log.Warn("couldn't change the concert's song length; songs keep their length: " + ex.Message);
            }
            return code;
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

    // Closing the results: the last song's hype animation may have ended a frame after the score was set, so the
    // concert's hype lands on the score again. A bigger difference is another mod's doing and stays.
    [HarmonyPatch(typeof(Concert_Popup), nameof(Concert_Popup.FinishConcert))]
    public class Concert_Popup_FinishConcert
    {
        public static void Prefix(Concert_Popup __instance)
        {
            try
            {
                Score score = ShowSession.LastScore;
                if (score == null || !ReferenceEquals(ShowSession.LastScoreConcert, __instance.Concert))
                    return;
                float drift = (float)score.Total - __instance.Concert.Hype;
                if (drift != 0f && Mathf.Abs(drift) < ShowSession.MaxDrift)
                    __instance.AddHype(drift);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("finish", "couldn't set the concert's hype to the score: " + ex.Message);
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
