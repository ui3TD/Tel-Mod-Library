using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace EffortlessTraining
{

    /// <summary>
    /// This class reduces the training vocal/dance stamina to 1/day.
    /// </summary>
    [HarmonyPatch(typeof(agency._room), "DoGirlTraining")]
    public class agency__room_DoGirlTraining
    {
        private const float DAILY_TRAINING_COST = 1f;

        /// <summary>
        /// Transpiler method to modify the IL code of the DoGirlTraining method.
        /// </summary>
        /// <param name="instructions">The original IL instructions of the method.</param>
        /// <returns>Modified IL instructions with reduced training stamina cost.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            for (int i = 0; i < instructionList.Count - 1; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldc_R4 && instructionList[i + 1].opcode == OpCodes.Ldc_R4 && (float)instructionList[i + 1].operand == 1440f)
                {
                    float dailyCost = (float)instructionList[i].operand;
                    if (dailyCost == 3f)
                    {
                        instructionList[i].operand = DAILY_TRAINING_COST;
                        return instructionList.AsEnumerable();
                    }
                    if (dailyCost == DAILY_TRAINING_COST)
                    {
                        // Already 1 per day: this patch ran twice
                        return instructionList.AsEnumerable();
                    }
                }
            }

            // Another mod changed the cost: leave it, and say why training costs aren't reduced
            Debug.LogError("[Effortless Training] Couldn't find the game's 3 stamina per day training cost in DoGirlTraining; training costs are left as they are");
            return instructionList.AsEnumerable();
        }
    }

}
