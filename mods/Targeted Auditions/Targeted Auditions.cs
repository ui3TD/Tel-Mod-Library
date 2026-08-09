using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// Calls Infix on the stat list right after the game shuffles it.
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            for (int i = 1; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Call &&
                    instructionList[i].operand is System.Reflection.MethodInfo method &&
                    method.DeclaringType == typeof(ExtensionMethods) &&
                    method.Name == nameof(ExtensionMethods.Shuffle) &&
                    StoreOf(instructionList[i - 1]) != null)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                Debug.LogWarning("[Targeted Auditions] data_girls.GenerateParams: stat shuffle not found; skill priorities are off.");
                return instructionList;
            }

            CodeInstruction loadList = instructionList[index - 1];
            instructionList.InsertRange(index + 1, new[]
            {
                new CodeInstruction(loadList.opcode, loadList.operand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_GenerateParams), nameof(Infix))),
                StoreOf(loadList),
            });

            return instructionList;
        }

        /// <summary>
        /// The instruction that stores to the local this instruction loads, or null if it doesn't load a local.
        /// </summary>
        private static CodeInstruction StoreOf(CodeInstruction load)
        {
            if (load.opcode == OpCodes.Ldloc_0) return new CodeInstruction(OpCodes.Stloc_0);
            if (load.opcode == OpCodes.Ldloc_1) return new CodeInstruction(OpCodes.Stloc_1);
            if (load.opcode == OpCodes.Ldloc_2) return new CodeInstruction(OpCodes.Stloc_2);
            if (load.opcode == OpCodes.Ldloc_3) return new CodeInstruction(OpCodes.Stloc_3);
            if (load.opcode == OpCodes.Ldloc_S) return new CodeInstruction(OpCodes.Stloc_S, load.operand);
            if (load.opcode == OpCodes.Ldloc) return new CodeInstruction(OpCodes.Stloc, load.operand);
            return null;
        }

        /// <summary>
        /// Reassigns the generated stat values according to Targeted Auditions priorities.
        /// Only during auditions; other generated girls (unique idols, rivals) keep the game's order.
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

        public static void LoadConfiguredAgeRange()
        {
            // The former per-audition age popup is retired. Always use the Mod Menu range.
            variables.Set(VARID_AGELIMIT_POPUP_TOGGLE, DEF_AGELIMIT_POPUP_TOGGLE);

            minAge = ReadInt(VARID_MINAGE, DEF_MINAGE_STR);
            maxAge = ReadInt(VARID_MAXAGE, DEF_MAXAGE_STR);
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
