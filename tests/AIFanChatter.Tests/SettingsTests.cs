using AICore;
using SimpleJSON;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// With ModMenus each setting starts at the menu's default; without it, at the code's. They must match,
    /// and every label must exist.
    /// </summary>
    public class ModMenuTests
    {
        private static string Assets([CallerFilePath] string testFile = "") =>
            Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "AI Fan Chatter", "assets", "JSON");

        private static List<JSONNode> Items()
        {
            JSONArray items = JSON.Parse(File.ReadAllText(Path.Combine(Assets(), "Mod Menu", "modmenu.json"))).AsArray;
            return Enumerable.Range(0, items.Count).Select(i => items[i]).ToList();
        }

        private static JSONNode Item(string varID) => Items().Single(i => i["varID"].Value == varID);

        [Fact]
        public void CodeDefaults_MatchTheModMenu()
        {
            Assert.Equal(Settings.DefaultEnabled, Item(Settings.EnabledVar)["defaultValue"].AsBool);
            Assert.Equal((int)Settings.DefaultProvider, Item(Settings.ProviderVar)["defaultValue"].AsInt);
            Assert.Equal("", Item(Settings.BaseUrlVar)["defaultValue"].Value);
            Assert.Equal("", Item(Settings.ModelVar)["defaultValue"].Value);
            Assert.Equal("", Item(Settings.ApiKeyVar)["defaultValue"].Value);
            Assert.Equal(Settings.DefaultPerWeek, Item(Settings.PerWeekVar)["defaultValue"].AsInt);
            Assert.Equal(0, Item(Settings.PerWeekVar)["minValue"].AsInt);
            Assert.Equal(Settings.MaxPerWeek, Item(Settings.PerWeekVar)["maxValue"].AsInt);
        }

        [Fact]
        public void Dropdowns_ListEveryChoiceInOrder()
        {
            Assert.Equal(3, Item(Settings.ProviderVar)["itemIDList"].AsArray.Count);
            Assert.EndsWith("ANTHROPIC", Item(Settings.ProviderVar)["itemIDList"][(int)Provider.Anthropic].Value);
            Assert.EndsWith("OPENAI", Item(Settings.ProviderVar)["itemIDList"][(int)Provider.OpenAI].Value);
            Assert.EndsWith("COMPATIBLE", Item(Settings.ProviderVar)["itemIDList"][(int)Provider.Compatible].Value);
        }

        /// <summary>
        /// ModMenus' Reset to Defaults would blank the key field, and Apply would then remove the saved key
        /// (KeyStore.Apply), so the key row opts out of Reset. Every other row is reset.
        /// </summary>
        [Fact]
        public void KeyRow_IsTheOnlyRowResetLeavesAlone()
        {
            Assert.Equal(new[] { Settings.ApiKeyVar }, Items().Where(i => i["reset"].Value == "false").Select(i => i["varID"].Value));
        }

        /// <summary>
        /// The key row must come after the provider row: Apply saves rows in order, and the key is filed under
        /// the provider just saved.
        /// </summary>
        [Fact]
        public void ProviderRow_ComesBeforeKeyRow()
        {
            List<string> order = Items().Select(i => i["varID"].Value).ToList();
            Assert.True(order.IndexOf(Settings.ProviderVar) < order.IndexOf(Settings.ApiKeyVar));
        }

        [Fact]
        public void EveryLabel_HasText()
        {
            JSONArray constants = JSON.Parse(File.ReadAllText(Path.Combine(Assets(), "Constants", "constants.json"))).AsArray;
            HashSet<string> ids = new(Enumerable.Range(0, constants.Count).Select(i => constants[i]["id"].Value));
            foreach (JSONNode item in Items())
            {
                Assert.Contains(item["labelID"].Value, ids);
                JSONArray list = item["itemIDList"].AsArray;
                for (int i = 0; list != null && i < list.Count; i++)
                {
                    Assert.Contains(list[i].Value, ids);
                }
            }
        }

        /// <summary>
        /// ModMenus (3.0.0 and later) shows the key as asterisks.
        /// </summary>
        [Fact]
        public void KeyField_IsTheOnlyPasswordField()
        {
            Assert.True(Item(Settings.ApiKeyVar)["password"].AsBool);
            Assert.All(Items().Where(i => i["varID"].Value != Settings.ApiKeyVar), i => Assert.False(i["password"].AsBool));
        }

        [Fact]
        public void EverySetting_UsesTheModsPrefix()
        {
            foreach (JSONNode item in Items().Where(i => i["type"].Value != "text"))
            {
                Assert.StartsWith(Settings.Prefix, item["varID"].Value);
            }
        }

        /// <summary>
        /// The settings show once the mod is turned on, and Base URL only while UsesBaseUrlVar is on, which the
        /// mod answers itself. No row has UsesBaseUrlVar, so ModMenus never saves it.
        /// </summary>
        [Fact]
        public void SettingRows_ShowOnlyWhenTheyApply()
        {
            Dictionary<string, string> conditions = Items().Where(i => i["type"].Value != "text").ToDictionary(i => i["varID"].Value, i => i["enabledIf"].Value);
            Assert.Equal(new Dictionary<string, string>
            {
                [Settings.EnabledVar] = "",
                [Settings.ProviderVar] = Settings.EnabledVar,
                [Settings.BaseUrlVar] = Settings.UsesBaseUrlVar,
                [Settings.ModelVar] = Settings.EnabledVar,
                [Settings.ApiKeyVar] = Settings.EnabledVar,
                [Settings.PerWeekVar] = Settings.EnabledVar,
            }, conditions);
            Assert.All(Items().Where(i => i["type"].Value == "text"), i => Assert.Equal("", i["enabledIf"].Value));
        }
    }

    public class SettingsTests
    {
        public SettingsTests() => TestEnv.Reset();

        [Fact]
        public void NoSettings_GiveTheDefaults()
        {
            Assert.False(Settings.Enabled);
            Assert.Equal(Provider.OpenAI, Settings.Provider);
            Assert.Equal(2, Settings.PerWeek);
            Assert.Equal("", Settings.Model);
            Assert.Equal("", Settings.BaseUrl);
        }

        [Theory]
        [InlineData("1", true)]
        [InlineData("1.0", true)]
        [InlineData("0", false)]
        [InlineData("2", false)]
        [InlineData("yes", false)]
        public void Enabled_ReadsModMenusCheckbox(string saved, bool expected)
        {
            TestEnv.Vars[Settings.EnabledVar] = saved;
            Assert.Equal(expected, Settings.Enabled);
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("21", 21)]
        [InlineData("3.0", 3)]
        [InlineData("22", 2)]
        [InlineData("-1", 2)]
        [InlineData("NaN", 2)]
        [InlineData("", 2)]
        public void PerWeek_OutOfRangeGivesTheDefault(string saved, int expected)
        {
            TestEnv.Vars[Settings.PerWeekVar] = saved;
            Assert.Equal(expected, Settings.PerWeek);
        }

        [Fact]
        public void Provider_PastTheListGivesTheDefault()
        {
            TestEnv.Vars[Settings.ProviderVar] = "5";
            Assert.Equal(Settings.DefaultProvider, Settings.Provider);
            TestEnv.Vars[Settings.ProviderVar] = "2";
            Assert.Equal(Provider.Compatible, Settings.Provider);
        }

        [Fact]
        public void Numbers_IgnoreTheRegionalFormat()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                TestEnv.Vars[Settings.PerWeekVar] = "3.0";
                Assert.Equal(3, Settings.PerWeek);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void TextSettings_AreTrimmed()
        {
            TestEnv.Vars[Settings.ModelVar] = "  gpt-x \t";
            TestEnv.Vars[Settings.BaseUrlVar] = " http://localhost:11434 ";
            Assert.Equal("gpt-x", Settings.Model);
            Assert.Equal("http://localhost:11434", Settings.BaseUrl);
        }

        /// <summary>
        /// The Base URL row's enabledIf: on only while the mod is on and OpenAI-compatible is picked, read as
        /// Enabled and Provider are read.
        /// </summary>
        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, "2", false)]
        [InlineData("0", "2", false)]
        [InlineData("2", "2", false)]
        [InlineData("1", null, false)]
        [InlineData("1", "0", false)]
        [InlineData("1", "1", false)]
        [InlineData("1", "5", false)]
        [InlineData("1", "2", true)]
        [InlineData("1.0", "2.0", true)]
        public void UsesBaseUrl_OnlyWhenOnWithOpenAICompatible(string enabled, string provider, bool expected)
        {
            if (enabled != null)
                TestEnv.Vars[Settings.EnabledVar] = enabled;
            if (provider != null)
                TestEnv.Vars[Settings.ProviderVar] = provider;
            Assert.Equal(expected, Settings.UsesBaseUrl);
        }

        /// <summary>
        /// While ModMenus' menu is open, what it shows wins over the saved settings, so the Base URL row follows
        /// the player's choices before Apply. Settings it has no row for are read as saved.
        /// </summary>
        [Fact]
        public void UsesBaseUrl_FollowsTheOpenMenu()
        {
            TestEnv.Vars[Settings.EnabledVar] = "0";
            TestEnv.Vars[Settings.ProviderVar] = "1";
            Dictionary<string, string> menu = new() { [Settings.EnabledVar] = "1", [Settings.ProviderVar] = "2" };
            Settings.MenuValue = varID => menu.TryGetValue(varID, out string value) ? value : null;
            Assert.True(Settings.UsesBaseUrl);

            menu[Settings.ProviderVar] = "1";
            Assert.False(Settings.UsesBaseUrl);

            menu.Remove(Settings.ProviderVar);
            TestEnv.Vars[Settings.ProviderVar] = "2";
            Assert.True(Settings.UsesBaseUrl);
        }

        /// <summary>
        /// ModMenus reads the Base URL row's enabledIf through variables.Get, and the mod answers it, whatever is
        /// stored under that name. If working it out fails, the row shows, so the address can still be set.
        /// </summary>
        [Fact]
        public void GetPatch_AnswersUsesBaseUrl()
        {
            string Get(string stored)
            {
                string result = stored;
                variables_Get.Postfix(Settings.UsesBaseUrlVar, ref result);
                return result;
            }

            Assert.Equal("0", Get(null));
            TestEnv.Configure(Provider.Compatible, baseUrl: "http://localhost:11434");
            Assert.Equal("1", Get(null));
            Assert.Equal("1", Get("0"));
            TestEnv.Vars[Settings.EnabledVar] = "0";
            Assert.Equal("0", Get("1"));

            Settings.MenuValue = _ => throw new System.InvalidOperationException();
            Assert.Equal("1", Get(null));
        }
    }
}
