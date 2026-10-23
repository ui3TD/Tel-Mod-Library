using AICore;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>How many fans of each fan code the agency has, only the codes it has any of.</summary>
        IDictionary<string, long> FanCounts { get; }
        /// <summary>The feed's posts, oldest first, then the queued ones that haven't appeared yet.</summary>
        IList<SNS_Manager._message> RecentPosts { get; }
        /// <summary>Queues the posts for the moment that happened at this in-game time.</summary>
        void Queue(List<SNS_Manager._message> posts, DateTime moment);
        /// <summary>Shows the posts and their replies at once, without a notification: the player asked for them.</summary>
        void ShowNow(List<SNS_Manager._message> posts);
    }

    /// <summary>
    /// Asks for posts about moments through the shared AI client, which keeps the requests within the
    /// player's limits. An agency with fewer than MinFans fans gets no posts. A reply with a single post, when the prompt asked for several, is taken as a failed
    /// reply and the request is sent once more; if that one has a single post too, it's shown, and there
    /// are no more such retries until the settings change. A reply with no posts at all stops requests until
    /// the player presses Apply, as the model will keep failing.
    /// </summary>
    public sealed class Dispatcher
    {
        public const double MinGapSeconds = AIClient.MinGapSeconds;
        public const int MaxPerHour = AIClient.MaxPerHour;
        public const double BackoffSeconds = AIClient.BackoffSeconds;
        public const int TestMaxTokens = AIClient.TestMaxTokens;

        /// <summary>Fewer fans than this, and there's no one to post.</summary>
        public const long MinFans = 100;

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
        public bool Paused => client.Paused;

        /// <summary>
        /// The Monday that starts the in-game week of a date.
        /// </summary>
        public static DateTime WeekOf(DateTime date) => AIClient.WeekOf(date);

        /// <summary>
        /// Why a request can't be sent now, or null when it can.
        /// </summary>
        /// <param name="manual">For the player's Refresh: see AIClient.Blocked.</param>
        public string Blocked(bool manual = false) => client.Blocked(manual) ?? (TooFewFans ? "the agency has fewer than " + MinFans + " fans" : null);

        /// <summary>Whether the agency has too few fans for anyone to post.</summary>
        public bool TooFewFans => host.FanCounts.Values.Sum() < MinFans;

        /// <summary>
        /// Asks for posts about a moment, if the limits allow. Returns whether a request was sent.
        /// </summary>
        /// <param name="manual">The player's Refresh: it isn't counted toward the limits (AIClient.Blocked), and its
        /// posts show at once instead of over the days after.</param>
        public bool Request(Moment moment, bool manual = false)
        {
            if (moment == null || Blocked(manual) != null)
            {
                return false;
            }
            // Only the kinds of fans the agency has post
            List<string> codes = Prompt.FanCodes.Where(host.FanCounts.ContainsKey).ToList();
            if (codes.Count == 0)
            {
                // The prompt never goes without accounts
                codes = Prompt.FanCodes.ToList();
            }
            string system = Prompt.System(host.LanguageName, codes, host.FanHandles, host.FanOpinions);
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
                List<SNS_Manager._message> posts = PostParser.Parse(reply.Text, random, codes);
                if (posts.Count == 0)
                {
                    // The model will keep failing, so requests stop until the player presses Apply
                    if ((reply.Text ?? "").Trim() == "")
                    {
                        reply.Unusable(Problem.EmptyReply, "The model's reply was empty.");
                    }
                    else
                    {
                        reply.Unusable(Problem.Unreadable, "The model's reply had no posts in the expected format.");
                    }
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
                if (manual)
                {
                    host.ShowNow(posts);
                }
                else
                {
                    host.Queue(posts, momentTime);
                }
            }

            return client.Send(system, user, Prompt.ReplySchemaFor(codes), reply => Handle(reply, false), moment.Label, manual);
        }

        /// <summary>
        /// Called when Apply changes one of the mod's settings: requests may run again, ask for the reply
        /// schema and low effort again, retry one-post replies again, the next error shows at once, and a
        /// connection test runs on the next frame, after every setting is saved.
        /// </summary>
        public void OnSettingsChanged() => client.OnSettingsChanged(Prompt.TestSystem, Prompt.TestUser);

        /// <summary>
        /// Called when the player presses Apply without changing anything: stopped or paused requests may
        /// run again, after a connection test. Nothing happens when requests weren't stopped.
        /// </summary>
        public void OnApplied() => client.OnApplied(Prompt.TestSystem, Prompt.TestUser);

        /// <summary>
        /// A tiny request that shows whether the settings work. It doesn't count towards the limits.
        /// </summary>
        public void TestConnection() => client.TestConnection(Prompt.TestSystem, Prompt.TestUser);
    }
}
