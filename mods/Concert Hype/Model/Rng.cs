using System;

namespace ConcertHype.Model
{
    /// <summary>
    /// The show's one random number generator. Every draw goes through here in a fixed order, so the same seed and
    /// the same decisions replay the same show. It never touches UnityEngine.Random.
    /// </summary>
    public sealed class Rng
    {
        private readonly Random random;

        public Rng(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Seed { get; }

        /// <summary>
        /// A number in [0, 1).
        /// </summary>
        public double Uniform() => random.NextDouble();

        /// <summary>
        /// A normal draw (Box-Muller); always uses two uniforms, so the draw count doesn't depend on earlier values.
        /// </summary>
        public double Normal(double sigma)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// A whole number from min to max, both included.
        /// </summary>
        public int Range(int min, int max) => max <= min ? min : random.Next(min, max + 1);
    }
}
