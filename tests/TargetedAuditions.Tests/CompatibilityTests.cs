using CustomAuditions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using static CustomAuditions.CustomAuditions;
using GirlType = Auditions.data._girl._type;

namespace TargetedAuditions.Tests
{
    /// <summary>
    /// The mod next to unknown mods that patch the same methods: they keep their own values, a mod that
    /// skips the game's method skips the mod's changes, and a failure leaves the game's audition.
    /// </summary>
    public class CompatibilityTests : IDisposable
    {
        private readonly Harmony otherMod = new("tests.TargetedAuditions.OtherMod");

        private static readonly MethodInfo GenerateParams = AccessTools.Method(typeof(data_girls), "GenerateParams");

        public CompatibilityTests()
        {
            Seams.Reset();
        }

        public void Dispose()
        {
            otherMod.UnpatchSelf();
        }

        private static Auditions NewAuditions(int numberOfGirls = GAME_DEF_COUNT)
        {
            Auditions auditions = Seams.Component<Auditions>();
            auditions.NumberOfGirls = numberOfGirls;
            return auditions;
        }

        private static bool StartAudition(Auditions auditions, bool runOriginal = true)
        {
            Auditions_GenerateGirls.Prefix(auditions, runOriginal, out bool state);
            return state;
        }

        [Fact]
        public void Count_ReplacesTheModsOwnLastValue()
        {
            Auditions auditions = NewAuditions();
            Seams.SetVariable(VARID_COUNT, "25");
            StartAudition(auditions);
            Auditions_GenerateGirls.Finalizer(null, true);

            Seams.SetVariable(VARID_COUNT, "10");
            StartAudition(auditions);

            Assert.Equal(10, auditions.NumberOfGirls);
            Assert.Empty(Seams.Errors);
        }

        [Fact]
        public void Count_KeepsAnotherModsValue_AndLogsOnce()
        {
            Auditions auditions = NewAuditions(numberOfGirls: 12);
            Seams.SetVariable(VARID_COUNT, "25");

            Assert.True(StartAudition(auditions));
            Auditions_GenerateGirls.Finalizer(null, true);
            StartAudition(auditions);

            Assert.Equal(12, auditions.NumberOfGirls);
            Assert.Single(Seams.Errors);
            Assert.Contains("12", Seams.Errors[0]);
        }

        [Fact]
        public void UnreadableSettings_GiveTheGamesAudition()
        {
            Auditions auditions = NewAuditions();
            List<variables._variable> saved = variables.variable;
            variables.variable = null;
            try
            {
                Assert.False(StartAudition(auditions));
            }
            finally
            {
                variables.variable = saved;
            }

            Assert.False(IsGeneratingAudition);
            Assert.Equal(GAME_DEF_COUNT, auditions.NumberOfGirls);
            Assert.Single(Seams.Errors);
            Assert.Null(Auditions_GenerateGirls.Finalizer(null, false));
        }

        [Fact]
        public void SkippedGenerateGirls_ChangesNothing()
        {
            Auditions auditions = NewAuditions();
            Seams.SetVariable(VARID_COUNT, "25");

            Assert.False(StartAudition(auditions, runOriginal: false));

            Assert.False(IsGeneratingAudition);
            Assert.Equal(GAME_DEF_COUNT, auditions.NumberOfGirls);
        }

        [Fact]
        public void SkippedGenerateGirl_ChangesNothing()
        {
            BeginAuditionGeneration();
            chanceLesbian = 100;
            Seams.Rolls();

            // Sexuality
            data_girls.girls girl = Seams.NewGirl();
            girl.sexuality = data_girls.girls._sexuality.straight;
            data_girls_GenerateGirl.Postfix(ref girl, __runOriginal: false);
            Assert.Equal(data_girls.girls._sexuality.straight, girl.sexuality);

            // Body pool: with nothing to pick from, the mod would free the used bodies
            Auditions.UsedBodyIDs.Add(1);
            data_girls_GenerateGirl.Prefix(genTextures: true, BodyAsset: null, __runOriginal: false);
            Assert.Equal(new[] { 1 }, Auditions.UsedBodyIDs);
        }

