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
        [InlineData("7", 7)]
        [InlineData("3.0", 3)]
        [InlineData("8", 2)]
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
    }
}
