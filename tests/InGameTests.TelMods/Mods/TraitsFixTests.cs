using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Traits Fix shows an Indiscreet leak as one of its own random events and, when its reply is chosen,
    /// gives the leak the consequences of the game's dating scandal template. The unit tests cover which
    /// event starts, the consequences and the results text, with the game's methods stubbed. These checks
    /// are what only the running game can show.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class TraitsFixTests
    {
        private const string HarmonyId = "com.tel.traitsfix";

        /// <summary>
        /// The game's event popup, as the player sees it: it finds the mod by name and loads the leak's
        /// picture from the mod's folder (Event_Popup.Set, LoadTexture), and fills in the culprit's name
        /// (_activeEvent.SwitchVariables, which needs the game's random generator and player data). Then the
        /// player's click goes through the game's own reply handling (ConcludeEvent closes the popup and,
        /// with the reply's "-" result, shows nothing of its own), after which the leak's results are the
        /// panel left open, starting with the clicked reply's result with its names filled in. Screenshots
        /// of both are recorded.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator LeakPopupAndResults(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            // Allowed dating and a neutral image: no scandal points, contracts or fans to undo afterwards
            business business = Game.Main.Data.GetComponent<business>();
            List<data_girls.girls> girls = data_girls.GetActiveGirls(null).Where(g => business.GetLiability(g) == 0).ToList();
            if (girls.Count < 2)
            {
                ctx.Fail("The fixture save needs two active idols without business contracts");
                yield break;
            }
            data_girls.girls aya = girls[0];
            data_girls.girls culprit = girls[1];

            Type traitsFix = mod.GetType("TraitFix.TraitsFix", true);
            Event_Manager manager = Game.Main.Data.GetComponent<Event_Manager>();
            PopupManager popups = Game.Main.Data.GetComponent<PopupManager>();
            Event_Popup popup = popups.GetByType(PopupManager._type.random_event).obj.GetComponent<Event_Popup>();
            string shots = Screenshots();
            ctx.Record("screenshots", shots);

            GirlState before = new GirlState(aya);
            data_girls.girls._dating_data._partner_status partnerWas = aya.DatingData.Partner_Status;
            DateTime latestEvent = Traverse.Create(typeof(Event_Overlord)).Field("Latest_Event").GetValue<DateTime>();
            using (TestTools.Restore(() => Traverse.Create(typeof(Event_Overlord)).Field("Latest_Event").SetValue(latestEvent)))
            using (TestTools.Restore(Policies(policies._value.image_neutral, policies._value.dating_allowed)))
            using (TestTools.Restore(() => { before.Restore(); aya.DatingData.Partner_Status = partnerWas; }))
            {
                aya.DatingData.Partner_Status = data_girls.girls._dating_data._partner_status.taken_outside_bf;
                int queued = manager.activeEvents.Count;
                bool started = (bool)AccessTools.Method(traitsFix, "StartLeakEvent").Invoke(null, new object[] { "bf", culprit, new[] { aya } });
                ctx.Assert(started, "No leak event was queued");
                if (!started)
                    yield break;
                Event_Manager._activeEvent leak = manager.activeEvents[queued];
                using (TestTools.Restore(() => { manager.activeEvents.Remove(leak); variables.Delete(leak.data.id + "_culprit"); }))
                {
                    Image picture = popup.image.GetComponent<Image>();
                    picture.sprite = null;
                    manager.StartCoroutine((IEnumerator)AccessTools.Method(typeof(Event_Manager), "OpenPopup").Invoke(manager, new object[] { leak }));
                    float start = Time.realtimeSinceStartup;
                    while ((picture.sprite == null || picture.sprite.texture.width != 600) && Time.realtimeSinceStartup - start < 5f)
                        yield return null;
                    yield return new WaitForSecondsRealtime(0.5f);

                    Texture texture = picture.sprite?.texture;
                    ctx.Assert(texture != null && texture.width == 600 && texture.height == 860,
                        leak.data.id + " shows a " + (texture == null ? "missing" : texture.width + "x" + texture.height) + " picture, not its 600x860 one");
                    string text = popup.description.GetComponent<TextMeshProUGUI>().text;
                    ctx.Assert(text.Contains(culprit.girlName(false)), leak.data.id + " doesn't name the culprit " + culprit.girlName(false));
                    ctx.Assert(!text.Contains("_culprit]"), leak.data.id + " shows the culprit's variable unfilled");
                    yield return Screenshot(Path.Combine(shots, "1-popup.png"));

                    popup.buttons_container.GetComponentInChildren<Event_ReplyButton>().OnClick();
                    yield return new WaitForSecondsRealtime(1f);

                    GameObject results = popups.GetByType(PopupManager._type.static_event).obj;
                    ctx.Assert(!popup.gameObject.activeInHierarchy, "The leak's popup is still open after its reply");
                    ctx.Assert(results.activeInHierarchy, "No results panel is open after the leak's reply");
                    string resultsText = results.GetComponentInChildren<TextMeshProUGUI>()?.text ?? "";
                    ctx.Assert(resultsText.Contains(Language.Data["IDOL__LEAK_REACTION_LOW"]), "The open results panel isn't the leak's: " + resultsText);
                    // The clicked reply reaches the mod, and the game fills in its result's names
                    string expected = leak.SwitchVariables(Language.Data[leak.data.id + "_result_1"]);
                    ctx.Assert(resultsText.Contains(expected) && !resultsText.Contains("[actor_") && !resultsText.Contains("_culprit]"),
                        "The results don't start with the first reply's result, names filled in: " + resultsText);
                    yield return Screenshot(Path.Combine(shots, "2-results.png"));
                    yield return Game.CloseAllPopups(ctx);
                }
            }
        }

        /// <summary>
        /// The leak copies the game's dating scandal template answered with "Do nothing". Only the game can
        /// run the template (it opens its own results panel), so this answers it for every image policy,
        /// dating policy and partner gender, and checks it reaches the damage, scandal points, buzz and fan
        /// opinion changes the mod's copy computes.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator LeakOutcomeMatchesGameTemplate(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            business business = Game.Main.Data.GetComponent<business>();
            data_girls.girls girl = data_girls.GetActiveGirls(null).FirstOrDefault(g => business.GetLiability(g) == 0);
            Event_Templates._template template = Event_Templates.GetTemplate("dating_scandal");
            Event_Templates._template._reply doNothing = template?.Replies.FirstOrDefault(r => r.Actions.Any(a => a.formula == "dating_do_nothing"));
            if (girl == null || doNothing == null)
            {
                ctx.Fail(girl == null ? "The fixture save needs an active idol without business contracts" : "The game's dating scandal template has no \"Do nothing\" reply");
                yield break;
            }

            MethodInfo getOutcome = AccessTools.Method(mod.GetType("TraitFix.TraitsFix", true), "GetLeakOutcome");
            GirlState before = new GirlState(girl);
            data_girls.girls._dating_data._partner_status partnerWas = girl.DatingData.Partner_Status;
            Event_Templates._active_template activeWas = Event_Templates.Active_Template;
            int compared = 0;
            using (TestTools.Restore(Policies(policies.GetSelectedPolicyValue(policies._type.image).Value, policies.GetSelectedPolicyValue(policies._type.dating).Value)))
            using (TestTools.Restore(() => { before.Restore(); girl.DatingData.Partner_Status = partnerWas; Event_Templates.Active_Template = activeWas; }))
            {
                foreach (bool female in new[] { false, true })
                foreach (policies._value image in new[] { policies._value.image_neutral, policies._value.image_orthodox, policies._value.image_rebellious })
                foreach (policies._value dating in new[] { policies._value.dating_forbidden, policies._value.dating_ambiguous, policies._value.dating_allowed })
                {
                    Policies(image, dating);
                    girl.DatingData.Partner_Status = female
                        ? data_girls.girls._dating_data._partner_status.taken_outside_gf
                        : data_girls.girls._dating_data._partner_status.taken_outside_bf;
                    float scandalBefore = girl.getParam(data_girls._paramType.scandalPoints).val;
                    long buzzBefore = resources.Get(resources.type.buzz);

                    Event_Templates._active_template active = new Event_Templates._active_template { Template = template };
                    active.Actors.Add(new Event_Manager._activeEvent._actor { girl = girl });
                    active.Actors.Add(new Event_Manager._activeEvent._actor { name = "Test Partner", female = female });
                    active.Variables["Damage"] = 50;
                    active.Variables["Can_Deny_Identity"] = 0;
                    Event_Templates.Active_Template = active;
                    active.SetActions(doNothing.Actions);
                    yield return null;

                    object outcome = getOutcome.Invoke(null, new object[] { female, image, dating });
                    Traverse expected = Traverse.Create(outcome);
                    string what = (female ? "girlfriend" : "boyfriend") + ", " + image + ", " + dating;
                    int damage = Traverse.Create(active).Field("Damage").GetValue<int>();
                    ctx.Assert(damage == expected.Field("Damage").GetValue<int>(), what + ": the template's damage is " + damage + ", the mod's " + expected.Field("Damage").GetValue<int>());
                    float scandal = girl.getParam(data_girls._paramType.scandalPoints).val - scandalBefore;
                    ctx.Assert(Mathf.Approximately(scandal, expected.Field("ScandalPoints").GetValue<int>()), what + ": the template gave " + scandal + " scandal points, the mod " + expected.Field("ScandalPoints").GetValue<int>());
                    long buzz = resources.Get(resources.type.buzz) - buzzBefore;
                    ctx.Assert(buzz == expected.Field("Buzz").GetValue<int>(), what + ": the template added " + buzz + " buzz, the mod " + expected.Field("Buzz").GetValue<int>());

                    string results = Game.Main.Data.GetComponent<PopupManager>().GetByType(PopupManager._type.static_event).obj.GetComponentInChildren<TextMeshProUGUI>()?.text ?? "";
                    foreach (resources.fanType type in new[] { resources.fanType.hardcore, resources.fanType.casual })
                    {
                        float opinion = expected.Field(type == resources.fanType.hardcore ? "HardcoreOpinion" : "CasualOpinion").GetValue<float>();
                        string line = resources.GetFanTitle(type) + " " + Language.Data["FANS_OPINION"] + ": ";
                        bool shown = results.Contains(line);
                        ctx.Assert(shown == (opinion < 0f), what + ": the template " + (shown ? "lost" : "didn't lose") + " " + type + " fans, the mod's opinion change is " + opinion);
                        if (shown && opinion < 0f)
                            ctx.Assert(results.Contains(line + opinion), what + ": the template's " + type + " opinion change isn't the mod's " + opinion);
                    }
                    compared++;
                    yield return Game.CloseAllPopups(ctx);
                    before.Restore();
                }
            }
            ctx.Record("combinations", compared);
        }

        private static Action Policies(policies._value image, policies._value dating)
        {
            policies._value imageWas = policies.GetSelectedPolicyValue(policies._type.image).Value;
            policies._value datingWas = policies.GetSelectedPolicyValue(policies._type.dating).Value;
            Select(policies._type.image, image);
            Select(policies._type.dating, dating);
            return () => { Select(policies._type.image, imageWas); Select(policies._type.dating, datingWas); };
        }

        private static void Select(policies._type type, policies._value value)
        {
            foreach (policies.value policy in policies.Values.Where(p => p.Type == type))
                policy.Selected = policy.Value == value;
        }

        /// <summary>What a leak or the template changes on an idol, kept to undo (fan losses aren't undone).</summary>
        private sealed class GirlState
        {
            private readonly data_girls.girls girl;
            private readonly float stamina;
            private readonly float staminaLostToday;
            private readonly float scandal;
            private readonly bool known;
            private readonly data_girls.girls._dating_data._partner_status knownStatus;
            private readonly bool hadScandal;
            private readonly bool hadScandalEver;
            private readonly bool usedGoods;
            private readonly DateTime lastScandal;

            public GirlState(data_girls.girls girl)
            {
                this.girl = girl;
                stamina = girl.getParam(data_girls._paramType.mentalStamina).val;
                staminaLostToday = girl.getParam(data_girls._paramType.mentalStamina).daily_change;
                scandal = girl.getParam(data_girls._paramType.scandalPoints).val;
                known = girl.DatingData.Is_Partner_Status_Known;
                knownStatus = girl.DatingData.Partner_Status_Known_To_Player;
                hadScandal = girl.DatingData.Had_Scandal;
                hadScandalEver = girl.DatingData.Had_Scandal_Ever;
                usedGoods = girl.DatingData.Used_Goods;
                lastScandal = girl.LastDatingScandal;
            }

            public void Restore()
            {
                girl.getParam(data_girls._paramType.mentalStamina).setVal(stamina);
                girl.getParam(data_girls._paramType.mentalStamina).daily_change = staminaLostToday;
                girl.getParam(data_girls._paramType.scandalPoints).setVal(scandal);
                girl.DatingData.Is_Partner_Status_Known = known;
                girl.DatingData.Partner_Status_Known_To_Player = knownStatus;
                girl.DatingData.Had_Scandal = hadScandal;
                girl.DatingData.Had_Scandal_Ever = hadScandalEver;
                girl.DatingData.Used_Goods = usedGoods;
                girl.LastDatingScandal = lastScandal;
            }
        }

        private static string Screenshots()
        {
            string shots = Path.Combine(Path.GetTempPath(), "TraitsFixLeakScreens");
            if (Directory.Exists(shots))
                Directory.Delete(shots, true);
            Directory.CreateDirectory(shots);
            return shots;
        }

        private static IEnumerator Screenshot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            float start = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - start < 5f)
                yield return null;
        }
    }
}
