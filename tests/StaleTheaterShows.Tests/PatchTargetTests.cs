using StaleTheater;
using TelModTests.Common;
using Xunit;

namespace StaleTheaterShows.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "StaleTheater";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Theaters__theater_GetSubRevenue).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Theaters__theater_GetSubRevenue).Assembly, ModNamespace);
        }
    }
}
