using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    public static partial class MomentContext
    {
        /// <summary>The most new idols an audition's list names before "and N more".</summary>
        public const int AuditionNames = 10;

        /// <summary>
        /// The idols hired from an audition the player launched, once its popup closes. Hires join the main
        /// group. Their audition rarity is the player's card colour, not something fans see, so it isn't sent.
        /// </summary>
        public static Moment Audition(IList<data_girls.girls> hired, Auditions.type scale)
        {
            bool one = hired.Count == 1;
            Moment moment = new Moment(one ? "a new idol joined the group" : "new idols joined the group")
                .Labelled(() => one ? Name(hired[0]) + " joining" : "the new members")
                .Add("New members", () => Names(hired, AuditionNames))
                .Add("Audition", () => Moment.Clean(Translate(ScaleKey(scale))));
            Groups._group group = MainGroup();
            Members.Add(moment, hired.Take(Members.AuditionCap).ToList(), Now, group, newcomer: true);
            History.Joined(moment, hired, data_girls.girl, Now);
            Dictionary<Groups._group, int> changes = new();
            foreach (data_girls.girls girl in hired)
            {
                Change(changes, GroupOf(() => girl.GetGroup()), 1);
            }
            return Agency(moment, group, changes: changes);
        }

        private static string ScaleKey(Auditions.type scale) => scale switch
        {
            Auditions.type.local => "AUDITION__LOCAL",
            Auditions.type.regional => "AUDITION__REGIONAL",
            Auditions.type.nationwide => "AUDITION__NATIONWIDE",
            _ => "",
        };

        /// <param name="rankBefore">Her place by fans in her group before she left, taken before the game moved her fans.</param>
        /// <param name="friends">Her clique before she left, taken before the game took her out of it; null reads it now.</param>
        /// <param name="bulliedBy">Who was bullying her before she left, taken before the game ended it; null reads it now.</param>
        /// <param name="bullied">Who she was bullying before she left, taken before the game took her out of her clique; null reads it now.</param>
        /// <param name="successor">The idol who joined in her place from her goodbye scene, if any.</param>
        /// <param name="sister">The successor is her younger sister.</param>
        /// <param name="staffer">The staff job she took at the agency from her goodbye scene, if any.</param>
        /// <param name="notice">Whether fans knew she was leaving: she announced it, or she left at once.</param>
        /// <param name="announcedOn">The day she announced it, for an announced graduation; unknown leaves the day out.</param>
        public static Moment Graduation(data_girls.girls girl, Members.Rank rankBefore, List<data_girls.girls> friends = null,
            List<data_girls.girls> bulliedBy = null, List<data_girls.girls> bullied = null,
            data_girls.girls successor = null, bool sister = false, staff._staff staffer = null,
            GraduationNotice notice = GraduationNotice.Sudden, DateTime announcedOn = default)
        {
            Moment moment = new Moment("an idol graduated (left the group)")
                .Labelled(() => Name(girl) + "'s graduation");
            GraduateFacts(moment, girl, bulliedBy, bullied, m => m
                // How she left first, so fans write a farewell they saw coming or react to a shock
                .Add("Announced her graduation", () => notice != GraduationNotice.Announced ? ""
                    : History.Ago(announcedOn, Now) is string ago && ago != "" ? ago : "beforehand")
                .Add("How she left", () => notice switch
                {
                    GraduationNotice.Fired => "dismissed by the agency with immediate effect, with no announcement beforehand",
                    GraduationNotice.Sudden => "with immediate effect, with no announcement beforehand",
                    _ => "",
                })
                .Add("After graduating", () => Members.Cut(Moment.Clean(girl.Graduation_Trivia_Text), Members.GraduationTriviaLength))
                // Her staff job, by title and what the staffer does, as for a firing
                .Add("Stays at the agency as", () => staffer == null ? "" : Role(staffer))
                .Add("Joined in her place", () => successor == null ? "" : Successor(girl, successor, sister)));
            Dictionary<data_girls.girls, Members.Rank> ranks = new();
            if (rankBefore != null)
            {
                ranks[girl] = rankBefore;
            }
            // A graduated idol stays in her group's list, so the group is still hers
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group, ranks);
            if (successor != null)
            {
                Members.Add(moment, new List<data_girls.girls> { successor }, Now, group, newcomer: true);
            }
            History.Graduation(moment, girl, data_girls.girl, Now);
            // She left her group; her successor joined the group the game put her in, often another one
            Dictionary<Groups._group, int> changes = new();
            Change(changes, group, -1);
            if (successor != null)
            {
                Change(changes, GroupOf(() => successor.GetGroup()), 1);
            }
            return Agency(moment, group, graduation: true, friends: friends ?? Members.Friends(girl), changes: changes);
        }

        /// <summary>
        /// An idol's graduation announcement, as the game confirms it: she leaves in 3 months and is a member
        /// until then. The graduation's own facts about her, without what only exists once she has left: what
        /// she does after, her successor, her staff job, and the group's smaller size.
        /// </summary>
        public static Moment GraduationAnnouncement(data_girls.girls girl)
        {
            Moment moment = new Moment("an idol announced her graduation: she leaves the group in 3 months and stays a member until then")
                .Labelled(() => Name(girl) + "'s graduation announcement");
            GraduateFacts(moment, girl, null, null, m => m
                .Add("Graduation date", () => DateAhead(girl.Graduation_Date)));
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group);
            History.Graduation(moment, girl, data_girls.girl, Now);
            return Agency(moment, group, graduation: true, friends: Members.Friends(girl));
        }

        /// <summary>
        /// A leaving idol's facts, shared by her graduation and its announcement: her name, then the moment's
        /// own lines about how she leaves, then the bullying fans suspect and the singles she was the center of.
        /// </summary>
        private static void GraduateFacts(Moment moment, data_girls.girls girl, List<data_girls.girls> bulliedBy,
            List<data_girls.girls> bullied, Func<Moment, Moment> leaving)
        {
            Bullying(leaving(moment.Add("Idol", () => Name(girl))), girl, bulliedBy, bullied);
            CenterSinglesOf(moment, girl);
        }

        /// <summary>
        /// The bullying fans suspect around her: only bullying the player has found out. Null lists read it now.
        /// </summary>
        private static Moment Bullying(Moment moment, data_girls.girls girl, List<data_girls.girls> bulliedBy = null, List<data_girls.girls> bullied = null)
        {
            bulliedBy ??= Members.BulliedBy(girl);
            bullied ??= Members.Bullied(girl);
            return moment
                .Add("Suspected to be bullied by", () => string.Join(", ", bulliedBy.Select(Name).ToArray()))
                .Add("Suspected to have bullied", () => string.Join(", ", bullied.Select(Name).ToArray()));
        }

        /// <summary>Her best-selling released singles as center, best first; left out when she centered none.</summary>
        private static void CenterSinglesOf(Moment moment, data_girls.girls girl)
        {
            moment.AddList(Moment.FactsSection, "Singles she was the center of",
                singles.Singles
                    .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null && CenterOf(s) == girl)
                    .OrderByDescending(s => s.ReleaseData.Sales)
                    .Take(CenterSingles)
                    .Select(s => (Func<object>)(() => TitleWithStyle(s))).ToList());
        }

        /// <summary>
        /// An idol going on hiatus, a break from all idol activities. Fans hear the reason by the status she
        /// had: an injury, health reasons for depression (never named: mental health is private), or none.
        /// Her stamina stays out of her line, so it can't hint at more than the reason says.
        /// </summary>
        /// <param name="lastEnd">The planned end of her previous hiatus, read before the game set the new one.</param>
        /// <param name="ended">Her contracts the game broke as she left.</param>
        /// <param name="shows">Her shows, read before the game refilled her slots; named without their cast, which has changed.</param>
        public static Moment HiatusStart(data_girls.girls girl, HiatusReason reason, DateTime lastEnd,
            IList<business.active_proposal> ended, IList<Shows._show> shows)
        {
            Moment moment = new Moment("an idol went on hiatus: a break from all idol activities")
                .Labelled(() => Name(girl) + "'s hiatus")
                .Add("Idol", () => Name(girl))
                .Add("Reason announced", () => HiatusReasons.Text(reason));
            ContractsCancelled(moment, ended);
            moment.AddList(Moment.FactsSection, "Shows she leaves",
                (shows ?? new List<Shows._show>()).Where(s => s != null).Select(s => (Func<object>)(() => History.ShowTitle(s))).ToList());
            moment.Add("Her last hiatus ended", () => History.Known(lastEnd) && lastEnd.Date < Now.Date ? History.Ago(lastEnd, Now) : "");
            return HiatusPeople(moment, girl);
        }

        /// <summary>
        /// An idol back from hiatus, with the reason fans heard when it started. Fans never knew when it was
        /// planned to end, so nothing says whether she came back early. Her shows and contracts ended when she
        /// left, so none are sent.
        /// </summary>
        public static Moment HiatusEnd(data_girls.girls girl, HiatusReason reason)
        {
            Moment moment = new Moment("an idol returned from hiatus")
                .Labelled(() => Name(girl) + "'s return from hiatus")
                .Add("Idol", () => Name(girl))
                .Add("Reason announced at the start", () => HiatusReasons.Text(reason));
            return HiatusPeople(moment, girl);
        }

        /// <summary>Her bullying, center singles, line without stamina, and friends; the hiatus keeps them all.</summary>
        private static Moment HiatusPeople(Moment moment, data_girls.girls girl)
        {
            Bullying(moment, girl);
            CenterSinglesOf(moment, girl);
            Groups._group group = GroupOf(() => girl.GetGroup());
            Members.Add(moment, new List<data_girls.girls> { girl }, Now, group, withStamina: false);
            return Agency(moment, group, friends: Members.Friends(girl));
        }

        /// <summary>
        /// "Uchida Hana, her younger sister" or "Sato Mio, the successor she chose", with the group she joined
        /// when it isn't the graduate's: the game puts every new idol in the main group.
        /// </summary>
        private static string Successor(data_girls.girls girl, data_girls.girls successor, bool sister)
        {
            string text = Name(successor) + (sister ? ", her younger sister" : ", the successor she chose");
            Groups._group hers = GroupOf(() => girl.GetGroup());
            Groups._group joined = GroupOf(() => successor.GetGroup());
            return joined != null && hers != null && joined != hers
                ? text + ", in " + (joined.IsMain() ? "the main group " : "") + Members.GroupName(joined)
                : text;
        }

        /// <summary>The most singles a graduate's list of center singles holds.</summary>
        public const int CenterSingles = 5;

        private static data_girls.girls CenterOf(singles._single single)
        {
            try
            {
                return single.GetCenter();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
