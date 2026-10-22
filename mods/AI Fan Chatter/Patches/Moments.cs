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
    /// <summary>
    /// Starts a request for a moment, unless the limits stop it first; then the moment isn't even read.
    /// </summary>
    internal static class Moments
    {
        public static void Trigger(Func<Moment> moment)
        {
            try
            {
                Dispatcher dispatcher = Dispatcher.Instance;
                if (dispatcher.Blocked() == null)
                {
                    dispatcher.Request(moment());
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't start a request: " + e);
            }
        }

        private static DateTime lastGraduation = DateTime.MinValue;

        /// <summary>
        /// One graduation a day: bankruptcy and story scenes graduate several idols at once.
        /// </summary>
        public static bool FirstGraduationToday(DateTime now)
        {
            if (now.Date == lastGraduation)
            {
                return false;
            }
            lastGraduation = now.Date;
            return true;
        }

        internal static void ResetGraduations() => lastGraduation = DateTime.MinValue;
    }
}
