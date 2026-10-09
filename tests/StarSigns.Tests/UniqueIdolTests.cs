using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static StarSigns.StarSigns;

namespace StarSigns.Tests
{
    /// <summary>
    /// A unique idol's params.json can set her sign with "starsign". The mod records it when the game
    /// loads the idol's portraits.
    /// </summary>
    public class StarsignLoadingTests
    {
        public StarsignLoadingTests() => TestGame.Reset();

        private static void Load(string paramsJson, string modName = "Idol Mod", int bodyId = 7)
        {
            data_girls_textures._textureAsset asset = new() { ModName = modName, body_id = bodyId };
            data_girls_textures_LoadAssetsData.Infix(JSON.Parse(paramsJson), asset);
        }

        [Fact]
        public void Starsign_IsRecordedForThatBody()
        {
            Load("{ \"starsign\": \"Capricorn\" }");

            KeyValuePair<string, Zodiac> data = Assert.Single(UniqueIdolSigns);
            Assert.Equal(UniqueIdolKey("Idol Mod", 7), data.Key);
            Assert.Equal(Zodiac.Capricorn, data.Value);
        }

        /// <summary>
        /// Reading the same idol's params.json again (the game reloads mods' portraits) keeps one entry,
        /// with the sign it says now.
        /// </summary>
        [Fact]
        public void ReadAgain_KeepsOneEntryWithTheLatestSign()
        {
            Load("{ \"starsign\": \"Capricorn\" }");
            Load("{ \"starsign\": \"Leo\" }");

            Assert.Equal(Zodiac.Leo, Assert.Single(UniqueIdolSigns).Value);
        }

        /// <summary>
        /// The same body number in two mods is two different idols.
        /// </summary>
        [Fact]
        public void SameBodyInTwoMods_RecordedApart()
        {
            Load("{ \"starsign\": \"Capricorn\" }", modName: "Mod A");
            Load("{ \"starsign\": \"Leo\" }", modName: "Mod B");

            Assert.Equal(Zodiac.Capricorn, UniqueIdolSigns[UniqueIdolKey("Mod A", 7)]);
            Assert.Equal(Zodiac.Leo, UniqueIdolSigns[UniqueIdolKey("Mod B", 7)]);
        }

        [Fact]
        public void Starsign_IgnoresCase()
        {
            Load("{ \"starsign\": \"sagittarius\" }");
            Assert.Equal(Zodiac.Sagittarius, Assert.Single(UniqueIdolSigns).Value);
        }

        [Fact]
        public void NoStarsign_RecordsNothing()
        {
            Load("{ \"left\": 10, \"top\": 20 }");
            Assert.Empty(UniqueIdolSigns);
        }

        /// <summary>
        /// The mod reads each params.json right after the game parses it, while the game's body asset for
        /// that folder is still in hand.
        /// </summary>
        [Fact]
        public void Transpiler_PassesTheParsedJsonAndBodyAsset()
        {
            MethodInfo original = AccessTools.Method(typeof(data_girls_textures), "LoadAssetsData", new[] { typeof(string) });
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(original);
            int originalCount = instructions.Count;

            List<CodeInstruction> patched = data_girls_textures_LoadAssetsData.Transpiler(instructions).ToList();

            MethodInfo infix = AccessTools.Method(typeof(data_girls_textures_LoadAssetsData), nameof(data_girls_textures_LoadAssetsData.Infix));
            Assert.Equal(originalCount + 3, patched.Count);
            int call = patched.FindIndex(i => i.Calls(infix));
            Assert.True(call >= 3, "Infix call not inserted");

            CodeInstruction stored = patched[call - 3];
            Assert.Equal(OpCodes.Stloc_S, stored.opcode);
            LocalVariableInfo json = Assert.IsAssignableFrom<LocalVariableInfo>(stored.operand);
            Assert.Equal(typeof(JSONNode), json.LocalType);

            Assert.Equal(OpCodes.Ldloc_S, patched[call - 2].opcode);
            Assert.Same(json, patched[call - 2].operand);

            Assert.Equal(OpCodes.Ldloc_S, patched[call - 1].opcode);
            LocalVariableInfo asset = Assert.IsAssignableFrom<LocalVariableInfo>(patched[call - 1].operand);
            Assert.Equal(typeof(data_girls_textures._textureAsset), asset.LocalType);
        }

        /// <summary>
        /// If a game update changes how params.json is read, the game's code is left as it is and the mod
        /// says why "starsign" stopped working, instead of failing to load.
        /// </summary>
        [Fact]
        public void Transpiler_GameCodeChanged_LeavesItAndLogs()
        {
            MethodInfo other = AccessTools.Method(typeof(data_girls_textures), "LoadDefaultAssets");
            List<CodeInstruction> instructions = PatchProcessor.GetOriginalInstructions(other);

            List<CodeInstruction> patched = data_girls_textures_LoadAssetsData.Transpiler(instructions).ToList();

            Assert.Equal(instructions.Select(i => i.ToString()), patched.Select(i => i.ToString()));
            Assert.Contains(Log.Messages, m => m.StartsWith("[Star Signs] Couldn't find"));
        }
    }

    /// <summary>
    /// When the game generates a unique idol with a recorded sign, her birthday is moved into that sign
    /// while she keeps her age.
    /// </summary>
    public class StarsignBirthdayTests
    {
        public StarsignBirthdayTests() => TestGame.Reset();

        private const string ModName = "Idol Mod";
        private const int BodyId = 7;

        private static void Record(Zodiac sign, string modName = ModName, int bodyId = BodyId) =>
            UniqueIdolSigns[UniqueIdolKey(modName, bodyId)] = sign;

