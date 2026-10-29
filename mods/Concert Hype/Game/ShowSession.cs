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

        /// <summary>
        /// Runs the action on the next frame.
        /// </summary>
        void NextFrame(Action action);
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

        public static SEvent_Concerts._concert LastScoreConcert { get; private set; }

        /// <summary>
        /// Where the finished show's log is written; null writes none.
        /// </summary>
        public static string LogPath;

        public static event Action Changed;

        /// <summary>
        /// The most the concert's hype may differ from the show's before a correction leaves it alone.
        /// </summary>
        public const float MaxDrift = 1f;

        public readonly SEvent_Concerts._concert Concert;
        public readonly GameShow Show;
        public readonly ShowState State;

        /// <summary>
        /// The lineup planned for the next song.
        /// </summary>
        public int[] Lineup;

        /// <summary>
        /// The talk break's row of host places, -1 where nobody is: with anyone on it, Start plays a talk break they
        /// host before the next song.
        /// </summary>
        public int[] Hosts = DropRules.NoHosts();

        public bool HasTalk => Hosts.Any(h => h >= 0);

        public SongResult LastResult;
        public McResult LastMc;

        internal bool StartHypeApplied;
        internal SEvent_Concerts._concert._song PendingItem;
        internal SongResult Pending;
        internal SEvent_Concerts._concert._mc PendingTalkItem;
        internal McResult PendingTalk;

        /// <summary>
        /// The host place each of the talk break's hosts came from, in host order.
        /// </summary>
        internal int[] PendingTalkPlaces = new int[0];
        private bool[] forcedBeforeTalk;

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
            LastScoreConcert = null;
            Current.log.AppendLine($"Concert Hype show: {concert.Title} ({concert.Venue}), seed {seed}");
            Current.log.AppendLine($"Setlist idols: {string.Join(", ", show.Setup.Members.Where(m => m.InSetlist).Select(m => m.Name))}");
            Current.log.AppendLine($"Others on the bench: {show.Setup.Members.Count(m => !m.InSetlist)}");
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

        /// <summary>
        /// A song or talk break is playing.
        /// </summary>
        public bool Playing => Pending != null || PendingTalk != null;

        /// <summary>
        /// Between songs the show starts from the concert's hype, which the game's start, talk breaks and other mods
        /// may have changed.
        /// </summary>
        public void SyncHype()
        {
            if (!Playing)
                State.Hype = Engine.Clamp(Concert.Hype, State.Setup.HypeFloor, Engine.MaxHype);
        }

        public SongPreview Preview() => State.Finished ? null : Engine.Preview(State, Lineup);

        /// <summary>
        /// An idol dragged from one spot to another, by the drop rules. Refused drops change nothing.
        /// </summary>
        public DropResult Drop(int member, DropPoint from, DropPoint to) => Apply(DropRules.Drop(State, Lineup, Hosts, member, from, to));

        /// <summary>
        /// A quick click on a bench idol on the song's tab.
        /// </summary>
        public DropResult Click(int member) => Apply(DropRules.Click(State, Lineup, Hosts, member));

        /// <summary>
        /// A quick click on a bench idol on the talk break's tab.
        /// </summary>
        public DropResult ClickHost(int member) => Apply(DropRules.ClickHost(State, Lineup, Hosts, member));

        private DropResult Apply(DropResult result)
        {
            if (result.Allowed && result.Changed && !Playing && !State.Finished)
            {
                Lineup = result.Lineup;
                Hosts = result.Hosts;
                Changed?.Invoke();
            }
            return result;
        }

        public void ResetLineup()
        {
            if (State.Finished)
                return;
            Lineup = Engine.DefaultLineup(State);
            Changed?.Invoke();
        }

        /// <summary>
        /// The popup's item now is the show's next song.
        /// </summary>
        private void CheckStep(IConcertStage stage)
        {
            var items = stage.Concert.SetListItems;
            int i = stage.SongCounter;
            if (Playing || State.Finished || i < 0 || i >= items.Count || !ReferenceEquals(items[i], Show.Items[State.SongIndex]))
                throw new InvalidOperationException($"the popup is on item {i} of {items.Count}, the show on song {State.SongIndex}");
        }

        /// <summary>
        /// The player started a talk break: the hosts' MC plays in the model now, and a talk break item with them, in
        /// their row's order, goes into the setlist where the popup is, so the popup plays it as the game plays a
        /// planned one. The next song stays next.
        /// </summary>
        public void StartTalkBreak(IConcertStage stage)
        {
            CheckStep(stage);
            int[] hosts = Hosts.Where(h => h >= 0).ToArray();
            int[] places = Enumerable.Range(0, Hosts.Length).Where(k => Hosts[k] >= 0).ToArray();
            State.Hype = stage.Concert.Hype;
            bool[] forced = Engine.ForcedSlots(State);
            McResult result = Engine.CallMc(State, hosts);
            forcedBeforeTalk = forced;

            SEvent_Concerts._concert._mc item = new SEvent_Concerts._concert._mc();
            for (int i = 0; i < hosts.Length; i++)
                item.SetGirl(Show.Girls[hosts[i]], i);
            var items = stage.Concert.SetListItems;
            items.Insert(stage.SongCounter, item);
            item.SetTitle(TalkTitle(items.Take(stage.SongCounter + 1).Count(i => i.isMC())));

            PendingTalkItem = item;
            PendingTalk = result;
            PendingTalkPlaces = places;
            Hosts = DropRules.NoHosts();
            log.AppendLine();
            log.AppendLine($"{item.GetTitle()} hosted by {string.Join(", ", hosts.Select(Name))} (talk {F(result.Talk)}): hype {F(result.HypeBefore)} -> {F(result.HypeAfter)}");
            Changed?.Invoke();
        }

        /// <summary>
        /// "Talk Break #2", as the game names the setlist's talk breaks.
        /// </summary>
        private static string TalkTitle(int number)
        {
            string title = Language.Data != null && Language.Data.TryGetValue("CONCERT__TALK_BREAK", out string text) && !string.IsNullOrEmpty(text) ? text : "Talk Break";
            return title + " #" + number;
        }

        /// <summary>
        /// The hype the popup animates for this talk break item, if it is the show's talk break being played.
        /// </summary>
        public bool HypeForTalk(SEvent_Concerts._concert._mc item, out float change)
        {
            change = 0;
            if (PendingTalk == null || !ReferenceEquals(item, PendingTalkItem))
                return false;
            change = (float)(PendingTalk.HypeAfter - PendingTalk.HypeBefore);
            return true;
        }

        /// <summary>
        /// The player started the next song: play it in the model now; the popup then animates its hype change.
        /// </summary>
        public void BeforeSong(IConcertStage stage)
        {
            CheckStep(stage);
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
        /// The song's or talk break's animation ended: land the hype exactly on the model's value. After a song the
        /// next one's default lineup is planned. After a talk break the same song comes next with the planned
        /// lineup, except that an idol back from absence or rest takes her own place back from its fill; if that
        /// isn't legal, the song's default lineup.
        /// </summary>
        public void AfterSong(IConcertStage stage)
        {
            if (PendingTalk != null)
            {
                McResult talk = PendingTalk;
                Land(stage, talk.HypeAfter, true, () => ReferenceEquals(LastMc, talk));
                LastMc = talk;
                PendingTalk = null;
                PendingTalkItem = null;
                Lineup = AfterReturns(Lineup, forcedBeforeTalk);
                if (Engine.Validate(State, Lineup) != null)
                    Lineup = Engine.DefaultLineup(State);
                Changed?.Invoke();
                return;
            }
            if (Pending == null)
                return;
            SongResult song = Pending;
            // After the last song the score takes over
            Land(stage, song.HypeAfter, !State.Finished, () => ReferenceEquals(LastResult, song));
            LastResult = song;
            LastMc = null;
            Pending = null;
            PendingItem = null;
            if (!State.Finished)
                Lineup = Engine.DefaultLineup(State);
            Changed?.Invoke();
        }

        /// <summary>
        /// The lineup with each place that had to be filled before, and no longer has to, given back to its idol.
        /// </summary>
        private int[] AfterReturns(int[] lineup, bool[] forcedBefore)
        {
            Song song = State.CurrentSong;
            if (song == null || forcedBefore == null || forcedBefore.Length != lineup.Length)
                return lineup;
            int[] next = (int[])lineup.Clone();
            for (int j = 0; j < next.Length; j++)
            {
                if (forcedBefore[j] && !Engine.IsForced(State, song, j))
                    next[j] = song.Slots[j].Original;
            }
            return next;
        }

        /// <summary>
        /// Sets the concert's hype to the model's. The game's hype animation can take its last step after the song
        /// ends, so it lands again a frame later while nothing else has played; a bigger difference is another
        /// mod's doing and stays.
        /// </summary>
        private void Land(IConcertStage stage, double hype, bool again, Func<bool> stillLast)
        {
            stage.AddHype((float)(hype - stage.Concert.Hype), false);
            if (!again)
                return;
            stage.NextFrame(() =>
            {
                float drift = (float)hype - stage.Concert.Hype;
                if (ReferenceEquals(Current, this) && !Playing && stillLast() && drift != 0f && Math.Abs(drift) < MaxDrift)
                    stage.AddHype(drift, false);
            });
        }

        /// <summary>
        /// The show is over: its peak-end score becomes the concert's hype, and the log is written.
        /// </summary>
        public Score Finish(IConcertStage stage)
        {
            Score score = Engine.Score(State);
            stage.AddHype((float)(score.Total - stage.Concert.Hype), true);
            LastScore = score;
            LastScoreConcert = Concert;
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
                string role = slot.Member == song.Slots[j].Original ? "original" : "substitute";
                RollParts p = slot.Parts;
                log.AppendLine($"{place}: {Name(slot.Member)} ({role}) roll {F(slot.Roll)} ({Outcomes.Of(State.Config, slot.Roll)}) = {F(p.EnergyFactor)} x skill {F(p.Skill)} + appeal {F(p.Appeal)} + senbatsu {F(p.Senbatsu)} + noise; " +
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
