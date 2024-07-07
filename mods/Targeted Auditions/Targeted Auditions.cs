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
            GameObject scrollContainer = new("ScrollContainer", typeof(RectTransform), typeof(ScrollRect));
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
        public static void Prefix(Auditions __instance)
        {
            // Set audition age limits (only if popup is not used)
            bool toggle = int.Parse(variables.Get("AuditionAgeLimit_TogglePopup") ?? "0") == 1;
            if (!toggle)
            {
                minAge = int.Parse(variables.Get("AuditionAgeLimit_MinAge") ?? "12");
                maxAge = int.Parse(variables.Get("AuditionAgeLimit_MaxAge") ?? "23");
                if (maxAge < minAge)
                {
                    // swap values
                    maxAge = int.Parse(variables.Get("AuditionAgeLimit_MinAge") ?? "23");
                    minAge = int.Parse(variables.Get("AuditionAgeLimit_MaxAge") ?? "12");

                    // correct default variables
                    defaultMaxAge = maxAge;
                    defaultMinAge = minAge;
                    variables.Set("AuditionAgeLimit_MaxAge", maxAge.ToString());
                    variables.Set("AuditionAgeLimit_MaxAge", minAge.ToString());
                }
            }

            // Set sexual orientation
            float varLesbian = float.Parse(variables.Get("CustomAudition_Gay") ?? "7");
            float varBi = float.Parse(variables.Get("CustomAudition_Bi") ?? "14");

            if (varLesbian + varBi > 100)
            {
                varLesbian = Mathf.Floor(varLesbian / (varLesbian + varBi) * 100);
                varBi = 100 - varLesbian;
                variables.Set("CustomAudition_Gay", varLesbian.ToString());
                variables.Set("CustomAudition_Bi", varBi.ToString());
            }
            chanceLesbian = (int)varLesbian;
            chanceBi = (int)Mathf.Floor(varBi / (100 - chanceLesbian) * 100);


            // Set stat priorities
            foreach (data_girls._paramType param in paramTypes)
            {
                int value = int.Parse(variables.Get($"CustomAudition_Prio_{param}") ?? "50");
                priorityDict[param] = value;
            }

            // Set girl count
            __instance.NumberOfGirls = int.Parse(variables.Get("CustomAudition_Count") ?? "5");


        }
    }

    /// <summary>
    /// Patches the data_girls class to apply girl sexuality.
    /// </summary>
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        /// <summary>
        /// Postfix method to set the sexuality of a generated girl.
        /// </summary>
        /// <param name="__result">The generated girl data.</param>
        public static void Postfix(ref data_girls.girls __result)
        {

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
            DateTime dateTime = staticVars.dateTime.AddYears(-maxAge - 1).AddYears(UnityEngine.Random.Range(0, maxAge - minAge + 1)).AddMonths(UnityEngine.Random.Range(0, 12)).AddDays((double)UnityEngine.Random.Range(0, 31));
            __instance.SetBirthday(dateTime);
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
            bool toggle = int.Parse(variables.Get("AuditionAgeLimit_TogglePopup") ?? "0") == 1;
            if (toggle)
            {
                defaultMinAge = int.Parse(variables.Get("AuditionAgeLimit_MinAge") ?? "12");
                defaultMaxAge = int.Parse(variables.Get("AuditionAgeLimit_MaxAge") ?? "23");
                if (defaultMaxAge < defaultMinAge)
                {
                    // swap values
                    defaultMaxAge = int.Parse(variables.Get("AuditionAgeLimit_MinAge") ?? "23");
                    defaultMinAge = int.Parse(variables.Get("AuditionAgeLimit_MaxAge") ?? "12");

                    // correct variables
                    variables.Set("AuditionAgeLimit_MaxAge", maxAge.ToString());
                    variables.Set("AuditionAgeLimit_MaxAge", minAge.ToString());
                }
                agePopup = true;
                Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().Open(PopupManager._type.staff_nickname, true);
            }
        }
    }

}
