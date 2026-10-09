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
            data_girls_textures_LoadAssetsData.Infix(asset, JSON.Parse(json));
            return asset;
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
        /// The mod reads the trait right after the game parses params.json, while the game's body asset for
        /// that folder is in hand: Infix(textureAsset, jsonNode). The game's own parse comes later, so for
        /// its own traits it still decides.
        /// </summary>
        [Fact]
        public void Transpiler_ReadsTheTraitAfterTheJsonIsParsed()
        {
            MethodInfo original = AccessTools.Method(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) });
            List<CodeInstruction> before = PatchProcessor.GetOriginalInstructions(original);
            List<CodeInstruction> after = data_girls_textures_LoadAssetsData.Transpiler(before.Select(i => i.Clone())).ToList();

            Assert.Equal(before.Count + 4, after.Count);
            MethodInfo infix = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.Infix));
            int call = after.FindIndex(i => i.opcode == OpCodes.Call && Equals(i.operand, infix));
            Assert.True(call > 2, "Infix call not inserted");

            Assert.True(after[call - 3].IsStloc());
            AssertLocal(after[call - 2], OpCodes.Ldloc_S, typeof(data_girls_textures._textureAsset));
            AssertLocal(after[call - 1], OpCodes.Ldloc_S, typeof(JSONNode));
            Assert.Same(after[call - 3].operand, after[call - 1].operand);
            int gameParse = after.FindIndex(i => i.opcode == OpCodes.Ldstr && (string)i.operand == "trait");
            Assert.True(gameParse > call, "The game's own trait parse should come after the mod's");

            // The catch block's warning goes through the mod, with the idol's JSON
            MethodInfo logWarning = AccessTools.Method(typeof(UnityEngine.Debug), nameof(UnityEngine.Debug.LogWarning), new[] { typeof(object) });
            MethodInfo warn = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.WarnUnlessModTrait));
            int notFound = after.FindIndex(i => i.opcode == OpCodes.Ldstr && (string)i.operand == "Trait not found: ");
            int warning = after.FindIndex(notFound, i => i.Calls(warn));
            Assert.True(warning > notFound, "Warning not redirected");
            AssertLocal(after[warning - 1], OpCodes.Ldloc_S, typeof(JSONNode));
            Assert.DoesNotContain(after.Skip(notFound).Take(warning - notFound), i => i.Calls(logWarning));
            Assert.Equal(before.Count(i => i.Calls(logWarning)) - 1, after.Count(i => i.Calls(logWarning)));
        }

        /// <summary>
        /// If a game update changes how params.json is read, the game's code is left as it is and the mod
        /// says why its traits stopped loading, instead of failing to load.
        /// </summary>
        [Fact]
        public void Transpiler_GameCodeChanged_LeavesItAndLogs()
        {
            TestGame.Reset();
            UnityEngine.ILogHandler gameLog = UnityEngine.Debug.unityLogger.logHandler;
            List<string> logged = new();
            UnityEngine.Debug.unityLogger.logHandler = new Recorder(logged);
            try
            {
                List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(data_girls_textures), "LoadDefaultAssets"));

                List<CodeInstruction> patched = data_girls_textures_LoadAssetsData.Transpiler(instructions).ToList();

                Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));
                Assert.Contains(logged, m => m.StartsWith("[Traits Expansion] Couldn't find"));
            }
            finally
            {
                UnityEngine.Debug.unityLogger.logHandler = gameLog;
            }
        }

        private class Recorder : UnityEngine.ILogHandler
        {
            private readonly List<string> messages;
            public Recorder(List<string> messages) => this.messages = messages;
            public void LogFormat(UnityEngine.LogType logType, UnityEngine.Object context, string format, params object[] args) => messages.Add(string.Format(format, args));
            public void LogException(Exception exception, UnityEngine.Object context) => messages.Add(exception.ToString());
        }

        [Theory]
        [InlineData("Cult_Leader")]
        [InlineData("Thespian")]
        public void ModTrait_NoWarning(string name)
        {
            TestGame.Reset();
            data_girls_textures_LoadAssetsData.WarnUnlessModTrait("Trait not found: " + name, JSON.Parse($"{{\"trait\": \"{name}\"}}"));
            Assert.Empty(Seams.Warnings);
        }

        [Fact]
        public void UnknownTrait_StillWarns()
        {
            TestGame.Reset();
            data_girls_textures_LoadAssetsData.WarnUnlessModTrait("Trait not found: Not_A_Trait", JSON.Parse("{\"trait\": \"Not_A_Trait\"}"));
            Assert.Equal(new[] { "Trait not found: Not_A_Trait" }, Seams.Warnings);
        }

        private static void AssertLocal(CodeInstruction instruction, OpCode opcode, Type type)
        {
            Assert.Equal(opcode, instruction.opcode);
            LocalVariableInfo local = Assert.IsAssignableFrom<LocalVariableInfo>(instruction.operand);
            Assert.Equal(type, local.LocalType);
        }
    }
}
