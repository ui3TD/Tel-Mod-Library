using ConcertHype.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace ConcertHype
{
    internal sealed class StageDriver : MonoBehaviour
    {
        private void Update() => StageRolls.Tick();
    }

    /// <summary>
    /// The formation on stage while a song plays, spread over the screen: each idol's portrait with a small
    /// marketing wheel above it. The portraits slide from the lineup panel's places to the stage's as the song
    /// starts, and back as it ends, while the bench fades out and in. The wheels spin as the song starts and land
    /// one by one, back row first and the center last; each then gives way to her outcome's effect, which settles
    /// into a small mark until the song ends, with the outcome on hover.
    /// </summary>
    internal static class StageRolls
    {
        private const string Name = "ConcertHype Stage";
        private const float EnterSeconds = 0.5f, LeaveSeconds = 0.4f, PopSeconds = 0.2f;

        /// <summary>
        /// A hurt idol's picture stays this grey after its red flash, as the bench dims an idol who can't perform.
        /// </summary>
        private static readonly Color HurtGrey = new Color(0.7f, 0.7f, 0.7f, 1f);

        /// <summary>
        /// The wheel's and the effect's centre, from the portrait's top-left corner.
        /// </summary>
        private static readonly Vector2 WheelCentre = new Vector2(27f, (float)(StageView.WheelSize / 2 - StageView.PortraitTop));

        private enum Phase
        {
            Entering,
            Playing,
            Leaving,
        }

        private static Concert_Popup owner;
        private static GameObject root;
        private static CanvasGroup group;
        private static readonly List<PortraitWidget> Places = new List<PortraitWidget>();
        private static readonly List<WheelWidget> Wheels = new List<WheelWidget>();
        private static readonly List<Image> Effects = new List<Image>();
        private static readonly List<IconTip> EffectTips = new List<IconTip>();
        private static readonly List<Image[]> Sparkles = new List<Image[]>();

        private static StageView view;
        private static Phase phase;
        private static float phaseTime;
        private static bool started;
        private static float elapsed, waited;
        private static float[] landedAt;

        /// <summary>
        /// Each place's portrait corner on the lineup panel and on stage, and its scale on stage.
        /// </summary>
        private static Vector2[] from, to;
        private static float stageScale = 1f;

        private static bool Alive => root != null && owner != null;

        public static Color ColorOf(Outcome outcome) => outcome switch
        {
            Outcome.CriticalFailure => mainScript.red32,
            Outcome.Failure => mainScript.red_light32,
            Outcome.Success => mainScript.green_light32,
            _ => mainScript.green32,
        };

        /// <summary>
        /// The song now playing, or the talk break's hosts. A failure here only loses the stage, not the show.
        /// </summary>
        public static void Show(Concert_Popup popup, ShowSession session)
        {
            try
            {
                view = StageView.Build(session);
                if (!view.Shown)
                {
                    Hide();
                    return;
                }
                Ensure(popup);
                List<int> rows = view.Slots.Select(slot => slot.Row).ToList();
                from = view.Talk ? LineupPanel.HostCorners(view.Slots.Select(slot => slot.HostPlace).ToList()) : LineupPanel.FormationPlaces(rows);
                StagePlace[] stage = StageView.Layout(rows);
                stageScale = (float)stage[0].Scale;
                to = stage.Select(place => new Vector2(
                    (float)(place.CenterX - StageView.ItemWidth / 2 * place.Scale),
                    (float)(place.CenterY + (StageView.PortraitTop - StageView.ItemHeight / 2) * place.Scale))).ToArray();

                for (int j = 0; j < Places.Count; j++)
                {
                    PortraitWidget widget = Places[j];
                    WheelWidget wheel = j < Wheels.Count ? Wheels[j] : null;
                    // A place nobody performs in isn't shown on stage
                    bool used = j < view.Slots.Count && view.Slots[j].OnStage;
                    widget.Root.SetActive(used);
                    if (wheel != null && !used)
                        wheel.Root.SetActive(false);
                    if (!used)
                        continue;
                    StageSlotView slot = view.Slots[j];
                    widget.SetGirl(session.Show.Girls[slot.Member]);
                    widget.SetEnergy(slot.EnergyBefore);
                    widget.Icon.Set(IconKind.None, "");
                    widget.Label.gameObject.SetActive(false);
                    Effects[j].gameObject.SetActive(false);
                    foreach (Image sparkle in Sparkles[j])
                        sparkle.gameObject.SetActive(false);
                    if (wheel != null)
                    {
                        wheel.Root.SetActive(slot.HasRoll);
                        wheel.SetPop(0f);
                        if (slot.HasRoll)
                        {
                            wheel.SetChances(slot.Chances);
                            wheel.SetAngle(0);
                        }
                    }
                }
                phase = Phase.Entering;
                phaseTime = 0f;
                started = false;
                elapsed = waited = 0f;
                landedAt = Enumerable.Repeat(-1f, view.Slots.Count).ToArray();
                Move(0f);
                group.alpha = 1f;
                // Over the song's title strip, under the bench column and the hype bar
                root.transform.SetSiblingIndex(popup.Song.transform.GetSiblingIndex() + 1);
                root.SetActive(true);
            }
            catch (Exception ex)
            {
                Log.WarnOnce("stage", "couldn't show the rolls on stage: " + ex);
                Hide();
            }
        }

        public static void Hide()
        {
            view = null;
            if (!(root is null))
                Deactivate();
        }

        // Apart from Hide, so a show with no stage built (and the unit tests) never reaches the engine
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Deactivate()
        {
            if (root != null)
                root.SetActive(false);
            BenchColumn.Fade(1f);
        }

        private static void Ensure(Concert_Popup popup)
        {
            if (Alive && ReferenceEquals(owner, popup))
                return;
            owner = popup;
            Places.Clear();
            Wheels.Clear();
            Effects.Clear();
            EffectTips.Clear();
            Sparkles.Clear();
            root = Ui.NewChild(popup.transform, Name).gameObject;
            Ui.Stretch(root);
            group = root.AddComponent<CanvasGroup>();
            root.AddComponent<StageDriver>();

            GameObject labelTemplate = Ui.LabelTemplate(popup);
            Single_Marketing_Roll template = WheelWidget.Template();
            for (int j = 0; j < GameReader.SenbatsuPositions; j++)
            {
                // Nobody is dragged off the stage while she sings
                PortraitWidget widget = PortraitWidget.Make(popup.prefab_concertGirl, labelTemplate, root.transform, "Place " + j, DropPoint.AtPlace(j), iconOnPicture: true,
                    draggable: false);
                // In the wheel's place, landing from its middle: the sparkles under the effect
                Image[] sparkles = new Image[StageView.SparkleCount];
                for (int k = 0; k < sparkles.Length; k++)
                    sparkles[k] = EffectImage(widget.Root.transform, "Sparkle " + k, (float)StageView.SparkleSize, false);
                Sparkles.Add(sparkles);
                Image effect = EffectImage(widget.Root.transform, "Effect", (float)StageView.MarkSize, true);
                Effects.Add(effect);
                EffectTips.Add(effect.gameObject.AddComponent<IconTip>());
                // The hurt icon, larger, in the picture's bottom-right corner, slamming from its middle
                float iconSize = (float)StageView.HurtIconSize, iconMiddle = 47f - iconSize / 2f;
                Ui.PlaceCentred(widget.Icon.Root, iconMiddle, iconMiddle, iconSize, iconSize);
                Places.Add(widget);
                if (template != null)
                    Wheels.Add(WheelWidget.Make(template, widget.Root.transform, WheelCentre.x, WheelCentre.y, (float)StageView.WheelSize));
                // Over the wheel, which shrinks away as the effect lands
                foreach (Image sparkle in sparkles)
                    sparkle.transform.SetAsLastSibling();
                effect.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// A picture this wide centred on the wheel, hidden until her wheel lands.
        /// </summary>
        private static Image EffectImage(Transform parent, string name, float size, bool hover)
        {
            GameObject obj = Ui.NewChild(parent, name).gameObject;
            Image image = obj.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = hover;
            Ui.PlaceCentred(obj, WheelCentre.x, WheelCentre.y, size, size);
            Pose(image, new EffectPose { Scale = 1, Alpha = 0 });
            obj.SetActive(false);
            return image;
        }

        /// <summary>
        /// An effect or a sparkle as it looks now, from the wheel's centre. Without its picture it shows as a
        /// patch of the outcome's colour.
        /// </summary>
        private static void Pose(Image image, EffectPose pose, Color? tint = null)
        {
            image.transform.localScale = new Vector3((float)pose.Scale, (float)pose.Scale, 1f);
            ((RectTransform)image.transform).anchoredPosition = new Vector2(WheelCentre.x + (float)pose.OffsetX, -WheelCentre.y - (float)pose.OffsetY);
            Color color = image.sprite != null ? Color.white : tint ?? Color.white;
            color.a = (float)pose.Alpha;
            image.color = color;
        }

        private static float Ease(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        /// <summary>
        /// The portraits this far (0 to 1) from the lineup panel's places to the stage's.
        /// </summary>
        private static void Move(float share)
        {
            float scale = Mathf.Lerp(LineupPanel.FormationScale, stageScale, share);
            for (int j = 0; j < view.Slots.Count && j < Places.Count; j++)
            {
                Places[j].PlaceAt(Vector2.Lerp(from[j], to[j], share), scale);
            }
        }

        public static void Tick()
        {
            if (!Alive || view == null)
                return;
            try
            {
                ShowSession session = ShowSession.Current;
                if (session == null || !ReferenceEquals(owner.Concert, session.Concert))
                {
                    Hide();
                    return;
                }
                if (phase != Phase.Leaving && (view.Talk ? session.PendingTalk == null : session.Pending == null))
                {
                    phase = Phase.Leaving;
                    phaseTime = 0f;
                }
                phaseTime += Time.deltaTime;

                if (phase == Phase.Leaving)
                {
                    float back = Mathf.Clamp01(phaseTime / LeaveSeconds);
                    Move(1f - Ease(back));
                    group.alpha = 1f - back;
                    BenchColumn.Fade(back);
                    if (back >= 1f)
                        Hide();
                    return;
                }

                if (phase == Phase.Entering)
                {
                    float there = Mathf.Clamp01(phaseTime / EnterSeconds);
                    Move(Ease(there));
                    BenchColumn.Fade(1f - there);
                    if (there >= 1f)
                    {
                        phase = Phase.Playing;
                        phaseTime = 0f;
                    }
                }
                float pop = phase == Phase.Playing ? Mathf.Clamp01(phaseTime / PopSeconds) : 0f;

                // The wheels turn with the song's progress bar
                if (!started)
                {
                    float bar = owner.SongProgressBar != null ? owner.SongProgressBar.transform.localScale.x : 0f;
                    waited += Time.unscaledDeltaTime;
                    if (bar > 0f && bar < 0.5f || waited >= 2f)
                        started = true;
                }
                if (started)
                    elapsed += Time.deltaTime;
                float song = Pace.SongSeconds();
                for (int j = 0; j < view.Slots.Count; j++)
                {
                    StageSlotView slot = view.Slots[j];
                    if (!slot.HasRoll)
                        continue;
                    WheelWidget wheel = j < Wheels.Count ? Wheels[j] : null;
                    if (landedAt[j] < 0f)
                    {
                        wheel?.SetPop(pop);
                        double t = Math.Min(1.0, elapsed / Math.Max(0.01, slot.LandAt * song));
                        wheel?.SetAngle(slot.Angle * (1 - Math.Pow(1 - t, 4)));
                        if (started && t >= 1.0)
                            Land(j, slot);
                        continue;
                    }
                    float since = elapsed - landedAt[j];
                    wheel?.SetPop(Mathf.Min(pop, 1f - Mathf.Clamp01(since / PopSeconds)));
                    PortraitWidget widget = Places[j];
                    if (since <= StageView.SettledSeconds + 0.1)
                    {
                        Pose(Effects[j], StageView.EffectMotion(since, slot.Outcome), ColorOf(slot.Outcome));
                        if (StageView.SparkleScale(slot.Outcome) > 0)
                        {
                            for (int k = 0; k < Sparkles[j].Length; k++)
                                Pose(Sparkles[j][k], StageView.SparkleMotion(since, k, slot.Outcome), ColorOf(slot.Outcome));
                        }
                    }
                    if (slot.Hurt)
                    {
                        float iconScale = (float)StageView.Slam(since);
                        widget.Icon.Root.transform.localScale = new Vector3(iconScale, iconScale, 1f);
                    }
                    // A hurt idol stays grey after her red flash; a critical failure only flashes
                    if (widget.Picture != null && (slot.Hurt || slot.Outcome == Outcome.CriticalFailure))
                        widget.Picture.color = Color.Lerp(slot.Hurt ? HurtGrey : Color.white, mainScript.red32, (float)StageView.HurtFlash(since));
                }
            }
            catch (Exception ex)
            {
                Log.WarnOnce("stage", "couldn't show the rolls on stage: " + ex);
                Hide();
            }
        }

        private static void Land(int j, StageSlotView slot)
        {
            landedAt[j] = elapsed;
            PortraitWidget widget = Places[j];
            Image effect = Effects[j];
            effect.sprite = Icons.Get(StageView.EffectOf(slot.Outcome));
            Pose(effect, new EffectPose { Scale = 1, Alpha = 0 });
            effect.gameObject.SetActive(true);
            EffectTips[j].Text = slot.Tip;
            if (StageView.SparkleScale(slot.Outcome) > 0)
            {
                // A success sends out small copies of its own sparkle
                Sprite burst = Icons.Get(slot.Outcome == Outcome.CriticalSuccess ? IconKind.Burst : IconKind.Success);
                foreach (Image sparkle in Sparkles[j])
                {
                    sparkle.sprite = burst;
                    Pose(sparkle, new EffectPose { Scale = 1, Alpha = 0 });
                    sparkle.gameObject.SetActive(burst != null);
                }
            }
            widget.SetEnergy(slot.EnergyAfter);
            if (slot.Hurt)
            {
                widget.Icon.Set(IconKind.Hurt, slot.HurtTip);
                widget.Icon.Root.transform.localScale = Vector3.zero;
                mainScript.playAudioClip("cheers_fail");
            }
            if (slot.Outcome == Outcome.CriticalSuccess)
                mainScript.playAudioClip("spinner_win_crit");
            else if (slot.Outcome == Outcome.CriticalFailure)
                mainScript.playAudioClip("spinner_fail_crit");
        }
    }
}
