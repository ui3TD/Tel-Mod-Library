using System.Collections.Generic;
using System;
using System.Linq;

namespace MBTIPersonalities
{
    /// <summary>
    /// Handles MBTI personalities and their effects on gameplay mechanics for idols.
    /// </summary>
    public class MBTIPersonalities
    {
        public const string MBTINodeID = "mbti";
        public const string constantTitlePrefix = "MBTI__TITLE_";
        public const string constantDescPrefix = "MBTI__DESC_";

        // Constants defining bonuses for each MBTI type
        public const float ENFPBonus = 0.1f;
        public const float INTJBonus = 1;
        public const float ISTPBonus = 0.5f;
        public const int ISTJBonus = 10;
        public const float ISFJBonus = 0.1f;
        public const float ISFPBonus = 0.1f;
        public const float INFPBonus = 0.1f;
        public const float INTPBonus = 0.5f;
        public const float ESTJBonus = 0.2f;
        public const float ESFPBonus = 0.05f;
        public const float ENFJBonus = 0.2f;
        public const float ESFJBonus = 5;

        // Stat bonuses for specific MBTI types
        public const float ENTJStatBonus = 5;
        public const float ENTPStatBonus = 5;
        public const float ESTPStatBonus = 5;
        public const float INFJStatBonus = 5;

        // Toggle flags for patching specific game behaviors. Prefixes set them and finalizers clear them,
        // so they're cleared even when the game's method throws.
        public static bool patchGetVal = false;
        public static bool patchAddParam = false;
        public static bool patchGetFan_Count_INFP = false;
        public static bool patchGetFan_Count_ENFJ = false;
        public static bool patchSetVal_INTJ = false;
        public static bool patchSet_INTJ = false;

        // Other patch condition flags
        public static bool isRisky = false;
        public static bool isShow = false;

        /// <summary>
        /// The INTJ birthday flags as the birthday prefix found them, for its finalizer to put back.
        /// </summary>
        public struct IntjFlags
        {
            public bool SetVal;
            public bool Set;
        }

        /// <summary>
        /// The stat-calculation flags as a prefix found them, for its finalizer to put back.
        /// </summary>
        public struct StatFlags
        {
            public bool GetVal;
            public bool Show;
            public bool Risky;

            public static StatFlags Save() => new() { GetVal = patchGetVal, Show = isShow, Risky = isRisky };

            public void Restore()
            {
                patchGetVal = GetVal;
                isShow = Show;
                isRisky = Risky;
            }
        }

        // Types set in unique idols' params.json, by UniqueIdolKey. Reading the files again overwrites, never duplicates.
        public static Dictionary<string, MBTI> UniqueIdolTypes = new();
        public static Dictionary<int, MBTI> MBTIReferenceDict = new();

        public static string UniqueIdolKey(string modName, int bodyId) => modName + "/" + bodyId;