        [Fact]
        public void SkippedGenerateBirthday_KeepsTheBirthday()
        {
            BeginAuditionGeneration();
            Seams.Rolls();
            data_girls.girls girl = Seams.NewGirl();
            DateTime birthday = new(1990, 5, 6);
            girl.SetBirthday(birthday);

            data_girls_girls_GenerateBirthday.Postfix(ref girl, __runOriginal: false);

            Assert.Equal(birthday, girl.birthday);
        }

        [Fact]
        public void SkippedGenerateParams_KeepsTheStats()
        {
            BeginAuditionGeneration();
            priorityDict[data_girls._paramType.vocal] = 1_000_000;
            Seams.Rolls();
            data_girls.girls girl = GirlWithStats();
            float[] before = Values(girl);

            data_girls_GenerateParams.Postfix(girl, __runOriginal: false);

            Assert.Equal(before, Values(girl));
        }

        [Fact]
        public void FailedPriorities_KeepTheGamesStats_AndLogOnce()
        {
            BeginAuditionGeneration();
            priorityDict.Clear();
            data_girls.girls girl = GirlWithStats();
            float[] before = Values(girl);

            data_girls_GenerateParams.Postfix(girl, __runOriginal: true);
            data_girls_GenerateParams.Postfix(girl, __runOriginal: true);

            Assert.Equal(before, Values(girl));
            Assert.Single(Seams.Errors);
        }

        /// <summary>
        /// The postfixes run first, in place of the game's roll, so later postfixes see the mod's values
        /// and can change them.
        /// </summary>
        [Theory]
        [InlineData(typeof(data_girls_GenerateGirl))]
        [InlineData(typeof(data_girls_GenerateParams))]
        [InlineData(typeof(data_girls_girls_GenerateBirthday))]
        public void Postfixes_RunFirst(Type patch)
        {
            HarmonyPriority priority = AccessTools.Method(patch, "Postfix").GetCustomAttribute<HarmonyPriority>();
            Assert.NotNull(priority);
            Assert.Equal(Priority.First, priority.info.priority);
        }

        private static float[] seenByOtherMod;

        private static void OtherModPostfix(data_girls.girls Girl)
        {
            seenByOtherMod = Values(Girl);
            Girl.setParam(data_girls._paramType.cute, 7);
        }

        /// <summary>
        /// Another mod's GenerateParams postfix, even one that asks to run early, sees the mod's
        /// reassigned stats, and its own change to a skill is kept.
        /// </summary>
        [Fact]
        public void OtherModsStatPostfix_SeesThePriorities_AndKeepsItsChange()
        {
            otherMod.Patch(GenerateParams,
                postfix: new HarmonyMethod(typeof(CompatibilityTests), nameof(OtherModPostfix)) { priority = Priority.High });
            BeginAuditionGeneration();
            foreach (data_girls._paramType p in paramTypes)
                priorityDict[p] = 0;
            priorityDict[data_girls._paramType.dance] = 1;
            Seams.Points = _ => 600;
            int dance = paramTypes.IndexOf(data_girls._paramType.dance);

            for (int seed = 0; seed < 20; seed++)
            {
                Seams.Seeded(seed);
                data_girls.girls girl = Seams.NewGirl();

                GenerateParams.Invoke(Seams.Component<data_girls>(), new object[] { girl, GirlType.golden });

                Assert.Equal(seenByOtherMod.Max(), seenByOtherMod[dance]);
                Assert.Equal(7, girl.getParam(data_girls._paramType.cute).val);
            }
        }

        private static data_girls.girls GirlWithStats()
        {
            data_girls.girls girl = Seams.NewGirl();
            for (int i = 0; i < paramTypes.Count; i++)
                girl.setParam(paramTypes[i], 10 * (i + 1));
            return girl;
        }

        private static float[] Values(data_girls.girls girl) => paramTypes.Select(p => girl.getParam(p).val).ToArray();
    }
}
