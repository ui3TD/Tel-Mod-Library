using HarmonyLib;
using System;
using System.Collections.Generic;
using TraitFix;
using Xunit;

namespace TraitsFixTests
{
    /// <summary>
    /// The weekly trait relationship changes and the Indiscreet leak roll run inside the game's new week.
    /// An error there is logged once and the week goes on.
    /// </summary>
    public class WeeklyErrorTests
    {
        public WeeklyErrorTests() => TestGame.Reset();

        public static void Broken() => throw new InvalidOperationException("broken");

        /// <summary>
        /// Calls the postfix twice with the mod's own weekly method made to throw, and returns what was logged.
        /// The error comes from the mod's own method: a small game method would be inlined in release builds.
        /// </summary>
        private static List<string> TwoBrokenWeeks(System.Reflection.MethodBase weekly, Action postfix)
        {
            List<string> logged = new();
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            UnityEngine.Debug.unityLogger.logHandler = new Recorder(logged);
            Harmony standIn = new("tests.TraitFix.StandIn");
            try
            {
                standIn.Patch(weekly, prefix: new HarmonyMethod(typeof(WeeklyErrorTests), nameof(Broken)));
                postfix();
                postfix();
            }
            finally
            {
                standIn.UnpatchSelf();
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
            return logged;
        }

        [Fact]
        public void TraitRelationshipChanges_ErrorLoggedOnce()
        {
            List<string> logged = TwoBrokenWeeks(AccessTools.Method(typeof(Relationships_Do_Dynamic), "ApplyWeeklyTraitChanges"), Relationships_Do_Dynamic.Postfix);
            Assert.StartsWith("[Traits Fix] Couldn't apply this week's trait relationship changes", Assert.Single(logged));
        }

        [Fact]
        public void IndiscreetLeakRoll_ErrorLoggedOnce()
        {
            List<string> logged = TwoBrokenWeeks(AccessTools.Method(typeof(TraitsFix), nameof(TraitsFix.RollIndiscreetLeaks)), Data_girls_UpdateDatingStatus.Postfix);
            Assert.StartsWith("[Traits Fix] Couldn't roll this week's Indiscreet leaks", Assert.Single(logged));
        }

        private class Recorder : UnityEngine.ILogHandler
        {
            private readonly List<string> messages;
            public Recorder(List<string> messages) => this.messages = messages;
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => messages.Add(exception.ToString());
        }
    }
}
