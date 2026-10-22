namespace AIFanChatter
{
    /// <summary>The reason fans hear for a hiatus.</summary>
    public enum HiatusReason
    {
        None,
        Injury,
        Health,
    }

    /// <summary>
    /// A hiatus's reason by the status she had as it started: the game's status before it, kept during the
    /// hiatus as previous_status, so the end reads the same reason, also after a reload.
    /// </summary>
    internal static class HiatusReasons
    {
        public static HiatusReason From(data_girls._status status) => status switch
        {
            data_girls._status.injured => HiatusReason.Injury,
            // Depression is announced as health reasons: mental health is private
            data_girls._status.depressed => HiatusReason.Health,
            _ => HiatusReason.None,
        };

        public static string Text(HiatusReason reason) => reason switch
        {
            HiatusReason.Injury => "an injury",
            HiatusReason.Health => "health reasons, no details given",
            _ => "no reason given",
        };
    }
}
