using Xunit;

namespace AICore.Tests
{
    public class ConnectionTests
    {
        public ConnectionTests() => TestEnv.Reset();

        [Fact]
        public void SettingNames_StartWithTheModsPrefix()
        {
            Assert.Equal("TestAIMod_Provider", ConnectionSettings.ProviderVar);
            Assert.Equal("TestAIMod_BaseURL", ConnectionSettings.BaseUrlVar);
            Assert.Equal("TestAIMod_Model", ConnectionSettings.ModelVar);
            Assert.Equal("TestAIMod_APIKey", ConnectionSettings.ApiKeyVar);
        }

        [Fact]
        public void BlankModel_IsTheServicesDefault_OnOpenAIAndAnthropic()
        {
            Assert.Equal(Providers.DefaultAnthropicModel, new Connection { Provider = Provider.Anthropic }.ResolvedModel);
            Assert.Equal("gpt-6-luna", new Connection { Provider = Provider.OpenAI }.ResolvedModel);
            Assert.Equal("", new Connection { Provider = Provider.Compatible }.ResolvedModel);
            Assert.Equal("mine", new Connection { Provider = Provider.Anthropic, Model = "mine" }.ResolvedModel);
        }

        /// <summary>
        /// Messages name the service as the player knows it, never the internal name "Compatible".
        /// </summary>
        [Theory]
        [InlineData(Provider.Anthropic, "", "Anthropic")]
        [InlineData(Provider.OpenAI, "", "OpenAI")]
        [InlineData(Provider.Compatible, "https://openrouter.ai/api/v1", "openrouter.ai")]
        [InlineData(Provider.Compatible, " http://localhost:11434 ", "localhost:11434")]
        [InlineData(Provider.Compatible, "openrouter.ai/api/v1", "openrouter.ai")]
        [InlineData(Provider.Compatible, "localhost:11434", "localhost:11434")]
        [InlineData(Provider.Compatible, "", "your OpenAI-compatible service")]
        [InlineData(Provider.Compatible, "not a url", "your OpenAI-compatible service")]
        public void ServiceName_IsWhatThePlayerKnows(Provider provider, string baseUrl, string expected)
        {
            Assert.Equal(expected, new Connection { Provider = provider, BaseUrl = baseUrl }.ServiceName);
        }

        [Fact]
        public void Describe_NamesServiceAndModel()
        {
            Connection connection = new() { Provider = Provider.Compatible, BaseUrl = "https://openrouter.ai/api/v1", Model = "anthropic/claude-haiku-5.5" };
            Assert.Equal("openrouter.ai with anthropic/claude-haiku-5.5", connection.Describe());
            Assert.Equal("Anthropic with claude-haiku-5-5", new Connection { Provider = Provider.Anthropic }.Describe());
            Assert.Equal("OpenAI with gpt-6-luna", new Connection { Provider = Provider.OpenAI }.Describe());
        }

        [Fact]
        public void MissingModel_NamesTheServiceByAddress()
        {
            Providers.BuildRequest(new Connection { Provider = Provider.Compatible, BaseUrl = "http://localhost:1234" }, "s", "u", 1, out string error);
            Assert.Equal("Set a model name for localhost:1234.", error);
            Assert.DoesNotContain("Compatible", error);
        }

        [Fact]
        public void Current_ReadsTheSelectedProvidersKey()
        {
            KeyStore.Set(Provider.Anthropic, "anthropic-key");
            KeyStore.Set(Provider.OpenAI, "openai-key");
            TestEnv.Vars[ConnectionSettings.ProviderVar] = "1";
            Assert.Equal("openai-key", ConnectionSettings.Current().ApiKey);
        }
    }
}
