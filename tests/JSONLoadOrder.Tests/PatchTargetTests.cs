using TelModTests.Common;
using Xunit;

namespace JSONLoadOrder.Tests
{
    public class PatchTargetTests
    {
        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Language__Load).Assembly, TestGame.ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Language__Load).Assembly, TestGame.ModNamespace);
        }
    }
}
