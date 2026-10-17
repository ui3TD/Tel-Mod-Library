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
            Assert.Equal("", ApplyKeyRow("  sk-ant-secret-9876  "));
            Assert.Equal("sk-ant-secret-9876", KeyStore.Get(Provider.Anthropic));
            Assert.Equal("sk-ant-secret-9876", File.ReadAllText(Path.Combine(TestEnv.KeyFolder, "anthropic.key")));
        }

        [Fact]
        public void BlankField_KeepsTheKey()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            KeyStore.Set(Provider.Anthropic, "kept-key");
            Assert.Equal("", ApplyKeyRow(""));
            Assert.Equal("kept-key", KeyStore.Get(Provider.Anthropic));
        }

        [Fact]
        public void Dash_RemovesTheKey()
        {
            Dispatcher.Instance = new Dispatcher(new FakeHost(), new FakeTransport(), new System.Random(1));
            KeyStore.Set(Provider.Anthropic, "old-key");
            Assert.Equal("", ApplyKeyRow("-"));
            Assert.Null(KeyStore.Get(Provider.Anthropic));
            Assert.False(File.Exists(Path.Combine(TestEnv.KeyFolder, "anthropic.key")));
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
        public void KeyFolder_IsOutsideSavesAndMods()
        {
            string root = Path.Combine(Path.GetTempPath(), "LocalLow", "Idol Manager");
            string folder = Path.GetFullPath(KeyStore.FolderUnder(root)) + Path.DirectorySeparatorChar;
            Assert.StartsWith(Path.GetFullPath(root), folder);
            Assert.DoesNotContain(Path.Combine(root, "data") + Path.DirectorySeparatorChar, folder);
            Assert.DoesNotContain(Path.Combine(root, "Mods") + Path.DirectorySeparatorChar, folder);
        }

        [Fact]
        public void Hint_ShowsOnlyTheLastFourCharacters()
        {
            KeyStore.Set(Provider.Anthropic, "sk-ant-abcdefgh-WXYZ");
            string hint = KeyStore.Hint(Provider.Anthropic);
            Assert.Contains("WXYZ", hint);
            Assert.DoesNotContain("abcdefgh", hint);
            Assert.Contains("No key saved", KeyStore.Hint(Provider.OpenAI));
        }

        [Fact]
        public void Redact_HidesEverySavedKey()
        {
            KeyStore.Set(Provider.OpenAI, "sk-proj-SECRET");
            Assert.Equal("invalid key: ***", KeyStore.Redact("invalid key: sk-proj-SECRET"));
        }

        [Fact]
        public void LogLines_NeverHoldTheKey()
        {
            KeyStore.Set(Provider.Anthropic, "sk-ant-LEAK");
            Log.Warn("echo sk-ant-LEAK");
            Assert.DoesNotContain("sk-ant-LEAK", TestEnv.Logs[0]);
        }

        [Fact]
        public void UnreadableKeyFile_DoesNotLoopOrThrow()
        {
            Directory.CreateDirectory(TestEnv.KeyFolder);
            // Another program holds the file open
            using (new FileStream(Path.Combine(TestEnv.KeyFolder, "anthropic.key"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Null(KeyStore.Get(Provider.Anthropic));
            }
            Assert.Single(TestEnv.Logs);
        }

        [Fact]
        public void Scrub_MovesAKeyWrittenStraightIntoTheVariables()
        {
            variables.variable.Add(new variables._variable { name = Settings.ApiKeyVar, value = "sneaky-key" });
            Assert.True(KeyStore.Scrub(variables.variable, Provider.Anthropic));
            Assert.Equal("", variables.Get(Settings.ApiKeyVar));
            Assert.Equal("sneaky-key", KeyStore.Get(Provider.Anthropic));
            Assert.False(KeyStore.Scrub(variables.variable, Provider.Anthropic));
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
