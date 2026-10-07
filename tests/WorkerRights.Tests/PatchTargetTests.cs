using TelModTests.Common;
using WorkerRights;
using Xunit;

namespace WorkerRightsTests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "WorkerRights";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(staff__staff_CanFire).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(staff__staff_CanFire).Assembly, ModNamespace);
        }
    }
}
