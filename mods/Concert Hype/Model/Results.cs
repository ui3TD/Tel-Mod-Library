using System;
using System.Collections.Generic;

namespace ConcertHype.Model
{
    /// <summary>
    /// What a song shares with the song before it.
    /// </summary>
    [Flags]
    public enum SongMatch
    {
        None = 0,
        Genre = 1,
        Lyrics = 2,
        Choreography = 4,
    }

    /// <summary>
    /// A song's hype change split by what made it, step by step through the hype rules, so the parts add up to the
    /// change. When the hype's floor or cap cuts the change, every part shrinks alike.
    /// </summary>
    public sealed class HypeParts
    {
        /// <summary>
        /// The lineup's performance against par.
        /// </summary>
        public double Performance;

        /// <summary>
        /// What a venue short of full takes off a gain; 0 or less.
        /// </summary>
        public double Venue;

        /// <summary>
        /// Flow, quality, appeal and novelty together.
        /// </summary>
        public double Production;

        public double Flow;
        public double Quality;
        public double Appeal;
        public double Novelty;

        public double Total => Performance + Venue + Production;
    }

    /// <summary>
    /// The parts of one idol's roll in one slot, without the random part.
    /// </summary>
    public sealed class RollParts
    {
        public double Skill;
        public double EnergyFactor;

        /// <summary>
        /// Roll points from her row's easier skill check.
        /// </summary>
        public double RowEase;

        /// <summary>
        /// Roll points for being the slot's original member.
        /// </summary>
        public double Senbatsu;

        /// <summary>
        /// The roll without noise: (skillBase + skillScale x skill) x energy factor + senbatsu + row ease.
        /// </summary>
        public double Raw;

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
        /// The hype steps at the expected performance, and the change they make split by what made it.
        /// </summary>
        public SongResult Steps;

        public HypeParts Parts;

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

        /// <summary>
        /// What the song shares with the song before it, and the hype points that adds before the multipliers.
        /// </summary>
        public SongMatch Matches;

        public double Flow;

        /// <summary>
        /// The change before the multipliers is a gain: the venue, quality and crowd multipliers apply.
        /// </summary>
        public bool Gain;

        /// <summary>
        /// As applied: 1 when the change isn't a gain.
        /// </summary>
        public double VenueMultiplier = 1;

        public double QualityMultiplier = 1;
        public double Crowd;

        /// <summary>
        /// How the song suits the lineup's fans; applied to gains only.
        /// </summary>
        public double CrowdMultiplier = 1;
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
        public List<int> Hosts = new List<int>();

        /// <summary>
        /// The hosts' talk together, 0 to 1.
        /// </summary>
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
