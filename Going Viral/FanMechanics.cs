using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using static GoingViral.TrendingManager;
using UnityEngine;
using static Achievements;
using static singles;

namespace GoingViral
{

    // Apply 3x casual bias to fan appeal for adding fans
    // Separate opinion from fan decrease
    [HarmonyPatch(typeof(data_girls.girls), "AddFans", new Type[] { typeof(long), typeof(resources.fanType?) })]
    public class data_girls_girls_AddFans
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            bool breakFlag = false;
            object operandi = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (breakFlag && instructionList[i].opcode == OpCodes.Stloc_S)
                {
                    index = i;
                    operandi = instructionList[i].operand;
                    break;
                }
                if (instructionList[i].opcode == OpCodes.Callvirt && instructionList[i].operand is MethodBase method && method.Name == "GetTotalAppeal" && method.DeclaringType == typeof(resources._fan))
                {
                    breakFlag = true;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_3));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Ldarg_1));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Ldloc_S, operandi));
                instructionList.Insert(index + 5, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_girls_AddFans), "Infix")));
                instructionList.Insert(index + 6, new CodeInstruction(OpCodes.Stloc_S, operandi));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(data_girls.girls __this, resources._fan fan, long val, float fanProportion)
        {

            // Apply default calc with opinions for decrease in fans
            if (val < 0)
            {
                float churnTotal = 0;

                foreach (resources._fan _fan in __this.Fans)
                {
                    churnTotal += GetFanChurn(_fan.appeal, _fan.Ratio, _fan.hardcoreness);
                }

                fanProportion = GetFanChurn(fan.appeal, fan.Ratio, fan.hardcoreness) / churnTotal;
            }
            // Apply new calc excluding opinions for increase in fans
            else if (val > 0)
            {
                float acquisitionTotal = 0;

                foreach (resources._fan _fan in __this.Fans)
                {
                    acquisitionTotal += GetFanAcquisition(__this, _fan.appeal, _fan.hardcoreness);
                }

                fanProportion = GetFanAcquisition(__this, fan.appeal, fan.hardcoreness) / acquisitionTotal;
            }


            return fanProportion;
        }


    }

    // When losing fans, use appeal and opinion instead of fame to distribute across girls
    [HarmonyPatch(typeof(data_girls), "AddFans")]
    public class data_girls_AddFans
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);

            int index = -1;
            object coeffOperand = null;
            object girlOperand = null;
            for (int i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo girl && girl.LocalIndex == 6)
                {
                    girlOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo coeff && coeff.LocalIndex == 7)
                {
                    index = i;
                    coeffOperand = instructionList[i].operand;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, girlOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, coeffOperand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Ldarg_0));
                instructionList.Insert(index + 4, new CodeInstruction(OpCodes.Ldarg_1));
                instructionList.Insert(index + 5, new CodeInstruction(OpCodes.Ldarg_2));
                instructionList.Insert(index + 6, new CodeInstruction(OpCodes.Ldarg_3));
                instructionList.Insert(index + 7, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_AddFans), "Infix")));
                instructionList.Insert(index + 8, new CodeInstruction(OpCodes.Stloc_S, coeffOperand));
            }

            return instructionList.AsEnumerable();
        }

        public static float Infix(data_girls.girls girl, float coeff, long total_fans, resources.fanType? fanType, List<data_girls.girls> Girls, data_girls.girls ExceptionGirl)
        {
            if (total_fans >= 0)
            {
                return coeff;
            }

            float churnTotal = 0;

            foreach (data_girls.girls _girl in Girls)
            {
                if (_girl.status != data_girls._status.graduated && _girl != ExceptionGirl)
                {
                    foreach (resources._fan _fan in _girl.Fans)
                    {
                        if (_fan.IsType(fanType))
                        {
                            churnTotal += GetFanChurn(_fan.appeal, _fan.Ratio, _fan.hardcoreness);
                        }
                    }
                }
            }

            float girlChurn = 0;
            foreach (resources._fan _fan in girl.Fans)
            {
                if (_fan.IsType(fanType))
                {
                    girlChurn += GetFanChurn(_fan.appeal, _fan.Ratio, _fan.hardcoreness);
                }
            }

            float fanProportion = girlChurn / churnTotal;


            return fanProportion;
        }
    }

    // Set opinion for theater
    [HarmonyPatch(typeof(resources), "OnNewWeek")]
    public class resources_OnNewWeek
    {
        public static void Postfix()
        {
            foreach (Theaters._theater theater in Theaters.Theaters_)
            {
                if(theater == null || theater.Stats.Count < 7)
                {
                    continue;
                }


                foreach (object fanTypeEnum in Enum.GetValues(typeof(resources.fanType)))
                {
                    resources.fanType type = (resources.fanType)fanTypeEnum;

                    float thisWeek = 0;
                    for (int i = 1; i <= 7; i++)
                    {
                        if(type == theater.Stats[theater.Stats.Count - i].Schedule.FanType)
                        {
                            thisWeek += 1;
                        }
                    }

                    if (theater.Stats.Count < 14)
                    {
                        if(thisWeek > 0)
                        {
                            foreach (data_girls.girls girl in theater.GetGroup().GetGirls())
                            {
                                if (girl != null && girl.status != data_girls._status.graduated)
                                {
                                    girl.AddAppeal(type, 1);
                                    NotificationManager.AddNotification(Language.Insert("THEATER__FANS_LIKE", new string[]
                                    {
                                        ExtensionMethods.color(resources.GetFanTitle(type), mainScript.green),
                                        theater.GetGroup().Title
                                    }), mainScript.green32, NotificationManager._notification._type.fans_opinion_change);
                                }
                            }
                        }
                        continue;
                    }

                    float pastWeek = 0;
                    for (int i = 8; i <= 14; i++)
                    {
                        if (type == theater.Stats[theater.Stats.Count - i].Schedule.FanType)
                        {
                            pastWeek += 1;
                        }
                    }

                    foreach (data_girls.girls girl in theater.GetGroup().GetGirls())
                    {
                        if (girl != null && girl.status != data_girls._status.graduated)
                        {
                            if(thisWeek > pastWeek)
                            {
                                girl.AddAppeal(type, 1);
                                NotificationManager.AddNotification(Language.Insert("THEATER__FANS_LIKE", new string[]
                                {
                                        ExtensionMethods.color(resources.GetFanTitle(type), mainScript.green),
                                        theater.GetGroup().Title
                                }), mainScript.green32, NotificationManager._notification._type.fans_opinion_change);
                            }
                            else if (thisWeek < pastWeek)
                            {
                                girl.AddAppeal(type, -1);
                                NotificationManager.AddNotification(Language.Insert("THEATER__FANS_DISLIKE", new string[]
                                {
                                        ExtensionMethods.color(resources.GetFanTitle(type), mainScript.red),
                                        theater.GetGroup().Title
                                }), mainScript.red32, NotificationManager._notification._type.fans_opinion_change);
                            }
                        }
                    }
                }

            }
        }
    }


    // Fixed fan opinion to be impacted by concerts, SSK/show cancellation and random events
    [HarmonyPatch(typeof(resources._fanOpinion), "Add")]
    public class resources__fanOpinion_Add
    {
        public static void Postfix(resources._fanOpinion __instance, float val)
        {
            if (Harmony.HasAnyPatches("com.tel.unofficialpatch"))
            {
                return;
            }
            foreach (data_girls.girls girl in data_girls.girl)
            {
                if (girl != null && girl.status != data_girls._status.graduated)
                {
                    girl.AddAppeal(__instance.type, val);
                }
            }
        }
    }


}
