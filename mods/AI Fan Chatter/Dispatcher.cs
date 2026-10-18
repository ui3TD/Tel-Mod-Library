using System;
using System.Collections.Generic;

namespace AIFanChatter
{
    public interface IHttpTransport
    {
        /// <summary>Sends the request and calls done on the game's main thread with the result.</summary>
        void Send(HttpRequestSpec request, Action<HttpResult> done);
    }

    /// <summary>
    /// What the dispatcher needs from the game. The game's version is GameHost; tests pass their own.
    /// </summary>
    public interface IGameHost
    {
        /// <summary>Seconds of real time, which doesn't speed up with the game clock.</summary>
        double RealTime { get; }
        DateTime GameTime { get; }
        /// <summary>Something that changes when a different game is loaded or the player leaves the game.</summary>
        object Session { get; }
        Connection ReadConnection();
        bool Enabled { get; }
        int PerWeek { get; }
        string LanguageName { get; }
        IDictionary<string, string> FanHandles { get; }
        /// <summary>Each fan code's opinion of the group, 0 to 100, where the game has one.</summary>
        IDictionary<string, int> FanOpinions { get; }
        /// <summary>The feed's posts, oldest first, then the queued ones that haven't appeared yet.</summary>
        IList<SNS_Manager._message> RecentPosts { get; }
        void Notify(string text, bool error);
        void NextFrame(Action action);
        void Queue(List<SNS_Manager._message> posts);
    }

    /// <summary>
    /// Sends the requests and keeps them within the player's limits: the weekly setting, one at a time,
    /// 30 seconds apart and 20 an hour in real time, so fast-forwarding can't run up a bill. A rejected
    /// key, model or address stops requests until the settings change; a busy or unreachable service
    /// pauses them for 10 minutes. Requests ask for the reply schema and for low reasoning effort; a
    /// service or model that rejects the request gets it again without the effort, and if it rejects that
    /// too, without the schema, and what it rejected isn't asked for again until the settings change. A
    /// reply with a single post, when the prompt asked for several, is taken as a failed reply and the
    /// request is sent once more; if that one has a single post too, it's shown, and there are no more
    /// such retries until the settings change.
    /// </summary>
    public sealed class Dispatcher
    {
        public const double MinGapSeconds = 30;
        public const int MaxPerHour = 20;
        public const double BackoffSeconds = 600;
        public const double ErrorNoticeSeconds = 600;
        public const int TestMaxTokens = 256;

        private static Dispatcher instance;

        /// <summary>The game's dispatcher. Tests set their own.</summary>
        public static Dispatcher Instance
        {
            get => instance ??= new Dispatcher(new GameHost(), new NetTransport(), new Random());
            internal set => instance = value;
        }

        private readonly IGameHost host;
        private readonly IHttpTransport transport;
        private readonly Random random;

        private bool inFlight;
        private double lastSend = double.NegativeInfinity;
        private readonly List<double> sentThisHour = new();
        private DateTime week;
        private int sentThisWeek;
        private bool suspended;
        private double backoffUntil = double.NegativeInfinity;
        private double lastErrorNotice = double.NegativeInfinity;
        private bool showNextError = true;
        private bool testPending;
        private bool noSchema;
        private bool noEffort;
        private bool noRetry;

        public Dispatcher(IGameHost host, IHttpTransport transport, Random random)
        {
            this.host = host;
            this.transport = transport;
            this.random = random;
        }

        public bool InFlight => inFlight;
        public bool Suspended => suspended;

