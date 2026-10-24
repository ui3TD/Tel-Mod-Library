namespace TraitsExpansion
{
    public class TraitsExpansion
    {
        public const int PERFPITCH_BONUS = 50;
        public const int BEAUTYGURU_BONUS = 30;
        public const int MENSA_BONUS = 50;
        public const int WELLENDOWED_BONUS = 30;
        public const int HOMELY_PENALTY = 10;
        public const int TONEDEAF_PENALTY = 30;

        public const float WOODACTING_PENALTY = 0.2f;
        public const float QUICKWIT_BONUS = 0.5f;
        public const float RECKLESS_THR_UPPER = 60f;
        public const float RECKLESS_THR_LOWER = 5f;
        public const float RECKLESS_CHANCE = 1f;
        public const float RECKLESS_CHANCE_SEVERE = 2f;
        public const float THESPIAN_COEFF = 0.5f;
        public const float CULT_COEFF = 1.5f;
        public const float STAGEFRIGHT_PENALTY = 10;
        public const float AEROPHOB_PENALTY = 30;
        public const int JOBHOPPER_DAYS_LOWER = 100;
        public const int JOBHOPPER_DAYS_UPPER = 365;
        public const int SADISTIC_MODIFIER = 10;
        public const float FASHIONISTA_COEFF = 1.2f;
        public const float FLIRTY_COEFF = 1.2f;
        public const float OTAKU_COEFF = 1.2f;
        public const float POLYGLOT_BONUS = 0.2f;


        // Set while the game works out a show, single, concert or proposal from idols' stats. The game
        // never calls those methods inside each other, but another mod might, so each finalizer puts back
        // the value its prefix found.
        public static bool patchGetVal = false;
        public static bool patchAddParam = false;
        public static bool patchGetFan_Count = false;

        // This method calculates the modifier to girl parameters based on their trait.
        public static int GetTraitModifier(data_girls.girls girls, data_girls._paramType type)
        {
            int num = 0;
            if (girls == null || !data_girls.IsStatParam(type))
                return num;

            switch ((NewTraits)girls.trait)
            {
                case NewTraits.Perfect_Pitch:
                    if (type == data_girls._paramType.vocal)
                        num += PERFPITCH_BONUS;
                    break;

                case NewTraits.Beauty_Guru:
                    if (type == data_girls._paramType.pretty)
                        num += BEAUTYGURU_BONUS;
                    break;

                case NewTraits.Mensa_Member:
                    if (type == data_girls._paramType.smart)
                        num += MENSA_BONUS;
                    break;

                case NewTraits.Well_Endowed:
                    if (type == data_girls._paramType.sexy)
                        num += WELLENDOWED_BONUS;
                    break;

                case NewTraits.Homely:
                    if (type == data_girls._paramType.pretty ||
                        type == data_girls._paramType.cute ||
                        type == data_girls._paramType.cool ||
                        type == data_girls._paramType.sexy)
                        num -= HOMELY_PENALTY;
                    break;

                case NewTraits.Tone_Deaf:
                    if (type == data_girls._paramType.vocal)
                        num -= TONEDEAF_PENALTY;
                    break;
            }

            return num;
        }

        public enum NewTraits
        {
            none = 0,
            Perfect_Pitch = 2801,
            Polyglot = 2802,
            Old_Money = 2803,
            Beauty_Guru = 2804,
            Mensa_Member = 2805,
            Quick_Wit = 2806,
            Fashionista = 2807,
            Flirty = 2808,
            Well_Endowed = 2809,
            Idol_Otaku = 2810,
            Wooden_Acting = 2811,
            Sadistic = 2812,
            Reckless = 2813,
            Job_Hopper = 2814,
            Aerophobia = 2815,
            Stage_Fright = 2816,
            Cult_Leader = 2817,
            Tone_Deaf = 2818,
            Homely = 2819,
            Thespian = 2820
        }
    }
}
