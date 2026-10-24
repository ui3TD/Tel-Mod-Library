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
        /// Every row is reset by ModMenus' Reset to Defaults; the keys are secret rows, which Reset leaves alone.
        /// </summary>
        [Fact]
        public void NoRow_OptsOutOfReset()
        {
            Assert.DoesNotContain(Items(), i => i["reset"].Value == "false");
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
        /// Each service's key is a ModMenus secret row (ModMenus 5.0.0 and later) naming its key file, shown while
        /// that service is picked. No row uses ModMenus' removed password option.
        /// </summary>
        [Fact]
        public void KeyRows_AreSecretRows_OnePerService()
        {
            List<JSONNode> secrets = Items().Where(i => i["type"].Value == "secret").ToList();
            Assert.Equal(new[]
            {
                (KeyStore.SecretID(Provider.Anthropic), Settings.UsesAnthropicVar),
                (KeyStore.SecretID(Provider.OpenAI), Settings.UsesOpenAIVar),
                (KeyStore.SecretID(Provider.Compatible), Settings.UsesCompatibleVar),
            }, secrets.Select(i => (i["secretID"].Value, i["enabledIf"].Value)));
            Assert.All(Items(), i => Assert.Equal("", i["password"].Value));
            Assert.Contains(Items(), i => i["type"].Value == "input" && i["varID"].Value == Settings.ModelVar);
            Assert.DoesNotContain(Items(), i => i["varID"].Value == Settings.ApiKeyVar);
        }

        [Fact]
        public void EverySetting_UsesTheModsPrefix()
        {
            foreach (JSONNode item in Items().Where(i => i["type"].Value != "text" && i["type"].Value != "secret"))
            {
                Assert.StartsWith(Settings.Prefix, item["varID"].Value);
            }
        }

        /// <summary>
        /// Only Base URL and the keys are hidden at times: each shows while its service is picked, which the mod
        /// answers itself (see Settings.ProviderShownBy). Every other setting shows whether or not the mod is on,
        /// so the service and key can be set up first. No row has those variables, so ModMenus never saves them.
        /// </summary>
        [Fact]
        public void OnlyBaseUrlAndKeyRows_AreHiddenAtTimes()
        {
            Dictionary<string, string> conditions = Items().Where(i => i["type"].Value != "text" && i["type"].Value != "secret").ToDictionary(i => i["varID"].Value, i => i["enabledIf"].Value);
            Assert.Equal(new Dictionary<string, string>
            {
                [Settings.EnabledVar] = "",
                [Settings.ProviderVar] = "",
                [Settings.BaseUrlVar] = Settings.UsesBaseUrlVar,
                [Settings.ModelVar] = "",
                [Settings.PerWeekVar] = "",
            }, conditions);
            Assert.All(Items().Where(i => i["type"].Value == "text"), i => Assert.Equal("", i["enabledIf"].Value));
            Assert.All(Items().Where(i => i["enabledIf"].Value != ""), i => Assert.NotNull(Settings.ProviderShownBy(i["enabledIf"].Value)));
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
        /// The Base URL row's enabledIf: on only while OpenAI-compatible is picked, read as Provider is read,
        /// whether or not the mod is on.
        /// </summary>
        [Theory]
        [InlineData(null, false)]
        [InlineData("0", false)]
        [InlineData("1", false)]
        [InlineData("5", false)]
        [InlineData("2", true)]
        [InlineData("2.0", true)]
        public void UsesBaseUrl_OnlyWithOpenAICompatible(string provider, bool expected)
        {
            if (provider != null)
                TestEnv.Vars[Settings.ProviderVar] = provider;
            Assert.Equal(expected, Settings.UsesBaseUrl);
            TestEnv.Vars[Settings.EnabledVar] = "1";
            Assert.Equal(expected, Settings.UsesBaseUrl);
        }

        /// <summary>
        /// While ModMenus' menu is open, what it shows wins over the saved settings, so the Base URL row follows
        /// the player's pick before Apply. Without a Provider row in the menu, the saved setting decides.
        /// </summary>
        [Fact]
        public void UsesBaseUrl_FollowsTheOpenMenu()
        {
            TestEnv.Vars[Settings.ProviderVar] = "1";
            Dictionary<string, string> menu = new() { [Settings.ProviderVar] = "2" };
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
            TestEnv.Vars[Settings.ProviderVar] = ((int)Provider.OpenAI).ToString();
            Assert.Equal("0", Get("1"));

            Settings.MenuValue = _ => throw new System.InvalidOperationException();
            Assert.Equal("1", Get(null));
        }

        /// <summary>
        /// Each key row shows only while its service is picked, following the open menu before Apply, so the line
        /// under it always tells about the service on screen. If working it out fails, every key row shows.
        /// </summary>
        [Fact]
        public void GetPatch_ShowsOnlyThePickedServicesKeyRow()
        {
            string[] Shown()
            {
                return new[] { Settings.UsesAnthropicVar, Settings.UsesOpenAIVar, Settings.UsesCompatibleVar }.Select(name =>
                {
                    string result = null;
                    variables_Get.Postfix(name, ref result);
                    return result;
                }).ToArray();
            }

            // Nothing saved: OpenAI, the default
            Assert.Equal(new[] { "0", "1", "0" }, Shown());
            TestEnv.Vars[Settings.ProviderVar] = ((int)Provider.Anthropic).ToString();
            Assert.Equal(new[] { "1", "0", "0" }, Shown());

            Dictionary<string, string> menu = new() { [Settings.ProviderVar] = ((int)Provider.Compatible).ToString() };
            Settings.MenuValue = varID => menu.TryGetValue(varID, out string value) ? value : null;
            Assert.Equal(new[] { "0", "0", "1" }, Shown());

            Settings.MenuValue = _ => throw new System.InvalidOperationException();
            Assert.Equal(new[] { "1", "1", "1" }, Shown());
        }

        [Fact]
        public void GetPatch_LeavesOtherVariablesAlone()
        {
            TestEnv.Configure(Provider.OpenAI);
            foreach ((string name, string stored) in new[] { ("SomeOtherMod_Name", null), (Settings.EnabledVar, "1"), (Settings.ApiKeyVar, "") })
            {
                string result = stored;
                variables_Get.Postfix(name, ref result);
                Assert.Equal(stored, result);
            }
        }
    }
}
