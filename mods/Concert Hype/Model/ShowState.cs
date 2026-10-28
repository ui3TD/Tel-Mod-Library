using System.Collections.Generic;

namespace ConcertHype.Model
{
    /// <summary>
    /// A show in progress.
    /// </summary>
    public sealed class ShowState
    {
        public ShowSetup Setup;
        public Config Config;
        public Rng Rng;

        /// <summary>
        /// Energy per cast member, 0 to 100; 100 is fresh.
        /// </summary>
        public double[] Energy;

        /// <summary>
        /// Songs (time units) until she can perform again; 0 is available.
        /// </summary>
        public int[] Absence;

        /// <summary>
        /// She reached 0 energy and hasn't recovered to the return threshold yet.
        /// </summary>
        public bool[] Exhausted;

        /// <summary>
        /// Alignment[member][song]: how her appeal leans the same way as the song's, -1 to 1.
        /// </summary>
        public double[][] Alignment;

        /// <summary>
        /// Fan weight per member: her share of the cast's average fans, never below the floor.
        /// </summary>
        public double[] FanWeight;

        public double Hype;

        /// <summary>
        /// Hype after each song played. MC breaks add nothing.
        /// </summary>
        public List<double> History = new List<double>();

        public int McCharges;

        /// <summary>
        /// An MC was called in the gap before the next song.
        /// </summary>
        public bool McInThisGap;

        /// <summary>
        /// The next song to play; equal to the song count once the show is over.
        /// </summary>
        public int SongIndex;

        public string PreviousGenre;

        /// <summary>
        /// For the balance report: accidents so far, and forced fills that found nobody.
        /// </summary>
        public int Accidents;

        public int EmptyFills;

        public bool Finished => SongIndex >= Setup.Songs.Count;

        public Song CurrentSong => Finished ? null : Setup.Songs[SongIndex];

        public bool IsAvailable(int member) =>
            member >= 0 && member < Energy.Length && Setup.Members[member].CanPerform && Absence[member] == 0 && !Exhausted[member];
    }
}
