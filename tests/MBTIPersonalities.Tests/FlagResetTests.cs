using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;
using static MBTIPersonalities.Tests.TestGirls;

namespace MBTIPersonalities.Tests
{
    /// <summary>
    /// The flags that switch the bonuses on are cleared even when the game's method throws, so a failed
    /// calculation can't leave a bonus applying everywhere. Each patch is applied to a stand-in method that
    /// throws, because the game's methods don't run outside the game.
    /// </summary>
    [Collection(MBTICollection.Name)]
    public class FlagResetTests
    {
        // Not "tests.MBTIPersonalities": PatchTargetTests unpatches everything under that ID
        private const string HarmonyId = "tests.MBTIPersonalities.FlagReset";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GameMethodThatThrows() => throw new InvalidOperationException("game method failed");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PopupSetThatThrows(business._proposal _proposal) => throw new InvalidOperationException("game method failed");

        private static void SetAllFlags()
        {
            patchGetVal = true;
            patchGetFan_Count_INFP = true;
            patchGetFan_Count_ENFJ = true;
            patchSetVal_INTJ = true;
            patchSet_INTJ = true;
            isShow = true;
            isRisky = true;
        }

        private static void ClearAllFlags()
        {
            patchGetVal = false;
            patchGetFan_Count_INFP = false;
            patchGetFan_Count_ENFJ = false;
            patchSetVal_INTJ = false;
            patchSet_INTJ = false;
            isShow = false;
            isRisky = false;
        }

        /// <summary>
        /// Patches the stand-in with the patch class's finalizer (and prefix, if given) and calls it.
        /// Returns the exception the caller saw.
        /// </summary>
        private static Exception RunPatched(Type patchClass, MethodInfo standIn, bool withPrefix, params object[] args)
        {
            Harmony harmony = new(HarmonyId);
            try
            {
                harmony.Patch(standIn,
                    prefix: withPrefix ? new HarmonyMethod(AccessTools.Method(patchClass, "Prefix")) : null,
                    finalizer: new HarmonyMethod(AccessTools.Method(patchClass, "Finalizer")));
                return Record.Exception(() =>
                {
                    try
                    {
                        standIn.Invoke(null, args);
                    }
                    catch (TargetInvocationException e)
                    {
                        throw e.InnerException;
                    }
                });
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        public static TheoryData<Type> FlagPatches => new()
        {
            typeof(singles_GenerateSales),
            typeof(Birthday_Popup_DoParam),
            typeof(SEvent_SSK__SSK_GenerateResults),
            typeof(Business__proposal_GetGirlCoeff),
            typeof(Data_girls_GetAverageParam),
            typeof(Shows__show_SenbatsuCalcParam),
            typeof(Singles__single_SenbatsuCalcParam),
            typeof(SEvent_Concerts__concert__song_GetSkillValue),
            typeof(SEvent_Concerts__concert__mc_GetSkillValue),
        };

        /// <summary>
        /// Every patch that sets a flag clears it in a finalizer, never a postfix (which is skipped on an exception).
        /// </summary>
        [Theory]
        [MemberData(nameof(FlagPatches))]
        public void FlagIsClearedInAFinalizer(Type patchClass)
        {
            Assert.NotNull(AccessTools.Method(patchClass, "Prefix"));
            Assert.NotNull(AccessTools.Method(patchClass, "Finalizer"));
            Assert.Null(AccessTools.Method(patchClass, "Postfix"));
        }

        [Theory]
        [MemberData(nameof(FlagPatches))]
        public void WhenTheGameThrows_FlagsAreCleared_AndTheErrorStillReachesTheGame(Type patchClass)
        {
            SetAllFlags();
            try
            {
                Exception thrown = RunPatched(patchClass, AccessTools.Method(typeof(FlagResetTests), nameof(GameMethodThatThrows)), withPrefix: false);

                Assert.IsType<InvalidOperationException>(thrown);
                if (patchClass == typeof(singles_GenerateSales))
                    Assert.False(patchGetFan_Count_INFP);
                else if (patchClass == typeof(Birthday_Popup_DoParam))
                    Assert.False(patchSetVal_INTJ || patchSet_INTJ);
                else if (patchClass == typeof(SEvent_SSK__SSK_GenerateResults))
                    Assert.False(patchGetFan_Count_ENFJ);
                else
                    Assert.False(patchGetVal);

                if (patchClass == typeof(Data_girls_GetAverageParam) || patchClass == typeof(Shows__show_SenbatsuCalcParam))
                    Assert.False(isShow);
                if (patchClass == typeof(Singles__single_SenbatsuCalcParam))
                    Assert.False(isRisky);
            }
            finally
            {
                ClearAllFlags();
            }
        }

        /// <summary>
        /// The proposal popup's stamina discount is undone even if the popup fails, so accepting the proposal
        /// doesn't discount it a second time.
        /// </summary>
        [Fact]
        public void WhenThePopupThrows_ENFPStaminaIsRestored()
        {
            business._proposal proposal = new() { girl = Make(MBTI.ENFP), stamina = 20 };

            Exception thrown = RunPatched(typeof(Business_Popup_Set), AccessTools.Method(typeof(FlagResetTests), nameof(PopupSetThatThrows)), withPrefix: true, proposal);

            Assert.IsType<InvalidOperationException>(thrown);
            Assert.Equal(20, proposal.stamina);
        }

        /// <summary>
        /// When the game's method succeeds, the flags are cleared just as before.
        /// </summary>
        [Fact]
        public void WhenTheGameSucceeds_FlagsAreCleared()
        {
            WithFlag(on => patchGetVal = on, () =>
            {
                Business__proposal_GetGirlCoeff.Finalizer();
                Assert.False(patchGetVal);
            });
        }
    }
}
