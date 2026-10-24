using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnofficialPatch
{
    // Fixes fan pie rendering so the adult slice accounts for YA/Teen stacking.
    [HarmonyPatch(typeof(Profile_Fans_Pies), nameof(Profile_Fans_Pies.Render_Pies))]
    public class Profile_Fans_Pies_Render_Pies
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Profile_Fans_Pies_Render_Pies>(ex);

        // No-fan sentinel for early exit.
        private const long NoFans = 0L;
        // Cap for cumulative pie fills.
        private const float MaxPieFill = 1f;

        // Difference below which a fill counts as the game's.
        private const float FillTolerance = 0.0001f;

        // Fixes age-pie rendering so the stacked slices match the teen/YA/adult ratios.
        // Last, so it sees what other mods' patches drew.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Profile_Fans_Pies __instance, data_girls.girls ___Girl, bool __runOriginal)
        {
            // Skip when another mod drew the pies instead, or there are no fans to render (avoiding
            // divide-by-zero ratios in vanilla code).
            if (!__runOriginal || ___Girl.GetFans_Total() == NoFans)
                return;

            float teen = ___Girl.GetFan_Ratio(resources.fanType.teen);
            float youngAdult = ___Girl.GetFan_Ratio(resources.fanType.youngAdult);
            float adult = ___Girl.GetFan_Ratio(resources.fanType.adult);

            // Pies that aren't the game's were already drawn by another mod.
            if (!DrawnByGame(__instance, teen, youngAdult, adult))
            {
                PatchLog.WarnResultChanged<Profile_Fans_Pies_Render_Pies>();
                return;
            }
            SetFills(__instance, Math.Max(0f, teen), Math.Max(0f, youngAdult), Math.Max(0f, adult));
        }

        // Whether the fills are the ones the game's Render_Pies draws: Teen and Young Adult at their ratios,
        // Adult at adult + teen (an Image clamps its fill to 0..1).
        public static bool DrawnByGame(Profile_Fans_Pies pies, float teenRatio, float yaRatio, float adultRatio)
        {
            return IsFill(pies.Fans_Pie_Teen, teenRatio)
                && IsFill(pies.Fans_Pie_YA, yaRatio)
                && IsFill(pies.Fans_Pie_Adult, adultRatio + teenRatio);
        }

        private static bool IsFill(GameObject pie, float value)
        {
            return Math.Abs(pie.GetComponent<Image>().fillAmount - Mathf.Clamp01(value)) < FillTolerance;
        }

        // Sets the cumulative fills for these slice ratios.
        public static void SetFills(Profile_Fans_Pies pies, float teenRatio, float yaRatio, float adultRatio)
        {
            Image teenImage = pies.Fans_Pie_Teen.GetComponent<Image>();
            Image yaImage = pies.Fans_Pie_YA.GetComponent<Image>();
            Image adultImage = pies.Fans_Pie_Adult.GetComponent<Image>();

            float totalRatio = teenRatio + yaRatio + adultRatio;
            // Guard against rounding overshoot by scaling to the expected 0..1 range.
            if (totalRatio > MaxPieFill && totalRatio > 0f)
            {
                float scale = MaxPieFill / totalRatio;
                teenRatio *= scale;
                yaRatio *= scale;
                adultRatio *= scale;
            }

            // The prefab renders Teen as the base image with Adult and YA as child images on top.
            // Use cumulative fills so the visible slices match the ratios (YA, then Adult, then Teen).
            yaImage.fillAmount = yaRatio;
            adultImage.fillAmount = adultRatio + yaRatio;
            teenImage.fillAmount = adultRatio + yaRatio + teenRatio;
        }
    }

    // Fixed fan opinion to be impacted by concerts, SSK/show cancellation and random events
    [HarmonyPatch(typeof(resources._fanOpinion), nameof(resources._fanOpinion.Add))]
    public class resources__fanOpinion_Add
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<resources__fanOpinion_Add>(ex);

        // The change, and the opinions of the idols' fans it applies to, before anything else ran.
        public class Before
        {
            public float Val;
            public List<KeyValuePair<resources._fan, Opinion>> Opinions = new List<KeyValuePair<resources._fan, Opinion>>();
        }

        // A fan's opinion: what AddOpinion changes.
        public struct Opinion
        {
            public int[] Vals;
            public float Temp;

            public static Opinion Of(resources._fan fan)
            {
                return new Opinion { Vals = fan.Vals != null ? fan.Vals.ToArray() : null, Temp = fan.Temp };
            }

            public bool SameAs(resources._fan fan)
            {
                if (Temp != fan.Temp)
                    return false;
                if (Vals == null || fan.Vals == null)
                    return Vals == null && fan.Vals == null;
                return Vals.SequenceEqual(fan.Vals);
            }
        }

        // First, so the opinions are read before any other mod's patch changes them.
        // The game counts val down to its fraction as it records whole points, so keep the full change.
        [HarmonyPriority(Priority.First)]
        public static void Prefix(resources._fanOpinion __instance, float val, out Before __state)
        {
            __state = new Before { Val = val };
            foreach (data_girls.girls girl in Eligible())
            {
                foreach (resources._fan fan in girl.Fans)
                {
                    if (fan != null && fan.IsType(__instance.type))
                        __state.Opinions.Add(new KeyValuePair<resources._fan, Opinion>(fan, Opinion.Of(fan)));
                }
            }
        }

        // Last, so it sees what other mods' patches did.
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(resources._fanOpinion __instance, Before __state, bool __runOriginal)
        {
            // Another mod skipped the change.
            if (!__runOriginal || __state == null)
                return;
            // The game doesn't touch idols' fans here, so a changed opinion means the change already reached them.
            if (__state.Opinions.Any(entry => !entry.Value.SameAs(entry.Key)))
                return;

            // Propagate global fan opinion changes to each active girl.
            foreach (data_girls.girls girl in Eligible())
            {
                // Apply the appeal delta for the matching fan type.
                girl.AddAppeal(__instance.type, __state.Val);
            }
        }

        // Idols who gain appeal: not null, sick or graduated.
        private static IEnumerable<data_girls.girls> Eligible()
        {
            if (data_girls.girl == null)
                yield break;
            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl != null && girl.Fans != null && !girl.IsSick() && girl.status != data_girls._status.graduated)
                    yield return girl;
            }
        }
    }

    // Fixes fans of one type (business deals, cafes, fans switching idols) landing outside that type and getting lost.
    // The game splits them by each matching segment's share of all 12 segments' weight, so only about half lands by
    // weight; the rest goes out one fan at a time to every segment, for at most 12,000 fans. Split them by the
    // matching segments' weight instead. Idempotent: the helper returns the same total whatever it is given.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(long), typeof(resources.fanType?) })]
    public class data_girls_girls_AddFans_Typed
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_girls_AddFans_Typed>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo appealTotal = AccessTools.Method(typeof(data_girls.girls), nameof(data_girls.girls.GetAppeal_Total));
            MethodInfo matching = AccessTools.Method(typeof(data_girls_girls_AddFans_Typed), nameof(MatchingTotal));

            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, matching)))
            {
                // Already calls the helper: patched twice.
                return instructions;
            }

            var matcher = new CodeMatcher(instructions).MatchEndForward(new CodeMatch(ci => IlHelpers.IsCallTo(ci, appealTotal)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<data_girls_girls_AddFans_Typed>("GetAppeal_Total call not found.");
                return instructions;
            }

            // GetAppeal_Total() becomes MatchingTotal(GetAppeal_Total(), this, fanType).
            matcher.Advance(1).Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, matching));
            return matcher.InstructionEnumeration();
        }

        // The weight the idol's segments of this type share. All of them for untyped fans, and the game's total
        // when the matching segments weigh nothing, so those cases stay as in the game.
        public static float MatchingTotal(float total, data_girls.girls girl, resources.fanType? fanType)
        {
            if (!fanType.HasValue || girl?.Fans == null)
                return total;

            float sum = 0f;
            foreach (resources._fan fan in girl.Fans)
            {
                if (fan != null && fan.IsType(fanType))
                    sum += fan.GetTotalAppeal();
            }
            return sum > 0f ? sum : total;
        }
    }

    // Fixes fans who switch idols after a scandal or event being moved twice. The game resets its switch counters
    // only for the fan type the call handles, but always moves both counters' fans to other idols, so a dating
    // scandal's hardcore switchers are moved again by its casual call (and by any later call that day). Move only
    // what this call added. Pattern-based: no change once another mod has fixed the moves.
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddFans), new[] { typeof(resources.fanType?), typeof(float), typeof(bool) })]
    public class data_girls_girls_AddFans_Oshihen
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<data_girls_girls_AddFans_Oshihen>(ex);

        // Arguments pushed between a counter and the static AddFans call that moves its fans: type, new, null, this.
        private const int ArgsAfterCounter = 4;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            FieldInfo casual = AccessTools.Field(typeof(data_girls.girls), nameof(data_girls.girls.Fans_Oshihened_Casual));
            FieldInfo hardcore = AccessTools.Field(typeof(data_girls.girls), nameof(data_girls.girls.Fans_Oshihened_Hardcore));
            FieldInfo fans = AccessTools.Field(typeof(data_girls.girls), nameof(data_girls.girls.Fans));
            MethodInfo getEnumerator = AccessTools.Method(typeof(List<resources._fan>), nameof(List<resources._fan>.GetEnumerator));
            MethodInfo move = AccessTools.Method(typeof(data_girls), nameof(data_girls.AddFans),
                new[] { typeof(long), typeof(resources.fanType?), typeof(List<data_girls.girls>), typeof(data_girls.girls) });

            List<CodeInstruction> codes = instructions.ToList();

            // The counters loaded for the moves: each followed, after its other arguments, by the static AddFans.
            List<int> moves = new List<int>();
            for (int i = 0; i + ArgsAfterCounter + 1 < codes.Count; i++)
            {
                if ((codes[i].LoadsField(casual) || codes[i].LoadsField(hardcore)) && IlHelpers.IsCallTo(codes[i + ArgsAfterCounter + 1], move))
                    moves.Add(i);
            }
            if (moves.Count == 0)
            {
                // Already gone: patched twice, or the game fixed it.
                return codes;
            }

            // The segment loop: this.Fans.GetEnumerator(), after the game resets the counters.
            int loop = codes.FindIndex(ci => IlHelpers.IsCallTo(ci, getEnumerator));
            if (loop < 2 || !codes[loop - 1].LoadsField(fans) || !IlHelpers.IsLdarg(codes[loop - 2], 0) || loop > moves[0])
            {
                PatchLog.WarnOncePerPatch<data_girls_girls_AddFans_Oshihen>("segment loop not found.");
                return codes;
            }

            // Before the loop, keep each counter as it stands; each move then passes counter - kept.
            LocalBuilder keptCasual = generator.DeclareLocal(typeof(long));
            LocalBuilder keptHardcore = generator.DeclareLocal(typeof(long));
            for (int m = moves.Count - 1; m >= 0; m--)
            {
                int at = moves[m];
                LocalBuilder kept = codes[at].LoadsField(casual) ? keptCasual : keptHardcore;
                codes.InsertRange(at + 1, new[] { new CodeInstruction(OpCodes.Ldloc, kept), new CodeInstruction(OpCodes.Sub) });
            }

            CodeInstruction start = codes[loop - 2];
            CodeInstruction first = new CodeInstruction(OpCodes.Ldarg_0);
            first.MoveLabelsFrom(start);
            codes.InsertRange(loop - 2, new[]
            {
                first,
                new CodeInstruction(OpCodes.Ldfld, casual),
                new CodeInstruction(OpCodes.Stloc, keptCasual),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, hardcore),
                new CodeInstruction(OpCodes.Stloc, keptHardcore),
            });
            return codes;
        }
    }

    // Fixes shows cast with the entire group giving their opinion votes to one idol. The game divides the vote by 4
    // inside the cast loop, so the first idol in roster order gets 1/4, the second 1/16, the third 1/64 and so on.
    // Divide the episode's vote once, so every idol gets 1/4. Pattern-based: no change once the division is gone.
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SetOpinion))]
    public class Shows__show_SetOpinion
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Shows__show_SetOpinion>(ex);

        private const float EntireGroupDivisor = 4f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo getCast = AccessTools.Method(typeof(Shows._show), nameof(Shows._show.GetCast));

            // num /= 4f: ldloc num, ldc.r4 4, div, stloc num
            var matcher = new CodeMatcher(instructions, generator).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == EntireGroupDivisor),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                // Already gone: patched twice, or the game fixed it.
                return instructions;
            }
            if (!SameLocal(matcher.Instruction, matcher.InstructionAt(3)))
            {
                // Already divides a kept vote: patched twice.
                return instructions;
            }
            int division = matcher.Pos;
            CodeInstruction loadVote = matcher.Instruction.Clone();

            matcher.Start().MatchEndForward(new CodeMatch(ci => IlHelpers.IsCallTo(ci, getCast)));
            if (matcher.IsInvalid || matcher.Pos > division)
            {
                PatchLog.WarnOncePerPatch<Shows__show_SetOpinion>("GetCast call not found before the vote division.");
                return instructions;
            }

            // Keep the episode's vote right after GetCast() (stack-neutral), and divide that each time.
            LocalBuilder vote = generator.DeclareLocal(typeof(float));
            matcher.Advance(1).Insert(loadVote, new CodeInstruction(OpCodes.Stloc, vote));
            // The division moved down by the 2 instructions inserted above it
            matcher.Start().Advance(division + 2);
            matcher.Instruction.opcode = OpCodes.Ldloc;
            matcher.Instruction.operand = vote;
            return matcher.InstructionEnumeration();
        }

        private static bool SameLocal(CodeInstruction load, CodeInstruction store)
        {
            return IlHelpers.LoadOfStore(store).opcode == load.opcode && Equals(IlHelpers.LoadOfStore(store).operand, load.operand);
        }
    }

    // Fixes single opinion votes ignoring song quality for the genre. GetAppealForOpinion reads the song's quality
    // modifier and throws it away, while sales apply it to the genre. Apply it to the genre here too.
    // Pattern-based: no change once the modifier is used.
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.GetAppealForOpinion))]
    public class singles__single_GetAppealForOpinion
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles__single_GetAppealForOpinion>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            MethodInfo appealMod = AccessTools.Method(typeof(singles._single._param), nameof(singles._single._param.GetAppealMod));
            MethodInfo appealVal = AccessTools.Method(typeof(singles._param), nameof(singles._param.GetAppealVal));

            // The song's GetAppealMod() result, popped.
            var matcher = new CodeMatcher(instructions, generator).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, appealMod)),
                new CodeMatch(OpCodes.Pop));
            if (matcher.IsInvalid)
            {
                // Already used: patched twice, or the game fixed it.
                return instructions;
            }
            int pop = matcher.Pos + 1;

            // The genre's GetAppealVal(...), the first after it.
            matcher.Advance(2).MatchEndForward(new CodeMatch(ci => IlHelpers.IsCallTo(ci, appealVal)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<singles__single_GetAppealForOpinion>("genre appeal not found after the song quality.");
                return instructions;
            }

            LocalBuilder songMod = generator.DeclareLocal(typeof(float));
            matcher.Advance(1).Insert(new CodeInstruction(OpCodes.Ldloc, songMod), new CodeInstruction(OpCodes.Mul));
            matcher.Start().Advance(pop);
            matcher.Instruction.opcode = OpCodes.Stloc;
            matcher.Instruction.operand = songMod;
            return matcher.InstructionEnumeration();
        }
    }

    // Fixes the first single always voting +male +female -young adult -adult. The game sorts the single's opinion
    // appeal, then reads the strongest and weakest types from the unsorted list. Read the sorted one.
    // Pattern-based: no change once the unsorted list isn't read.
    [HarmonyPatch(typeof(singles), nameof(singles.AddOpinion))]
    public class singles_AddOpinion
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles_AddOpinion>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo unsorted = AccessTools.Field(typeof(singles._single), nameof(singles._single.FanAppeal));
            MethodInfo sorted = AccessTools.Method(typeof(singles_AddOpinion), nameof(Sorted));

            List<CodeInstruction> codes = instructions.ToList();
            foreach (CodeInstruction code in codes)
            {
                if (code.LoadsField(unsorted))
                {
                    // single.FanAppeal becomes Sorted(single)
                    code.opcode = OpCodes.Call;
                    code.operand = sorted;
                }
            }
            return codes;
        }

        // The single's opinion appeal, which the game has just sorted from strongest to weakest.
        public static List<singles._fanAppeal> Sorted(singles._single single)
        {
            List<singles._fanAppeal> appeal = single.ReleaseData?.FanAppeal;
            return appeal != null && appeal.Count > 0 ? appeal : single.FanAppeal;
        }
    }

    // Fixes the bottom bar's fans tooltip without Fan Attrition: a segment whose opinion votes, summed over the idols,
    // are all positive (or all negative) shows 50%; and opinion shows from 0 to 100% while the idol profile shows
    // -100 to +100%. Show the real share, on the profile's scale. Idempotent.
    [HarmonyPatch(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Set), new[] { typeof(resources._fan) })]
    public class tooltip_fans_line_Set
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<tooltip_fans_line_Set>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo negative = AccessTools.Field(typeof(tooltip_fans_line), nameof(tooltip_fans_line.Negative));
            MethodInfo toPercent = AccessTools.Method(typeof(ExtensionMethods), nameof(ExtensionMethods.toPercent));
            MethodInfo ratio = AccessTools.Method(typeof(tooltip_fans_line_Set), nameof(Ratio));
            MethodInfo shown = AccessTools.Method(typeof(tooltip_fans_line_Set), nameof(Shown));

            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, ratio) || IlHelpers.IsCallTo(ci, shown)))
            {
                // Already calls the helpers: patched twice.
                return instructions;
            }

            // num = Positive / (Positive + Negative), stored in the opinion local
            var matcher = new CodeMatcher(instructions).MatchEndForward(
                new CodeMatch(ci => ci.LoadsField(negative)),
                new CodeMatch(OpCodes.Add),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<tooltip_fans_line_Set>("opinion share not found.");
                return instructions;
            }
            CodeInstruction store = matcher.Instruction.Clone();
            CodeInstruction load = IlHelpers.LoadOfStore(store);

            // The opinion's toPercent(num): the colour checks between keep reading the share
            matcher.MatchStartForward(
                new CodeMatch(ci => ci.opcode == load.opcode && Equals(ci.operand, load.operand)),
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, toPercent)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<tooltip_fans_line_Set>("opinion percent not found.");
                return instructions;
            }
            matcher.Advance(1).Instruction.operand = shown;

            // After the game's if (Positive != 0 && Negative != 0), at the first instruction both paths reach:
            // num = Ratio(num, this)
            matcher.Start().MatchEndForward(
                new CodeMatch(ci => ci.LoadsField(negative)),
                new CodeMatch(OpCodes.Add),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(ci => IlHelpers.IsStloc(ci))).Advance(1);
            CodeInstruction first = load.Clone();
            first.MoveLabelsFrom(matcher.Instruction);
            matcher.Insert(first, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, ratio), store);
            return matcher.InstructionEnumeration();
        }

        // The line's vote counts, which are private.
        private static readonly AccessTools.FieldRef<tooltip_fans_line, float> Positive =
            AccessTools.FieldRefAccess<tooltip_fans_line, float>(nameof(tooltip_fans_line.Positive));
        private static readonly AccessTools.FieldRef<tooltip_fans_line, float> Negative =
            AccessTools.FieldRefAccess<tooltip_fans_line, float>(nameof(tooltip_fans_line.Negative));

        // Positive / (Positive + Negative) whenever there are votes, otherwise the game's value.
        public static float Ratio(float gameValue, tooltip_fans_line line)
        {
            float votes = Positive(line) + Negative(line);
            return votes > 0f ? Positive(line) / votes : gameValue;
        }

        // The opinion on the idol profile's scale: 0 is -100, 0.5 is 0, 1 is +100.
        public static int Shown(float ratio)
        {
            return Mathf.RoundToInt(ratio * 200f - 100f);
        }
    }

    // Fixes a fan segment with no opinion votes reading as not-a-number instead of neutral: _fan.Recalc sets 0.5 for
    // no votes, then overwrites it with 0 / 0. Idols always hold votes, but a mod's segments may not. State-based:
    // only a not-a-number result is changed.
    [HarmonyPatch(typeof(resources._fan), nameof(resources._fan.Recalc))]
    public class resources__fan_Recalc
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<resources__fan_Recalc>(ex);

        private const float Neutral = 0.5f;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(resources._fan __instance)
        {
            if (float.IsNaN(__instance.Ratio))
                __instance.Ratio = Neutral;
        }
    }
}
