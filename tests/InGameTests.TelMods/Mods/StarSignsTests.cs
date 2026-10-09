using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Every label on the Extras tab's first text (the game's trait, other mods' lines, the star sign)
        /// is black, so it stands out from the blue descriptions. In the base game the trait label is the
        /// same blue as its description. Screenshots per idol go to %TEMP%\StarSignsScreens.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator ProfileLabelsAreBlack(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            string shots = Path.Combine(Path.GetTempPath(), "StarSignsScreens");
            if (Directory.Exists(shots))
                Directory.Delete(shots, true);
            Directory.CreateDirectory(shots);
            ctx.Record("screenshots", shots);
            foreach (int id in ProfileIdols)
            {
                yield return Game.OpenProfile(Game.Girl(id), Profile_Popup._tabs.extras);
                yield return new WaitForSecondsRealtime(0.5f);
                string text = ExtrasText() ?? "";
                Match[] labels = Regex.Matches(text, "<color=(#?\\w+)>([^<]*)</color>").Cast<Match>().ToArray();
                ctx.Record("idol" + id, string.Join(" | ", labels.Select(m => m.Groups[2].Value.Trim() + " " + m.Groups[1].Value).ToArray()));
                ctx.Assert(labels.Length > 0, "Idol " + id + "'s Extras tab has no labels: " + text);
                foreach (Match label in labels)
                {
                    ctx.Assert(label.Groups[1].Value == mainScript.black,
                        "Idol " + id + "'s \"" + label.Groups[2].Value.Trim() + "\" label is " + label.Groups[1].Value + ", not black " + mainScript.black);
                }
                yield return Screenshot(Path.Combine(shots, "idol" + id + ".png"));
            }
            yield return Game.CloseAllPopups(ctx);
        }

        private static IEnumerator Screenshot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            float start = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - start < 5f)
                yield return null;
        }

        /// <summary>The first text of the open profile's Extras tab: the trait line that the Extras patches append to.</summary>
        private static string ExtrasText()
        {
            Transform text = Game.ProfilePopup.Extras_Container.transform.Find("Text(Clone)");
            return text == null ? null : text.GetComponent<TMPro.TextMeshProUGUI>().text;
        }
    }
}
