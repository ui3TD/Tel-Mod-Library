using AICore;
using HarmonyLib;
using System;
using System.Reflection;

namespace AIFanChatter
{
    /// <summary>
    /// The mod's settings, saved by ModMenus in each save's variables. Without ModMenus nothing is saved
    /// and the defaults apply, which leave the mod off. Every default matches modmenu.json. The service's
    /// settings are the shared AI code's, under this mod's prefix.
    /// </summary>
    public static class Settings
    {
        public const string Prefix = AIMod.SettingsPrefix;
        public const string EnabledVar = Prefix + "Enabled";
        public const string ProviderVar = ConnectionSettings.ProviderVar;
        public const string BaseUrlVar = ConnectionSettings.BaseUrlVar;
        public const string ModelVar = ConnectionSettings.ModelVar;
        public const string ApiKeyVar = ConnectionSettings.ApiKeyVar;
        public const string PerWeekVar = Prefix + "PerWeek";

        /// <summary>
        /// Whether the Base URL row is shown, for its enabledIf. No row has it, so it's never saved; the mod
        /// answers it in variables.Get (see UsesBaseUrl).
        /// </summary>
        public const string UsesBaseUrlVar = Prefix + "UsesBaseURL";

        public const bool DefaultEnabled = false;
        public const Provider DefaultProvider = ConnectionSettings.DefaultProvider;
        public const int DefaultPerWeek = 2;
        public const int MaxPerWeek = 21;

        /// <summary>
        /// The model a blank Model setting uses on OpenAI, the default service.
        /// </summary>
        public const string DefaultOpenAIModel = Providers.DefaultOpenAIModel;

        /// <summary>
        /// The model a blank Model setting uses on Anthropic. Other services need one typed in.
        /// </summary>
        public const string DefaultAnthropicModel = Providers.DefaultAnthropicModel;

        /// <summary>
        /// Reads a saved setting; null when there is none. Tests replace it.
        /// </summary>
        internal static Func<string, string> Read
        {
            get => ConnectionSettings.Read;
            set => ConnectionSettings.Read = value;
        }

        public static bool Enabled => ReadInt(EnabledVar, DefaultEnabled ? 1 : 0, 0, 1) != 0;

        public static Provider Provider => ConnectionSettings.Provider;

        /// <summary>
        /// ModMenus' MenuValue: what its open menu shows for a setting, applied or not; null while the menu is
        /// closed. Found by name on first use, when ModMenus is asking for UsesBaseUrlVar, so the mod also runs
        /// without ModMenus. Null without ModMenus 4.2.0 or later. Tests replace it.
        /// </summary>
        internal static Func<string, string> MenuValue = varID => ModMenusValue.Value?.Invoke(varID);

        private static readonly Lazy<Func<string, string>> ModMenusValue = new(() =>
        {
            MethodInfo method = AccessTools.Method("ModMenus.ModMenusUtils:MenuValue", new[] { typeof(string) });
            return method != null && method.ReturnType == typeof(string)
                ? (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), method)
                : null;
        });

        /// <summary>
        /// Whether the Base URL row is shown: while OpenAI-compatible is picked, as the open mod menu shows it, so
        /// the row follows the player's pick before Apply; while the menu is closed, as saved.
        /// </summary>
        public static bool UsesBaseUrl => ConnectionSettings.ProviderOf(MenuValue(ProviderVar) ?? Read(ProviderVar)) == Provider.Compatible;

        public static int PerWeek => ReadInt(PerWeekVar, DefaultPerWeek, 0, MaxPerWeek);

        public static string BaseUrl => ConnectionSettings.BaseUrl;

        public static string Model => ConnectionSettings.Model;

        /// <summary>
        /// A whole-number setting. ModMenus saves numbers as text with a dot decimal; anything missing,
        /// unreadable or out of range gives the default.
        /// </summary>
        internal static int ReadInt(string varID, int def, int min, int max) => ConnectionSettings.ReadInt(varID, def, min, max);

        /// <summary>
        /// Everything a request needs, read once so a request doesn't mix settings from before and after an Apply.
        /// </summary>
        public static Connection Current() => ConnectionSettings.Current();
    }
}
