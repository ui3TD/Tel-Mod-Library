using TelModTests.Common;
using TraitFix;
using Xunit;

namespace TraitsFixTests
{
    public class PatchTargetTests
    {
        [Theory]
        [InlineData("TraitFix")]
        [InlineData("StatLimits")]
        public void AllPatchTargetsExistInGame(string ns)
        {
            PatchTargetAssert.TargetsResolve(typeof(TraitsFix).Assembly, ns);
        }

        [Theory]
        [InlineData("TraitFix")]
        [InlineData("StatLimits")]
        public void AllPatchesApplyToGame(string ns)
        {
            PatchTargetAssert.PatchesApply(typeof(TraitsFix).Assembly, ns);
        }
    }
}
