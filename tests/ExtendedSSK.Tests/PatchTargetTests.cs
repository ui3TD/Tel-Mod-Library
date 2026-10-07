using TelModTests.Common;
using Xunit;

namespace ExtendedSSK.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "ExtendedSSK";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(ExtendedSSK).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(ExtendedSSK).Assembly, ModNamespace);
        }
    }
}
