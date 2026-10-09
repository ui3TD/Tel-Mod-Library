using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Targeted Auditions on a real 16-candidate audition: the audition popup with the mod's scroll
    /// area, the game's body picker over every installed idol pack, and the mod's skill priorities
    /// running in the same method as Unofficial Patch's stat reroll. The unit tests cover the
    /// settings, the body pool rules and the priority maths with the mod alone.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class TargetedAuditionsTests
    {
        private const string HarmonyId = "com.tel.customauditions";
        private const int Candidates = 16;
        private const data_girls._paramType Prioritised = data_girls._paramType.vocal;
        private static readonly data_girls._paramType[] Skills =
        {
            data_girls._paramType.cute, data_girls._paramType.cool, data_girls._paramType.sexy, data_girls._paramType.pretty,
            data_girls._paramType.vocal, data_girls._paramType.dance, data_girls._paramType.funny, data_girls._paramType.smart,
        };

        // With vocal at 100 and the other skills at 1, a candidate's best rolled value goes to vocal
        // 100 times in 107; by chance alone, about 1 time in 8. Out of 16, fewer than 10 with the mod
        // working, or 10 or more by chance, are each under 1 in 3000. Other mods change skills as
        // they're read, so the stored values are read as GenerateParams leaves them.
        private const int AtLeastVocalBest = 10;
        private static readonly List<bool> vocalBestAtGeneration = new List<bool>();

        /// <summary>
        /// A nationwide audition of 16 candidates, vocal prioritised. Nationwide guarantees a gold
        /// candidate, the case Unofficial Patch's stat reroll exists for. Every card is shown, in one
        /// scroll area; no body repeats before the eligible pool runs out; vocal is mostly the best skill.
        /// </summary>
        [InGameTest(Suite = ModTest.AuditionsSuite, Order = 10)]
        private static IEnumerator SixteenCandidateAuditionWorks(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            var settings = new List<IDisposable> { Game.Variable("CustomAudition_Count", Candidates.ToString()) };
            foreach (data_girls._paramType skill in Skills)
                settings.Add(Game.Variable("CustomAudition_Prio_" + skill, skill == Prioritised ? "100" : "1"));
            DateTime nationwideDate = Auditions.Nationwide_Date;
            try
            {
                vocalBestAtGeneration.Clear();
                using (TestTools.Spy(AccessTools.Method(typeof(data_girls), "GenerateParams"),
                           postfix: AccessTools.Method(typeof(TargetedAuditionsTests), nameof(RecordSkills))))
                {
                    yield return Game.OpenAudition(ctx, Auditions.type.nationwide);
                }
                Popup_Audition popup = Game.AuditionPopup;
                List<Auditions.data._girl> girls = Game.Main.Data.GetComponent<Auditions>().Get(Auditions.type.nationwide).Girls;

                // The game reveals the cards one by one once their portraits are in, then shows Finish.
                yield return TestTools.WaitFor(ctx, () => popup.Button_Finish.activeSelf, 120f, "the audition to reveal its cards");
                yield return new WaitForSecondsRealtime(1f);

                ctx.Record("candidates", girls.Count);
                ctx.Assert(girls.Count == Candidates, $"The audition has {girls.Count} candidates; Targeted Auditions was set to {Candidates}");
                CheckCards(ctx, popup, girls);
                CheckScrollArea(ctx, popup);
                CheckBodies(ctx, girls);
                CheckPriority(ctx);

                popup.Close();
                yield return Game.CloseAllPopups(ctx);
            }
            finally
            {
                Auditions.Nationwide_Date = nationwideDate;
                foreach (IDisposable setting in settings)
                    setting.Dispose();
            }
        }

        /// <summary>One closed card per candidate, each revealed and clickable. Records each card for diagnosis.</summary>
        private static void CheckCards(TestContext ctx, Popup_Audition popup, List<Auditions.data._girl> girls)
        {
            List<Audition_Closed_Card> cards = popup.Cards_Container.GetComponentsInChildren<Audition_Closed_Card>(true).ToList();
            ctx.Assert(cards.Count == girls.Count, $"The popup has {cards.Count} closed cards for {girls.Count} candidates");
            ctx.Assert(girls.Select(g => g.CardObject).Distinct().Count() == girls.Count
                       && girls.All(g => g.CardObject != null && g.CardObject.transform.parent == popup.Cards_Container.transform),
                "Some candidates don't have a card of their own in the popup");

            for (int i = 0; i < cards.Count; i++)
            {
                Audition_Closed_Card card = cards[i];
                float alpha = card.Container.GetComponent<CanvasGroup>().alpha;
                bool clickable = card.Container.GetComponent<Button>().interactable;
                Sprite sprite = card.Portrait.GetComponent<Image>().sprite;
                ctx.Record($"card{i + 1:00}", $"alpha {alpha:0.00}, clickable {clickable}, portrait {(sprite == null ? "none" : "set")}, x {card.transform.localPosition.x:0}");
                ctx.Assert(alpha > 0.99f && clickable, $"Card {i + 1} of {cards.Count} wasn't revealed: alpha {alpha:0.00}, clickable {clickable}");
            }
        }

        /// <summary>The mod's scroll area: the cards' parent, the only scroll area above them, scrolling the cards.</summary>
        private static void CheckScrollArea(TestContext ctx, Popup_Audition popup)
        {
            Transform container = popup.Cards_Container.transform;
            ScrollRect[] scrolls = container.GetComponentsInParent<ScrollRect>(true);
            ctx.Record("scrollAreas", scrolls.Length);
            ctx.Assert(scrolls.Length == 1, $"The cards are inside {scrolls.Length} scroll areas; expected 1");
            ctx.Assert(container.parent != null && container.parent.GetComponent<ScrollRect>() != null,
                "The cards' parent isn't a scroll area");
            ctx.Assert(scrolls.Length == 0 || ReferenceEquals(scrolls[0].content, container.GetComponent<RectTransform>()),
                "The scroll area doesn't scroll the cards");
        }

        /// <summary>
        /// The first min(16, pool) candidates have different bodies, and no unique idol's body appears
        /// twice. The pool is what the game's body picker (data_girls_textures.getRandomBodyAsset)
        /// can choose from, worked out here with the same filter.
        /// </summary>
        private static void CheckBodies(TestContext ctx, List<Auditions.data._girl> girls)
        {
            var assets = (List<data_girls_textures._textureAsset>)AccessTools.Field(typeof(data_girls_textures), "textureAssets").GetValue(null);
            List<data_girls_textures._textureAsset> eligible = assets
                .Where(a => a != null && !a.Add_To_Default && a.type == data_girls_textures._spriteType.body && a.CanBeHired())
                .ToList();
            int pool = eligible.Select(a => a.body_id).Distinct().Count();
            HashSet<int> unique = new HashSet<int>(eligible.Where(a => a.Unique).Select(a => a.body_id));

            List<int> bodies = girls.Select(g => g.girl.GetTextureAsset(data_girls_textures._spriteType.body).asset.body_id).ToList();
            int firstCycle = Math.Min(bodies.Count, pool);
            ctx.Record("bodyPool", pool);
            ctx.Record("bodies", string.Join(" ", bodies.Select(b => b.ToString()).ToArray()));
            ctx.Assert(bodies.Take(firstCycle).Distinct().Count() == firstCycle,
                $"A body repeats among the first {firstCycle} candidates, before the pool of {pool} bodies ran out");
            List<int> repeatedUnique = bodies.Where(unique.Contains).GroupBy(b => b).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            ctx.Assert(repeatedUnique.Count == 0, "A unique idol's body appears twice: " + string.Join(", ", repeatedUnique.Select(b => b.ToString()).ToArray()));
        }

        /// <summary>Vocal, prioritised far above the rest, is at least tied for the best rolled skill of most candidates.</summary>
        private static void CheckPriority(TestContext ctx)
        {
            int vocalBest = vocalBestAtGeneration.Count(best => best);
            ctx.Record("vocalBest", vocalBest + " of " + vocalBestAtGeneration.Count);
            ctx.Assert(vocalBestAtGeneration.Count >= Candidates,
                $"Only {vocalBestAtGeneration.Count} stat rolls were seen for {Candidates} candidates");
            ctx.Assert(vocalBest >= AtLeastVocalBest,
                $"Vocal is the best rolled skill of only {vocalBest} of {vocalBestAtGeneration.Count} candidates; with it prioritised, expected at least {AtLeastVocalBest}");
        }

        /// <summary>
        /// Records whether vocal is at least tied for the best skill, as the game's stat roll leaves it.
        /// Reads the stored values: other mods change skills as they're read (param.val).
        /// </summary>
        private static void RecordSkills(data_girls.girls Girl)
        {
            float vocal = Girl.getParam(Prioritised)._val;
            vocalBestAtGeneration.Add(Skills.All(s => Girl.getParam(s)._val <= vocal));
        }
    }
}
