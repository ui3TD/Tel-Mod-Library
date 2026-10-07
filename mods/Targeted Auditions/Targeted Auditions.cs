using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
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

            // Set audition age limits (only if popup is not used)
            bool toggle = int.Parse(variables.Get(VARID_AGELIMIT_POPUP_TOGGLE) ?? DEF_AGELIMIT_POPUP_TOGGLE) == 1;
            if (!toggle)
            {
                minAge = int.Parse(variables.Get(VARID_MINAGE) ?? DEF_MINAGE_STR);
                maxAge = int.Parse(variables.Get(VARID_MAXAGE) ?? DEF_MAXAGE_STR);
                if (maxAge < minAge)
                {
                    // swap values
                    maxAge = int.Parse(variables.Get(VARID_MINAGE) ?? DEF_MAXAGE_STR);
                    minAge = int.Parse(variables.Get(VARID_MAXAGE) ?? DEF_MINAGE_STR);

                    // correct default variables
                    defaultMaxAge = maxAge;
                    defaultMinAge = minAge;
                    variables.Set(VARID_MAXAGE, maxAge.ToString());
                    variables.Set(VARID_MINAGE, minAge.ToString());
                }
            }

            // Set sexual orientation
            float varLesbian = float.Parse(variables.Get(VARID_LESCHANCE) ?? DEF_CHANCE_LES_STR);
            float varBi = float.Parse(variables.Get(VARID_BICHANCE) ?? DEF_CHANCE_BI_STR);

            if (varLesbian + varBi > 100)
            {
                varLesbian = Mathf.Floor(varLesbian / (varLesbian + varBi) * 100);
                varBi = 100 - varLesbian;
                variables.Set(VARID_LESCHANCE, varLesbian.ToString());
                variables.Set(VARID_BICHANCE, varBi.ToString());
            }
            chanceLesbian = (int)varLesbian;
            chanceBi = (int)Mathf.Floor(varBi / (100 - chanceLesbian) * 100);


            // Set stat priorities
            foreach (data_girls._paramType param in paramTypes)
            {
                int value = int.Parse(variables.Get($"{VARID_PRIO_PREFIX}{param}") ?? DEF_PRIO);
                priorityDict[param] = value;
            }

            // Set girl count
            __instance.NumberOfGirls = int.Parse(variables.Get(VARID_COUNT) ?? DEF_COUNT);

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
        private const int MaxStatValue = 99;
        private const int MaxNoProgressIterations = 4096;

        /// <summary>
        /// Reproduces vanilla audition stat generation while preventing its unreachable-budget loop.
        /// Vanilla reserves up to three stats, then spends the entire remaining point budget only on
        /// the other stats. Gold/platinum rolls can request more points than those adjustable stats can
        /// hold, causing the main thread to spin forever once they all reach 99.
        /// </summary>
        public static bool Prefix(data_girls __instance, data_girls.girls Girl, Auditions.data._girl._type Type)
        {
            if (!IsGeneratingAudition)
            {
                return true;
            }

            GenerateParamsSafely(Girl, Type);
            return false;
        }

        private static void GenerateParamsSafely(data_girls.girls girl, Auditions.data._girl._type type)
        {
            int requestedPoints = Auditions.GetPointsByType(type);
            int pointsRemaining = requestedPoints - 8;
            List<int> statValues = new List<int>();

            int reservedCount = 0;
            if (mainScript.chance(20))
            {
                reservedCount = 1;
            }
            else if (mainScript.chance(20))
            {
                reservedCount = 2;
            }
            else if (mainScript.chance(5))
            {
                reservedCount = 3;
            }

            int potentialLow = 90;
            int potentialHigh = 99;
            const int reservedMin = 40;
            const int reservedMax = 80;

            if (type == Auditions.data._girl._type.golden)
            {
                potentialLow = 70;
                potentialHigh = 95;
            }
            else if (type == Auditions.data._girl._type.silver)
            {
                potentialLow = 55;
                potentialHigh = 85;
            }
            else if (type == Auditions.data._girl._type.normal)
            {
                potentialLow = 10;
                potentialHigh = 80;
            }

            for (int i = 0; i < reservedCount; i++)
            {
                int value = UnityEngine.Random.Range(reservedMin, reservedMax);
                statValues.Add(value);
                pointsRemaining -= value;
            }

            for (int i = 0; i < 8 - reservedCount; i++)
            {
                statValues.Add(1);
            }

            int adjustableCapacity = 0;
            for (int i = reservedCount; i < statValues.Count; i++)
            {
                adjustableCapacity += MaxStatValue - statValues[i];
            }

            // Spend exactly as vanilla does whenever the requested budget is reachable. If it is not,
            // cap the vanilla phase at the physical capacity of the adjustable stats and carry the
            // impossible residual into the originally reserved stats afterward.
            int residualForReservedStats = Math.Max(0, pointsRemaining - adjustableCapacity);
            int adjustableBudget = pointsRemaining - residualForReservedStats;
            SpendVanillaBudget(statValues, reservedCount, statValues.Count, ref adjustableBudget);

            if (adjustableBudget > 0)
            {
                // This should only be reachable after an extreme run of zero-progress RNG. Keep the
                // mod hard-safe even then instead of recreating vanilla's unbounded loop.
                int forced = ForceSpendBudget(statValues, reservedCount, statValues.Count, adjustableBudget);
                adjustableBudget -= forced;
                if (adjustableBudget > 0)
                {
                    residualForReservedStats += adjustableBudget;
                    adjustableBudget = 0;
                }
            }

            if (residualForReservedStats > 0)
            {
                int recovered = ForceSpendBudget(statValues, 0, reservedCount, residualForReservedStats);
                residualForReservedStats -= recovered;

            }

            bool foundOne = false;
            for (int i = 0; i < statValues.Count; i++)
            {
                if (statValues[i] == 1 && foundOne)
                {
                    statValues[i] = UnityEngine.Random.Range(1, 20);
                }
                else if (statValues[i] == 1)
                {
                    foundOne = true;
                }
            }

            ExtensionMethods.Shuffle<int>(statValues);
            statValues = Infix(statValues);

            girl.setParam(data_girls._paramType.cute, statValues[0]);
            girl.setParam(data_girls._paramType.cool, statValues[1]);
            girl.setParam(data_girls._paramType.sexy, statValues[2]);
            girl.setParam(data_girls._paramType.pretty, statValues[3]);
            girl.setParam(data_girls._paramType.vocal, statValues[4]);
            girl.setParam(data_girls._paramType.dance, statValues[5]);
            girl.setParam(data_girls._paramType.funny, statValues[6]);
            girl.setParam(data_girls._paramType.smart, statValues[7]);

            if (type == Auditions.data._girl._type.platinum)
            {
                foreach (data_girls._paramType paramType in paramTypes)
                {
                    girl.getParam(paramType).potential = 99;
                }
                return;
            }

            girl.getParam(data_girls._paramType.cute).potential = GeneratePotential(statValues[0], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.cool).potential = GeneratePotential(statValues[1], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.sexy).potential = GeneratePotential(statValues[2], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.pretty).potential = GeneratePotential(statValues[3], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.vocal).potential = GeneratePotential(statValues[4], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.dance).potential = GeneratePotential(statValues[5], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.funny).potential = GeneratePotential(statValues[6], potentialLow, potentialHigh);
            girl.getParam(data_girls._paramType.smart).potential = GeneratePotential(statValues[7], potentialLow, potentialHigh);
        }

        private static void SpendVanillaBudget(List<int> statValues, int startIndex, int endExclusive, ref int budget)
        {
            if (budget <= 0 || startIndex >= endExclusive)
            {
                return;
            }

            int index = startIndex;
            int noProgressIterations = 0;

            while (budget > 0 && noProgressIterations < MaxNoProgressIterations)
            {
                int roll = UnityEngine.Random.Range(0, 20);
                int add;

                if (statValues[index] >= MaxStatValue)
                {
                    add = 0;
                }
                else if (statValues[index] >= 90)
                {
                    add = mainScript.chance(50) ? 1 : 0;
                }
                else if (budget <= roll)
                {
                    add = budget;
                }
                else
                {
                    add = roll;
                }

                if (statValues[index] + add > MaxStatValue)
                {
                    add = 0;
                }

                statValues[index] += add;
                budget -= add;
                noProgressIterations = add == 0 ? noProgressIterations + 1 : 0;

                index++;
                if (index == endExclusive)
                {
                    index = startIndex;
                }
            }
        }

        /// <summary>
        /// Deterministically spends as much of a residual budget as the selected stat range can hold.
        /// This is used only for the impossible-budget recovery path (or an extreme RNG no-progress
        /// guard), so ordinary reachable vanilla rolls retain their normal random distribution.
        /// </summary>
        private static int ForceSpendBudget(List<int> statValues, int startIndex, int endExclusive, int budget)
        {
            if (budget <= 0 || startIndex >= endExclusive)
            {
                return 0;
            }

            int originalBudget = budget;
            int index = startIndex;

            while (budget > 0)
            {
                bool addedAny = false;
                for (int visited = 0; visited < endExclusive - startIndex && budget > 0; visited++)
                {
                    if (statValues[index] < MaxStatValue)
                    {
                        statValues[index]++;
                        budget--;
                        addedAny = true;
                    }

                    index++;
                    if (index == endExclusive)
                    {
                        index = startIndex;
                    }
                }

                if (!addedAny)
                {
                    break;
                }
            }

            return originalBudget - budget;
        }

        private static int GeneratePotential(int value, int lowVal, int highVal)
        {
            if (value > lowVal)
            {
                lowVal = value;
            }
            if (lowVal >= highVal)
            {
                return lowVal;
            }
            return UnityEngine.Random.Range(lowVal, highVal);
        }

        /// <summary>
        /// Reassigns the generated stat values according to Targeted Auditions priorities.
        /// This is the same priority behavior the previous transpiler applied after vanilla shuffled
        /// the generated values.
        /// </summary>
        public static List<int> Infix(List<int> statValues)
        {
            if (!IsGeneratingAudition)
            {
                return statValues;
            }

            List<int> output = new List<int>(statValues);
            Dictionary<data_girls._paramType, int> priorityDictTemp = new Dictionary<data_girls._paramType, int>(priorityDict);
            List<data_girls._paramType> remainingParamTypes = priorityDictTemp.Keys.ToList();
            List<int> sortedStatValues = statValues.OrderByDescending(v => v).ToList();

            foreach (int statValue in sortedStatValues)
            {
                int totalPriority = remainingParamTypes.Sum(p => priorityDictTemp[p]);
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
                        output[paramTypes.IndexOf(remainingParamTypes[i])] = statValue;
                        remainingParamTypes.RemoveAt(i);
                        break;
                    }
                }
            }

            return output;
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
    /// Patches the CM_Player_Audition_Button class to handle age input popup. (obsolete)
    /// </summary>
    [HarmonyPatch(typeof(CM_Player_Audition_Button), "OnClick")]
    public class Auditions_GenerateAudition
    {
        /// <summary>
        /// Postfix method to show the age input popup if enabled. (disabled)
        /// </summary>
        public static void Postfix()
        {
            bool toggle = int.Parse(variables.Get(VARID_AGELIMIT_POPUP_TOGGLE) ?? DEF_AGELIMIT_POPUP_TOGGLE) == 1;
            if (toggle)
            {
                defaultMinAge = int.Parse(variables.Get(VARID_MINAGE) ?? DEF_MINAGE_STR);
                defaultMaxAge = int.Parse(variables.Get(VARID_MAXAGE) ?? DEF_MAXAGE_STR);
                if (defaultMaxAge < defaultMinAge)
                {
                    // swap values
                    defaultMaxAge = int.Parse(variables.Get(VARID_MINAGE) ?? DEF_MAXAGE_STR);
                    defaultMinAge = int.Parse(variables.Get(VARID_MAXAGE) ?? DEF_MINAGE_STR);

                    // correct variables
                    variables.Set(VARID_MAXAGE, maxAge.ToString());
                    variables.Set(VARID_MINAGE, minAge.ToString());
                }
                agePopup = true;
                Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().Open(PopupManager._type.staff_nickname, true);
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

        public const string VARID_AGELIMIT_POPUP_TOGGLE = "AuditionAgeLimit_TogglePopup";
        public const string DEF_AGELIMIT_POPUP_TOGGLE = "0";

        public static int defaultMinAge = 12;
        public static int defaultMaxAge = 23;
        public static int minAge = defaultMinAge;
        public static int maxAge = defaultMaxAge;

        public static int chanceLesbian = 7;
        public static int chanceBi = 14;

        public static bool agePopup = false;
        public static bool inputValid = false;

        /// <summary>
        /// Parses the age range string and sets the minAge and maxAge values.
        /// </summary>
        /// <param name="ageRange">The age range string to parse.</param>
        public static void ParseAgeRange(string ageRange)
        {
            if (IsInputValid(ageRange))
            {
                string[] ageLimits = ageRange.Split('-');
                minAge = int.Parse(ageLimits[0].Trim());
                maxAge = int.Parse(ageLimits[1].Trim());
            }
            else
            {
                minAge = defaultMinAge;
                maxAge = defaultMaxAge;
            }
        }

        /// <summary>
        /// Validates the input age range string.
        /// </summary>
        /// <param name="ageRange">The age range string to validate.</param>
        /// <returns>True if the input is valid, false otherwise.</returns>
        public static bool IsInputValid(string ageRange)
        {

            string[] ageLimits = ageRange.Split('-');

            if (ageLimits == null || ageLimits.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(ageLimits[0].Trim(), out int min))
            {
                return false;
            }
            if (!int.TryParse(ageLimits[1].Trim(), out int max))
            {
                return false;
            }

            if (max < 1 || min < 1)
            {
                return false;
            }

            if (max < min)
            {
                return false;
            }

            return true;
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

        private static readonly System.Reflection.FieldInfo textureAssetsField =
            AccessTools.Field(typeof(data_girls_textures), "textureAssets");

        public static bool HasUnusedEligibleBody()
        {
            List<data_girls_textures._textureAsset> textureAssets =
                textureAssetsField?.GetValue(null) as List<data_girls_textures._textureAsset>;
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
            List<data_girls_textures._textureAsset> textureAssets =
                textureAssetsField?.GetValue(null) as List<data_girls_textures._textureAsset>;
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
