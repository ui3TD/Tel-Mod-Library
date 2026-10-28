using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ConcertHype.Tests.Balance
{
    /// <summary>
    /// The spec's balance checks over random setups, next to their placeholder targets. A report, not a test: the
    /// targets are for the designer to tune, so nothing here fails on them. Run alone with
    /// dotnet test --filter Category=Balance --logger "console;verbosity=detailed".
    /// </summary>
    public class BalanceReport
    {
        private const int ShowCount = 2000;

        private readonly ITestOutputHelper output;

        public BalanceReport(ITestOutputHelper output) => this.output = output;

        private sealed class Run
        {
            public double Score;
            public List<double> History;
            public int Accidents;
            public int EmptyFills;
            public List<(double before, double change)> Mcs = new();
        }

        private static Run Play(Policy policy, int seed, Config config)
        {
            ShowState state = Engine.Start(RandomSetups.Make(seed), config, seed);
            Random decisions = new(seed * 7919 + 1);
            Run run = new();
            while (!state.Finished)
            {
                int host = policy.ChooseMcHost(state, decisions);
                if (host >= 0)
                {
                    McResult mc = Engine.CallMc(state, host);
                    run.Mcs.Add((mc.HypeBefore, mc.HypeAfter - mc.HypeBefore));
                }
                Engine.Play(state, policy.ChooseLineup(state, decisions));
            }
            run.Score = Engine.Score(state).Total;
            run.History = state.History;
            run.Accidents = state.Accidents;
            run.EmptyFills = state.EmptyFills;
            return run;
        }

        /// <summary>
        /// The expected hype change from swapping one equal (or better) bench idol into the center slot, minus the
        /// change with the originals, at a given hype.
        /// </summary>
        private static double SwapGain(Config config, double hype, double skillBonus)
        {
            ShowSetup setup = Shows.Setup(members: 8, slots: 6, songs: 1, skill: 60);
            Shows.SetSkill(setup, 7, 60 + skillBonus);
            ShowState state = Engine.Start(setup, config, 1);
            state.Hype = hype;
            int[] originals = Engine.DefaultLineup(state);
            int[] swapped = (int[])originals.Clone();
            swapped[0] = 7;
            return Engine.Preview(state, swapped).Change - Engine.Preview(state, originals).Change;
        }

        private static double Correlation(IList<double> x, IList<double> y)
        {
            double mx = x.Average(), my = y.Average();
            double sxy = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum();
            double sxx = x.Sum(a => (a - mx) * (a - mx)), syy = y.Sum(b => (b - my) * (b - my));
            return sxx <= 0 || syy <= 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
        }

        [Fact]
        [Trait("Category", "Balance")]
        public void Report()
        {
            Config config = new();
            Policy[] policies = { new OriginalsOnly(), new Greedy(), new RandomPlayer(), new Sandbagger() };
            Dictionary<string, List<Run>> runs = policies.ToDictionary(p => p.Name, p => Enumerable.Range(1, ShowCount).Select(seed => Play(p, seed, config)).ToList());
            List<Run> greedy = runs["Greedy"];
            double Mean(string policy) => runs[policy].Average(r => r.Score);

            int songs = greedy[0].History.Count;
            int middle = (songs + 1) / 2 - 1;
            List<Run> behind = greedy.Where(r => r.History[middle] < 100).ToList();
            double comeback = behind.Count == 0 ? double.NaN : behind.Count(r => r.History[songs - 1] >= 100) / (double)behind.Count;
            int third = Math.Max(1, songs / 3);
            double snowball = Correlation(greedy.Select(r => r.History.Take(third).Average()).ToList(), greedy.Select(r => r.Score).ToList());
            List<(double before, double change)> mcs = runs["Greedy"].Concat(runs["Random"]).SelectMany(r => r.Mcs).ToList();
            double McMean(Func<double, bool> band) => mcs.Where(m => band(m.before)).Select(m => m.change).DefaultIfEmpty(double.NaN).Average();

            string F(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.###", CultureInfo.InvariantCulture);
            string Pct(double v) => double.IsNaN(v) ? "n/a" : (100 * v).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string Mark(bool pass) => pass ? "pass" : "FAIL";

            double decisions = Mean("Greedy") / Mean("Originals only") - 1;
            double below = SwapGain(config, 70, 0), above = SwapGain(config, 130, 0);
            double skilledBelow = SwapGain(config, 70, 20), skilledAbove = SwapGain(config, 130, 20);
            double mcBelow = McMean(h => h < 100), mcAbove = McMean(h => h > 100);
            double accidents = greedy.Average(r => r.Accidents);
            double exhaustion = greedy.Count(r => r.EmptyFills > 0) / (double)greedy.Count;

            StringBuilder report = new();
            report.AppendLine($"Concert Hype balance report: {ShowCount} random setups per policy (made-up groups, not real play)");
            report.AppendLine();
            report.AppendLine("Mean score: " + string.Join(", ", policies.Select(p => $"{p.Name} {F(Mean(p.Name))}")));
            report.AppendLine();
            report.AppendLine("| Check | Measured | Target | Result |");
            report.AppendLine("|---|---|---|---|");
            report.AppendLine($"| Decisions matter (Greedy vs Originals only) | {Pct(decisions)} | at least 10% higher | {Mark(decisions >= 0.10)} |");
            report.AppendLine($"| Comeback rate ({behind.Count} Greedy shows below 100% after song {middle + 1}) | {Pct(comeback)} | 25-35% | {Mark(comeback >= 0.25 && comeback <= 0.35)} |");
            report.AppendLine($"| Snowball strength (first-third hype vs score) | {F(snowball)} | report only; below 0.9 | {Mark(snowball < 0.9)} |");
            report.AppendLine($"| No sandbagging (Sandbagger vs Greedy) | {F(Mean("Sandbagger"))} vs {F(Mean("Greedy"))} | never higher | {Mark(Mean("Sandbagger") <= Mean("Greedy"))} |");
            report.AppendLine($"| Novelty by band (equal-skill swap into the center) | {F(below)} at 70%, {F(above)} at 130% | positive below, negative above | {Mark(below > 0 && above < 0)} |");
            report.AppendLine($"| Skilled substitute (+20 skill) | {F(skilledBelow)} at 70%, {F(skilledAbove)} at 130% | positive in both bands | {Mark(skilledBelow > 0 && skilledAbove > 0)} |");
            report.AppendLine($"| MC timing ({mcs.Count} MCs, Greedy and Random) | {F(mcBelow)} below 100%, {F(mcAbove)} above | positive below, negative above | {Mark(mcBelow > 0 && mcAbove < 0)} |");
            report.AppendLine($"| Accidents per show (Greedy) | {F(accidents)} | 0.5-1.5 | {Mark(accidents >= 0.5 && accidents <= 1.5)} |");
            report.AppendLine($"| Exhaustion (Greedy shows where a forced fill found nobody) | {Pct(exhaustion)} | under 5% | {Mark(exhaustion < 0.05)} |");

            string text = report.ToString();
            output.WriteLine(text);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "balance-report.md"), text);
            Assert.All(runs.Values.SelectMany(r => r), r => Assert.Equal(songs, r.History.Count));
        }
    }
}
