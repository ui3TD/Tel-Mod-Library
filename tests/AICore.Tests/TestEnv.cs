using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

// Every test shares the shared code's static state (settings, key store)
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AICore.Tests
{
    /// <summary>
    /// A clean state for each test: settings from a dictionary, keys in a fresh temp folder with an old
    /// key folder next to it, and log lines collected instead of sent to Unity.
    /// </summary>
    public static class TestEnv
    {
        public static readonly Dictionary<string, string> Vars = new();
        public static readonly List<string> Logs = new();
        public static string KeyFolder;
        public static string LegacyFolder;

        public static void Reset()
        {
            Vars.Clear();
            Logs.Clear();
            ConnectionSettings.Read = name => Vars.TryGetValue(name, out string value) ? value : null;
            Log.Sink = Logs.Add;
            string root = Path.Combine(Path.GetTempPath(), "AICoreTests", Guid.NewGuid().ToString("N"));
            KeyFolder = Path.Combine(root, "shared");
            LegacyFolder = Path.Combine(root, "old");
            KeyStore.FolderOverride = KeyFolder;
            KeyStore.LegacyFolderOverride = LegacyFolder;
            KeyStore.ClearCache();
            variables.variable.Clear();
        }

        /// <summary>
        /// Selects a service and saves its key.
        /// </summary>
        public static void Configure(Provider provider = Provider.Anthropic, string key = "sk-test-1234", string model = "", string baseUrl = "")
        {
            Vars[ConnectionSettings.ProviderVar] = ((int)provider).ToString();
            Vars[ConnectionSettings.ModelVar] = model;
            Vars[ConnectionSettings.BaseUrlVar] = baseUrl;
            if (key != null)
            {
                KeyStore.Set(provider, key);
            }
        }
    }

    public sealed class FakeTransport : IHttpTransport
    {
        public readonly List<HttpRequestSpec> Sent = new();
        private readonly Queue<Action<HttpResult>> pending = new();

        public void Send(HttpRequestSpec request, Action<HttpResult> done)
        {
            Sent.Add(request);
            pending.Enqueue(done);
        }

        public int Pending => pending.Count;

        public void Reply(long status, string body) => pending.Dequeue()(new HttpResult { Status = status, Body = body });

        public void NoConnection(string error = "Cannot resolve destination host") => pending.Dequeue()(new HttpResult { Status = 0, NetworkError = error });
    }

    public sealed class FakeHost : IAIHost
    {
        public double RealTime { get; set; } = 1000;
        public DateTime GameTime { get; set; } = new DateTime(2020, 6, 3, 10, 0, 0);
        public object Session { get; set; } = new object();
        public bool Enabled { get; set; } = true;
        public int PerWeek { get; set; } = 7;

        public readonly List<(string Text, bool Error)> Notices = new();
        public readonly List<Action> Frames = new();

        public Connection ReadConnection() => ConnectionSettings.Current();
        public void Notify(string text, bool error) => Notices.Add((text, error));
        public void NextFrame(Action action) => Frames.Add(action);

        public void RunFrames()
        {
            List<Action> frames = new(Frames);
            Frames.Clear();
            frames.ForEach(f => f());
        }
    }

    public static class Replies
    {
        public static string Anthropic(string text, string stop = "end_turn") =>
            "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":" + Json.Quote(text) + "}],\"stop_reason\":\"" + stop + "\"}";

        public static string OpenAI(string text, string finish = "stop") =>
            "{\"id\":\"c1\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":" + Json.Quote(text) + "},\"finish_reason\":\"" + finish + "\"}]}";
    }
}
