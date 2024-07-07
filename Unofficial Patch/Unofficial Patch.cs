﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnofficialPatch
{

    [HarmonyPatch(typeof(Profile_Fans_Pies), "Render_Pies")]
    public class Profile_Fans_Pies_Render_Pies
    {

        // Fixed fan pie charts so that it is correct
        public static void Postfix(Profile_Fans_Pies __instance)
        {
            //NotificationManager.AddNotification("postfix applied", mainScript.green32, NotificationManager._notification._type.other);
            data_girls.girls Girl = Traverse.Create(__instance).Field("Girl").GetValue() as data_girls.girls;

            if (Girl.GetFans_Total(null) != 0L)
            {
                __instance.Fans_Pie_Adult.GetComponent<Image>().fillAmount += __instance.Fans_Pie_YA.GetComponent<Image>().fillAmount - __instance.Fans_Pie_Teen.GetComponent<Image>().fillAmount;
            }
        }

    }

    [HarmonyPatch(typeof(Tour_New_Popup), "Render")]
    public class Tour_New_Popup_Render
    {
        // Fixed tour revenue text color to consider savings
        public static void Postfix(ref Tour_New_Popup __instance)
        {
            //NotificationManager.AddNotification("postfix applied", mainScript.green32, NotificationManager._notification._type.other);

            if (__instance.Tour.ExpectedRevenue > __instance.Tour.ProductionCost - __instance.Tour.Saving)
            {
                ExtensionMethods.SetColor(__instance.ExpectedRevenue, mainScript.green32);
            }

        }
    }

    [HarmonyPatch(typeof(Theaters), "GetStaminaCost")]
    public class Theaters_GetStaminaCost
    {
        // Fixed Theater so that it uses stamina.
        public static void Postfix(Theaters._theater._schedule._type Type, ref float __result)
        {
            float num = 0f;
            if (Type == Theaters._theater._schedule._type.performance)
            {
                num = 5f;
            }
            else if (Type == Theaters._theater._schedule._type.manzai)
            {
                num = 2f;
            }
            if (staticVars.IsHard())
            {
                num *= 2f;
            }
            __result = num;
        }
    }

    [HarmonyPatch(typeof(Theaters), "CompleteDay")]
    public class Theaters_CompleteDay
    {
        // Fixed Theater so that revenue stats are not offset by one day
        public static bool Prefix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                // Fix so that auto schedules contribute revenue on the day of
                theater.Doing_Now = theater.GetSchedule().Type;
            }
            return true;
        }


        // Fixed Theater so that average stats ignore days off, and so that girls earnings are increased by revenue
        public static void Postfix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                // Fix so that auto schedules contribute revenue on the day of
                if (theater.GetSchedule().Type == Theaters._theater._schedule._type.auto &&
                    (theater.Doing_Now == Theaters._theater._schedule._type.manzai || theater.Doing_Now == Theaters._theater._schedule._type.performance))
                {
                    long rev = theater.GetTicketSales();
                    if (rev > 0) resources.Add(resources.type.money, rev);
                    if (staticVars.dateTime.Day != 1)
                    {
                        theater.GetRoom().addFloat(Floats.type.icon_money, "", true, null, 0f, 1f, 0f, null);
                    }
                }

                // Fix so that days off have no revenue
                if (theater.Doing_Now == Theaters._theater._schedule._type.day_off)
                {
                    theater.Stats[theater.Stats.Count - 1].Revenue = 0;
                }

                if (theater.Doing_Now == Theaters._theater._schedule._type.performance || theater.Doing_Now == Theaters._theater._schedule._type.manzai)
                {
                    int num2 = theater.GetGroup().GetGirls(true, false, null).Count;
                    long num = theater.GetTicketSales();
                    if (theater.AreSubsUnlocked() && staticVars.dateTime.Day == 1)
                    {
                        num += theater.GetSubRevenue();
                    }
                    foreach (data_girls.girls girls2 in theater.GetGroup().GetGirls(true, false, null))
                    {
                        if (num > 0L && num2 > 0)
                        {
                            girls2.Earn(num / (long)num2);
                        }
                    }
                }
            }
        }
    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgAttendance")]
    public class Theaters__theater_GetAvgAttendance
    {
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            float num = 0f;
            float num2 = 7f;
            if (__instance.Stats.Count == 0)
            {
                __result = 0;
            }
            if (__instance.Stats.Count < 7)
            {
                num2 = (float)__instance.Stats.Count;
            }
            int num3 = __instance.Stats.Count - 1;
            int num4 = 0;
            while ((float)num3 >= (float)__instance.Stats.Count - num2)
            {
                if (__instance.Stats[num3].Schedule.Type != Theaters._theater._schedule._type.day_off)
                {
                    num += (float)__instance.Stats[num3].Attendance;
                    num4++;
                }
                num3--;
            }
            if (num4 != 0)
            {
                num /= (float)num4;
            }
            __result = Mathf.RoundToInt(num);
        }

    }

	// Fixed Theater so that average stats ignore days off
    [HarmonyPatch(typeof(Theaters._theater), "GetAvgRevenue")]
    public class Theaters__theater_GetAvgRevenue
    {
        public static void Postfix(ref int __result, Theaters._theater __instance)
        {
            float num = 0f;
            float num2 = 7f;
            if (__instance.Stats.Count == 0)
            {
                __result = 0;
                return;
            }
            if (__instance.Stats.Count < 7)
            {
                num2 = (float)__instance.Stats.Count;
            }
            int num3 = __instance.Stats.Count - 1;
            int num4 = 0;
            while ((float)num3 >= (float)__instance.Stats.Count - num2)
            {
                if (__instance.Stats[num3].Schedule.Type != Theaters._theater._schedule._type.day_off)
                {
                    num += (float)__instance.Stats[num3].Revenue;
                    num4++;
                }
                num3--;
            }
            if (num4 != 0)
            {
                num /= (float)num4;
            }
            __result = Mathf.RoundToInt(num);
        }
    }


	// Fixed Theater so that money tooltip includes 7 days instead of 6, and include sub revenue
    [HarmonyPatch(typeof(Theaters), "GetLastWeekEarning")]
    public class Theaters_GetLastWeekEarning
    {
        public static void Postfix(ref long __result)
        {
            long output = __result;
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if (theater.Stats.Count >= 7)
                {
                    output += theater.Stats[theater.Stats.Count - 7].Revenue;
                }
                if (theater.AreSubsUnlocked())
                {
                    output += (long)Mathf.Round(theater.GetSubRevenue() / 4.35f);
                }
            }
            __result = output;
        }
    }

	// Fixed Cafe so that money tooltip includes 7 days instead of 6
    [HarmonyPatch(typeof(Cafes), "GetLastWeekEarning")]
    public class Cafes_GetLastWeekEarning
    {
        public static void Postfix(ref int __result)
        {
            int output = __result;
            foreach (Cafes._cafe cafe in Cafes.Cafes_)
            {
                if (cafe.Stats.Count >= 7)
                {
                    output += cafe.Stats[cafe.Stats.Count - 7].Profit;
                }
            }
            __result = output;
        }
    }


	// Fixed so that when girls dating within the group break up, their relationship status is no longer known
    [HarmonyPatch(typeof(Relationships._relationship), "BreakUp")]
    public class Relationships__relationship_BreakUp
    {
        public static void Postfix(ref Relationships._relationship __instance)
        {
            if (__instance.Dating)
            {
                __instance.Girls[0].DatingData.Is_Partner_Status_Known = false;
                __instance.Girls[1].DatingData.Is_Partner_Status_Known = false;
            }
        }
    }

	// Fixed Concert revenue formula so that it shows accurate estimated values
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetRevenue")]
    public class SEvent_Concerts__concert__projectedValues_GetRevenue
    {
        //public static void Postfix(ref long __result, SEvent_Concerts._concert._projectedValues __instance)
        //{
        //    long output = __result;

        //    float hype = __instance.GetHype() * 100f;
        //    if (hype > 100f)
        //    {
        //        float num;
        //        float num2 = hype - 100f;
        //        LinearFunction._function function = new LinearFunction._function();
        //        function.Init(0f, 50f, 100f, 25f);
        //        float num3 = function.GetY(num2) / 100f;
        //        num = num2 * num3 + 100f;
        //        output = (long)Mathf.Round(output / hype * num);
        //    }

        //    __result = output;
        //}
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Call && (MethodInfo)instructionList[i].operand == AccessTools.Method(typeof(SEvent_Concerts._concert._projectedValues), "GetHype"))
                {
                    instructionList[i].operand = AccessTools.Method(typeof(SEvent_Concerts__concert__projectedValues_GetRevenue), "Infix");
                    break;
                }
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(SEvent_Concerts._concert._projectedValues __this)
        {

            float hype = __this.GetHype();
            if (hype > 1)
            {
                LinearFunction._function function = new();
                function.Init(0f, 0.5f, 1f, 0.25f);

                float num2 = hype - 1f;
                hype = num2 * function.GetY(num2) + 1f;
            }

            return hype;
        }
    }


	// Fixed Concert revenue formula so that it shows accurate estimated values
    [HarmonyPatch(typeof(SEvent_Concerts._concert._projectedValues), "GetString")]
    public class SEvent_Concerts__concert__projectedValues_GetString
    {
        public static bool Prefix(ref float _val)
        {
            if (_val >= 99.5)
            {
                _val = 99;
            }
            return true;
        }
    }

	// Fixed senbatsu stats calculation so it doesn't punish you if you don't have enough idols to fill all rows
    [HarmonyPatch(typeof(singles._single), "SenbatsuCalcParam")]
    public class singles__single_SenbatsuCalcParam
    {
        //public static void Postfix(Groups._group Group, singles._single __instance, ref data_girls.girls.param __result )
        //{
        //    Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
        //    if (Group == null)
        //    {
        //        Group = __instance.GetGroup();
        //    }
        //    int num = Group.GetNumberOfNonGraduatedGirls();
        //    if (Group.IsMain())
        //    {
        //        foreach (Groups._group group in Groups.Groups_)
        //        {
        //            if (group != Group)
        //            {
        //                num += group.GetNumberOfNonGraduatedGirls();
        //            }
        //        }
        //    }
        //    float num2_orig = 5;
        //    float num2 = 5;
        //    if (num == 0)
        //    {
        //        return;
        //    }
        //    else if (num == 1)
        //    {
        //        return;
        //    }
        //    else if (num <= 3)
        //    {
        //        num2_orig = 2;
        //        num2 = 1 + (num - 1) / 2;
        //    }
        //    else if (num <= 6)
        //    {
        //        num2_orig = 3;
        //        num2 = 2 + (num - 3) / 3;
        //    }
        //    else if (num <= 10)
        //    {
        //        num2_orig = 4;
        //        num2 = 3 + (num - 6) / 4;
        //    }
        //    else if (num <= 15)
        //    {
        //        num2_orig = 5;
        //        num2 = 3 + (num - 10) / 5;
        //    }
        //    float num3 = 100f / num2;
        //    float num3_orig = 100f / num2_orig;
        //    float coeff = num3 / num3_orig;

        //    float val_capped = __result.val * coeff;
        //    if (val_capped > 100f)
        //    {
        //        val_capped = 100f;
        //    }
        //    data_girls.girls.param output = new data_girls.girls.param
        //    {
        //        type = __result.type,
        //        val = val_capped
        //    };
        //    __result = output;
        //}


        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            int index = -1;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_2)
                {
                    index = i;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(singles__single_SenbatsuCalcParam), "Infix")));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Stloc_2));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(int num)
        {
            //singles._single s = Camera.main.GetComponent<mainScript>().Data.GetComponent<PopupManager>().GetByType(PopupManager._type.single_senbatsu).obj.GetComponent<SinglePopup_Senbatsu>().single;
            //Debug.Log("Tranpiler Injection: " + num.ToString() + ", " + x.ToString());
            float num2 = 5;
            if (num == 0)
            {
                num2 = 0;
            }
            else if (num == 1)
            {
                num2 = 1;
            }
            else if (num <= 3)
            {
                num2 = 1 + (num - 1) / 2;
            }
            else if (num <= 6)
            {
                num2 = 2 + (num - 3) / 3;
            }
            else if (num <= 10)
            {
                num2 = 3 + (num - 6) / 4;
            }
            else if (num <= 15)
            {
                num2 = 3 + (num - 10) / 5;
            }
            return 100f / num2;
        }
    }


    // Dating status is visible for underage members
    [HarmonyPatch(typeof(data_girls.girls), "GetPartnerString")]
    public class data_girls_girls_GetPartnerString
    {
        //public static void Postfix(ref string __result, data_girls.girls __instance)
        //{
        //    string output = __result;
        //    if (!__instance.Is_AOC())
        //    {
        //        string text = "";
        //        if (!__instance.DatingData.Is_Partner_Status_Known)
        //        {
        //            text += Language.Data["PROFILE__DATING_UNKNOWN"];
        //        }
        //        else if (__instance.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.free)
        //        {
        //            text += Language.Data["PROFILE__DATING_NOT_DATING"];
        //        }
        //        else if (__instance.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_idol)
        //        {
        //            data_girls.girls girlfriend = __instance.GetGirlfriend();
        //            if (girlfriend != null)
        //            {
        //                text += Language.Insert("PROFILE__DATING_IDOL", new string[]
        //                {
        //            girlfriend.GetName(true)
        //                });
        //            }
        //            else
        //            {
        //                text += Language.Data["PROFILE__DATING_IDOL_UNKNOWN"];
        //            }
        //        }
        //        else if (__instance.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_outside_bf)
        //        {
        //            text += Language.Data["PROFILE__DATING_HAS_BF"];
        //        }
        //        else if (__instance.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_outside_gf)
        //        {
        //            text += Language.Data["PROFILE__DATING_HAS_GF"];
        //        }
        //        else if (__instance.DatingData.Partner_Status_Known_To_Player == data_girls.girls._dating_data._partner_status.taken_player)
        //        {
        //            text += Language.Data["PROFILE__DATING_YOU"];
        //        }
        //        text += "\n";
        //        if (__instance.DatingData.Is_Sexuality_Known)
        //        {
        //            if (__instance.sexuality == data_girls.girls._sexuality.straight)
        //            {
        //                text += Language.Data["PROFILE__DATING_STRAIGHT"];
        //            }
        //            else if (__instance.sexuality == data_girls.girls._sexuality.lesbian)
        //            {
        //                text += Language.Data["PROFILE__DATING_LESBIAN"];
        //            }
        //            else
        //            {
        //                text += Language.Data["PROFILE__DATING_BI"];
        //            }
        //        }
        //        else
        //        {
        //            text += Language.Data["PROFILE__DATING_PREF_UNKNOWN"];
        //        }
        //        if (__instance.DatingData.Previous_Attempt != Date_Flirt._flirt._category.NONE && __instance.DatingData.Partner_Status_Known_To_Player != data_girls.girls._dating_data._partner_status.taken_player)
        //        {
        //            text += "\n";
        //            if (__instance.DatingData.Is_Uninterested || (__instance.DatingData.Is_Sexuality_Known && !Date_Flirt.IsCompatibleSexuality(__instance)))
        //            {
        //                text += Language.Data["PROFILE__DATING_NOT_INTERESTED"];
        //            }
        //            else
        //            {
        //                text += Language.Data["PROFILE__DATING_INTERESTED"];
        //            }
        //        }
        //        output = text;
        //    }
        //    __result = output;
        //}


        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ret)
                {
                    instructionList[i - 1].opcode = OpCodes.Nop;
                    instructionList[i].opcode = OpCodes.Nop;
                    break;
                }
            }

            return instructionList.AsEnumerable();
        }
    }


    // Fixed fan opinion to be impacted by concerts, SSK/show cancellation and random events
    [HarmonyPatch(typeof(resources._fanOpinion), "Add")]
    public class resources__fanOpinion_Add
    {
        public static void Postfix(resources._fanOpinion __instance, float val)
        {
            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl != null && girl.status != data_girls._status.graduated)
                {
                    girl.AddAppeal(__instance.type, val);
                }
            }
        }
    }


    // Fixed gossip so girl doesn't gossip about herself
    [HarmonyPatch(typeof(Date_Gossip), "GetAvailableGossips")]
    public class Date_Gossip_GetAvailableGossips
    {
        public static void Postfix(ref List<Date_Gossip._gossip> __result, data_girls.girls Snitch)
        {
            if (__result.Count == 0)
            {
                return;
            }
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                if (__result[i].BullyingTarget == Snitch)
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }


    // Fixed event and dialogue checks for Influence to check for Influence instead of Friendship
    [HarmonyPatch(typeof(vn_requirements), "CheckGirl", new Type[] { typeof(data_girls.girls), typeof(string), typeof(string) })]
    public class vn_requirements_CheckGirl
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            bool breakFlag = false;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Ldstr && (string)instructionList[i].operand == "influence")
                {
                    breakFlag = true;
                }
                if (breakFlag && instructionList[i].opcode == OpCodes.Ldc_I4_1)
                {
                    instructionList[i].opcode = OpCodes.Ldc_I4_2;
                    break;
                }
                if (breakFlag && instructionList[i].opcode == OpCodes.Callvirt)
                {
                    // abort if it reaches the Callvirt operation without finding Ldc_I4_1
                    break;
                }
            }

            return instructionList.AsEnumerable();
        }
    }


    // Fix stamina cost of performance thumbnail if Energetic policy
    [HarmonyPatch(typeof(Activities._activity), "GetDescription")]
    public class Activities__activity_GetDescription
    {
        public static void Postfix(ref Activities._activity __instance, ref string __result)
        {
            if (__instance.type == Activity._type.performance && policies.GetSelectedPolicyValue(policies._type.performances).Value == policies._value.performances_energy)
            {
                __result = string.Concat(new object[]
                {
                    "-",
                    4,
                    Language.Data["PT"],
                    " ",
                    Language.Data["STAMINA"].ToLower()
                });
            }
        }
    }


    // Fix custom event check for "hired a staffer of type"
    [HarmonyPatch(typeof(vn_requirements), "CheckMeta")]
    public class vn_requirements_CheckMeta
    {
        public static void Postfix(string parameter, string formula, ref bool __result)
        {
            if (parameter == "staff")
            {
                if (formula == "vocal" && staff.CountStaffersOfType(agency._type.recordingStudio) > 0)
                {
                    __result = true;
                }
                else if(formula == "dance" && staff.CountStaffersOfType(agency._type.danceStudio) > 0)
                {
                    __result = true;
                }
                else if(formula == "office" && staff.CountStaffersOfType(agency._type.office) > 0)
                {
                    __result = true;
                }
                else if(formula == "style" && staff.CountStaffersOfType(agency._type.dressingRoom) > 0)
                {
                    __result = true;
                }
            }
            
        }
    }

}
