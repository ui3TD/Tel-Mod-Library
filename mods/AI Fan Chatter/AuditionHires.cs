using System.Collections.Generic;

namespace AIFanChatter
{
    /// <summary>
    /// The audition the player launched and paid for, from the moment its candidates are shown until its
    /// popup closes, and the idols hired from it. Story auditions (the rival group, Aya, the copycat) never
    /// start one. The mod's own notes; the game's state is only read.
    /// </summary>
    internal static class AuditionHires
    {
        public sealed class Audition
        {
            public Auditions.type Scale;
            public readonly List<data_girls.girls> Hired = new();
        }

        public static Audition Pending;

        public static void Start(Auditions.type scale) => Pending = new Audition { Scale = scale };

        public static void Clear() => Pending = null;

        /// <summary>
        /// The pending audition, now over; null when there was none.
        /// </summary>
        public static Audition Take()
        {
            Audition audition = Pending;
            Pending = null;
            return audition;
        }
    }
}
