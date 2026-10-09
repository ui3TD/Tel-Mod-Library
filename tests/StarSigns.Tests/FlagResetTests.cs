using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using static StarSigns.StarSigns;

namespace StarSigns.Tests
{
    /// <summary>
    /// The flags that switch the Leo and Aquarius bonuses on are cleared even when the game's method
    /// throws, so a failed leader pick or push update can't leave a bonus applying everywhere. Each patch
    /// is applied to a stand-in method that throws, because the game's methods don't run outside the game.
    /// </summary>
    public class FlagResetTests
    {
        // Not "tests.StarSigns": PatchTargetTests unpatches everything under that ID
        private const string HarmonyId = "tests.StarSigns.FlagReset";

        public FlagResetTests() => TestGame.Reset();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void GameMethodThatThrows() => throw new InvalidOperationException("game method failed");

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void GameMethodThatReadsTheFlags() => FlagsSeenInside = (patchGetVal, patchAddRelationship);

        private static (bool, bool) FlagsSeenInside;

        /// <summary>
        /// Patches the stand-in with the patch class's prefix and finalizer and calls it. Returns the
        /// exception the caller saw.
        /// </summary>
        private static Exception RunPatched(Type patchClass, string standIn)
        {
            Harmony harmony = new(HarmonyId);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(FlagResetTests), standIn),
                    prefix: new HarmonyMethod(AccessTools.Method(patchClass, "Prefix")),
                    finalizer: new HarmonyMethod(AccessTools.Method(patchClass, "Finalizer")));
                return Record.Exception(() =>
                {
                    try
                    {
                        AccessTools.Method(typeof(FlagResetTests), standIn).Invoke(null, null);
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
            typeof(Relationships__clique_UpdateLeader),
            typeof(Pushes_OnNewDay),
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
        public void WhenTheGameThrows_FlagIsCleared_AndTheErrorStillReachesTheGame(Type patchClass)
        {
            Exception thrown = RunPatched(patchClass, nameof(GameMethodThatThrows));

            Assert.IsType<InvalidOperationException>(thrown);
            Assert.False(patchGetVal);
            Assert.False(patchAddRelationship);
        }

        [Fact]
        public void LeaderPick_FlagIsOnOnlyInside()
        {
            Assert.Null(RunPatched(typeof(Relationships__clique_UpdateLeader), nameof(GameMethodThatReadsTheFlags)));

            Assert.Equal((true, false), FlagsSeenInside);
            Assert.False(patchGetVal);
        }

        [Fact]
        public void PushUpdate_FlagIsOnOnlyInside()
        {
            Assert.Null(RunPatched(typeof(Pushes_OnNewDay), nameof(GameMethodThatReadsTheFlags)));

            Assert.Equal((false, true), FlagsSeenInside);
            Assert.False(patchAddRelationship);
        }
    }
}
