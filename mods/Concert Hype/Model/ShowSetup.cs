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
        /// The axes every fan belongs to one type of: gender, commitment, age.
        /// </summary>
        public static readonly int[][] Axes = { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5, 6 } };

        /// <summary>
        /// Each type's share of a fanbase split evenly: half of each gender and commitment, a third of each age.
        /// </summary>
        public static double[] EvenMix()
        {
            double[] mix = new double[Count];
            foreach (int[] axis in Axes)
            {
                foreach (int i in axis)
                    mix[i] = 1.0 / axis.Length;
            }
            return mix;
        }
    }

    /// <summary>
    /// An idol in the show's cast.
    /// </summary>
    public sealed class Member
    {
        public string Name = "";
        public double Fans;

        /// <summary>
        /// Her group's ID, or -1 when she's in none.
        /// </summary>
        public int Group = -1;

        /// <summary>
        /// (funny + smart) / 200, used when she hosts an MC.
        /// </summary>
        public double Talk;

        /// <summary>
        /// Each fan type's share of her fans, 0 to 1; even when she has none.
        /// </summary>
        public double[] FanMix = FanTypes.EvenMix();

        /// <summary>
        /// False for an idol who can't perform at all in this show.
        /// </summary>
        public bool CanPerform = true;

        /// <summary>
        /// She is in one of the setlist's singles or a setup center pick; false for the rest of the agency, who
        /// can be brought in from the bench.
        /// </summary>
        public bool InSetlist = true;
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

        /// <summary>
        /// The single's genre, lyrics and choreography titles; empty when it has none.
        /// </summary>
        public string Genre = "", Lyrics = "", Choreography = "";

        /// <summary>
        /// The single's appeal to each fan type, as saved when it was released; 0 where it has none.
        /// </summary>
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
        /// The agency's average released single's appeal to each fan type: a song's crowd appeal is measured
        /// against it, so a typical single leaves the hype as it is.
        /// </summary>
        public double[] AverageSongAppeal = new double[FanTypes.Count];

        /// <summary>
        /// Skill[member][song]: her skill for that song, 0 to 100.
        /// </summary>
        public double[][] Skill = new double[0][];

        /// <summary>
        /// Tickets sold divided by the venue's capacity, 0 to 1.
        /// </summary>
        public double CapacityFilled = 1;

        /// <summary>
        /// The venue's capacity in seats; 0 when unknown.
        /// </summary>
        public int Seats;

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
