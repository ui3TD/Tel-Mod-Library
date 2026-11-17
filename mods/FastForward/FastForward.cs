using HarmonyLib;
using UnityEngine;
using TMPro;
using System;
using System.Globalization;
using static FastForward.FastForward;

namespace FastForward
{
    /// <summary>
    /// Values and helpers the two patches share.
    /// </summary>
    public class FastForward
    {
        /// <summary>
        /// Name of the in-game variable that holds the speed multiplier. The ModMenus mod stores the slider's value
        /// in it (see assets/JSON/Mod Menu/modmenu.json).
        /// </summary>
        public const string VARID = "FastForward_Multiplier";

        // The speed with no setting or an unreadable one; the same as the Mod Menu slider's default
        public const double DEFAULT_MULTIPLIER = 5d;

        // The game's own fast speed, in in-game minutes per second
        public const double BASE_FAST_SPEED = 200d;

        // The highest multiplier. The Mod Menu slider stops at 20x; this caps any value the menu didn't
        // produce (a hand-edited save, an old setting). Each time tick adds speed/4 minutes; above 28.8x a
        // tick spans over a day and skips onNewDay/onNewWeek.
        public const double MAX_MULTIPLIER = 28d;

        // How far apart two speeds can be and still count as the same speed
        public const double EPSILON = 0.001d;

        /// <summary>
        /// The speed multiplier from the ModMenus setting, between 1 and MAX_MULTIPLIER.
        /// </summary>
        internal static double GetConfiguredMultiplier()
        {
            // Missing (no ModMenus), unreadable or not a number: the default.
            // InvariantCulture reads "2.5" as two and a half on every PC, whatever its regional number format.
            if (!double.TryParse(variables.Get(VARID), NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier)
                || double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                multiplier = DEFAULT_MULTIPLIER;
            }

            if (multiplier < 1d)
            {
                multiplier = 1d;
            }
            else if (multiplier > MAX_MULTIPLIER)
            {
                multiplier = MAX_MULTIPLIER;
            }

            return multiplier;
        }

        internal static double GetConfiguredSpeed()
        {
            return BASE_FAST_SPEED * GetConfiguredMultiplier();
        }

        /// <summary>
        /// Switches the game to fast, then raises the speed and colours the fast button gold.
        /// </summary>
        internal static void ApplySuperFast(mainScript main)
        {
            if (main == null)
            {
                return;
            }

            // The game's own switch to fast runs first, with all its usual effects.
            main.Time_SetState(mainScript._time_state.fast);
            SetSuperSpeed();
        }

        /// <summary>
        /// Speeds fast up to super-fast. Does nothing unless the game is in its fast state, such as when another
        /// mod kept it from switching to fast.
        /// </summary>
        internal static void SetSuperSpeed()
        {
            if (staticVars.timeState != mainScript._time_state.fast)
            {
                return;
            }

            staticVars.dateTimeAddMinutesPerSecond = GetConfiguredSpeed();
            SetFastButtonColor(mainScript.gold32);
        }

        internal static void SetFastButtonColor(Color32 color)
        {
            // Any of these Unity objects can be missing
            if (Camera.main == null)
            {
                return;
            }

            mainScript main = Camera.main.GetComponent<mainScript>();
            if (main == null || main.TimeControls_Fast == null)
            {
                return;
            }

            TextMeshProUGUI fastLabel = main.TimeControls_Fast.GetComponent<TextMeshProUGUI>();
            if (fastLabel != null)
            {
                fastLabel.color = color;
            }
        }
    }

    /// <summary>
    /// Patch class for the TimeControlButton's OnClick method to implement faster time acceleration.
    /// [HarmonyPatch] names the game class and method to patch; nameof makes a renamed method a compile error.
    /// </summary>
    [HarmonyPatch(typeof(TimeControlButton), nameof(TimeControlButton.OnClick))]
    public class TimeControlButton_OnClick
    {
        /// <summary>
        /// Whether this click is a second click on fast forward, which switches on super-fast: the fast button,
        /// while already at fast but not yet super-fast speed. Read before the click changes the speed.
        /// A prefix runs before the game's method. This one returns nothing, so the game's method always runs;
        /// the postfix then speeds it up.
        /// </summary>
        /// <param name="__instance">The instance of the TimeControlButton being clicked.</param>
        /// <param name="__runOriginal">False if another mod's prefix blocked the click.</param>
        /// <param name="__state">Whether the postfix switches on super-fast. Harmony passes it from prefix to postfix.</param>
        public static void Prefix(TimeControlButton __instance, bool __runOriginal, out bool __state)
        {
            // Under these cases, clicking the time control button behaves as normal:
            // 1. Another mod blocked the click.
            // 2. The button being clicked isn't the fast button.
            // 3. The speed isn't currently fast.
            // 4. The speed is already accelerated.
            __state = __runOriginal
                && __instance.Type == mainScript._time_state.fast
                && staticVars.timeState == mainScript._time_state.fast
                && Math.Abs(staticVars.dateTimeAddMinutesPerSecond - GetConfiguredSpeed()) > EPSILON;
        }

        /// <summary>
        /// After the click has run vanilla's switch to fast, speeds it up to super-fast, unless the click was blocked.
        /// A postfix runs after the game's method.
        /// </summary>
        public static void Postfix(bool __state, bool __runOriginal)
        {
            if (__state && __runOriginal)
            {
                SetSuperSpeed();
            }
        }
    }

    /// <summary>
    /// Patch class for the Controls' Update method to implement hotkey-based time acceleration.
    /// </summary>
    [HarmonyPatch(typeof(Controls), nameof(Controls.Update))]
    public class Controls_Update
    {
        /// <summary>The key row for the super-fast key in ModMenus, where the player can change it from 4.</summary>
        public const string KEY_VAR = "FastForward_Key";

        /// <summary>
        /// Postfix method to add hotkey functionality for speeding up time when pressing the super-fast key: 4,
        /// unless the player changes it on ModMenus' Hotkeys tab. Not when another mod skipped Controls.Update to
        /// block the game's hotkeys. The game's Update already handles keys 1 to 3.
        /// </summary>
        public static void Postfix(bool __runOriginal)
        {
            // Another mod blocked the game's hotkeys, or a popup or dialogue is open: hotkeys are off
            if (!__runOriginal || mainScript.IsBlockingHotkeys())
                return;

            if (ModMenusKeys.Pressed(KEY_VAR, KeyCode.Alpha4))
            {
                ApplySuperFast(Camera.main != null ? Camera.main.GetComponent<mainScript>() : null);
            }
        }
    }
}
