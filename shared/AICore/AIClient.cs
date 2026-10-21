using System;
using System.Collections.Generic;

namespace AICore
{
    /// <summary>
    /// What the client needs from the game. Each mod gives its own; tests pass theirs.
    /// </summary>
    public interface IAIHost
    {
        /// <summary>Seconds of real time, which doesn't speed up with the game clock.</summary>
        double RealTime { get; }
        DateTime GameTime { get; }
        /// <summary>Something that changes when a different game is loaded or the player leaves the game.</summary>
        object Session { get; }
        Connection ReadConnection();
        bool Enabled { get; }
        int PerWeek { get; }
        void Notify(string text, bool error);
        void NextFrame(Action action);
    }

    /// <summary>
    /// Sends the requests and keeps them within the player's limits: the weekly setting, one at a time,
    /// 30 seconds apart and 20 an hour in real time, so fast-forwarding can't run up a bill. A rejected
    /// key, model or address, and a model whose reply can't be used (out of room, empty, the wrong
    /// format), stop requests until the player presses Apply; a busy or unreachable service pauses them
    /// for 10 minutes; a refusal or a content filter only costs that one request. Nothing is retried.
    /// Requests ask for the reply schema and for low reasoning effort; a service or model that rejects the
    /// request gets it again without the effort, and if it rejects that too, without the schema, and what
    /// it rejected isn't asked for again until the settings change.
    /// </summary>
    public sealed class AIClient
    {
        public const double MinGapSeconds = 30;
        public const int MaxPerHour = 20;
        public const double BackoffSeconds = 600;
        public const double ErrorNoticeSeconds = 600;
        public const int TestMaxTokens = 256;

        /// <summary>
        /// A usable reply, with what can be done about one the mod can't use.
        /// </summary>
        public sealed class Reply
        {
            private readonly AIClient client;
            private readonly Action<Action<Reply>> retry;
            private readonly Connection connection;
            private readonly string label;

            internal Reply(AIClient client, string text, Action<Action<Reply>> retry, Connection connection, string label)
            {
                this.client = client;
                Text = text;
                this.retry = retry;
                this.connection = connection;
                this.label = label;
            }

            public string Text { get; }

            /// <summary>
            /// Sends the same request again, which counts as the same request.
            /// </summary>
            public void Retry(Action<Reply> done)
            {
                client.inFlight = true;
                retry(done);
            }

            /// <summary>
            /// A reply the mod can't use (Problem.EmptyReply or Problem.Unreadable): the model will keep
            /// failing, so it's reported and requests stop until the player presses Apply.
            /// </summary>
            public void Unusable(Problem problem, string error)
            {
                client.HandleFailure(new ProviderReply { Failure = Failure.BadReply, Problem = problem, Error = error }, connection, label);
            }
        }

        private readonly IAIHost host;
        private readonly IHttpTransport transport;

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

        public AIClient(IAIHost host, IHttpTransport transport)
        {
            this.host = host;
            this.transport = transport;
        }

        public bool InFlight => inFlight;
        public bool Suspended => suspended;

