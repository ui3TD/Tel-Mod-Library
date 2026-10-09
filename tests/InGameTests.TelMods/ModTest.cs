using System.Reflection;

namespace InGameTests.TelMods
{
    /// <summary>
    /// What the per-mod checks in Mods/ share: the suite name and whether a Tel mod is live. Game
    /// helpers come from the runner (Game, TestTools); mod types are reached by reflection, since
    /// this project doesn't reference the mods and runs with any of them off.
    /// </summary>
    internal static class ModTest
    {
        public const string Suite = "mods";

        /// <summary>
        /// The mod's loaded assembly when its patches are live; otherwise notes the skip and
        /// returns false. Fails when no mod project in this checkout has the HarmonyID, which
        /// catches a mistyped ID. Every per-mod check starts with this.
        /// </summary>
        public static bool Require(TestContext ctx, string harmonyId, out Assembly assembly)
        {
            assembly = null;
            TelMod mod = TelMod.ByHarmonyId(harmonyId);
            if (mod == null)
            {
                ctx.Fail("No mod project in this checkout has HarmonyID " + harmonyId);
                return false;
            }
            if (!mod.IsEnabled() || (assembly = mod.Assembly()) == null)
            {
                ctx.Note(mod.Name + " isn't enabled; skipped");
                return false;
            }
            return true;
        }

        /// <summary>Whether another Tel mod's patches are live, for checks whose expected value depends on it.</summary>
        public static bool IsActive(string harmonyId)
        {
            TelMod mod = TelMod.ByHarmonyId(harmonyId);
            return mod != null && mod.IsEnabled() && mod.Assembly() != null;
        }
    }
}
