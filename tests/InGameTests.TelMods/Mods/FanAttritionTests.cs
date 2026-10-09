using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Fan Attrition replaces the fan tooltip's Render and fills its lines by position. The unit
    /// tests use a fake tooltip with 15 lines; here it's the game's own tooltip object.
    /// </summary>
    internal static class FanAttritionTests
    {
        private const string HarmonyId = "com.tel.fanattrition";
        private const int Lines = 15;

        /// <summary>
        /// The game's fan tooltip renders exactly the 15 lines the mod fills, ending with the
        /// weekly churn, and its total line is the mod's.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator FanTooltipShowsChurn(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out _))
                yield break;

            tooltip_fans tooltip = Resources.FindObjectsOfTypeAll<tooltip_fans>().FirstOrDefault(t => t.gameObject.scene.IsValid());
            if (tooltip == null)
            {
                ctx.Fail("No fan tooltip (tooltip_fans) in the scene");
                yield break;
            }

            // The tooltip is built the first time it's shown (Start); show it if it hasn't been.
            var shown = new List<GameObject>();
            if (!tooltip.gameObject.activeInHierarchy)
            {
                ctx.Note("The fan tooltip wasn't active; activating it for the check");
                for (Transform t = tooltip.transform; t != null; t = t.parent)
                {
                    if (!t.gameObject.activeSelf)
                    {
                        shown.Add(t.gameObject);
                        t.gameObject.SetActive(true);
                    }
                }
                yield return null;
            }

            try
            {
                AccessTools.Method(typeof(tooltip_fans), "Render").Invoke(tooltip, null);
                Text[] texts = tooltip.GetComponentsInChildren<Text>();
                ctx.Record("lines", texts.Length);
                ctx.Assert(texts.Length == Lines, "The fan tooltip has " + texts.Length + " text lines; Fan Attrition fills " + Lines + " by position");
                if (texts.Length >= Lines)
                {
                    string churn = texts[Lines - 1].text;
                    ctx.Record("churnLine", churn);
                    ctx.Assert(churn.StartsWith(Language.Data["CHURN"] + ": "), "Line " + Lines + " isn't the churn line: " + churn);
                }
                string total = tooltip.fan_change.GetComponentInChildren<TMPro.TMP_Text>()?.text
                               ?? tooltip.fan_change.GetComponentInChildren<Text>()?.text;
                ctx.Record("totalLine", total);
                ctx.Assert(total != null && total.StartsWith(Language.Data["TOTAL"] + ": ") && total.Contains(Language.Data["PER_WEEK"]),
                    "The total line isn't Fan Attrition's weekly total: " + total);
            }
            finally
            {
                foreach (GameObject obj in shown)
                    obj.SetActive(false);
            }
        }
    }
}
