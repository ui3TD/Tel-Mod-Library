using TelModTests.Common;
using TraitsExpansion;
using Xunit;

namespace TraitsExpansionTests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "TraitsExpansion";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(traits_GetTraitType).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(traits_GetTraitType).Assembly, ModNamespace);
        }
    }
}
