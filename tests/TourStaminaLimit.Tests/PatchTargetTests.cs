using TelModTests.Common;
using TourStamina;
using Xunit;

namespace TourStaminaLimit.Tests
{
    public class PatchTargetTests
    {
        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(SEvent_Tour_tour_SelectCountry).Assembly, TestGame.ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(SEvent_Tour_tour_SelectCountry).Assembly, TestGame.ModNamespace);
        }
    }
}
