using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AIFanChatter
{
    /// <summary>
    /// A paid policy change: the scandal points the player spent on it, as the game's scandal points popup
    /// holds them until it's next opened.
    /// </summary>
    public static class PolicyChange
    {
        /// <summary>Scandal points the player spent: an idol's own, or the group's.</summary>
        public sealed class Cleared
        {
            public data_girls.girls Girl;
            public bool Group;
            public int Points;
        }

        /// <summary>The popup's private list of the points chosen (ScandalPoints_Popup.Points).</summary>
        internal static readonly FieldInfo PointsField = AccessTools.Field(typeof(ScandalPoints_Popup), "Points");

        /// <summary>
        /// The points the player chose to spend in the popup, once each idol or the group; empty when the
        /// list can't be read.
        /// </summary>
        public static List<Cleared> ClearedPoints()
        {
            List<Cleared> cleared = new();
            try
            {
                if (PointsField?.GetValue(null) is not IEnumerable points)
                {
                    return cleared;
                }
                foreach (ScandalPoints_Popup._points point in points.OfType<ScandalPoints_Popup._points>())
                {
                    if (point.ToRemove > 0 && (point.Group || point.Girl != null))
                    {
                        cleared.Add(new Cleared { Girl = point.Group ? null : point.Girl, Group = point.Group, Points = point.ToRemove });
                    }
                }
            }
            catch (Exception)
            {
                // No reason line
            }
            return cleared;
        }
    }
}
