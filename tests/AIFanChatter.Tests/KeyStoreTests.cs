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