        /// <summary>
        /// A girl as GenerateGirl returns her, with this body and birthday.
        /// </summary>
        private static data_girls.girls Generated(DateTime birthday, int assetAge = 0, string modName = ModName, int bodyId = BodyId)
        {
            data_girls_textures._textureAsset body = new() { ModName = modName, body_id = bodyId, Age = assetAge };
            return new data_girls.girls
            {
                birthday = birthday,
                textureAssets = new List<data_girls.girls._textureAsset>
                {
                    new() { type = data_girls_textures._spriteType.body, asset = body },
                },
            };
        }

        private static data_girls.girls Generate(data_girls.girls girl, bool genTextures = true)
        {
            data_girls_GenerateGirl.Postfix(ref girl, genTextures);
            return girl;
        }

        public static IEnumerable<object[]> AllSigns() => TestGame.Signs.Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(AllSigns))]
        public void UniqueIdol_GetsHerSign_AtTheAgeInHerParams(Zodiac sign)
        {
            Record(sign);
            data_girls.girls girl = Generate(Generated(TestGame.Birthday(sign == Zodiac.Leo ? Zodiac.Virgo : Zodiac.Leo, 19), assetAge: 16));

            Assert.Equal(sign, GetGirlZodiac(girl));
            Assert.Equal(16, girl.GetAge());
        }

        [Fact]
        public void NoAgeInParams_KeepsHerGeneratedAge()
        {
            Record(Zodiac.Aquarius);
            data_girls.girls girl = Generate(Generated(TestGame.Birthday(Zodiac.Leo, 19)));

            Assert.Equal(Zodiac.Aquarius, GetGirlZodiac(girl));
            Assert.Equal(19, girl.GetAge());
        }

        /// <summary>
        /// Every sign is reachable on every day of the year, without changing the idol's age.
        /// </summary>
        [Fact]
        public void EverySign_OnEveryDay_KeepsHerAge()
        {
            for (DateTime today = new(2024, 1, 1); today.Year == 2024; today = today.AddDays(1))
            {
                staticVars.dateTime = today;
                foreach (Zodiac sign in TestGame.Signs)
                {
                    UniqueIdolSigns.Clear();
                    Record(sign);
                    Seams.Seeded(today.DayOfYear);

                    data_girls.girls girl = Generate(Generated(today.AddYears(-18), assetAge: 18));

                    Assert.True(sign == GetGirlZodiac(girl), $"{sign} on {today:d}: got {GetGirlZodiac(girl)} ({girl.birthday:d})");
                    Assert.True(girl.GetAge() == 18, $"{sign} on {today:d}: aged {girl.GetAge()} ({girl.birthday:d})");
                }
            }
        }

        /// <summary>
        /// Generated on 20 February, a Pisces aged 18: a roll of March, 11 months back, then 29 days back
        /// from 20 March 2004 lands on 20 February 2004, which would make her 19, so the mod rolls again.
        /// </summary>
        [Fact]
        public void LongestRollBack_RollsAgain()
        {
            staticVars.dateTime = new DateTime(2023, 2, 20);
            Record(Zodiac.Pisces);
            Seams.Rolls(1, 29, 1, 0);

            data_girls.girls girl = Generate(Generated(new DateTime(2004, 8, 1), assetAge: 18));

            Assert.Equal(new DateTime(2004, 3, 20), girl.birthday);
            Assert.Equal(18, girl.GetAge());
        }

        /// <summary>
        /// A "starsign" the mod can't read (a typo, "None", a number that isn't a sign) is skipped with a
        /// warning, and the idol keeps the birthday the game rolled.
        /// </summary>
        [Theory]
        [InlineData("Capricon")]
        [InlineData("None")]
        [InlineData("13")]
        public void UnknownStarsign_LeavesHerBirthday(string starsign)
        {
            data_girls_textures_LoadAssetsData.Infix(JSON.Parse($"{{ \"starsign\": \"{starsign}\" }}"),
                new data_girls_textures._textureAsset { ModName = ModName, body_id = BodyId });
            DateTime birthday = TestGame.Birthday(Zodiac.Leo);

            Assert.Empty(UniqueIdolSigns);
            Assert.Contains("Star sign not found: " + starsign, Log.Messages);
            Assert.Equal(birthday, Generate(Generated(birthday)).birthday);
        }

        [Fact]
        public void AlreadyInHerSign_Unchanged()
        {
            Record(Zodiac.Leo);
            DateTime birthday = TestGame.Birthday(Zodiac.Leo, 18);

            Assert.Equal(birthday, Generate(Generated(birthday, assetAge: 18)).birthday);
            Assert.Equal(0, Seams.RangeCalls);
        }

        [Theory]
        [InlineData("Other Mod", BodyId)]
        [InlineData(ModName, BodyId + 1)]
        public void OtherBodies_Unchanged(string modName, int bodyId)
        {
            Record(Zodiac.Capricorn);
            DateTime birthday = TestGame.Birthday(Zodiac.Leo);

            Assert.Equal(birthday, Generate(Generated(birthday, modName: modName, bodyId: bodyId)).birthday);
        }

        [Fact]
        public void WithoutTextures_Unchanged()
        {
            Record(Zodiac.Capricorn);
            DateTime birthday = TestGame.Birthday(Zodiac.Leo);

            Assert.Equal(birthday, Generate(Generated(birthday), genTextures: false).birthday);
        }

        [Fact]
        public void NoTextureAssets_Unchanged()
        {
            Record(Zodiac.Capricorn);
            data_girls.girls girl = Generated(TestGame.Birthday(Zodiac.Leo));
            girl.textureAssets.Clear();

            Assert.Equal(TestGame.Birthday(Zodiac.Leo), Generate(girl).birthday);
        }
    }
}
