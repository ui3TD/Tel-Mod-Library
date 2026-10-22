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
        /// The yearly awards show, once it's over: what the agency won, what it was nominated for and lost
        /// (with the winner the show announced), and the winners' acceptance speeches. The awards' gameplay
        /// bonuses aren't public, so they aren't sent.
        /// </summary>
        public static Moment Awards(AwardsShow show)
        {
            Moment moment = new Moment("the yearly idol awards show was held")
                .Labelled(() => "the awards show");
            moment.AddList(Moment.FactsSection, "Won",
                show.Nominations.Where(n => n.Won).Select(n => (Func<object>)(() => Award(n))).ToList());
            moment.AddList(Moment.FactsSection, "Nominated but lost",
                show.Nominations.Where(n => !n.Won).Select(n => (Func<object>)(() => Award(n) + LostTo(n))).ToList());
            moment.AddList(Moment.FactsSection, "Acceptance speeches",
                show.Speeches.Where(s => s.Won && s.Girl != null).Select(s => (Func<object>)(() =>
                    show.SpeechTexts.TryGetValue(s, out string said) ? SpeechSaid(s, said) : Speech(s))).ToList());
            // Winners first, then the idols who spoke for the agency's awards, then the nominees who lost
            List<data_girls.girls> idols = show.Nominations.Where(n => n.Won).Select(n => n.Girl)
                .Concat(show.Speeches.Where(s => s.Won).Select(s => s.Girl))
                .Concat(show.Nominations.Where(n => !n.Won).Select(n => n.Girl))
                .Where(g => g != null).Distinct().Take(Members.AwardsCap).ToList();
            Groups._group group = MainGroup();
            Members.Add(moment, idols, Now, group);
            History.Awards(moment, show, global::Awards._Awards, global::Awards._Nominations);
            return Agency(moment, group);
        }

        /// <summary>
        /// "Best Debut Idol: Kondo Tsubasa", "Best Single: \"Title\" (style)", "Most Performances": the
        /// award's title, and the idol or single it's for.
        /// </summary>
        private static string Award(AwardsShow.Nomination nomination)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(nomination.Type)));
            string subject = nomination.Girl != null ? Name(nomination.Girl)
                : nomination.Single != null ? Part(() => TitleWithStyle(nomination.Single))
                : "";
            return subject == "" ? title : title + ": " + subject;
        }

        /// <summary>
        /// ", lost to Saito Yuna of Starlight Ribbon", ", lost to \"Title\" by Starlight Ribbon", ", lost to
        /// Starlight Ribbon": the winner the show announced; ", lost" when it named none.
        /// </summary>
        private static string LostTo(AwardsShow.Nomination nomination)
        {
            string name = nomination.RivalName ?? "";
            string group = nomination.RivalGroup ?? "";
            if (name == "")
            {
                return group == "" ? ", lost" : ", lost to " + group;
            }
            if (nomination.Type == global::Awards._type.best_single)
            {
                return ", lost to \"" + name + "\"" + (group == "" ? "" : " by " + group);
            }
            return ", lost to " + name + (group == "" ? "" : " of " + group);
        }

        /// <summary>
        /// "Best Debut Idol, Kondo Tsubasa: \"Thank you so much...\"": the speech in the words the show gave.
        /// </summary>
        private static string SpeechSaid(global::Awards._speech speech, string said)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(speech.Type)));
            return title + ", " + Name(speech.Girl) + ": \"" + said + "\"";
        }

        /// <summary>
        /// "Best Debut Idol: Kondo Tsubasa thanked the group's producer (Haruto Sato)": who spoke for the award
        /// and whom she thanked, as the player chose in the speeches popup.
        /// </summary>
        private static string Speech(global::Awards._speech speech)
        {
            string title = Moment.Clean(Part(() => global::Awards.GetAwardTitle(speech.Type)));
            string thanked = speech.Thanks switch
            {
                global::Awards._speech._thanks.you => Producer(),
                // The game's name for him, as the speeches popup shows it
                global::Awards._speech._thanks.fujimoto => Language.Data != null && Language.Data.TryGetValue("AWARDS__THANK_FUJIMOTO", out string fujimoto)
                    ? Moment.Clean(fujimoto) : "Fujimoto",
                global::Awards._speech._thanks.staff => speech.Target_Staff == null ? "the staff"
                    : Moment.Clean(Part(() => speech.Target_Staff.GetName())) is string staffer && staffer != ""
                        ? staffer + " (" + Moment.Clean(Part(() => speech.Target_Staff.GetJobTitle())) + ")"
                        : "the staff",
                _ => speech.Target_Girl != null ? Name(speech.Target_Girl) : "",
            };
            return title + ": " + Name(speech.Girl) + (thanked == "" ? " gave an acceptance speech" : " thanked " + thanked);
        }
    }
}