        /// <summary>
        /// Called when the settings change, after the client clears what it held back.
        /// </summary>
        public event Action SettingsChanged;

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
        /// Sends a system prompt and one user message, if the limits allow, and calls done with the
        /// model's text. Failures are reported here and done isn't called. Returns whether a request was sent.
        /// </summary>
        /// <param name="schema">A JSON schema the reply must follow, for services that support one; null for none.</param>
        /// <param name="label">What the request is for, as the player knows it ("Kaho's graduation"), for the notices.</param>
        public bool Send(string system, string user, string schema, Action<Reply> done, string label = null)
        {
            if (Blocked() != null)
            {
                return false;
            }
            Connection connection = host.ReadConnection();
            bool withSchema = schema != null && !noSchema;
            bool withEffort = !noEffort;
            HttpRequestSpec request = Providers.BuildRequest(connection, system, user, Providers.MaxOutputTokens, out string error,
                withSchema ? schema : null, lowEffort: withEffort);
            if (request == null)
            {
                suspended = true;
                ReportError(error, always: true);
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

            void Handle(HttpRequestSpec sent, HttpResult result, bool schemaSent, bool effortSent, Action<Reply> then)
            {
                if ((schemaSent || effortSent) && (result.Status == 400 || result.Status == 422))
                {
                    // The service or model won't take the request: again without the effort, then without the
                    // schema too, which counts as the same request
                    bool schemaNext = effortSent && schemaSent;
                    HttpRequestSpec smaller = Providers.BuildRequest(connection, system, user, Providers.MaxOutputTokens, out _,
                        schemaNext ? schema : null);
                    if (smaller != null)
                    {
                        Log.Warn(effortSent
                            ? "The service doesn't accept low reasoning effort (HTTP " + result.Status + "); sending the request without it."
                            : "The service doesn't accept a reply schema (HTTP " + result.Status + "); sending the request without one.");
                        Transmit(smaller, retry => Handle(smaller, retry, schemaNext, false, then), connection, label);
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
                    HandleFailure(reply, connection, label);
                    return;
                }
                if (reply.FromReasoning)
                {
                    Log.Warn("The service sent the answer in the reasoning field, with no content; the answer there is used.");
                }
                then(new Reply(this, reply.Text, next => Transmit(sent, again => Handle(sent, again, schemaSent, effortSent, next), connection, label),
                    connection, label));
            }

            Transmit(request, result => Handle(request, result, withSchema, withEffort, done), connection, label);
            return true;
        }

        /// <summary>
        /// Called when Apply changes one of the mod's settings: requests may run again, ask for the reply
        /// schema and low effort again, the next error shows at once, and a connection test runs on the
        /// next frame, after every setting is saved.
        /// </summary>
        public void OnSettingsChanged(string testSystem, string testUser)
        {
            suspended = false;
            noSchema = false;
            noEffort = false;
            backoffUntil = double.NegativeInfinity;
            showNextError = true;
            SettingsChanged?.Invoke();
            TestNextFrame(testSystem, testUser);
        }

        /// <summary>
        /// Called when the player presses Apply without changing anything. If requests are stopped or
        /// paused, they may run again, the next error shows at once, and a connection test shows whether the
        /// problem is fixed. Otherwise nothing happens, so an Apply costs nothing.
        /// </summary>
        public void OnApplied(string testSystem, string testUser)
        {
            if (!suspended && host.RealTime >= backoffUntil)
            {
                return;
            }
            suspended = false;
            backoffUntil = double.NegativeInfinity;
            showNextError = true;
            TestNextFrame(testSystem, testUser);
        }

        private void TestNextFrame(string testSystem, string testUser)
        {
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
                    TestConnection(testSystem, testUser);
                }
            });
        }

        /// <summary>
        /// A tiny request that shows whether the settings work. It doesn't count towards the limits.
        /// </summary>
        public void TestConnection(string system, string user)
        {
            Connection connection = host.ReadConnection();
            HttpRequestSpec request = Providers.BuildRequest(connection, system, user, TestMaxTokens, out string error);
            if (request == null)
            {
                suspended = true;
                ReportError(error, always: true);
                return;
            }
            Transmit(request, result =>
            {
                ProviderReply reply = Providers.ParseResponse(connection.Provider, result);
                // Any proper reply means the address, key and model work, even one cut short (a model that
                // reasons always runs out of room on this tiny test) or declined
                bool answered = result.Status >= 200 && result.Status < 300
                    && (reply.Failure == Failure.BadReply || reply.Failure == Failure.Declined);
                if (reply.Ok || answered)
                {
                    host.Notify(AIMod.Name + " connected: " + connection.Describe(), error: false);
                }
                else
                {
                    HandleFailure(reply, connection, null);
                }
            }, connection, null);
        }

        private void Transmit(HttpRequestSpec request, Action<HttpResult> done, Connection connection, string label)
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
                HandleFailure(ProviderReply.Fail(Problem.SendFailed, "Couldn't send the request: " + e.Message, e.Message), connection, label);
            }
        }

        /// <summary>
        /// Stops requests for the settings or a model whose replies can't be used, pauses them for a busy
        /// or unreachable service, and only reports a failure that affects one request. The notice says
        /// what happened, for what, and what the player can do; one that stops requests always shows.
        /// </summary>
        internal void HandleFailure(ProviderReply reply, Connection connection, string label)
        {
            string notice =Notices.Text(reply.Problem, reply.Detail, reply.Status, connection, label);
            switch (reply.Failure)
            {
                case Failure.Settings:
                case Failure.BadReply:
                    suspended = true;
                    ReportError(notice, always: true);
                    break;
                case Failure.Transient:
                    backoffUntil = host.RealTime + BackoffSeconds;
                    ReportError(notice);
                    break;
                default:
                    ReportError(notice);
                    break;
            }
        }

        /// <summary>
        /// Logs every error and shows it in game at most once every 10 minutes, apart from the first one
        /// after the settings change and one that stops requests, which always show.
        /// </summary>
        private void ReportError(string error, bool always = false)
        {
            Log.Warn(error);
            double now = host.RealTime;
            if (always || showNextError || now - lastErrorNotice >= ErrorNoticeSeconds)
            {
                showNextError = false;
                lastErrorNotice = now;
                host.Notify(AIMod.Name + ": " + KeyStore.Redact(error), error: true);
            }
        }
    }
}
