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
    /// Keeps the API key out of the saves. ModMenus' Apply saves every setting through variables.Set; the
    /// key goes to its file instead and the save gets "". Runs first, so no other patch on Set sees the
    /// key. Any change to the mod's settings lets requests run again and tests the connection.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Set))]
    [HarmonyPriority(Priority.First)]
    public class variables_Set
    {
        public static void Prefix(string name, ref string value)
        {
            if (name == null || !name.StartsWith(Settings.Prefix, StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                bool changed;
                if (name == Settings.ApiKeyVar)
                {
                    // The provider row comes before the key row, so Apply has already saved the provider
                    changed = KeyStore.Apply(Settings.Provider, value);
                    value = "";
                }
                else
                {
                    changed = name != FeedStore.OlderPostsVar && name != FeedQueue.QueueVar && variables.Get(name) != value;
                }
                if (changed)
                {
                    Dispatcher.Instance.OnSettingsChanged();
                }
            }
            catch (Exception e)
            {
                if (name == Settings.ApiKeyVar)
                {
                    value = "";
                }
                Log.Warn("Couldn't apply setting " + name + ": " + e.Message);
            }
        }
    }

    /// <summary>
    /// A backstop for a key that reached the variables some other way than variables.Set: it moves to the
    /// key file before the save is written.
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.SaveFunction))]
    public class variables_SaveFunction
    {
        public static void Postfix()
        {
            try
            {
                KeyStore.Scrub(variables.variable, Settings.Provider);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't check the saved settings for a key: " + e.Message);
            }
        }
    }

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

    // The moment patches run last, so they read the values after other mods' postfixes changed them, and
    // check that the moment really happened, in case another mod's prefix skipped the game's method.

    [HarmonyPatch(typeof(singles), nameof(singles.ReleaseSingle))]
    [HarmonyPriority(Priority.Last)]
    public class singles_ReleaseSingle
    {
        public static void Postfix(singles._single single, bool __runOriginal)
        {
            if (__runOriginal && single != null && single.status == singles._single._status.released)
            {
                Moments.Trigger(() => MomentContext.Single(single));
            }
        }
    }

    [HarmonyPatch(typeof(SEvent_Concerts._concert), nameof(SEvent_Concerts._concert.Finish))]
    [HarmonyPriority(Priority.Last)]
    public class SEvent_Concerts_concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance, bool __runOriginal)
        {
            if (__runOriginal && __instance != null && __instance.Status == SEvent_Tour.tour._status.finished)
            {
                Moments.Trigger(() => MomentContext.Concert(__instance));
            }
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.Graduate))]
    [HarmonyPriority(Priority.Last)]
    public class data_girls_girls_Graduate
    {
        /// <summary>
        /// Her fans before graduating: the game moves them to other idols as she leaves.
        /// </summary>
        public static void Prefix(data_girls.girls __instance, out long __state)
        {
            __state = 0;
            try
            {
                __state = __instance.GetFans_Total();
            }
            catch (Exception)
            {
                // The fan count is left out
            }
        }

        public static void Postfix(data_girls.girls __instance, long __state, bool __runOriginal)
        {
            if (__runOriginal && __instance != null && __instance.status == data_girls._status.graduated
                && Moments.FirstGraduationToday(staticVars.dateTime))
            {
                Moments.Trigger(() => MomentContext.Graduation(__instance, __state));
            }
        }
    }

    /// <summary>
    /// Random events, scandals included. Events that already post on the feed are left to the game.
    /// </summary>
    [HarmonyPatch(typeof(Event_Manager), nameof(Event_Manager.ConcludeEvent))]
    [HarmonyPriority(Priority.Last)]
    public class Event_Manager_ConcludeEvent
    {
        /// <summary>
        /// The event being answered; the game may mark it complete during the method.
        /// </summary>
        public static void Prefix(Event_Manager __instance, out Event_Manager._activeEvent __state)
        {
            __state = null;
            try
            {
                __state = __instance.GetActiveEvent();
            }
            catch (Exception)
            {
                // No event, no request
            }
        }

        public static void Postfix(Event_Manager._randomEvent._reply reply, Event_Manager._activeEvent __state, bool __runOriginal)
        {
            if (ShouldPost(reply, __state, __runOriginal))
            {
                Moments.Trigger(() => MomentContext.Event(__state, reply));
            }
        }

        internal static bool ShouldPost(Event_Manager._randomEvent._reply reply, Event_Manager._activeEvent active, bool ran)
        {
            return ran && reply != null && active != null && (reply.SNS == null || reply.SNS.Count == 0);
        }
    }

    /// <summary>
    /// Releases queued posts as the game clock runs, from the same tick the game's own systems use.
    /// staticVars.SetTime isn't patched: it's a one-line setter the runtime may inline.
    /// </summary>
    [HarmonyPatch(typeof(mainScript), nameof(mainScript.Start))]
    public class mainScript_Start
    {
        public static void Postfix(mainScript __instance)
        {
            try
            {
                if (!mainScript.IsMainMenu())
                {
                    __instance.onTimeTick += FeedRelease.OnTimeTick;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't follow the game clock; queued posts won't appear: " + e.Message);
            }
        }
    }

    /// <summary>
    /// A new game or scene starts with an empty feed, so the queue empties too.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.Awake))]
    public class SNS_Manager_Awake
    {
        public static void Postfix()
        {
            FeedQueue.Clear();
            FeedRelease.NewSession();
            Moments.ResetGraduations();
        }
    }

    /// <summary>
    /// Raises the feed's limit from 20 posts to 100 by changing only the 20 in "if (Messages.Count == 20)".
    /// When that check isn't there exactly once, another mod has already changed the method, and it's
    /// left as it is.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.AddMessage))]
    public class SNS_Manager_AddMessage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return RaiseCap(instructions.ToList(), out _);
        }

        internal static List<CodeInstruction> RaiseCap(List<CodeInstruction> code, out bool changed)
        {
            changed = false;
            FieldInfo messages = AccessTools.Field(typeof(SNS_Manager), nameof(SNS_Manager.Messages));
            MethodInfo count = AccessTools.PropertyGetter(typeof(List<SNS_Manager._message>), nameof(List<SNS_Manager._message>.Count));
            List<int> matches = new();
            for (int i = 0; i + 2 < code.Count; i++)
            {
                if (code[i].LoadsField(messages) && code[i + 1].Calls(count) && code[i + 2].LoadsConstant(FeedStore.VanillaCap))
                {
                    matches.Add(i + 2);
                }
            }
            if (matches.Count != 1)
            {
                Log.Warn("The feed's limit check isn't the game's own (another mod changed it), so the feed keeps that limit.");
                return code;
            }
            CodeInstruction limit = code[matches[0]];
            code[matches[0]] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FeedStore), nameof(FeedStore.CapValue)))
            {
                labels = limit.labels,
                blocks = limit.blocks
            };
            changed = true;
            return code;
        }
    }

    /// <summary>
    /// Saves the newest 20 posts where the game saves them, the older ones and the queue in the mod's variables.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.SaveFunction))]
    [HarmonyPriority(Priority.Last)]
    public class SNS_Manager_SaveFunction
    {
        public static void Postfix()
        {
            try
            {
                SaveManager.SavedData data = Camera.main.GetComponent<mainScript>().GetSavedData();
                data.SNS_Manager__Messages = SaveFeed(data.SNS_Manager__Messages, variables.variable);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't save the older posts and the queue: " + e.Message);
            }
        }

        /// <summary>
        /// The list for the game's own field. It splits what's in the field now, so another mod's change to it is kept.
        /// Variables go in the game's live list, which is the one the save stores, whichever event runs first.
        /// </summary>
        internal static List<SNS_Manager._message> SaveFeed(List<SNS_Manager._message> saved, List<variables._variable> vars)
        {
            List<SNS_Manager._message> kept = FeedStore.SplitForSave(saved, out List<SNS_Manager._message> older);
            FeedStore.SetVariable(vars, FeedStore.OlderPostsVar, older == null ? null : FeedStore.Serialize(older));
            FeedStore.SetVariable(vars, FeedQueue.QueueVar, FeedQueue.Items.Count == 0 ? null : FeedQueue.Serialize());
            return kept;
        }
    }

    /// <summary>
    /// Puts the older posts back in front of the loaded feed and reloads the queue.
    /// </summary>
    [HarmonyPatch(typeof(SNS_Manager), nameof(SNS_Manager.LoadFunction))]
    public class SNS_Manager_LoadFunction
    {
        public static void Postfix(SNS_Manager __instance)
        {
            try
            {
                FeedRelease.NewSession();
                // Read from the save itself: the variables may not be loaded yet
                SaveManager.SavedData data = Camera.main.GetComponent<mainScript>().GetSavedData();
                if (LoadFeed(SNS_Manager.Messages, data.variables__variables))
                {
                    __instance.RenderMessages();
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't load the older posts and the queue: " + e.Message);
            }
        }

        /// <summary>
        /// Returns whether the feed changed.
        /// </summary>
        internal static bool LoadFeed(List<SNS_Manager._message> feed, List<variables._variable> vars)
        {
            FeedQueue.Load(FeedStore.GetVariable(vars, FeedQueue.QueueVar));
            if (feed == null)
            {
                return false;
            }
            int before = feed.Count;
            int added = FeedStore.MergeOnLoad(feed, FeedStore.Deserialize(FeedStore.GetVariable(vars, FeedStore.OlderPostsVar)));
            FeedStore.TrimToCap(feed);
            return added > 0 || feed.Count != before;
        }
    }

    /// <summary>
    /// Keeps the notification of a later post in a batch from showing: only its Type changes, to the one
    /// the game never shows. The game shows that type only when the player has turned every notification
    /// off, and then it's left alone.
    /// </summary>
    [HarmonyPatch(typeof(NotificationManager), nameof(NotificationManager.AddNotification),
        new[] { typeof(string), typeof(Color32), typeof(NotificationManager._notification._type) })]
    public class NotificationManager_AddNotification
    {
        public static void Prefix(ref NotificationManager._notification._type Type)
        {
            if (!FeedRelease.Quiet)
            {
                return;
            }
            try
            {
                Type = QuietType(Type, staticVars.Settings?.notification_enabled);
            }
            catch (Exception)
            {
                // The notification shows
            }
        }

        internal static NotificationManager._notification._type QuietType(NotificationManager._notification._type type, List<NotificationManager._notification._type> enabled)
        {
            return enabled != null && enabled.Contains(NotificationManager._notification._type.none) ? type : NotificationManager._notification._type.none;
        }
    }
}
