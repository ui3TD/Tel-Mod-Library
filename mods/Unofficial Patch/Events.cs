using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System;

namespace UnofficialPatch
{
    // Fixes dating scandals never costing mental stamina. MentalDamageDating should skip Thick Skin and Shameless
    // idols, but tests trait != Thick_Skin || trait != Shameless, which is true for every idol, so it always returns
    // "". Skip only those two traits. Pattern-based: no change once the test is fixed.
    [HarmonyPatch(typeof(Event_Templates._active_template), nameof(Event_Templates._active_template.MentalDamageDating))]
    public class Event_Templates__active_template_MentalDamageDating
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<Event_Templates__active_template_MentalDamageDating>(ex);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo trait = AccessTools.Field(typeof(data_girls.girls), nameof(data_girls.girls.trait));

            // if (trait != Thick_Skin) return ""; if (trait == Shameless) apply; return "";
            var matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => ci.LoadsField(trait)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, (int)traits._trait._type.Thick_Skin)),
                new CodeMatch(ci => ci.opcode == OpCodes.Bne_Un || ci.opcode == OpCodes.Bne_Un_S));
            if (matcher.IsInvalid)
            {
                // Already gone: patched twice, or the game fixed it.
                return instructions;
            }
            CodeInstruction thickSkin = matcher.InstructionAt(2);

            matcher.Advance(3).MatchStartForward(
                new CodeMatch(ci => ci.LoadsField(trait)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, (int)traits._trait._type.Shameless)),
                new CodeMatch(ci => ci.opcode == OpCodes.Beq || ci.opcode == OpCodes.Beq_S));
            if (matcher.IsInvalid)
            {
                PatchLog.WarnOncePerPatch<Event_Templates__active_template_MentalDamageDating>("Shameless test not found.");
                return instructions;
            }

            // if (trait == Thick_Skin) return ""; if (trait != Shameless) apply; return "";
            thickSkin.opcode = thickSkin.opcode == OpCodes.Bne_Un ? OpCodes.Beq : OpCodes.Beq_S;
            CodeInstruction shameless = matcher.InstructionAt(2);
            shameless.opcode = shameless.opcode == OpCodes.Beq ? OpCodes.Bne_Un : OpCodes.Bne_Un_S;
            return matcher.InstructionEnumeration();
        }
    }

    // Fixed event and dialogue checks for Influence to check for Influence instead of Friendship
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckGirl), new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckGirl>(ex);

        // Requirement key used by the game for influence checks.
        private const string InfluenceParameter = "influence";

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Resolve the relationship checker to ensure we update the correct call site.
            MethodInfo checkRelationship = AccessTools.Method(
                typeof(vn_requirements),
                "CheckRelationship",
                new Type[] { typeof(data_girls.girls), typeof(string), typeof(Relationships_Player._type) });
            MethodInfo stringEquals = AccessTools.Method(typeof(string), "op_Equality", new Type[] { typeof(string), typeof(string) });
            if (checkRelationship == null || stringEquals == null)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship or string equality lookup failed.");
                return instructions;
            }

            List<CodeInstruction> code = new List<CodeInstruction>(instructions);

            // The switch compares the parameter with "influence", then branches: to the case on true, or past
            // it on false (the case then follows the branch).
            int compare = code.FindIndex(ci => ci.opcode == OpCodes.Ldstr && ci.operand is string text && text == InfluenceParameter);
            if (compare < 0 || compare + 2 >= code.Count || !IlHelpers.IsCallTo(code[compare + 1], stringEquals))
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("\"influence\" comparison not found.");
                return code;
            }
            CodeInstruction branch = code[compare + 2];
            int caseStart;
            if (IlHelpers.IsBranchTrue(branch))
            {
                caseStart = code.FindIndex(ci => ci.labels.Contains((Label)branch.operand));
            }
            else if (branch.opcode == OpCodes.Brfalse || branch.opcode == OpCodes.Brfalse_S)
            {
                caseStart = compare + 3;
            }
            else
            {
                caseStart = -1;
            }
            if (caseStart < 0)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("\"influence\" case not found.");
                return code;
            }

            // The case's call: CheckRelationship(girl, formula, Friendship), the type pushed just before it.
            int call = code.FindIndex(caseStart, ci => IlHelpers.IsCallTo(ci, checkRelationship) || ci.opcode == OpCodes.Ret);
            if (call <= caseStart || code[call].opcode == OpCodes.Ret)
            {
                PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("CheckRelationship call not found in the \"influence\" case.");
                return code;
            }

            CodeInstruction type = code[call - 1];
            if (IlHelpers.IsLdcI4(type, (int)Relationships_Player._type.Friendship))
            {
                // Replace Friendship with Influence.
                type.opcode = OpCodes.Ldc_I4_2;
                type.operand = null;
                return code;
            }

            if (IlHelpers.IsLdcI4(type, (int)Relationships_Player._type.Influence))
            {
                // Already patched or game fixed it upstream.
                return code;
            }

            PatchLog.WarnOncePerPatch<vn_requirements_CheckGirl>("unexpected type before the \"influence\" CheckRelationship call.");
            return code;
        }
    }

    // Fix "variable" requirements to respect leading negation. The game sees the "!", then throws away
    // formula.Substring(1), so "!met_fan" checks a variable named "!met_fan". Store it back into formula.
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckGirl), new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl_Variable
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckGirl_Variable>(ex);

        // CheckGirl(girl, parameter, formula) is static, so formula is argument 2.
        private const int FormulaArgument = 2;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo substring = AccessTools.Method(typeof(string), nameof(string.Substring), new Type[] { typeof(int) });

            // formula.Substring(1); with the result dropped.
            var matcher = new CodeMatcher(instructions).MatchStartForward(
                new CodeMatch(ci => IlHelpers.IsLdarg(ci, FormulaArgument)),
                new CodeMatch(ci => IlHelpers.IsLdcI4(ci, 1)),
                new CodeMatch(ci => IlHelpers.IsCallTo(ci, substring)),
                new CodeMatch(OpCodes.Pop));
            if (matcher.IsInvalid)
            {
                // Already stored: patched twice, or the game fixed it.
                return instructions;
            }

            // formula = formula.Substring(1);
            matcher.Advance(3);
            matcher.Instruction.opcode = OpCodes.Starg_S;
            matcher.Instruction.operand = (byte)FormulaArgument;
            return matcher.InstructionEnumeration();
        }
    }

    // Fix custom event check for "hired a staffer of type"
    [HarmonyPatch(typeof(vn_requirements), nameof(vn_requirements.CheckMeta))]
    public class vn_requirements_CheckMeta
    {
        public static Exception Cleanup(Exception ex) => PatchLog.PatchFailed<vn_requirements_CheckMeta>(ex);

        // Metadata parameter and formula keys.
        private const string StaffParameter = "staff";
        private const string VocalFormula = "vocal";
        private const string DanceFormula = "dance";
        private const string OfficeFormula = "office";
        private const string StyleFormula = "style";
        private const int MinimumStaffCount = 0;

        public static void Postfix(string parameter, string formula, ref bool __result)
        {
            // Only handle staff-type checks; let other meta parameters remain unchanged.
            if (parameter != StaffParameter)
                return;

            // Map formula keys to their matching staff room types.
            if (formula == VocalFormula && staff.CountStaffersOfType(agency._type.recordingStudio) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == DanceFormula && staff.CountStaffersOfType(agency._type.danceStudio) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == OfficeFormula && staff.CountStaffersOfType(agency._type.office) > MinimumStaffCount)
            {
                __result = true;
            }
            else if(formula == StyleFormula && staff.CountStaffersOfType(agency._type.dressingRoom) > MinimumStaffCount)
            {
                __result = true;
            }

            
        }
    }
}
