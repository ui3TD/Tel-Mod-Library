using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConcertHype
{
    /// <summary>
    /// One place on stage while a song plays (her wheel and how it lands), or a host during a talk break.
    /// </summary>
    public sealed class StageSlotView
    {
        public int Member = -1;
        public int Row;

        /// <summary>
        /// A talk break's host: on stage, with no wheel.
        /// </summary>
        public bool Host;

        /// <summary>
        /// The host place she came from on the talk break's tab.
        /// </summary>
        public int HostPlace;

        public bool OnStage => Member >= 0;

        public bool HasRoll => Member >= 0 && !Host;

        public Outcome Outcome;

        /// <summary>
        /// A critical success or failure: her effect lands bigger, with a pulse or a shake.
        /// </summary>
        public bool Critical => HasRoll && (Outcome == Outcome.CriticalSuccess || Outcome == Outcome.CriticalFailure);

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
        /// The outcome, shown when her effect is hovered.
        /// </summary>
        public string Tip = "";
    }

    /// <summary>
    /// Where one place's item (her wheel over her portrait) sits on stage: its centre in the concert popup's
    /// 1024 x 640 coordinates, from the top left, and the scale every item shares.
    /// </summary>
    public struct StagePlace
    {
        public double CenterX, CenterY, Scale;
    }

    /// <summary>
    /// How a landed effect (or a hurt icon, or a sparkle) looks some time after it lands: its scale, how far it's
    /// moved in pixels (sideways, and down), and its opacity.
    /// </summary>
    public struct EffectPose
    {
        public double Scale, OffsetX, OffsetY, Alpha;
    }

    /// <summary>
    /// What the stage shows while a song plays, worked out from the song's result alone (no Unity). The wheels land
    /// one by one, back row first and the center last. A talk break shows its hosts in one row, with no wheels.
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

        /// <summary>
        /// A talk break's hosts, not a song.
        /// </summary>
        public bool Talk;

        /// <summary>
        /// The stage's free area while a song plays: under the hype bar, over the title strip, the full width (the
        /// bench is faded out).
        /// </summary>
        public const double AreaLeft = 40, AreaRight = 984, AreaTop = 40, AreaBottom = 500;

        /// <summary>
        /// One item at full size: a wheel, a gap, then the portrait with its energy bar.
        /// </summary>
        public const double ItemWidth = 54, ItemHeight = 112, WheelSize = 48, PortraitTop = 54;

        /// <summary>
        /// The least space between two items, and the most from one row or place to the next, so a small
        /// formation doesn't fly apart.
        /// </summary>
        public const double MinGap = 12, MaxRowPitch = 130, MaxPlacePitch = 240;

        /// <summary>
        /// The size of an effect once it has settled into her mark, the size of a critical success's sparkles, and
        /// the hurt icon's size.
        /// </summary>
        public const double MarkSize = 32, SparkleSize = 20, HurtIconSize = 26;

        /// <summary>
        /// The slam of a critical outcome: it starts this large and settles over this long, opaque after the first
        /// moment; then a critical success pulses and a critical failure shakes for a while.
        /// </summary>
        public const double SlamFrom = 2.4, SlamSeconds = 0.35, FadeInSeconds = 0.08;
        public const double AfterSeconds = 0.25, PulseScale = 1.15, ShakePixels = 4, ShakeCount = 3, HurtFlashSeconds = 0.4;

        /// <summary>
        /// A success pops in, a little too big before it settles, then bobs up once; a failure fades in, then
        /// sinks a little and comes back.
        /// </summary>
        public const double PopSeconds = 0.25, BobPixels = 6, FailFadeSeconds = 0.25, SinkPixels = 5;

        /// <summary>
        /// Every effect lands this much larger than her mark (a critical one larger still), holds for a while,
        /// then shrinks into the mark.
        /// </summary>
        public const double MomentScale = 1.25, CriticalMomentScale = 1.5, HoldSeconds = 0.9, ShrinkSeconds = 0.3;

        /// <summary>
        /// A critical success's sparkles fly out this far (in pixels, along each diagonal) and fade, starting
        /// partway through the slam. A success's are this much smaller and fly this much less far.
        /// </summary>
        public const int SparkleCount = 4;
        public const double SparkleDelay = 0.15, SparkleSeconds = 0.6, SparkleDistance = 36, SuccessSparkleScale = 0.7;

        /// <summary>
        /// From this long after it lands, an effect is her mark and no longer moves.
        /// </summary>
        public const double SettledSeconds = HoldSeconds + ShrinkSeconds;

        /// <summary>
        /// A landed effect this long after it landed, in units of her mark: a critical one slams down from large,
        /// dipping once before it settles, then pulses or shakes; a success pops in and bobs; a failure fades in
        /// and sinks. Each then shrinks into her mark.
        /// </summary>
        public static EffectPose EffectMotion(double since, Outcome outcome)
        {
            EffectPose pose = new EffectPose { Scale = 1, Alpha = 1 };
            double after;
            switch (outcome)
            {
                case Outcome.Success:
                {
                    double u = Math.Min(1, Math.Max(0, since / PopSeconds)) - 1;
                    pose.Scale = 1 + 2.70158 * u * u * u + 1.70158 * u * u;
                    after = (since - PopSeconds) / AfterSeconds;
                    if (after > 0 && after < 1)
                        pose.OffsetY = -BobPixels * Math.Sin(Math.PI * after);
                    break;
                }
                case Outcome.Failure:
                    pose.Alpha = Math.Min(1, Math.Max(0, since / FailFadeSeconds));
                    after = (since - FailFadeSeconds) / (2 * AfterSeconds);
                    if (after > 0 && after < 1)
                        pose.OffsetY = SinkPixels * Math.Sin(Math.PI * after);
                    break;
                default:
                {
                    pose.Scale = Slam(since);
                    pose.Alpha = Math.Min(1, Math.Max(0, since / FadeInSeconds));
                    after = (since - SlamSeconds) / AfterSeconds;
                    if (after > 0 && after < 1)
                    {
                        if (outcome == Outcome.CriticalSuccess)
                            pose.Scale *= 1 + (PulseScale - 1) * Math.Sin(Math.PI * after);
                        else
                            pose.OffsetX = ShakePixels * Math.Sin(2 * Math.PI * ShakeCount * after) * (1 - after);
                    }
                    break;
                }
            }
            pose.Scale *= MomentSize(since, outcome == Outcome.CriticalSuccess || outcome == Outcome.CriticalFailure);
            return pose;
        }

        /// <summary>
        /// A slam's scale this long after it lands: from large down to its size, dipping to about 0.9 once.
        /// </summary>
        public static double Slam(double since)
        {
            double t = Math.Min(1, Math.Max(0, since / SlamSeconds));
            return 1 + (SlamFrom - 1) * Math.Pow(1 - t, 3) * Math.Cos(1.5 * Math.PI * t);
        }

        /// <summary>
        /// How large an effect is against her mark: its moment's size, then shrinking into the mark.
        /// </summary>
        public static double MomentSize(double since, bool critical)
        {
            double moment = critical ? CriticalMomentScale : MomentScale;
            double t = Math.Min(1, Math.Max(0, (since - HoldSeconds) / ShrinkSeconds));
            return moment + (1 - moment) * (t * t * (3 - 2 * t));
        }

        /// <summary>
        /// How large an outcome's sparkles are, against a critical success's; 0 for an outcome that sends none.
        /// </summary>
        public static double SparkleScale(Outcome outcome) => outcome switch
        {
            Outcome.CriticalSuccess => 1,
            Outcome.Success => SuccessSparkleScale,
            _ => 0,
        };

        /// <summary>
        /// Sparkle k (0 to 3) of a success or critical success this long after it landed, in units of a critical
        /// success's sparkle: it flies out along its diagonal from the effect's middle, shrinking and fading. A
        /// failure sends none.
        /// </summary>
        public static EffectPose SparkleMotion(double since, int k, Outcome outcome)
        {
            double size = SparkleScale(outcome);
            double u = (since - SparkleDelay) / SparkleSeconds;
            if (size <= 0 || u <= 0 || u >= 1)
                return new EffectPose { Scale = 1, Alpha = 0 };
            double away = size * SparkleDistance * (1 - Math.Pow(1 - u, 3));
            return new EffectPose
            {
                Scale = size * (1 - 0.4 * u),
                OffsetX = (k % 2 == 0 ? -1 : 1) * away,
                OffsetY = (k < 2 ? -1 : 1) * away * 0.8,
                Alpha = 1 - u,
            };
        }

        /// <summary>
        /// How red a picture is this long after her wheel lands (a hurt idol's, or a critical failure's): fully at
        /// once, fading out.
        /// </summary>
        public static double HurtFlash(double since) => Math.Min(1, Math.Max(0, 1 - since / HurtFlashSeconds));

        /// <summary>
        /// The formation spread over the free area: rows evenly apart and centred as a block, each row's places
        /// evenly apart and centred. When the rows don't fit at full size with the least gap between them, every
        /// item shrinks alike.
        /// </summary>
        /// <param name="rows">Each place's row, in place order.</param>
        public static StagePlace[] Layout(IList<int> rows)
        {
            StagePlace[] places = new StagePlace[rows.Count];
            if (rows.Count == 0)
                return places;
            int[] perRow = PanelView.RowSizes(rows);
            int rowCount = perRow.Length;
            int[] placed = new int[rowCount];
            int widest = perRow.Max();

            double width = AreaRight - AreaLeft, height = AreaBottom - AreaTop;
            double scale = Math.Min(1, Math.Min(
                (height - (rowCount - 1) * MinGap) / (rowCount * ItemHeight),
                (width - (widest - 1) * MinGap) / (widest * ItemWidth)));
            double itemHeight = ItemHeight * scale, itemWidth = ItemWidth * scale;
            double rowPitch = rowCount == 1 ? 0 : Math.Min(MaxRowPitch, (height - itemHeight) / (rowCount - 1));
            double placePitch = widest == 1 ? 0 : Math.Min(MaxPlacePitch, (width - itemWidth) / (widest - 1));
            double top = AreaTop + (height - ((rowCount - 1) * rowPitch + itemHeight)) / 2;
            double middle = (AreaLeft + AreaRight) / 2;

            for (int j = 0; j < rows.Count; j++)
            {
                int row = rows[j];
                places[j] = new StagePlace
                {
                    CenterX = middle + (placed[row]++ - (perRow[row] - 1) / 2.0) * placePitch,
                    CenterY = top + row * rowPitch + itemHeight / 2,
                    Scale = scale,
                };
            }
            return places;
        }

        public static StageView Build(ShowSession session) => session.PendingTalk != null
            ? BuildTalk(session.State, session.PendingTalk, session.PendingTalkPlaces)
            : Build(session.State, session.Pending, session.Name);

        /// <summary>
        /// A talk break's hosts in one row, in host order, with their energy (hosts don't recover during it).
        /// </summary>
        /// <param name="places">The host place each host came from.</param>
        public static StageView BuildTalk(ShowState state, McResult talk, IList<int> places)
        {
            StageView view = new StageView { Talk = true };
            if (talk == null)
                return view;
            for (int k = 0; k < talk.Hosts.Count; k++)
            {
                int member = talk.Hosts[k];
                view.Slots.Add(new StageSlotView
                {
                    Member = member,
                    Host = true,
                    HostPlace = k < places.Count ? places[k] : k,
                    EnergyBefore = state.Energy[member],
                    EnergyAfter = state.Energy[member],
                });
            }
            return view;
        }

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
                place.Chances = Outcomes.Chances(config, expected);
                place.Angle = 360 * Turns + 3.6 * Outcomes.WheelPercent(config, expected, slot.Roll);
                place.EnergyBefore = slot.EnergyBefore;
                place.EnergyAfter = slot.EnergyAfter;
                place.Hurt = slot.Accident;
                if (slot.Accident)
                    place.HurtTip = Texts.Format("CONCERTHYPE__TIP_HURT", slot.AbsenceLength == 1 ? Texts.Get("CONCERTHYPE__SONG") : Texts.Format("CONCERTHYPE__SONGS", slot.AbsenceLength));
                place.Tip = Texts.Format("CONCERTHYPE__OUTCOME_TIP", name(slot.Member), OutcomeName(place.Outcome));
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

        /// <summary>
        /// Each outcome's effect.
        /// </summary>
        public static IconKind EffectOf(Outcome outcome) => outcome switch
        {
            Outcome.CriticalFailure => IconKind.CriticalFailure,
            Outcome.Failure => IconKind.Failure,
            Outcome.Success => IconKind.Success,
            _ => IconKind.CriticalSuccess,
        };
    }
}
