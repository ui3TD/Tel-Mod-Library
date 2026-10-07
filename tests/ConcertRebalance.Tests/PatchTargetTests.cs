using TelModTests.Common;
using Xunit;

namespace ConcertRebalance.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "ConcertRebalance";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(ConcertRebalance).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(ConcertRebalance).Assembly, ModNamespace);
        }
    }
}
