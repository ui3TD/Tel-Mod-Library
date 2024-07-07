using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TraitFix
{

    [HarmonyPatch(typeof(data_girls), "AgeDeterioration")]
    public class Data_girls_AgeDeterioration
    {

        // Girls with Live Fast trait have double the rate of stat decreases after their peak age
        public static void Postfix()
        {
            foreach (data_girls.girls girls in data_girls.girl)
            {
                if (girls.status != data_girls._status.graduated)
                {
                    if (girls.trait == traits._trait._type.Live_fast)
                    {
                        girls.AgeDeterioration();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Birthday_Popup), "DoParam")]
    public class Birthday_Popup_DoParam
    {
        // Girls with Live Fast trait have double the rate of stat decreases after their peak age
        //public static bool Prefix(float Delay, data_girls._paramType Prm, ref Birthday_Popup __instance)
        //{

        //    int age = __instance.Girl.GetAge();
        //    int yrToPeak = age - __instance.Girl.peakAge;

        //    if (__instance.Girl.trait == traits._trait._type.Live_fast && yrToPeak > 0 && Prm != data_girls._paramType.funny && Prm != data_girls._paramType.smart)
        //    {
        //        float paramVal = __instance.Girl.getParam(Prm).val;
        //        float newVal = paramVal * (0.95f - 0.005f * (float)yrToPeak);
        //        if (newVal > 100f)
        //        {
        //            newVal = 100f;
        //        }
        //        else if (newVal < 1f)
        //        {
        //            newVal = 1f;
        //        }
        //        __instance.Girl.getParam(Prm).setVal(newVal);
        //        if (age < 19 && (Prm == data_girls._paramType.cool || Prm == data_girls._paramType.sexy))
        //        {
        //            paramVal = data_girls.SexyOrCool(age - 1, paramVal);
        //            newVal = data_girls.SexyOrCool(age, newVal);
        //        }
        //        else if (age < 19 && Prm == data_girls._paramType.cute)
        //        {
        //            paramVal = data_girls.GetCuteVal(age - 1, paramVal);
        //            newVal = data_girls.GetCuteVal(age, newVal);
        //        }

        //        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.prefab_stat);
        //        gameObject.GetComponent<Birthday_Stat>().Set(Prm, paramVal, newVal, Delay);
        //        gameObject.transform.SetParent(__instance.Stat_Container.transform, false);

        //        return false;
        //    }

        //    return true;
        //}
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            int index = -1;
            bool breakFlag = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (breakFlag && instructionList[i].opcode == OpCodes.Stloc_1)
                {
                    index = i;
                    break;
                }
                if (instructionList[i].opcode == OpCodes.Ldc_R4 && (float)instructionList[i].operand == 0.975f)
                {
                    breakFlag = true;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_1));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Ldloc_0));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Birthday_Popup_DoParam), "Infix")));
                instructionList.Insert(index + 5, new CodeInstruction(OpCodes.Stloc_1));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(Birthday_Popup __this, float newVal, float orig)
        {
            float output = orig;
            if(__this.Girl.trait == traits._trait._type.Live_fast)
            {
                output = 2 * newVal - orig;
            }
            return output;
        }
    }


    [HarmonyPatch(typeof(data_girls.girls), "GetAppealOfStat")]
    public class Data_girls_girls_GetAppealOfStat
    {
        // Girls with Trendy trait have 1.5x the appeal to non-adults and 0.5x the appeal to adults.
        public static void Postfix(ref float __result, resources.fanType _FanType, data_girls.girls __instance)
        {
            float output = __result;
            if (__instance.trait == traits._trait._type.Trendy)
            {
                if (_FanType == resources.fanType.adult)
                {
                    output *= 0.5f;
                }
                else if(_FanType == resources.fanType.youngAdult)
                {
                    output *= 1.5f;
                }
                else if (_FanType == resources.fanType.teen)
                {
                    output *= 1.5f;
                }
            }
            __result = output;
        }
    }

    [HarmonyPatch(typeof(data_girls.girls), "UpdateDatingStatus")]
    public class Data_girls_girls_UpdateDatingStatus
    {

        // If there is an Indiscreet member, girls in dating relationships unknown to the player have a 2% chance
        // of having the relationship revealed each week
        public static void Postfix(ref data_girls.girls __instance)
        {
            if (mainScript.chance(2) && __instance.DatingData.Is_Taken() && !__instance.DatingData.Is_Partner_Status_Known)
            {
                bool leaker = false;
                foreach (data_girls.girls girls in data_girls.GetActiveGirls(null))
                {
                    if (girls != __instance && girls.trait == traits._trait._type.Indiscreet)
                    {
                        leaker = true;
                    }
                }
                if (leaker)
                {
                    if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_forbidden)
                    {
                        NotificationManager.AddNotification(Language.Insert("IDOL__OUTSIDE_LEAK_SCANDAL", new string[]
                        {
                            __instance.GetName(true)
                        }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                        __instance.addParam(data_girls._paramType.scandalPoints, 1f, false);
                    }
                    else
                    {
                        NotificationManager.AddNotification(Language.Insert("IDOL__OUTSIDE_LEAK", new string[]
                        {
                            __instance.GetName(true)
                        }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                    }
                    __instance.getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                    __instance.DatingData.Is_Partner_Status_Known = true;
                    __instance.DatingData.Partner_Status_Known_To_Player = __instance.DatingData.Partner_Status;
                }
            }
            return;
        }
    }

    [HarmonyPatch(typeof(Relationships._relationship), "CheckDating")]
    public class Relationships__relationship_CheckDating
    {
        // If there is an Indiscreet member, girls in dating relationships unknown to the player have a 2% chance
        // of having the relationship revealed each week
        public static void Postfix(ref bool __result, ref Relationships._relationship __instance)
        {
            bool output = __result;

            if (mainScript.chance(2) && __instance.Dating && !__instance.IsRelationshipKnown())
            {
                bool leaker = false;
                foreach (data_girls.girls girls in data_girls.GetActiveGirls(null))
                {
                    if (girls != __instance.Girls[0] && girls != __instance.Girls[1] && girls.trait == traits._trait._type.Indiscreet)
                    {
                        leaker = true;
                    }
                }
                if (leaker)
                {
                    if (policies.GetSelectedPolicyValue(policies._type.dating).Value == policies._value.dating_forbidden)
                    {
                        __instance.Girls[0].addParam(data_girls._paramType.scandalPoints, 1f, false);
                        __instance.Girls[1].addParam(data_girls._paramType.scandalPoints, 1f, false);
                        NotificationManager.AddNotification(Language.Insert("IDOL__INSIDE_LEAK_SCANDAL", new string[]
                        {
                            __instance.Girls[0].GetName(true),
                            __instance.Girls[1].GetName(true)
                        }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                    }
                    else
                    {
                        NotificationManager.AddNotification(Language.Insert("IDOL__INSIDE_LEAK", new string[]
                        {
                            __instance.Girls[0].GetName(true),
                            __instance.Girls[1].GetName(true)
                        }), mainScript.red32, NotificationManager._notification._type.idol_relationship_change);
                    }
                    __instance.Girls[0].getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                    __instance.Girls[1].getParam(data_girls._paramType.mentalStamina).add(-30f, false);
                    __instance.Girls[0].DatingData.Is_Partner_Status_Known = true;
                    __instance.Girls[1].DatingData.Is_Partner_Status_Known = true;
                    __instance.Girls[0].DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
                    __instance.Girls[1].DatingData.Partner_Status_Known_To_Player = data_girls.girls._dating_data._partner_status.taken_idol;
                }
            }
            __result = output;
        }
    }

    // Maternal and Precocious default relationship is positive based on age criteria
    [HarmonyPatch(typeof(Relationships._relationship), "Initialize")]
    public class Relationships__relationship_Initialize
    {
        public static void Postfix(ref Relationships._relationship __instance)
        {
            int age = __instance.Girls[0].GetAge();
            int age2 = __instance.Girls[1].GetAge();
            if (__instance.Girls[0].trait == traits._trait._type.Maternal && age > age2)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[1].trait == traits._trait._type.Maternal && age2 > age)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[0].trait == traits._trait._type.Precocious && age < age2)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
            else if (__instance.Girls[1].trait == traits._trait._type.Precocious && age2 < age)
            {
                __instance.Dynamic = Relationships._relationship._dynamic.positive;
            }
        }
    }

    // Maternal and Precocious default relationship is positive based on age criteria
    [HarmonyPatch(typeof(Relationships), "Do_Dynamic")]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if (relationship.Dynamic == Relationships._relationship._dynamic.positive)
                {
                    if (relationship.Girls[0].trait == traits._trait._type.Maternal && relationship.Girls[0].GetAge() > relationship.Girls[1].GetAge())
                    {
                        relationship.Add(0.3f);
                    }
                    else if (relationship.Girls[1].trait == traits._trait._type.Maternal && relationship.Girls[1].GetAge() > relationship.Girls[0].GetAge())
                    {
                        relationship.Add(0.3f);
                    }
                    if (relationship.Girls[0].trait == traits._trait._type.Precocious && relationship.Girls[0].GetAge() < relationship.Girls[1].GetAge())
                    {
                        relationship.Add(0.3f);
                    }
                    else if (relationship.Girls[1].trait == traits._trait._type.Precocious && relationship.Girls[1].GetAge() < relationship.Girls[0].GetAge())
                    {
                        relationship.Add(0.3f);
                    }
                }
                singles._single single = singles.GetLatestReleasedSingle(false, relationship.Girls[0].GetGroup());
                singles._single single2 = singles.GetLatestReleasedSingle(false, relationship.Girls[1].GetGroup());
                singles._single single3 = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
                if (single == null)
                {
                    single = single3;
                }
                if (single2 == null)
                {
                    single2 = single3;
                }

                data_girls.girls center1 = null;
                data_girls.girls center2 = null;
                data_girls.girls center3 = null;
                if (single != null)
                {
                    center1 = single.GetCenter();
                }
                if (single2 != null)
                {
                    center2 = single2.GetCenter();
                }
                if (single3 != null)
                {
                    center3 = single3.GetCenter();
                }
                if (center1 != null || center2 != null)
                {
                    if ((center1 == relationship.Girls[0] || center3 == relationship.Girls[0]) && relationship.Girls[0].trait == traits._trait._type.Arrogant)
                    {
                        relationship.Add(-0.5f);
                    }
                    else if ((center2 == relationship.Girls[1] || center3 == relationship.Girls[1]) && relationship.Girls[1].trait == traits._trait._type.Arrogant)
                    {
                        relationship.Add(-0.5f);
                    }
                }
            }
        }
    }

    // Girls with Forgiving trait will never dislike or hate any other girls
    [HarmonyPatch(typeof(Relationships._relationship), "Recalc")]
    public class Relationships__relationship_Recalc
    {
        public static void Postfix(ref Relationships._relationship __instance)
        {
            if (__instance.Ratio < 0.5f)
            {
                if ((__instance.Girls[0].trait == traits._trait._type.Forgiving || __instance.Girls[1].trait == traits._trait._type.Forgiving))
                {
                    __instance.Ratio = 0.5f;
                }
            }
        }
    }

    // Girls with Meme Queen trait get +10 to all stats for internet shows
    [HarmonyPatch(typeof(Shows._show), "AddCastParam")]
    public class Shows__show_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, ref Shows._show __instance)
        {
            if(type != data_girls._paramType.teamChemistry)
            {
                float output = 0;
                float counter = 0;
                float sum = 0f;
                foreach (data_girls.girls girls in girlList)
                {
                    if (girls != null)
                    {
                        counter++;
                        if (girls.trait == traits._trait._type.Meme_queen)
                        {
                            if (__instance.medium.media_type == Shows._param._media_type.internet)
                            {
                                sum += 10f;
                            }
                        }
                    }
                }
                if (counter > 0)
                {
                    output = sum / counter;
                }
                float val = __instance.girlParams.Last().val + output;
                __instance.girlParams.Last().val = val;
            }    
        }
    }

    // Girls with Meme Queen trait get +10 to all stats for internet shows
    [HarmonyPatch(typeof(Show_Popup), "AddCastParam")]
    public class Show_Popup_AddCastParam
    {
        public static void Postfix(data_girls._paramType type, List<data_girls.girls> girlList, ref Show_Popup __instance)
        {
            Shows._param medium = Traverse.Create(__instance).Field("medium").GetValue() as Shows._param;
            List<data_girls.girls.param> girlParams = Traverse.Create(__instance).Field("girlParams").GetValue() as List<data_girls.girls.param>;

            if (type != data_girls._paramType.teamChemistry)
            {
                float output = 0;
                float counter = 0;
                float sum = 0f;
                foreach (data_girls.girls girls in girlList)
                {
                    if (girls != null)
                    {
                        counter++;
                        if (girls.trait == traits._trait._type.Meme_queen)
                        {
                            if (medium.media_type == Shows._param._media_type.internet)
                            {
                                sum += 10f;
                            }
                        }
                    }
                }
                if (counter > 0)
                {
                    output = sum / counter;
                }
                float val = girlParams.Last().val + output;
                girlParams.Last().val = val;
                Traverse.Create(__instance).Field("girlParams").SetValue(girlParams);
            }
        }
    }

    // Update shows on medium selection just in case of meme queen
    [HarmonyPatch(typeof(Show_Popup), "SetParam")]
    public class Show_Popup_SetParam
    {
        public static void Postfix(ref Show_Popup __instance)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            Shows._show._castType? castType = Traverse.Create(__instance).Field("castType").GetValue() as Shows._show._castType?;
            if (castType != null)
            {
                __instance.SetCastType(castType.Value);
            }
        }
    }

    // Girls with Meme Queen trait get +10% success rate and +5% crit success rate when participating in viral marketing campaigns
    [HarmonyPatch(typeof(singles._param), "GetSuccessChance", new Type[] { typeof(Single_Marketing_Roll._result), typeof(int), typeof(singles._single) })]
    public class Singles__param_GetSuccessChance
    {
        public static void Postfix(ref float __result, singles._param __instance, Single_Marketing_Roll._result Result, singles._single Single)
        {
            float output = __result;
            if (Single != null && __instance.Special_Type == singles._param._special_type.viral_campaign)
            {
                bool flag = false;
                foreach (data_girls.girls girls in Single.girls)
                {
                    if (girls != null && girls.trait == traits._trait._type.Meme_queen)
                    {
                        flag = true;
                    }
                }
                if (flag)
                {
                    if (Result == Single_Marketing_Roll._result.success)
                    {
                        output += 10;
                    }
                    else if (Result == Single_Marketing_Roll._result.success_crit)
                    {
                        output += 5;
                    }
                    else if (Result == Single_Marketing_Roll._result.fail)
                    {
                        output -= 15;
                    }
                    __result = output;
                }
            }
        }
    }

    // Girls with Annoying trait cause other members to spend 1.2x physical stamina in shows
    [HarmonyPatch(typeof(Shows._show), "SetStamina")]
    public class Shows__show_SetStamina
    {
        public static void Postfix(Shows._show __instance)
        {
            List<data_girls.girls> cast = __instance.GetCast();
            float staminaCost = __instance.GetStaminaCost();
            int annoyCount = 0;
            foreach (data_girls.girls girls in cast)
            {
                if (girls != null && girls.trait == traits._trait._type.Annoying && girls.IsActive())
                {
                    annoyCount++;
                }
            }
            if(annoyCount > 0)
            {
                foreach (data_girls.girls girls2 in cast)
                {
                    if (girls2.IsActive())
                    {
                        if (annoyCount == 1)
                        {
                            if (girls2.trait != traits._trait._type.Annoying)
                            {
                                girls2.addParam(data_girls._paramType.physicalStamina, -staminaCost * 0.2f, false);
                            }
                        }
                        else if (annoyCount > 1)
                        {
                            girls2.addParam(data_girls._paramType.physicalStamina, -staminaCost * 0.2f, false);
                        }
                    }
                }
            }
        }
    }

    // Girls with Misandry trait have a 20% chance of receiving bad opinions from Male fans when participating in a single with handshakes
    [HarmonyPatch(typeof(singles), "ReleaseSingle")]
    public class Singles_ReleaseSingle
    {
        public static void Postfix(singles._single single)
        {
            foreach (data_girls.girls girls in single.girls)
            {
                if (girls != null)
                {
                    if (girls.trait == traits._trait._type.Misandry && single.IsGroupHS() && mainScript.chance(20))
                    {
                        girls.AddAppeal(resources.fanType.male, -1f);
                    }
                    else if (girls.trait == traits._trait._type.Misandry && single.IsIndividualHS() && mainScript.chance(20))
                    {
                        girls.AddAppeal(resources.fanType.male, -1f);
                    }
                }
            }
        }
    }

    // Girls with Perfectionist trait get -20 to mental stamina when world tours end with less than 80% average attendance
    [HarmonyPatch(typeof(SEvent_Tour), "FinishTour")]
    public class SEvent_Tour_FinishTour
    {
        public static void Postfix(SEvent_Tour __instance)
        {
            List<data_girls.girls> activeGirls = data_girls.GetActiveGirls(null);
            foreach (data_girls.girls girls in activeGirls)
            {
                if (girls.trait == traits._trait._type.Perfectionist && __instance.Tour.GetAverageAttendance() < 80)
                {
                    girls.getParam(data_girls._paramType.mentalStamina).add(-20f, false);
                }
            }
        }
    }

    // Girls with Perfectionist trait get -20 to mental stamina when they participate in concerts with less than 100% hype.
    [HarmonyPatch(typeof(SEvent_Concerts._concert), "Finish")]
    public class SEvent_Concerts__concert_Finish
    {
        public static void Postfix(SEvent_Concerts._concert __instance)
        {
            foreach (data_girls.girls girls in __instance.GetGirls(true))
            {
                if (girls.trait == traits._trait._type.Perfectionist && __instance.Hype < 100f)
                {
                    girls.getParam(data_girls._paramType.mentalStamina).add(-20f, false);
                }
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
            TraitsFix.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix(data_girls.girls _girl, ref float __result, business._proposal __instance)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            // Girls with Photogenic trait have +100% to photoshoots
            if (__instance.type == business._type.photoshoot && _girl.trait == traits._trait._type.Photogenic)
            {
                __result += 1f;
            }

            TraitsFix.patchGetVal = false;
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
            TraitsFix.patchGetVal = true;
            TraitsFix.girlList = Girls;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsFix.patchGetVal = false;
            TraitsFix.girlList = null;
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
            TraitsFix.patchGetVal = true;
            TraitsFix.girlList = _girls;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsFix.patchGetVal = false;
            TraitsFix.girlList = null;
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
            TraitsFix.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsFix.patchGetVal = false;
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
            TraitsFix.patchGetVal = true;
            return true;
        }

        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            TraitsFix.patchGetVal = false;
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
            TraitsFix.patchGetVal = true;
            return true;
        }


        [HarmonyPriority(Priority.VeryLow)]
        public static void Postfix()
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);

            TraitsFix.patchGetVal = false;
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

    // Apply traits to parameters and skills
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (TraitsFix.patchGetVal)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                __result += TraitsFix.GetTraitModifier(__instance.Parent, __instance.type, TraitsFix.girlList);
            }
        }
    }


    public class TraitsFix
    {
        public static bool patchGetVal = false;
        public static bool patchSetVal = false;
        public static bool patchSet = false;

        public static List<data_girls.girls> girlList = null;

        // This method calculates the modifier to girl parameters based on their trait.
        public static int GetTraitModifier(data_girls.girls girls, data_girls._paramType type, List<data_girls.girls> allGirls = null)
        {
            //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
            float num = 0;
            if (girls != null && data_girls.IsStatParam(type))
            {
                int ind = 0;
                foreach (SEvent_Tour.tour tour in SEvent_Tour.Tours)
                {
                    if (tour.Status == SEvent_Tour.tour._status.normal)
                    {
                        ind++;
                    }
                }
                foreach (SEvent_SSK._SSK sSK in SEvent_SSK.Elections)
                {
                    if (sSK.Status == SEvent_Tour.tour._status.normal)
                    {
                        ind++;
                    }
                }
                foreach (SEvent_Concerts._concert concert in SEvent_Concerts.Concerts)
                {
                    if (concert.Status == SEvent_Tour.tour._status.normal)
                    {
                        ind++;
                    }
                }
                if (girls.trait == traits._trait._type.Anxiety && ind > 0)
                {
                    num -= 10f;
                }
                if (girls.trait == traits._trait._type.Clumsy && type == data_girls._paramType.dance)
                {
                    num -= 30f;
                }
                if (girls.trait == traits._trait._type.Clumsy && type == data_girls._paramType.funny)
                {
                    num += 30f;
                }
                if (girls.trait == traits._trait._type.Worrier && resources.GetScandalPointsTotal() > 0L)
                {
                    num -= 20f;
                }
                singles._single single = singles.GetLatestReleasedSingle(false, girls.GetGroup());
                singles._single single2 = singles.GetLatestReleasedSingle(false, Groups.GetMainGroup());
                singles._single single3 = null;
                data_girls.girls center1 = null;
                data_girls.girls center2 = null;
                if (single == null)
                {
                    single = single2;
                }
                if (single != null)
                {
                    center1 = single.GetCenter();
                    if(single2 != null)
                    {
                        center2 = single2.GetCenter();
                        if (single.ReleaseData.ReleaseDate > single2.ReleaseData.ReleaseDate)
                        {
                            single3 = single;
                        }
                        else if (single.ReleaseData.ReleaseDate < single2.ReleaseData.ReleaseDate)
                        {
                            single3 = single2;
                        }
                        else if (single.ReleaseData.Sales > single2.ReleaseData.Sales)
                        {
                            single3 = single;
                        }
                        else
                        {
                            single3 = single2;
                        }
                    }
                    else
                    {
                        single3 = single;
                    }
                }
                if (single != null && girls.trait == traits._trait._type.Complacent && (girls == center1 || girls == center2) && (type == data_girls._paramType.vocal || type == data_girls._paramType.dance))
                {
                    num -= 20f;
                }
                if (single3 != null && (staticVars.dateTime - single3.ReleaseData.ReleaseDate).Days >= staticVars.dateTime.Day && single3.ReleaseData.Chart_Position != 1)
                {
                    if (girls.trait == traits._trait._type.Defeatist)
                    {
                        num -= 20f;
                    }
                    else if (girls.trait == traits._trait._type.Underdog)
                    {
                        num += 20f;
                    }
                }
                if (allGirls != null && girls.trait == traits._trait._type.Lone_Wolf)
                {
                    int count = 0;
                    foreach (data_girls.girls girls1 in allGirls)
                    {
                        if (girls1 != null) count++;
                    }
                    if (count == 1)
                    {
                        num += 40f;
                    }
                }
            }
            return (int)num;
        }

    }

}
