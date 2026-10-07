using Xunit;
using TelModTests.Common;

namespace FastForward.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "FastForward";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(FastForward).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(FastForward).Assembly, ModNamespace);
        }
    }
}
