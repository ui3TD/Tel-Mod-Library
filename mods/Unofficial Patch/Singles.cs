using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;

namespace UnofficialPatch
{
	// Fixed senbatsu stats calculation so it doesn't punish you if you don't have enough idols to fill all rows
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
    public class singles__single_SenbatsuCalcParam
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles__single_SenbatsuCalcParam>(ex);

        // Percent scaling constant used by the base formula.
        private const float PercentScale = 100f;
        // IL pattern length for the 100f / rows divisor.
        private const int DivPatternLength = 4;
        // Senbatsu row limits and guard values.
        private const int NoIdols = 0;
        private const int MinRows = 1;
        private const int MaxRows = 5;
        // Constants for triangular number inversion.
        private const float TriangularScale = 8f;
        private const float TriangularOffset = 1f;
        private const float TriangularDivisor = 2f;
        private const float ZeroPercent = 0f;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the infix method that computes percent based on the rows the agency can fill.
            MethodInfo infix = AccessTools.Method(typeof(singles__single_SenbatsuCalcParam), nameof(Infix));
            MethodInfo countGirls = AccessTools.Method(typeof(Groups._group), nameof(Groups._group.GetNumberOfNonGraduatedGirls));
            if (infix == null || countGirls == null)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("Infix or GetNumberOfNonGraduatedGirls lookup failed.");
                return instructions;
            }

            // Read once: another mod's transpiler may hand over a sequence that rebuilds on every read.
            instructions = instructions.ToList();
            if (instructions.Any(ci => IlHelpers.IsCallTo(ci, infix)))
            {
                // Already calls the Infix: patched twice.
                return instructions;
            }

            var matcher = new CodeMatcher(instructions);
            // Find the local holding the agency's idol count (num): the group's count, plus the other groups' for the main group.
            matcher.MatchForward(false,
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, countGirls)),
                new CodeMatch(ci => IlHelpers.IsStloc(ci)));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("idol count local not found.");
                return instructions;
            }
            CodeInstruction storeIdolCount = matcher.InstructionAt(1);

            matcher.Start();
            // Find the sequence that divides 100f by the row count (num2).
            matcher.MatchForward(false,
                new CodeMatch(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float value && value == PercentScale),
                new CodeMatch(ci => IlHelpers.IsLdloc(ci)),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(OpCodes.Div));

            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("100/rows divisor not found.");
                return instructions;
            }

            // Ensure the divisor result is immediately stored in a local.
            CodeInstruction storeInstruction = matcher.InstructionAt(DivPatternLength);
            if (storeInstruction == null || !IlHelpers.IsStloc(storeInstruction))
            {
                PatchLog.WarnOncePerPatch<singles__single_SenbatsuCalcParam>("100/rows divisor found, but store opcode not found.");
                return instructions;
            }

            // Replace 100f / num2 with Infix(num) to count only the rows the agency can fill completely.
            var labels = matcher.Instruction.labels.ToList();
            var blocks = matcher.Instruction.blocks.ToList();
            matcher.RemoveInstructions(DivPatternLength);
            var loadIdolCount = IlHelpers.LoadOfStore(storeIdolCount);
            loadIdolCount.labels.AddRange(labels);
            loadIdolCount.blocks.AddRange(blocks);
            matcher.Insert(loadIdolCount, new CodeInstruction(OpCodes.Call, infix));
            return matcher.InstructionEnumeration();
        }

        public static float Infix(int idolCount)
        {
            // Total rows in the senbatsu formation:
            // 1, 2, 3, 4, 5  (total capacity = 15)
            // Safety: if no idols, don't divide by zero.
            if (idolCount <= NoIdols)
                return ZeroPercent;

            // Triangular number inversion:
            // r represents the number of rows the agency's idols (n) can fill completely:
            // the largest r such that r(r+1)/2 <= n.
            // A row the agency can't fill doesn't count against the single; idols placed in it add on top.
            //
            // r = floor((sqrt(8N + 1) - 1) / 2)
            float n = idolCount;
            float r = (Mathf.Sqrt(TriangularScale * n + TriangularOffset) - TriangularOffset) / TriangularDivisor;

            // Round down to the last complete row.
            int rowsUsed = Mathf.FloorToInt(r);

            // Clamp to the real formation size:
            // Anything above 15 idols still just uses all 5 rows.
            rowsUsed = Mathf.Clamp(rowsUsed, MinRows, MaxRows);

            // The game wants a "percentage per used row" kind of factor.
            return PercentScale / rowsUsed;
        }

    }

    // Fix senbatsu parameter queries to use the requested param type. The game passes cute (0) to
    // SenbatsuCalcParam whatever type was asked for; pass the Type argument instead.
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.GetSenbatsuParamValue))]
    public class singles__single_GetSenbatsuParamValue
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles__single_GetSenbatsuParamValue>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo calcParam = AccessTools.Method(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam),
                new Type[] { typeof(List<data_girls.girls>), typeof(data_girls._paramType), typeof(Groups._group) });
            if (calcParam == null)
            {
                PatchLog.WarnOncePerPatch<singles__single_GetSenbatsuParamValue>("SenbatsuCalcParam lookup failed.");
                return instructions;
            }

            // SenbatsuCalcParam(girls, cute, null): the type is the second argument pushed before the call.
            var matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, (int)data_girls._paramType.cute)),
                new CodeMatch(OpCodes.Ldnull),
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, calcParam)));
            if (matcher.IsInvalid)
            {
                // Already passes the type: patched twice, or the game fixed it.
                return instructions;
            }

            // GetSenbatsuParamValue(Type) is an instance method, so Type is argument 1.
            matcher.Instruction.opcode = OpCodes.Ldarg_1;
            matcher.Instruction.operand = null;
            return matcher.InstructionEnumeration();
        }
    }

    // Edgy and Artsy PVs now change sales and new fans. The game checks the wrong fan field for them
    // (gender == teen, hardcoreness == female, ...), which is never true, so they did nothing on success or
    // failure. Compare the field each fan type belongs to, the targets the marketing spinner shows.
    [HarmonyPatch(typeof(singles), nameof(singles.ValueAfterMarketing))]
    public class singles_ValueAfterMarketing
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<singles_ValueAfterMarketing>(ex);

        // Edgy and Artsy each check 2 fields on success and 2 on failure.
        public const int ExpectedFixes = 8;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo gender = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.gender));
            FieldInfo hardcoreness = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.hardcoreness));
            FieldInfo age = AccessTools.Field(typeof(resources._fan), nameof(resources._fan.age));

            List<CodeInstruction> codes = instructions.ToList();
            List<int> fixes = new List<int>();
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode != OpCodes.Ldfld || !(codes[i].operand is FieldInfo field) || (field != gender && field != hardcoreness))
                    continue;

                FieldInfo owner = OwnerOfComparedType(codes[i + 1], gender, hardcoreness, age);
                if (owner != null && owner != field)
                    fixes.Add(i);
            }

            if (fixes.Count == 0)
            {
                // Already compares the right fields: patched twice, or the game fixed it.
                return codes;
            }
            if (fixes.Count != ExpectedFixes)
            {
                PatchLog.WarnOncePerPatch<singles_ValueAfterMarketing>("found " + fixes.Count + " wrong fan fields, expected " + ExpectedFixes + "; left unchanged.");
                return codes;
            }

            foreach (int i in fixes)
                codes[i].operand = OwnerOfComparedType(codes[i + 1], gender, hardcoreness, age);
            return codes;
        }

        // The fan field holding the fan type an instruction compares against: ldc.i4 <type>, or brtrue/brfalse
        // for male (0). Null if it doesn't compare against a fan type.
        private static FieldInfo OwnerOfComparedType(CodeInstruction compare, FieldInfo gender, FieldInfo hardcoreness, FieldInfo age)
        {
            if (IlHelpers.IsBranchTrue(compare) || compare.opcode == OpCodes.Brfalse || compare.opcode == OpCodes.Brfalse_S)
                return gender;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.male) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.female))
                return gender;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.casual) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.hardcore))
                return hardcoreness;
            if (IlHelpers.IsLdcI4(compare, (int)resources.fanType.teen) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.youngAdult) || IlHelpers.IsLdcI4(compare, (int)resources.fanType.adult))
                return age;
            return null;
        }
    }
}
