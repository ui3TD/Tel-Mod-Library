using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcertHype.Model
{
    /// <summary>
    /// The show's rules. A lineup is an array with one cast member index per slot of the current song (-1 for empty).
    /// </summary>
    public static class Engine
    {
        public const double MaxHype = 200;

        public static ShowState Start(ShowSetup setup, Config config, int seed)
        {
            int members = setup.Members.Count;
            ShowState state = new ShowState
            {
                Setup = setup,
                Config = config,
                Rng = new Rng(seed),
                Energy = Enumerable.Repeat(100.0, members).ToArray(),
                Absence = new int[members],
                Exhausted = new bool[members],
                McCharges = config.McCharges,
                Hype = Clamp(setup.StartingHype, setup.HypeFloor, MaxHype),
                Alignment = new double[members][],
                FanWeight = new double[members],
            };

            for (int m = 0; m < members; m++)
                state.Alignment[m] = setup.Songs.Select(song => Alignment(setup.Members[m].Appeal, song.Appeal)).ToArray();

            // Weighed against the setlist's idols: the rest of the agency on the bench doesn't change anyone's weight
            List<Member> setlist = setup.Members.Where(member => member.InSetlist).ToList();
            if (setlist.Count == 0)
                setlist = setup.Members;
            double averageFans = setlist.Count == 0 ? 0 : setlist.Average(member => member.Fans);
            for (int m = 0; m < members; m++)
                state.FanWeight[m] = averageFans <= 0 ? 1 : Math.Max(config.FanWeightFloor, setup.Members[m].Fans / averageFans);
            return state;
        }

        /// <summary>
        /// The cosine of two appeal leans: within each fan axis, each value minus the axis average. 0 when either lean is flat.
        /// </summary>
        public static double Alignment(double[] memberAppeal, double[] songAppeal)
        {
            double[] a = Lean(memberAppeal);
            double[] b = Lean(songAppeal);
            double dot = 0, aa = 0, bb = 0;
            for (int i = 0; i < FanTypes.Count; i++)
            {
                dot += a[i] * b[i];
                aa += a[i] * a[i];
                bb += b[i] * b[i];
            }
            if (aa < 1e-12 || bb < 1e-12)
                return 0;
            return Clamp(dot / Math.Sqrt(aa * bb), -1, 1);
        }

        private static double[] Lean(double[] appeal)
        {
            double[] lean = new double[FanTypes.Count];
            if (appeal == null || appeal.Length < FanTypes.Count)
                return lean;
            foreach (int[] axis in FanTypes.Axes)
            {
                double average = axis.Average(i => appeal[i]);
                foreach (int i in axis)
                    lean[i] = appeal[i] - average;
            }
            return lean;
        }

        /// <summary>
        /// A slot must be filled from the bench when its original member can't perform.
        /// </summary>
        public static bool IsForced(ShowState state, Song song, int slot)
        {
            int original = song.Slots[slot].Original;
            return original < 0 || !state.IsAvailable(original);
        }

        public static bool[] ForcedSlots(ShowState state)
        {
            Song song = state.CurrentSong;
            return Enumerable.Range(0, song.Slots.Count).Select(j => IsForced(state, song, j)).ToArray();
        }

        private static bool IsOriginalElsewhere(Song song, int member, int slot)
        {
            for (int k = 0; k < song.Slots.Count; k++)
            {
                if (k != slot && song.Slots[k].Original == member)
                    return true;
            }
            return false;
        }

        public static RollParts ExpectedRoll(ShowState state, int slot, int member)
        {
            Config config = state.Config;
            Song song = state.CurrentSong;
            int songIndex = state.SongIndex;
            RollParts parts = new RollParts
            {
                Skill = state.Setup.Skill[member][songIndex],
                EnergyFactor = config.EnergyMinFactor + (1 - config.EnergyMinFactor) * state.Energy[member] / 100,
                Appeal = config.KAppeal * state.Alignment[member][songIndex],
                Senbatsu = song.Slots[slot].Original == member ? config.KSenbatsu : 0,
            };
            parts.Expected = Math.Max(0, parts.EnergyFactor * parts.Skill + parts.Appeal + parts.Senbatsu);
            return parts;
        }

        /// <summary>
        /// The available bench member with the highest expected roll in this slot, or -1.
        /// </summary>
        public static int BestFill(ShowState state, int slot, ICollection<int> taken)
        {
            Song song = state.CurrentSong;
            int best = -1;
            double bestRoll = double.MinValue;
            for (int m = 0; m < state.Setup.Members.Count; m++)
            {
                if (!state.IsAvailable(m) || taken.Contains(m) || IsOriginalElsewhere(song, m, slot))
                    continue;
                double roll = ExpectedRoll(state, slot, m).Expected;
                if (roll > bestRoll)
                {
                    best = m;
                    bestRoll = roll;
                }
            }
            return best;
        }

        /// <summary>
        /// The original senbatsu, the song's preset substitute in the center slot, and the best bench member in each
        /// slot whose original can't perform.
        /// </summary>
        public static int[] DefaultLineup(ShowState state)
        {
            Song song = state.CurrentSong;
            int[] lineup = Enumerable.Repeat(-1, song.Slots.Count).ToArray();
            HashSet<int> taken = new HashSet<int>();
            for (int j = 0; j < lineup.Length; j++)
            {
                if (!IsForced(state, song, j))
                {
                    lineup[j] = song.Slots[j].Original;
                    taken.Add(lineup[j]);
                }
            }

            int preset = song.PresetSubstitute;
            int center = song.Slots.FindIndex(s => s.Position == 0);
            if (preset >= 0 && center >= 0 && state.IsAvailable(preset) && !taken.Contains(preset) && !IsOriginalElsewhere(song, preset, center))
            {
                if (lineup[center] >= 0)
                    taken.Remove(lineup[center]);
                lineup[center] = preset;
                taken.Add(preset);
            }

            for (int j = 0; j < lineup.Length; j++)
            {
                if (lineup[j] < 0)
                {
                    lineup[j] = BestFill(state, j, taken);
                    if (lineup[j] >= 0)
                        taken.Add(lineup[j]);
                }
            }
            return lineup;
        }

        /// <summary>
        /// Null when the lineup is legal for the current song, else why not.
        /// </summary>
        public static string Validate(ShowState state, int[] lineup)
        {
            Song song = state.CurrentSong;
            if (song == null)
                return "the show is over";
            if (lineup == null || lineup.Length != song.Slots.Count)
                return "the lineup doesn't match the song's slots";

            HashSet<int> seen = new HashSet<int>();
            int changes = 0;
            for (int j = 0; j < lineup.Length; j++)
            {
                int m = lineup[j];
                bool forced = IsForced(state, song, j);
                if (m < 0)
                {
                    if (!forced)
                        return $"slot {j} is empty but its original member can perform";
                    continue;
                }
                if (m >= state.Setup.Members.Count || !state.IsAvailable(m))
                    return $"member {m} can't perform";
                if (!seen.Add(m))
                    return $"member {m} is in two slots";
                if (IsOriginalElsewhere(song, m, j))
                    return $"member {m} belongs in another slot";
                if (!forced && m != song.Slots[j].Original)
                    changes++;
            }
            if (changes > state.Config.SubstitutionsPerSong)
                return $"{changes} substitutions; at most {state.Config.SubstitutionsPerSong} per song";

            for (int j = 0; j < lineup.Length; j++)
            {
                if (lineup[j] < 0 && BestFill(state, j, seen) >= 0)
                    return $"slot {j} must be filled: someone on the bench can perform";
            }
            return null;
        }

        /// <summary>
        /// Row-weighted share of slots not held by their original member. An empty slot counts as changed.
        /// </summary>
        public static double Novelty(ShowState state, Song song, int[] lineup)
        {
            double total = 0, changed = 0;
            for (int j = 0; j < song.Slots.Count; j++)
            {
                double r = state.Config.RowWeight(song.Slots[j].Row);
                total += r;
                if (lineup[j] < 0 || lineup[j] != song.Slots[j].Original)
                    changed += r;
            }
            return total <= 0 ? 0 : changed / total;
        }

        public static double ShowMultiplier(ShowSetup setup, Config config) =>
            (config.CapacityFloor + (1 - config.CapacityFloor) * Clamp(setup.CapacityFilled, 0, 1))
            * (config.ProductionFloor + config.ProductionRange * Clamp(setup.Production, 0, 1));

        private static double Weight(ShowState state, Song song, int slot, int member) =>
            (member < 0 ? 1 : state.FanWeight[member]) * state.Config.RowWeight(song.Slots[slot].Row);

        private static double Weighted(ShowState state, Song song, int[] lineup, double[] rolls)
        {
            double sum = 0, weights = 0;
            for (int j = 0; j < lineup.Length; j++)
            {
                double w = Weight(state, song, j, lineup[j]);
                weights += w;
                sum += w * (lineup[j] < 0 ? 0 : rolls[j]);
            }
            return weights <= 0 ? 0 : sum / weights;
        }

        /// <summary>
        /// The hype steps from a weighted performance: base change, genre flow, show multiplier on gains, then
        /// novelty amplification by hype band, then clamping. Fills the result's step fields.
        /// </summary>
        private static void HypeSteps(ShowState state, Song song, double novelty, double weighted, SongResult result)
        {
            Config config = state.Config;
            double h = state.Hype;
            double d = config.KPerf * (weighted - config.Par);
            result.BaseChange = d;
            result.GenreFlow = state.PreviousGenre != null && song.Genre.Length > 0 && song.Genre == state.PreviousGenre ? config.GenreFlowBonus : 0;
            d += result.GenreFlow;
            result.ShowMultiplier = 1;
            if (d > 0)
            {
                result.ShowMultiplier = ShowMultiplier(state.Setup, config);
                d *= result.ShowMultiplier;
            }

            double band = config.NoveltyBlendBand;
            double t = band <= 0 ? (h >= 100 ? 1 : 0) : Clamp((h - 100 + band) / (2 * band), 0, 1);
            double gainAmp = Lerp(config.AmpHigh, config.AmpLow, t);
            double lossAmp = Lerp(config.AmpLow, config.AmpHigh, t);
            result.Novelty = novelty;
            result.Amplification = 1 + novelty * (d >= 0 ? gainAmp : lossAmp);
            d *= result.Amplification;

            result.HypeBefore = h;
            result.Weighted = weighted;
            result.HypeAfter = Clamp(h + d, state.Setup.HypeFloor, MaxHype);
            result.Change = result.HypeAfter - h;
        }

        public static SongPreview Preview(ShowState state, int[] lineup)
        {
            Song song = state.CurrentSong;
            SongPreview preview = new SongPreview
            {
                Slots = new RollParts[lineup.Length],
                Forced = Enumerable.Range(0, lineup.Length).Select(j => IsForced(state, song, j)).ToArray(),
                Novelty = Novelty(state, song, lineup),
            };
            double[] rolls = new double[lineup.Length];
            double weights = 0, squares = 0;
            for (int j = 0; j < lineup.Length; j++)
            {
                double w = Weight(state, song, j, lineup[j]);
                weights += w;
                if (lineup[j] < 0)
                    continue;
                preview.Slots[j] = ExpectedRoll(state, j, lineup[j]);
                rolls[j] = preview.Slots[j].Expected;
                squares += w * w;
            }
            preview.Weighted = Weighted(state, song, lineup, rolls);
            double spread = weights <= 0 ? 0 : state.Config.RollSigma * Math.Sqrt(squares) / weights;

            SongResult steps = new SongResult();
            HypeSteps(state, song, preview.Novelty, preview.Weighted, steps);
            preview.Change = steps.Change;
            HypeSteps(state, song, preview.Novelty, preview.Weighted - spread, steps);
            preview.Low = steps.Change;
            HypeSteps(state, song, preview.Novelty, preview.Weighted + spread, steps);
            preview.High = steps.Change;
            return preview;
        }

        public static double AccidentChance(Config config, double roll, double energy) =>
            Clamp(config.PBase
                + config.PRoll * Math.Max(0, config.AccidentRollThreshold - roll)
                + config.PTired * (1 - energy / 100), 0, config.PMax);

        public static double EnergyCost(Config config, int row, double roll) =>
            config.EnergyCostBase * config.RowCostOf(row)
            * Clamp(1 - config.KRollCost * (roll - config.Par) / 100, config.RollCostMin, config.RollCostMax);

        /// <summary>
        /// Plays the current song with this lineup: rolls (noise, then the accident check, per performer in slot
        /// order), hype, energy, absences, exhaustion. Throws when the lineup isn't legal.
        /// </summary>
        public static SongResult Play(ShowState state, int[] lineup)
        {
            string error = Validate(state, lineup);
            if (error != null)
                throw new ArgumentException("Illegal lineup: " + error);

            Config config = state.Config;
            Song song = state.CurrentSong;
            int members = state.Setup.Members.Count;
            bool[] absentBefore = state.Absence.Select(a => a > 0).ToArray();
            SongResult result = new SongResult
            {
                SongIndex = state.SongIndex,
                Title = song.Title,
                Slots = new SlotResult[lineup.Length],
            };

            double[] rolls = new double[lineup.Length];
            for (int j = 0; j < lineup.Length; j++)
            {
                int m = lineup[j];
                SlotResult slot = new SlotResult { Member = m };
                result.Slots[j] = slot;
                if (m < 0)
                {
                    result.EmptyFills++;
                    continue;
                }
                slot.Parts = ExpectedRoll(state, j, m);
                double raw = slot.Parts.EnergyFactor * slot.Parts.Skill + slot.Parts.Appeal + slot.Parts.Senbatsu;
                slot.Roll = Math.Max(0, raw + state.Rng.Normal(config.RollSigma));
                rolls[j] = slot.Roll;
                slot.EnergyBefore = state.Energy[m];
                slot.AccidentChance = AccidentChance(config, slot.Roll, state.Energy[m]);
                slot.Accident = state.Rng.Uniform() < slot.AccidentChance;
                if (slot.Accident)
                    slot.AbsenceLength = state.Rng.Range(config.AbsenceMin, config.AbsenceMax);
            }

            HypeSteps(state, song, Novelty(state, song, lineup), Weighted(state, song, lineup, rolls), result);
            double after = result.HypeAfter;

            bool[] performing = new bool[members];
            for (int j = 0; j < lineup.Length; j++)
            {
                int m = lineup[j];
                if (m < 0)
                    continue;
                performing[m] = true;
                double change = after < 100
                    ? -EnergyCost(config, song.Slots[j].Row, rolls[j])
                    : config.HypeLiftBase + config.HypeLiftPerPoint * (after - 100);
                state.Energy[m] = Clamp(state.Energy[m] + change, 0, 100);
            }
            for (int m = 0; m < members; m++)
            {
                if (!performing[m])
                    state.Energy[m] = Clamp(state.Energy[m] + config.BenchRecovery, 0, 100);
            }

            // Absences already running count down; the ones this song starts count from the next song.
            for (int m = 0; m < members; m++)
            {
                if (absentBefore[m])
                    state.Absence[m] = Math.Max(0, state.Absence[m] - 1);
            }
            foreach (SlotResult slot in result.Slots)
            {
                if (slot.Member >= 0)
                {
                    slot.EnergyAfter = state.Energy[slot.Member];
                    if (slot.Accident)
                    {
                        state.Absence[slot.Member] = slot.AbsenceLength;
                        state.Accidents++;
                    }
                }
            }

            ReturnRested(state, result.Returned);
            for (int m = 0; m < members; m++)
            {
                if (performing[m] && state.Energy[m] <= 0 && !state.Exhausted[m])
                {
                    state.Exhausted[m] = true;
                    result.Exhausted.Add(m);
                }
            }

            state.EmptyFills += result.EmptyFills;
            state.History.Add(after);
            state.Hype = after;
            state.PreviousGenre = song.Genre;
            state.SongIndex++;
            state.McInThisGap = false;
            return result;
        }

        private static void ReturnRested(ShowState state, List<int> returned)
        {
            for (int m = 0; m < state.Exhausted.Length; m++)
            {
                if (state.Exhausted[m] && state.Energy[m] >= state.Config.ReturnThreshold)
                {
                    state.Exhausted[m] = false;
                    returned?.Add(m);
                }
            }
        }

        /// <summary>
        /// Null when an MC can be called now, else why not.
        /// </summary>
        public static string CanCallMc(ShowState state)
        {
            if (state.Finished)
                return "the show is over";
            if (state.SongIndex == 0)
                return "not before the first song";
            if (state.McCharges <= 0)
                return "no MC breaks left";
            if (state.McInThisGap)
                return "one MC break per gap";
            return null;
        }

        /// <summary>
        /// An MC break: pulls hype toward 100 by the host's talk, everyone recovers energy, absences count down.
        /// Adds nothing to the hype history.
        /// </summary>
        public static McResult CallMc(ShowState state, int host)
        {
            string error = CanCallMc(state);
            if (error == null && !state.IsAvailable(host))
                error = $"member {host} can't host";
            if (error != null)
                throw new InvalidOperationException("Can't call an MC: " + error);

            Config config = state.Config;
            McResult result = new McResult { Host = host, Talk = state.Setup.Members[host].Talk, HypeBefore = state.Hype };
            state.Hype = McHype(state, host);
            result.HypeAfter = state.Hype;
            for (int m = 0; m < state.Energy.Length; m++)
            {
                state.Energy[m] = Clamp(state.Energy[m] + config.McRecovery, 0, 100);
                state.Absence[m] = Math.Max(0, state.Absence[m] - config.McTimeUnits);
            }
            ReturnRested(state, null);
            state.McCharges--;
            state.McInThisGap = true;
            return result;
        }

        /// <summary>
        /// The hype an MC hosted by this member would pull the crowd to now.
        /// </summary>
        public static double McHype(ShowState state, int host) =>
            Clamp(state.Hype + state.Config.KMc * state.Setup.Members[host].Talk * (100 - state.Hype), state.Setup.HypeFloor, MaxHype);

        /// <summary>
        /// The peak-end score of a hype history; the starting hype when no song was played.
        /// </summary>
        public static Score Score(ShowState state)
        {
            List<double> history = state.History;
            if (history.Count == 0)
                return new Score { Total = state.Hype, Average = state.Hype, Peak = state.Hype, Finale = state.Hype };
            Config config = state.Config;
            Score score = new Score { Average = history.Average(), Peak = history.Max(), Finale = history[history.Count - 1] };
            score.Total = config.WAvg * score.Average + config.WPeak * score.Peak + config.WEnd * score.Finale;
            return score;
        }

        public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
