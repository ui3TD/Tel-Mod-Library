using System.Collections.Generic;

namespace ConcertHype.Model
{
    /// <summary>
    /// The parts of one idol's roll in one slot, without the random part.
    /// </summary>
    public sealed class RollParts
    {
        public double Skill;
        public double EnergyFactor;

        /// <summary>
        /// Roll points from appeal alignment (kAppeal x alignment).
        /// </summary>
        public double Appeal;

        /// <summary>
        /// Roll points for being the slot's original member.
        /// </summary>
        public double Senbatsu;

        /// <summary>
        /// The roll without noise, floored at 0.
        /// </summary>
        public double Expected;
    }

    /// <summary>
    /// What a lineup is expected to do, before the song is played.
    /// </summary>
    public sealed class SongPreview
    {
        /// <summary>
        /// Per slot; null for an empty slot.
        /// </summary>
        public RollParts[] Slots;

        public bool[] Forced;
        public double Novelty;
        public double Weighted;

        /// <summary>
        /// The expected hype change, and the change one standard deviation of the lineup's performance either side.
        /// </summary>
        public double Change;

        public double Low;
        public double High;
    }

    public sealed class SlotResult
    {
        public int Member = -1;
        public RollParts Parts;
        public double Roll;
        public double AccidentChance;
        public bool Accident;
        public int AbsenceLength;
        public double EnergyBefore;
        public double EnergyAfter;
    }

    /// <summary>
    /// How one song went, step by step.
    /// </summary>
    public sealed class SongResult
    {
        public int SongIndex;
        public string Title = "";
        public SlotResult[] Slots;
        public double HypeBefore;
        public double Weighted;
        public double BaseChange;
        public double GenreFlow;
        public double ShowMultiplier = 1;
        public double Novelty;
        public double Amplification = 1;

        /// <summary>
        /// The hype change after clamping: HypeAfter - HypeBefore.
        /// </summary>
        public double Change;

        public double HypeAfter;
        public int EmptyFills;
        public List<int> Exhausted = new List<int>();
        public List<int> Returned = new List<int>();
    }

    public sealed class McResult
    {
        public int Host;
        public double Talk;
        public double HypeBefore;
        public double HypeAfter;
    }

    public sealed class Score
    {
        public double Total;
        public double Average;
        public double Peak;
        public double Finale;
    }
}
