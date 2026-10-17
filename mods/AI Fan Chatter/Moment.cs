using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    /// <summary>
    /// Something fans react to, as "key: value" lines for the model.
    /// </summary>
    public sealed class Moment
    {
        public readonly string Kind;
        public readonly List<KeyValuePair<string, string>> Facts = new();

        public Moment(string kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// Adds a fact. Each is read on its own, so one the game can't give (an unloaded part, another
        /// mod's change) only leaves that line out.
        /// </summary>
        public Moment Add(string name, Func<object> value)
        {
            try
            {
                string text = Clean(Convert.ToString(value(), CultureInfo.InvariantCulture));
                if (text != "")
                {
                    Facts.Add(new KeyValuePair<string, string>(name, text));
                }
            }
            catch (Exception)
            {
                // Left out
            }
            return this;
        }

        public string Describe()
        {
            StringBuilder sb = new();
            sb.Append("Event: ").Append(Kind);
            foreach (KeyValuePair<string, string> fact in Facts)
            {
                sb.Append('\n').Append(fact.Key).Append(": ").Append(fact.Value);
            }
            return sb.ToString();
        }

        private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// The game's text without its colour and style tags, on one line.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return Tags.Replace(text, "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    /// <summary>
    /// The facts for each kind of moment, read from the game.
    /// </summary>
    public static class MomentContext
    {
        public static string Translate(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "";
            }
            return Language.Data != null && Language.Data.TryGetValue(key, out string text) ? text : key;
        }

        public static string Names(IEnumerable<data_girls.girls> girls, int max)
        {
            return string.Join(", ", girls.Where(g => g != null).Take(max).Select(g => g.GetName(true)).ToArray());
        }

        private static Moment Agency(Moment moment)
        {
            return moment
                .Add("Group", () => staticVars.PlayerData.GetGroupName())
                .Add("Agency fame level", () => resources.GetFameLevel())
                .Add("Total fans", () => resources.GetFansTotal())
                .Add("Date", () => staticVars.dateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));
        }

        public static Moment Single(singles._single single)
        {
            Moment moment = new Moment("the group released a new single")
                .Add("Title", () => single.title)
                .Add("Center", () => single.GetCenter()?.GetName(true))
                .Add("Members", () => Names(single.girls, 7))
                .Add("Genre", () => single.genre?.GetTitle())
                .Add("Lyrics", () => single.lyrics?.GetTitle())
                .Add("Choreography", () => single.choreography?.GetTitle())
                .Add("Sales", () => single.ReleaseData.Sales)
                .Add("Quality (0 to 100)", () => single.ReleaseData.Quality)
                .Add("Fan satisfaction (0 to 100)", () => single.ReleaseData.Fan_Satisfaction)
                .Add("New fans", () => single.ReleaseData.NewFans)
                .Add("Marketing", () => single.marketing.Count > 0 ? single.Marketing_Result_Status.ToString() : "");
            return Agency(moment);
        }

        public static Moment Concert(SEvent_Concerts._concert concert)
        {
            Moment moment = new Moment("the group held a concert")
                .Add("Title", () => concert.GetTitle())
                .Add("Venue", () => SEvent_Concerts.GetVenueTitle(concert.Venue))
                .Add("Audience", () => concert.ProjectedValues.Actual_Audience)
                .Add("Attendance", () => Math.Round(concert.ProjectedValues.Actual_Attendance * 100) + "%")
                .Add("Hype (0 to 100)", () => concert.ProjectedValues.Actual_Hype)
                .Add("Made a profit", () => concert.ProjectedValues.Actual_Revenue >= concert.ProjectedValues.Actual_Cost ? "yes" : "no")
                .Add("Performers", () => Names(concert.GetGirls(false), 10));
            return Agency(moment);
        }

        public static Moment Graduation(data_girls.girls girl, long fansBefore)
        {
            Moment moment = new Moment("an idol graduated (left the group)")
                .Add("Idol", () => girl.GetName(true))
                .Add("Her fame level", () => girl.GetFameLevel())
                .Add("Her fans", () => fansBefore)
                .Add("Years in the group", () => Math.Max(0, (staticVars.dateTime - girl.Hiring_Date).Days / 365));
            return Agency(moment);
        }

        public static Moment Event(Event_Manager._activeEvent active, Event_Manager._randomEvent._reply reply)
        {
            Moment moment = new Moment("something happened at the agency")
                .Add("What happened", () => Translate(active.data?.title))
                .Add("Details", () => active.GetDescription())
                .Add("The agency's response", () => active.SwitchVariables(Translate(reply.text)))
                .Add("Outcome", () => reply.description == "-" ? "" : active.SwitchVariables(Translate(reply.description)))
                .Add("People involved", () => string.Join(", ", active.actors.Select(a => a.GetName(true)).Where(n => n != "").ToArray()));
            return Agency(moment);
        }
    }
}
