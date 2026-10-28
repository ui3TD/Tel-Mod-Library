using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ConcertHype
{
    public enum SlotBadge
    {
        Original,
        Substitute,
        Fill,
        Empty,
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
    }

    public sealed class BenchView
    {
        public int Member;
        public double Energy;
        public bool Available;

        /// <summary>
        /// Why she can't perform ("out 2", "resting"), or empty.
        /// </summary>
        public string State = "";
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

        public string Hype = "";
        public string Expected = "";
        public string Last = "";
        public string Hint = "";
        public string TalkTitle = "";
        public string TalkText = "";
        public int Host = -1;
        public Highlight HostHighlight;

        /// <summary>
        /// The Start button's label, or null for the game's own ("Start Next Song").
        /// </summary>
        public string StartLabel;

        public static string Signed(double value) => (value >= 0 ? "+" : "") + Math.Round(value).ToString(CultureInfo.InvariantCulture);

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

        /// <param name="message">The last refused drop's reason, shown in the hint line.</param>
        public static PanelView Build(ShowSession session, DragView drag = null, string message = null)
        {
            ShowState state = session.State;
            PanelView view = new PanelView();
            int[] lineup = session.Lineup;
            int host = session.Host;
            string hint = string.IsNullOrEmpty(message) ? Texts.Get("CONCERTHYPE__HINT_DRAG") : message;

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
                    view.Slots.Add(new SlotView
                    {
                        Member = member,
                        Row = song.Slots[j].Row,
                        Badge = member < 0 ? SlotBadge.Empty : member == song.Slots[j].Original ? SlotBadge.Original
                            : preview.Forced[j] ? SlotBadge.Fill : SlotBadge.Substitute,
                        Roll = preview.Slots[j] == null ? "" : Whole(preview.Slots[j].Expected),
                        Highlight = drag != null && target.Equals(place) && !drag.From.Equals(place) ? over : Highlight.None,
                        // In the hand: still in her place in the preview, and not dropped back on it or on the host place
                        Dragged = drag != null && drag.From.Equals(place) && !target.Equals(place) && target.Spot != Spot.Host && member == drag.Member,
                    });
                }
                double h = state.Hype, band = state.Config.NoveltyBlendBand;
                string mood = h < 100 - band ? "CONCERTHYPE__BAND_LOW" : h > 100 + band ? "CONCERTHYPE__BAND_HIGH" : "CONCERTHYPE__BAND_MID";
                view.Hype = Texts.Format("CONCERTHYPE__HYPE", Whole(h), Texts.Get(mood));
                view.Expected = Texts.Format("CONCERTHYPE__EXPECTED", Whole(100 * preview.Novelty), Signed(preview.Change), Signed(preview.Low), Signed(preview.High));
            }

            view.Last = LastLine(session);
            view.Hint = hint;
            view.TalkTitle = Texts.Format("CONCERTHYPE__TALK_BREAK", state.McCharges);
            view.Host = host;
            view.HostHighlight = drag != null && target.Spot == Spot.Host && drag.From.Spot != Spot.Host ? over : Highlight.None;
            string blocked = DropRules.TalkBlocked(state);
            view.TalkText = host >= 0
                ? Texts.Format("CONCERTHYPE__TALK_PULL", session.Name(host), Whole(Engine.McHype(state, host)))
                : Texts.Get(blocked ?? "CONCERTHYPE__TALK_DROP");
            view.StartLabel = session.Host >= 0 ? Texts.Get("CONCERTHYPE__START_TALK") : null;

            // The bench is the committed lineup's: a hover preview doesn't move idols in or out of the column
            for (int m = 0; m < state.Setup.Members.Count; m++)
            {
                if (session.Lineup.Contains(m) || m == session.Host)
                    continue;
                string why = state.Absence[m] > 0 ? Texts.Format("CONCERTHYPE__OUT", state.Absence[m])
                    : state.Exhausted[m] ? Texts.Get("CONCERTHYPE__RESTING") : "";
                view.Bench.Add(new BenchView
                {
                    Member = m,
                    Energy = state.Energy[m],
                    Available = state.IsAvailable(m),
                    State = why,
                });
            }
            return view;
        }

        private static string LastLine(ShowSession session)
        {
            if (session.LastMc != null)
            {
                McResult mc = session.LastMc;
                return Texts.Format("CONCERTHYPE__TALK_RESULT", session.Name(mc.Host), Whole(mc.HypeBefore), Whole(mc.HypeAfter));
            }
            SongResult last = session.LastResult;
            if (last == null)
                return "";
            List<string> parts = new List<string> { Texts.Format("CONCERTHYPE__LAST_SONG", Signed(last.Change)) };
            foreach (SlotResult slot in last.Slots.Where(s => s.Accident))
            {
                string length = slot.AbsenceLength == 1 ? Texts.Get("CONCERTHYPE__SONG") : Texts.Format("CONCERTHYPE__SONGS", slot.AbsenceLength);
                parts.Add(Texts.Format("CONCERTHYPE__HURT", session.Name(slot.Member), length));
            }
            foreach (int member in last.Exhausted)
                parts.Add(Texts.Format("CONCERTHYPE__EXHAUSTED", session.Name(member)));
            return string.Join("; ", parts);
        }

        public static string BadgeText(SlotBadge badge) =>
            badge == SlotBadge.Substitute ? Texts.Get("CONCERTHYPE__BADGE_SUB")
            : badge == SlotBadge.Fill ? Texts.Get("CONCERTHYPE__BADGE_FILL")
            : badge == SlotBadge.Empty ? Texts.Get("CONCERTHYPE__BADGE_EMPTY")
            : Texts.Get("CONCERTHYPE__BADGE_ORIGINAL");

        public static string ScoreLine(Score score) =>
            Texts.Format("CONCERTHYPE__SCORE", Whole(score.Total), Whole(score.Average), Whole(score.Peak), Whole(score.Finale));

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
