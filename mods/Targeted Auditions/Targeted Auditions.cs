using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using static CustomAuditions.CustomAuditions;

namespace CustomAuditions
{

    /// <summary>
    /// Patches the Popup_Audition class to allow scrolling cards in the audition popup.
    /// </summary>
    // Set up audition popup to allow scrolling cards
    [HarmonyPatch(typeof(Popup_Audition), "Start")]
    public class Popup_Audition_Start
    {

        /// <summary>
        /// Sets the properties of a RectTransform.
        /// </summary>
        /// <param name="rt">The RectTransform to modify.</param>
        /// <param name="anchorMin">The minimum anchor point.</param>
        /// <param name="anchorMax">The maximum anchor point.</param>
        /// <param name="offsetMin">The minimum offset.</param>
        /// <param name="offsetMax">The maximum offset.</param>
        private static void SetRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// Postfix method to set up the scrollable audition popup.
        /// </summary>
        /// <param name="__instance">The instance of Popup_Audition being patched.</param>
        public static void Postfix(Popup_Audition __instance)
        {
            if (__instance.Cards_Container.transform.parent.GetComponent<ScrollRect>() != null)
                return;

            // Create ScrollRect container and attach to panel
            GameObject scrollContainer = new(AUD_SCROLLRECT_NAME, typeof(RectTransform), typeof(ScrollRect));
            RectTransform scrollRectTransform = scrollContainer.GetComponent<RectTransform>();
            SetRectTransform(scrollRectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Configure the ScrollRect
            ScrollRect scrollRect = scrollContainer.GetComponent<ScrollRect>();
            scrollRect.content = __instance.Cards_Container.GetComponent<RectTransform>(); // attach content
            scrollRect.vertical = false;
            scrollRect.horizontal = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = false;
            scrollRect.scrollSensitivity = 20;

            // Configure hierarchy
            scrollContainer.transform.SetParent(__instance.Cards_Container.transform.parent, false);
            __instance.Cards_Container.transform.SetParent(scrollContainer.transform, false);

            __instance.Cards_Container.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>
    /// Patches the Auditions class to set variables at the start of an audition.
    /// </summary>
    [HarmonyPatch(typeof(Auditions), "GenerateGirls")]
    public class Auditions_GenerateGirls
    {
        /// <summary>
        /// Prefix method to set audition parameters before generating girls.
        /// </summary>
        /// <param name="__instance">The instance of Auditions being patched.</param>
        public static void Prefix(Auditions __instance, out bool __state)
        {
            __state = false;

            LoadConfiguredAgeRange();

            // Set sexual orientation
            float varLesbian = ReadFloat(VARID_LESCHANCE, DEF_CHANCE_LES_STR);
            float varBi = ReadFloat(VARID_BICHANCE, DEF_CHANCE_BI_STR);

            if (varLesbian + varBi > 100)
            {
                varLesbian = Mathf.Floor(varLesbian / (varLesbian + varBi) * 100);
                varBi = 100 - varLesbian;
                variables.Set(VARID_LESCHANCE, varLesbian.ToString(CultureInfo.InvariantCulture));
                variables.Set(VARID_BICHANCE, varBi.ToString(CultureInfo.InvariantCulture));
            }
            chanceLesbian = (int)varLesbian;
            chanceBi = (int)Mathf.Floor(varBi / (100 - chanceLesbian) * 100);


            // Set stat priorities
            foreach (data_girls._paramType param in paramTypes)
            {
                int value = ReadInt($"{VARID_PRIO_PREFIX}{param}", DEF_PRIO);
                priorityDict[param] = value;
            }

            // Set girl count
            __instance.NumberOfGirls = ReadInt(VARID_COUNT, DEF_COUNT);

            BeginAuditionGeneration();
            __state = true;
        }

        public static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
            {
                EndAuditionGeneration();
            }
            if (__exception != null)
            {
                Debug.LogError(
                    "[Targeted Auditions] Auditions.GenerateGirls failed:\n" +
                    __exception);
            }

            // Preserve the original exception.
            return __exception;
        }
    }

    /// <summary>
    /// Patches the data_girls class to apply girl sexuality.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        /// <summary>
        /// Before an audition candidate is generated, allow body IDs to repeat only after
        /// every currently eligible body ID has been used once in this audition.
        /// Unique idols' bodies never repeat within an audition.
        /// </summary>
        public static void Prefix(bool genTextures, data_girls_textures._textureAsset BodyAsset)
        {
            if (!IsGeneratingAudition || !genTextures || BodyAsset != null)
            {
                return;
            }

            if (!HasUnusedEligibleBody())
            {
                ClearUsedBodiesExceptUnique();
            }
        }

        /// <summary>
        /// Postfix method to set the sexuality of a generated audition candidate.
        /// </summary>
        /// <param name="__result">The generated girl data.</param>
        public static void Postfix(ref data_girls.girls __result)
        {
            if (!IsGeneratingAudition || __result == null)
            {
                return;
            }

            data_girls.girls._sexuality sexuality = data_girls.girls._sexuality.straight;
            if (mainScript.chance(chanceLesbian))
            {
                sexuality = data_girls.girls._sexuality.lesbian;
            }
            else if (mainScript.chance(chanceBi))
            {
                sexuality = data_girls.girls._sexuality.bi;
            }
            __result.sexuality = sexuality;
        }
    }

