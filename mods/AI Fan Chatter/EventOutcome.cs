using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    /// <summary>
    /// Whether an event's answer reached the public, judged by its outcome, never by its text: the public
    /// sees fans, fans' opinion, buzz, scandal points and fame. Money, stamina, relationships, business,
    /// dialogues and later events stay inside the agency.
    /// </summary>
    public static class EventOutcome
    {
        /// <summary>
        /// The values the public sees, to compare before and after an answer: the agency's buzz, fame and
        /// scandal points, how many idols are active, and each active idol's fans, fans' opinion of her, fame
        /// and scandal points. Measured, so outcomes another mod applies in code count too.
        /// </summary>
        public static List<double> PublicState()
        {
            List<double> state = new()
            {
                resources.Get(resources.type.buzz),
                resources.Get(resources.type.fame),
                resources.GetScandalPointsTotal(),
            };
            List<data_girls.girls> active = data_girls.girl.Where(g => g != null && g.status != data_girls._status.graduated).ToList();
            state.Add(active.Count);
            foreach (data_girls.girls girl in active)
            {
                state.Add(girl.getParam(data_girls._paramType.famePoints)._val);
                state.Add(girl.getParam(data_girls._paramType.scandalPoints)._val);
                foreach (resources._fan fan in girl.Fans)
                {
                    state.Add(fan.people);
                    state.Add(fan.appeal);
                    state.Add(fan.Ratio);
                    state.Add(fan.Temp);
                    state.Add(fan.Vals.Sum());
                    state.Add(fan.Vals.Count);
                }
            }
            return state;
        }

        public static bool Changed(IList<double> before, IList<double> after) =>
            before == null || after == null || !before.SequenceEqual(after);

        /// <summary>
        /// For an answer given in the past, which can't be measured any more: whether the game data says it
        /// has a public outcome, or the game posted about it on the feed.
        /// </summary>
        public static bool IsPublic(Event_Manager._randomEvent._reply reply)
        {
            if (reply == null)
            {
                return false;
            }
            return (reply.SNS != null && reply.SNS.Count > 0) || (reply.Effects != null && reply.Effects.Any(IsPublic));
        }

        /// <summary>
        /// Whether any of the event's answers can add scandal points, to the agency or to an idol: a scandal,
        /// whichever answer the player gave.
        /// </summary>
        public static bool CanAddScandalPoints(Event_Manager._randomEvent data)
        {
            return data?.reply != null && data.reply.Any(r => r?.Effects != null && r.Effects.Any(effect =>
                effect != null && (effect.parameter == "scandalPoints" || effect.parameter == "param scandalPoints")));
        }

        private static bool IsPublic(data_dialogues._action effect)
        {
            if (effect == null)
            {
                return false;
            }
            string target = effect.target ?? "";
            string parameter = effect.parameter ?? "";
            return target switch
            {
                "resource" => parameter is "buzz" or "scandalPoints" or "fans" or "fame",
                "group" => parameter is "add_fans" or "buzz",
                _ => target.StartsWith("@actor", StringComparison.Ordinal)
                    && parameter is "add_fans" or "add_opinion" or "param famePoints" or "param scandalPoints",
            };
        }
    }
}
