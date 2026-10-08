using TelModTests.Common;
using Xunit;

namespace EffortlessTraining.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "EffortlessTraining";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(agency__room_DoGirlTraining).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(agency__room_DoGirlTraining).Assembly, ModNamespace);
        }
    }
}
