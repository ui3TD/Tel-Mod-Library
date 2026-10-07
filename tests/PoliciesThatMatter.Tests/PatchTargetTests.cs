using TelModTests.Common;
using Xunit;

namespace PoliciesThatMatter.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "PoliciesThatMatter";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(policies_Load_RemoveDuplicateValues).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(policies_Load_RemoveDuplicateValues).Assembly, ModNamespace);
        }
    }
}
