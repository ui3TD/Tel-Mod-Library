using TelModTests.Common;
using Xunit;

namespace GrowingDistant.Tests
{
    [Collection(WeeklyDecayTests.Collection)]
    public class PatchTargetTests
    {
        private const string ModNamespace = "GrowingDistant";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(data_girls_girls_UpdateRelationshipBasedOnSalary).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(data_girls_girls_UpdateRelationshipBasedOnSalary).Assembly, ModNamespace);
        }
    }
}
