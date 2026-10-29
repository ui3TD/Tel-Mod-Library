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
        /// On the song's tab, the song's expected hype change, with its range and the crowd's mood in the tip; on
        /// the talk break's, the hype the talk break is expected to leave, hidden with nobody hosting.
        /// </summary>
        public Readout Expected = new Readout();

        /// <summary>
        /// Shown on the song's tab only.
        /// </summary>

        public Readout Changed = new Readout();

        /// <summary>
        /// The last song's or talk break's hype change; hidden before the first song.
        /// </summary>
        public Readout Last = new Readout();

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

        /// <summary>
        /// A change of hype shown as a value: in the hype bar's percent, signed, green when it's not a loss, red when it is.
        /// </summary>
        private static Readout Change(string titleKey, double change) => new Readout
        {
            Title = Texts.Get(titleKey),
            Value = Signed(change) + "%",
            Tone = Math.Round(change) >= 0 ? Tone.Good : Tone.Bad,
        };

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

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
                    view.Slots.Add(slot);
                }
                if (tab == FormationTab.Song)
                {
                    double h = state.Hype, band = state.Config.NoveltyBlendBand;
                    string mood = h < 100 - band ? "CONCERTHYPE__BAND_LOW" : h > 100 + band ? "CONCERTHYPE__BAND_HIGH" : "CONCERTHYPE__BAND_MID";
                    view.Expected = Change("CONCERTHYPE__EXPECTED_TITLE", preview.Change);
                    view.Expected.Tip = Texts.Format("CONCERTHYPE__EXPECTED_TIP", Signed(preview.Low) + "%", Signed(preview.High) + "%", Whole(h), Texts.Get(mood));
                    view.Changed = new Readout
                    {
                        Title = Texts.Get("CONCERTHYPE__CHANGED_TITLE"),
                        Value = Whole(100 * preview.Novelty) + "%",
                        Tip = Texts.Get("CONCERTHYPE__CHANGED_TIP"),
                    };
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
                    view.Expected = new Readout
                    {
                        Title = Texts.Get("CONCERTHYPE__AFTER_TALK_TITLE"),
                        Value = Whole(Engine.McHype(state, talking)) + "%",
                        Tip = Texts.Get("CONCERTHYPE__AFTER_TALK_TIP"),
                    };
                }
            }

            view.Last = LastReadout(session);
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

        /// <summary>
        /// Who is out after it, and for how long, shows on the bench.
        /// </summary>
        private static Readout LastReadout(ShowSession session)
        {
            if (session.LastMc != null)
                return Change("CONCERTHYPE__LAST_TALK_TITLE", session.LastMc.HypeAfter - session.LastMc.HypeBefore);
            return session.LastResult == null ? new Readout() : Change("CONCERTHYPE__LAST_SONG_TITLE", session.LastResult.Change);
        }

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
        /// The free space the formation fits in. A row beside the readouts stays right of them, one beside the
        /// buttons stays right of those, and every row stays left of the bench and above the bottom.
        /// </summary>
        public struct FormationSpace
        {
            public float ReadoutsRight, ReadoutsBottom, ButtonsRight, ButtonsTop, BenchLeft, Top, Bottom, Margin;

            /// <summary>
            /// The tabs' half width: they're centred over the formation, right of the readouts.
            /// </summary>
            public float TabsHalf;
        }

        /// <summary>
        /// A place is its portrait, its energy and success bars, and its icon under them.
        /// </summary>
        public const float PlaceWidth = 54f, PlaceHeight = 82f, CellWidth = 70f, RowHeight = 82f, MaxScale = 1.4f, MinScale = 0.5f;

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
                float lowest = space.ReadoutsRight + space.TabsHalf, highest = space.BenchLeft - space.TabsHalf;
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
