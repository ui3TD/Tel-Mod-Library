using System.IO;
using Xunit;

namespace AICore.Tests
{
    public class KeyStoreTests
    {
        public KeyStoreTests() => TestEnv.Reset();

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
            variables.variable.Add(new variables._variable { name = ConnectionSettings.ApiKeyVar, value = "sneaky-key" });
            Assert.True(KeyStore.Scrub(variables.variable, Provider.Anthropic));
            Assert.Equal("", variables.Get(ConnectionSettings.ApiKeyVar));
            Assert.Equal("sneaky-key", KeyStore.Get(Provider.Anthropic));
            Assert.False(KeyStore.Scrub(variables.variable, Provider.Anthropic));
        }

        /// <summary>
        /// A variable holding only spaces is blanked, and never removes the saved key.
        /// </summary>
        [Fact]
        public void Scrub_SpacesAlone_KeepTheKey()
        {
            KeyStore.Set(Provider.Anthropic, "sk-ant-kept");
            variables.variable.Add(new variables._variable { name = ConnectionSettings.ApiKeyVar, value = "  " });
            Assert.True(KeyStore.Scrub(variables.variable, Provider.Anthropic));
            Assert.Equal("", variables.Get(ConnectionSettings.ApiKeyVar));
            Assert.Equal("sk-ant-kept", KeyStore.Get(Provider.Anthropic));
        }

        /// <summary>
        /// ModMenus keeps a secret in ModConfig\&lt;secretID&gt;.key: each provider's secretID names its key file.
        /// </summary>
        [Fact]
        public void SecretIDs_NameEachProvidersKeyFile()
        {
            Assert.Equal("Tel AI Mods/anthropic", KeyStore.SecretID(Provider.Anthropic));
            Assert.Equal("Tel AI Mods/openai", KeyStore.SecretID(Provider.OpenAI));
            Assert.Equal("Tel AI Mods/compatible", KeyStore.SecretID(Provider.Compatible));
            Assert.True(KeyStore.IsKeySecret("Tel AI Mods/openai"));
            Assert.False(KeyStore.IsKeySecret("Tel AI Mods/other"));
            Assert.False(KeyStore.IsKeySecret("tel ai mods/openai"));
        }

        [Fact]
        public void KeyFolder_IsSharedByTelsAIMods()
        {
            Assert.Equal(Path.Combine("data", "ModConfig", "Tel AI Mods"), KeyStore.FolderUnder("data"));
            Assert.Equal(Path.Combine("data", "ModConfig", "Old Test AI Mod"), KeyStore.LegacyFolderUnder("data"));
        }

        private static void SaveOldKey(Provider provider, string key)
        {
            Directory.CreateDirectory(TestEnv.LegacyFolder);
            File.WriteAllText(Path.Combine(TestEnv.LegacyFolder, provider.ToString().ToLowerInvariant() + ".key"), key);
        }

        [Fact]
        public void OldKey_MovesToTheSharedFolder()
        {
            SaveOldKey(Provider.OpenAI, " sk-old-key \n");
            Assert.Equal("sk-old-key", KeyStore.Get(Provider.OpenAI));
            Assert.Equal("sk-old-key", File.ReadAllText(Path.Combine(TestEnv.KeyFolder, "openai.key")));
            // Not kept in two places; the emptied old folder goes too
            Assert.False(Directory.Exists(TestEnv.LegacyFolder));
            Assert.Empty(TestEnv.Logs);
        }

        [Fact]
        public void OldFolder_StaysWhileItHoldsOtherKeys()
        {
            SaveOldKey(Provider.OpenAI, "sk-old-openai");
            SaveOldKey(Provider.Anthropic, "sk-old-anthropic");
            Assert.Equal("sk-old-openai", KeyStore.Get(Provider.OpenAI));
            Assert.True(File.Exists(Path.Combine(TestEnv.LegacyFolder, "anthropic.key")));
            Assert.Equal("sk-old-anthropic", KeyStore.Get(Provider.Anthropic));
            Assert.False(Directory.Exists(TestEnv.LegacyFolder));
        }

        [Fact]
        public void MoveLegacyKeys_MovesEveryKeyWithoutReadingThem()
        {
            SaveOldKey(Provider.OpenAI, "sk-old-openai");
            SaveOldKey(Provider.Compatible, "local-key");
            KeyStore.MoveLegacyKeys();
            Assert.Equal("sk-old-openai", File.ReadAllText(Path.Combine(TestEnv.KeyFolder, "openai.key")));
            Assert.Equal("local-key", File.ReadAllText(Path.Combine(TestEnv.KeyFolder, "compatible.key")));
            Assert.False(Directory.Exists(TestEnv.LegacyFolder));
            Assert.Equal("local-key", KeyStore.Get(Provider.Compatible));
        }

        [Fact]
        public void SharedKey_WinsOverAnOldOne()
        {
            KeyStore.Set(Provider.Anthropic, "sk-shared");
            KeyStore.ClearCache();
            SaveOldKey(Provider.Anthropic, "sk-old");
            Assert.Equal("sk-shared", KeyStore.Get(Provider.Anthropic));
            Assert.True(File.Exists(Path.Combine(TestEnv.LegacyFolder, "anthropic.key")));
        }

        /// <summary>
        /// A removed key must not come back from the old folder.
        /// </summary>
        [Fact]
        public void Delete_AlsoRemovesTheOldKey()
        {
            KeyStore.Set(Provider.Anthropic, "sk-shared");
            KeyStore.ClearCache();
            SaveOldKey(Provider.Anthropic, "sk-old");
            KeyStore.Delete(Provider.Anthropic);
            KeyStore.ClearCache();
            Assert.Null(KeyStore.Get(Provider.Anthropic));
            Assert.False(File.Exists(Path.Combine(TestEnv.LegacyFolder, "anthropic.key")));
        }

        [Fact]
        public void UnreadableOldKey_IsLoggedOnceAndDoesNotThrow()
        {
            SaveOldKey(Provider.Anthropic, "sk-old");
            using (new FileStream(Path.Combine(TestEnv.LegacyFolder, "anthropic.key"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Null(KeyStore.Get(Provider.Anthropic));
                Assert.Null(KeyStore.Get(Provider.Anthropic));
            }
            Assert.Single(TestEnv.Logs);
            Assert.Contains("shared key folder", TestEnv.Logs[0]);
        }

        [Fact]
        public void NoOldFolder_WhileTheFolderIsOverriddenAlone()
        {
            KeyStore.LegacyFolderOverride = null;
            Assert.Null(KeyStore.Get(Provider.OpenAI));
            Assert.Empty(TestEnv.Logs);
        }

        /// <summary>
        /// Every AI mod has its own copy of the key store, so a key another mod saves must be read again.
        /// </summary>
        [Fact]
        public void KeySavedByAnotherMod_IsReadAgain()
        {
            KeyStore.Set(Provider.OpenAI, "sk-first");
            Assert.Equal("sk-first", KeyStore.Get(Provider.OpenAI));
            string file = Path.Combine(TestEnv.KeyFolder, "openai.key");
            File.WriteAllText(file, "sk-second");
            File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));
            Assert.Equal("sk-second", KeyStore.Get(Provider.OpenAI));

            File.Delete(file);
            Assert.Null(KeyStore.Get(Provider.OpenAI));
        }
    }
}
