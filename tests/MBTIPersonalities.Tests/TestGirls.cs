using System;
using Xunit;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities.Tests
{
    /// <summary>
    /// Runs every MBTI test class one at a time: several seed the game's static lists,
    /// and PatchTargetTests patches the game for real.
    /// </summary>
    [CollectionDefinition(Name)]
    public class MBTICollection
    {
        public const string Name = "MBTIPersonalities";
    }

    /// <summary>
    /// Makes idols with a known MBTI by seeding the mod's lookup, so the hash-based
    /// assignment never runs.
    /// </summary>
    public static class TestGirls
    {
        private static int nextId = 100000;

        public static data_girls.girls Make(MBTI mbti)
        {
            data_girls.girls girl = new() { id = nextId++ };
            MBTIReferenceDict[girl.id] = mbti;
            return girl;
        }

        /// <summary>
        /// Sets one of the mod's static flags for the duration of a test.
        /// </summary>
        public static void WithFlag(Action<bool> set, Action body)
        {
            set(true);
            try
            {
                body();
            }
            finally
            {
                set(false);
            }
        }
    }
}
