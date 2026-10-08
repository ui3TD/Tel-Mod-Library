using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using static staticVars._playerData;

namespace FanAttrition.Tests
{
    /// <summary>
    /// The in-game and Steam text matches what the code does.
    /// </summary>
    public class AssetTests
    {
        public AssetTests() => TestGame.Reset();

        /// <summary>
        /// From the game's own business.json.
        /// </summary>
        private static readonly int[] GameDramaNewFans = { 20, 30, 40, 50, 60, 80, 100, 120, 160, 200 };
        private const int GameMaxStamina = 30;

        private static string SteamDescription() => File.ReadAllText(TestGame.ModAsset("steam description.txt"));

        private static Dictionary<string, string> Constants()
        {
            JSONNode constants = TestGame.LoadJson("JSON/Constants/constants.json");
            return Enumerable.Range(0, constants.Count).ToDictionary(i => (string)constants[i]["id"], i => (string)constants[i]["text"]);
        }

        private static int[] Ints(JSONNode array) => Enumerable.Range(0, array.Count).Select(i => array[i].AsInt).ToArray();

        private static JSONNode Business(string type)
        {
            JSONNode business = TestGame.LoadJson("JSON/Business/business.json");
            return Enumerable.Range(0, business.Count).Select(i => business[i]).Single(b => (string)b["type"] == type);
        }

        [Fact]
        public void Constants_DefineEveryLabelTheCodeUses()
        {
            Dictionary<string, string> text = Constants();
            string[] labels = typeof(Utility).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.Name.EndsWith("_LABEL"))
                .Select(f => (string)f.GetRawConstantValue())
                .ToArray();

            Assert.Equal(7, labels.Length);
            foreach (string label in labels)
                Assert.False(string.IsNullOrWhiteSpace(text.TryGetValue(label, out string value) ? value : null), label);
        }

        /// <summary>
        /// Each PV's description names the two stats the code averages, and the success rate is their average.
        /// </summary>
        [Theory]
        [InlineData("LEWD", singles._param._special_type.lewd_pv, "sexy", "cute")]
        [InlineData("EDGY", singles._param._special_type.edgy_pv, "cool", "funny")]
        [InlineData("ARTSY", singles._param._special_type.artsy_pv, "pretty", "smart")]
        public void PvDescriptions_MatchTheirStats(string id, singles._param._special_type pv, string stat1, string stat2)
        {
            string description = Constants()[$"MARKETING__{id}_PV_DESCRIPTION"];
            Assert.EndsWith("Success rate is equal to formation's average {green}" + stat1 + "</color> and {green}" + stat2 + "</color> stats", description);

            singles._single single = MarketingTests.Formation(
                ((data_girls._paramType)Enum.Parse(typeof(data_girls._paramType), stat1), 50f),
                ((data_girls._paramType)Enum.Parse(typeof(data_girls._paramType), stat2), 70f));
            singles._param param = MarketingTests.Marketing(pv);
            float success = MarketingTests.Chance(param, Single_Marketing_Roll._result.success, single)
                + MarketingTests.Chance(param, Single_Marketing_Roll._result.success_crit, single);
            Assert.Equal(60f, success, 3);
        }

        /// <summary>
        /// Each host's description gives the new fan bonus the code gives for her fame.
        /// </summary>
        [Fact]
        public void HostDescriptions_MatchTheFanBonus()
        {
            JSONNode hosts = TestGame.LoadJson("JSON/Shows/mc.json");
            Assert.Equal(6, hosts.Count);

            for (int i = 0; i < hosts.Count; i++)
            {
                Shows._show show = new() { mc = new Shows._param { fame = hosts[i]["fame"].AsInt } };
                int bonus = (int)Math.Round(Shows__show_SetSales_MC.Infix(show, 1000) / 10.0) - 100;

                Match described = Regex.Match(hosts[i]["description"], @"\{green\}\+(\d+)%</color>");
                Assert.True(described.Success, (string)hosts[i]["title"]);
                Assert.Equal(bonus, int.Parse(described.Groups[1].Value));
            }
        }

        [Fact]
        public void Business_ChangesOnlyAdsAndDramas()
        {
            JSONNode business = TestGame.LoadJson("JSON/Business/business.json");

            Assert.Equal(new[] { "ad", "tv_drama" }, Enumerable.Range(0, business.Count).Select(i => (string)business[i]["type"]));
        }

        /// <summary>
        /// Ads' and dramas' stamina tops out at 20, down from the game's 30.
        /// </summary>
        [Theory]
        [InlineData("ad")]
        [InlineData("tv_drama")]
        public void Business_StaminaTopsOutAt20(string type)
        {
            int[] stamina = Ints(Business(type)["stamina"]);

            Assert.Equal(10, stamina.Length);
            Assert.Equal(20, stamina.Max());
            Assert.Equal(20, stamina.Last());
            Assert.True(stamina.Max() < GameMaxStamina);
        }

