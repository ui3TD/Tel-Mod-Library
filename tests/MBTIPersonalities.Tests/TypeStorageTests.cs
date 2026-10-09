using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities.Tests
{
    /// <summary>
    /// Records what the mod logs, for the length of a test.
    /// </summary>
    public sealed class LogRecorder : ILogHandler, IDisposable
    {
        private readonly ILogHandler gameLog = Debug.unityLogger.logHandler;
        public readonly List<string> Messages = new();

        public LogRecorder() => Debug.unityLogger.logHandler = this;

        public void Dispose() => Debug.unityLogger.logHandler = gameLog;

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) =>
            Messages.Add(string.Format(format, args));

        public void LogException(Exception exception, UnityEngine.Object context) => Messages.Add(exception.ToString());
    }

    /// <summary>
    /// Each idol carries one type in her save variables, next to the game's own variables.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class TypeVariableTests : IDisposable
    {
        private static int nextId = 200000;
        private readonly List<data_girls.girls> gameGirls = data_girls.girl;

        public TypeVariableTests() => data_girls.girl = new List<data_girls.girls>();

        public void Dispose()
        {
            foreach (data_girls.girls girl in data_girls.girl)
                MBTIReferenceDict.Remove(girl.id);
            data_girls.girl = gameGirls;
        }

        /// <summary>
        /// An idol in the save with these variables.
        /// </summary>
        private static data_girls.girls Saved(params string[] variables)
        {
            data_girls.girls girl = new() { id = nextId++, firstName = "Yui", lastName = "Sato", birthday = new DateTime(2004, 7, 1), Variables = variables.ToList() };
            data_girls.girl.Add(girl);
            return girl;
        }

        private static void Load() => data_girls_LoadFunction.Postfix();

        [Fact]
        public void ChangingTheType_KeepsOnlyTheNewOne()
        {
            data_girls.girls girl = Saved();

            SetGirlMBTI(girl, MBTI.INTJ);
            SetGirlMBTI(girl, MBTI.ENFP);

            Assert.Equal(new[] { "ENFP" }, girl.Variables);
            Assert.Equal(MBTI.ENFP, GetGirlMBTI(girl));
        }

        [Fact]
        public void SettingTheType_LeavesTheGamesVariables()
        {
            data_girls.girls girl = Saved("met_president", "INTJ", "festival_done");

            SetGirlMBTI(girl, MBTI.ENFP);

            Assert.Equal(new[] { "met_president", "festival_done", "ENFP" }, girl.Variables);
        }

        [Fact]
        public void Load_ReadsTheSavedType_AnyCase()
        {
            data_girls.girls girl = Saved("met_president", "intj");

            Load();

            Assert.Equal(MBTI.INTJ, GetGirlMBTI(girl));
        }

        /// <summary>
        /// A save from an older version can hold two types. The last one wins, as before, and the other is
        /// removed, so the save is right again from then on.
        /// </summary>
        [Fact]
        public void Load_TwoSavedTypes_LastWins_AndTheOtherIsRemoved()
        {
            data_girls.girls girl = Saved("INTJ", "met_president", "ENFP");

            Load();

            Assert.Equal(MBTI.ENFP, GetGirlMBTI(girl));
            Assert.Equal(new[] { "met_president", "ENFP" }, girl.Variables);
        }

        /// <summary>
        /// Only type names count: numbers and lists, which Enum.TryParse would accept, are left alone and
        /// the idol gets her usual type.
        /// </summary>
        [Theory]
        [InlineData("3")]
        [InlineData("ISTJ, ENFP")]
        [InlineData("None")]
        public void Load_NotATypeName_IsNotAType(string variable)
        {
            data_girls.girls girl = Saved(variable);

            Load();

            Assert.False(MBTIReferenceDict.ContainsKey(girl.id));
            Assert.Equal(GenerateMBTI(girl), GetGirlMBTI(girl));
        }

        /// <summary>
        /// Older versions wrote "None" for a unique idol whose "mbti" couldn't be read. Once she has a type,
        /// it's gone.
        /// </summary>
        [Fact]
        public void OldNoneVariable_IsReplacedByHerType()
        {
            data_girls.girls girl = Saved("None");

            MBTI type = GetGirlMBTI(girl);

            Assert.Equal(new[] { type.ToString() }, girl.Variables);
        }
    }

    /// <summary>
    /// A unique idol's params.json can set her type with "mbti". The mod records it when the game loads
    /// the idol's portraits, and gives it to her when she's generated.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class UniqueIdolTypeTests : IDisposable
    {
        public UniqueIdolTypeTests() => UniqueIdolTypes.Clear();

        public void Dispose() => UniqueIdolTypes.Clear();

        private static void Read(string paramsJson, string modName = "Idol Mod", int bodyId = 7) =>
            data_girls_textures_LoadAssetsData.Infix(JSON.Parse(paramsJson), new data_girls_textures._textureAsset { ModName = modName, body_id = bodyId });

        [Fact]
        public void Type_IsRecordedForThatBody_AnyCase()
        {
            Read("{ \"mbti\": \"intj\" }");

            KeyValuePair<string, MBTI> entry = Assert.Single(UniqueIdolTypes);
            Assert.Equal(UniqueIdolKey("Idol Mod", 7), entry.Key);
            Assert.Equal(MBTI.INTJ, entry.Value);
        }

        [Fact]
        public void NoMbti_RecordsNothing()
        {
            Read("{ \"left\": 10 }");
            Assert.Empty(UniqueIdolTypes);
        }

        /// <summary>
        /// A misspelled type is skipped with a warning, and the idol gets her usual type. It used to record
        /// "no type".
        /// </summary>
        [Theory]
        [InlineData("INTF")]
        [InlineData("None")]
        [InlineData("3")]
        public void UnknownType_IsSkippedWithAWarning(string mbti)
        {
            using LogRecorder log = new();

            Read("{ \"mbti\": \"" + mbti + "\" }");

            Assert.Empty(UniqueIdolTypes);
            Assert.Contains("[MBTI Personalities] MBTI type not found: " + mbti, log.Messages);
        }

        /// <summary>
        /// The game reads mods' portraits again on every scene load; the idol keeps one entry.
        /// </summary>
        [Fact]
        public void ReadAgain_KeepsOneEntryWithTheLatestType()
        {
            Read("{ \"mbti\": \"INTJ\" }");
            Read("{ \"mbti\": \"ENFP\" }");

            Assert.Equal(MBTI.ENFP, Assert.Single(UniqueIdolTypes).Value);
        }

        [Fact]
        public void SameBodyInTwoMods_RecordedApart()
        {
            Read("{ \"mbti\": \"INTJ\" }", modName: "Mod A");
            Read("{ \"mbti\": \"ENFP\" }", modName: "Mod B");

            Assert.Equal(MBTI.INTJ, UniqueIdolTypes[UniqueIdolKey("Mod A", 7)]);
            Assert.Equal(MBTI.ENFP, UniqueIdolTypes[UniqueIdolKey("Mod B", 7)]);
        }

        [Fact]
        public void GeneratedUniqueIdol_GetsHerType()
        {
            Read("{ \"mbti\": \"INTJ\" }");
            data_girls.girls girl = new()
            {
                id = 300000,
                textureAssets = new List<data_girls.girls._textureAsset> { new() { asset = new() { ModName = "Idol Mod", body_id = 7 } } },
            };
            try
            {
                data_girls_GenerateGirl.Postfix(ref girl, true);

                Assert.Equal(MBTI.INTJ, GetGirlMBTI(girl));
                Assert.Equal(new[] { "INTJ" }, girl.Variables);
            }
            finally
            {
                MBTIReferenceDict.Remove(girl.id);
            }
        }

        [Fact]
        public void GeneratedIdolWithoutTextures_Unchanged()
        {
            Read("{ \"mbti\": \"INTJ\" }");
            data_girls.girls girl = new() { id = 300001, textureAssets = new List<data_girls.girls._textureAsset>() };

            data_girls_GenerateGirl.Postfix(ref girl, true);

            Assert.False(MBTIReferenceDict.ContainsKey(girl.id));
        }

        /// <summary>
        /// The mod reads each params.json right after the game parses it, while the game's body asset for
        /// that folder is still in hand.
        /// </summary>
        [Fact]
        public void Transpiler_PassesTheParsedJsonAndBodyAsset()
        {
            MethodInfo original = AccessTools.Method(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) });
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(original);

            List<CodeInstruction> patched = data_girls_textures_LoadAssetsData.Transpiler(instructions).ToList();

            MethodInfo infix = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.Infix));
            Assert.Equal(instructions.Count + 3, patched.Count);
            int call = patched.FindIndex(i => i.Calls(infix));
            Assert.True(call >= 3, "Infix call not inserted");

            Assert.True(patched[call - 3].IsStloc());
            LocalVariableInfo json = Assert.IsAssignableFrom<LocalVariableInfo>(patched[call - 3].operand);
            Assert.Equal(typeof(JSONNode), json.LocalType);
            Assert.Same(json, patched[call - 2].operand);
            LocalVariableInfo asset = Assert.IsAssignableFrom<LocalVariableInfo>(patched[call - 1].operand);
            Assert.Equal(typeof(data_girls_textures._textureAsset), asset.LocalType);
            Assert.Equal(OpCodes.Ldloc_S, patched[call - 1].opcode);
        }

        /// <summary>
        /// If a game update changes how params.json is read, the game's code is left as it is and the mod
        /// says why "mbti" stopped working, instead of failing to load.
        /// </summary>
        [Fact]
        public void Transpiler_GameCodeChanged_LeavesItAndLogs()
        {
            using LogRecorder log = new();
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(data_girls_textures), "LoadDefaultAssets"));

            List<CodeInstruction> patched = data_girls_textures_LoadAssetsData.Transpiler(instructions).ToList();

            Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));
            Assert.Contains(log.Messages, m => m.StartsWith("[MBTI Personalities] Couldn't find"));
        }
    }

    /// <summary>
    /// A type's text missing from the game's language table (a partial translation, or the mod's
    /// constants not loaded) shows the key instead of breaking the tooltip.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class MissingTextTests
    {
        [Fact]
        public void LoadedText_IsUsed()
        {
            Language.Data["MBTI__TEST_KEY"] = "Loaded";
            try
            {
                Assert.Equal("Loaded", Text("MBTI__TEST_KEY"));
            }
            finally
            {
                Language.Data.Remove("MBTI__TEST_KEY");
            }
        }

        [Fact]
        public void IdolTooltip_MissingText_ShowsTheKeys()
        {
            data_girls.girls girl = TestGirls.Make(MBTI.INTJ);
            Language.Data.Remove(constantTitlePrefix + "INTJ");
            Language.Data.Remove(constantDescPrefix + "INTJ");
            string tooltip = "Tooltip";

            data_girls_girls_GetTooltipText.Postfix(girl, ref tooltip);

            Assert.Equal("Tooltip\nMBTI__TITLE_INTJ: MBTI__DESC_INTJ", tooltip);
        }
    }
}
