using AICore;
using System.IO;
using Xunit;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The key never reaches a save: ModMenus' Apply saves every row through variables.Set, and the mod's
    /// prefix files the key away and saves "".
    /// </summary>
    public class KeyStoreTests
    {
        public KeyStoreTests() => TestEnv.Reset();

        private static string ApplyKeyRow(string typed)
        {
            string value = typed;
            variables_Set.Prefix(Settings.ApiKeyVar, ref value);
            return value;
        }

        [Fact]
        public void TypedKey_IsFiledAndTheSaveGetsBlank()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            Assert.Equal("", ApplyKeyRow("  sk-secret-9876  "));
            Assert.Equal("sk-secret-9876", KeyStore.Get(Provider.OpenAI));
            Assert.Equal("sk-secret-9876", File.ReadAllText(Path.Combine(TestEnv.KeyFolder, "openai.key")));
        }

        [Fact]
        public void BlankField_KeepsTheKey()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            KeyStore.Set(Provider.OpenAI, "kept-key");
            Assert.Equal("", ApplyKeyRow(""));
            Assert.Equal("kept-key", KeyStore.Get(Provider.OpenAI));
        }

        [Fact]
        public void Dash_RemovesTheKey()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            KeyStore.Set(Provider.OpenAI, "old-key");
            Assert.Equal("", ApplyKeyRow("-"));
            Assert.Null(KeyStore.Get(Provider.OpenAI));
            Assert.False(File.Exists(Path.Combine(TestEnv.KeyFolder, "openai.key")));
        }

        [Fact]
        public void EachProvider_HasItsOwnKey()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            TestEnv.Vars[Settings.ProviderVar] = "1";
            ApplyKeyRow("openai-key");
            TestEnv.Vars[Settings.ProviderVar] = "0";
            ApplyKeyRow("anthropic-key");
            KeyStore.ClearCache();
            Assert.Equal("openai-key", KeyStore.Get(Provider.OpenAI));
            Assert.Equal("anthropic-key", KeyStore.Get(Provider.Anthropic));
            Assert.Null(KeyStore.Get(Provider.Compatible));
        }

        [Fact]
        public void OtherVariables_AreUntouched()
        {
            string value = "female_name";
            variables_Set.Prefix("SomeOtherMod_Name", ref value);
            Assert.Equal("female_name", value);
            Assert.False(Directory.Exists(TestEnv.KeyFolder));
        }

        /// <summary>
        /// Apply saves every row, changed or not. Pressing it with nothing changed lifts a stop and tests
        /// the connection, so the player can try again after fixing the cause; with nothing stopped it does
        /// nothing, so it costs nothing.
        /// </summary>
        [Fact]
        public void UnchangedApply_LiftsAStop_AndOnlyThen()
        {
            TestEnv.Configure();
            FakeHost host = new();
            FakeTransport transport = new();
            Dispatcher.Instance = new Dispatcher(host, transport, new System.Random(1));

            ApplyKeyRow("");
            Assert.Empty(host.Frames);

            Assert.True(Dispatcher.Instance.Request(new Moment("a test").Add("Title", () => "Song")));
            transport.Reply(200, Replies.Anthropic("no posts here"));
            Assert.True(Dispatcher.Instance.Suspended);

            // Other rows saved unchanged don't count as pressing Apply
            variables.variable.Add(new variables._variable { name = Settings.ModelVar, value = "" });
            string model = "";
            variables_Set.Prefix(Settings.ModelVar, ref model);
            Assert.True(Dispatcher.Instance.Suspended);

            ApplyKeyRow("");
            Assert.False(Dispatcher.Instance.Suspended);
            Assert.Single(host.Frames);
        }

        [Fact]
        public void SettingChange_StartsAConnectionTestOnTheNextFrame()
        {
            FakeHost host = new();
            Dispatcher.Instance = new Dispatcher(host, new FakeTransport(), new System.Random(1));
            string value = "claude-sonnet-5-5";
            variables_Set.Prefix(Settings.ModelVar, ref value);
            Assert.Single(host.Frames);

            // Setting the same value again (every Apply saves every row) changes nothing
            Dispatcher.Instance = new Dispatcher(host = new FakeHost(), new FakeTransport(), new System.Random(1));
            variables.variable.Add(new variables._variable { name = Settings.ModelVar, value = "same" });
            value = "same";
            variables_Set.Prefix(Settings.ModelVar, ref value);
            Assert.Empty(host.Frames);
        }
    }
}
