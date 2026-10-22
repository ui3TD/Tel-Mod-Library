using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for how often this one happened before.</param>
        /// <param name="ended">The business contracts the answer ended.</param>
        public static Moment Event(Event_Manager._activeEvent active, Event_Manager._randomEvent._reply reply, IEnumerable<Event_Manager._activeEvent> log = null,
            IList<business.active_proposal> ended = null)
        {
            Moment moment = new Moment("something happened at the agency")
                // Some of the game's events have no title
                .Labelled(() => Translate(active.data?.title) is string title && title.Trim() != "" ? "the event " + title : "an event at the agency")
                .Add("What happened", () => Translate(active.data?.title))
                // The game's event texts speak to the player, who is the producer
                .Add("You", () => SpeaksToYou(Part(() => active.GetDescription()), Part(() => Translate(reply.text)), Part(() => Translate(reply.description)))
                    ? Producer() : "")
                .Add("Details", () => active.GetDescription())
                .Add("The agency's response", () => active.SwitchVariables(Translate(reply.text)))
                .Add("Outcome", () => reply.description == "-" ? "" : active.SwitchVariables(Translate(reply.description)))
                .Add("People involved", () => string.Join(", ", active.actors.Select(ActorName).Where(n => n != "").ToArray()));
            ContractsCancelled(moment, ended);
            // An event is about its main subject's group
            List<data_girls.girls> idols = Members.ForEvent(active);
            Groups._group group = idols.Count > 0 ? GroupOf(() => idols[0].GetGroup()) : null;
            Members.Add(moment, idols, Now, group ?? MainGroup());
            History.Event(moment, active, log, Now);
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group dating rule", AgencyRules.Dating));
        }

        /// <param name="reactions">The outcomes the game picked for the answer, in order.</param>
        /// <param name="ended">The business contracts the answer ended.</param>
        public static Moment DatingScandal(Event_Templates._active_template active, Event_Templates._template._reply reply,
            IList<Event_Templates._dating_reactions._type> reactions, IList<business.active_proposal> ended = null)
        {
            data_girls.girls idol = active.Actors[0].girl;
            string details = Part(() => ScandalDetails(active));
            string response = Part(() => active.SwitchVariables(reply.Text));
            string outcome = Part(() => active.SwitchVariables(string.Join(" ", reactions.Select(ReactionText).Where(t => t != "").ToArray())));
            Moment moment = new Moment("an idol's dating life became a scandal")
                .Labelled(() => Name(idol) + "'s dating scandal")
                .Add("You", () => SpeaksToYou(details, response, outcome) ? Producer() : "")
                .Add("What happened", () => details)
                .Add("The agency's response", () => response)
                .Add("Outcome", () => outcome)
                .Add("People involved", () => string.Join(", ", active.Actors.Where(a => a != null).Select(ActorName).Where(n => n != "").ToArray()));
            ContractsCancelled(moment, ended);
            Groups._group group = GroupOf(() => idol.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { idol }, Now, group ?? MainGroup());
            return Agency(moment, group, rules: m => m
                .AddTo(Moment.AgencySection, "Group dating rule", AgencyRules.Dating));
        }

        /// <param name="cleared">The scandal points the player spent on the change.</param>
        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for each one's latest scandal.</param>
        public static Moment PolicyChange(policies.value previous, policies.value value, IList<PolicyChange.Cleared> cleared,
            IEnumerable<Event_Manager._activeEvent> log = null)
        {
            string policy = Part(() => value.GetParent()?.Title);
            Payers payers = new(cleared);
            Moment moment = new Moment("the agency changed one of its policies for all groups")
                .Labelled(() => "the " + Moment.Clean(policy) + " policy change")
                .Add("Policy", () => policy)
                .Add("Changed from", () => previous?.Title)
                .Add("Changed to", () => value.Title)
                .Add("Reason for change", () => payers.Whose.Count == 0 ? "" : "scandals involving " + JoinAnd(payers.Whose));
            // Policies are agency-wide; the moment is about the first named idol's group, as an event is
            return payers.AddTo(moment, log);
        }

        /// <param name="budget">Whether the player accepted the staffer's deal to announce budget problems.</param>
        /// <param name="cleared">The scandal points the player spent on the firing.</param>
        /// <param name="log">The game's events so far (Event_Manager.activeEvents), for each one's latest scandal.</param>
        /// <param name="contracts">The agency's running business contracts (business.ActiveProposals).</param>
        public static Moment StaffFiring(staff._staff staffer, bool budget, IList<PolicyChange.Cleared> cleared,
            IEnumerable<Event_Manager._activeEvent> log = null, IEnumerable<business.active_proposal> contracts = null)
        {
            Payers payers = new(cleared);
            string name = Moment.Clean(Part(() => staffer.GetName()));
            Moment moment = new Moment("the agency fired a staff member")
                .Labelled(() => name + "'s firing")
                .Add("Staff member", () => name)
                .Add("Role", () => Role(staffer))
                .Add("Time at the agency", () => History.Known(staffer.HireDate) && staffer.HireDate <= Now ? History.Span((Now.Date - staffer.HireDate.Date).Days) : "")
                // The staffer's deal: the agency announces budget problems instead of blaming them. The scandal
                // history below stays, so fans can doubt it.
                .Add("Announced reason", () => budget ? "budget problems (ostensibly)"
                    : payers.Whose.Count == 0 ? "blamed for the agency's scandals" : "blamed for scandals involving " + JoinAnd(payers.Whose));
            // Production staff: the agency's latest singles released while they worked there
            if (WorkOf(staffer) == staff._skill_type.production)
            {
                moment.AddList(Moment.FactsSection, "Latest singles released while they worked there",
                    singles.Singles
                        .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null
                            && s.ReleaseData.ReleaseDate >= staffer.HireDate && s.ReleaseData.ReleaseDate <= Now)
                        .OrderByDescending(s => s.ReleaseData.ReleaseDate)
                        .Take(StaffSingles)
                        .Select(s => (Func<object>)(() => StaffSingle(s))).ToList());
            }
            // Dealmakers: the agency's biggest running contracts, by weekly pay, which isn't sent
            if (WorkOf(staffer) == staff._skill_type.deals)
            {
                moment.AddList(Moment.FactsSection, "Biggest business contracts running now",
                    (contracts ?? Enumerable.Empty<business.active_proposal>())
                        .Where(c => c != null)
                        .OrderByDescending(c => c.Payment_per_week)
                        .Take(StaffContracts)
                        .Select(c => (Func<object>)(() => Contract(c))).ToList());
            }
            return payers.AddTo(moment, log);
        }

        /// <summary>
        /// "Choreographer (production staff)", "Dance Coach (trainer)": the job title, and what the staffer
        /// mostly did by their skills; the title alone for other work.
        /// </summary>
        private static string Role(staff._staff staffer)
        {
            string title = Part(() => staffer.GetJobTitle());
            string work = WorkOf(staffer) switch
            {
                staff._skill_type.production => "production staff",
                staff._skill_type.coaching => "trainer",
                staff._skill_type.deals => "dealmaker",
                staff._skill_type.physical_health => "physical health care",
                staff._skill_type.mental_health => "mental health care",
                staff._skill_type.cute_pretty => "cute and pretty styling",
                staff._skill_type.cool_sexy => "cool and sexy styling",
                _ => "",
            };
            return work == "" ? title : title + " (" + work + ")";
        }

        /// <summary>
        /// The skill a staffer is strongest in, as the game levels them: a dance coach can grow into
        /// production work, a production manager into deals. On a tie, the role's main skill (primary). none
        /// without skills.
        /// </summary>
        internal static staff._skill_type WorkOf(staff._staff staffer)
        {
            try
            {
                staff._staff._skill top = staffer.skills?.Where(k => k != null)
                    .OrderByDescending(k => k.level).ThenByDescending(k => k.primary).FirstOrDefault();
                return top?.skill_type ?? staff._skill_type.none;
            }
            catch (Exception)
            {
                return staff._skill_type.none;
            }
        }

        /// <summary>The most singles a fired staffer's list holds.</summary>
        public const int StaffSingles = 3;

        /// <summary>The most business contracts a fired dealmaker's list holds.</summary>
        public const int StaffContracts = 3;

        /// <summary>"\"Title\" (Pop, Love lyrics, Cool choreography) by Aozora Chapter, released 2 months ago".</summary>
        private static string StaffSingle(singles._single single)
        {
            Groups._group group = History.GroupOf(single);
            string ago = History.Ago(single.ReleaseData.ReleaseDate, Now);
            return TitleWithStyle(single) + (group == null ? "" : " by " + Members.GroupName(group)) + (ago == "" ? "" : ", released " + ago);
        }

        /// <summary>
        /// The idols and the group whose scandal points the player spent, for a paid policy change or a
        /// firing: their friends and known bullying (as for a graduation), their lines, their latest scandals,
        /// and the moment's group, the first idol's.
        /// </summary>
        private sealed class Payers
        {
            public readonly List<data_girls.girls> Idols;
            public readonly bool Group;
            /// <summary>"Takeda Rin", ..., "the group itself", for the reason line.</summary>
            public readonly List<string> Whose;

            public Payers(IList<PolicyChange.Cleared> cleared)
            {
                Idols = (cleared ?? new List<PolicyChange.Cleared>()).Where(c => !c.Group && c.Girl != null)
                    .Select(c => c.Girl).Distinct().Take(Members.PolicyCap).ToList();
                Group = cleared != null && cleared.Any(c => c.Group);
                Whose = Idols.Select(Name).ToList();
                if (Group)
                {
                    Whose.Add("the group itself");
                }
            }

            public Moment AddTo(Moment moment, IEnumerable<Event_Manager._activeEvent> log)
            {
                // Known friends and bullying, as for a graduation; named per idol when there are several
                foreach (data_girls.girls girl in Idols)
                {
                    string who = Idols.Count == 1 ? "" : Name(girl) + ": ";
                    string Label(string text) => who == "" ? char.ToUpperInvariant(text[0]) + text.Substring(1) : who + text;
                    // A clique can span groups: one line per group, hers first
                    Groups._group own = GroupOf(() => girl.GetGroup());
                    foreach (IGrouping<Groups._group, data_girls.girls> mates in Members.Friends(girl)
                        .GroupBy(f => GroupOf(() => f.GetGroup())).Where(g => g.Key != null)
                        .OrderBy(g => g.Key == own ? -1 : Groups.Groups_.IndexOf(g.Key)))
                    {
                        moment.Add(Label("friends in " + Members.GroupName(mates.Key)), () => string.Join(", ", mates.Select(Name).ToArray()));
                    }
                    moment
                        .Add(Label("suspected to be bullied by"), () => string.Join(", ", Members.BulliedBy(girl).Select(Name).ToArray()))
                        .Add(Label("suspected to have bullied"), () => string.Join(", ", Members.Bullied(girl).Select(Name).ToArray()));
                }
                Groups._group about = Idols.Count > 0 ? GroupOf(() => Idols[0].GetGroup()) : null;
                Members.Add(moment, Idols, Now, about ?? MainGroup());
                History.ScandalsPaidFor(moment, Idols, Group, log, Now);
                return Agency(moment, about);
            }
        }

        /// <summary>
        /// The scandal as the game describes it (_active_template.GetDescription), without the question to the
        /// player that follows the last part: the formula is cut after its last [part].
        /// </summary>
        public static string ScandalDetails(Event_Templates._active_template active)
        {
            string text = active.Template.Formula ?? "";
            int end = text.LastIndexOf(']');
            if (end >= 0)
            {
                text = text.Substring(0, end + 1);
            }
            foreach (Event_Templates._template._part part in active.Template.Parts)
            {
                Event_Templates._template._part._value chosen = active.Parts.FirstOrDefault(v => v != null && v.Parent == part);
                text = text.Replace("[" + part.ID + "]", chosen?.Text ?? "");
            }
            return active.SwitchVariables(text);
        }

        /// <summary>The game's text for an outcome (template_reactions.json), without its colour.</summary>
        private static string ReactionText(Event_Templates._dating_reactions._type reaction)
        {
            return Event_Templates.Dating_Reactions.FirstOrDefault(r => r != null && r.Type == reaction)?.Text ?? "";
        }
    }
}
