using HarmonyLib;
using UnityEngine;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for generating a girl.
    /// Assigns an MBTI type to the generated girl based on texture data if applicable.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateGirl))]
    public class data_girls_GenerateGirl
    {
        /// <summary>
        /// Harmony patch for generating a girl.
        /// Assigns an MBTI type to the generated girl based on texture data if applicable.
        /// </summary>
        /// <param name="__result">Generated girl instance.</param>
        /// <param name="genTextures">Flag indicating whether textures should be generated.</param>
        public static void Postfix(ref data_girls.girls __result, bool genTextures)
        {
            if (!genTextures)
                return;

            if (__result.textureAssets == null || __result.textureAssets.Count == 0)
                return;

            data_girls_textures._textureAsset asset = __result.textureAssets[0].asset;
            if (asset != null && UniqueIdolTypes.TryGetValue(UniqueIdolKey(asset.ModName, asset.body_id), out MBTI mbti))
            {
                SetGirlMBTI(__result, mbti);
            }
        }
    }

    /// <summary>
    /// Harmony patch for adding points in player relationships.
    /// Increases the influence gain if the girl has the ISFJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(Relationships_Player), nameof(Relationships_Player.AddPoints))]
    public class Relationships_Player_AddPoints
    {

        /// <summary>
        /// Harmony patch for adding points in player relationships.
        /// Increases the influence gain if the girl has the ISFJ MBTI type.
        /// </summary>
        /// <param name="Type">Type of relationship points being added.</param>
        /// <param name="Girl">Girl instance for which points are being added.</param>
        /// <param name="Points">Number of points being added.</param>
        public static void Prefix(Relationships_Player._type Type, data_girls.girls Girl, ref int Points)
        {
            MBTI mBTI = GetGirlMBTI(Girl);
            if (mBTI != MBTI.ISFJ)
                return;

            if(Type == Relationships_Player._type.Influence && Points > 0)
            {
                Points = Mathf.RoundToInt(Points * (1 + ISFJBonus));
            }
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameter changes during birthdays.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(Birthday_Popup), nameof(Birthday_Popup.DoParam))]
    public class Birthday_Popup_DoParam
    {
        /// <summary>
        /// Harmony patch for calculating parameter changes during birthdays.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="Prm">Type of parameter being changed.</param>
        /// <param name="__instance">Instance of the birthday popup being patched.</param>
        [HarmonyPriority(Priority.High)]
        public static void Prefix(data_girls._paramType Prm, ref Birthday_Popup __instance)
        {
            if (GetGirlMBTI(__instance.Girl) != MBTI.INTJ)
                return;

            if (Prm == data_girls._paramType.smart)
            {
                patchSetVal_INTJ = true;
                patchSet_INTJ = true;
            }

        }

        /// <summary>
        /// Harmony patch for calculating parameter changes during birthdays.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        public static void Finalizer()
        {
            patchSetVal_INTJ = false;
            patchSet_INTJ = false;
        }
    }

    /// <summary>
    /// Harmony patch for setting the value of a girl's parameter.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.setVal))]
    public class data_girls_girls_param_setVal
    {
        /// <summary>
        /// Harmony patch for setting the value of a girl's parameter.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="newVal">New value of the parameter.</param>
        /// <param name="__instance">Instance of the girl's parameter being patched.</param>
        public static void Prefix(ref float newVal, data_girls.girls.param __instance)
        {
            if (!patchSetVal_INTJ)
                return;

            float paramVal = __instance.val;
            newVal = Mathf.Min(100, (1 + INTJBonus) * (newVal - paramVal) + paramVal);
        }
    }

    /// <summary>
    /// Harmony patch for setting the stat value during a birthday.
    /// Applies the INTJ bonus to stat growth.
    /// </summary>
    [HarmonyPatch(typeof(Birthday_Stat), nameof(Birthday_Stat.Set))]
    public class Birthday_Stat_Set
    {
        /// <summary>
        /// Harmony patch for setting the stat value during a birthday.
        /// Applies the INTJ bonus to stat growth.
        /// </summary>
        /// <param name="OldVal">Old value of the stat.</param>
        /// <param name="NewVal">New value of the stat.</param>
        public static void Prefix(float OldVal, ref float NewVal)
        {
            if (!patchSet_INTJ)
                return;

            NewVal = Mathf.Min(100, (1 + INTJBonus) * (NewVal - OldVal) + OldVal);
        }
    }

    /// <summary>
    /// Harmony patch for calculating the duration of a girl's training.
    /// Reduces the duration if the girl has the ESTJ MBTI type.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetDuration))]
    public class data_girls_girls_param_GetDuration
    {
        /// <summary>
        /// Harmony patch for calculating the duration of a girl's training.
        /// Reduces the duration if the girl has the ESTJ MBTI type.
        /// </summary>
        /// <param name="__instance">Instance of the girl's parameter being patched.</param>
        /// <param name="__result">Calculated training duration.</param>
        public static void Postfix(data_girls.girls.param __instance, ref float __result)
        {
            if (GetGirlMBTI(__instance.Parent) != MBTI.ESTJ)
                return;

            __result *= 1 - ESTJBonus;
        }
    }
}
