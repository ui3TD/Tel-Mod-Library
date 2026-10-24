using AICore;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace AIFanChatter
{
    /// <summary>
    /// The API keys are ModMenus' secret rows (ModMenus 5.0.0 and later), kept in the key files KeyStore
    /// reads. ModMenus tells mods which secrets an Apply changed; a changed key is a settings change, so
    /// requests may run again and the connection is tested. Found by name, so the mod also runs without
    /// ModMenus.
    /// </summary>
    internal static class ModMenusSecrets
    {
        public const string ModMenusType = "ModMenus.ModMenusUtils";
        public const string EventName = "SecretsChanged";

        public const string UpdateNotice = "Update ModMenus to 5.0.0 or later to change your API key. Your saved key still works.";

        private static bool listening;
        private static bool noticeShown;

        /// <summary>
        /// Starts listening, once. With a ModMenus too old to have secret rows, the key can't be changed: the
        /// player is told once a game is open, not on the main menu.
        /// </summary>
        public static void Listen()
        {
            if (listening)
            {
                return;
            }
            try
            {
                Type modMenus = AccessTools.TypeByName(ModMenusType);
                if (TryListen(modMenus?.GetEvent(EventName, BindingFlags.Public | BindingFlags.Static)))
                {
                    return;
                }
                if (modMenus != null && !noticeShown && !mainScript.IsMainMenu())
                {
                    noticeShown = true;
                    Log.Warn(UpdateNotice);
                    TabNotices.Add(UpdateNotice, NoticeKind.Missed);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't listen for API keys changed in ModMenus: " + e.Message);
            }
        }

        /// <summary>
        /// Adds OnSecretsChanged to ModMenus' event, if it's the one this mod knows. Returns whether it did.
        /// </summary>
        internal static bool TryListen(EventInfo secretsChanged)
        {
            if (secretsChanged == null || secretsChanged.EventHandlerType != typeof(Action<string[]>))
            {
                return false;
            }
            secretsChanged.AddEventHandler(null, new Action<string[]>(OnSecretsChanged));
            listening = true;
            return true;
        }

        internal static void OnSecretsChanged(string[] secretIDs)
        {
            try
            {
                if (secretIDs != null && secretIDs.Any(KeyStore.IsKeySecret))
                {
                    Dispatcher.Instance.OnSettingsChanged();
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't apply the changed API key: " + e.Message);
            }
        }

        internal static void Reset()
        {
            listening = false;
            noticeShown = false;
        }
    }
}
