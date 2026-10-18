using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

// Every test shares the mod's static state (settings, key store, queue, dispatcher)
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AIFanChatter.Tests
{
    /// <summary>
    /// A clean mod for each test: settings from a dictionary, keys in a fresh temp folder, an empty
    /// queue, and log lines collected instead of sent to Unity.
    /// </summary>
    public static class TestEnv
    {
        public static readonly Dictionary<string, string> Vars = new();
        public static readonly List<string> Logs = new();
        public static string KeyFolder;

        public static void Reset()
        {
            Vars.Clear();
            Logs.Clear();
            Settings.Read = name => Vars.TryGetValue(name, out string value) ? value : null;
            Log.Sink = Logs.Add;
            KeyFolder = Path.Combine(Path.GetTempPath(), "AIFanChatterTests", Guid.NewGuid().ToString("N"));
            KeyStore.FolderOverride = KeyFolder;
            KeyStore.ClearCache();
            FeedQueue.Clear();
            Moments.ResetGraduations();
            FeedRelease.Quiet = false;
            variables.variable.Clear();
            TestGame.Clear();
            LoadConstants();
        }

        /// <summary>
        /// The mod's constants.json in Language.Data, read the way the game reads it (Language._Load, whose
        /// ProcessInboundData is JSON.Parse).
        /// </summary>
        public static void LoadConstants([CallerFilePath] string testFile = "")
        {
            string file = Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "AI Fan Chatter", "assets", "JSON", "Constants", "constants.json");
            Language.Data.Clear();
            foreach (SimpleJSON.JSONNode item in SimpleJSON.JSON.Parse(File.ReadAllText(file)).AsArray)
            {
                Language.Data[item["id"]] = item["text"];
            }
        }

        /// <summary>
        /// Turns the mod on with Anthropic and a saved key.
        /// </summary>
        public static void Configure(Provider provider = Provider.Anthropic, string key = "sk-test-1234", string model = "", string baseUrl = "")
        {
            Vars[Settings.EnabledVar] = "1";
            Vars[Settings.ProviderVar] = ((int)provider).ToString();
            Vars[Settings.ModelVar] = model;
            Vars[Settings.BaseUrlVar] = baseUrl;
            if (key != null)
            {
                KeyStore.Set(provider, key);
            }
        }

        public static SNS_Manager._message Post(string user, string text, params SNS_Manager._message[] replies)
        {
            SNS_Manager._message post = new() { User = user, Message = text };
            post.Replies.AddRange(replies);
            return post;
        }

        public static List<SNS_Manager._message> Feed(int count, string prefix = "post")
        {
            List<SNS_Manager._message> feed = new();
            for (int i = 0; i < count; i++)
            {
                feed.Add(Post("F_C_T", prefix + " " + i));
            }
            return feed;
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

    public sealed class FakeHost : IGameHost
    {
        public double RealTime { get; set; } = 1000;
        public DateTime GameTime { get; set; } = new DateTime(2020, 6, 3, 10, 0, 0);
        public object Session { get; set; } = new object();
        public bool Enabled => Settings.Enabled;
        public int PerWeek => Settings.PerWeek;
        public string LanguageName { get; set; } = "English";
        public IDictionary<string, string> FanHandles { get; } = new Dictionary<string, string>();
        public IDictionary<string, int> FanOpinions { get; } = new Dictionary<string, int>();
        public List<SNS_Manager._message> Posts { get; } = new();
        /// <summary>Replaces reading Posts, for a feed that can't be read.</summary>
        public Func<IList<SNS_Manager._message>> ReadPosts;
        public IList<SNS_Manager._message> RecentPosts => ReadPosts != null ? ReadPosts() : Posts;

        public readonly List<(string Text, bool Error)> Notices = new();
        public readonly List<Action> Frames = new();
        public readonly List<List<SNS_Manager._message>> Queued = new();
        public readonly List<DateTime> QueuedFor = new();

        public Connection ReadConnection() => Settings.Current();
        public void Notify(string text, bool error) => Notices.Add((text, error));
        public void NextFrame(Action action) => Frames.Add(action);
        public void Queue(List<SNS_Manager._message> posts, DateTime moment)
        {
            Queued.Add(posts);
            QueuedFor.Add(moment);
        }

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

        public const string Posts = "{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"Best single yet!\",\"replies\":[{\"user\":\"M_C_A\",\"text\":\"Agreed\"}]},{\"user\":\"M_C_YA\",\"text\":\"The center was great\"},{\"user\":\"F_C_A\",\"text\":\"Bought two copies\"}]}";
    }
}