    /// <summary>
    /// Patches the data_girls class to apply custom girl stats.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), "GenerateParams")]
    public static class data_girls_GenerateParams
    {
        /// <summary>
        /// Reassigns the candidate's stats to skills by priority, after the game has rolled them. Each value
        /// moves with the potential the game rolled for it, so a high stat keeps a matching potential.
        /// Only during auditions; other generated girls (unique idols, rivals) keep the game's order.
        /// </summary>
        public static void Postfix(data_girls.girls Girl)
        {
            if (!IsGeneratingAudition || Girl == null)
            {
                return;
            }

            // _val, not val: val adds trait and award bonuses, and the game set these as whole numbers
            List<data_girls.girls.param> stats = paramTypes.Select(Girl.getParam).ToList();
            float[] values = stats.Select(stat => stat._val).ToArray();
            int[] potentials = stats.Select(stat => stat.potential).ToArray();

            int[] source = AssignByPriority(values.Select(v => (int)v).ToList());
            for (int i = 0; i < paramTypes.Count; i++)
            {
                Girl.setParam(paramTypes[i], values[source[i]]);
                stats[i].potential = potentials[source[i]];
            }
        }

        /// <summary>
        /// For each skill (in paramTypes order), the index of the value it gets. Values go highest first,
        /// each to a remaining skill rolled with probability priority / remaining total. If the remaining
        /// priorities add up to nothing (hand-edited settings), the rest keep their order.
        /// </summary>
        public static int[] AssignByPriority(List<int> statValues)
        {
            int[] source = new int[statValues.Count];
            List<int> byValue = Enumerable.Range(0, statValues.Count).OrderByDescending(i => statValues[i]).ToList();
            List<data_girls._paramType> remainingParamTypes = new List<data_girls._paramType>(paramTypes);

            int next = 0;
            for (; next < byValue.Count; next++)
            {
                int totalPriority = remainingParamTypes.Sum(p => priorityDict[p]);
                if (totalPriority <= 0)
                {
                    break;
                }

                int roll = UnityEngine.Random.Range(1, totalPriority + 1);
                int cumulativePriority = 0;
                for (int i = 0; i < remainingParamTypes.Count; i++)
                {
                    cumulativePriority += priorityDict[remainingParamTypes[i]];
                    if (roll <= cumulativePriority)
                    {
                        source[paramTypes.IndexOf(remainingParamTypes[i])] = byValue[next];
                        remainingParamTypes.RemoveAt(i);
                        break;
                    }
                }
            }

            // Anything left: the remaining values, highest first, to the remaining skills in order
            for (int i = 0; next < byValue.Count; next++, i++)
            {
                source[paramTypes.IndexOf(remainingParamTypes[i])] = byValue[next];
            }

            return source;
        }

        /// <summary>
        /// The values as AssignByPriority would arrange them across skills (paramTypes order).
        /// </summary>
        public static List<int> ArrangeByPriority(List<int> statValues)
        {
            return AssignByPriority(statValues).Select(i => statValues[i]).ToList();
        }
    }

    /// <summary>
    /// Patches the data_girls.girls class to apply age limits.
    /// </summary>
    [HarmonyPatch(typeof(data_girls.girls), "GenerateBirthday")]
    public class data_girls_girls_GenerateBirthday
    {
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (IsGeneratingAudition)
            {
                ApplyRandomBirthdayInConfiguredRange(__instance);
            }
        }
    }

    /// <summary>
    /// Contains utility methods and variables for custom auditions.
    /// </summary>
    class CustomAuditions
    {
        public const string DEF_MINAGE_STR = "12";
        public const string DEF_MAXAGE_STR = "23";
        public const string DEF_CHANCE_LES_STR = "7";
        public const string DEF_CHANCE_BI_STR = "14";
        public const string DEF_PRIO = "50";
        public const string DEF_COUNT = "5";

        public const string VARID_MINAGE = "AuditionAgeLimit_MinAge";
        public const string VARID_MAXAGE = "AuditionAgeLimit_MaxAge";
        public const string VARID_BICHANCE = "CustomAudition_Bi";
        public const string VARID_LESCHANCE = "CustomAudition_Gay";
        public const string VARID_PRIO_PREFIX = "CustomAudition_Prio_";
        public const string VARID_COUNT = "CustomAudition_Count";


