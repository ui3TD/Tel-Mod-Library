using TelModTests.Common;
using Xunit;

namespace NeverGraduate.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "NeverGraduate";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(data_girls_UpdateGraduationDates).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(data_girls_UpdateGraduationDates).Assembly, ModNamespace);
        }
    }
}
