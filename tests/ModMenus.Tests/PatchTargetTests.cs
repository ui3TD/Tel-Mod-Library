using Xunit;
using TelModTests.Common;

namespace ModMenus.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "ModMenus";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(ModMenusUtils).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(ModMenusUtils).Assembly, ModNamespace);
        }
    }
}
