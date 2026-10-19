using AICore;
using System;
using System.Collections.Generic;

namespace AIFanChatter
{
    /// <summary>
    /// What the dispatcher needs from the game. The game's version is GameHost; tests pass their own.
    /// </summary>
    public interface IGameHost : IAIHost
    {
        string LanguageName { get; }
        IDictionary<string, string> FanHandles { get; }
        /// <summary>Each fan code's opinion of the group, 0 to 100, where the game has one.</summary>
        IDictionary<string, int> FanOpinions { get; }
        /// <summary>The feed's posts, oldest first, then the queued ones that haven't appeared yet.</summary>
        IList<SNS_Manager._message> RecentPosts { get; }
        /// <summary>Queues the posts for the moment that happened at this in-game time.</summary>
        void Queue(List<SNS_Manager._message> posts, DateTime moment);
    }

    /// <summary>
    /// Asks for posts about moments through the shared AI client, which keeps the requests within the
    /// player's limits. A reply with a single post, when the prompt asked for several, is taken as a failed
    /// reply and the request is sent once more; if that one has a single post too, it's shown, and there
    /// are no more such retries until the settings change.
    /// </summary>
    public sealed class Dispatcher
    {
        public const double MinGapSeconds = AIClient.MinGapSeconds;
        public const int MaxPerHour = AIClient.MaxPerHour;
        public const double BackoffSeconds = AIClient.BackoffSeconds;
        public const double ErrorNoticeSeconds = AIClient.ErrorNoticeSeconds;
        public const int TestMaxTokens = AIClient.TestMaxTokens;

        private static Dispatcher instance;

        /// <summary>The game's dispatcher. Tests set their own.</summary>
        public static Dispatcher Instance
        {
            get => instance ??= new Dispatcher(new GameHost(), new NetTransport(), new Random());
            internal set => instance = value;
        }

        private readonly IGameHost host;
        private readonly AIClient client;
        private readonly Random random;

        private bool noRetry;

        public Dispatcher(IGameHost host, IHttpTransport transport, Random random)
        {
            this.host = host;
            this.random = random;
            client = new AIClient(host, transport);
            client.SettingsChanged += () => noRetry = false;
        }

        public bool InFlight => client.InFlight;
        public bool Suspended => client.Suspended;

        /// <summary>
        /// The Monday that starts the in-game week of a date.
        /// </summary>
        public static DateTime WeekOf(DateTime date) => AIClient.WeekOf(date);

        /// <summary>
        /// Why a request can't be sent now, or null when it can.
        /// </summary>
        public string Blocked() => client.Blocked();

        /// <summary>
        /// Asks for posts about a moment, if the limits allow. Returns whether a request was sent.
        /// </summary>
        public bool Request(Moment moment)
        {
            if (moment == null || Blocked() != null)
            {
                return false;
            }
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
            // The posts come in over the days after the moment, not after the reply
            DateTime momentTime = host.GameTime;
            object session = host.Session;
            // A player's prompt that asks for a set number of posts has no {min}, and may ask for one
            bool mayRetry = !noRetry && Prompt.AsksForSeveralPosts();

            void Handle(AIClient.Reply reply, bool retried)
            {
                List<SNS_Manager._message> posts = PostParser.Parse(reply.Text, random);
                if (posts.Count == 0)
                {
                    reply.Error("The model's reply had no posts in the expected format.");
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
                        reply.Retry(again => Handle(again, true));
                        return;
                    }
                    // Twice in a row: this model keeps to one post, so it's shown, and no more retries for now
                    noRetry = true;
                    Log.Warn("The model's reply had only one post again; it's shown, and one-post replies aren't sent again until the settings change.");
                }
                host.Queue(posts, momentTime);
            }

            return client.Send(system, user, Prompt.ReplySchema, reply => Handle(reply, false));
        }

        /// <summary>
        /// Called when Apply changes one of the mod's settings: requests may run again, ask for the reply
        /// schema and low effort again, retry one-post replies again, the next error shows at once, and a
        /// connection test runs on the next frame, after every setting is saved.
        /// </summary>
        public void OnSettingsChanged() => client.OnSettingsChanged(Prompt.TestSystem, Prompt.TestUser);

        /// <summary>
        /// A tiny request that shows whether the settings work. It doesn't count towards the limits.
        /// </summary>
        public void TestConnection() => client.TestConnection(Prompt.TestSystem, Prompt.TestUser);
    }
}
