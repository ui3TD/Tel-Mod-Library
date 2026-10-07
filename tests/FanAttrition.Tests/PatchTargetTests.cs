using TelModTests.Common;
using Xunit;

namespace FanAttrition.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "FanAttrition";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Utility).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Utility).Assembly, ModNamespace);
        }
    }
}
