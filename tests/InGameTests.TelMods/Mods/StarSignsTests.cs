using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Star Signs on the real profile popup, where other mods (MBTI Personalities) append to the
    /// same text. The unit tests use a fake popup with Star Signs alone.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class StarSignsTests
    {
        private const string HarmonyId = "com.tel.starsigns";
        private static readonly int[] ProfileIdols = { 1, 167, 222 };

        /// <summary>
        /// The Extras tab of an idol's profile ends with her star sign. Star Signs runs after the
        /// other Extras patches, so its line comes last.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator ProfileExtrasEndWithSign(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;

            Type utility = mod.GetType("StarSigns.StarSigns", true);
            foreach (int id in ProfileIdols)
            {
                data_girls.girls girl = Game.Girl(id);
                string sign = AccessTools.Method(utility, "GetGirlZodiac").Invoke(null, new object[] { girl }).ToString();
                ctx.Record("idol" + id, sign);
                if (sign == "None")
                {
                    ctx.Fail("Idol " + id + " has no star sign");
                    continue;
                }
                yield return Game.OpenProfile(girl, Profile_Popup._tabs.extras);
                string text = ExtrasText();
                string title = Language.Data["STARSIGN__TITLE_" + sign.ToUpper()] + ": ";
                string lastLine = text?.Split('\n').Last();
                ctx.Assert(lastLine != null && lastLine.Contains(title),
                    "Idol " + id + "'s Extras tab doesn't end with \"" + title + "\": " + text);
            }
            yield return Game.CloseAllPopups(ctx);
        }

        /// <summary>The first text of the open profile's Extras tab: the trait line that the Extras patches append to.</summary>
        private static string ExtrasText()
        {
            Transform text = Game.ProfilePopup.Extras_Container.transform.Find("Text(Clone)");
            return text == null ? null : text.GetComponent<TMPro.TextMeshProUGUI>().text;
        }
    }
}
