using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TraitFix.TraitsFix;

namespace TraitFix
{
    // Runs once a week, after every idol's own dating update. Each Indiscreet idol gets one leak roll, so
    // leaks grow with the number of Indiscreet idols, not with the number of secret relationships.
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.UpdateDatingStatus))]
    // An error here would stop the rest of the game's new week, so it's logged (once) and the week goes on.
    public class Data_girls_UpdateDatingStatus
    {
        private static bool loggedError;

        public static void Postfix()
        {
            try
            {
                RollIndiscreetLeaks();
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Debug.LogError("[Traits Fix] Couldn't roll this week's Indiscreet leaks: " + e);
                }
            }
        }
    }

    // Last, so other mods' patches see the leak event as the active one, as they would see any event whose
    // reply shows no results of the game's own.
    [HarmonyPatch(typeof(Event_Manager), nameof(Event_Manager.ConcludeEvent))]
    [HarmonyPriority(Priority.Last)]
    public class Event_Manager_ConcludeEvent_Leak
    {
        public static void Prefix(Event_Manager __instance, out Event_Manager._activeEvent __state)
        {
            __state = __instance?.GetActiveEvent();
        }

        // The leak happens as its reply is chosen, after the game has closed the event popup, as the dating
        // scandal template's replies do. Not if another mod skipped the game's handling of the reply.
        public static void Postfix(Event_Manager._randomEvent._reply reply, Event_Manager._activeEvent __state, bool __runOriginal)
        {
            if (__runOriginal)
                ResolveLeak(__state, reply);
        }
    }

    public partial class TraitsFix
    {
        // Leak events are <prefix><kind>_1 to _<count> in the mod's random_events.json. The culprit is named
        // by the variable <event id><culprit suffix>.
        public const string INDISCREET_EVENT_PREFIX = "traitsfix_indiscreet_";
        public const string INDISCREET_CULPRIT_SUFFIX = "_culprit";
        // Each reply's result is the text <event id><result suffix><reply number from 1>
        public const string INDISCREET_RESULT_SUFFIX = "_result_";
        public const string INDISCREET_LABEL_REACTION_LOW = "IDOL__LEAK_REACTION_LOW";
        public const string INDISCREET_LABEL_REACTION_HIGH = "IDOL__LEAK_REACTION_HIGH";

        // A leak's consequences are the game's dating scandal template (Event_Templates) answered with its
        // "Do nothing" reply: the template's starting damage, its image and dating policy adjustments, half
        // damage for a female partner, then the reply's own buzz and scandal points scaled by damage.
        public const int SCANDAL_TEMPLATE_DAMAGE = 50;
        public const int SCANDAL_TEMPLATE_HIGH_DAMAGE = 50;
        public const int SCANDAL_TEMPLATE_BUZZ = 20;
        public const int SCANDAL_TEMPLATE_SCANDAL_POINTS = 1;
        public const int SCANDAL_TEMPLATE_BLOCK_DAYS = 180;
        public const string INDISCREET_KIND_BOYFRIEND = "bf";
        public const string INDISCREET_KIND_GIRLFRIEND = "gf";
        public const string INDISCREET_KIND_IDOLS = "idols";
        public const int INDISCREET_EVENTS_PER_KIND = 5;
        // Which stories of a kind were shown is the variable <prefix><kind><order suffix>. Once all are
        // shown, the most recent few are kept back.
        public const string INDISCREET_ORDER_SUFFIX = "_order";
        public const int INDISCREET_RECENT_STORIES = 2;

        /// <summary>
        /// Each active Indiscreet idol, in roster order, has a chance to let slip one secret relationship she
        /// isn't part of, picked at random. One that leaks waits for the player, so a second Indiscreet idol
        /// succeeding the same week leaks another.
        /// </summary>
        public static void RollIndiscreetLeaks()
        {
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls(null);
            if (activeGirls == null)
                return;

            foreach (data_girls.girls culprit in activeGirls)
            {
                if (culprit == null || culprit.trait != traits._trait._type.Indiscreet)
                    continue;
                List<data_girls.girls[]> secrets = FindSecretRelationships(culprit);
                if (secrets.Count == 0 || !mainScript.chance(INDISCREET_CHANCE))
                    continue;

                data_girls.girls[] couple = secrets.Count == 1 ? secrets[0] : secrets[RandomIndex(secrets.Count)];
                string kind = couple.Length == 2
                    ? INDISCREET_KIND_IDOLS
                    : couple[0].DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.taken_outside_gf
                        ? INDISCREET_KIND_GIRLFRIEND
                        : INDISCREET_KIND_BOYFRIEND;
                // The event is the leak: its effects apply when it opens, and with no event there's no leak
                StartLeakEvent(kind, culprit, couple);
            }
        }

        /// <summary>
        /// The secret relationships the culprit could let slip: each idol dating outside the agency whose
        /// relationship the player doesn't know (one idol), and each secret idol couple (two idols). Not her
        /// own, and not one whose leak is already waiting.
        /// </summary>
        public static List<data_girls.girls[]> FindSecretRelationships(data_girls.girls culprit)
        {
            List<data_girls.girls[]> secrets = new List<data_girls.girls[]>();
            if (data_girls.girl != null)
            {
                foreach (data_girls.girls girl in data_girls.girl)
                {
                    if (girl?.DatingData != null && girl != culprit && girl.status != data_girls._status.graduated
                        && girl.DatingData.Is_Taken_Outside() && !girl.DatingData.Is_Partner_Status_Known && !IsInWaitingLeak(girl))
                    {
                        secrets.Add(new[] { girl });
                    }
                }
            }
            if (Relationships.RelationshipsData != null)
            {
                foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
                {
                    if (IsSecretCouple(relationship, out data_girls.girls girl0, out data_girls.girls girl1) && girl0 != culprit && girl1 != culprit)
                        secrets.Add(new[] { girl0, girl1 });
                }
            }
            return secrets;
        }

        public static int RandomIndex(int count) => UnityEngine.Random.Range(0, count);

        /// <summary>
        /// Queues one of the kind's leak events to open now, with the couple as its actors and the culprit
        /// named in its text. Returns false, queuing nothing, when no event of the kind can start (all
        /// already waiting, or not loaded). Built here rather than through Event_Manager.StartEvent, which
        /// can only name one actor and doesn't start events while the player has random events turned off.
        /// </summary>
        public static bool StartLeakEvent(string kind, data_girls.girls culprit, data_girls.girls[] couple)
        {
            Event_Manager manager = GetEventManager();
            if (manager == null || manager.activeEvents == null)
                return false;

            List<int> available = new List<int>();
            for (int i = 1; i <= INDISCREET_EVENTS_PER_KIND; i++)
            {
                Event_Manager._randomEvent randomEvent = Event_Manager.GetRandomEvent(INDISCREET_EVENT_PREFIX + kind + "_" + i);
                if (randomEvent?.actor != null && randomEvent.actor.Count == couple.Length && !IsWaiting(manager, randomEvent))
                    available.Add(i);
            }
            if (available.Count == 0)
                return false;

            List<int> shown = GetShownStories(kind);
            int story = PickStory(available, shown);
            Event_Manager._randomEvent chosen = Event_Manager.GetRandomEvent(INDISCREET_EVENT_PREFIX + kind + "_" + story);
            Event_Manager._activeEvent activeEvent = new Event_Manager._activeEvent
            {
                data = chosen,
                state = Event_Manager._activeEvent._state.waiting,
                date = staticVars.dateTime,
            };
            for (int i = 0; i < chosen.actor.Count; i++)
                activeEvent.actors.Add(new Event_Manager._activeEvent._actor { data = chosen.actor[i], girl = couple[i] });
            // Coloured like the actors' names, which the game colours as it fills them in
            SetVariable(chosen.id + INDISCREET_CULPRIT_SUFFIX, ExtensionMethods.color(culprit.girlName(false), mainScript.blue));
            manager.activeEvents.Add(activeEvent);
            shown.Remove(story);
            shown.Insert(0, story);
            SetVariable(INDISCREET_EVENT_PREFIX + kind + INDISCREET_ORDER_SUFFIX, string.Join(",", shown));
            return true;
        }

        /// <summary>
        /// A story not shown yet if there is one; otherwise any but the most recently shown few, so the
        /// stories don't settle into a fixed cycle. Falls back to any available story when only recent
        /// ones are left (the others already waiting).
        /// </summary>
        private static int PickStory(List<int> available, List<int> shown)
        {
            List<int> candidates = available.FindAll(story => !shown.Contains(story));
            if (candidates.Count == 0)
            {
                List<int> recent = shown.GetRange(0, Math.Min(INDISCREET_RECENT_STORIES, shown.Count));
                candidates = available.FindAll(story => !recent.Contains(story));
            }
            if (candidates.Count == 0)
                candidates = available;
            return candidates[RandomIndex(candidates.Count)];
        }

        /// <summary>
        /// The kind's story numbers, most recently shown first, saved with the game. Entries that aren't
        /// one of the kind's stories are dropped, so a damaged record only makes stories count as unseen.
        /// </summary>
        private static List<int> GetShownStories(string kind)
        {
            List<int> shown = new List<int>();
            string record = GetVariable(INDISCREET_EVENT_PREFIX + kind + INDISCREET_ORDER_SUFFIX);
            if (string.IsNullOrEmpty(record))
                return shown;
            foreach (string entry in record.Split(','))
            {
                if (int.TryParse(entry.Trim(), out int story) && story >= 1 && story <= INDISCREET_EVENTS_PER_KIND && !shown.Contains(story))
                    shown.Add(story);
            }
            return shown;
        }

        public static bool IsLeakEvent(Event_Manager._activeEvent activeEvent) =>
            activeEvent?.data?.id != null && activeEvent.data.id.StartsWith(INDISCREET_EVENT_PREFIX);

        /// <summary>
        /// Whether the idol is in a leak event that hasn't been answered yet, so her relationship isn't leaked again.
        /// </summary>
        public static bool IsInWaitingLeak(data_girls.girls girl)
        {
            Event_Manager manager = GetEventManager();
            if (manager == null || manager.activeEvents == null)
                return false;
            foreach (Event_Manager._activeEvent activeEvent in manager.activeEvents)
            {
                if (activeEvent.state == Event_Manager._activeEvent._state.complete || !IsLeakEvent(activeEvent))
                    continue;
                foreach (Event_Manager._activeEvent._actor actor in activeEvent.actors)
                {
                    if (actor.girl == girl)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the dating scandal template's "Do nothing" reply does for this partner and these policies:
        /// the scandal's damage, the buzz and scandal points it scales, and the fans' opinion changes.
        /// </summary>
        public struct LeakOutcome
        {
            public int Damage;
            public int Buzz;
            public int ScandalPoints;
            public float HardcoreOpinion;
            public float CasualOpinion;

            public bool IsHighDamage => Damage >= SCANDAL_TEMPLATE_HIGH_DAMAGE;
        }

        // Event_Templates._active_template.DoCustom, in the same order
        public static LeakOutcome GetLeakOutcome(bool femalePartner, policies._value image, policies._value dating)
        {
            LeakOutcome outcome = new LeakOutcome { Damage = SCANDAL_TEMPLATE_DAMAGE };
            switch (image)
            {
                case policies._value.image_orthodox:
                    outcome.Damage += 50;
                    outcome.HardcoreOpinion -= 2f;
                    break;
                case policies._value.image_rebellious:
                    outcome.Damage -= 15;
                    outcome.CasualOpinion++;
                    outcome.HardcoreOpinion++;
                    break;
            }
            switch (dating)
            {
                case policies._value.dating_forbidden:
                    outcome.HardcoreOpinion--;
                    outcome.Damage += 50;
                    break;
                case policies._value.dating_allowed:
                    outcome.Damage -= 25;
                    outcome.HardcoreOpinion++;
                    outcome.CasualOpinion++;
                    break;
            }
            if (femalePartner)
            {
                outcome.Damage /= 2;
                outcome.HardcoreOpinion++;
                outcome.CasualOpinion++;
            }
            // The "Do nothing" reply
            if (outcome.Damage > 100)
                outcome.Damage = Mathf.RoundToInt(outcome.Damage * 1.5f);
            outcome.Buzz = Mathf.RoundToInt(SCANDAL_TEMPLATE_BUZZ * (outcome.Damage / 100f));
            outcome.ScandalPoints = Mathf.RoundToInt(SCANDAL_TEMPLATE_SCANDAL_POINTS * (outcome.Damage / 100f));
            return outcome;
        }

        /// <summary>
        /// The leak, as its reply is chosen. Each idol in the couple (the event's actors) loses mental
        /// stamina, and gets the dating scandal template's consequences and scandal flags; the player learns
        /// who they're dating; and the results show as the template shows them, after the chosen reply's own
        /// result. Both replies have the same consequences.
        /// </summary>
        public static void ResolveLeak(Event_Manager._activeEvent leak, Event_Manager._randomEvent._reply reply)
        {
            if (!IsLeakEvent(leak))
                return;
            // Answered either way: an event left active would keep its idols from leaking again
            leak.state = Event_Manager._activeEvent._state.complete;
            if (leak.actors == null || leak.actors.Count == 0)
                return;
            List<data_girls.girls> couple = new List<data_girls.girls>();
            foreach (Event_Manager._activeEvent._actor actor in leak.actors)
            {
                if (actor.girl?.DatingData == null)
                    return;
                couple.Add(actor.girl);
            }

            // Two idols dating each other are both women, like an outside girlfriend
            bool femalePartner = couple.Count == 2
                || couple[0].DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.taken_outside_gf;
            policies._value image = policies.GetSelectedPolicyValue(policies._type.image)?.Value ?? policies._value.image_neutral;
            policies._value dating = policies.GetSelectedPolicyValue(policies._type.dating)?.Value ?? policies._value.dating_ambiguous;
            LeakOutcome outcome = GetLeakOutcome(femalePartner, image, dating);
            if (dating == policies._value.dating_forbidden && image == policies._value.image_orthodox)
                Achievements.Unlock(Achievements.ID.ACH_SEISO);

            string text = ExtensionMethods.color(Language.Data[outcome.IsHighDamage ? INDISCREET_LABEL_REACTION_HIGH : INDISCREET_LABEL_REACTION_LOW], mainScript.blue);
            // The reply's result is in the mod's text, since the game would show its own results for it
            int replyNumber = leak.data.reply == null ? 0 : leak.data.reply.IndexOf(reply) + 1;
            if (replyNumber > 0 && Language.Data.TryGetValue(leak.data.id + INDISCREET_RESULT_SUFFIX + replyNumber, out string result))
                text = ExtensionMethods.color(leak.SwitchVariables(result), mainScript.blue) + mainScript.separator + text;
            string staminaLines = "";
            foreach (data_girls.girls girl in couple)
            {
                // The template spares these two (Event_Templates._active_template.MentalDamageDating)
                if (girl.trait == traits._trait._type.Thick_Skin || girl.trait == traits._trait._type.Shameless)
                    continue;
                girl.addParam(data_girls._paramType.mentalStamina, INDISCREET_MENTAL);
                staminaLines += (staminaLines == "" ? "" : "\n") + girl.GetName() + ": "
                    + ExtensionMethods.color(Language.Data["MENTAL_STAMINA"].ToLower() + " " + INDISCREET_MENTAL + Language.Data["PT"], mainScript.red);
            }
            if (staminaLines != "")
                text += mainScript.separator + staminaLines;

            if (outcome.Buzz != 0)
                resources.Add(resources.type.buzz, outcome.Buzz);
            if (outcome.ScandalPoints != 0)
            {
                foreach (data_girls.girls girl in couple)
                    girl.addParam(data_girls._paramType.scandalPoints, outcome.ScandalPoints);
                text += mainScript.separator + Language.Data["SCANDAL_POINTS"] + ExtensionMethods.color(": ", mainScript.blue)
                    + ExtensionMethods.color("+" + outcome.ScandalPoints, mainScript.red);
            }

            if (outcome.IsHighDamage)
            {
                business business = GetBusiness();
                long liability = business == null ? 0L : business.GetLiability(leak.actors);
                if (liability != 0L)
                {
                    text += mainScript.separator + Language.Data["BIZ__LIABILITY"] + ExtensionMethods.color(": ", mainScript.blue)
                        + ExtensionMethods.color(ExtensionMethods.formatMoney(-liability), mainScript.red);
                    business.BreakContracts(leak.actors);
                }
            }

            if (outcome.HardcoreOpinion < 0f || outcome.CasualOpinion < 0f)
            {
                foreach (data_girls.girls girl in couple)
                    text += mainScript.separator + LoseFans(girl, outcome);
            }

            foreach (data_girls.girls girl in couple)
            {
                girl.DatingData.Had_Scandal = true;
                girl.DatingData.Had_Scandal_Ever = true;
                girl.DatingData.Used_Goods = true;
                girl.LastDatingScandal = staticVars.dateTime;
            }
            if (couple.Count == 2)
            {
                MarkIdolRelationshipKnown(couple[0], couple[1]);
            }
            else
            {
                couple[0].DatingData.Is_Partner_Status_Known = true;
                couple[0].DatingData.Partner_Status_Known_To_Player = couple[0].DatingData.Partner_Status;
            }

            ShowLeakResults(text, couple[0], couple.Count > 1 ? couple[1] : null);
        }

        // As the template reports an idol's lost fans
        private static string LoseFans(data_girls.girls girl, LeakOutcome outcome)
        {
            string text = Language.Insert("ACTORS_FANS", girl.GetName()) + ":";
            if (outcome.HardcoreOpinion < 0f)
            {
                girl.AddFans(resources.fanType.hardcore, outcome.HardcoreOpinion, oshihen: true);
                girl.AddAppeal(resources.fanType.hardcore, outcome.HardcoreOpinion);
                text += FansLine(resources.fanType.hardcore, outcome.HardcoreOpinion, girl.Fans_Oshihened_Hardcore, girl.Fans_Quit_Hardcore);
            }
            if (outcome.CasualOpinion < 0f)
            {
                girl.AddFans(resources.fanType.casual, outcome.CasualOpinion, oshihen: true);
                girl.AddAppeal(resources.fanType.casual, outcome.CasualOpinion);
                text += FansLine(resources.fanType.casual, outcome.CasualOpinion, girl.Fans_Oshihened_Casual, girl.Fans_Quit_Casual);
            }
            return text;
        }

        private static string FansLine(resources.fanType type, float opinion, long switched, long quit)
        {
            return ExtensionMethods.color("\n" + resources.GetFanTitle(type) + " " + Language.Data["FANS_OPINION"] + ": " + opinion, mainScript.red)
                + "\n" + resources.GetFanTitle(type) + " " + Language.Data["FANS"] + ": "
                + ExtensionMethods.color(ExtensionMethods.formatNumber(switched) + " " + Language.Data["FANS__SWITCHED"], mainScript.blue) + " | "
                + ExtensionMethods.color(ExtensionMethods.formatNumber(quit) + " " + Language.Data["FANS__ABANDONED"], mainScript.red);
        }

        public static void ShowLeakResults(string text, data_girls.girls girl0, data_girls.girls girl1)
        {
            PopupManager popups = GetGameComponent<PopupManager>();
            if (popups == null)
                return;
            popups.Open(PopupManager._type.static_event);
            popups.GetByType(PopupManager._type.static_event).obj.GetComponent<Event_Static_Popup>().Set(text, girl0, girl1);
        }

        private static bool IsWaiting(Event_Manager manager, Event_Manager._randomEvent randomEvent)
        {
            foreach (Event_Manager._activeEvent activeEvent in manager.activeEvents)
            {
                if (activeEvent?.data == randomEvent && activeEvent.state != Event_Manager._activeEvent._state.complete)
                    return true;
            }
            return false;
        }

        // variables.Set also logs and checks story tasks, neither of which a leak needs
        private static void SetVariable(string name, string value)
        {
            foreach (variables._variable variable in variables.variable)
            {
                if (variable.name == name)
                {
                    variable.value = value;
                    return;
                }
            }
            variables.variable.Add(new variables._variable { name = name, value = value });
        }

        private static string GetVariable(string name)
        {
            if (variables.variable == null)
                return null;
            foreach (variables._variable variable in variables.variable)
            {
                if (variable?.name == name)
                    return variable.value;
            }
            return null;
        }
    }
}
