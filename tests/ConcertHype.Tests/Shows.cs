using ConcertHype.Model;
using System.Linq;

namespace ConcertHype.Tests
{
    /// <summary>
    /// Small hand-made shows for the rule tests.
    /// </summary>
    public static class Shows
    {
        /// <summary>
        /// A config with no randomness in the rolls and no accidents, so a test sees each rule alone.
        /// </summary>
        public static Config Quiet()
        {
            return new Config { RollSigma = 0, PBase = 0, PRoll = 0, PTired = 0 };
        }

        /// <summary>
        /// Members with equal fans and flat appeal (alignment 0); each song's slots are positions 0.., held by
        /// members 0.. in order. Every member has the same skill in every song.
        /// </summary>
        public static ShowSetup Setup(int members = 8, int slots = 3, int songs = 3, double skill = 60)
        {
            ShowSetup setup = new ShowSetup
            {
                Members = Enumerable.Range(0, members).Select(m => new Member { Name = "M" + m, Fans = 100, Talk = 0.5 }).ToList(),
                Songs = Enumerable.Range(0, songs).Select(s => new Song
                {
                    Title = "Song " + s,
                    Genre = "Genre " + s,
                    Slots = Enumerable.Range(0, slots).Select(j => new Slot { Position = j, Row = Slot.RowOf(j), Original = j }).ToList(),
                }).ToList(),
                CapacityFilled = 1,
                Production = 0.5,
                StartingHype = 50,
            };
            setup.Skill = Enumerable.Range(0, members).Select(_ => Enumerable.Repeat(skill, songs).ToArray()).ToArray();
            return setup;
        }

        public static ShowState Start(ShowSetup setup, Config config = null, int seed = 1) =>
            Engine.Start(setup, config ?? Quiet(), seed);

        /// <summary>
        /// Every member gets this skill in every song.
        /// </summary>
        public static void SetSkill(ShowSetup setup, int member, double skill)
        {
            for (int s = 0; s < setup.Songs.Count; s++)
                setup.Skill[member][s] = skill;
        }
    }
}
