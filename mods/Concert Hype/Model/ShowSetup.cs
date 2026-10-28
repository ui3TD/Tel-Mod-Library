using System.Collections.Generic;

namespace ConcertHype.Model
{
    /// <summary>
    /// The 7 fan types in the game's order (resources.fanType): male, female, casual, hardcore, teen, young adult, adult.
    /// </summary>
    public static class FanTypes
    {
        public const int Count = 7;

        /// <summary>
        /// The axes a lean is taken on: gender, commitment, age.
        /// </summary>
        public static readonly int[][] Axes = { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5, 6 } };
    }

    /// <summary>
    /// An idol in the show's cast.
    /// </summary>
    public sealed class Member
    {
        public string Name = "";
        public double Fans;

        /// <summary>
        /// (funny + smart) / 200, used when she hosts an MC.
        /// </summary>
        public double Talk;

        /// <summary>
        /// Her appeal to each fan type, from her stats.
        /// </summary>
        public double[] Appeal = new double[FanTypes.Count];

        /// <summary>
        /// False for an idol who can't perform at all in this show.
        /// </summary>
        public bool CanPerform = true;
    }

    /// <summary>
    /// A place in a song's formation.
    /// </summary>
    public sealed class Slot
    {
        /// <summary>
        /// The senbatsu position, 0 to 14; 0 is the center.
        /// </summary>
        public int Position;

        /// <summary>
        /// The row of the triangle, 0 to 4 (rows of 1, 2, 3, 4 and 5).
        /// </summary>
        public int Row;

        /// <summary>
        /// The cast member who holds this place in the single, or -1 when that idol isn't in the cast.
        /// </summary>
        public int Original = -1;

        public static int RowOf(int position) => position == 0 ? 0 : position <= 2 ? 1 : position <= 5 ? 2 : position <= 9 ? 3 : 4;
    }

    /// <summary>
    /// A song of the setlist.
    /// </summary>
    public sealed class Song
    {
        public string Title = "";
        public string Genre = "";
        public double[] Appeal = new double[FanTypes.Count];
        public List<Slot> Slots = new List<Slot>();

        /// <summary>
        /// A cast member who starts in the center slot as this song's substitution (the setup's center pick), or -1.
        /// </summary>
        public int PresetSubstitute = -1;
    }

    /// <summary>
    /// Everything a show starts from.
    /// </summary>
    public sealed class ShowSetup
    {
        public List<Member> Members = new List<Member>();
        public List<Song> Songs = new List<Song>();

        /// <summary>
        /// Skill[member][song]: her skill for that song, 0 to 100.
        /// </summary>
        public double[][] Skill = new double[0][];

        /// <summary>
        /// Tickets sold divided by the venue's capacity, 0 to 1.
        /// </summary>
        public double CapacityFilled = 1;

        /// <summary>
        /// Production quality, 0 to 1.
        /// </summary>
        public double Production = 0.5;

        /// <summary>
        /// The lowest hype the show can fall to.
        /// </summary>
        public double HypeFloor;

        /// <summary>
        /// Hype before the first song.
        /// </summary>
        public double StartingHype = 50;
    }
}
