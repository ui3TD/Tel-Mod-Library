using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcertHype.Tests.Balance
{
    /// <summary>
    /// The spec's batch-mode players. Their own decisions use a separate random generator, so the show's draws stay
    /// in the show's order.
    /// </summary>
    public abstract class Policy
    {
        public abstract string Name { get; }

        /// <summary>
        /// Before each song: maybe call an MC (return the host, or -1), then pick the lineup.
        /// </summary>
        public virtual int ChooseMcHost(ShowState state, Random random) => -1;

        public abstract int[] ChooseLineup(ShowState state, Random random);

        /// <summary>
        /// The default lineup and every legal single substitution of it.
        /// </summary>
        public static IEnumerable<int[]> Candidates(ShowState state)
        {
            int[] lineup = Engine.DefaultLineup(state);
            yield return lineup;
            bool[] forced = Engine.ForcedSlots(state);
            for (int j = 0; j < lineup.Length; j++)
            {
                if (forced[j])
                    continue;
                for (int m = 0; m < state.Setup.Members.Count; m++)
                {
                    if (lineup.Contains(m) || !state.IsAvailable(m))
                        continue;
                    int[] candidate = (int[])lineup.Clone();
                    candidate[j] = m;
                    if (Engine.Validate(state, candidate) == null)
                        yield return candidate;
                }
            }
        }

        protected static int BestTalker(ShowState state) =>
            Enumerable.Range(0, state.Setup.Members.Count).Where(state.IsAvailable)
                .OrderByDescending(m => state.Setup.Members[m].Talk).DefaultIfEmpty(-1).First();
    }

    public sealed class OriginalsOnly : Policy
    {
        public override string Name => "Originals only";

        public override int[] ChooseLineup(ShowState state, Random random) => Engine.DefaultLineup(state);
    }

    public class Greedy : Policy
    {
        public override string Name => "Greedy";

        public override int ChooseMcHost(ShowState state, Random random) =>
            Engine.CanCallMc(state) == null && state.Hype < 70 ? BestTalker(state) : -1;

        public override int[] ChooseLineup(ShowState state, Random random) =>
            Candidates(state).OrderByDescending(lineup => Engine.Preview(state, lineup).Change).First();
    }

    public sealed class RandomPlayer : Policy
    {
        public override string Name => "Random";

        public override int ChooseMcHost(ShowState state, Random random)
        {
            if (Engine.CanCallMc(state) != null || random.NextDouble() >= 0.3)
                return -1;
            List<int> hosts = Enumerable.Range(0, state.Setup.Members.Count).Where(state.IsAvailable).ToList();
            return hosts.Count == 0 ? -1 : hosts[random.Next(hosts.Count)];
        }

        public override int[] ChooseLineup(ShowState state, Random random)
        {
            List<int[]> candidates = Candidates(state).ToList();
            if (candidates.Count == 1 || random.NextDouble() < 0.5)
                return candidates[0];
            return candidates[1 + random.Next(candidates.Count - 1)];
        }
    }

    /// <summary>
    /// The weakest expected lineup for the first third of the show, then Greedy.
    /// </summary>
    public sealed class Sandbagger : Greedy
    {
        public override string Name => "Sandbagger";

        private static bool Sandbagging(ShowState state) => state.SongIndex < state.Setup.Songs.Count / 3;

        public override int ChooseMcHost(ShowState state, Random random) => Sandbagging(state) ? -1 : base.ChooseMcHost(state, random);

        public override int[] ChooseLineup(ShowState state, Random random) =>
            Sandbagging(state)
                ? Candidates(state).OrderBy(lineup => Engine.Preview(state, lineup).Change).First()
                : base.ChooseLineup(state, random);
    }
}
