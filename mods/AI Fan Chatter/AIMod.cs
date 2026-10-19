namespace AICore
{
    /// <summary>
    /// AI Fan Chatter's values for the shared AI code.
    /// </summary>
    internal static partial class AIMod
    {
        /// <summary>The name in log lines, notifications and the request thread's name.</summary>
        public const string Name = "AI Fan Chatter";

        /// <summary>The start of the mod's ModMenus variables.</summary>
        public const string SettingsPrefix = "AIFanChatter_";

        /// <summary>The key folder the mod used before the shared one, whose keys move over; null for none.</summary>
        public const string LegacyKeyFolder = "AI Fan Chatter";

        /// <summary>The reply schema's name, which OpenAI and compatible services require.</summary>
        public const string SchemaName = "fan_posts";

        public const string RefusalText = "The model declined to write the posts.";
    }
}
