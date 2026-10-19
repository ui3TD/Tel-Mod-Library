using System;
using System.Globalization;

namespace AICore
{
    /// <summary>
    /// What each AI mod fills in about itself, in a file of its own that completes this class. A mod that
    /// leaves out a value doesn't build.
    /// </summary>
    internal static partial class AIMod
    {
    }

    /// <summary>
    /// The settings for the AI service, saved by ModMenus in each save's variables under the mod's prefix.
    /// Without ModMenus nothing is saved and the defaults apply.
    /// </summary>
    public static class ConnectionSettings
    {
        public const string ProviderVar = AIMod.SettingsPrefix + "Provider";
        public const string BaseUrlVar = AIMod.SettingsPrefix + "BaseURL";
        public const string ModelVar = AIMod.SettingsPrefix + "Model";
        public const string ApiKeyVar = AIMod.SettingsPrefix + "APIKey";

        public const Provider DefaultProvider = Provider.OpenAI;

        /// <summary>
        /// Reads a saved setting; null when there is none. Tests replace it.
        /// </summary>
        internal static Func<string, string> Read = variables.Get;

        public static Provider Provider => (Provider)ReadInt(ProviderVar, (int)DefaultProvider, 0, 2);

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
        /// The model the request names: the setting, or OpenAI's or Anthropic's default when it's blank.
        /// </summary>
        public string ResolvedModel => Model != "" ? Model
            : Provider == Provider.OpenAI ? Providers.DefaultOpenAIModel
            : Provider == Provider.Anthropic ? Providers.DefaultAnthropicModel
            : "";

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
