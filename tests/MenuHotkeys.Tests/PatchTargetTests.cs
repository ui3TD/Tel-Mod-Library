using Xunit;
using TelModTests.Common;

namespace MenuHotkeys.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "MenuHotkeys";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Controls_Update).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Controls_Update).Assembly, ModNamespace);
        }
    }
}