        // The 16 types by name, any case. Enum.TryParse would also take "3" or "ISTJ, ENFP".
        private static readonly Dictionary<string, MBTI> TypesByName = Enum.GetValues(typeof(MBTI)).Cast<MBTI>()
            .Where(m => m != MBTI.None).ToDictionary(m => m.ToString(), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads a type name, such as "INTJ" or "intj". None and anything else isn't a type.
        /// </summary>
        public static bool TryParseMBTI(string name, out MBTI mbti)
        {
            mbti = MBTI.None;
            return name != null && TypesByName.TryGetValue(name, out mbti);
        }

        /// <summary>
        /// The game's text for a key, or the key itself if none is loaded (a missing entry would throw in the
        /// game's tooltip and profile code).
        /// </summary>
        public static string Text(string key) => Language.Data.TryGetValue(key, out string text) ? text : key;

        /// <summary>
        /// Calculates the modifier to idol parameters based on their MBTI traits.
        /// </summary>
        /// <param name="girls">The idol for whom the modifier is calculated.</param>
        /// <param name="type">Type of parameter to modify.</param>
        /// <returns>Modifier value based on MBTI traits.</returns>
        public static float GetTraitModifier(data_girls.girls girls, data_girls._paramType type)
        {
            float num = 0;
            if (girls != null && data_girls.IsStatParam(type))
            {
                MBTI mBTI = GetGirlMBTI(girls);
                switch (mBTI)
                {
                    case MBTI.ENTJ:
                        if (girls.Is_Pushed())
                            num += ENTJStatBonus;
                        break;

                    case MBTI.ENTP:
                        if (girls.GetScandalPoints() > 0)
                            num += ENTPStatBonus;
                        break;

                    case MBTI.ESTP:
                        if (isRisky)
                            num += ESTPStatBonus;
                        break;

                    case MBTI.INFJ:
                        if (isShow)
                            num += INFJStatBonus;
                        break;
                }
            }
            return num;
        }


        /// <summary>
        /// Retrieves the MBTI assigned to a specific idol.
        /// </summary>
        /// <param name="girls">The idol whose MBTI is retrieved.</param>
        /// <returns>MBTI type assigned to the idol.</returns>
        public static MBTI GetGirlMBTI(data_girls.girls girls)
        {
            // A job offer with no idol, a café dish whose idol has left, a stat with no idol: no type, so no bonus
            if (girls == null)
                return MBTI.None;

            if (MBTIReferenceDict.TryGetValue(girls.id, out MBTI mbti))
            {
                return mbti;
            }

            mbti = GenerateMBTI(girls);
            SetGirlMBTI(girls, mbti);
            return mbti;

        }

        /// <summary>
        /// Sets the MBTI for a specific idol.
        /// </summary>
        /// <param name="girls">The idol whose MBTI is set.</param>
        /// <param name="mbti">MBTI type to assign to the idol.</param>
        public static void SetGirlMBTI(data_girls.girls girls, MBTI mbti)
        {
            if (girls == null)
                return;

            MBTIReferenceDict[girls.id] = mbti;
            // One type per idol: a second one would be saved too, and the last one wins on load.
            // "None" was written by older versions for an unreadable "mbti" in params.json.
            girls.Variables.RemoveAll(v => TypesByName.ContainsKey(v) || string.Equals(v, nameof(MBTI.None), StringComparison.OrdinalIgnoreCase));
            if (mbti != MBTI.None)
                girls.SetVariable(mbti.ToString());
        }

        /// <summary>
        /// Generates an MBTI type based on idol-specific attributes.
        /// </summary>
        /// <param name="girls">The idol for whom MBTI is generated.</param>
        /// <returns>Generated MBTI type.</returns>
        public static MBTI GenerateMBTI(data_girls.girls girls)
        {
            string id = girls.firstName + girls.lastName + girls.birthday.DayOfYear;
            uint hash = ComputeStringHash(id);
            int r = (int)(hash % 16U);
            MBTI mbtiType = (MBTI)(r + 1);

            return mbtiType;
        }

        /// <summary>
        /// Resets all stored MBTI data.
        /// </summary>
        public static void ResetMBTI()
        {
            MBTIReferenceDict.Clear();
        }

        /// <summary>
        /// Computes a hash value for a given string.
        /// </summary>
        /// <param name="s">String to compute hash for.</param>
        /// <returns>Computed hash value.</returns>
        private static uint ComputeStringHash(string s)
        {
            uint num = 0;
            if (s != null)
            {
                num = 2166136261U;
                for (int i = 0; i < s.Length; i++)
                {
                    num = (s[i] ^ num) * 16777619U;
                }
            }
            return num;
        }

        public enum MBTI
        {
            None,
            ISTJ,
            ISTP,
            ISFJ,
            ISFP,
            INFJ,
            INFP,
            INTJ,
            INTP,
            ESTP,
            ESTJ,
            ESFP,
            ESFJ,
            ENFP,
            ENFJ,
            ENTP,
            ENTJ
        }


    }
}
