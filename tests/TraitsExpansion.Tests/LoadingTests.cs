using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TraitsExpansion;
using Xunit;
using static TraitsExpansion.TraitsExpansion;

namespace TraitsExpansionTests
{
    /// <summary>
    /// The game reads trait names from traits.json, events and unique idols' params.json.
    /// The mod's names resolve to its own trait numbers.
    /// </summary>
    public class TraitNameTests
    {
        public TraitNameTests() => TestGame.Reset(patched: true);

        [Fact]
        public void ModTraitNames_Resolve()
        {
            foreach (NewTraits trait in Enum.GetValues(typeof(NewTraits)))
            {
                if (trait == NewTraits.none)
                    continue;
                Assert.Equal((traits._trait._type)trait, traits.GetTraitType(trait.ToString()));
            }
        }

        [Theory]
        [InlineData("Prodigy", traits._trait._type.Prodigy)]
        [InlineData("Moonlighter", traits._trait._type.Moonlighter)]
        [InlineData("Not_A_Trait", traits._trait._type.None)]
        [InlineData("", traits._trait._type.None)]
        public void VanillaNames_Unchanged(string name, traits._trait._type expected)
        {
            Assert.Equal(expected, traits.GetTraitType(name));
        }

        /// <summary>
        /// Every entry in the mod's traits.json loads as the matching trait, through the game's loader.
        /// </summary>
        [Fact]
        public void TraitsJson_LoadsAsModTraits()
        {
            JSONNode list = TestGame.LoadJson("JSON/Idols/traits.json");
            Assert.Equal(Enum.GetValues(typeof(NewTraits)).Length - 1, list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                traits._trait trait = new();
                trait.Set(list[i]);
                Assert.Equal((traits._trait._type)Enum.Parse(typeof(NewTraits), list[i]["type"]), trait.Type);
            }
        }
    }

    /// <summary>
    /// A unique idol's params.json can give her one of the mod's traits.
    /// </summary>
    public class UniqueIdolTraitTests
    {
        private static data_girls_textures._textureAsset Load(string json, traits._trait._type vanilla = traits._trait._type.None)
        {
            data_girls_textures._textureAsset asset = new() { Trait = vanilla };
            return data_girls_textures_LoadAssetsData.Infix(asset, JSON.Parse(json));
        }

        [Theory]
        [InlineData("Cult_Leader", NewTraits.Cult_Leader)]
        [InlineData("Perfect_Pitch", NewTraits.Perfect_Pitch)]
        [InlineData("Thespian", NewTraits.Thespian)]
        public void ModTrait_Set(string name, NewTraits expected)
        {
            Assert.Equal((traits._trait._type)expected, Load($"{{\"trait\": \"{name}\"}}").Trait);
        }

        [Theory]
        [InlineData("{\"trait\": \"Prodigy\"}")]
        [InlineData("{\"trait\": \"Not_A_Trait\"}")]
        [InlineData("{}")]
        public void OtherwiseUnchanged(string json)
        {
            Assert.Equal(traits._trait._type.Clumsy, Load(json, traits._trait._type.Clumsy).Trait);
        }

        /// <summary>
        /// The game only reaches the mod's code when its own parse fails: the transpiler inserts
        /// textureAsset = Infix(textureAsset, jsonNode) at the start of the "Trait not found" catch block.
        /// </summary>
        [Fact]
        public void Transpiler_InsertsCallInTraitCatch()
        {
            MethodInfo original = AccessTools.Method(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) });
            List<CodeInstruction> before = PatchProcessor.GetOriginalInstructions(original);
            List<CodeInstruction> after = data_girls_textures_LoadAssetsData.Transpiler(before.Select(i => i.Clone())).ToList();

            Assert.Equal(before.Count + 4, after.Count);
            MethodInfo infix = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.Infix));
            int call = after.FindIndex(i => i.opcode == OpCodes.Call && Equals(i.operand, infix));
            Assert.True(call > 2, "Infix call not inserted");

            Assert.Equal(OpCodes.Pop, after[call - 3].opcode);
            AssertLocal(after[call - 2], OpCodes.Ldloc_S, typeof(data_girls_textures._textureAsset));
            AssertLocal(after[call - 1], OpCodes.Ldloc_S, typeof(JSONNode));
            AssertLocal(after[call + 1], OpCodes.Stloc_S, typeof(data_girls_textures._textureAsset));
            Assert.Equal(OpCodes.Ldstr, after[call + 2].opcode);
            Assert.Equal("Trait not found: ", after[call + 2].operand);
        }

        private static void AssertLocal(CodeInstruction instruction, OpCode opcode, Type type)
        {
            Assert.Equal(opcode, instruction.opcode);
            LocalVariableInfo local = Assert.IsAssignableFrom<LocalVariableInfo>(instruction.operand);
            Assert.Equal(type, local.LocalType);
        }
    }
}
