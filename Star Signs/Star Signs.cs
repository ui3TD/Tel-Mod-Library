using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using SimpleJSON;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using static StarSigns.StarSigns;
using static data_girls;

namespace StarSigns
{
    [HarmonyPatch(typeof(Profile_Popup), "RenderTab_Extras")]
    public class Profile_Popup_RenderTab_Extras
    {
        [HarmonyPriority(Priority.LowerThanNormal)]
        public static void Postfix(Profile_Popup __instance)
        {
            StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(__instance.Girl);
            if(zodiac != StarSigns.Zodiac.None)
            {
                string txt = "\n" + ExtensionMethods.color(Language.Data["STARSIGN__TITLE_" + zodiac.ToString().ToUpper()] + ": ", mainScript.blue) + Language.Data["STARSIGN__DESC_" + zodiac.ToString().ToUpper()];

                __instance.Extras_Container.transform.Find("Text(Clone)").GetComponent<TextMeshProUGUI>().text += txt;
                __instance.Extras_Container.transform.Find("Text(Clone)").GetComponent<TextMeshProUGUI>().text = __instance.Extras_Container.transform.Find("Text(Clone)").GetComponent<TextMeshProUGUI>().text.Replace(mainScript.blue, mainScript.black);
                LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Extras_Container.GetComponent<RectTransform>());
            }
        }
    }


    // Show on audition card
    [HarmonyPatch(typeof(Audition_Data_Card), "Show")]
    public class Audition_Data_Card_Show
    {
        public static void Postfix(ref Audition_Data_Card __instance)
        {
            StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(__instance.Girl.girl);
            if (zodiac != StarSigns.Zodiac.None)
            {
                string txt = " (" + Language.Data["STARSIGN__TITLE_" + zodiac.ToString().ToUpper()] + ")";

                __instance.Age.GetComponent<TextMeshProUGUI>().text += txt;
            }
        }
    }

    // Show on audition card
    [HarmonyPatch(typeof(Audition_Data_Card), "Show_Fast")]
    public class Audition_Data_Card_Show_Fast
    {
        public static void Postfix(ref Audition_Data_Card __instance)
        {
            StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(__instance.Girl.girl);
            if (zodiac != StarSigns.Zodiac.None)
            {
                string txt = " (" + Language.Data["STARSIGN__TITLE_" + zodiac.ToString().ToUpper()] + ")";

                __instance.Age.GetComponent<TextMeshProUGUI>().text += txt;
            }
        }
    }


    // Dynamic relationships changes
    [HarmonyPatch(typeof(Relationships), "Do_Dynamic")]
    public class Relationships_Do_Dynamic
    {
        public static void Postfix()
        {
            foreach (Relationships._relationship relationship in Relationships.RelationshipsData)
            {
                if(relationship.Girls[0] == relationship.Girls[1])
                {
                    return;
                }
                StarSigns.Zodiac zodiac0 = StarSigns.GetGirlZodiac(relationship.Girls[0]);
                StarSigns.Zodiac zodiac1 = StarSigns.GetGirlZodiac(relationship.Girls[1]);
                if (zodiac0 == StarSigns.Zodiac.Cancer || zodiac1 == StarSigns.Zodiac.Cancer)
                {
                    if(relationship.IsSameClique() && mainScript.chance(10))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Pisces || zodiac1 == StarSigns.Zodiac.Pisces)
                {
                    if (mainScript.chance(10))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Virgo)
                {
                    if (relationship.Girls[1].getAverageParam() > 70 && mainScript.chance(10))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac1 == StarSigns.Zodiac.Virgo)
                {
                    if (relationship.Girls[0].getAverageParam() > 70 && mainScript.chance(10))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac1 == StarSigns.Zodiac.Libra)
                {
                    if (Relationships.IsBullied(relationship.Girls[0]) && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Libra)
                {
                    if (Relationships.IsBullied(relationship.Girls[1]) && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac1 == StarSigns.Zodiac.Sagittarius)
                {
                    if (relationship.Girls[0].GetScandalPoints() > 0 && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Sagittarius)
                {
                    if (relationship.Girls[1].GetScandalPoints() > 0 && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac1 == StarSigns.Zodiac.Capricorn)
                {
                    if (relationship.Girls[0].DatingData.Partner_Status == girls._dating_data._partner_status.free && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Capricorn)
                {
                    if (relationship.Girls[1].DatingData.Partner_Status == girls._dating_data._partner_status.free && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac1 == StarSigns.Zodiac.Aries)
                {
                    if (relationship.Girls[1].Is_Pushed() && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
                else if (zodiac0 == StarSigns.Zodiac.Aries)
                {
                    if (relationship.Girls[0].Is_Pushed() && mainScript.chance(20))
                    {
                        relationship.Add(0.1f);
                    }
                }
            }
        }
    }


    // Dynamic relationships changes
    [HarmonyPatch(typeof(Relationships._relationship), "Add")]
    public class Relationships__relationship_Add
    {
        public static bool Prefix(Relationships._relationship __instance, ref float val)
        {
            StarSigns.Zodiac zodiac0 = StarSigns.GetGirlZodiac(__instance.Girls[0]);
            StarSigns.Zodiac zodiac1 = StarSigns.GetGirlZodiac(__instance.Girls[1]);
            if (zodiac0 == StarSigns.Zodiac.Taurus || zodiac1 == StarSigns.Zodiac.Taurus)
            {
                val *= 0.8f;
            }
            else if (zodiac0 == StarSigns.Zodiac.Gemini || zodiac1 == StarSigns.Zodiac.Gemini)
            {
                val *= 1.2f;
            }
            return true;
        }
    }

    // patch leader selection
    [HarmonyPatch(typeof(Relationships._clique), "UpdateLeader")]
    public class Relationships__clique_UpdateLeader
    {
        public static bool Prefix()
        {
            StarSigns.patchGetVal = true;
            return true;
        }

        public static void Postfix()
        {
            StarSigns.patchGetVal = false;
        }
    }

    // apply leader bonus to Leo
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (StarSigns.patchGetVal)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(__instance.Parent);
                if(zodiac == StarSigns.Zodiac.Leo && (__instance.type == data_girls._paramType.funny || __instance.type == data_girls._paramType.smart))
                {
                    __result += 10;
                }
            }
        }
    }

    // patch bully chance
    [HarmonyPatch(typeof(Relationships._clique), "AddBulliedGirl")]
    public class Relationships__clique_AddBulliedGirl
    {
        public static bool Prefix(data_girls.girls Girl)
        {
            StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(Girl);
            if(zodiac == StarSigns.Zodiac.Scorpio)
            {
                if(mainScript.chance(20))
                {
                    return false;
                }
            }
            return true;
        }
    }

    // patch push jealousy
    [HarmonyPatch(typeof(Pushes), "OnNewDay")]
    public class Pushes_OnNewDay
    {
        public static bool Prefix()
        {
            StarSigns.patchAddRelationship = true;
            return true;
        }

        public static void Postfix()
        {
            StarSigns.patchAddRelationship = false;
        }
    }

    // apply leader bonus to Leo
    [HarmonyPatch(typeof(data_girls.girls), "AddRelationship")]
    public class data_girls_girls_AddRelationship
    {
        public static bool Prefix(data_girls.girls __instance, ref float val)
        {
            if (StarSigns.patchAddRelationship)
            {
                //Debug.Log(MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + MethodBase.GetCurrentMethod().DeclaringType.Name + "." + MethodBase.GetCurrentMethod().Name);
                StarSigns.Zodiac zodiac = StarSigns.GetGirlZodiac(__instance);
                if (zodiac == StarSigns.Zodiac.Aquarius)
                {
                    val *= 0.8f;
                }
            }
            return true;
        }
    }



    // Load starsign from unique idol textures
    [HarmonyPatch(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) })]
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
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable && localVariable.LocalIndex == 8)
                {
                    textureAssetOperand = instructionList[i].operand;
                }
                if (instructionList[i].opcode == OpCodes.Stloc_S && instructionList[i].operand is LocalVariableInfo localVariable2 && localVariable2.LocalIndex == 13)
                {
                    index = i;
                    jsonNodeOperand = instructionList[i].operand;
                    break;
                }
            }

            if (index != -1)
            {
                instructionList.Insert(index + 1, new CodeInstruction(OpCodes.Ldloc_S, jsonNodeOperand));
                instructionList.Insert(index + 2, new CodeInstruction(OpCodes.Ldloc_S, textureAssetOperand));
                instructionList.Insert(index + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), "Infix")));
            }

            return instructionList.AsEnumerable();
        }

        public static void Infix(JSONNode jsonnode, data_girls_textures._textureAsset textureAsset)
        {
            if (jsonnode["starsign"] == null)
            {
                return;
            }
            string zodiacStr = jsonnode["starsign"];
            if (!Enum.TryParse(zodiacStr, true, out Zodiac tryZodiac))
            {
                tryZodiac = Zodiac.None;
            }
            StarSigns.ZodiacTextureData zodiacTextureData = new()
            {
                ModName = textureAsset.ModName,
                body_id = textureAsset.body_id,
                zodiac = tryZodiac
            };

            StarSigns.ZodiacTextureReferenceList.Add(zodiacTextureData);
        }
    }

    // Load texture on generation
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result, bool genTextures)
        {
            if (genTextures)
            {
                foreach (StarSigns.ZodiacTextureData data in StarSigns.ZodiacTextureReferenceList)
                {
                    if (__result.textureAssets.Count != 0 && __result.textureAssets[0].asset != null && __result.textureAssets[0].asset.ModName == data.ModName && __result.textureAssets[0].asset.body_id == data.body_id)
                    {
                        if (__result.textureAssets[0].asset.Age != 0)
                        {
                            while (StarSigns.DateToZodiac(__result.birthday) != data.zodiac)
                            {
                                __result.birthday = staticVars.dateTime.AddYears(-__result.textureAssets[0].asset.Age).AddMonths(-UnityEngine.Random.Range(0, 11)).AddDays((double)(-(double)UnityEngine.Random.Range(0, 25)));
                            }
                        }
                        else
                        {
                            while (StarSigns.DateToZodiac(__result.birthday) != data.zodiac)
                            {
                                __result.GenerateBirthday();
                            }
                        }
                        break;
                    }
                }
            }
        }
    }

    public class StarSigns
    {
        public static bool patchGetVal = false;
        public static bool patchAddRelationship = false;

        public static List<ZodiacTextureData> ZodiacTextureReferenceList = new();

        public class ZodiacTextureData
        {
            public string ModName;
            public int body_id;
            public int Age;
            public Zodiac zodiac;
        }

        public static Zodiac GetGirlZodiac(data_girls.girls girls)
        {
            DateTime bday = girls.birthday;
            return DateToZodiac(bday);
        }

        public static Zodiac DateToZodiac(DateTime date)
        {
            int mo = date.Month;
            int d = date.Day;

            switch(mo)
            {
                case 1:
                    if(d < 20)
                    {
                        return Zodiac.Capricorn;
                    }
                    else
                    {
                        return Zodiac.Aquarius;
                    }
                case 2:
                    if (d < 19)
                    {
                        return Zodiac.Aquarius;
                    }
                    else
                    {
                        return Zodiac.Pisces;
                    }
                case 3:
                    if (d < 21)
                    {
                        return Zodiac.Pisces;
                    }
                    else
                    {
                        return Zodiac.Aries;
                    }
                case 4:
                    if (d < 20)
                    {
                        return Zodiac.Aries;
                    }
                    else
                    {
                        return Zodiac.Taurus;
                    }
                case 5:
                    if (d < 21)
                    {
                        return Zodiac.Taurus;
                    }
                    else
                    {
                        return Zodiac.Gemini;
                    }
                case 6:
                    if (d < 22)
                    {
                        return Zodiac.Gemini;
                    }
                    else
                    {
                        return Zodiac.Cancer;
                    }
                case 7:
                    if (d < 23)
                    {
                        return Zodiac.Cancer;
                    }
                    else
                    {
                        return Zodiac.Leo;
                    }
                case 8:
                    if (d < 23)
                    {
                        return Zodiac.Leo;
                    }
                    else
                    {
                        return Zodiac.Virgo;
                    }
                case 9:
                    if (d < 23)
                    {
                        return Zodiac.Virgo;
                    }
                    else
                    {
                        return Zodiac.Libra;
                    }
                case 10:
                    if (d < 24)
                    {
                        return Zodiac.Libra;
                    }
                    else
                    {
                        return Zodiac.Scorpio;
                    }
                case 11:
                    if (d < 22)
                    {
                        return Zodiac.Scorpio;
                    }
                    else
                    {
                        return Zodiac.Sagittarius;
                    }
                case 12:
                    if (d < 22)
                    {
                        return Zodiac.Sagittarius;
                    }
                    else
                    {
                        return Zodiac.Capricorn;
                    }
                default:
                    break;
            }

            return Zodiac.None;
        }

        public enum Zodiac
        {
            None,
            Aries,          // 20% more positive relationship if pushed
            Taurus,         // Relationships develop 20% slower
            Gemini,         // Relationships develop 20% faster
            Cancer,         // Relationships improve 10% more within clique
            Leo,            // +40 bonus to being clique leader
            Virgo,          // Relationships improve 10% more with skilled girls
            Libra,          // Relationships improve 20% more with bullied girls
            Scorpio,        // 20% less chance of being bullied
            Sagittarius,    // Relationships improve 20% more with scandal girls
            Capricorn,      // Relationships improve 20% more with non-dating girls
            Aquarius,       // 20% less jealous of pushes
            Pisces          // 10% bonus to all relationships
        }

    }

}
