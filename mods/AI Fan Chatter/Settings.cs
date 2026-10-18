using System;
using System.Globalization;

namespace AIFanChatter
{
    public enum Provider
    {
        Anthropic = 0,
        OpenAI = 1,
        Compatible = 2
    }

    /// <summary>
    /// The mod's settings, saved by ModMenus in each save's variables. Without ModMenus nothing is saved
    /// and the defaults apply, which leave the mod off. Every default matches modmenu.json.
    /// </summary>
    public static class Settings
    {
        public const string Prefix = "AIFanChatter_";
        public const string EnabledVar = Prefix + "Enabled";
        public const string ProviderVar = Prefix + "Provider";
        public const string BaseUrlVar = Prefix + "BaseURL";
        public const string ModelVar = Prefix + "Model";
        public const string ApiKeyVar = Prefix + "APIKey";
        public const string PerWeekVar = Prefix + "PerWeek";

        public const bool DefaultEnabled = false;
        public const Provider DefaultProvider = Provider.Anthropic;
        public const int DefaultPerWeek = 2;
        public const int MaxPerWeek = 7;

        /// <summary>
        /// The model a blank Model setting uses on Anthropic. OpenAI and compatible services need one typed in.
        /// </summary>
        public const string DefaultAnthropicModel = "claude-haiku-5-5";

        /// <summary>
        /// Reads a saved setting; null when there is none. Tests replace it.
        /// </summary>
        internal static Func<string, string> Read = variables.Get;

        public static bool Enabled => ReadInt(EnabledVar, DefaultEnabled ? 1 : 0, 0, 1) != 0;

        public static Provider Provider => (Provider)ReadInt(ProviderVar, (int)DefaultProvider, 0, 2);

        public static int PerWeek => ReadInt(PerWeekVar, DefaultPerWeek, 0, MaxPerWeek);

        public static string BaseUrl => (Read(BaseUrlVar) ?? "").Trim();

        public static string Model => (Read(ModelVar) ?? "").Trim();

        /// <summary>
        /// A whole-number setting. ModMenus saves numbers as text with a dot decimal; anything missing,
        /// unreadable or out of range gives the default.
        /// </summary>
        internal static int ReadInt(string varID, int def, int min, int max)
        {
            if (!double.TryParse(Read(varID), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.IsNaN(value) || double.IsInfinity(value))
            {
                return def;
            }
            int whole = (int)Math.Floor(value);
            return whole < min || whole > max ? def : whole;
        }

        /// <summary>
        /// Everything a request needs, read once so a request doesn't mix settings from before and after an Apply.
        /// </summary>
        public static Connection Current()
        {
            Provider provider = Provider;
            return new Connection
            {
                Provider = provider,
                BaseUrl = BaseUrl,
                Model = Model,
                ApiKey = KeyStore.Get(provider)
            };
        }
    }

    public sealed class Connection
    {
        public Provider Provider;
        public string BaseUrl = "";
        public string Model = "";
        public string ApiKey;

        /// <summary>
        /// The model the request names: the setting, or Anthropic's default when it's blank.
        /// </summary>
        public string ResolvedModel => Model != "" ? Model : Provider == Provider.Anthropic ? Settings.DefaultAnthropicModel : "";

        /// <summary>
        /// The service as the player knows it: Anthropic, OpenAI, or for any other service its address
        /// (openrouter.ai, localhost:11434), never the internal name.
        /// </summary>
        public string ServiceName
        {
            get
            {
                switch (Provider)
                {
                    case Provider.Anthropic:
                        return "Anthropic";
                    case Provider.OpenAI:
                        return "OpenAI";
                    default:
                        return Uri.TryCreate((BaseUrl ?? "").Trim(), UriKind.Absolute, out Uri uri) && uri.Authority != ""
                            ? uri.Authority
                            : "your OpenAI-compatible service";
                }
            }
        }

        public string Describe() => ServiceName + " with " + (ResolvedModel != "" ? ResolvedModel : "no model");
    }
}
