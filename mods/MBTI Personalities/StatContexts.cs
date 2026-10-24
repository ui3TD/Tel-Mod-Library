using HarmonyLib;
using static MBTIPersonalities.MBTIPersonalities;

namespace MBTIPersonalities
{
    /// <summary>
    /// Harmony patch for getting the girl coefficient for a business proposal.
    /// Applies MBTI-related bonuses to the proposal.
    /// </summary>
    [HarmonyPatch(typeof(business._proposal), nameof(business._proposal.GetGirlCoeff))]
    public class Business__proposal_GetGirlCoeff
    {
        /// <summary>
        /// Harmony patch for getting the girl coefficient for a business proposal.
        /// Applies MBTI-related bonuses to the proposal.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        /// <summary>
        /// Harmony patch for getting the girl coefficient for a business proposal.
        /// Applies MBTI-related bonuses to the proposal.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the average parameter value for a group of girls.
    /// Applies MBTI-related bonuses to the parameter value.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GetAverageParam))]
    public class Data_girls_GetAverageParam
    {
        /// <summary>
        /// Harmony patch for getting the average parameter value for a group of girls.
        /// Applies MBTI-related bonuses to the parameter value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
            isShow = true;
        }

        /// <summary>
        /// Harmony patch for getting the average parameter value for a group of girls.
        /// Applies MBTI-related bonuses to the parameter value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
            isShow = false;
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameters for a show's senbatsu.
    /// Applies MBTI-related bonuses to the parameters.
    /// </summary>
    [HarmonyPatch(typeof(Shows._show), nameof(Shows._show.SenbatsuCalcParam))]
    public class Shows__show_SenbatsuCalcParam
    {
        /// <summary>
        /// Harmony patch for calculating parameters for a show's senbatsu.
        /// Applies MBTI-related bonuses to the parameters.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
            isShow = true;
        }

        /// <summary>
        /// Harmony patch for calculating parameters for a show's senbatsu.
        /// Applies MBTI-related bonuses to the parameters.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
            isShow = false;
        }
    }

    /// <summary>
    /// Harmony patch for calculating parameters for a single's senbatsu.
    /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
    /// </summary>
    [HarmonyPatch(typeof(singles._single), nameof(singles._single.SenbatsuCalcParam))]
    public class Singles__single_SenbatsuCalcParam
    {
        /// <summary>
        /// Harmony patch for calculating parameters for a single's senbatsu.
        /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
        /// </summary>
        /// <param name="__instance">Instance of the single being patched.</param>
        [HarmonyPriority(Priority.First)]
        public static void Prefix(singles._single __instance)
        {
            patchGetVal = true;

            isRisky = __instance.GetRiskyMarketing() != null;
        }

        /// <summary>
        /// Harmony patch for calculating parameters for a single's senbatsu.
        /// Applies MBTI-related bonuses to the parameters and handles risky marketing.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
            isRisky = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the skill value for a concert song.
    /// Applies MBTI-related bonuses to the skill value.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), nameof(SEvent_Concerts._concert._song.GetSkillValue))]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        /// <summary>
        /// Harmony patch for getting the skill value for a concert song.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        /// <summary>
        /// Harmony patch for getting the skill value for a concert song.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
        }
    }

    /// <summary>
    /// Harmony patch for getting the skill value for a concert MC.
    /// Applies MBTI-related bonuses to the skill value.
    /// </summary>
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), nameof(SEvent_Concerts._concert._mc.GetSkillValue))]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        /// <summary>
        /// Harmony patch for getting the skill value for a concert MC.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }
        /// <summary>
        /// Harmony patch for getting the skill value for a concert MC.
        /// Applies MBTI-related bonuses to the skill value.
        /// </summary>
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {

            patchGetVal = false;
        }
    }

    /// <summary>
    /// Harmony patch to modify game parameters based on MBTI traits assigned to idols.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetVal))]
    public class data_girls_girls_param_GetVal
    {
        /// <summary>
        /// Postfix method to add trait-based modifiers to parameter values.
        /// </summary>
        /// <param name="__result">The original result of the parameter value.</param>
        /// <param name="__instance">Instance of the parameter being accessed.</param>
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (patchGetVal)
            {
                __result += GetTraitModifier(__instance.Parent, __instance.type);
            }
        }
    }
}
