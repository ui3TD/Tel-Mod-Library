using TelModTests.Common;
using Xunit;

namespace TargetedAuditions.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "CustomAuditions";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(CustomAuditions.Auditions_GenerateGirls).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(CustomAuditions.Auditions_GenerateGirls).Assembly, ModNamespace);
        }
    }
}
