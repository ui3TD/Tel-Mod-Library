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
    // Forget the previous game's types. Reset runs on every scene start (new game or load)
    // and inside LoadFunction; a new game restarts idol IDs at 0, so cached types would carry over.
    [HarmonyPatch(typeof(data_girls), "Reset")]
    public class data_girls_Reset
    {
        public static void Postfix()
        {
            ResetMBTI();
        }
    }

    // Load MBTI data
    [HarmonyPatch(typeof(data_girls), "LoadFunction")]
    public class data_girls_LoadFunction
    {
        public static void Postfix()
        {
            ResetMBTI();

            foreach (data_girls.girls girl in data_girls.girl)
            {
                // A save from an older version can hold more than one type; the last one wins, as it
                // always did, and setting it removes the others
                MBTI saved = MBTI.None;
                foreach (string variable in girl.Variables)
                {
                    if (TryParseMBTI(variable, out MBTI mBTI))
                    {
                        saved = mBTI;
                    }
                }
                if (saved != MBTI.None)
                {
                    SetGirlMBTI(girl, saved);
                }
            }
        }
    }


    // Load MBTI from unique idol textures
    [HarmonyPatch(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) })]
    public class data_girls_textures_LoadAssetsData
    {
        // After the game parses a body folder's params.json into a local, call Infix with it and that folder's body asset
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);
            CodeMatcher matcher = new CodeMatcher(instructionList).MatchEndForward(
                new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(data_girls_textures), "ProcessInboundData"))),
                new CodeMatch(ci => ci.IsStloc()));
            if (matcher.IsInvalid)
            {
                UnityEngine.Debug.LogError("[MBTI Personalities] Couldn't find where the game reads params.json; \"mbti\" is ignored");
                return instructionList;
            }
            object jsonNode = matcher.Operand;

            // The body asset is the last one stored before params.json is read
            object textureAsset = instructionList.Take(matcher.Pos).LastOrDefault(ci => ci.IsStloc() && ci.operand is LocalVariableInfo local
                && local.LocalType == typeof(data_girls_textures._textureAsset))?.operand;
            if (textureAsset == null)
            {
                UnityEngine.Debug.LogError("[MBTI Personalities] Couldn't find the body asset that params.json is read into; \"mbti\" is ignored");
                return instructionList;
            }

            return matcher.Advance(1).Insert(
                new CodeInstruction(OpCodes.Ldloc_S, jsonNode),
                new CodeInstruction(OpCodes.Ldloc_S, textureAsset),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(Infix))))
                .InstructionEnumeration();
        }

        public static void Infix(JSONNode jsonnode, data_girls_textures._textureAsset textureAsset)
        {
            if (jsonnode[MBTINodeID] == null)
                return;

            string mbtiString = jsonnode[MBTINodeID];
            if (!TryParseMBTI(mbtiString, out MBTI girlMBTI))
            {
                UnityEngine.Debug.LogWarning("[MBTI Personalities] MBTI type not found: " + mbtiString);
                return;
            }

            UniqueIdolTypes[UniqueIdolKey(textureAsset.ModName, textureAsset.body_id)] = girlMBTI;
        }
    }

}