        public const string AUD_SCROLLRECT_NAME = "ScrollContainer";

        public static int minAge = 12;
        public static int maxAge = 23;

        public static int chanceLesbian = 7;
        public static int chanceBi = 14;

        /// <summary>
        /// Reads a Mod Menu setting as a number. A missing or unreadable value (e.g. a hand-edited save,
        /// or "14,5" from a decimal-comma locale) gives the default instead of throwing.
        /// </summary>
        /// <param name="varID">The setting's variable ID.</param>
        /// <param name="def">The default value, as written in the constants above.</param>
        public static float ReadFloat(string varID, string def)
        {
            if (float.TryParse(variables.Get(varID), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
                !float.IsNaN(value) && !float.IsInfinity(value))
            {
                return value;
            }
            return float.Parse(def, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a Mod Menu setting as a whole number, rounding any fraction. See <see cref="ReadFloat"/>.
        /// </summary>
        public static int ReadInt(string varID, string def)
        {
            return (int)Math.Round(ReadFloat(varID, def));
        }


        public static List<data_girls._paramType> paramTypes = new()
        {
            data_girls._paramType.cute,
            data_girls._paramType.cool,
            data_girls._paramType.sexy,
            data_girls._paramType.pretty,
            data_girls._paramType.vocal,
            data_girls._paramType.dance,
            data_girls._paramType.funny,
            data_girls._paramType.smart
        };

        public static Dictionary<data_girls._paramType, int> priorityDict = new();

        private static int auditionGenerationDepth = 0;
        public static bool IsGeneratingAudition => auditionGenerationDepth > 0;

        public static void BeginAuditionGeneration()
        {
            auditionGenerationDepth++;
        }

        public static void EndAuditionGeneration()
        {
            if (auditionGenerationDepth > 0)
            {
                auditionGenerationDepth--;
            }
        }

        // The game's private static list of every portrait asset
        private static readonly AccessTools.FieldRef<List<data_girls_textures._textureAsset>> TextureAssets =
            AccessTools.StaticFieldRefAccess<List<data_girls_textures._textureAsset>>(AccessTools.Field(typeof(data_girls_textures), "textureAssets"));

        public static bool HasUnusedEligibleBody()
        {
            List<data_girls_textures._textureAsset> textureAssets = TextureAssets();
            if (textureAssets == null)
            {
                return false;
            }

            return textureAssets.Any(asset =>
                asset != null &&
                !asset.Add_To_Default &&
                asset.type == data_girls_textures._spriteType.body &&
                !Auditions.UsedBodyIDs.Contains(asset.body_id) &&
                asset.CanBeHired());
        }

        /// <summary>
        /// Frees the used body IDs for reuse, except unique idols' bodies, so a unique idol
        /// already shown in this audition can't be shown (and hired) a second time.
        /// If only those bodies are left, the game skips the candidate rather than repeat one.
        /// </summary>
        public static void ClearUsedBodiesExceptUnique()
        {
            List<data_girls_textures._textureAsset> textureAssets = TextureAssets();
            HashSet<int> uniqueBodyIDs = new();
            if (textureAssets != null)
            {
                foreach (data_girls_textures._textureAsset asset in textureAssets)
                {
                    if (asset != null &&
                        asset.Unique &&
                        !asset.Add_To_Default &&
                        asset.type == data_girls_textures._spriteType.body)
                    {
                        uniqueBodyIDs.Add(asset.body_id);
                    }
                }
            }

            Auditions.UsedBodyIDs.RemoveAll(id => !uniqueBodyIDs.Contains(id));
        }

        public static void LoadConfiguredAgeRange()
        {
            minAge = ReadInt(VARID_MINAGE, DEF_MINAGE_STR);
            maxAge = ReadInt(VARID_MAXAGE, DEF_MAXAGE_STR);
            if (maxAge < minAge)
            {
                int originalMinAge = minAge;
                minAge = maxAge;
                maxAge = originalMinAge;

                variables.Set(VARID_MAXAGE, maxAge.ToString());
                variables.Set(VARID_MINAGE, minAge.ToString());
            }
        }

        public static void ApplyRandomBirthdayInConfiguredRange(data_girls.girls girl)
        {
            if (girl == null)
            {
                return;
            }

            int age = UnityEngine.Random.Range(minAge, maxAge + 1);
            DateTime latestBirthday = staticVars.dateTime.AddYears(-age);
            DateTime earliestBirthday = staticVars.dateTime.AddYears(-age - 1).AddDays(1);
            int possibleDays = (latestBirthday - earliestBirthday).Days + 1;
            DateTime dateTime = earliestBirthday.AddDays(UnityEngine.Random.Range(0, possibleDays));
            girl.SetBirthday(dateTime);
        }

    }
}
