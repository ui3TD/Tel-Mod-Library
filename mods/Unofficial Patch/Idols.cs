using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
	// Fixed so that when girls dating within the group break up, their relationship status is no longer known
    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.BreakUp))]
    public class Relationships__relationship_BreakUp
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Relationships__relationship_BreakUp>(ex);

        // Indices for the two relationship participants.
        private const int FirstPartnerIndex = 0;
        private const int SecondPartnerIndex = 1;

        public static void Prefix(Relationships._relationship __instance, ref bool __state)
        {
            // Capture whether the pair was dating before BreakUp clears the flag.
            __state = __instance != null && __instance.Dating;
        }

        public static void Postfix(Relationships._relationship __instance, bool __state)
        {
            // Only clear known status when the pair was dating and has now broken up (another mod may have
            // skipped the breakup).
            if (!__state || __instance == null || __instance.Dating || __instance.Girls == null || __instance.Girls.Count < 2)
                return;

            // Hide partner status for both sides after breakup.
            if (__instance.Girls[FirstPartnerIndex]?.DatingData != null)
            {
                __instance.Girls[FirstPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
            if (__instance.Girls[SecondPartnerIndex]?.DatingData != null)
            {
                __instance.Girls[SecondPartnerIndex].DatingData.Is_Partner_Status_Known = false;
            }
        }
    }

    // Fixed a freeze when generating some platinum and gold idols (auditions, Aya, hiring a rival).
    // The stat roll reserves up to 3 stats, then spends the rest of the points on the other stats, up to 99 each.
    // When the points can't fit (about 1 in 12 platinum rolls), the spending loop never ends.
    // The roll that can't fit is thrown away and the game rolls again, so every idol is one the game could make.
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateParams))]
    public class data_girls_GenerateParams
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_GenerateParams>(ex);

        // Highest value a stat can hold.
        private const int MaxStat = 99;
        // Points the game takes off for the 8 stats starting at 1.
        private const int StatCount = 8;
        // Rerolls before giving up and dropping the points that can't fit. Only reached if another mod
        // raises the points so far that no roll can fit; the game's points fail about 1 time in 12.
        private const int MaxRerolls = 100;

        // Rerolls in a row for the idol being generated.
        private static int rerolls;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo getPoints = AccessTools.Method(typeof(Auditions), nameof(Auditions.GetPointsByType));
            ConstructorInfo newList = AccessTools.Constructor(typeof(List<int>), Type.EmptyTypes);
            MethodInfo shouldReroll = AccessTools.Method(typeof(data_girls_GenerateParams), nameof(ShouldReroll));
            MethodInfo fitBudget = AccessTools.Method(typeof(data_girls_GenerateParams), nameof(FitBudget));
            if (getPoints == null || newList == null || shouldReroll == null || fitBudget == null)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("method lookup failed.");
                return instructions;
            }

            // Read once: another mod's transpiler may hand over a sequence that rebuilds on every read.
            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, shouldReroll)))
            {
                // Already rerolls: patched twice.
                return instructions;
            }

            var matcher = new CodeMatcher(instructions, generator);

            // The points left to spend: GetPointsByType(Type) - 8.
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, getPoints)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, StatCount)),
                new CodeMatch(OpCodes.Sub),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("points local not found.");
                return instructions;
            }
            CodeInstruction storeBudget = matcher.InstructionAt(3);

            // The stat list.
            matcher.Start();
            matcher.MatchForward(false,
                new CodeMatch(ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo ctor && ctor == newList),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("stat list local not found.");
                return instructions;
            }
            CodeInstruction storeList = matcher.InstructionAt(1);

            // The end of the loop that adds a 1 for each stat that isn't reserved: i < 8 - reserved.
            // The spending loop starts right after it.
            matcher.Start();
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, StatCount)),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Sub),
                new CodeMatch(ci => ci.opcode == OpCodes.Blt || ci.opcode == OpCodes.Blt_S));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("spending loop not found.");
                return instructions;
            }
            CodeInstruction loadReserved = matcher.InstructionAt(1);
            matcher.Advance(4);
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("spending loop start not found.");
                return instructions;
            }

            // A reroll jumps back to the method's first instruction.
            Label start = generator.DefineLabel();
            matcher.InstructionAt(-matcher.Pos).labels.Add(start);

            // Before spending: if (ShouldReroll(list, reserved, points)) start again;
            // then points = FitBudget(list, reserved, points).
            var labels = matcher.Instruction.labels.ToList();
            matcher.Instruction.labels.Clear();
            var firstInserted = IlHelpers.LoadOfStore(storeList);
            firstInserted.labels.AddRange(labels);
            matcher.Insert(
                firstInserted,
                new CodeInstruction(loadReserved.opcode, loadReserved.operand),
                IlHelpers.LoadOfStore(storeBudget),
                new CodeInstruction(OpCodes.Call, shouldReroll),
                new CodeInstruction(OpCodes.Brtrue, start),
                IlHelpers.LoadOfStore(storeList),
                new CodeInstruction(loadReserved.opcode, loadReserved.operand),
                IlHelpers.LoadOfStore(storeBudget),
                new CodeInstruction(OpCodes.Call, fitBudget),
                new CodeInstruction(storeBudget.opcode, storeBudget.operand));
            return matcher.InstructionEnumeration();
        }

        // How many more points the stats that aren't reserved can hold.
        public static int Room(List<int> stats, int reserved)
        {
            int room = 0;
            for (int i = reserved; i < stats.Count; i++)
            {
                room += Math.Max(0, MaxStat - stats[i]);
            }
            return room;
        }

        // True when the points can't fit, so the game should roll this idol again.
        public static bool ShouldReroll(List<int> stats, int reserved, int budget)
        {
            if (budget <= Room(stats, reserved))
            {
                rerolls = 0;
                return false;
            }

            if (rerolls >= MaxRerolls)
            {
                // FitBudget drops the points that can't fit instead.
                rerolls = 0;
                PatchLog.WarnOncePerPatch<data_girls_GenerateParams>("no roll fit its points after " + MaxRerolls + " rerolls; dropped the extra points.");
                return false;
            }

            rerolls++;
            return true;
        }

        // The points to spend: unchanged when they fit, otherwise only what fits.
        public static int FitBudget(List<int> stats, int reserved, int budget)
        {
            return Math.Min(budget, Room(stats, reserved));
        }
    }

    // Fixes the English birthday title, which says "@th birthday" for every age ("21th"). In English, uses this mod's
    // UNOFFICIALPATCH__BD_TITLE_1/2 text instead, which needs no ordinal. Other languages, and a title another mod
    // already changed, are left alone.
    [HarmonyPatch(typeof(Birthday_Popup), nameof(Birthday_Popup.RenderTitle))]
    public class Birthday_Popup_RenderTitle
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Birthday_Popup_RenderTitle>(ex);

        public const string TextLanguage = "en";
        public const string Title1 = "UNOFFICIALPATCH__BD_TITLE_1";
        public const string Title2 = "UNOFFICIALPATCH__BD_TITLE_2";

        public static void Postfix(Birthday_Popup __instance)
        {
            if (__instance.Title == null || __instance.Girl == null)
                return;
            string title = NewTitle(staticVars.Settings.Language, ExtensionMethods.GetText(__instance.Title),
                __instance.Girl.GetName(), __instance.Girl.GetAge().ToString());
            if (title != null)
                ExtensionMethods.SetText(__instance.Title, title);
        }

        /// <summary>The title from this mod's text; null to keep the current title.</summary>
        public static string NewTitle(string language, string title, string name, string age)
        {
            if (language != TextLanguage || !Language.Data.ContainsKey(Title1) || !Language.Data.ContainsKey(Title2))
                return null;
            if (title != Language.Insert("BD__TITLE_1", name) + " " + Language.Insert("BD__TITLE_2", age))
                return null;
            return Language.Insert(Title1, name) + " " + Language.Insert(Title2, age);
        }
    }

    // Dating status is visible for underage members. The game returns "" unless the idol is an adult
    // (Is_AOC); treat every idol as one, so the rest of the game's own method builds the text.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.GetPartnerString))]
    public class data_girls_girls_GetPartnerString
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_girls_GetPartnerString>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo isAoc = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.Is_AOC));
            var matcher = new CodeMatcher(instructions).MatchStartForward(new CodeMatch(ci => IlHelpers.IsCallTo(ci, isAoc)));
            if (matcher.IsInvalid)
            {
                // Already gone: patched twice, or the game fixed it.
                return instructions;
            }

            // this.Is_AOC() becomes true: drop "this", push 1.
            matcher.Instruction.opcode = OpCodes.Pop;
            matcher.Instruction.operand = null;
            matcher.Advance(1).Insert(new CodeInstruction(OpCodes.Ldc_I4_1));
            return matcher.InstructionEnumeration();
        }
    }

    // Fixed gossip so girl doesn't gossip about herself
    [HarmonyPatch(typeof(Date_Gossip), nameof(Date_Gossip.GetAvailableGossips))]
    public class Date_Gossip_GetAvailableGossips
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Date_Gossip_GetAvailableGossips>(ex);

        public static void Postfix(ref List<Date_Gossip._gossip> __result, data_girls.girls Snitch)
        {
            // Nothing to filter if the list is empty.
            if (__result == null || __result.Count == 0)
            {
                return;
            }
            // Walk backwards so removals do not affect remaining indices.
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                // Remove any gossip targeting the snitch herself.
                if (__result[i].BullyingTarget == Snitch)
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    // Fix stamina cost of performance thumbnail if Energetic policy
    [HarmonyPatch(typeof(Activities._activity), nameof(Activities._activity.GetDescription))]
    public class Activities__activity_GetDescription
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Activities__activity_GetDescription>(ex);

        // The Energetic policy's stamina cost, as the game's Activities.GetStaminaCost charges it (-4f). Used when
        // the scene's Activities component can't be reached; the unit tests check the two match.
        private const int EnergeticStaminaCost = 4;
        private const string PointsKey = "PT";
        private const string StaminaKey = "STAMINA";
        private const string CostPrefix = "-";
        private const string CostSeparator = " ";

        public static void Postfix(Activities._activity __instance, ref string __result)
        {
            // Only adjust performance activities when Energetic policy is active.
            if (__instance.type == Activity._type.performance && policies.GetSelectedPolicyValue(policies._type.performances)?.Value == policies._value.performances_energy)
            {
                // Build a localized "-4 PT stamina" string for the thumbnail.
                __result = string.Concat(new object[]
                {
                    CostPrefix,
                    StaminaCost(),
                    Language.Data[PointsKey],
                    CostSeparator,
                    Language.Data[StaminaKey].ToLower()
                });
            }
        }

        // The cost the game charges, including any other mod's change to it.
        private static int StaminaCost()
        {
            try
            {
                return ChargedStaminaCost();
            }
            catch (Exception)
            {
                // No scene (main menu, unit tests): the game's own cost.
                return EnergeticStaminaCost;
            }
        }

        // Separate, so a failure to reach the scene is caught by the caller.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int ChargedStaminaCost()
        {
            return Mathf.RoundToInt(-Camera.main.GetComponent<mainScript>().Data.GetComponent<Activities>().GetStaminaCost());
        }
    }
}
