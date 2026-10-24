using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
    // Fixes the show and concert editors' idol pickers letting you pick an idol who is already in the cast, which put
    // her in two slots. The picker greys out cast idols when it opens, but its list is still being built for a
    // moment after the editor opens, and every idol button enables itself when it is drawn. Keep cast idols greyed
    // out whenever a picker button is drawn. It only greys buttons, so other greying (injured idols) is kept.
    [HarmonyPatch(typeof(GirlButtonSmall), nameof(GirlButtonSmall.UpdateData))]
    public class GirlButtonSmall_UpdateData
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<GirlButtonSmall_UpdateData>(ex);

        // The faded look the concert picker gives cast idols (Concert_New_Popup.UpdateGirlsList).
        private const float ConcertGreyAlpha = 0.4f;

        // The casts the editors grey out: the show's slots, and the concert setlist item being cast.
        private static readonly AccessTools.FieldRef<Show_Popup, data_girls.girls[]> ShowGirls =
            AccessTools.FieldRefAccess<Show_Popup, data_girls.girls[]>("girls");
        private static readonly AccessTools.FieldRef<Concert_New_Popup, SEvent_Concerts._concert.ISetlistItem> ConcertItem =
            AccessTools.FieldRefAccess<Concert_New_Popup, SEvent_Concerts._concert.ISetlistItem>("girl_setListItem");

        // Applies the editor's own greying rule to the button the game just drew.
        public static void Postfix(GirlButtonSmall __instance)
        {
            try
            {
                ButtonDefault button = __instance.buttonDefault;
                if (__instance.girl == null || button == null || !button.active || !__instance.gameObject.activeInHierarchy)
                    return;
                Transform grid = __instance.transform.parent;
                if (grid == null)
                    return;

                Show_Popup show = grid.GetComponentInParent<Show_Popup>();
                if (show != null && show.Girl_Select_Grid != null && grid == show.Girl_Select_Grid.transform)
                {
                    if (InCast(__instance.girl, ShowCast(show)))
                        button.Activate(val: false);
                    return;
                }

                Concert_New_Popup concert = grid.GetComponentInParent<Concert_New_Popup>();
                if (concert != null && concert.Girl_Select_Grid != null && grid == concert.Girl_Select_Grid.transform
                    && InCast(__instance.girl, ConcertCast(concert)))
                {
                    button.Activate(val: false);
                    CanvasGroup group = __instance.GetComponent<CanvasGroup>();
                    if (group != null)
                        group.alpha = ConcertGreyAlpha;
                }
            }
            catch (Exception ex)
            {
                PatchLog.WarnOncePerPatch<GirlButtonSmall_UpdateData>("failed: " + ex);
            }
        }

        public static IEnumerable<data_girls.girls> ShowCast(Show_Popup popup) => ShowGirls(popup);

        // Null until the concert picker is first opened.
        public static IEnumerable<data_girls.girls> ConcertCast(Concert_New_Popup popup) => ConcertItem(popup)?.GetGirls();

        public static bool InCast(data_girls.girls girl, IEnumerable<data_girls.girls> cast) => girl != null && cast != null && cast.Contains(girl);
    }

    // Stores the result of DateTime.AddMonths/AddDays calls whose result the game throws away (DateTime is immutable).
    // Compatible without knowing other mods: it only rewrites the discarded-call pattern, so once any mod has fixed a
    // call site there is nothing left to match and this does nothing there.
    public static class DiscardedDate
    {
        private static readonly MethodInfo AddMonths = AccessTools.Method(typeof(DateTime), nameof(DateTime.AddMonths));
        private static readonly MethodInfo AddDays = AccessTools.Method(typeof(DateTime), nameof(DateTime.AddDays));
        private static readonly MethodInfo AddMonthsToMethod = AccessTools.Method(typeof(DiscardedDate), nameof(AddMonthsTo));
        private static readonly MethodInfo AddDaysToMethod = AccessTools.Method(typeof(DiscardedDate), nameof(AddDaysTo));

        public static void AddMonthsTo(ref DateTime date, int months) => date = date.AddMonths(months);

        public static void AddDaysTo(ref DateTime date, double days) => date = date.AddDays(days);

        // Rewrites `<address>; <argument>; call AddMonths/AddDays; pop` where the address load matches isDate.
        public static IEnumerable<CodeInstruction> Store(IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> isDate, Type patchType)
        {
            List<CodeInstruction> codes = instructions.ToList();
            int stored = 0;
            for (int i = 2; i < codes.Count - 1; i++)
            {
                MethodInfo replacement = ReferenceEquals(codes[i].operand, AddMonths) ? AddMonthsToMethod
                    : ReferenceEquals(codes[i].operand, AddDays) ? AddDaysToMethod : null;
                if (replacement == null || codes[i].opcode != OpCodes.Call || codes[i + 1].opcode != OpCodes.Pop || !isDate(codes[i - 2]))
                    continue;

                codes[i].operand = replacement;
                codes[i + 1].opcode = OpCodes.Nop;
                stored++;
            }
            if (stored == 0)
                PatchLog.WarnOncePerPatch(patchType, "no discarded date change left to fix (another mod may have fixed it).");
            return codes;
        }
    }

    // Fixes the show tooltip's next episode date: GetNextEpisodeDate throws away its first AddDays(1), so on the
    // show's weekday the next episode shown is today rather than next week (also used for the cancellation date).
    // Pattern-based, so it does nothing once another mod has fixed the call.
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.GetNextEpisodeDate), new[] { typeof(DateTime) })]
    public class Shows__show_GetNextEpisodeDate
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Shows__show_GetNextEpisodeDate>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return DiscardedDate.Store(instructions, ci => ci.opcode == OpCodes.Ldarga || ci.opcode == OpCodes.Ldarga_S, typeof(Shows__show_GetNextEpisodeDate));
        }
    }

    // Fixes an idol taking two slots of the same show cast: when an idol leaves a permanent cast, the game refills
    // her slot with a random active idol who may already be in another slot, so she then counts twice everywhere the
    // show uses its cast. Swap such a duplicate for an idol who isn't in the cast, or leave the slot empty if there
    // is none. State-based: does nothing when the refill isn't a duplicate, and refills a slot another mod emptied
    // after the game's refill. When another mod skips the game's RemoveGirl, its result is kept.
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.RemoveGirl))]
    public class Shows__show_RemoveGirl
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Shows__show_RemoveGirl>(ex);

        private static readonly System.Random Rng = new System.Random();

        public static void Prefix(Shows._show __instance, data_girls.girls _Girl, out List<int> __state)
        {
            __state = new List<int>();
            if (__instance.castType != Shows._show._castType.permanentCast)
                return;
            for (int i = 0; i < __instance.girls.Length; i++)
            {
                if (__instance.girls[i] == _Girl)
                    __state.Add(i);
            }
        }

        public static void Postfix(Shows._show __instance, data_girls.girls _Girl, List<int> __state, bool __runOriginal)
        {
            if (!__runOriginal)
                return;
            bool changed = false;
            foreach (int slot in __state)
            {
                data_girls.girls refill = __instance.girls[slot];
                if (refill != null && !IsElsewhere(__instance.girls, slot, refill))
                    continue;
                List<data_girls.girls> free = data_girls.GetActiveGirls().Where(g => g != _Girl && !__instance.girls.Contains(g)).ToList();
                data_girls.girls replacement = free.Count > 0 ? free[Rng.Next(free.Count)] : null;
                changed |= replacement != refill;
                __instance.girls[slot] = replacement;
            }
            if (changed)
                __instance.Update?.Invoke();
        }

        public static bool IsElsewhere(data_girls.girls[] cast, int slot, data_girls.girls girl)
        {
            for (int i = 0; i < cast.Length; i++)
            {
                if (i != slot && cast[i] == girl)
                    return true;
            }
            return false;
        }
    }
}
