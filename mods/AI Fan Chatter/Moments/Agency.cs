using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <summary>
        /// The main group, or null when the game has no groups yet.
        /// </summary>
        internal static Groups._group MainGroup()
        {
            try
            {
                return Groups.Groups_.FirstOrDefault(g => g != null && g.IsMain());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The group a moment is about; null when the game can't say.
        /// </summary>
        private static Groups._group GroupOf(Func<Groups._group> group)
        {
            try
            {
                return group();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The agency's facts, with the moment's group as "the group": a sister group's moment names that
        /// group and the main group it belongs to. Null is the main group.
        /// </summary>
        /// <param name="graduation">The moment is about one idol leaving: her graduation or its announcement.</param>
        /// <param name="friends">The leaving idol's clique, named by group in place of the most popular members.</param>
        /// <param name="rules">Adds the agency rules that matter to this kind of moment, after the concept.</param>
        /// <param name="changes">How many members each group gained (more than 0) or lost (less than 0) in the moment.
        /// Only these groups' sizes are given, one line each; a moment that changes no group gives none.</param>
        private static Moment Agency(Moment moment, Groups._group group, bool graduation = false,
            List<data_girls.girls> friends = null, Func<Moment, Moment> rules = null, IDictionary<Groups._group, int> changes = null)
        {
            const string section = Moment.AgencySection;
            Groups._group main = MainGroup();
            group ??= main;
            bool sister = group != null && !group.IsMain();
            moment
                .AddTo(section, "Group", () => group != null ? Members.GroupName(group) : staticVars.PlayerData.GetGroupName())
                .AddTo(section, "Sister group of", () => !sister ? "" : main != null ? Members.GroupName(main) : staticVars.PlayerData.GetGroupName());
            // Group sizes only where the moment changed them, before and after: "Members in Moons: 4 now, down from 5"
            foreach (KeyValuePair<Groups._group, int> change in (changes ?? new Dictionary<Groups._group, int>())
                .Where(c => c.Key != null && c.Value != 0).OrderBy(c => c.Key == group ? -1 : Groups.Groups_.IndexOf(c.Key)))
            {
                moment.AddTo(section, "Members in " + Members.GroupName(change.Key), () =>
                {
                    int now = change.Key.GetNumberOfNonGraduatedGirls();
                    return now + " now, " + (change.Value > 0 ? "up" : "down") + " from " + (now - change.Value);
                });
            }
            moment
                // The group's totals, once here rather than in every idol's line
                .AddTo(section, "Group formed", () => group != null ? History.Ago(group.Date_Created, Now) : "")
                .AddTo(section, "Singles released by the group", () => Members.ReleasedSingles(group).Count)
                .AddTo(section, "Most popular members", () => graduation || group == null ? ""
                    : string.Join(", ", Members.ByFans(group.GetGirls(true)).Take(Members.MostPopular).Select(Name).ToArray()));
            if (friends != null)
            {
                // A clique can span groups: one line per group, hers first
                foreach (IGrouping<Groups._group, data_girls.girls> mates in (friends ?? new List<data_girls.girls>())
                    .GroupBy(f => GroupOf(() => f.GetGroup())).Where(g => g.Key != null)
                    .OrderBy(g => g.Key == group ? -1 : Groups.Groups_.IndexOf(g.Key)))
                {
                    moment.AddTo(section, "Friends in " + Members.GroupName(mates.Key), () => string.Join(", ", mates.Select(Name).ToArray()));
                }
            }
            moment
                // A graduation is about one idol, so the group's scandals stay out of it
                .AddTo(section, "Group reputation", () => graduation ? "" : Members.AgencyReputation(resources.GetScandalPointsTotal()))
                .AddTo(section, "Group concept", AgencyRules.Concept);
            return (rules?.Invoke(moment) ?? moment)
                .AddTo(section, "Date", () => LongDate(staticVars.dateTime));
        }

        /// <summary>
        /// Adds a gain or loss of members to a group's change; a null group is left out.
        /// </summary>
        private static void Change(Dictionary<Groups._group, int> changes, Groups._group group, int members)
        {
            if (group != null)
            {
                changes[group] = (changes.TryGetValue(group, out int before) ? before : 0) + members;
            }
        }
    }
}
