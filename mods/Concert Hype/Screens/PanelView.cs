using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ConcertHype
{
    /// <summary>
    /// Who holds a place: her own idol from the single, anyone else, or nobody.
    /// </summary>
    public enum SlotBadge
    {
        Original,
        Substitute,
        Empty,
    }

    /// <summary>
    /// The lineup icons and the stage's outcome effects, each a PNG in the mod's Images folder named as
    /// <see cref="Icons.FileName"/> names it.
    /// </summary>
    public enum IconKind
    {
        None,
        Original,
        Substitute,
        Hurt,
        Resting,
        CriticalSuccess,
        Success,
        Failure,
        CriticalFailure,

        /// <summary>
        /// The small sparkles a critical success sends out.
        /// </summary>
        Burst,
    }

    /// <summary>
    /// The next-song screen's two set-ups: the song's formation, or the talk break's row of hosts before it.
    /// </summary>
    public enum FormationTab
    {
        Song,
        Talk,
    }

    /// <summary>
    /// How a place or a host place shows the idol dragged over it.
    /// </summary>
    public enum Highlight
    {
        None,
        Allowed,
        Refused,
    }

    public sealed class SlotView
    {
        public int Member = -1;
        public int Row;
        public SlotBadge Badge;

        /// <summary>
        /// Her chance of a success or a critical success in percent; -1 for an empty place.
        /// </summary>
        public double Success = -1;

        public Highlight Highlight;

        /// <summary>
        /// The idol here is being dragged away.
        /// </summary>
        public bool Dragged;

        public IconKind Icon;

        /// <summary>
        /// The icon's meaning, shown when it's hovered.
        /// </summary>
        public string Tip = "";

        /// <summary>
        /// Shown when her energy bar or her success bar is hovered; empty for an empty place.
        /// </summary>
        public string EnergyTip = "", SuccessTip = "";

        /// <summary>
        /// An empty place's expected hype cost this song, as shown under it ("-3%"), and its tooltip; empty for a
        /// filled place.
        /// </summary>
        public string GapText = "", GapTip = "";
    }

    /// <summary>
    /// A place in the talk break's row of hosts.
    /// </summary>
    public sealed class HostView
    {
        public int Member = -1;
        public Highlight Highlight;

        /// <summary>
        /// Shown when her energy bar is hovered; empty for an empty host place.
        /// </summary>
        public string EnergyTip = "";

        /// <summary>
        /// The host here is being dragged away.
        /// </summary>
        public bool Dragged;
    }

    public sealed class BenchView
    {
        public int Member;
        public double Energy;
        public bool Available;

        /// <summary>
        /// Why she can't perform (hurt or resting), or none.
        /// </summary>
        public IconKind Icon;

        public string Tip = "";

        /// <summary>
        /// Shown when her energy bar is hovered.
        /// </summary>
        public string EnergyTip = "";

        /// <summary>
        /// Songs she's still out when hurt; empty otherwise.
        /// </summary>
        public string Count = "";
    }

    /// <summary>
    /// A value's colour, as the game colours its values: green for good, red for bad, else its own.
    /// </summary>
    public enum Tone
    {
        Plain,
        Good,
        Bad,
    }

    /// <summary>
    /// A title over a value, as the game's next-song and results screens show their numbers, with an optional
    /// tooltip on its info icon. Hidden while the value is empty.
    /// </summary>
    public sealed class Readout
    {
        public string Title = "";
        public string Value = "";
        public Tone Tone;
        public string Tip = "";

        public bool Shown => Value.Length > 0;
    }

    /// <summary>
    /// A row of the next-song screen's left block: a title, a bar from the rows' shared zero line (right for hype
    /// added, left for hype lost) and its value, with its parts in the tip. Hidden while the title is empty.
    /// </summary>
    public sealed class BarRow
    {
        public string Title = "";
        public string Value = "";
        public Tone Tone;

        /// <summary>
        /// The hype points the bar stands for, signed.
        /// </summary>
        public double Amount;

        /// <summary>
        /// The likely range, drawn faintly behind the bar; NaN for none.
        /// </summary>
        public double Low = double.NaN, High = double.NaN;

        public string Tip = "";

        /// <summary>
        /// A small line under the bar; empty for none.
        /// </summary>
        public string Caption = "";

        public bool Shown => Title.Length > 0;

        public bool HasRange => !double.IsNaN(Low) && !double.IsNaN(High);
    }

    /// <summary>
    /// What the mod draws on the game's hype bar: where the tab's next song or talk break should take the hype, and
    /// where the hype was before the last one.
    /// </summary>
    public sealed class HypeBarView
    {
        public double Hype;

        /// <summary>
        /// NaN with nothing to preview.
        /// </summary>
        public double Target = double.NaN;

        /// <summary>
        /// NaN before the first song.
        /// </summary>
        public double Before = double.NaN;

        public string Tip = "";

        public bool HasTarget => !double.IsNaN(Target);

        public bool HasBefore => !double.IsNaN(Before);
    }

    /// <summary>
    /// An idol being dragged, and where she is over now (null: over nothing that takes her).
    /// </summary>
    public sealed class DragView
    {
        public int Member;
        public DropPoint From;
        public DropPoint? Over;
    }

    /// <summary>
    /// What the lineup panel and the bench column show on a tab, worked out from the session alone (no Unity), so
    /// the tests can check it. While an idol is dragged over a place or a host place, the panel shows the lineup or
    /// hosts the drop would make, or why it's refused.
    /// </summary>
    public sealed class PanelView
    {
        public List<SlotView> Slots = new List<SlotView>();

        public List<HostView> Hosts = new List<HostView>();

        /// <summary>
        /// The idols who could join the tab's set-up, in cast order: on the song's tab everyone not on stage, on
        /// the talk break's everyone not hosting.
        /// </summary>
        public List<BenchView> Bench = new List<BenchView>();

        /// <summary>
        /// The left block's rows: on the song's tab the hype each consideration adds and their total; on the talk
        /// break's only the total, the talk break's, while someone hosts.
        /// </summary>
        public BarRow Venue = new BarRow(), Performance = new BarRow(), Production = new BarRow(), Total = new BarRow();

        public BarRow[] Rows => new[] { Venue, Performance, Production, Total };

        public HypeBarView Bar = new HypeBarView();

        /// <summary>
        /// Why the last drop, or the one hovered now, is refused; empty otherwise.
        /// </summary>
        public string Hint = "";

        /// <summary>
        /// The talk break tab's label, with the talk breaks left.
        /// </summary>
        public string TalkTitle = "";

        /// <summary>
        /// A talk break can be called now: the talk break's tab is disabled otherwise.
        /// </summary>
        public bool TalkOpen;

        /// <summary>
        /// Why no talk break can be called now, shown when the disabled tab is hovered; empty while one can.
        /// </summary>
        public string TalkTip = "";

        /// <summary>
        /// The Start button's label, or null for the game's own ("Start Next Song").
        /// </summary>
        public string StartLabel;

        /// <summary>
        /// An idol is being dragged: the bars' hover strips let the mouse through to the places under them.
        /// </summary>
        public bool Dragging;

        public static string Signed(double value)
        {
            double rounded = Math.Round(value);
            return rounded >= 0 ? "+" + Math.Abs(rounded).ToString(CultureInfo.InvariantCulture) : rounded.ToString(CultureInfo.InvariantCulture);
        }

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

        private static string Percent(double whole) => Signed(whole) + "%";

        private static Tone ToneOf(double whole) => whole > 0 ? Tone.Good : whole < 0 ? Tone.Bad : Tone.Plain;

        /// <summary>
        /// Whole numbers for parts shown one after another, after a sum already shown, so that the parts shown add up
        /// to the rounded total: each is the rounded running sum minus the one before it.
        /// </summary>
        public static double[] RoundedParts(double start, params double[] parts)
        {
            double[] rounded = new double[parts.Length];
            double sum = start, shown = Math.Round(start);
            for (int i = 0; i < parts.Length; i++)
            {
                sum += parts[i];
                double next = Math.Round(sum);
                rounded[i] = next - shown;
                shown = next;
            }
            return rounded;
        }

        private static readonly double[] ScaleSteps = { 5, 10, 15, 20, 30, 40, 50, 75, 100, 150, 200 };

        /// <summary>
        /// The hype points half a bar stands for: the smallest step that holds every amount, and never less than the
        /// scale before it on the same screen, so the bars don't jump while the lineup changes.
        /// </summary>
        /// <param name="previous">The screen's scale so far; NaN on a new screen.</param>
        public static double BarScale(double previous, IEnumerable<double> amounts)
        {
            double need = amounts.Where(a => !double.IsNaN(a)).Select(Math.Abs).DefaultIfEmpty(0).Max();
            double step = ScaleSteps.FirstOrDefault(s => s >= need - 1e-9);
            if (step == 0)
                step = ScaleSteps[ScaleSteps.Length - 1];
            return double.IsNaN(previous) ? step : Math.Max(previous, step);
        }

        /// <summary>
        /// Where a stretch of hype points lies on a row's bar, as shares of its width from the left: 0 hype is the
        /// middle, gains run right and losses left, cut off at the ends.
        /// </summary>
        public static Stretch BarSpan(double from, double to, double scale)
        {
            float At(double amount) => (float)Engine.Clamp(0.5 + amount / (2 * scale), 0, 1);
            return new Stretch { From = At(Math.Min(from, to)), To = At(Math.Max(from, to)) };
        }

        /// <summary>
        /// A stretch of a bar, as shares of its width from the left.
        /// </summary>
        public struct Stretch
        {
            public float From, To;
        }

        /// <summary>
        /// A hype as a share of the game's hype bar, which runs to 200%.
        /// </summary>
        public static float HypeShare(double hype) => (float)Engine.Clamp(hype / Engine.MaxHype, 0, 1);

        private static BarRow Row(string titleKey, double amount, double shown, string tip) => new BarRow
        {
            Title = Texts.Get(titleKey),
            Value = Percent(shown),
            Tone = ToneOf(shown),
            Amount = amount,
            Tip = tip,
        };

        private static string Lines(params string[] lines) => string.Join("\n", lines.Where(line => !string.IsNullOrEmpty(line)));

        /// <summary>
        /// The left block's rows for the song: the hype the venue, the lineup's performance and the production add,
        /// rounded so they add up to the total under them.
        /// </summary>
        private static void SongRows(PanelView view, ShowSession session, int[] lineup, SongPreview preview)
        {
            ShowState state = session.State;
            HypeParts parts = preview.Parts;
            double[] shown = RoundedParts(0, parts.Venue, parts.Performance, parts.Production);
            view.Venue = Row("CONCERTHYPE__VENUE_TITLE", parts.Venue, shown[0], VenueTip(state.Setup, preview.Steps.Gain));
            view.Performance = Row("CONCERTHYPE__PERFORMANCE_TITLE", parts.Performance, shown[1], PerformanceTip(state, lineup, preview));
            view.Production = Row("CONCERTHYPE__PRODUCTION_TITLE", parts.Production, shown[2], ProductionTip(state, preview, shown[2]));
            double total = shown.Sum();
            string low = Percent(Math.Round(preview.Low)), high = Percent(Math.Round(preview.High));
            view.Total = Row("CONCERTHYPE__EXPECTED_TITLE", preview.Change, total, Lines(Texts.Format("CONCERTHYPE__TOTAL_TIP", low, high), LastLine(session)));
            view.Total.Low = preview.Low;
            view.Total.High = preview.High;
            view.Total.Caption = Texts.Format("CONCERTHYPE__LIKELY", low, high);
        }

        public static string VenueTip(ShowSetup setup, bool gain)
        {
            double filled = Engine.Clamp(setup.CapacityFilled, 0, 1);
            string seats = setup.Seats > 0
                ? Texts.Format("CONCERTHYPE__VENUE_TIP", Math.Round(setup.Seats * filled).ToString("N0", CultureInfo.InvariantCulture),
                    setup.Seats.ToString("N0", CultureInfo.InvariantCulture), Whole(100 * filled))
                : Texts.Format("CONCERTHYPE__VENUE_TIP_SHARE", Whole(100 * filled));
            return Lines(seats, gain ? "" : Texts.Get("CONCERTHYPE__VENUE_LOSS"));
        }

        /// <summary>
        /// Each row's average chance of a success, front row first, then the empty places and what counts most.
        /// </summary>
        public static string PerformanceTip(ShowState state, int[] lineup, SongPreview preview)
        {
            Song song = state.CurrentSong;
            List<string> lines = new List<string>();
            int empty = 0;
            for (int row = 0; row <= song.Slots.Max(slot => slot.Row); row++)
            {
                List<int> places = Enumerable.Range(0, song.Slots.Count).Where(j => song.Slots[j].Row == row).ToList();
                List<int> filled = places.Where(j => j < lineup.Length && lineup[j] >= 0 && preview.Slots[j] != null).ToList();
                empty += places.Count - filled.Count;
                if (filled.Count == 0)
                    continue;
                string success = Whole(filled.Average(j => SuccessChance(state.Config, preview.Slots[j].Expected)));
                lines.Add(row == 0 ? Texts.Format("CONCERTHYPE__PERF_CENTER", success) : Texts.Format("CONCERTHYPE__PERF_ROW", row + 1, success));
            }
            if (empty > 0)
                lines.Add(empty == 1 ? Texts.Get("CONCERTHYPE__PERF_EMPTY_ONE") : Texts.Format("CONCERTHYPE__PERF_EMPTY", empty));
            lines.Add(Texts.Get("CONCERTHYPE__PERF_WEIGHTS"));
            return Lines(lines.ToArray());
        }

        /// <summary>
        /// The production's parts, each the hype it adds, rounded so they add up to the Production row's value; the
        /// crowd's mood after the changed lineup's line; the flow from the song before, from the second song on.
        /// </summary>
        /// <param name="shown">The Production row's value.</param>
        public static string ProductionTip(ShowState state, SongPreview preview, double shown)
        {
            HypeParts parts = preview.Parts;
            bool flow = state.SongIndex > 0;
            double[] amounts = flow ? new[] { parts.Quality, parts.Appeal, parts.Novelty, parts.Flow } : new[] { parts.Quality, parts.Appeal, parts.Novelty };
            double[] rounded = RoundedParts(parts.Venue + parts.Performance, amounts);
            // The last line takes what rounding left, so the lines always add up to the row
            rounded[rounded.Length - 1] += shown - rounded.Sum();

            double h = state.Hype, band = state.Config.NoveltyBlendBand;
            string mood = h < 100 - band ? "CONCERTHYPE__BAND_LOW" : h > 100 + band ? "CONCERTHYPE__BAND_HIGH" : "CONCERTHYPE__BAND_MID";
            List<string> lines = new List<string>
            {
                Texts.Format("CONCERTHYPE__PROD_QUALITY", Whole(100 * Engine.Clamp(state.Setup.Production, 0, 1)), Percent(rounded[0])),
                Texts.Format("CONCERTHYPE__PROD_APPEAL", Percent(rounded[1])),
                Texts.Format("CONCERTHYPE__PROD_CHANGED", Whole(100 * preview.Novelty), Percent(rounded[2])),
                Texts.Format("CONCERTHYPE__PROD_MOOD", Whole(h), Texts.Get(mood)),
            };
            if (flow)
            {
                SongMatch matches = preview.Steps.Matches;
                lines.Add(matches == SongMatch.None
                    ? Texts.Format("CONCERTHYPE__PROD_NO_FLOW", Percent(rounded[3]))
                    : Texts.Format("CONCERTHYPE__PROD_FLOW", MatchList(matches), Percent(rounded[3])));
            }
            if (!preview.Steps.Gain)
                lines.Add(Texts.Get("CONCERTHYPE__PROD_LOSS"));
            return Lines(lines.ToArray());
        }

        /// <summary>
        /// "genre", "genre and lyrics", "genre, lyrics and choreography".
        /// </summary>
        public static string MatchList(SongMatch matches)
        {
            List<string> words = new List<string>();
            if ((matches & SongMatch.Genre) != 0)
                words.Add(Texts.Get("CONCERTHYPE__FLOW_GENRE"));
            if ((matches & SongMatch.Lyrics) != 0)
                words.Add(Texts.Get("CONCERTHYPE__FLOW_LYRICS"));
            if ((matches & SongMatch.Choreography) != 0)
                words.Add(Texts.Get("CONCERTHYPE__FLOW_CHOREO"));
            if (words.Count <= 1)
                return words.FirstOrDefault() ?? "";
            return string.Join(", ", words.Take(words.Count - 1)) + " " + Texts.Get("CONCERTHYPE__AND") + " " + words[words.Count - 1];
        }

        /// <summary>
        /// The last song's or talk break's change, as a tip line; empty before the first song.
        /// </summary>
        private static string LastLine(ShowSession session)
        {
            if (session.LastMc != null)
                return Texts.Format("CONCERTHYPE__LAST_TALK_TIP", Percent(Math.Round(session.LastMc.HypeAfter - session.LastMc.HypeBefore)));
            return session.LastResult == null ? "" : Texts.Format("CONCERTHYPE__LAST_SONG_TIP", Percent(Math.Round(session.LastResult.Change)));
        }

        /// <summary>
        /// Where the hype was before the last song or talk break; NaN before the first song.
        /// </summary>
        private static double HypeBeforeLast(ShowSession session) =>
            session.LastMc != null ? session.LastMc.HypeBefore : session.LastResult != null ? session.LastResult.HypeBefore : double.NaN;

        /// <summary>
        /// A gap's hype cost in whole percent, or to one decimal under 1, so a small cost doesn't read as none.
        /// </summary>
        public static string GapCost(double cost) =>
            cost >= 0.95 ? Whole(cost) : Math.Max(0.1, Math.Round(cost, 1)).ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>
        /// The chance in percent that a roll of this expected value is a success or a critical success.
        /// </summary>
        public static double SuccessChance(Config config, double expected)
        {
            double[] chances = Outcomes.Chances(config, expected);
            return chances[(int)Outcome.Success] + chances[(int)Outcome.CriticalSuccess];
        }

        /// <summary>
        /// A place's success bar tip: her chance of a success (critical ones included), of a critical success, and
        /// of a critical failure.
        /// </summary>
        public static string SuccessTipOf(Config config, double expected)
        {
            double[] chances = Outcomes.Chances(config, expected);
            return Texts.Format("CONCERTHYPE__TIP_SUCCESS", Whole(SuccessChance(config, expected)), Whole(chances[(int)Outcome.CriticalSuccess]), Whole(chances[(int)Outcome.CriticalFailure]));
        }

        /// <summary>
        /// A place's energy bar tip: her energy, and what the song costs her in this row at an average performance.
        /// </summary>
        public static string PlaceEnergyTip(Config config, double energy, int row) =>
            Texts.Format("CONCERTHYPE__TIP_ENERGY_PLACE", Whole(energy), Whole(Engine.EnergyCost(config, row, config.Par)));

        /// <param name="message">The last refused drop's reason, shown in the hint line.</param>
        /// <param name="group">The group the bench shows, by ID; -1 for everyone.</param>
        public static PanelView Build(ShowSession session, DragView drag = null, string message = null, FormationTab tab = FormationTab.Song, int group = -1)
        {
            ShowState state = session.State;
            PanelView view = new PanelView();
            int[] lineup = session.Lineup;
            int[] hosts = session.Hosts;
            string hint = message ?? "";

            // Over nothing that takes her, the drop goes to the bench
            DropPoint target = drag?.Over ?? DropPoint.Bench;
            Highlight over = Highlight.None;
            if (drag != null && !state.Finished)
            {
                DropResult result = DropRules.Drop(state, lineup, hosts, drag.Member, drag.From, target);
                over = result.Allowed ? Highlight.Allowed : Highlight.Refused;
                if (result.Allowed)
                {
                    lineup = result.Lineup;
                    hosts = result.Hosts;
                }
                else
                {
                    hint = result.Message;
                }
            }

            Song song = state.CurrentSong;
            if (song != null)
            {
                SongPreview preview = Engine.Preview(state, lineup);
                double[] gaps = Engine.GapCosts(state, lineup);
                for (int j = 0; j < song.Slots.Count; j++)
                {
                    int member = lineup[j];
                    DropPoint place = DropPoint.AtPlace(j);
                    SlotView slot = new SlotView
                    {
                        Member = member,
                        Row = song.Slots[j].Row,
                        Badge = member < 0 ? SlotBadge.Empty : member == song.Slots[j].Original ? SlotBadge.Original : SlotBadge.Substitute,
                        Success = preview.Slots[j] == null ? -1 : SuccessChance(state.Config, preview.Slots[j].Expected),
                        Highlight = HighlightAt(drag, target, place, over),
                        Dragged = InHand(drag, target, place, member),
                    };
                    slot.Icon = IconOf(slot.Badge);
                    slot.Tip = TipOf(slot.Badge);
                    if (member >= 0)
                        slot.EnergyTip = PlaceEnergyTip(state.Config, state.Energy[member], slot.Row);
                    if (preview.Slots[j] != null)
                        slot.SuccessTip = SuccessTipOf(state.Config, preview.Slots[j].Expected);
                    if (member < 0)
                    {
                        string cost = GapCost(gaps[j]);
                        slot.GapText = Texts.Format("CONCERTHYPE__GAP", cost);
                        slot.GapTip = Texts.Format("CONCERTHYPE__GAP_TIP", cost);
                    }
                    view.Slots.Add(slot);
                }
                if (tab == FormationTab.Song)
                {
                    SongRows(view, session, lineup, preview);
                    view.Bar.Target = Engine.Clamp(state.Hype + preview.Change, 0, Engine.MaxHype);
                    view.Bar.Tip = Texts.Format("CONCERTHYPE__BAR_SONG", Whole(state.Hype), Whole(view.Bar.Target));
                }
            }

            for (int k = 0; k < hosts.Length; k++)
            {
                DropPoint place = DropPoint.AtHost(k);
                view.Hosts.Add(new HostView
                {
                    Member = hosts[k],
                    Highlight = HighlightAt(drag, target, place, over),
                    Dragged = InHand(drag, target, place, hosts[k]),
                    EnergyTip = hosts[k] < 0 ? "" : Texts.Format("CONCERTHYPE__TIP_ENERGY_HOST", Whole(state.Energy[hosts[k]])),
                });
            }
            string blocked = DropRules.TalkBlocked(state);
            if (tab == FormationTab.Talk)
            {
                int[] talking = hosts.Where(h => h >= 0).ToArray();
                if (talking.Length > 0 && blocked == null)
                {
                    double after = Engine.McHype(state, talking), change = after - state.Hype;
                    view.Total = Row("CONCERTHYPE__AFTER_TALK_TITLE", change, Math.Round(change), Lines(
                        Texts.Format("CONCERTHYPE__TALK_TOTAL_TIP", Whole(100 * Engine.Talk(state, talking)), Whole(after)), LastLine(session)));
                    view.Bar.Target = after;
                    view.Bar.Tip = Texts.Format("CONCERTHYPE__BAR_TALK", Whole(state.Hype), Whole(after));
                }
            }

            view.Bar.Hype = state.Hype;
            view.Bar.Before = HypeBeforeLast(session);
            view.Bar.Tip = Lines(view.Bar.HasTarget ? view.Bar.Tip : Texts.Format("CONCERTHYPE__BAR_NOW", Whole(state.Hype)), LastLine(session));
            view.Hint = hint;
            view.Dragging = drag != null;
            view.TalkTitle = Texts.Format("CONCERTHYPE__TALK_BREAK", state.McCharges);
            view.TalkOpen = blocked == null;
            view.TalkTip = blocked == null ? "" : Texts.Get(blocked);
            view.StartLabel = session.HasTalk ? Texts.Get("CONCERTHYPE__START_TALK") : null;

            // The bench is the committed lineup's and hosts': a hover preview doesn't move idols in or out of the column
            for (int m = 0; m < state.Setup.Members.Count; m++)
            {
                if (tab == FormationTab.Song ? session.Lineup.Contains(m) : session.Hosts.Contains(m))
                    continue;
                if (group >= 0 && state.Setup.Members[m].Group != group)
                    continue;
                BenchView bench = new BenchView
                {
                    Member = m,
                    Energy = state.Energy[m],
                    Available = state.IsAvailable(m),
                    EnergyTip = Texts.Format("CONCERTHYPE__TIP_ENERGY_BENCH", Whole(state.Energy[m]), Whole(state.Config.BenchRecovery)),
                };
                if (state.Absence[m] > 0)
                {
                    bench.Icon = IconKind.Hurt;
                    bench.Count = state.Absence[m].ToString(CultureInfo.InvariantCulture);
                    bench.Tip = Texts.Format("CONCERTHYPE__TIP_HURT", SongCount(state.Absence[m]));
                }
                else if (state.Exhausted[m])
                {
                    bench.Icon = IconKind.Resting;
                    bench.Tip = Texts.Format("CONCERTHYPE__TIP_RESTING", Whole(state.Config.ReturnThreshold));
                }
                view.Bench.Add(bench);
            }
            return view;
        }

        /// <summary>
        /// A place shows whether the drop over it is allowed, except while she's held over her own place.
        /// </summary>
        private static Highlight HighlightAt(DragView drag, DropPoint target, DropPoint place, Highlight over) =>
            drag != null && target.Equals(place) && !drag.From.Equals(place) ? over : Highlight.None;

        /// <summary>
        /// In the hand: still in her place in the preview, and not dropped back on it.
        /// </summary>
        private static bool InHand(DragView drag, DropPoint target, DropPoint place, int member) =>
            drag != null && drag.From.Equals(place) && !target.Equals(place) && member == drag.Member;

        private static string SongCount(int songs) => songs == 1 ? Texts.Get("CONCERTHYPE__SONG") : Texts.Format("CONCERTHYPE__SONGS", songs);

        /// <summary>
        /// An empty place has no icon: it shows the game's empty senbatsu place.
        /// </summary>
        public static IconKind IconOf(SlotBadge badge) =>
            badge == SlotBadge.Substitute ? IconKind.Substitute : badge == SlotBadge.Empty ? IconKind.None : IconKind.Original;

        public static string TipOf(SlotBadge badge) =>
            badge == SlotBadge.Empty ? "" : Texts.Get(badge == SlotBadge.Substitute ? "CONCERTHYPE__TIP_SUB" : "CONCERTHYPE__TIP_ORIGINAL");

        /// <summary>
        /// The text symbol shown when an icon's picture can't be loaded.
        /// </summary>
        public static string FallbackText(IconKind icon, string count) =>
            icon == IconKind.Hurt ? Texts.Format("CONCERTHYPE__OUT", count)
            : icon == IconKind.Resting ? Texts.Get("CONCERTHYPE__RESTING")
            : icon == IconKind.Substitute ? BadgeText(SlotBadge.Substitute)
            : icon == IconKind.Original ? BadgeText(SlotBadge.Original) : "";

        public static string BadgeText(SlotBadge badge) =>
            badge == SlotBadge.Substitute ? Texts.Get("CONCERTHYPE__BADGE_SUB")
            : badge == SlotBadge.Empty ? ""
            : Texts.Get("CONCERTHYPE__BADGE_ORIGINAL");

        /// <summary>
        /// The show's score on the results screen, with what it's made of in the tip.
        /// </summary>
        public static Readout ScoreReadout(Score score) => new Readout
        {
            Title = Texts.Get("CONCERTHYPE__SCORE_TITLE"),
            Value = Whole(score.Total) + "%",
            Tip = Texts.Format("CONCERTHYPE__SCORE_TIP", Whole(score.Average), Whole(score.Peak), Whole(score.Finale)),
        };

        /// <summary>
        /// Where the formation goes on the next-song screen, in the popup's top-left coordinates: the centre of its
        /// rows and how much its places are scaled.
        /// </summary>
        public struct FormationFrame
        {
            public float CenterX;
            public float Scale;
        }

        /// <summary>
        /// The free space the formation fits in. A row beside the left block (the readouts) stays right of it, one
        /// beside the buttons stays right of those, and every row stays left of the bench and above the bottom.
        /// </summary>
        public struct FormationSpace
        {
            public float ReadoutsRight, ReadoutsBottom, ButtonsRight, ButtonsTop, BenchLeft, Top, Bottom, Margin;

            /// <summary>
            /// The tabs' half width, and the leftmost they may start: they're centred over the formation, above the
            /// left block, and the song's title ends before them.
            /// </summary>
            public float TabsHalf, TabsLeft;
        }

        /// <summary>
        /// A place is its portrait over its energy and success bars, with its icon beside them.
        /// </summary>
        public const float PlaceWidth = 54f, PlaceHeight = 66f, CellWidth = 70f, RowHeight = 70f, MaxScale = 1.4f, MinScale = 0.5f;

        /// <summary>
        /// The largest scale (up to <see cref="MaxScale"/>, in steps of 0.05) at which every row fits the space,
        /// centred as near the middle of the space between the readouts and the bench as it can be.
        /// </summary>
        /// <param name="rowWidths">How many places each row has, from the front row back.</param>
        public static FormationFrame FitFormation(IList<int> rowWidths, FormationSpace space)
        {
            float middle = (space.ReadoutsRight + space.BenchLeft) / 2f;
            for (float scale = MaxScale; scale >= MinScale - 1e-4f; scale -= 0.05f)
            {
                float lowest = space.TabsLeft + space.TabsHalf, highest = space.BenchLeft - space.TabsHalf;
                bool fits = true;
                for (int row = 0; row < rowWidths.Count && fits; row++)
                {
                    float top = space.Top + row * RowHeight * scale, bottom = top + PlaceHeight * scale;
                    float half = ((rowWidths[row] - 1) / 2f * CellWidth + PlaceWidth / 2f) * scale;
                    float left = top < space.ReadoutsBottom ? space.ReadoutsRight : bottom > space.ButtonsTop ? space.ButtonsRight : space.Margin;
                    lowest = Math.Max(lowest, left + half);
                    highest = Math.Min(highest, space.BenchLeft - half);
                    fits = bottom <= space.Bottom;
                }
                if (fits && lowest <= highest)
                    return new FormationFrame { CenterX = Math.Min(Math.Max(middle, lowest), highest), Scale = (float)Math.Round(scale, 2) };
            }
            return new FormationFrame { CenterX = middle, Scale = MinScale };
        }

        /// <summary>
        /// How many places each row has, from the front row to the highest row given; empty with no places.
        /// </summary>
        /// <param name="rows">Each place's row, in place order.</param>
        public static int[] RowSizes(IList<int> rows)
        {
            int[] sizes = new int[rows.Count == 0 ? 0 : rows.Max() + 1];
            foreach (int row in rows)
                sizes[row]++;
            return sizes;
        }

        /// <summary>
        /// The group dropdown's labels, everyone first then each group; empty with fewer than two groups, when there's
        /// nothing to filter. Item i stands for <see cref="GroupAt"/>(i).
        /// </summary>
        public static List<string> GroupChoices(IList<GroupChoice> groups) =>
            groups.Count < 2 ? new List<string>() : new[] { Texts.Get("CONCERTHYPE__ALL_GROUPS") }.Concat(groups.Select(g => g.Title)).ToList();

        /// <summary>
        /// The group ID a dropdown item stands for; -1 for everyone.
        /// </summary>
        public static int GroupAt(IList<GroupChoice> groups, int item) => item >= 1 && item <= groups.Count ? groups[item - 1].Id : -1;

        /// <summary>
        /// The bench in two columns that fill its width: the cells' scale, the height its rows take, and whether
        /// that's more than its area shows, so it scrolls.
        /// </summary>
        public static float TwoColumnBench(int count, float width, float height, float cellWidth, float cellHeight, out float contentHeight, out bool scrolls)
        {
            float scale = width / (2 * cellWidth);
            int rows = (Math.Max(0, count) + 1) / 2;
            contentHeight = rows * cellHeight * scale;
            scrolls = contentHeight > height + 0.5f;
            return scale;
        }
    }
}
