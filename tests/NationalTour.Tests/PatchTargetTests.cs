using TelModTests.Common;
using Xunit;

namespace NationalTour.Tests
{
    public class PatchTargetTests
    {
        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Utility).Assembly, TestGame.ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Utility).Assembly, TestGame.ModNamespace);
        }
    }
}
