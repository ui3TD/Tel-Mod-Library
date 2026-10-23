using AICore;
using System;

namespace AIFanChatter
{
    /// <summary>
    /// For other mods: fan posts about their own events. Every argument is a plain .NET type, so a mod can
    /// find Trigger by name (AccessTools.Method("AIFanChatter.ModMoments:Trigger")) and run without AI Fan
    /// Chatter installed. The player's limits apply as to every other moment. docs/AIFanChatterForModders.md
    /// explains it for modders.
    /// </summary>
    public static class ModMoments
    {
        /// <summary>What separates a fact's name from its value: "Ride: the Starlight Coaster".</summary>
        public const string FactSeparator = ": ";
        public const int MaxFacts = 20;
        /// <summary>The longest kind, label or fact line, so one mod's event can't fill the prompt.</summary>
        public const int MaxLength = 200;

        /// <summary>
        /// Asks for posts about another mod's event. Returns null when the request started, or why it didn't, in
        /// plain words. It never throws.
        /// </summary>
        /// <param name="kind">What happened, in plain words and any language: "the group opened a theme park". Posts are in the player's language either way.</param>
        /// <param name="label">What the player's notices call it: "the theme park opening". Null or "" is the kind.</param>
        /// <param name="facts">The event's details, each "Name: value". Null is none.</param>
        /// <param name="idolIds">The game's IDs of the idols it's about (data_girls.girls.id). Null is none.</param>
        public static string Trigger(string kind, string label, string[] facts, int[] idolIds)
        {
            try
            {
                string problem = Problem(kind, label, facts);
                if (problem != null)
                {
                    Log.Warn("Another mod's event was left out: " + problem);
                    return problem;
                }
                Dispatcher dispatcher = Dispatcher.Instance;
                string blocked = dispatcher.Blocked();
                if (blocked != null)
                {
                    return blocked;
                }
                return dispatcher.Request(MomentContext.ModEvent(kind, label, facts, idolIds)) ? null : "the request didn't start";
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't start a request for another mod's event: " + e);
                return "AI Fan Chatter couldn't start the request: " + e.Message;
            }
        }

        /// <summary>
        /// What's wrong with an event's text, or null when nothing is.
        /// </summary>
        internal static string Problem(string kind, string label, string[] facts)
        {
            if (Moment.Clean(kind) == "")
            {
                return "the kind is empty";
            }
            if (kind.Length > MaxLength)
            {
                return "the kind is longer than " + MaxLength + " characters";
            }
            if (label != null && label.Length > MaxLength)
            {
                return "the label is longer than " + MaxLength + " characters";
            }
            if (facts == null)
            {
                return null;
            }
            if (facts.Length > MaxFacts)
            {
                return "there are more than " + MaxFacts + " facts";
            }
            foreach (string fact in facts)
            {
                if (fact == null)
                {
                    return "a fact is null";
                }
                if (fact.Length > MaxLength)
                {
                    return "a fact is longer than " + MaxLength + " characters: " + fact.Substring(0, 40) + "...";
                }
                if (!IsFact(fact))
                {
                    return "a fact isn't \"Name" + FactSeparator + "value\": " + fact;
                }
            }
            return null;
        }

        private static bool IsFact(string fact)
        {
            int split = fact.IndexOf(FactSeparator, StringComparison.Ordinal);
            return split > 0 && Moment.Clean(fact.Substring(0, split)) != "" && Moment.Clean(fact.Substring(split + FactSeparator.Length)) != "";
        }
    }
}
