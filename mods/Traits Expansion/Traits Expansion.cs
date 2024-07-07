using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using static TraitsExpansion.TraitsExpansion;
using System.Reflection.Emit;
using System.Reflection;
using SimpleJSON;

namespace TraitsExpansion
{

    [HarmonyPatch(typeof(traits), "GetTraitType")]
    public class traits_GetTraitType
    {
        public static void Postfix(string str, ref traits._trait._type __result)
        {
            try
            {
                __result = (traits._trait._type)Enum.Parse(typeof(NewTraits), str);
                Debug.Log("Custom trait found: " + str);
            }
            catch { }
        }
    }

    // Load traits from unique idols
    [HarmonyPatch(typeof(data_girls_textures), "LoadAssetsData", new[] {typeof(string)})]
    public class data_girls_textures_LoadAssetsData
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object textureAssetOperand = null;
            object jsonNodeOperand = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 8)
                {
                    textureAssetOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Ldloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 13)
                {
                    jsonNodeOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Pop)
                {
                    index = i;
                }
                if (instructionList[i].opcode == OpCodes.Ldstr && (string)instructionList[i].operand == "Trait not found: ")
                {
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, textureAssetOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, jsonNodeOperand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), "Infix")));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Stloc_S, textureAssetOperand));
            }

            return instructionList.AsEnumerable();
        }

        public static data_girls_textures._textureAsset Infix(data_girls_textures._textureAsset textureAsset, JSONNode jsonnode)
        {
            traits._trait._type getTrait = textureAsset.Trait;
            try
            {
                getTrait = (traits._trait._type)Enum.Parse(typeof(NewTraits), jsonnode["trait"]);
            }
            catch { }
            
            textureAsset.Trait = getTrait;

            return textureAsset;
        }
    }



    // World tours give 1.2x more fans for each multilingual member
    [HarmonyPatch(typeof(SEvent_Tour.tour), "GetNewFansByAttendance")]
    public class SEvent_Tour_tour_GetNewFansByAttendance
    {
        public static void Postfix(ref int __result)
        {
            int count = 0;
            foreach (data_girls.girls girl in data_girls.GetActiveGirls())
            {
                if (girl != null && girl.trait == (traits._trait._type)NewTraits.Polyglot)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                __result = Mathf.RoundToInt(__result * (1 + 0.2f * count));
            }
        }
    }

    // Old Money is always at 200% salary satisfaction
    [HarmonyPatch(typeof(data_girls.girls), "GetSalarySatisfaction")]
    public class data_girls_girls_GetSalarySatisfaction
    {
        [HarmonyPriority(Priority.LowerThanNormal)]
        public static void Postfix(ref float __result, data_girls.girls __instance)
        {
            if (__instance.trait == (traits._trait._type)NewTraits.Old_Money)
            {
                __result = 2f;
            }
        }
    }

    // Old Money doesn't lose mental stamina on low salary
    [HarmonyPatch(typeof(data_girls), "CheckLowSalaries")]
    public class data_girls_CheckLowSalaries
    {
        public static bool Prefix()
        {
            TraitsExpansion.patchAddParam = true;
            return true;
        }
        public static void Postfix()
        {
            TraitsExpansion.patchAddParam = false;
        }
    }

    // Apply Old Money effects to mental decrease
    [HarmonyPatch(typeof(data_girls.girls), "addParam")]
    public class data_girls_girls_addParam
    {
        public static bool Prefix(data_girls.girls __instance, data_girls._paramType type, ref float val)
        {
            if (TraitsExpansion.patchAddParam)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                if (__instance.trait == (traits._trait._type)NewTraits.Old_Money && type == data_girls._paramType.mentalStamina)
                {
                    val = 0;
                }
            }
            return true;
        }
    }

    // appeal adjustments for traits
    [HarmonyPatch(typeof(data_girls.girls), "GetAppealOfStat")]
    public class Data_girls_girls_GetAppealOfStat
    {
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            if (__instance.trait == (traits._trait._type)NewTraits.Fashionista)
            {
                if (_FanType == resources.fanType.female)
                {
                    __result *= 1.2f;
                }
            }
            else if (__instance.trait == (traits._trait._type)NewTraits.Flirty)
            {
                if (_FanType == resources.fanType.male)
                {
                    __result *= 1.2f;
                }
            }
            else if (__instance.trait == (traits._trait._type)NewTraits.Idol_Otaku)
            {
                if (_FanType == resources.fanType.hardcore)
                {
                    __result *= 1.2f;
                }
            }
        }
    }

    // Sadistic bullying
    [HarmonyPatch(typeof(Relationships), "Do_Bullying")]
    public class Relationships_Do_Bullying
    {
        public static void Postfix()
        {
            int sadistic;
            foreach (Relationships._clique clique in Relationships.Cliques)
            {
                sadistic = 0;
                foreach(data_girls.girls member in clique.Members)
                {
                    if(member.trait == (traits._trait._type)NewTraits.Sadistic && clique.IsBullied(member))
                    {
                        sadistic++;
                    }
                }
                if(sadistic > 0)
                {
                    foreach (data_girls.girls girls in clique.Bullied_Girls)
                    {
                        girls.getParam(data_girls._paramType.mentalStamina).add(-10f, false);
                        if (clique.KnownBulliedGirls.Contains(girls))
                        {
                            NotificationManager.AddNotification(girls.GetName(true) + Language.Insert("REL__BULLYING_LOST", new string[]
                            {
                        "10"
                            }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                        }
                        else
                        {
                            NotificationManager.AddNotification(Language.Insert("IDOL__BULLIED_UNKNOWN", new string[]
                            {
                        "10"
                            }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                        }
                    }
                }
            }
        }
    }

    // Job Hopper graduation date within the next year
    [HarmonyPatch(typeof(data_girls.girls), "Graduation_Set_Default_Date")]
    public class data_girls_girls_Graduation_Set_Default_Date
    {
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (__instance.trait == (traits._trait._type)NewTraits.Job_Hopper)
            {
                __instance.Graduation_Date = staticVars.dateTime.AddDays((double)UnityEngine.Random.Range(100, 365));
            }
        }
    }

    // Aerophobic stamina penalty
    [HarmonyPatch(typeof(SEvent_Tour), "UseStamina")]
    public class SEvent_Tour_UseStamina
    {
        public static void Postfix()
        {
            foreach (data_girls.girls girls in data_girls.GetActiveGirls())
            {
                if (girls != null && girls.trait == (traits._trait._type)NewTraits.Aerophobia)
                {
                    girls.addParam(data_girls._paramType.mentalStamina, -50);
                }
            }
        }
    }

    // Stage Fright stamina penalty
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Finish")]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            foreach (SEvent_Concerts._concert.ISetlistItem setlistItem in __instance.SetListItems)
            {
                foreach (data_girls.girls girls in setlistItem.GetGirls(true))
                {
                    if (girls != null && girls.trait == (traits._trait._type)NewTraits.Stage_Fright)
                    {
                        NotificationManager.AddNotification(Language.Insert("IDOL__STAGEFRIGHT", new string[]
                        {
                            girls.GetName(true)
                        }), mainScript.red32, NotificationManager._notification._type.idol_stat_change);
                        girls.addParam(data_girls._paramType.mentalStamina, -30);
                    }
                }
            }
        }
    }

    // Cult Leader vote bonus
    [HarmonyPatch(typeof(SEvent_SSK._SSK), "GenerateResults")]
    public class SEvent_SSK__SSK_GenerateResults
    {
        public static bool Prefix()
        {
            TraitsExpansion.patchGetFan_Count = true;
            return true;
        }
        public static void Postfix()
        {
            TraitsExpansion.patchGetFan_Count = false;
        }
    }

    // Cult Leader vote bonus
    [HarmonyPatch(typeof(data_girls.girls), "GetFan_Count", new Type[] {typeof(resources.fanType)})]
    public class data_girls_girls_GetFan_Count
    {
        public static void Postfix(data_girls.girls __instance, ref long __result)
        {
            if(TraitsExpansion.patchGetFan_Count)
            {
                if(__instance.trait == (traits._trait._type)NewTraits.Cult_Leader)
                {
                    __result = (long)Mathf.Round(__result * 1.5f);
                }
            }
        }
    }


    // Thespian drama bonus (popup UI)
    [HarmonyPatch(typeof(Business_Popup), "Set")]
    public class Business_Popup_Set
    {
        public static bool Prefix(ref business._proposal _proposal, ref int __state)
        {
            __state = 0;
            if (_proposal.type == business._type.tv_drama && _proposal.girl.trait == (traits._trait._type)NewTraits.Thespian)
            {
                __state = _proposal.stamina;
                _proposal.stamina = Mathf.RoundToInt(_proposal.stamina / 2);
            }
            return true;
        }

        public static void Postfix(ref business._proposal _proposal, ref int __state)
        {
            if (__state != 0)
            {
                _proposal.stamina = __state;
            }

        }
    }

    // Thespian drama bonus (popup UI)
    [HarmonyPatch(typeof(business), "Accept")]
    public class business_Accept
    {
        public static bool Prefix(ref business __instance)
        {
            if (__instance.ActiveProposal.type == business._type.tv_drama && __instance.ActiveProposal.girl.trait == (traits._trait._type)NewTraits.Thespian)
            {
                __instance.ActiveProposal.stamina = Mathf.RoundToInt(__instance.ActiveProposal.stamina / 2);
            }
            return true;
        }
    }


    // Reckless
    [HarmonyPatch(typeof(data_girls.girls), "Try_Injury")]
    public class data_girls_girls_Try_Injury
    {
        public static void Postfix(data_girls.girls __instance)
        {
            if (__instance.trait == (traits._trait._type)NewTraits.Reckless && __instance.status != data_girls._status.injured)
            {
                if (__instance.room != null && __instance.room.type == agency._type.doctorsOffice)
                {
                    return;
                }
                float val = __instance.getParam(data_girls._paramType.physicalStamina).val;
                // 1% chance below 60
                if (val >= 60f)
                {
                    return;
                }
                float num;
                // 1.5% chance below 20
                if (val > 5f)
                {
                    num = 1f;
                }
                // 3% chance below 5
                else
                {
                    num = 2f;
                }
                if (mainScript.chance(num))
                {
                    __instance.Set_Injured();
                }
            }
        }
    }


    [HarmonyPatch(typeof(Profile_Popup), "RenderTab_Extras")]
    public class Profile_Popup_RenderTab_Extras
    {

        public static void Postfix(Profile_Popup __instance)
        {
            traits._trait trait = __instance.Girl.GetTrait();
            if(trait != null)
            {
                Debug.Log("Trait Type: " + ((int)trait.Type));
            }
        }
    }

    // Apply traits to businesses
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            // Girls with Wooden Acting receive penalty
            if (__instance.type == business._type.tv_drama && _girl.trait == (traits._trait._type)NewTraits.Wooden_Acting)
            {
                __result -= 0.2f;
            }
            // Girls with Wooden Acting receive penalty
            else if (__instance.type == business._type.variety && _girl.trait == (traits._trait._type)NewTraits.Quick_Wit)
            {
                __result += 0.5f;
            }
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = false;
        }
    }


    // Stat changes for shows
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(List<data_girls.girls> Girls)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            TraitsExpansion.girlList = Girls;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = false;
            TraitsExpansion.girlList = null;
        }
    }

    // Stat changes for shows (for team chemistray calc)
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(List<data_girls.girls> _girls)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            TraitsExpansion.girlList = _girls;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = false;
            TraitsExpansion.girlList = null;
        }
    }

    // Stat changes for singles
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = false;
        }
    }

    // Stat changes for concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = false;
        }
    }


    // Stat changes for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsExpansion.patchGetVal = true;
            return true;
        }


        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);

            TraitsExpansion.patchGetVal = false;
        }
    }

    // Limits for business proposal stats
    [HarmonyPatch(typeof(business._proposal), "GetGirlCoeff")]
    public class Business__proposal_GetGirlCoeff_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            // Girls with Photogenic trait have +100% to photoshoots
            float output = __result;
            if (output > 20f)
            {
                output = 20f;
            }
            else if (output < 0f)
            {
                output = 0f;
            }
            __result = output;
        }
    }


    // Limits for show stats
    [HarmonyPatch(typeof(data_girls), "GetAverageParam")]
    public class Data_girls_GetAverageParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            float output = __result;
            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }
            __result = output;
        }
    }


    // Limits for single senbatsu stats
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class Singles__single_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {

            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            if (__result.val < 0f)
            {
                __result.val = 0f;
            }
            else if (__result.val > 100f)
            {
                __result.val = 100f;
            }

        }
    }

    // Limits for show senbatsu stats
    [HarmonyPatch(typeof(Shows._show), "SenbatsuCalcParam")]
    public class Shows__show_SenbatsuCalcParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref data_girls.girls.param __result)
        {

            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            if (__result.val < 0f)
            {
                __result.val = 0f;
            }
            else if (__result.val > 100f)
            {
                __result.val = 100f;
            }

        }
    }

    // Limits for stats of concert songs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._song), "GetSkillValue")]
    public class SEvent_Concerts__concert__song_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            int output = __result;

            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }
            __result = output;

        }
    }


    // Limits for stats for concert MCs
    [HarmonyPatch(typeof(SEvent_Concerts._concert._mc), "GetSkillValue")]
    public class SEvent_Concerts__concert__mc_GetSkillValue_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref int __result)
        {
            int output = __result;

            if (output < 0)
            {
                output = 0;
            }
            else if (output > 100)
            {
                output = 100;
            }


            __result = output;
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Show_Popup __instance)
        {
            List<data_girls.girls.param> girlParams = Traverse.Create(__instance).Field("girlParams").GetValue() as List<data_girls.girls.param>;

            float val = girlParams.Last().val;
            if (val > 100)
            {
                val = 100;
            }
            else if (val < 0)
            {
                val = 0;
            }
            girlParams.Last().val = val;
            Traverse.Create(__instance).Field("girlParams").SetValue(girlParams);
        }
    }

    // Limit the show cast params
    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam_Limits
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Shows._show __instance)
        {
            float val = __instance.girlParams.Last().val;
            if (val > 100)
            {
                val = 100;
            }
            else if (val < 0)
            {
                val = 0;
            }
            __instance.girlParams.Last().val = val;
        }
    }

    // Limit team chemistry
    [HarmonyPatch(typeof(data_girls), "GetTeamChemistry")]
    public class data_girls_GetTeamChemistry_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (__result > 100)
            {
                __result = 100;
            }
            else if (__result < 0)
            {
                __result = 0;
            }
        }
    }


    // Apply traits to parameters and skills
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (TraitsExpansion.patchGetVal)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                __result += TraitsExpansion.GetTraitModifier(__instance.Parent, __instance.type, TraitsExpansion.girlList);
            }
        }
    }

    public class TraitsExpansion
    {
        public static bool patchGetVal = false;

        public static List<data_girls.girls> girlList = null;

        public static bool patchAddParam = false;

        public static bool patchGetFan_Count = false;

        // This method calculates the modifier to girl parameters based on their trait.
        public static int GetTraitModifier(data_girls.girls girls, data_girls._paramType type, List<data_girls.girls> _1 = null)
        {
            float num = 0;
            if (girls != null && data_girls.IsStatParam(type))
            {
                if (girls.trait == (traits._trait._type)NewTraits.Perfect_Pitch && type == data_girls._paramType.vocal)
                {
                    num += 50;
                }
                else if (girls.trait == (traits._trait._type)NewTraits.Beauty_Guru && type == data_girls._paramType.pretty)
                {
                    num += 30;
                }
                else if (girls.trait == (traits._trait._type)NewTraits.Mensa_Member && type == data_girls._paramType.smart)
                {
                    num += 50;
                }
                else if (girls.trait == (traits._trait._type)NewTraits.Well_Endowed && type == data_girls._paramType.sexy)
                {
                    num += 30;
                }
                else if (girls.trait == (traits._trait._type)NewTraits.Homely && (type == data_girls._paramType.pretty || type == data_girls._paramType.cute || type == data_girls._paramType.cool || type == data_girls._paramType.sexy))
                {
                    num -= 10;
                }
                else if (girls.trait == (traits._trait._type)NewTraits.Tone_Deaf && type == data_girls._paramType.vocal)
                {
                    num -= 30;
                }
            }
            return (int)num;
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
