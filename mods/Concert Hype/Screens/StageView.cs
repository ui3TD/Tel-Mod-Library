using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ConcertHype
{
    /// <summary>
    /// One place on stage while a song plays: her wheel and how it lands.
    /// </summary>
    public sealed class StageSlotView
    {
        public int Member = -1;
        public int Row;

        public bool HasRoll => Member >= 0;

        public Outcome Outcome;

        /// <summary>
        /// The roll, rounded, shown when her wheel lands.
        /// </summary>
        public string Roll = "";

        /// <summary>
        /// Her chance of each outcome in percent, by <see cref="Model.Outcome"/>: the wheel's arcs.
        /// </summary>
        public double[] Chances = new double[Outcomes.Count];

        /// <summary>
        /// How far the wheel turns, in degrees: 20 turns, then to her roll, as the game's marketing roll.
        /// </summary>
        public double Angle;

        /// <summary>
        /// When her wheel lands, as a share of the song.
        /// </summary>
        public double LandAt;

        public double EnergyBefore;
        public double EnergyAfter;

        /// <summary>
        /// Hurt in this song: the hurt icon shows when her wheel lands.
        /// </summary>
        public bool Hurt;

        public string HurtTip = "";

        /// <summary>
        /// The outcome and her chances, shown when her number is hovered.
        /// </summary>
        public string Tip = "";
    }

    /// <summary>
    /// What the stage shows while a song plays, worked out from the song's result alone (no Unity). The wheels land
    /// one by one, back row first and the center last; a talk break shows no stage.
    /// </summary>
    public sealed class StageView
    {
        /// <summary>
        /// The first and last wheel land at these shares of the song, so the last number shows before it ends.
        /// </summary>
        public const double FirstLanding = 0.35, LastLanding = 0.85;

        public const double Turns = 20;

        /// <summary>
        /// One per place of the song, empty places included.
        /// </summary>
        public List<StageSlotView> Slots = new List<StageSlotView>();

        public bool Shown => Slots.Count > 0;

        public static StageView Build(ShowSession session) => Build(session.State, session.Pending, session.Name);

        public static StageView Build(ShowState state, SongResult result, Func<int, string> name)
        {
            StageView view = new StageView();
            if (result == null)
                return view;
            Config config = state.Config;
            Song song = state.Setup.Songs[result.SongIndex];
            for (int j = 0; j < result.Slots.Length; j++)
            {
                SlotResult slot = result.Slots[j];
                StageSlotView place = new StageSlotView { Member = slot.Member, Row = song.Slots[j].Row };
                view.Slots.Add(place);
                if (slot.Member < 0)
                    continue;
                double expected = slot.Parts.Expected;
                place.Outcome = Outcomes.Of(config, slot.Roll);
                place.Roll = Whole(slot.Roll);
                place.Chances = Outcomes.Chances(config, expected);
                place.Angle = 360 * Turns + 3.6 * Outcomes.WheelPercent(config, expected, slot.Roll);
                place.EnergyBefore = slot.EnergyBefore;
                place.EnergyAfter = slot.EnergyAfter;
                place.Hurt = slot.Accident;
                if (slot.Accident)
                    place.HurtTip = Texts.Format("CONCERTHYPE__TIP_HURT", slot.AbsenceLength == 1 ? Texts.Get("CONCERTHYPE__SONG") : Texts.Format("CONCERTHYPE__SONGS", slot.AbsenceLength));
                place.Tip = Texts.Format("CONCERTHYPE__ROLL_TIP", name(slot.Member), OutcomeName(place.Outcome), place.Roll, Whole(expected),
                    Whole(place.Chances[0]), Whole(place.Chances[1]), Whole(place.Chances[2]), Whole(place.Chances[3]));
            }

            // Back row first, left to right; the center last
            List<StageSlotView> order = view.Slots.Where(s => s.HasRoll).OrderByDescending(s => s.Row).ToList();
            for (int k = 0; k < order.Count; k++)
                order[k].LandAt = order.Count == 1 ? LastLanding : FirstLanding + (LastLanding - FirstLanding) * k / (order.Count - 1);
            return view;
        }

        public static string OutcomeName(Outcome outcome) => Texts.Get(outcome switch
        {
            Outcome.CriticalFailure => "CONCERTHYPE__CRIT_FAIL",
            Outcome.Failure => "CONCERTHYPE__FAIL",
            Outcome.Success => "CONCERTHYPE__SUCCESS",
            _ => "CONCERTHYPE__CRIT_SUCCESS",
        });

        private static string Whole(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }
}
