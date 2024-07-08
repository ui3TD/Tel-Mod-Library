using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Reflection;
using static MBTIPersonalities.MBTIPersonalities;
using SimpleJSON;
using System.Linq;
using System.Reflection.Emit;

namespace MBTIPersonalities
{
    // Load MBTI data
    [HarmonyPatch(typeof(data_girls), "LoadFunction")]
    public class data_girls_LoadFunction
    {
        public static void Postfix()
        {
            ResetMBTI();

            foreach (data_girls.girls girl in data_girls.girl)
            {
                foreach (string variable in girl.Variables)
                {
                    if(Enum.TryParse(variable, true, out MBTI mBTI))
                    {
                        SetGirlMBTI(girl, mBTI);
                    }
                }
            }
        }
    }


    // Load MBTI from unique idol textures
    [HarmonyPatch(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) })]
    public class data_girls_textures_LoadAssetsData
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object textureAssetOperand = null;
            object jsonNodeOperand = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 8)
                {
                    textureAssetOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 13)
                {
                    index = i;
                    jsonNodeOperand = instructionList[i].operand;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, jsonNodeOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, textureAssetOperand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), "Infix")));
            }

            return instructionList.AsEnumerable();
        }

        public static void Infix(JSONNode jsonnode, data_girls_textures._textureAsset textureAsset)
        {
            if (jsonnode[MBTINodeID] == null)
                return;

            string mbtiString = jsonnode[MBTINodeID];

            MBTI girlMBTI = Enum.TryParse(mbtiString, true, out MBTI tryMBTI) ? tryMBTI : MBTI.None;

            MBTITextureData mBTITextureData = new()
            {
                ModName = textureAsset.ModName,
                body_id = textureAsset.body_id,
                mbti = girlMBTI
            };

            MBTITextureReferenceList.Add(mBTITextureData);
        }
    }

}
