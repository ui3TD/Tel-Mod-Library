using System.Linq;

namespace AIFanChatter
{
    /// <summary>
    /// The agency's policies that fans would notice, in plain words. The game's policies cover every group.
    /// A policy left at the game's default gives "", so its line is left out, except the dating rule, which
    /// fans talk about even when the agency leaves it unclear. A value the game doesn't have (another
    /// mod's) also gives "".
    /// </summary>
    public static class AgencyRules
    {
        /// <summary>The vibe and image, as one line: "cute, rebellious image".</summary>
        public static string Concept()
        {
            string vibe = Selected(policies._type.vibe) switch
            {
                policies._value.vibe_cute => "cute",
                policies._value.vibe_cool => "cool",
                policies._value.vibe_sexy => "sexy",
                policies._value.vibe_pretty => "pretty",
                policies._value.vibe_funny => "funny",
                policies._value.vibe_smart => "smart",
                _ => "",
            };
            string image = Selected(policies._type.image) switch
            {
                policies._value.image_orthodox => "traditional idol image",
                policies._value.image_rebellious => "rebellious image",
                _ => "",
            };
            return string.Join(", ", new[] { vibe, image }.Where(part => part != "").ToArray());
        }

        public static string Dating() => Selected(policies._type.dating) switch
        {
            policies._value.dating_forbidden => "dating is forbidden",
            policies._value.dating_ambiguous => "unclear: dating is neither allowed nor forbidden",
            policies._value.dating_allowed => "dating is allowed",
            _ => "",
        };

        public static string Costumes() => Selected(policies._type.skirts) switch
        {
            policies._value.skirts_short => "short skirts without safety shorts",
            policies._value.skirts_long_skirts => "long skirts",
            policies._value.skirts_pants => "pants",
            _ => "",
        };

        public static string PerformanceStyle() => Selected(policies._type.performances) switch
        {
            policies._value.performances_energy => "energy and enthusiasm",
            policies._value.performances_quality => "quality and polish",
            _ => "",
        };

        /// <summary>Only for a single promoted with a handshake event, group or individual.</summary>
        public static string HandshakeSecurity(singles._single single)
        {
            if (single == null || !(single.IsGroupHS() || single.IsIndividualHS()))
            {
                return "";
            }
            return Selected(policies._type.security) switch
            {
                policies._value.security_relaxed => "relaxed",
                policies._value.security_restrictive => "restrictive",
                _ => "",
            };
        }

        private static policies._value Selected(policies._type type)
        {
            return policies.GetSelectedPolicyValue(type)?.Value ?? policies._value.NONE;
        }
    }
}
