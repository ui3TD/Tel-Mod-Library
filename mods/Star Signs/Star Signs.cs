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
using static UnityEngine.UIElements.StyleVariableResolver;

namespace StarSigns
{
    [HarmonyPatch(typeof(Profile_Popup), "RenderTab_Extras")]
    public class Profile_Popup_RenderTab_Extras
    {
        [HarmonyPriority(Priority.LowerThanNormal)]
        public static void Postfix(Profile_Popup __instance)
        {
            Zodiac zodiac = GetGirlZodiac(__instance.Girl);
            if (zodiac == Zodiac.None)
                return;

            string txt = "\n" + ExtensionMethods.color(Language.Data[CONSTANT_SIGN_PREFIX + zodiac.ToString().ToUpper()] + ": ", mainScript.blue) + Language.Data[CONSTANT_DESC_PREFIX + zodiac.ToString().ToUpper()];

            TextMeshProUGUI textComponent = __instance.Extras_Container.transform.Find("Text(Clone)").GetComponent<TextMeshProUGUI>();
            textComponent.text += txt;
            textComponent.text = textComponent.text.Replace(mainScript.blue, mainScript.black);
            LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Extras_Container.GetComponent<RectTransform>());

        }
    }


    // Show on audition card
    [HarmonyPatch(typeof(Audition_Data_Card), "Show")]
    public class Audition_Data_Card_Show
    {
        public static void Postfix(ref Audition_Data_Card __instance)
        {
            Zodiac zodiac = GetGirlZodiac(__instance.Girl.girl);
            if (zodiac == Zodiac.None)
                return;

            string txt = " (" + Language.Data[CONSTANT_SIGN_PREFIX + zodiac.ToString().ToUpper()] + ")";

            __instance.Age.GetComponent<TextMeshProUGUI>().text += txt;
        }
    }

    // Show on audition card
    [HarmonyPatch(typeof(Audition_Data_Card), "Show_Fast")]
    public class Audition_Data_Card_Show_Fast
    {
        public static void Postfix(ref Audition_Data_Card __instance)
        {
            Zodiac zodiac = GetGirlZodiac(__instance.Girl.girl);
            if (zodiac == Zodiac.None)
                return;

            string txt = " (" + Language.Data[CONSTANT_SIGN_PREFIX + zodiac.ToString().ToUpper()] + ")";

            __instance.Age.GetComponent<TextMeshProUGUI>().text += txt;
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
                    continue;

                Zodiac zodiac0 = GetGirlZodiac(relationship.Girls[0]);
                Zodiac zodiac1 = GetGirlZodiac(relationship.Girls[1]);

                if(CheckZodiacBonus(relationship, zodiac0, relationship.Girls[1], relationship.Girls[0]))
                {
                    relationship.Add(0.05f);
                }
                if (CheckZodiacBonus(relationship, zodiac1, relationship.Girls[0], relationship.Girls[1]))
                {
                    relationship.Add(0.05f);
                }
            }

        }

        private static bool CheckZodiacBonus(Relationships._relationship relationship, Zodiac zodiac, data_girls.girls otherGirl, data_girls.girls thisGirl)
        {
            switch (zodiac)
            {
                case Zodiac.Cancer:
                    if (relationship.IsSameClique() && mainScript.chance(CANCER_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Pisces:
                    if (mainScript.chance(PISCES_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Virgo:
                    if (otherGirl.getAverageParam() > VIRGO_SKILL_THR && mainScript.chance(VIRGO_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Libra:
                    if (Relationships.IsBullied(otherGirl) && mainScript.chance(LIBRA_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Sagittarius:
                    if (otherGirl.GetScandalPoints() > 0 && mainScript.chance(SAGG_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Capricorn:
                    if (otherGirl.DatingData.Partner_Status == data_girls.girls._dating_data._partner_status.free && mainScript.chance(CAPR_REL_CHANCE))
                        return true;
                    break;
                case Zodiac.Aries:
                    if (thisGirl.Is_Pushed() && mainScript.chance(ARIES_REL_CHANCE))
                        return true;
                    break;
            }
            return false;
        }
    }


    // Dynamic relationships changes
    [HarmonyPatch(typeof(Relationships._relationship), "Add")]
    public class Relationships__relationship_Add
    {
        public static void Prefix(Relationships._relationship __instance, ref float val)
        {
            Zodiac zodiac0 = GetGirlZodiac(__instance.Girls[0]);
            Zodiac zodiac1 = GetGirlZodiac(__instance.Girls[1]);
            if (zodiac0 == Zodiac.Taurus || zodiac1 == Zodiac.Taurus)
            {
                val *= TAURUS_REL_COEFF;
            }
            if (zodiac0 == Zodiac.Gemini || zodiac1 == Zodiac.Gemini)
            {
                val *= GEMINI_REL_COEFF;
            }
        }
    }

    // patch leader selection
    [HarmonyPatch(typeof(Relationships._clique), "UpdateLeader")]
    public class Relationships__clique_UpdateLeader
    {
        public static void Prefix()
        {
            patchGetVal = true;
        }

        public static void Postfix()
        {
            patchGetVal = false;
        }
    }

    // apply leader bonus to Leo
    [HarmonyPatch(typeof(data_girls.girls.param), "GetVal")]
    public class data_girls_girls_param_GetVal
    {
        public static void Postfix(ref float __result, data_girls.girls.param __instance)
        {
            if (!patchGetVal)
                return;

            Zodiac zodiac = GetGirlZodiac(__instance.Parent);
            if(zodiac == Zodiac.Leo && (__instance.type == data_girls._paramType.funny || __instance.type == data_girls._paramType.smart))
            {
                __result += LEO_LEADER_BONUS;
            }
        }
    }

    // patch bully chance
    [HarmonyPatch(typeof(Relationships._clique), "AddBulliedGirl")]
    public class Relationships__clique_AddBulliedGirl
    {
        public static bool Prefix(data_girls.girls Girl)
        {
            Zodiac zodiac = GetGirlZodiac(Girl);
            if (zodiac != Zodiac.Scorpio)
                return true;

            if(mainScript.chance(SCORP_BULLY_BONUS))
                return false;

            return true;
        }
    }

    // patch push jealousy
    [HarmonyPatch(typeof(Pushes), "OnNewDay")]
    public class Pushes_OnNewDay
    {
        public static void Prefix()
        {
            patchAddRelationship = true;
        }

        public static void Postfix()
        {
            patchAddRelationship = false;
        }
    }

    // apply aquarius bonus
    [HarmonyPatch(typeof(data_girls.girls), "AddRelationship")]
    public class data_girls_girls_AddRelationship
    {
        public static void Prefix(data_girls.girls __instance, ref float val)
        {
            if (!patchAddRelationship)
                return;

            Zodiac zodiac = GetGirlZodiac(__instance);
            if (zodiac != Zodiac.Aquarius)
                return;

            val *= AQUA_REL_COEFF;
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
                return;

            string zodiacStr = jsonnode["starsign"];
            if (!Enum.TryParse(zodiacStr, true, out Zodiac tryZodiac))
            {
                tryZodiac = Zodiac.None;
            }
            ZodiacTextureData zodiacTextureData = new()
            {
                ModName = textureAsset.ModName,
                body_id = textureAsset.body_id,
                zodiac = tryZodiac
            };

            ZodiacTextureReferenceList.Add(zodiacTextureData);
        }
    }

    // Load texture on generation
    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result, bool genTextures)
        {
            if (!genTextures)
                return;

            foreach (ZodiacTextureData data in ZodiacTextureReferenceList)
            {
                if (__result.textureAssets == null || __result.textureAssets.Count == 0)
                    continue;

                data_girls_textures._textureAsset asset = __result.textureAssets[0].asset;

                if (asset != null && asset.ModName == data.ModName && asset.body_id == data.body_id)
                {
                    int[] months = GetZodiacMonthRange(data.zodiac);
                    int currentMonth = staticVars.dateTime.Month;

                    int targetAge = __result.GetAge();
                    if (asset.Age > 0)
                    {
                        targetAge = asset.Age;
                    }

                    while (DateToZodiac(__result.birthday) != data.zodiac)
                    {
                        int targetMonth = months[UnityEngine.Random.Range(0, 3)];
                        int monthsToSubtract = (currentMonth - targetMonth + 12) % 12;

                        __result.birthday = staticVars.dateTime
                            .AddYears(-targetAge)
                            .AddMonths(-monthsToSubtract)
                            .AddDays(-UnityEngine.Random.Range(0, 30));
                    }
                    break;
                }
            }
        }

        private static int[] GetZodiacMonthRange(Zodiac zodiac)
        {
            return zodiac switch
            {
                Zodiac.Capricorn => new[] { 12, 1, 2 },
                Zodiac.Aquarius => new[] { 1, 2, 3 },
                Zodiac.Pisces => new[] { 2, 3, 4 },
                Zodiac.Aries => new[] { 3, 4, 5 },
                Zodiac.Taurus => new[] { 4, 5, 6 },
                Zodiac.Gemini => new[] { 5, 6, 7 },
                Zodiac.Cancer => new[] { 6, 7, 8 },
                Zodiac.Leo => new[] { 7, 8, 9 },
                Zodiac.Virgo => new[] { 8, 9, 10 },
                Zodiac.Libra => new[] { 9, 10, 11 },
                Zodiac.Scorpio => new[] { 10, 11, 12 },
                Zodiac.Sagittarius => new[] { 11, 12, 1 },
                _ => throw new ArgumentException("Invalid zodiac sign")
            };
        }
    }

    public class StarSigns
    {
        public const string CONSTANT_SIGN_PREFIX = "STARSIGN__TITLE_";
        public const string CONSTANT_DESC_PREFIX = "STARSIGN__DESC_";

        public const int CANCER_REL_CHANCE = 10;
        public const int PISCES_REL_CHANCE = 10;
        public const int VIRGO_REL_CHANCE = 10;
        public const int VIRGO_SKILL_THR = 70;
        public const int LIBRA_REL_CHANCE = 20;
        public const int SAGG_REL_CHANCE = 20;
        public const int CAPR_REL_CHANCE = 20;
        public const int ARIES_REL_CHANCE = 20;
        public const float TAURUS_REL_COEFF = 0.8f;
        public const float GEMINI_REL_COEFF = 1.2f;
        public const int SCORP_BULLY_BONUS = 20;
        public const float LEO_LEADER_BONUS = 10;
        public const float AQUA_REL_COEFF = 0.8f;

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
            int month = date.Month;
            int day = date.Day;

            var zodiacDates = new[]
            {
                new { Month = 12, Day = 21, Sign = Zodiac.Sagittarius },
                new { Month = 1, Day = 19, Sign = Zodiac.Capricorn },
                new { Month = 2, Day = 18, Sign = Zodiac.Aquarius },
                new { Month = 3, Day = 20, Sign = Zodiac.Pisces },
                new { Month = 4, Day = 19, Sign = Zodiac.Aries },
                new { Month = 5, Day = 20, Sign = Zodiac.Taurus },
                new { Month = 6, Day = 21, Sign = Zodiac.Gemini },
                new { Month = 7, Day = 22, Sign = Zodiac.Cancer },
                new { Month = 8, Day = 22, Sign = Zodiac.Leo },
                new { Month = 9, Day = 22, Sign = Zodiac.Virgo },
                new { Month = 10, Day = 23, Sign = Zodiac.Libra },
                new { Month = 11, Day = 21, Sign = Zodiac.Scorpio },
                new { Month = 12, Day = 21, Sign = Zodiac.Sagittarius },
                new { Month = 1, Day = 19, Sign = Zodiac.Capricorn }
            };

            if ((month == zodiacDates[month].Month && day <= zodiacDates[month].Day) ||
                (month == zodiacDates[month - 1].Month && day > zodiacDates[month - 1].Day))
            {
                return zodiacDates[month].Sign;
            }

            return zodiacDates[month + 1].Sign;
        }

        public enum Zodiac
        {
            None,
            Aries,          // 20% more positive relationship if pushed
            Taurus,         // Relationships develop 20% slower
            Gemini,         // Relationships develop 20% faster
            Cancer,         // Relationships improve 10% more within clique
            Leo,            // +20 bonus to being clique leader
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
