using TMPro;
using UnityEngine;

namespace ConcertHype
{
    /// <summary>
    /// The show's score under the profit on the results screen, as the game shows production cost, revenue and
    /// profit, with what it's made of on its info icon.
    /// </summary>
    internal static class ScoreLine
    {
        private const string Name = "ConcertHype Score";

        private static ReadoutWidget score;

        /// <param name="readout">Null for a show the mod didn't run.</param>
        public static void Show(Concert_Popup popup, Readout readout)
        {
            Transform final = popup.Final_Container.transform;
            bool made = score != null && score.Root != null && score.Root.transform.parent == final;
            if (!made && readout == null)
                return;
            if (!made)
            {
                GameObject title = final.Find("ProfitTitle")?.gameObject, value = final.Find("Profit")?.gameObject;
                GameObject revenueTitle = final.Find("RevenueTitle")?.gameObject;
                if (title == null || value == null)
                    return;
                Rect titleAt = Ui.Placed(title, final), valueAt = Ui.Placed(value, final);
                // One step below Profit, as Profit is below Revenue
                float step = revenueTitle != null ? titleAt.yMin - Ui.Placed(revenueTitle, final).yMin : 132f;
                Color plain = popup.NextSong_AccidentChance?.GetComponent<TextMeshProUGUI>()?.color ?? Color.white;
                score = ReadoutWidget.Make(final, final, Name, title, value, InfoIcon.From(popup), valueAt.xMin, titleAt.yMin + step, plain);
            }
            score.Set(readout ?? new Readout());
        }
    }
}
