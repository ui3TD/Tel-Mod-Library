using AICore;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AIFanChatter
{
    // Every patch reads, or changes an argument or a constant; none skips a game method. Each catches its
    // own errors, so a problem in the mod never stops the game's method.

    /// <summary>
    /// A part of an award's acceptance speech, as the show builds it into the line it's about to show.
    /// </summary>
    [HarmonyPatch(typeof(vn_actions), "DoCustom")]
    public class vn_actions_DoCustom
    {
        public static void Postfix(string formula, ActiveDialogueController ___ADC, bool __runOriginal)
        {
            try
            {
                if (__runOriginal && AwardsShow.IsSpeechPart(formula) && !ReferenceEquals(___ADC, null))
                {
                    AwardsShow.Heard(Award_Speeches_Popup.SelectedSpeech, ___ADC.activeNode?.val);
                }
            }
            catch (Exception)
            {
                // Posted without the speech's words
            }
        }
    }

    /// <summary>
    /// The yearly awards show, as its dialogue ends: the game clears this year's nominations there, so the
    /// prefix copies them first.
    /// </summary>
    [HarmonyPatch(typeof(Awards), nameof(Awards.Reset_After_Awards))]
    [HarmonyPriority(Priority.Last)]
    public class Awards_Reset_After_Awards
    {
        // The date of the last show posted: the show ends once a year
        private static DateTime posted = DateTime.MinValue;

        internal static void Reset() => posted = DateTime.MinValue;

        public static void Prefix(out AwardsShow __state)
        {
            __state = null;
            try
            {
                __state = AwardsShow.Read();
            }
            catch (Exception)
            {
                // No request
            }
        }

        public static void Postfix(AwardsShow __state, bool __runOriginal)
        {
            try
            {
                if (__runOriginal && __state == null)
                {
                    Log.Warn("The awards show ended without nominations to read; no posts for it.");
                }
                if (ShouldPost(__state, __runOriginal, staticVars.dateTime))
                {
                    posted = staticVars.dateTime.Date;
                    Moments.Trigger(() => MomentContext.Awards(__state));
                }
            }
            catch (Exception)
            {
                // No request
            }
        }

        internal static bool ShouldPost(AwardsShow show, bool ran, DateTime now)
        {
            return ran && show != null && show.Nominations.Count > 0 && now.Date != posted;
        }
    }
}
