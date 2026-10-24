using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System;

namespace UnofficialPatch
{
    // Shared IL helper predicates to keep opcode checks consistent and reusable.
    internal static class IlHelpers
    {
        // Identifies call/callvirt instructions targeting a specific method.
        public static bool IsCallTo(CodeInstruction instruction, MethodInfo target)
        {
            if (instruction == null || target == null)
                return false;

            if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                return false;

            return instruction.operand is MethodInfo method && method == target;
        }

        // Checks whether an instruction loads an int constant with any ldc.i4 opcode form.
        public static bool IsLdcI4(CodeInstruction instruction, int value)
        {
            if (instruction == null)
                return false;

            // Match the specific ldc.i4 opcode variants that encode constant -1..8 directly.
            if (instruction.opcode == OpCodes.Ldc_I4_M1)
                return value == -1;
            if (instruction.opcode == OpCodes.Ldc_I4_0)
                return value == 0;
            if (instruction.opcode == OpCodes.Ldc_I4_1)
                return value == 1;
            if (instruction.opcode == OpCodes.Ldc_I4_2)
                return value == 2;
            if (instruction.opcode == OpCodes.Ldc_I4_3)
                return value == 3;
            if (instruction.opcode == OpCodes.Ldc_I4_4)
                return value == 4;
            if (instruction.opcode == OpCodes.Ldc_I4_5)
                return value == 5;
            if (instruction.opcode == OpCodes.Ldc_I4_6)
                return value == 6;
            if (instruction.opcode == OpCodes.Ldc_I4_7)
                return value == 7;
            if (instruction.opcode == OpCodes.Ldc_I4_8)
                return value == 8;
            if (instruction.opcode == OpCodes.Ldc_I4_S)
                return instruction.operand is sbyte sbyteValue && sbyteValue == value;
            if (instruction.opcode == OpCodes.Ldc_I4)
                return instruction.operand is int intValue && intValue == value;

            return false;
        }

        // Checks whether an instruction loads a local variable.
        public static bool IsLdloc(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Ldloc ||
                instruction.opcode == OpCodes.Ldloc_S ||
                instruction.opcode == OpCodes.Ldloc_0 ||
                instruction.opcode == OpCodes.Ldloc_1 ||
                instruction.opcode == OpCodes.Ldloc_2 ||
                instruction.opcode == OpCodes.Ldloc_3;
        }

        // Checks whether an instruction stores a local variable.
        public static bool IsStloc(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Stloc ||
                instruction.opcode == OpCodes.Stloc_S ||
                instruction.opcode == OpCodes.Stloc_0 ||
                instruction.opcode == OpCodes.Stloc_1 ||
                instruction.opcode == OpCodes.Stloc_2 ||
                instruction.opcode == OpCodes.Stloc_3;
        }

        // Builds the instruction that loads the local a stloc instruction stores to.
        public static CodeInstruction LoadOfStore(CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0)
                return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1)
                return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2)
                return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3)
                return new CodeInstruction(OpCodes.Ldloc_3);
            if (store.opcode == OpCodes.Stloc_S)
                return new CodeInstruction(OpCodes.Ldloc_S, store.operand);
            return new CodeInstruction(OpCodes.Ldloc, store.operand);
        }

        // Checks whether an instruction loads the argument at this index (static methods count from 0).
        public static bool IsLdarg(CodeInstruction instruction, int index)
        {
            if (instruction == null)
                return false;

            if (instruction.opcode == OpCodes.Ldarg_0) return index == 0;
            if (instruction.opcode == OpCodes.Ldarg_1) return index == 1;
            if (instruction.opcode == OpCodes.Ldarg_2) return index == 2;
            if (instruction.opcode == OpCodes.Ldarg_3) return index == 3;
            if (instruction.opcode == OpCodes.Ldarg_S || instruction.opcode == OpCodes.Ldarg)
                return Convert.ToInt32(instruction.operand) == index;

            return false;
        }

        // Checks for conditional branches that jump when the stack value is true.
        public static bool IsBranchTrue(CodeInstruction instruction)
        {
            if (instruction == null)
                return false;

            return instruction.opcode == OpCodes.Brtrue || instruction.opcode == OpCodes.Brtrue_S;
        }
    }
}
