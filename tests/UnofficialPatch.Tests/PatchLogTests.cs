using System.Linq;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The mod's warnings name the game method the patch is on, read from its [HarmonyPatch] attribute.
    /// </summary>
    public class PatchLogTests
    {
        public PatchLogTests() => TestGame.Reset();

        [Fact]
        public void Warning_NamesTheTarget()
        {
            PatchLog.Warn<Theaters_GetStaminaCost>("test");

            Assert.Equal("[UnofficialPatch] Theaters.GetStaminaCost: test", Log.Messages.Single().message);
        }

        [Fact]
        public void Warning_NamesTheArgumentTypes()
        {
            PatchLog.Warn<vn_requirements_CheckGirl>("test");

            Assert.Equal("[UnofficialPatch] vn_requirements.CheckGirl(girls, String, String): test", Log.Messages.Single().message);
        }

        [Fact]
        public void Warning_WithoutATarget_NamesThePatchClass()
        {
            PatchLog.Warn<PatchLogTests>("test");

            Assert.Equal("[UnofficialPatch] UnknownTarget (patch: PatchLogTests): test", Log.Messages.Single().message);
        }
    }
}
