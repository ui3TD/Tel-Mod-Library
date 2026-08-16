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
            if (__instance == null || __instance.Cards_Container == null)
                return;

            Transform currentParent = __instance.Cards_Container.transform.parent;
            if (currentParent == null)
                return;

            if (currentParent.GetComponent<ScrollRect>() != null)
                return;

            // Create ScrollRect container and attach to panel
            GameObject scrollContainer = new(AUD_SCROLLRECT_NAME, typeof(RectTransform), typeof(ScrollRect));
            RectTransform scrollRectTransform = scrollContainer.GetComponent<RectTransform>();
            SetRectTransform(scrollRectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Configure the ScrollRect
            ScrollRect scrollRect = scrollContainer.GetComponent<ScrollRect>();
            scrollRect.content = __instance.Cards_Container.GetComponent<RectTransform>(); // attach content
            scrollRect.viewport = scrollRectTransform;
            scrollRect.vertical = false;
            scrollRect.horizontal = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = false;
            scrollRect.scrollSensitivity = 20;

            // Configure hierarchy
            scrollContainer.transform.SetParent(currentParent, false);
            __instance.Cards_Container.transform.SetParent(scrollContainer.transform, false);

            // Reuse existing fitter if one exists to avoid duplicate component warnings.
            ContentSizeFitter fitter = __instance.Cards_Container.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = __instance.Cards_Container.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>
    /// Tracks audition popup loading from the moment Set begins.
    /// Starting before the original Set keeps the watchdog state aligned with
    /// Assistant Manager's delayed per-manager audition handoff.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Audition), "Set", new Type[] { typeof(Auditions.data), typeof(bool) })]
    public class Popup_Audition_Set
    {
        public static void Prefix(Popup_Audition __instance)
        {
            if (__instance == null)
            {
                return;
            }

            auditionLoadStartedAt[__instance.GetInstanceID()] = Time.unscaledTime;
        }

        public static Exception Finalizer(Popup_Audition __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            if (__instance != null)
            {
                auditionLoadStartedAt.Remove(__instance.GetInstanceID());
            }

            Debug.LogError(
                "[Targeted Auditions] Popup_Audition.Set failed:\n" +
                __exception);

            // Preserve the original exception.
            return __exception;
        }
    }

    /// <summary>
    /// Clears load watchdog state when audition popup is reset.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Audition), "Reset")]
    public class Popup_Audition_Reset
    {
        /// <summary>
        /// Postfix method that removes stale watchdog entries.
        /// </summary>
        /// <param name="__instance">Popup instance.</param>
        public static void Postfix(Popup_Audition __instance)
        {
            if (__instance == null)
            {
                return;
            }

            auditionLoadStartedAt.Remove(__instance.GetInstanceID());
        }
    }

    /// <summary>
    /// Clears load watchdog state when audition popup is closed.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Audition), "Close")]
    public class Popup_Audition_Close
    {
        /// <summary>
        /// Prefix method that removes stale watchdog entries before close logic runs.
        /// </summary>
        /// <param name="__instance">Popup instance.</param>
        public static void Prefix(Popup_Audition __instance)
        {
            if (__instance == null)
            {
                return;
            }

            auditionLoadStartedAt.Remove(__instance.GetInstanceID());
        }
    }

    /// <summary>
    /// Prevents recruitment popup deadlocks when one portrait never resolves.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Audition), "PortraitsLoaded")]
    public class Popup_Audition_PortraitsLoaded
    {
        /// <summary>
        /// Postfix method that applies a timeout fallback for stuck portrait loading.
        /// </summary>
        /// <param name="__instance">Popup instance.</param>
        /// <param name="__result">Original readiness result.</param>
        public static void Postfix(Popup_Audition __instance, ref bool __result)
        {
            if (__result || __instance == null || __instance.Cards_Container == null)
            {
                return;
            }

            int popupId = __instance.GetInstanceID();
            if (!auditionLoadStartedAt.TryGetValue(popupId, out float startedAt))
            {
                return;
            }

            float elapsed = Time.unscaledTime - startedAt;
            if (elapsed < PORTRAIT_LOAD_TIMEOUT_SECONDS)
            {
                return;
            }

            // The vanilla coroutine waits indefinitely for all portraits. With large candidate counts this can
            // deadlock the popup (blur shown, cards never become interactive). After timeout, continue anyway.
            EnsurePopupIsVisible(__instance);
            Sprite fallbackSprite = FindFallbackPortraitSprite(__instance);
            bool missingPortraits = FillMissingPortraits(__instance, fallbackSprite);
            if (missingPortraits)
            {
                Debug.Log("[Targeted Auditions] Portrait load timed out. Continuing with fallback portraits.");
            }

            __result = true;
        }

        private static void EnsurePopupIsVisible(Popup_Audition popup)
        {
            CanvasGroup cg = popup.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }

            RectTransform rt = popup.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
            }
        }

        private static Sprite FindFallbackPortraitSprite(Popup_Audition popup)
        {
            foreach (Transform child in popup.Cards_Container.transform)
            {
                Audition_Closed_Card closedCard = child.GetComponent<Audition_Closed_Card>();
                if (closedCard == null || closedCard.Portrait == null)
                {
                    continue;
                }

                Image image = closedCard.Portrait.GetComponent<Image>();
                if (image != null && image.sprite != null)
                {
                    return image.sprite;
                }
            }

            return null;
        }

        private static bool FillMissingPortraits(Popup_Audition popup, Sprite fallback)
        {
            bool hadMissing = false;
            foreach (Transform child in popup.Cards_Container.transform)
            {
                Audition_Closed_Card closedCard = child.GetComponent<Audition_Closed_Card>();
                if (closedCard == null || closedCard.Portrait == null)
                {
                    continue;
                }

                Image image = closedCard.Portrait.GetComponent<Image>();
                if (image == null || image.sprite != null)
                {
                    continue;
                }

                hadMissing = true;
                if (fallback != null)
                {
                    image.sprite = fallback;
                }
            }

            return hadMissing;
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
        /// </summary>
        public static void Prefix(bool genTextures, data_girls_textures._textureAsset BodyAsset)
        {
            if (!IsGeneratingAudition || !genTextures || BodyAsset != null)
            {
                return;
            }

            if (!HasUnusedEligibleBody())
            {
                Auditions.UsedBodyIDs.Clear();
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
        /// Transpiler method to modify the IL code for generating girl parameters.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>The modified IL instructions.</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count - 1; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldloc_1 && instructionList[i + 1].opcode == OpCodes.Call)
                {
                    index = i + 1;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_1));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_GenerateParams), "Infix")));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Stloc_1));
            }

            return instructionList.AsEnumerable();
        }

        /// <summary>
        /// Infix method to assign stat values based on priorities.
        /// </summary>
        /// <param name="statValues">The list of stat values to assign.</param>
        /// <returns>A new list of assigned stat values.</returns>
        public static List<int> Infix(List<int> statValues)
        {
            if (!IsGeneratingAudition)
            {
                return statValues;
            }

            List<int> output = new(statValues);
            Dictionary<data_girls._paramType, int> priorityDictTemp = new(priorityDict);
            List<data_girls._paramType> remainingParamTypes = priorityDictTemp.Keys.ToList();

            List<int> sortedStatValues = statValues.OrderByDescending(v => v).ToList();

            // Assign stats based on priority
            foreach (int statValue in sortedStatValues)
            {

                // Calculate total priority
                int totalPriority = remainingParamTypes.Sum(p => priorityDictTemp[p]);

                // Roll a random number
                int roll = UnityEngine.Random.Range(1, totalPriority + 1);

                // Find which param "wins" this roll
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
        public const float PORTRAIT_LOAD_TIMEOUT_SECONDS = 6f;

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
        public static Dictionary<int, float> auditionLoadStartedAt = new();

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

        public static void LoadConfiguredAgeRange()
        {
            // The former per-audition age popup is retired. Always use the Mod Menu range.
            variables.Set(VARID_AGELIMIT_POPUP_TOGGLE, DEF_AGELIMIT_POPUP_TOGGLE);

            minAge = int.Parse(variables.Get(VARID_MINAGE) ?? DEF_MINAGE_STR);
            maxAge = int.Parse(variables.Get(VARID_MAXAGE) ?? DEF_MAXAGE_STR);
            if (maxAge < minAge)
            {
                int originalMinAge = minAge;
                minAge = maxAge;
                maxAge = originalMinAge;

                defaultMaxAge = maxAge;
                defaultMinAge = minAge;
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
