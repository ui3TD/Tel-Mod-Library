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

namespace StarSigns
{
    [HarmonyPatch(typeof(Profile_Popup), nameof(Profile_Popup.RenderTab_Extras))]
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
    [HarmonyPatch(typeof(Audition_Data_Card), nameof(Audition_Data_Card.Show))]
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
    [HarmonyPatch(typeof(Audition_Data_Card), nameof(Audition_Data_Card.Show_Fast))]
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
    [HarmonyPatch(typeof(Relationships), nameof(Relationships.Do_Dynamic))]
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
    [HarmonyPatch(typeof(Relationships._relationship), nameof(Relationships._relationship.Add))]
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
    [HarmonyPatch(typeof(Relationships._clique), nameof(Relationships._clique.UpdateLeader))]
    public class Relationships__clique_UpdateLeader
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchGetVal = true;
        }

        // A finalizer, not a postfix: it runs even if UpdateLeader throws
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchGetVal = false;
        }
    }

    // apply leader bonus to Leo
    [HarmonyPatch(typeof(data_girls.girls.param), nameof(data_girls.girls.param.GetVal))]
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
    [HarmonyPatch(typeof(Relationships._clique), nameof(Relationships._clique.AddBulliedGirl))]
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
    [HarmonyPatch(typeof(Pushes), nameof(Pushes.OnNewDay))]
    public class Pushes_OnNewDay
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            patchAddRelationship = true;
        }

        // A finalizer, not a postfix: it runs even if OnNewDay throws
        [HarmonyPriority(Priority.VeryLow)]
        public static void Finalizer()
        {
            patchAddRelationship = false;
        }
    }

    // apply aquarius bonus
    [HarmonyPatch(typeof(data_girls.girls), nameof(data_girls.girls.AddRelationship))]
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
    [HarmonyPatch(typeof(data_girls_textures), nameof(data_girls_textures.LoadAssetsData), new[] { typeof(string) })]
    public class data_girls_textures_LoadAssetsData
    {
        // After the game parses a body folder's params.json into a local, call Infix with it and that folder's body asset
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new(instructions);
            CodeMatcher matcher = new CodeMatcher(instructionList).MatchEndForward(
                new CodeMatch(ci => ci.Calls(AccessTools.Method(typeof(data_girls_textures), nameof(data_girls_textures.ProcessInboundData)))),
                new CodeMatch(ci => ci.IsStloc()));
            if (matcher.IsInvalid)
            {
                Debug.LogError("[Star Signs] Couldn't find where the game reads params.json; \"starsign\" is ignored");
                return instructionList;
            }
            object jsonNode = matcher.Operand;

            // The body asset is the last one stored before params.json is read
            object textureAsset = instructionList.Take(matcher.Pos).LastOrDefault(ci => ci.IsStloc() && ci.operand is LocalVariableInfo local
                && local.LocalType == typeof(data_girls_textures._textureAsset))?.operand;
            if (textureAsset == null)
            {
                Debug.LogError("[Star Signs] Couldn't find the body asset that params.json is read into; \"starsign\" is ignored");
                return instructionList;
            }

            return matcher.Advance(1).Insert(
                new CodeInstruction(OpCodes.Ldloc_S, jsonNode),
                new CodeInstruction(OpCodes.Ldloc_S, textureAsset),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(Infix))))
                .InstructionEnumeration();
        }

        public static void Infix(JSONNode jsonnode, data_girls_textures._textureAsset textureAsset)
        {
            if (jsonnode["starsign"] == null)
                return;

            string zodiacStr = jsonnode["starsign"];
            if (!Enum.TryParse(zodiacStr, true, out Zodiac tryZodiac) || tryZodiac == Zodiac.None || !Enum.IsDefined(typeof(Zodiac), tryZodiac))
            {
                Debug.LogWarning("Star sign not found: " + zodiacStr);
                return;
            }
            UniqueIdolSigns[UniqueIdolKey(textureAsset.ModName, textureAsset.body_id)] = tryZodiac;
        }
    }

    // Load texture on generation
    [HarmonyPatch(typeof(data_girls), nameof(data_girls.GenerateGirl))]
    public class data_girls_GenerateGirl
    {
        public static void Postfix(ref data_girls.girls __result, bool genTextures)
        {
            if (!genTextures)
                return;

            if (__result.textureAssets == null || __result.textureAssets.Count == 0)
                return;

            data_girls_textures._textureAsset asset = __result.textureAssets[0].asset;
            if (asset == null || !UniqueIdolSigns.TryGetValue(UniqueIdolKey(asset.ModName, asset.body_id), out Zodiac zodiac))
                return;

            int[] months = GetZodiacMonthRange(zodiac);
            int currentMonth = staticVars.dateTime.Month;

            int targetAge = __result.GetAge();
            if (asset.Age > 0)
            {
                targetAge = asset.Age;
            }

            // Going back 11 months and up to 29 days can cross February into the year before
            while (DateToZodiac(__result.birthday) != zodiac || __result.GetAge() != targetAge)
            {
                int targetMonth = months[UnityEngine.Random.Range(0, 3)];
                int monthsToSubtract = (currentMonth - targetMonth + 12) % 12;

                __result.birthday = staticVars.dateTime
                    .AddYears(-targetAge)
                    .AddMonths(-monthsToSubtract)
                    .AddDays(-UnityEngine.Random.Range(0, 30));
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

        // Signs set in unique idols' params.json, by UniqueIdolKey. Reading the files again overwrites, never duplicates.
        public static Dictionary<string, Zodiac> UniqueIdolSigns = new();

        public static string UniqueIdolKey(string modName, int bodyId) => modName + "/" + bodyId;

        public static Zodiac GetGirlZodiac(data_girls.girls girls)
        {
            DateTime bday = girls.birthday;
            return DateToZodiac(bday);
        }

        // By month, January first: the last day of the sign the month starts in, that sign, and the sign after it
        private static readonly int[] LastDayOfFirstSign = { 19, 18, 20, 19, 20, 21, 22, 22, 22, 23, 21, 21 };
        private static readonly Zodiac[] FirstSign =
        {
            Zodiac.Capricorn, Zodiac.Aquarius, Zodiac.Pisces, Zodiac.Aries, Zodiac.Taurus, Zodiac.Gemini,
            Zodiac.Cancer, Zodiac.Leo, Zodiac.Virgo, Zodiac.Libra, Zodiac.Scorpio, Zodiac.Sagittarius
        };
        private static readonly Zodiac[] SecondSign =
        {
            Zodiac.Aquarius, Zodiac.Pisces, Zodiac.Aries, Zodiac.Taurus, Zodiac.Gemini, Zodiac.Cancer,
            Zodiac.Leo, Zodiac.Virgo, Zodiac.Libra, Zodiac.Scorpio, Zodiac.Sagittarius, Zodiac.Capricorn
        };

        // Called for every relationship pair every day, so it allocates nothing
        public static Zodiac DateToZodiac(DateTime date)
        {
            int month = date.Month - 1;
            return date.Day <= LastDayOfFirstSign[month] ? FirstSign[month] : SecondSign[month];
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
