using System.Linq;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// Where each key works, so two rows with the same key are marked only if both can work at once.
    /// </summary>
    /// <remarks>
    /// "normal" and "debug" are the agency screen with debug mode off and on; "dialogue" is a dialogue showing;
    /// "profile" and "credits" are those screens. Popups and dialogues block the agency keys, and debug mode swaps
    /// the activity keys for the audition cheats, so, say, Space pausing doesn't clash with Space in a dialogue.
    /// </remarks>
    public static class Contexts
    {
        /// <summary>
        /// Other mods' keys: mods read keys on the agency screen, in either mode.
        /// </summary>
        public static readonly string[] OtherMods = { "normal", "debug" };

        public static string[] Of(string varID)
        {
            return varID != null && Rows.ByVarID.TryGetValue(varID, out Row row) ? row.Contexts : OtherMods;
        }

        /// <summary>
        /// Whether two key rows clash when they have the same key: they work in a place in common.
        /// </summary>
        public static bool Clash(string a, string b)
        {
            return Of(a).Intersect(Of(b)).Any();
        }
    }
}
