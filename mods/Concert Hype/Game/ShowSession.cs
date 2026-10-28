using ConcertHype.Model;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ConcertHype
{
    /// <summary>
    /// What the session needs from the concert popup. The game's popup in play; a fake in the tests.
    /// </summary>
    public interface IConcertStage
    {
        SEvent_Concerts._concert Concert { get; }

        /// <summary>
        /// The setlist index the popup is on.
        /// </summary>
        int SongCounter { get; }

        void AddHype(float value, bool animate);
    }

    /// <summary>
    /// The new show for one concert, from StartConcert to Finish. Only one runs at a time. If anything goes wrong it
    /// ends, and the rest of that concert plays as in the original game.
    /// </summary>
    public sealed class ShowSession
    {
        public static ShowSession Current { get; private set; }

        /// <summary>
        /// The last show's score, for the results screen after the session has ended.
        /// </summary>
        public static Score LastScore { get; private set; }

        /// <summary>
        /// Where the finished show's log is written; null writes none.
        /// </summary>
        public static string LogPath;

        public static event Action Changed;

        public readonly SEvent_Concerts._concert Concert;
        public readonly GameShow Show;
        public readonly ShowState State;

        /// <summary>
        /// The lineup planned for the next song.
        /// </summary>
        public int[] Lineup;

        public SongResult LastResult;
        public McResult LastMc;

        internal bool StartHypeApplied;
        internal SEvent_Concerts._concert._song PendingItem;
        internal SongResult Pending;

        private readonly StringBuilder log = new StringBuilder();

        private ShowSession(SEvent_Concerts._concert concert, GameShow show, ShowState state)
        {
            Concert = concert;
            Show = show;
            State = state;
            Lineup = state.Finished ? new int[0] : Engine.DefaultLineup(state);
        }

        /// <summary>
        /// The switch is on and every setlist item is a song: a setlist with talk breaks (set up before the mod was
        /// on) plays the original show.
        /// </summary>
        public static bool Qualifies(SEvent_Concerts._concert concert) =>
            concert != null && Switch.Enabled && concert.SetListItems.Count > 0
            && concert.SetListItems.All(item => item is SEvent_Concerts._concert._song);

        public static ShowSession Begin(SEvent_Concerts._concert concert, Config config)
        {
            GameShow show = GameReader.Read(concert, config);
            int seed = config.Seed != 0 ? config.Seed : (int)(DateTime.Now.Ticks & int.MaxValue);
            ShowState state = Engine.Start(show.Setup, config, seed);
            Current = new ShowSession(concert, show, state);
            LastScore = null;
            Current.log.AppendLine($"Concert Hype show: {concert.Title} ({concert.Venue}), seed {seed}");
            Current.log.AppendLine($"Cast: {string.Join(", ", show.Setup.Members.Select(m => m.Name))}");
            Current.log.AppendLine($"Capacity filled {F(show.Setup.CapacityFilled)}, production {F(show.Setup.Production)}, hype floor {F(show.Setup.HypeFloor)}");
            Changed?.Invoke();
            return Current;
        }

        public static void End()
        {
            Current = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// Logs the error and ends the session; the concert carries on as in the original game.
        /// </summary>
        public static void Fail(Exception ex, string where)
        {
            Log.Error($"{where} failed, so the rest of this concert plays as in the original game: {ex}");
            End();
        }

        public string Name(int member) => member < 0 ? "nobody" : State.Setup.Members[member].Name;

        public SongPreview Preview() => State.Finished ? null : Engine.Preview(State, Lineup);

        /// <summary>
        /// Puts a bench idol in a slot. Changing a slot whose original can perform is the song's one substitution,
        /// so it puts back any other one. False when the result wouldn't be legal.
        /// </summary>
        public bool Assign(int slot, int member)
        {
            if (State.Finished || slot < 0 || slot >= Lineup.Length || Array.IndexOf(Lineup, member) >= 0)
                return false;
            Song song = State.CurrentSong;
            bool[] forced = Engine.ForcedSlots(State);
            int[] next = (int[])Lineup.Clone();
            next[slot] = member;
            if (!forced[slot] && member != song.Slots[slot].Original)
            {
                for (int k = 0; k < next.Length; k++)
                {
                    if (k != slot && !forced[k] && next[k] != song.Slots[k].Original)
                        next[k] = song.Slots[k].Original;
                }
            }
            if (Engine.Validate(State, next) != null)
                return false;
            Lineup = next;
            Changed?.Invoke();
            return true;
        }

        public void ResetLineup()
        {
            if (State.Finished)
                return;
            Lineup = Engine.DefaultLineup(State);
            Changed?.Invoke();
        }

        public McResult CallMc(int host, IConcertStage stage)
        {
            State.Hype = stage.Concert.Hype;
            McResult result = Engine.CallMc(State, host);
            stage.AddHype((float)(result.HypeAfter - stage.Concert.Hype), true);
            LastMc = result;
            // Idols back from absence or rest may make the planned lineup illegal: keep it only if it still is
            if (Engine.Validate(State, Lineup) != null)
                Lineup = Engine.DefaultLineup(State);
            log.AppendLine($"MC break hosted by {Name(host)} (talk {F(result.Talk)}): hype {F(result.HypeBefore)} -> {F(result.HypeAfter)}");
            Changed?.Invoke();
            return result;
        }

        /// <summary>
        /// The player started the next song: play it in the model now; the popup then animates its hype change.
        /// </summary>
        public void BeforeSong(IConcertStage stage)
        {
            if (Pending != null || stage.SongCounter != State.SongIndex)
                throw new InvalidOperationException($"the popup is on item {stage.SongCounter}, the show on song {State.SongIndex}");
            State.Hype = stage.Concert.Hype;
            if (Engine.Validate(State, Lineup) != null)
                Lineup = Engine.DefaultLineup(State);
            PendingItem = Show.Items[State.SongIndex];
            Pending = Engine.Play(State, Lineup);
            LogSong(Pending);
        }

        /// <summary>
        /// The hype the popup animates for this item, if it is the song being played.
        /// </summary>
        public bool HypeFor(SEvent_Concerts._concert._song item, out float change)
        {
            change = 0;
            if (Pending == null || !ReferenceEquals(item, PendingItem))
                return false;
            change = (float)Pending.Change;
            return true;
        }

        /// <summary>
        /// The song's animation ended: land the hype exactly on the model's value and plan the next song.
        /// </summary>
        public void AfterSong(IConcertStage stage)
        {
            if (Pending == null)
                return;
            stage.AddHype((float)(Pending.HypeAfter - stage.Concert.Hype), false);
            LastResult = Pending;
            LastMc = null;
            Pending = null;
            PendingItem = null;
            if (!State.Finished)
                Lineup = Engine.DefaultLineup(State);
            Changed?.Invoke();
        }

        /// <summary>
        /// The show is over: its peak-end score becomes the concert's hype, and the log is written.
        /// </summary>
        public Score Finish(IConcertStage stage)
        {
            Score score = Engine.Score(State);
            stage.AddHype((float)(score.Total - stage.Concert.Hype), true);
            LastScore = score;
            log.AppendLine($"Score {F(score.Total)} = 0.5 x average {F(score.Average)} + 0.25 x peak {F(score.Peak)} + 0.25 x finale {F(score.Finale)}");
            log.AppendLine($"Accidents: {State.Accidents}; forced fills that found nobody: {State.EmptyFills}");
            Log.Info($"show over: score {F(score.Total)} (average {F(score.Average)}, peak {F(score.Peak)}, finale {F(score.Finale)})");
            WriteLog();
            return score;
        }

        public string LogText => log.ToString();

        private void WriteLog()
        {
            if (string.IsNullOrEmpty(LogPath))
                return;
            try
            {
                File.WriteAllText(LogPath, log.ToString());
            }
            catch (Exception ex)
            {
                Log.WarnOnce("log", $"couldn't write {LogPath}: {ex.Message}");
            }
        }

        private void LogSong(SongResult result)
        {
            Song song = State.Setup.Songs[result.SongIndex];
            log.AppendLine();
            log.AppendLine($"Song {result.SongIndex + 1}: {result.Title} ({song.Genre})");
            for (int j = 0; j < result.Slots.Length; j++)
            {
                SlotResult slot = result.Slots[j];
                string place = $"  row {song.Slots[j].Row + 1} pos {song.Slots[j].Position}";
                if (slot.Member < 0)
                {
                    log.AppendLine($"{place}: empty (roll 0)");
                    continue;
                }
                string role = slot.Member == song.Slots[j].Original ? "original" : Engine.IsForced(State, song, j) ? "fill" : "substitute";
                RollParts p = slot.Parts;
                log.AppendLine($"{place}: {Name(slot.Member)} ({role}) roll {F(slot.Roll)} = {F(p.EnergyFactor)} x skill {F(p.Skill)} + appeal {F(p.Appeal)} + senbatsu {F(p.Senbatsu)} + noise; " +
                    $"energy {F(slot.EnergyBefore)} -> {F(slot.EnergyAfter)}" + (slot.Accident ? $"; ACCIDENT (chance {F(slot.AccidentChance)}), out {slot.AbsenceLength}" : ""));
            }
            log.AppendLine($"  weighted {F(result.Weighted)}, base {F(result.BaseChange)}, genre flow {F(result.GenreFlow)}, show x{F(result.ShowMultiplier)}, " +
                $"novelty {F(result.Novelty)}, amplification x{F(result.Amplification)}: hype {F(result.HypeBefore)} -> {F(result.HypeAfter)} ({F(result.Change)})");
            if (result.Exhausted.Count > 0)
                log.AppendLine("  exhausted: " + string.Join(", ", result.Exhausted.Select(Name)));
            if (result.Returned.Count > 0)
                log.AppendLine("  rested enough to return: " + string.Join(", ", result.Returned.Select(Name)));
        }

        private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
