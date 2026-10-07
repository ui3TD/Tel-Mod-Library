using TelModTests.Common;
using Xunit;

namespace MBTIPersonalities.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "MBTIPersonalities";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(SEvent_Concerts__concert_AccidentSuccessChance).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(SEvent_Concerts__concert_AccidentSuccessChance).Assembly, ModNamespace);
        }
    }
}
