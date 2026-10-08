using PromotionTierTweaks;
using TelModTests.Common;
using Xunit;

namespace PromotionTierTweaks.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "PromotionTierTweaks";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Activities_GetMaxLevel_Promotion).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Activities_GetMaxLevel_Promotion).Assembly, ModNamespace);
        }
    }
}
