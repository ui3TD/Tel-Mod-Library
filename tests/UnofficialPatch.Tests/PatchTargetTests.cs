using System.Linq;
using TelModTests.Common;
using Xunit;

namespace UnofficialPatch.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "UnofficialPatch";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Theaters_CompleteDay).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Theaters_CompleteDay).Assembly, ModNamespace);
        }

        [Fact]
        public void PatchesApplyWithoutWarnings()
        {
            // A transpiler that can't find its target logs a warning and leaves the game unchanged
            TestGame.Reset();
            Assert.Empty(TestGame.PatchingLog);
        }

        [Fact]
        public void OnlyPatchesOnNativeCodeAreLeftToDirectTests()
        {
            // These targets call Unity's native code, so they can't be compiled outside the game.
            // Their tests call the patch methods directly (or check the transpiled code).
            TestGame.Reset();
            Assert.Equal(
                new[]
                {
                    nameof(data_girls_textures_SetSprite),
                    nameof(Popup_Audition_Close),
                    nameof(Popup_Audition_LoadCards),
                    nameof(Popup_Audition_OpenCard),
                    nameof(Popup_Audition_PortraitsLoaded),
                    nameof(SaveManager_SaveData),
                    nameof(vn_requirements_CheckGirl),
                    nameof(vn_requirements_CheckGirl_Variable),
                },
                TestGame.NotApplied.Select(t => t.Name).OrderBy(n => n));
        }
    }
}