        /// <summary>
        /// The Monday that starts the in-game week of a date.
        /// </summary>
        public static DateTime WeekOf(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        /// <summary>
        /// Why a request can't be sent now, or null when it can.
        /// </summary>
        public string Blocked()
        {
            if (!host.Enabled)
                return "the mod is off";
            if (host.PerWeek <= 0)
                return "requests per week is 0";
            if (suspended)
                return "requests are stopped until the settings change";
            if (inFlight)
                return "a request is still running";
            double now = host.RealTime;
            if (now < backoffUntil)
                return "waiting after the service was unavailable";
            if (now - lastSend < MinGapSeconds)
                return "the last request was under 30 seconds ago";
            sentThisHour.RemoveAll(t => now - t >= 3600);
            if (sentThisHour.Count >= MaxPerHour)
                return "20 requests were sent in the last hour";
            if (WeekOf(host.GameTime) == week && sentThisWeek >= host.PerWeek)
                return "this week's requests are used up";
            return null;
        }

        /// <summary>
        /// Asks for posts about a moment, if the limits allow. Returns whether a request was sent.
        /// </summary>
        public bool Request(Moment moment)
        {
            if (moment == null || Blocked() != null)
            {
                return false;
            }
            Connection connection = host.ReadConnection();
            string system = Prompt.System(host.LanguageName, host.FanHandles, host.FanOpinions);
            IList<SNS_Manager._message> feed = null;
            try
            {
                feed = host.RecentPosts;
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't read the feed for the prompt; it's sent without it: " + e.Message);
            }
            string user = Prompt.User(moment, feed);
            bool withSchema = !noSchema;
            bool withEffort = !noEffort;
            HttpRequestSpec request = Providers.BuildRequest(connection, system, user, Providers.MaxOutputTokens, out string error,
                withSchema ? Prompt.ReplySchema : null, lowEffort: withEffort);
            if (request == null)
            {
                suspended = true;
                ReportError(error);
                return false;
            }

            double now = host.RealTime;
            lastSend = now;
            sentThisHour.Add(now);
            DateTime thisWeek = WeekOf(host.GameTime);
            if (thisWeek != week)
            {
                week = thisWeek;
                sentThisWeek = 0;
            }
            sentThisWeek++;
            inFlight = true;
            object session = host.Session;
            // A player's prompt that asks for a set number of posts has no {min}, and may ask for one
            bool mayRetry = !noRetry && Prompt.AsksForSeveralPosts();

            void Handle(HttpRequestSpec sent, HttpResult result, bool schemaSent, bool effortSent, bool retried)
            {
                if ((schemaSent || effortSent) && (result.Status == 400 || result.Status == 422))
                {
                    // The service or model won't take the request: again without the effort, then without the
                    // schema too, which counts as the same request
                    bool schemaNext = effortSent && schemaSent;
                    HttpRequestSpec smaller = Providers.BuildRequest(connection, system, user, Providers.MaxOutputTokens, out _,
                        schemaNext ? Prompt.ReplySchema : null);
                    if (smaller != null)
                    {
                        Log.Warn(effortSent
                            ? "The service doesn't accept low reasoning effort (HTTP " + result.Status + "); sending the request without it."
                            : "The service doesn't accept a reply schema (HTTP " + result.Status + "); sending the request without one.");
                        Send(smaller, retry => Handle(smaller, retry, schemaNext, false, retried));
                        return;
                    }
                }
                inFlight = false;
                if (result.Status >= 200 && result.Status < 300)
                {
                    // Only what was dropped on the way to this answer stays dropped
                    noEffort |= withEffort && !effortSent;
                    noSchema |= withSchema && !schemaSent;
                }
                ProviderReply reply = Providers.ParseResponse(connection.Provider, result);
                if (!reply.Ok)
                {
                    HandleFailure(reply);
                    return;
                }
                List<SNS_Manager._message> posts = PostParser.Parse(reply.Text, random);
                if (posts.Count == 0)
                {
                    ReportError("The model's reply had no posts in the expected format.");
                    return;
                }
                if (!ReferenceEquals(session, host.Session))
                {
                    Log.Warn("Posts arrived after a different game was loaded; dropped.");
                    return;
                }
                if (posts.Count == 1 && mayRetry)
                {
                    if (!retried)
                    {
                        // Taken as a failed reply: the same request again, which counts as the same request
                        Log.Warn("The model's reply had only one post; sending the request again.");
                        inFlight = true;
                        Send(sent, retry => Handle(sent, retry, schemaSent, effortSent, true));
                        return;
                    }
                    // Twice in a row: this model keeps to one post, so it's shown, and no more retries for now
                    noRetry = true;
                    Log.Warn("The model's reply had only one post again; it's shown, and one-post replies aren't sent again until the settings change.");
                }
                host.Queue(posts);
            }

            Send(request, result => Handle(request, result, withSchema, withEffort, false));
            return true;
        }

        /// <summary>
        /// Called when Apply changes one of the mod's settings: requests may run again, ask for the reply
        /// schema and low effort again, retry one-post replies again, the next error shows at once, and a
        /// connection test runs on the next frame, after every setting is saved.
        /// </summary>
        public void OnSettingsChanged()
        {
            suspended = false;
            noSchema = false;
            noEffort = false;
            noRetry = false;
            backoffUntil = double.NegativeInfinity;
            showNextError = true;
            if (testPending)
            {
                return;
            }
            testPending = true;
            host.NextFrame(() =>
            {
                testPending = false;
                if (host.Enabled)
                {
                    TestConnection();
                }
            });
        }

        /// <summary>
        /// A tiny request that shows whether the settings work. It doesn't count towards the limits.
        /// </summary>
        public void TestConnection()
        {
            Connection connection = host.ReadConnection();
            HttpRequestSpec request = Providers.BuildRequest(connection, Prompt.TestSystem, Prompt.TestUser, TestMaxTokens, out string error);
            if (request == null)
            {
                suspended = true;
                ReportError(error);
                return;
            }
            Send(request, result =>
            {
                ProviderReply reply = Providers.ParseResponse(connection.Provider, result);
                // Any proper reply means the address, key and model work, even one cut short
                if (reply.Ok || reply.Failure == Failure.BadReply && result.Status >= 200 && result.Status < 300)
                {
                    host.Notify("AI Fan Chatter connected: " + connection.Describe(), error: false);
                }
                else
                {
                    HandleFailure(reply);
                }
            });
        }

        private void Send(HttpRequestSpec request, Action<HttpResult> done)
        {
            try
            {
                transport.Send(request, result =>
                {
                    try
                    {
                        done(result);
                    }
                    catch (Exception e)
                    {
                        inFlight = false;
                        Log.Warn("Couldn't handle the reply: " + e);
                    }
                });
            }
            catch (Exception e)
            {
                inFlight = false;
                ReportError("Couldn't send the request: " + e.Message);
            }
        }

        private void HandleFailure(ProviderReply reply)
        {
            if (reply.Failure == Failure.Settings)
            {
                suspended = true;
                ReportError(reply.Error + " Requests stop until the settings change.");
            }
            else if (reply.Failure == Failure.Transient)
            {
                backoffUntil = host.RealTime + BackoffSeconds;
                ReportError(reply.Error + " Trying again in 10 minutes.");
            }
            else
            {
                ReportError(reply.Error);
            }
        }

        /// <summary>
        /// Logs every error and shows it in game at most once every 10 minutes, apart from the first one
        /// after the settings change, which always shows.
        /// </summary>
        private void ReportError(string error)
        {
            Log.Warn(error);
            double now = host.RealTime;
            if (showNextError || now - lastErrorNotice >= ErrorNoticeSeconds)
            {
                showNextError = false;
                lastErrorNotice = now;
                host.Notify("AI Fan Chatter: " + KeyStore.Redact(error), error: true);
            }
        }
    }
}
