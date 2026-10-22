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
        /// A show's first episode, aired as the player launched it. Its viewers, budget, revenue and new fans
        /// aren't public, so none of them is sent.
        /// </summary>
        public static Moment ShowStart(Shows._show show)
        {
            List<data_girls.girls> cast = CastOf(show);
            Moment moment = ShowFacts(new Moment("a new show starring the agency's idols started airing")
                .Labelled(() => "the show \"" + Moment.Clean(show.title) + "\""), show, cast, "first");
            return ShowPeople(moment, show, cast, started: true);
        }

        /// <summary>
        /// A show that aired for a year or more, as the player's cancellation ends it. The year runs from its
        /// first episode, through any concept updates. As at its start, no viewer or money figures are sent.
        /// </summary>
        public static Moment ShowEnd(Shows._show show)
        {
            List<data_girls.girls> cast = CastOf(show);
            Moment moment = ShowFacts(new Moment("a long-running show starring the agency's idols was cancelled")
                    .Labelled(() => "the end of the show \"" + Moment.Clean(show.title) + "\""), show, cast, "last")
                .Add("Ran for", () => History.Known(show.LaunchDate)
                    ? History.Span((Now.Date - show.LaunchDate.Date).Days) + ", " + show.episodeCount + (show.episodeCount == 1 ? " episode" : " episodes")
                    : "");
            return ShowPeople(moment, show, cast, started: false);
        }

        /// <summary>The show by the game's names, and its cast, as of its first or last episode.</summary>
        private static Moment ShowFacts(Moment moment, Shows._show show, List<data_girls.girls> cast, string episode)
        {
            return moment
                .Add("Title", () => "\"" + Moment.Clean(show.title) + "\"")
                .Add("Medium", () => show.medium?.GetTitle())
                .Add("Genre", () => show.genre?.GetTitle())
                // The host's tier as the game names it ("Seasoned"); tier 0 is the game's "No Host"
                .Add("Host", () => show.mc == null ? "" : show.mc.id == 0 ? show.mc.GetTitle() : Moment.Clean(show.mc.GetTitle()) + " host")
                .Add("Cast", () => show.castType == Shows._show._castType.entireGroup ? "the whole group"
                    : show.castType == Shows._show._castType.rotatingCast ? "a rotating cast, in the " + episode + " episode " + Names(cast, 7)
                    : Names(cast, 7));
        }

        /// <summary>The cast's lines, the agency's other shows, and the agency, for the cast's group.</summary>
        private static Moment ShowPeople(Moment moment, Shows._show show, List<data_girls.girls> cast, bool started)
        {
            bool wholeGroup = show.castType == Shows._show._castType.entireGroup;
            List<data_girls.girls> idols = (wholeGroup ? Members.ByFans(cast) : cast).Take(Members.ShowCap).ToList();
            Groups._group group = (wholeGroup || cast.Count == 0 ? null : GroupOf(() => cast[0].GetGroup())) ?? MainGroup();
            Members.Add(moment, idols, Now, group);
            History.Shows(moment, show, Shows.shows, Now, started);
            return Agency(moment, group);
        }

        private static List<data_girls.girls> CastOf(Shows._show show)
        {
            try
            {
                return show.GetCast().Where(g => g != null).Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<data_girls.girls>();
            }
        }

        /// <summary>
        /// A lasting contract, as the player accepts it: an ad or a TV drama, always one idol's. What it pays
        /// and costs her isn't public, so only who, what, for whom and how long is sent.
        /// </summary>
        /// <param name="running">The agency's running contracts, this one included.</param>
        /// <param name="history">Every deal the agency accepted before (business.History); this one isn't in it yet.</param>
        public static Moment ContractStart(business.active_proposal contract, IEnumerable<business.active_proposal> running,
            IEnumerable<business._history> history)
        {
            data_girls.girls girl = contract.Girl;
            bool tv = contract.Type == business._type.tv_drama;
            string kind = Moment.Clean(Part(() => business.GetData(contract.Type)?.GetTitle()));
            Moment moment = new Moment(tv ? "an idol was cast in a TV drama" : "an idol landed an advertising contract")
                .Labelled(() => Name(girl) + (tv ? "'s TV drama" : "'s advertising contract"))
                .Add("Idol", () => Name(girl))
                .Add("Contract", () => kind)
                .Add(tv ? "Studio or channel" : "Brand", () => Moment.Clean(contract.Agent_Name))
                .Add("Drama", () => tv && !string.IsNullOrEmpty(Moment.Clean(contract.Product_Name)) ? "\"" + Moment.Clean(contract.Product_Name) + "\"" : "")
                .Add("Image used", () => data_girls.GetParamName(contract.Skill))
                .Add("Length", () => business.GetData(contract.Type) is business._data data && data.duration > 0
                    ? data.duration + (data.duration == 1 ? " month" : " months") : "");
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group ?? MainGroup());
            History.Contracts(moment, contract, running, history, Now);
            return Agency(moment, group);
        }

        /// <summary>
        /// "Advertisement for Sparkle Soda from Hoshi Beverages, with Takeda Rin, 8 weeks left": the kind's full
        /// title from the game's business data, or its short one.
        /// </summary>
        /// <summary>
        /// The business contracts an answer ended, each by kind, client and idol, as a list; left out when it
        /// ended none.
        /// </summary>
        private static void ContractsCancelled(Moment moment, IList<business.active_proposal> ended)
        {
            moment.AddList(Moment.FactsSection, "Contracts cancelled because of this",
                (ended ?? new List<business.active_proposal>()).Select(c => (Func<object>)(() => Contract(c, weeksLeft: false))).ToList());
        }

        internal static string Contract(business.active_proposal contract, bool weeksLeft = true)
        {
            string kind = Moment.Clean(Part(() => business.GetData(contract.Type)?.GetTitle()));
            if (kind == "")
            {
                kind = Moment.Clean(Part(() => business.GetTypeTitle(contract.Type)));
            }
            string product = Moment.Clean(contract.Product_Name);
            string client = Moment.Clean(contract.Agent_Name);
            string who = contract.isGroup ? "the whole group" : contract.Girl != null ? Name(contract.Girl) : "";
            int weeks = !weeksLeft ? -1 : Part(() => contract.GetWeeksLeft().ToString(CultureInfo.InvariantCulture)) is string w && int.TryParse(w, out int n) ? n : -1;
            return kind + (product == "" ? "" : " for " + product) + (client == "" ? "" : " from " + client)
                + (who == "" ? "" : ", with " + who) + (weeks < 0 ? "" : ", " + weeks + (weeks == 1 ? " week" : " weeks") + " left");
        }
    }
}
