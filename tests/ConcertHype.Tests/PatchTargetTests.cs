using TelModTests.Common;
using Xunit;

namespace ConcertHype.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "ConcertHype";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(ShowSession).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(ShowSession).Assembly, ModNamespace);
        }
    }
}
