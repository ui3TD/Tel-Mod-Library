using AICore;
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
    /// Sends fourteen moments from a made-up agency (Agency.cs) to a real AI service through the mod's own
    /// prompt, dispatcher, request and reply code, once per model, and writes every request, reply and
    /// queued post to a JSON file for tools/ai_fan_chatter_live_report.py. Runs outside the game: the
    /// game's text comes from its install folder and the key from the mod's key file. It costs money.
    ///
    ///     AIFanChatterLiveReport --game-dir DIR --provider 2 --base-url URL --model M [--model M2] --out FILE
    ///     AIFanChatterLiveReport --game-dir DIR --dry-run 1      (prints the prompts; sends nothing)
    /// Options: --moment single|concert|event|scandal|policy|firing|tour|election|concert-election|awards|show|contract|audition|graduation (repeat to pick several; all fourteen by default),
    /// --single voting|election makes the single an upcoming election's voting single or the single made from Election #1's results,
    /// --setlist "Signal Fire/Rin; talk: Mao, Kaede" for the concert (see Agency.SetList), and for the
    /// random event --event ID (from the game's random_events.json), --event-reply N (the player's
    /// answer, from 0; by default the first one without posts of its own) and --event-idols "Rena, Mao"
    /// (who plays the event's idols, by first name; Rin by default; the first also plays the dating
    /// scandal's idol).
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
            firingBudget = Option("firing-budget") == "1";
            firingRole = Option("firing-role") ?? firingRole;
            singleKind = Option("single") ?? singleKind;
            graduationSuccessor = Option("graduation-successor") ?? graduationSuccessor;
            graduationStaff = Option("graduation-staff") != "0";
            if (gameDir == null || !dryRun && (outFile == null || models.Count == 0))
            {
                Console.Error.WriteLine("Usage: AIFanChatterLiveReport --game-dir DIR --provider N --base-url URL --model M [--model M2] --out FILE");
                return 2;
            }

            Log.Sink = message => Console.Error.WriteLine(message);
            string gameData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Glitch Pitch", "Idol Manager");
            KeyStore.FolderOverride = KeyStore.FolderUnder(gameData);
            // A key saved before the shared key folder moves over, as it does in the game
            KeyStore.LegacyFolderOverride = KeyStore.LegacyFolderUnder(gameData);
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
                    int queuedBefore = FeedQueue.Items.Count;
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
                    if (host.LastNotice != null)
                    {
                        result["notice"] = host.LastNotice;
                        host.LastNotice = null;
                    }
                    if (dispatcher.Suspended)
                    {
                        // The mod stops requests here until the player presses Apply; the report carries on with a
                        // fresh dispatcher so the model still gets every moment
                        result["stopped"] = "true";
                        dispatcher = new Dispatcher(host, transport, new Random());
                    }

                    JSONArray queued = new();
                    foreach (QueuedItem item in FeedQueue.Items.Skip(queuedBefore))
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
            Console.WriteLine(Prompt.System(host.LanguageName, Prompt.FanCodes.Where(host.FanCounts.ContainsKey).ToList(), host.FanHandles, host.FanOpinions));
            foreach (var moment in moments)
            {
                Console.WriteLine("\n======== " + moment.Key + "\n" + Prompt.User(moment.Value(), host.RecentPosts));
            }
        }

        private static string setList;
        private static List<string> picked;
        private static string eventId = "onions_30";
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
                new("single", () => MomentContext.Single(agency.SingleAs(singleKind))),
                new("concert", () => MomentContext.Concert(agency.NewConcert)),
                new("event", () => EventMoment(agency)),
                new("scandal", () => ScandalMoment(agency)),
                new("policy", () => PolicyMoment(agency)),
                new("firing", () => FiringMoment(agency)),
                new("tour", () => MomentContext.Tour(agency.TourNow())),
                // After the others: the new election becomes every idol's last one
                new("election", () => MomentContext.Election(agency.ElectionNow())),
                // A fresh agency: its election is the one the concert just held ends with
                new("concert-election", () => { Agency fresh = Agency.Build(setList); return MomentContext.ConcertElection(fresh.NewConcert, fresh.ElectionNow()); }),
                new("awards", () => MomentContext.Awards(agency.AwardsNow())),
                new("show", () => MomentContext.ShowStart(agency.ShowNow())),
                new("contract", () => { var contract = agency.ContractNow(); return MomentContext.ContractStart(contract, new[] { contract }, business.History); }),
                new("audition", () => MomentContext.Audition(agency.AuditionNow(), Auditions.type.nationwide)),
                // Last, as the graduate leaves the group
                new("graduation", () => GraduationMoment(agency)),
            };
            string unknown = picked?.FirstOrDefault(name => all.All(m => m.Key != name));
            if (unknown != null)
            {
                throw new ArgumentException("No moment " + unknown + "; the moments are single, concert, event, scandal, policy, firing, tour, election, concert-election, awards, show, contract, audition and graduation");
            }
            return all.Where(m => picked == null || picked.Contains(m.Key))
                .Select(m => new KeyValuePair<string, Func<Moment>>(Names[m.Key], m.Value)).ToList();
        }

        private static readonly Dictionary<string, string> Names = new()
        {
            ["single"] = "a single release",
            ["concert"] = "a concert",
            ["event"] = "a random event",
            ["scandal"] = "a dating scandal",
            ["policy"] = "a paid policy change",
            ["firing"] = "a staff firing",
            ["tour"] = "a tour",
            ["election"] = "an election's results",
            ["concert-election"] = "a concert with its election's results",
            ["awards"] = "the awards show",
            ["show"] = "a show starting",
            ["contract"] = "a business contract starting",
            ["audition"] = "new members from an audition",
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
            List<string> names = eventIdols.Split(',').Select(n => n.Trim()).Where(n => n != "").ToList();
            Event_Manager._activeEvent active = agency.Event(data, names);
            List<Event_Manager._activeEvent> log = new() { active };
            // The event that starts this one, when it's part of a storyline, as already answered 3 days ago
            // with the same idols
            Event_Manager._randomEvent earlier = GameText.Events.Values.FirstOrDefault(e => e.reply.Any(r => r.Effects.Any(effect =>
                effect.parameter.StartsWith("trigger_event") && (effect.formula ?? "").Split(' ')[0] == eventId)));
            if (earlier != null)
            {
                Event_Manager._activeEvent chapter = agency.Event(earlier, names);
                chapter.date = active.date.AddDays(-3);
                chapter.state = Event_Manager._activeEvent._state.complete;
                log.Insert(0, chapter);
            }
            return MomentContext.Event(active, data.reply[reply], log);
        }

        /// <summary>
        /// The game's dating scandal template with one fixed set of its parts: the first --event-idols idol
        /// seen having dinner with a male fashion designer, photos posted on a message board, her saying it
        /// was a one-time encounter, and the agency having her apologize publicly. In the game the outcome
        /// comes from the answer's popup, which needs the engine; the report passes the game's outcome text
        /// for an ordinary apology straight to the mod.
        /// </summary>
        private static Moment ScandalMoment(Agency agency)
        {
            Event_Templates._template template = Event_Templates.Templates.FirstOrDefault(t => t.ID == DatingScandal.TemplateId)
                ?? throw new ArgumentException("No dating scandal in the game's event_templates.json");
            string name = eventIdols.Split(',').Select(n => n.Trim()).FirstOrDefault(n => n != "") ?? "";
            Event_Templates._active_template active = new() { Template = template };
            active.Actors.Add(agency.Event(new Event_Manager._randomEvent { id = "scandal", actor = { new Event_Manager._randomEvent._actor() } }, new[] { name }).actors[0]);
            active.Actors.Add(new Event_Manager._activeEvent._actor { name = "Mori Takeshi", female = false });
            foreach (KeyValuePair<string, int> part in new Dictionary<string, int>
            {
                ["how_spotted"] = 0, ["activity"] = 2, ["gender"] = 0, ["profession"] = 5, ["fling"] = 4,
                ["how_photos_delivered"] = 2, ["what_girl_admits"] = 1,
            })
            {
                active.Parts.Add(template.Parts.First(p => p.ID == part.Key && p.Values.Count > 0).Values[part.Value]);
            }
            Event_Templates._template._reply reply = template.Replies.First(r => r.Actions.Any(a => a.formula == "dating_apologize"));
            // Her running ad, cancelled over the scandal as the game's damages cancel it
            business.active_proposal ad = new() { Type = business._type.ad, Girl = active.Actors[0].girl, Agent_Name = "Burger Queen", EndDate = Agency.Today.AddMonths(2) };
            return MomentContext.DatingScandal(active, reply, new[] { Event_Templates._dating_reactions._type.Apologize__normal }, new[] { ad });
        }

        /// <summary>
        /// The agency's dating rule changed from Ambiguous to Forbidden, paid with 1 of the first --event-idols
        /// idol's scandal points, with the game's stream_rant event about her 3 weeks before as her latest
        /// scandal. Titles come from the game's policies.json.
        /// </summary>
        private static Moment PolicyMoment(Agency agency)
        {
            string name = eventIdols.Split(',').Select(n => n.Trim()).FirstOrDefault(n => n != "") ?? "";
            string Title(policies._value value) =>
                GameText.Policies.FirstOrDefault(p => p.Type == policies._type.dating && p.Value == value)?.Title ?? value.ToString();
            policies.Values.Add(new policies.value { Type = policies._type.dating, Value = policies._value.NONE, Title = Title(policies._value.NONE) });
            policies.value now = policies.GetSelectedPolicyValue(policies._type.dating);
            now.Title = Title(now.Value);
            policies.value before = new() { Type = policies._type.dating, Value = policies._value.dating_ambiguous, Title = Title(policies._value.dating_ambiguous) };
            if (!GameText.Events.TryGetValue("stream_rant", out Event_Manager._randomEvent rant))
            {
                throw new ArgumentException("No random event stream_rant in the game's random_events.json");
            }
            Event_Manager._activeEvent scandal = agency.Event(rant, new[] { name });
            scandal.date = scandal.date.AddDays(-21);
            scandal.state = Event_Manager._activeEvent._state.complete;
            data_girls.girls girl = scandal.actors[0].girl;
            return MomentContext.PolicyChange(before, now, new List<PolicyChange.Cleared> { new() { Girl = girl, Points = 1 } },
                new List<Event_Manager._activeEvent> { scandal });
        }

        private static bool firingBudget;
        private static string firingRole = "choreographer";
        private static string singleKind = "plain";
        private static string graduationSuccessor = "sister";
        private static bool graduationStaff = true;

        /// <summary>
        /// The graduation after its goodbye scene: by default she brings in her younger sister and stays on as
        /// staff. --graduation-successor sister|successor|none picks who joins, --graduation-staff 0 drops the
        /// staff job.
        /// </summary>
        private static Moment GraduationMoment(Agency agency)
        {
            Members.Rank rank = agency.GraduateNow();
            data_girls.girls successor = graduationSuccessor switch
            {
                "sister" => agency.SuccessorNow(sister: true),
                "successor" => agency.SuccessorNow(sister: false),
                "none" => null,
                _ => throw new ArgumentException("No graduation successor " + graduationSuccessor + "; the choices are sister, successor and none"),
            };
            staff._staff staffer = graduationStaff ? agency.GraduateStafferNow() : null;
            return MomentContext.Graduation(agency.Graduate, rank, successor: successor, sister: graduationSuccessor == "sister", staffer: staffer);
        }

        /// <summary>
        /// A choreographer hired 18 months before, fired with 1 of the first --event-idols idol's scandal points blamed
        /// on him, with the game's stream_rant event about her 3 weeks before as her latest scandal.
        /// --firing-budget makes it the staffer's deal instead: budget problems announced. --firing-role makes him
        /// a dance_coach (a trainer, no singles) or a sales_manager (a dealmaker, with the agency's running
        /// contracts) instead.
        /// </summary>
        private static Moment FiringMoment(Agency agency)
        {
            string name = eventIdols.Split(',').Select(n => n.Trim()).FirstOrDefault(n => n != "") ?? "";
            if (!GameText.Events.TryGetValue("stream_rant", out Event_Manager._randomEvent rant))
            {
                throw new ArgumentException("No random event stream_rant in the game's random_events.json");
            }
            Event_Manager._activeEvent scandal = agency.Event(rant, new[] { name });
            scandal.date = scandal.date.AddDays(-21);
            scandal.state = Event_Manager._activeEvent._state.complete;
            // A professional's starting skills (staff.cs): the role's main skill at 4, the other at 1
            (staff._type type, staff._skill_type main, staff._skill_type other) = firingRole switch
            {
                "choreographer" => (staff._type.choreographer, staff._skill_type.production, staff._skill_type.coaching),
                "dance_coach" => (staff._type.dance_coach, staff._skill_type.coaching, staff._skill_type.production),
                "sales_manager" => (staff._type.sales_manager, staff._skill_type.deals, staff._skill_type.production),
                _ => throw new ArgumentException("No firing role " + firingRole + "; the roles are choreographer, dance_coach and sales_manager"),
            };
            staff._staff staffer = new()
            {
                firstName = "Kenji",
                lastName = "Hayashi",
                type = type,
                HireDate = scandal.date.AddMonths(-18),
                skills = new List<staff._staff._skill>
                {
                    new() { skill_type = main, level = 4, primary = true },
                    new() { skill_type = other, level = 1 },
                },
            };
            data_girls.girls mirai = data_girls.girl.First(g => g.firstName == "Mirai");
            List<business.active_proposal> contracts = new()
            {
                new() { Type = business._type.ad, Product_Name = "Sparkle Soda", Agent_Name = "Hoshi Beverages", Girl = mirai, Payment_per_week = 900000, EndDate = Agency.Today.AddDays(60) },
                new() { Type = business._type.tv_drama, Product_Name = "Station of Dreams", Agent_Name = "NTX Television", isGroup = true, Payment_per_week = 1500000, EndDate = Agency.Today.AddDays(30) },
                new() { Type = business._type.ad, Product_Name = "Kumo Mobile", Agent_Name = "Kumo Telecom", Girl = scandal.actors[0].girl, Payment_per_week = 400000, EndDate = Agency.Today.AddDays(80) },
                new() { Type = business._type.ad, Product_Name = "Fresh Mint Gum", Agent_Name = "Daiwa Foods", Girl = mirai, Payment_per_week = 100000, EndDate = Agency.Today.AddDays(10) },
            };
            return MomentContext.StaffFiring(staffer, firingBudget, new List<PolicyChange.Cleared> { new() { Girl = scandal.actors[0].girl, Points = 1 } },
                new List<Event_Manager._activeEvent> { scandal }, contracts);
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

        /// <summary>The game's policies with their titles, as policies.Load reads them.</summary>
        public static readonly List<policies.value> Policies = new();

        /// <summary>The game's show mediums, genres and host tiers by id, as Shows.LoadParam reads them.</summary>
        public static readonly Dictionary<string, List<Shows._param>> ShowParams = new();

        /// <summary>The game's awards speech parts, by "category/part" (solo/intro_1, group/thanks_2, ...).</summary>
        public static readonly Dictionary<string, string> Speeches = new();

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
            // The tour venues' sizes, as SEvent_Tour reads them
            SEvent_Tour.Const_Capacity.Clear();
            foreach (JSONNode size in Read(Path.Combine(english, "SpecialEvents", "WorldTour", "constants.json"))["capacity"].AsArray)
            {
                SEvent_Tour.Const_Capacity.Add(size.AsInt);
            }
            ShowParams.Clear();
            foreach (string kind in new[] { "medium", "genres", "mc" })
            {
                ShowParams[kind] = new List<Shows._param>();
                foreach (JSONNode item in Read(Path.Combine(english, "Shows", kind + ".json")).AsArray)
                {
                    ShowParams[kind].Add(new Shows._param { id = item["id"].AsInt, title = item["title"] });
                }
            }
            // The awards speeches' parts by "category/part", first of each, as the show draws them
            Speeches.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "SpecialEvents", "Awards", "award_speeches.json")).AsArray)
            {
                string key = item["Categories"][0] + "/" + item["Categories"][1];
                if (!Speeches.ContainsKey(key))
                {
                    Speeches[key] = item["Text"];
                }
            }
            // Outside the game there's no dialogue controller to fill the speeches' variables
            AwardsShow.Parse = text => text.Replace("[groupname]", staticVars.PlayerData.GetGroupName())
                .Replace("[player:polite]", staticVars.PlayerData.GetPlayerName(staticVars._playerData.name_type.polite));
            Events.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Events", "random_events.json")).AsArray)
            {
                Event_Manager._randomEvent data = new();
                data.Set(item);
                Events[data.id] = data;
            }
            // The game's own list, where the mod finds which events start which
            Event_Manager.RandomEvents.Clear();
            Event_Manager.RandomEvents.AddRange(Events.Values);
            business.data.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Business", "business.json")).AsArray)
            {
                business._data data = new();
                data.Set(item);
                business.data.Add(data);
            }
            Policies.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Policies", "policies.json")).AsArray)
            {
                policies.value policy = new();
                policy.SetData(item);
                Policies.Add(policy);
            }
            Event_Templates.Templates.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Events", "event_templates.json")).AsArray)
            {
                Event_Templates._template template = new();
                template.Set(item);
                Event_Templates.Templates.Add(template);
            }
            Event_Templates.Dating_Reactions.Clear();
            foreach (JSONNode item in Read(Path.Combine(english, "Events", "template_reactions.json")).AsArray)
            {
                Event_Templates._dating_reactions reaction = new();
                reaction.Set(item);
                Event_Templates.Dating_Reactions.Add(reaction);
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
        public IDictionary<string, long> FanCounts => game.FanCounts;
        public IList<SNS_Manager._message> RecentPosts => game.RecentPosts;
        /// <summary>The last notice the mod showed, for the report.</summary>
        public string LastNotice;

        public void Notify(string text, bool error)
        {
            LastNotice = text;
            Console.Error.WriteLine((error ? "Notice (error): " : "Notice: ") + text);
        }
        public void NextFrame(Action action) => action();
        // Straight into the queue: the game's feed isn't there to release posts into, and the report lists them all
        public void Queue(List<SNS_Manager._message> posts, DateTime moment) => FeedQueue.AddBatch(posts, moment, new Random());
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
