namespace ConcertHype
{
    /// <summary>
    /// With the new show, hype no longer drives revenue: a concert pays what 200% hype paid in the game's formula,
    /// so only ticket sales matter.
    /// </summary>
    public static class Revenue
    {
        public const float FixedHype = 200f;

        /// <summary>
        /// Set by the revenue transpiler when it found its anchors; otherwise a postfix does the same job.
        /// </summary>
        internal static bool TranspilerApplied;

        /// <summary>
        /// The live show's concert, or a concert not yet held that would get the new show. A finished concert keeps
        /// the hype it was paid with.
        /// </summary>
        public static bool PaysFixedHype(SEvent_Concerts._concert concert)
        {
            if (concert == null)
                return false;
            if (ReferenceEquals(ShowSession.Current?.Concert, concert))
                return true;
            return concert.Status != SEvent_Tour.tour._status.finished && ShowSession.Qualifies(concert);
        }

        /// <summary>
        /// Replaces the concert's hype where the game's payout works out its hype multiplier.
        /// </summary>
        public static float RevenueHype(SEvent_Concerts._concert concert) => PaysFixedHype(concert) ? FixedHype : concert.Hype;

        /// <summary>
        /// The game's payout multiplier at 200% hype (SEvent_Concerts._concert.RecalcProjectedValues): linear at a
        /// club (x2.0), its diminishing curve elsewhere (x1.25). Only for the fallback when the transpiler can't apply.
        /// </summary>
        public static float MultiplierAtFixedHype(SEvent_Concerts._venue venue) => venue == SEvent_Concerts._venue.club ? 2f : 1.25f;
    }
}
