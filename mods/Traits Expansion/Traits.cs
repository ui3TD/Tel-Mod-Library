using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using static TraitsExpansion.TraitsExpansion;
using System.Reflection.Emit;
using System.Reflection;
using SimpleJSON;

namespace TraitsExpansion
{
    [HarmonyPatch(typeof(traits), nameof(traits.GetTraitType))]
    public class traits_GetTraitType
    {
        public static void Postfix(string str, ref traits._trait._type __result)
        {
            if (Enum.TryParse(str, out NewTraits parsedEnum))
            {
                __result = (traits._trait._type)parsedEnum;
            }
        }
    }

    // Load traits from unique idols. The game only knows its own traits: for one of this mod's, its
    // Enum.Parse throws and it logs "Trait not found". Read the mod's traits right after the game parses
    // params.json (the game's parse, later, still wins for its own traits), and only warn about traits
    // neither knows.
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.LoadAssetsData), new[] {typeof(string)})]
    public class data_girls_textures_LoadAssetsData
    {
        private const string TraitNotFound = "Trait not found: ";

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);
            CodeMatcher matcher = new CodeMatcher(instructionList).MatchEndForward(
                new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(data_girls_textures), nameof(data_girls_textures.ProcessInboundData)))),
                new CodeMatch(ci => ci.IsStloc()));
            if (matcher.IsInvalid)
            {
                Debug.LogError("[Traits Expansion] Couldn't find where the game reads params.json; idol packs can't set this mod's traits");
                return instructionList;
            }
            object jsonNode = matcher.Operand;

            // The body asset is the last one stored before params.json is read
            object textureAsset = instructionList.Take(matcher.Pos).LastOrDefault(ci => ci.IsStloc() && ci.operand is LocalVariableInfo local
                && local.LocalType == typeof(data_girls_textures._textureAsset))?.operand;
            if (textureAsset == null)
            {
                Debug.LogError("[Traits Expansion] Couldn't find the body asset that params.json is read into; idol packs can't set this mod's traits");
                return instructionList;
            }

            matcher.Advance(1).Insert(
                new CodeInstruction(OpCodes.Ldloc_S, textureAsset),
                new CodeInstruction(OpCodes.Ldloc_S, jsonNode),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(Infix))));

            // Debug.LogWarning("Trait not found: " + ...) becomes WarnUnlessModTrait(message, jsonNode)
            MethodInfo logWarning = AccessTools.Method(typeof(Debug), nameof(Debug.LogWarning), new[] { typeof(object) });
            matcher.MatchStartForward(new CodeMatch(ci => ci.Is(OpCodes.Ldstr, TraitNotFound)))
                .MatchStartForward(new CodeMatch(ci => ci.Calls(logWarning)));
            if (matcher.IsInvalid)
            {
                Debug.LogWarning("[Traits Expansion] Couldn't find the game's \"Trait not found\" warning; it will also name this mod's traits");
                return matcher.InstructionEnumeration();
            }
            matcher.Operand = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(WarnUnlessModTrait));
            matcher.Insert(new CodeInstruction(OpCodes.Ldloc_S, jsonNode));
            return matcher.InstructionEnumeration();
        }

        public static void WarnUnlessModTrait(object message, JSONNode jsonnode)
        {
            if (jsonnode == null || !Enum.TryParse(jsonnode["trait"], out NewTraits _))
            {
                Debug.LogWarning(message);
            }
        }

        public static void Infix(data_girls_textures._textureAsset textureAsset, JSONNode jsonnode)
        {
            if (textureAsset != null && jsonnode != null && jsonnode["trait"] != null && Enum.TryParse(jsonnode["trait"], out NewTraits parsedEnum))
            {
                textureAsset.Trait = (traits._trait._type)parsedEnum;
            }
        }
    }
}