        /// <summary>
        /// The Steam description says "over 3x the fans". That holds for levels 1-7; levels 8-10 give
        /// 2.9x, 2.5x and 2.5x.
        /// </summary>
        [Fact]
        public void Business_DramasGiveMoreFans()
        {
            int[] newFans = Ints(Business("tv_drama")["newFans"]);

            Assert.Equal(GameDramaNewFans.Length, newFans.Length);
            for (int level = 0; level < newFans.Length; level++)
                Assert.True(newFans[level] >= 2.5 * GameDramaNewFans[level], $"Level {level + 1}: {newFans[level]}");
            Assert.All(newFans.Take(7).Zip(GameDramaNewFans, (mod, game) => (mod, game)), l => Assert.True(l.mod >= 3 * l.game));
        }

        /// <summary>
        /// "At 100k fans, you can expect a fan churn rate of 2% per month in Normal mode and 5% per month in Unfair mode."
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal, 2)]
        [InlineData(_difficulty.hard, 5)]
        public void SteamDescription_ChurnAt100k(_difficulty difficulty, int percentPerMonth)
        {
            Assert.Contains("At 100k fans, you can expect a fan churn rate of 2% per month in Normal mode and 5% per month in Unfair mode", SteamDescription());

            TestGame.SetDifficulty(difficulty);
            TestGame.FanBase(100000);
            // The first day works out the churn, and each of the next 30 takes it
            for (int day = 0; day <= 30; day++)
                Utility.DailyFanChurn();

            Assert.Equal(percentPerMonth, (int)Math.Round((100000 - resources.GetFansTotal()) / 1000.0));
        }

        /// <summary>
        /// "In Normal mode you get 50% audience at 100% fatigue, but in Unfair you get 20% audience at 100% fatigue."
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal, 0.5f)]
        [InlineData(_difficulty.hard, 0.2f)]
        public void SteamDescription_AudienceAtFullFatigue(_difficulty difficulty, float audience)
        {
            Assert.Contains("In Normal mode you get 50% audience at 100% fatigue, but in Unfair you get 20% audience at 100% fatigue", SteamDescription());

            TestGame.SetDifficulty(difficulty);
            foreach (Shows._param._media_type medium in new[] { Shows._param._media_type.tv, Shows._param._media_type.radio })
            {
                Shows._show show = new() { medium = new Shows._param { media_type = medium }, episodeCount = 1 };
                show.fatigue.Add(100f);

                Assert.Equal(audience, Shows__show_SetSales_Fatigue.Infix(show, 1f), 3);
            }
        }

        /// <summary>
        /// "Audience of TV and Radio shows decrease with fatigue [...] Internet shows aren't affected."
        /// </summary>
        [Theory]
        [InlineData(_difficulty.normal)]
        [InlineData(_difficulty.hard)]
        public void SteamDescription_InternetShowsIgnoreFatigue(_difficulty difficulty)
        {
            string description = SteamDescription();
            Assert.Contains("Audience of TV and Radio shows decrease with fatigue", description);
            Assert.Contains("Internet shows aren't affected.", description);

            TestGame.SetDifficulty(difficulty);
            Shows._show show = new() { medium = new Shows._param { media_type = Shows._param._media_type.internet }, episodeCount = 1 };
            show.fatigue.Add(100f);

            Assert.Equal(1f, Shows__show_SetSales_Fatigue.Infix(show, 1f));
        }

        /// <summary>
        /// "Single PVs have success chances increased by 33%"
        /// </summary>
        [Theory]
        [MemberData(nameof(MarketingTests.PVs), MemberType = typeof(MarketingTests))]
        public void SteamDescription_PvSuccessUpAThird(singles._param._special_type pv)
        {
            Assert.Contains("Single PVs have success chances increased by 33%", SteamDescription());

            singles._single single = MarketingTests.Formation(pv, 60);
            singles._param param = MarketingTests.Marketing(pv);
            Single_Marketing_Roll._result[] successes = { Single_Marketing_Roll._result.success, Single_Marketing_Roll._result.success_crit };
            float game = successes.Sum(roll => param.GetSuccessChance(roll, 1, single));
            float mod = successes.Sum(roll => MarketingTests.Chance(param, roll, single));

            Assert.Equal(33, (int)Math.Round((mod / game - 1) * 100));
        }

        /// <summary>
        /// The Steam changelog ends with an entry for the version in the project file, short enough for
        /// the game's 100-character change notes box.
        /// </summary>
        [Fact]
        public void SteamChangelog_ListsCurrentVersion()
        {
            string version = Regex.Match(File.ReadAllText(TestGame.ModFile("Fan Attrition.csproj")), "<Version>(.+)</Version>").Groups[1].Value;
            string last = File.ReadAllLines(TestGame.ModAsset("steam description.txt")).Last();
            Assert.StartsWith($"- {version}: ", last);
            Assert.InRange(last.Length - "- ".Length, 1, 100);
        }
    }
}
