using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <summary>
        /// Another mod's event (ModMoments.Trigger): its facts as the mod gave them, the idols it names with the
        /// same lines as every other moment's (without stamina: the mod says what the event is about), and the
        /// agency, whose group is the first named idol's, or the main group when none is named.
        /// </summary>
        /// <param name="facts">"Name: value" lines, already checked by ModMoments.</param>
        /// <param name="idolIds">The game's idol IDs; unknown ones and repeats are left out.</param>
        public static Moment ModEvent(string kind, string label, IEnumerable<string> facts, IEnumerable<int> idolIds)
        {
            Moment moment = new Moment(Moment.Clean(kind))
                .Labelled(() => label);
            foreach (string fact in facts ?? Enumerable.Empty<string>())
            {
                int split = fact.IndexOf(ModMoments.FactSeparator, StringComparison.Ordinal);
                moment.Add(Moment.Clean(fact.Substring(0, split)), () => fact.Substring(split + ModMoments.FactSeparator.Length));
            }
            List<data_girls.girls> idols = (idolIds ?? Enumerable.Empty<int>()).Distinct()
                .Select(IdolByID)
                .Where(girl => girl != null).ToList();
            Groups._group group = idols.Count > 0 ? GroupOf(() => idols[0].GetGroup()) : null;
            Members.Add(moment, idols, Now, group, withStamina: false);
            return Agency(moment, group);
        }

        /// <summary>The idol with the game's ID; null when there's none.</summary>
        private static data_girls.girls IdolByID(int id)
        {
            try
            {
                return data_girls.GetGirlByID(id);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
