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
    /// Keeps the API key out of the saves. ModMenus' Apply saves every setting through variables.Set; the
    /// key goes to its file instead and the save gets "". Runs first, so no other patch on Set sees the
    /// key. Any change to the mod's settings lets requests run again and tests the connection, and so does
    /// pressing Apply with nothing changed while requests are stopped.
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
                    changed = KeyStore.Apply(Settings.Provider, value, variables_Get.Shown);
                    variables_Get.Shown = false;
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
                else if (name == Settings.ApiKeyVar)
                {
                    // Apply saves every row, changed or not, and only Apply saves the key row: pressing it
                    // with nothing changed lifts a stop, so the player can try again after fixing the cause
                    Dispatcher.Instance.OnApplied();
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
    /// Shows that a key is saved without the key ever being in the variables: ModMenus fills each field from
    /// variables.Get, so while the chosen service has a key, the key row reads as the placeholder, which the
    /// masked field shows as asterisks. Only what Get returns changes; the save still holds "".
    /// </summary>
    [HarmonyPatch(typeof(variables), nameof(variables.Get))]
    [HarmonyPriority(Priority.Last)]
    public class variables_Get
    {
        /// <summary>The placeholder was handed out since the last Apply, so a blank key field was cleared on purpose.</summary>
        internal static bool Shown;

        public static void Postfix(string name, ref string __result)
        {
            if (name != Settings.ApiKeyVar || !string.IsNullOrEmpty(__result))
            {
                return;
            }
            try
            {
                if (KeyStore.Has(Settings.Provider))
                {
                    __result = KeyStore.SavedPlaceholder;
                    Shown = true;
                }
            }
            catch (Exception)
            {
                // The field shows nothing, as without the mod's help
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
    /// Moves keys saved before the shared key folder into it, from the main menu on. Releases queued
    /// posts as the game clock runs, from the same tick the game's own systems use. staticVars.SetTime
    /// isn't patched: it's a one-line setter the runtime may inline.
    /// </summary>
    [HarmonyPatch(typeof(mainScript), nameof(mainScript.Start))]
    public class mainScript_Start
    {
        public static void Postfix(mainScript __instance)
        {
            KeyStore.MoveLegacyKeys();
            try
            {
                if (!mainScript.IsMainMenu())
                {
                    __instance.onTimeTick += FeedRelease.OnTimeTick;
                    __instance.onTimeTick += ActiveDialogueController_Hide.OnTimeTick;
                    __instance.onTimeTick += SEvent_Concerts_concert_Finish.OnTimeTick;
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
            TabNotices.Clear();
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
            TabNotices.Clear();
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
