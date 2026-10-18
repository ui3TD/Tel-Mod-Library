using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AIFanChatter.LiveReport
{
    /// <summary>
    /// Sends four moments from a made-up agency (Agency.cs) to a real AI service through the mod's own
    /// prompt, dispatcher, request and reply code, once per model, and writes every request, reply and
    /// queued post to a JSON file for tools/ai_fan_chatter_live_report.py. Runs outside the game: the
    /// game's text comes from its install folder and the key from the mod's key file. It costs money.
    ///
    ///     AIFanChatterLiveReport --game-dir DIR --provider 2 --base-url URL --model M [--model M2] --out FILE
    ///     AIFanChatterLiveReport --game-dir DIR --dry-run 1      (prints the prompts; sends nothing)
    /// Options: --moment single|concert|event|graduation (repeat to pick several; all four by default),
    /// --setlist "Signal Fire/Rin; talk: Mao, Kaede" for the concert (see Agency.SetList), and for the
    /// random event --event ID (from the game's random_events.json), --event-reply N (the player's
    /// answer, from 0; by default the first one without posts of its own) and --event-idols "Rena, Mao"
    /// (who plays the event's idols, by first name; Rin by default).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Dictionary<string, List<string>> options = Options(args);
            string Option(string name) => options.TryGetValue(name, out List<string> values) ? values.Last() : null;
            string gameDir = Option("game-dir");
            string outFile = Option("out");
            List<string> models = options.TryGetValue("model", out List<string> m) ? m : new List<string>();
            bool dryRun = Option("dry-run") == "1";
            setList = Option("setlist");
            picked = options.TryGetValue("moment", out List<string> p) ? p : null;
            eventId = Option("event") ?? eventId;
            eventReply = Option("event-reply") is string r ? int.Parse(r, CultureInfo.InvariantCulture) : null;
            eventIdols = Option("event-idols") ?? eventIdols;
            if (gameDir == null || !dryRun && (outFile == null || models.Count == 0))
            {
                Console.Error.WriteLine("Usage: AIFanChatterLiveReport --game-dir DIR --provider N --base-url URL --model M [--model M2] --out FILE");
                return 2;
            }

            Log.Sink = message => Console.Error.WriteLine(message);
            KeyStore.FolderOverride = KeyStore.FolderUnder(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Glitch Pitch", "Idol Manager"));
            GameText.Load(gameDir, ModFolder());
            OutsideUnity.Patch();

            Dictionary<string, string> settings = new()
            {
                [Settings.EnabledVar] = "1",
                [Settings.ProviderVar] = Option("provider") ?? "2",
                [Settings.BaseUrlVar] = Option("base-url") ?? "",
                [Settings.PerWeekVar] = Settings.MaxPerWeek.ToString(CultureInfo.InvariantCulture),
            };
            Settings.Read = name => settings.TryGetValue(name, out string value) ? value : null;
            if (dryRun)
            {
                PrintPrompts();
                return 0;
            }

            JSONClass output = new();
            output["gameDate"] = Agency.Today.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            output["language"] = "en";
            JSONArray runs = new();
            output["runs"] = runs;

            foreach (string model in models)
            {
                settings[Settings.ModelVar] = model;
                JSONClass run = new();
                run["model"] = model;
                JSONArray results = new();
                run["moments"] = results;
                runs.Add(run);

                // Each model starts from the same agency, with a fresh dispatcher and its limits
                List<KeyValuePair<string, Func<Moment>>> moments = Moments();
                ReportHost host = new();
                RecordingTransport transport = new();
                Dispatcher dispatcher = new(host, transport, new Random());

                foreach (var moment in moments)
                {
                    JSONClass result = new();
                    result["name"] = moment.Key;
                    results.Add(result);
                    // The mod's 30 seconds between requests pass at once
                    host.Clock += Dispatcher.MinGapSeconds + 1;
                    Console.Error.WriteLine(model + ": " + moment.Key);

                    Moment built;
                    try
                    {
                        built = moment.Value();
                    }
                    catch (Exception e)
                    {
                        result["error"] = "Couldn't build the moment: " + e.Message;
                        continue;
                    }
                    int batchesBefore = FeedQueue.Items.Count == 0 ? 0 : FeedQueue.Items.Max(i => i.Batch);
                    JSONArray calls = new();
                    result["calls"] = calls;
                    transport.Calls = calls;
                    // The transport answers before Request returns, so the reply is queued by then
                    if (!dispatcher.Request(built))
                    {
                        result["error"] = "Not sent: " + dispatcher.Blocked();
                        continue;
                    }
                    transport.Calls = null;

                    JSONArray queued = new();
                    foreach (QueuedItem item in FeedQueue.Items.Where(i => i.Batch > batchesBefore))
                    {
                        JSONClass entry = new();
                        entry["user"] = item.User;
                        entry["text"] = item.Text;
                        entry["isReply"] = item.IsReply ? "true" : "false";
                        entry["parentUser"] = item.ParentUser;
                        entry["parentText"] = item.ParentText;
                        entry["due"] = item.Due.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                        queued.Add(entry);
                    }
                    result["queued"] = queued;
                    Console.Error.WriteLine("  " + calls.Count + " calls, " + queued.Count + " queued");
                }
            }

            File.WriteAllText(outFile, output.ToString());
            Console.Error.WriteLine("Wrote " + outFile);
            return 0;
        }

        /// <summary>
        /// The prompts the first model would get, each moment's reply left out of the next one's feed.
        /// </summary>
        private static void PrintPrompts()
        {
            List<KeyValuePair<string, Func<Moment>>> moments = Moments();
            ReportHost host = new();
            Console.WriteLine(Prompt.System(host.LanguageName, host.FanHandles, host.FanOpinions));
            foreach (var moment in moments)
            {
                Console.WriteLine("\n======== " + moment.Key + "\n" + Prompt.User(moment.Value(), host.RecentPosts));
            }
        }

        private static string setList;
        private static List<string> picked;
        private static string eventId = "dating_caught_aftermath";
        private static int? eventReply;
        private static string eventIdols = "Rin";

        /// <summary>
        /// Builds the agency afresh, with an empty queue, and gives the moments picked, in order.
        /// </summary>
        private static List<KeyValuePair<string, Func<Moment>>> Moments()
        {
            Agency agency = Agency.Build(setList);
            FeedQueue.Clear();
            var all = new List<KeyValuePair<string, Func<Moment>>>
            {
                new("single", () => MomentContext.Single(agency.NewSingle)),
                new("concert", () => MomentContext.Concert(agency.NewConcert)),
                new("event", () => EventMoment(agency)),
                // Last, as the graduate leaves the group
                new("graduation", () => MomentContext.Graduation(agency.Graduate, agency.GraduateNow())),
            };
            string unknown = picked?.FirstOrDefault(name => all.All(m => m.Key != name));
            if (unknown != null)
            {
                throw new ArgumentException("No moment " + unknown + "; the moments are single, concert, event and graduation");
            }
            return all.Where(m => picked == null || picked.Contains(m.Key))
                .Select(m => new KeyValuePair<string, Func<Moment>>(Names[m.Key], m.Value)).ToList();
        }

        private static readonly Dictionary<string, string> Names = new()
        {
            ["single"] = "a single release",
            ["concert"] = "a concert",
            ["event"] = "a random event",
            ["graduation"] = "a graduation",
        };

        /// <summary>
        /// The game's random event as the player answered it, with the agency's idols in its parts. Only an
        /// answer without posts of its own reaches the mod: the game posts those itself.
        /// </summary>
        private static Moment EventMoment(Agency agency)
        {
            if (!GameText.Events.TryGetValue(eventId, out Event_Manager._randomEvent data))
            {
                throw new ArgumentException("No random event " + eventId + " in the game's random_events.json");
            }
            int reply = eventReply ?? data.reply.FindIndex(r => r.SNS.Count == 0);
            if (reply < 0 || reply >= data.reply.Count)
            {
                throw new ArgumentException("Event " + eventId + " has no answer " + reply + "; it has " + data.reply.Count);
            }
            if (data.reply[reply].SNS.Count > 0)
            {
                throw new ArgumentException("Answer " + reply + " of " + eventId + " posts on the feed itself, so the mod sends nothing for it");
            }
            Event_Manager._activeEvent active = agency.Event(data, eventIdols.Split(',').Select(n => n.Trim()).Where(n => n != "").ToList());
            return MomentContext.Event(active, data.reply[reply], new List<Event_Manager._activeEvent> { active });
        }

        /// <summary>
        /// "--name value" pairs; a name given more than once keeps every value.
        /// </summary>
        private static Dictionary<string, List<string>> Options(string[] args)
        {
            Dictionary<string, List<string>> options = new();
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string name = args[i].TrimStart('-');
                if (!options.TryGetValue(name, out List<string> values))
                {
                    options[name] = values = new List<string>();
                }
                values.Add(args[i + 1]);
            }
            return options;
        }

        private static string ModFolder([CallerFilePath] string source = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), "..", "..", "mods", "AI Fan Chatter"));
    }

    /// <summary>
    /// The game's English text as Language._Load and traits.Load read it from the install folder, with the
    /// mod's constants.json on top, as the game loads a mod's.
    /// </summary>
    internal static class GameText
    {
        /// <summary>The game's ways to promote a single, as singles.Params holds them.</summary>
        public static readonly List<singles._param> Marketing = new();

        /// <summary>The game's random events by id.</summary>
        public static readonly Dictionary<string, Event_Manager._randomEvent> Events = new();

        public static void Load(string gameDir, string modFolder)
        {
            string english = Path.Combine(gameDir, "IM_Data", "StreamingAssets", "Languages", "en", "JSON");
            Language.Data.Clear();
            foreach (string file in new[] { Path.Combine(english, "Constants", "constants.json"), Path.Combine(modFolder, "assets", "JSON", "Constants", "constants.json") })
            {
                foreach (JSONNode item in Read(file).AsArray)
                {
                    Language.Data[item["id"]] = item["text"];
                }
            }
            Language.Variables.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Constants", "vars.json")).AsArray)
            {
                Language.Variables.Add(new Language._variable { Variable = item["variable"], Male = item["male"], Female = item["female"], Color = item["color"] ?? "" });
            }
            Marketing.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Singles", "marketing.json")).AsArray)
            {
                Marketing.Add(new singles._param
                {
                    type = singles._param._type.marketing,
                    id = item["id"].AsInt,
                    title = item["title"],
                    Group_Handshake = item["group_handshake"].AsBool,
                    Individual_Handshake = item["individual_handshake"].AsBool,
                });
            }
            Events.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Events", "random_events.json")).AsArray)
            {
                Event_Manager._randomEvent data = new();
                data.Set(item);
                Events[data.id] = data;
            }
            traits.Traits.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Idols", "traits.json")).AsArray)
            {
                traits._trait trait = new();
                trait.Set(item);
                traits.Traits.Add(trait);
            }
        }

        private static JSONNode Read(string file)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("The game's text isn't there; check --game-dir: " + file);
            }
            return JSON.Parse(File.ReadAllText(file));
        }
    }

    /// <summary>
    /// Game methods that call into the engine's native code, which only runs inside the game, replaced with
    /// managed equivalents.
    /// </summary>
    internal static class OutsideUnity
    {
        private static readonly Random Random = new();

        public static void Patch()
        {
            Harmony harmony = new("com.tel.aifanchatter.livereport");
            // A random event's text names a random idol for [random_idol], with UnityEngine.Random
            harmony.Patch(AccessTools.Method(typeof(data_girls), nameof(data_girls.GetRandomActiveGirl), Type.EmptyTypes),
                prefix: new HarmonyMethod(typeof(OutsideUnity), nameof(RandomActiveGirl)));
        }

        private static bool RandomActiveGirl(ref data_girls.girls __result)
        {
            List<data_girls.girls> active = data_girls.GetActiveGirls();
            __result = active.Count == 0 ? null : active[Random.Next(active.Count)];
            return false;
        }
    }

    /// <summary>
    /// The game as the dispatcher sees it, from the game's lists that Agency fills, with a clock the report
    /// moves on itself.
    /// </summary>
    internal sealed class ReportHost : IGameHost
    {
        private readonly GameHost game = new();

        public double Clock;

        public double RealTime => Clock;
        public DateTime GameTime => staticVars.dateTime;
        public object Session => FeedRelease.Session;
        public Connection ReadConnection() => Settings.Current();
        public bool Enabled => Settings.Enabled;
        public int PerWeek => Settings.PerWeek;
        public string LanguageName => "English";
        public IDictionary<string, string> FanHandles => game.FanHandles;
        public IDictionary<string, int> FanOpinions => game.FanOpinions;
        public IList<SNS_Manager._message> RecentPosts => game.RecentPosts;
        public void Notify(string text, bool error) => Console.Error.WriteLine((error ? "Notice (error): " : "Notice: ") + text);
        public void NextFrame(Action action) => action();
        public void Queue(List<SNS_Manager._message> posts) => game.Queue(posts);
    }

    /// <summary>
    /// The mod's own HTTP exchange, run on this thread, recording each request and its reply.
    /// </summary>
    internal sealed class RecordingTransport : IHttpTransport
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();

        public JSONArray Calls;

        public void Send(HttpRequestSpec request, Action<HttpResult> done)
        {
            JSONClass call = new();
            call["sentAt"] = Seconds();
            call["url"] = request.Url;
            call["request"] = request.Body;
            HttpResult result = NetTransport.Exchange(request);
            call["repliedAt"] = Seconds();
            call["status"] = result.Status.ToString(CultureInfo.InvariantCulture);
            call["response"] = result.Body ?? "";
            call["networkError"] = result.NetworkError ?? "";
            // Requests outside a moment aren't recorded
            Calls?.Add(call);
            done(result);
        }

        private string Seconds() => watch.Elapsed.TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
    }
}
