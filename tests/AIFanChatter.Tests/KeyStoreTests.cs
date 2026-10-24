using AICore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The keys are ModMenus' secret rows, one per service, which write the key files KeyStore reads and never
    /// go through the variables. ModMenus tells the mod which secrets an Apply changed; a changed key is a
    /// settings change.
    /// </summary>
    public class KeyStoreTests : IDisposable
    {
        public KeyStoreTests()
        {
            TestEnv.Reset();
            ModMenusSecrets.Reset();
        }

        public void Dispose()
        {
            FakeModMenus.Clear();
            ModMenusSecrets.Reset();
        }

        /// <summary>
        /// ModMenus keeps a secret in ModConfig\&lt;secretID&gt;.key; each service's row names that service's key file.
        /// </summary>
        [Fact]
        public void SecretIDs_NameTheKeyFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "LocalLow", "Idol Manager");
            foreach (Provider provider in (Provider[])Enum.GetValues(typeof(Provider)))
            {
                string[] parts = KeyStore.SecretID(provider).Split('/');
                string modMenusFile = Path.Combine(Path.Combine(Path.Combine(root, "ModConfig"), parts[0]), parts[1] + ".key");
                string keyStoreFile = Path.Combine(KeyStore.FolderUnder(root), provider.ToString().ToLowerInvariant() + ".key");
                Assert.Equal(keyStoreFile, modMenusFile);
                Assert.True(KeyStore.IsKeySecret(KeyStore.SecretID(provider)));
            }
            Assert.Equal("Tel AI Mods/anthropic", KeyStore.SecretID(Provider.Anthropic));
            Assert.False(KeyStore.IsKeySecret("Other Mod/anthropic"));
            Assert.False(KeyStore.IsKeySecret(null));
        }

        [Fact]
        public void ChangedKey_StartsAConnectionTest()
        {
            FakeHost host = new();
            Dispatcher.Instance = new Dispatcher(host, new FakeTransport(), new Random(1));

            ModMenusSecrets.OnSecretsChanged(new[] { "Other Mod/key" });
            Assert.Empty(host.Frames);

            ModMenusSecrets.OnSecretsChanged(new[] { "Other Mod/key", KeyStore.SecretID(Provider.OpenAI) });
            Assert.Single(host.Frames);
        }

        /// <summary>
        /// A key changed in ModMenus lifts a stop, as any other setting change does.
        /// </summary>
        [Fact]
        public void ChangedKey_LiftsAStop()
        {
            TestEnv.Configure();
            FakeTransport transport = new();
            Dispatcher.Instance = new Dispatcher(new FakeHost(), transport, new Random(1));
            Assert.True(Dispatcher.Instance.Request(new Moment("a test").Add("Title", () => "Song")));
            transport.Reply(401, "{\"error\":{\"message\":\"invalid x-api-key\"}}");
            Assert.True(Dispatcher.Instance.Suspended);

            ModMenusSecrets.OnSecretsChanged(new[] { KeyStore.SecretID(Provider.Anthropic) });

            Assert.False(Dispatcher.Instance.Suspended);
        }

        /// <summary>
        /// The mod listens to ModMenus' event once it's found, once only; an event of another shape is left alone.
        /// </summary>
        [Fact]
        public void Listening_TakesOnlyTheEventItKnows()
        {
            FakeHost host = new();
            Dispatcher.Instance = new Dispatcher(host, new FakeTransport(), new Random(1));
            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            Assert.False(ModMenusSecrets.TryListen(null));
            Assert.False(ModMenusSecrets.TryListen(typeof(FakeModMenus).GetEvent(nameof(FakeModMenus.OtherShape), flags)));
            Assert.True(ModMenusSecrets.TryListen(typeof(FakeModMenus).GetEvent(nameof(FakeModMenus.SecretsChanged), flags)));

            FakeModMenus.Raise(new[] { KeyStore.SecretID(Provider.Compatible) });
            Assert.Single(host.Frames);
        }

        /// <summary>
        /// A save from before has the key's variable empty, and the key in its file: it's used as it is, with
        /// nothing stopped and no connection test.
        /// </summary>
        [Fact]
        public void UpgradedSave_KeepsUsingTheSavedKey()
        {
            TestEnv.Configure(Provider.OpenAI, key: "sk-saved-before");
            KeyStore.ClearCache();
            variables.variable.Add(new variables._variable { name = Settings.ApiKeyVar, value = "" });
            FakeHost host = new();
            Dispatcher.Instance = new Dispatcher(host, new FakeTransport(), new Random(1));

            variables_SaveFunction.Postfix();

            Assert.Equal("sk-saved-before", Settings.Current().ApiKey);
            Assert.False(Dispatcher.Instance.Suspended);
            Assert.Empty(host.Frames);
        }

        [Fact]
        public void OtherVariables_AreUntouched()
        {
            variables_Set.Prefix("SomeOtherMod_Name", "female_name");
            Assert.False(Directory.Exists(TestEnv.KeyFolder));
        }

        /// <summary>
        /// Apply saves every row, changed or not. Pressing it with nothing changed lifts a stop and tests
        /// the connection, so the player can try again after fixing the cause; with nothing stopped it does
        /// nothing, so it costs nothing. The service row tells it was pressed: only Apply saves it.
        /// </summary>
        [Fact]
        public void UnchangedApply_LiftsAStop_AndOnlyThen()
        {
            TestEnv.Configure();
            FakeHost host = new();
            FakeTransport transport = new();
            Dispatcher.Instance = new Dispatcher(host, transport, new Random(1));
            string provider = ((int)Provider.Anthropic).ToString();
            variables.variable.Add(new variables._variable { name = Settings.ProviderVar, value = provider });

            variables_Set.Prefix(Settings.ProviderVar, provider);
            Assert.Empty(host.Frames);

            Assert.True(Dispatcher.Instance.Request(new Moment("a test").Add("Title", () => "Song")));
            transport.Reply(200, Replies.Anthropic("no posts here"));
            Assert.True(Dispatcher.Instance.Suspended);

            // Other rows saved unchanged don't count as pressing Apply
            variables.variable.Add(new variables._variable { name = Settings.ModelVar, value = "" });
            variables_Set.Prefix(Settings.ModelVar, "");
            Assert.True(Dispatcher.Instance.Suspended);

            variables_Set.Prefix(Settings.ProviderVar, provider);
            Assert.False(Dispatcher.Instance.Suspended);
            Assert.Single(host.Frames);
        }

        [Fact]
        public void SettingChange_StartsAConnectionTestOnTheNextFrame()
        {
            FakeHost host = new();
            Dispatcher.Instance = new Dispatcher(host, new FakeTransport(), new Random(1));
            variables_Set.Prefix(Settings.ModelVar, "claude-sonnet-5-5");
            Assert.Single(host.Frames);

            // Setting the same value again (every Apply saves every row) changes nothing
            Dispatcher.Instance = new Dispatcher(host = new FakeHost(), new FakeTransport(), new Random(1));
            variables.variable.Add(new variables._variable { name = Settings.ModelVar, value = "same" });
            variables_Set.Prefix(Settings.ModelVar, "same");
            Assert.Empty(host.Frames);
        }

        /// <summary>
        /// Stands in for ModMenus' SecretsChanged event, and one of another shape.
        /// </summary>
        public static class FakeModMenus
        {
            public static event Action<string[]> SecretsChanged;
            public static event Action<string> OtherShape;

            public static void Raise(string[] ids) => SecretsChanged?.Invoke(ids);

            public static void Clear()
            {
                SecretsChanged = null;
                OtherShape = null;
            }
        }
    }
}
