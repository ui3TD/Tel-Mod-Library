using System;
using System.Linq;

namespace ConcertHype.Model
{
    /// <summary>
    /// How an idol's song went, as the game names a single's marketing roll.
    /// </summary>
    public enum Outcome
    {
        CriticalFailure,
        Failure,
        Success,
        CriticalSuccess,
    }

    /// <summary>
    /// The outcome is the band her roll lands in: under the accident threshold a critical failure, under par a
    /// failure, from the critical success roll up a critical success, a success otherwise. The bands only name the
    /// roll; the hype comes from the roll itself.
    /// </summary>
    public static class Outcomes
    {
        public const int Count = 4;

        /// <summary>
        /// The arcs of the game's marketing wheel from the pointer's 0°: success, critical success, critical
        /// failure, failure.
        /// </summary>
        public static readonly Outcome[] WheelOrder = { Outcome.Success, Outcome.CriticalSuccess, Outcome.CriticalFailure, Outcome.Failure };

        /// <summary>
        /// The share of its arc the wheel never lands in at either end, so it stops clearly inside the arc.
        /// </summary>
        private const double ArcMargin = 0.1;

        /// <summary>
        /// The lowest roll of each outcome after critical failure: failure, success, critical success.
        /// </summary>
        private static double[] Edges(Config config) => new[] { config.AccidentRollThreshold, config.Par, config.CriticalSuccessRoll };

        public static Outcome Of(Config config, double roll)
        {
            double[] edges = Edges(config);
            int band = 0;
            while (band < edges.Length && roll >= edges[band])
                band++;
            return (Outcome)band;
        }

        /// <summary>
        /// The chance of each outcome in percent, by <see cref="Outcome"/>, for a roll of this expected value and
        /// the config's spread. They add up to 100.
        /// </summary>
        public static double[] Chances(Config config, double expected)
        {
            double[] chances = new double[Count];
            if (config.RollSigma <= 0)
            {
                chances[(int)Of(config, expected)] = 100;
                return chances;
            }
            double below = 0;
            double[] edges = Edges(config);
            for (int band = 0; band < Count; band++)
            {
                double upTo = band < edges.Length ? Rng.NormalCdf((edges[band] - expected) / config.RollSigma) : 1;
                chances[band] = 100 * Math.Max(0, upTo - below);
                below = Math.Max(below, upTo);
            }
            return chances;
        }

        /// <summary>
        /// Where on the wheel (0 to 100, as the game's marketing roll) this roll lands: in its outcome's arc, as far
        /// into it as the roll is into its band's chance.
        /// </summary>
        public static double WheelPercent(Config config, double expected, double roll)
        {
            double[] chances = Chances(config, expected);
            Outcome outcome = Of(config, roll);
            double start = WheelOrder.TakeWhile(o => o != outcome).Sum(o => chances[(int)o]);
            double arc = chances[(int)outcome];

            double within = 0.5;
            if (config.RollSigma > 0 && arc > 1e-9)
            {
                double[] edges = Edges(config);
                int band = (int)outcome;
                double low = band == 0 ? 0 : Rng.NormalCdf((edges[band - 1] - expected) / config.RollSigma);
                within = (Rng.NormalCdf((roll - expected) / config.RollSigma) - low) / (arc / 100);
            }
            within = Engine.Clamp(within, ArcMargin, 1 - ArcMargin);
            return start + arc * within;
        }
    }
}
