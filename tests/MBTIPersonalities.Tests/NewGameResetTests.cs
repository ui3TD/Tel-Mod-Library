using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;
using static MBTIPersonalities.Tests.TestGirls;

namespace MBTIPersonalities.Tests
{
    /// <summary>
    /// Starting a new game forgets the previous game's types. A new game restarts idol IDs at 0
    /// without running LoadFunction, so a kept type would go to the new idol with the same ID.
    /// The patch is applied to a stand-in, because the game's Reset needs the scene.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class NewGameResetTests
    {
        // Not "tests.MBTIPersonalities": PatchTargetTests unpatches everything under that ID
        private const string HarmonyId = "tests.MBTIPersonalities.NewGameReset";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GameReset() { }

        [Fact]
        public void PatchTargetsDataGirlsReset()
        {
            HarmonyPatch attribute = (HarmonyPatch)Attribute.GetCustomAttribute(typeof(data_girls_Reset), typeof(HarmonyPatch));
            Assert.Equal(typeof(data_girls), attribute.info.declaringType);
            Assert.Equal("Reset", attribute.info.methodName);
            Assert.NotNull(AccessTools.DeclaredMethod(typeof(data_girls), "Reset", Type.EmptyTypes));
        }

        [Fact]
        public void ResetForgetsCachedTypes()
        {
            data_girls.girls oldIdol = Make(MBTI.INTJ);
            Harmony harmony = new(HarmonyId);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(NewGameResetTests), nameof(GameReset)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(data_girls_Reset), "Postfix")));
                GameReset();
                Assert.False(MBTIReferenceDict.ContainsKey(oldIdol.id));
            }
            finally
            {
                harmony.UnpatchSelf();
                MBTIReferenceDict.Remove(oldIdol.id);
            }
        }

        /// <summary>
        /// After the reset, an idol reusing an old ID gets her own type and it's written to her save
        /// variables, so it stays the same after a save and reload.
        /// </summary>
        [Fact]
        public void NewIdolWithReusedIdGetsAndSavesHerOwnType()
        {
            data_girls.girls newIdol = new() { id = 100, firstName = "Yui", lastName = "Sato", birthday = new DateTime(2004, 7, 1) };
            MBTI own = GenerateMBTI(newIdol);
            MBTI stale = own == MBTI.INTJ ? MBTI.ENFP : MBTI.INTJ;
            MBTIReferenceDict[newIdol.id] = stale;
            try
            {
                ResetMBTI();
                Assert.Equal(own, GetGirlMBTI(newIdol));
                Assert.Contains(own.ToString(), newIdol.Variables);
            }
            finally
            {
                MBTIReferenceDict.Remove(newIdol.id);
            }
        }
    }
}
