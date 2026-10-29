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
    /// The lineup icons, each a PNG of this name in lower case in the mod's Images folder.
    /// </summary>
    public enum IconKind
    {
        None,
        Original,
        Substitute,
        Hosting,
        Hurt,
        Resting,
    }

    /// <summary>
    /// How a place or the host place shows the idol dragged over it.
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
        /// The expected roll, rounded; empty for an empty slot.
        /// </summary>
        public string Roll = "";

        public Highlight Highlight;

        /// <summary>
        /// The idol here is being dragged away.
        /// </summary>
        public bool Dragged;

        /// <summary>
        /// Her own place while she hosts the talk break: shown empty, and hers again for the song.
        /// </summary>
        public bool Hosting;

        public IconKind Icon;

        /// <summary>
        /// The icon's meaning, shown when it's hovered.
        /// </summary>
        public string Tip = "";
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
    /// What the lineup panel and the bench column show, worked out from the session alone (no Unity), so the tests
    /// can check it. While an idol is dragged over a place or the host place, the panel shows the lineup the drop
    /// would make, or why it's refused.
    /// </summary>
    public sealed class PanelView
    {
        public List<SlotView> Slots = new List<SlotView>();

        /// <summary>
        /// The idols who aren't on stage or on the host place, in cast order.
        /// </summary>
        public List<BenchView> Bench = new List<BenchView>();

        /// <summary>
        /// The song's expected hype change, with its range and the crowd's mood in the tip; with a talk break
        /// first, the hype it's expected to leave.
        /// </summary>
        public Readout Expected = new Readout();

        public Readout Changed = new Readout();

        /// <summary>
        /// The last song's or talk break's hype change; hidden before the first song.
        /// </summary>
        public Readout Last = new Readout();

        /// <summary>
        /// Why the last drop, or the one hovered now, is refused; empty otherwise.
        /// </summary>
        public string Hint = "";

        public string TalkTitle = "";
        public int Host = -1;
        public Highlight HostHighlight;

        /// <summary>
        /// A talk break can be called now; the host place is greyed out when it can't and nobody is on it.
        /// </summary>
        public bool TalkOpen;

        /// <summary>
        /// The talk break title and place show from the second song on.
        /// </summary>
        public bool TalkShown;

        /// <summary>
        /// A talk break plays first: the formation is greyed out, with a note under it.
        /// </summary>
        public bool Faded;

        public string FormationNote = "";

        /// <summary>
        /// The Start button's label, or null for the game's own ("Start Next Song").
        /// </summary>
        public string StartLabel;

        public static string Signed(double value)
        {
            double rounded = Math.Round(value);
            return rounded >= 0 ? "+" + Math.Abs(rounded).ToString(CultureInfo.InvariantCulture) : rounded.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A change shown as a value: signed, green when it's not a loss, red when it is.
        /// </summary>
        private static Readout Change(string titleKey, double change) => new Readout
        {
            Title = Texts.Get(titleKey),
            Value = Signed(change),
            Tone = Math.Round(change) >= 0 ? Tone.Good : Tone.Bad,
        };

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

        /// <param name="message">The last refused drop's reason, shown in the hint line.</param>
        public static PanelView Build(ShowSession session, DragView drag = null, string message = null)
        {
            ShowState state = session.State;
            PanelView view = new PanelView();
            int[] lineup = session.Lineup;
            int host = session.Host;
            string hint = message ?? "";

            // Over nothing that takes her, the drop goes to the bench
            DropPoint target = drag?.Over ?? DropPoint.Bench;
            Highlight over = Highlight.None;
            if (drag != null && !state.Finished)
            {
                DropResult result = DropRules.Drop(state, lineup, host, drag.Member, drag.From, target);
                over = result.Allowed ? Highlight.Allowed : Highlight.Refused;
                if (result.Allowed)
                {
                    lineup = result.Lineup;
                    host = result.Host;
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
                        Roll = preview.Slots[j] == null ? "" : Whole(preview.Slots[j].Expected),
                        Highlight = drag != null && target.Equals(place) && !drag.From.Equals(place) ? over : Highlight.None,
                        // In the hand: still in her place in the preview, and not dropped back on it or on the host place
                        Dragged = drag != null && drag.From.Equals(place) && !target.Equals(place) && target.Spot != Spot.Host && member == drag.Member,
                        Hosting = member >= 0 && member == host,
                    };
                    if (slot.Hosting)
                    {
                        slot.Roll = "";
                        slot.Icon = IconKind.Hosting;
                        slot.Tip = Texts.Format("CONCERTHYPE__TIP_HOSTING", session.Name(member));
                    }
                    else
                    {
                        slot.Icon = IconOf(slot.Badge);
                        slot.Tip = TipOf(slot.Badge);
                    }
                    view.Slots.Add(slot);
                }
                double h = state.Hype, band = state.Config.NoveltyBlendBand;
                string mood = h < 100 - band ? "CONCERTHYPE__BAND_LOW" : h > 100 + band ? "CONCERTHYPE__BAND_HIGH" : "CONCERTHYPE__BAND_MID";
                // With a talk break first, the song's lineup waits for it
                view.Faded = host >= 0;
                if (view.Faded)
                {
                    view.Expected = new Readout
                    {
                        Title = Texts.Get("CONCERTHYPE__AFTER_TALK_TITLE"),
                        Value = Whole(Engine.McHype(state, host)) + "%",
                        Tip = Texts.Get("CONCERTHYPE__AFTER_TALK_TIP"),
                    };
                }
                else
                {
                    view.Expected = Change("CONCERTHYPE__EXPECTED_TITLE", preview.Change);
                    view.Expected.Tip = Texts.Format("CONCERTHYPE__EXPECTED_TIP", Signed(preview.Low), Signed(preview.High), Whole(h), Texts.Get(mood));
                }
                view.Changed = new Readout { Title = Texts.Get("CONCERTHYPE__CHANGED_TITLE"), Value = Whole(100 * preview.Novelty) + "%" };
                view.FormationNote = view.Faded ? Texts.Get("CONCERTHYPE__AFTER_TALK") : "";
            }

            view.Last = LastReadout(session);
            view.Hint = hint;
            view.TalkTitle = Texts.Format("CONCERTHYPE__TALK_BREAK", state.McCharges);
            view.Host = host;
            view.HostHighlight = drag != null && target.Spot == Spot.Host && drag.From.Spot != Spot.Host ? over : Highlight.None;
            view.TalkOpen = DropRules.TalkBlocked(state) == null;
            view.TalkShown = state.SongIndex > 0;
            view.StartLabel = session.Host >= 0 ? Texts.Get("CONCERTHYPE__START_TALK") : null;

            // The bench is the committed lineup's: a hover preview doesn't move idols in or out of the column
            for (int m = 0; m < state.Setup.Members.Count; m++)
            {
                if (session.Lineup.Contains(m) || m == session.Host)
                    continue;
                BenchView bench = new BenchView { Member = m, Energy = state.Energy[m], Available = state.IsAvailable(m) };
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
        /// The bench column's grid for this many idols in an area this size: the column count that lets the cells
        /// be largest, never above their full size, with fewer columns on a tie. Returns the cells' scale.
        /// </summary>
        public static float BenchGrid(int count, float width, float height, float cellWidth, float cellHeight, int maxColumns, out int columns)
        {
            columns = 1;
            if (count <= 0)
                return 1f;
            float bestScale = 0f;
            for (int c = 1; c <= Math.Max(1, maxColumns); c++)
            {
                int rows = (count + c - 1) / c;
                float scale = Math.Min(1f, Math.Min(width / (c * cellWidth), height / (rows * cellHeight)));
                if (scale > bestScale + 1e-4f)
                {
                    bestScale = scale;
                    columns = c;
                }
            }
            return bestScale;
        }
    }
}
