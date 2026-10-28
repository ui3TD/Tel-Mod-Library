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

    public sealed class SlotView
    {
        public int Member = -1;
        public int Row;
        public SlotBadge Badge;

        /// <summary>
        /// The expected roll, rounded; empty for an empty slot.
        /// </summary>
        public string Roll = "";

        public bool Selected;
    }

    public sealed class CastView
    {
        public double Energy;
        public bool Available;

        /// <summary>
        /// Why she can't perform ("out 2", "resting"), or empty.
        /// </summary>
        public string State = "";

        public bool OnStage;
    }

    /// <summary>
    /// What the lineup panel and the cast column show, worked out from the session alone (no Unity), so the tests
    /// can check it.
    /// </summary>
    public sealed class PanelView
    {
        public List<SlotView> Slots = new List<SlotView>();
        public List<CastView> Cast = new List<CastView>();
        public string Hype = "";
        public string Expected = "";
        public string Last = "";
        public string Hint = "";
        public string TalkBreak = "";
        public bool TalkBreakEnabled;

        public static string Signed(double value) => (value >= 0 ? "+" : "") + Math.Round(value).ToString(CultureInfo.InvariantCulture);

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

        public static PanelView Build(ShowSession session, int selectedSlot, bool pickingHost)
        {
            ShowState state = session.State;
            PanelView view = new PanelView();
            Song song = state.CurrentSong;
            SongPreview preview = session.Preview();
            if (song != null && preview != null)
            {
                for (int j = 0; j < song.Slots.Count; j++)
                {
                    int member = session.Lineup[j];
                    view.Slots.Add(new SlotView
                    {
                        Member = member,
                        Row = song.Slots[j].Row,
                        Badge = member < 0 ? SlotBadge.Empty : member == song.Slots[j].Original ? SlotBadge.Original
                            : preview.Forced[j] ? SlotBadge.Fill : SlotBadge.Substitute,
                        Roll = preview.Slots[j] == null ? "" : Whole(preview.Slots[j].Expected),
                        Selected = j == selectedSlot,
                    });
                }
                double h = state.Hype, band = state.Config.NoveltyBlendBand;
                string mood = h < 100 - band ? "CONCERTHYPE__BAND_LOW" : h > 100 + band ? "CONCERTHYPE__BAND_HIGH" : "CONCERTHYPE__BAND_MID";
                view.Hype = Texts.Format("CONCERTHYPE__HYPE", Whole(h), Texts.Get(mood));
                view.Expected = Texts.Format("CONCERTHYPE__EXPECTED", Whole(100 * preview.Novelty), Signed(preview.Change), Signed(preview.Low), Signed(preview.High));
            }

            view.Last = LastLine(session);
            view.Hint = Texts.Get(pickingHost ? "CONCERTHYPE__HINT_HOST" : selectedSlot >= 0 ? "CONCERTHYPE__HINT_PICK" : "CONCERTHYPE__HINT_PLACE");
            view.TalkBreak = Texts.Format("CONCERTHYPE__TALK_BREAK", state.McCharges);
            view.TalkBreakEnabled = Engine.CanCallMc(state) == null;

            for (int m = 0; m < state.Setup.Members.Count; m++)
            {
                string why = state.Absence[m] > 0 ? Texts.Format("CONCERTHYPE__OUT", state.Absence[m])
                    : state.Exhausted[m] ? Texts.Get("CONCERTHYPE__RESTING") : "";
                view.Cast.Add(new CastView
                {
                    Energy = state.Energy[m],
                    Available = state.IsAvailable(m),
                    State = why,
                    OnStage = session.Lineup.Contains(m),
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
    }
}
