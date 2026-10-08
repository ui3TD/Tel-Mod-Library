using TelModTests.Common;
using Xunit;

namespace StarSigns.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "StarSigns";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Relationships_Do_Dynamic).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Relationships_Do_Dynamic).Assembly, ModNamespace);
        }
    }
}
