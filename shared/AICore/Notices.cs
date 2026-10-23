using System;

namespace AICore
{
    /// <summary>
    /// What went wrong with a request, in enough detail to tell the player what to do.
    /// </summary>
    public enum Problem
    {
        None,
        /// <summary>No connection, a timeout or a network error.</summary>
        NoConnection,
        /// <summary>HTTP 429.</summary>
        RateLimited,
        /// <summary>HTTP 408 or 5xx.</summary>
        Unavailable,
        /// <summary>HTTP 401.</summary>
        KeyRejected,
        /// <summary>HTTP 403: the key or the account may not use this model or service.</summary>
        Forbidden,
        /// <summary>HTTP 402.</summary>
        NoCredit,
        /// <summary>HTTP 404.</summary>
        NotFound,
        /// <summary>Any other 4xx.</summary>
        Rejected,
        /// <summary>A 2xx reply that isn't JSON, such as a web page at the wrong address.</summary>
        NotJson,
        /// <summary>A 2xx reply that says the service's provider failed.</summary>
        ProviderFailed,
        /// <summary>A 2xx reply with no body or no answer in it.</summary>
        EmptyAnswer,
        /// <summary>The model used up the reply limit.</summary>
        OutOfTokens,
        /// <summary>The model's answer was empty.</summary>
        EmptyReply,
        /// <summary>The model's answer wasn't in the format the mod asked for.</summary>
        Unreadable,
        /// <summary>The model declined.</summary>
        Refused,
        /// <summary>The service's content filter stopped the answer.</summary>
        Filtered,
        /// <summary>The request couldn't be sent at all.</summary>
        SendFailed
    }

    /// <summary>
    /// The notices the player sees. Each says what happened, with which service and model, for which
    /// moment, what to do, and whether requests go on.
    /// </summary>
    public static class Notices
    {
        public const string Stopped = "Requests stopped: ";
        public const string StartAgain = ", then press Apply in Mod Settings.";
        public const string TryingAgain = "Retrying in 10 min.";
        public const string TryNow = "Retrying in 10 min, or press Apply to retry now.";

        /// <summary>
        /// The most of the service's own message a notice quotes; the log has all of it. The game's
        /// notification column is narrow, so a long notice is many lines tall.
        /// </summary>
        public const int DetailLength = 100;

        /// <summary>
        /// How the client handles a problem: stop requests, pause them, or only report it.
        /// </summary>
        public static Failure FailureOf(Problem problem)
        {
            switch (problem)
            {
                case Problem.None:
                    return Failure.None;
                case Problem.KeyRejected:
                case Problem.Forbidden:
                case Problem.NoCredit:
                case Problem.NotFound:
                case Problem.Rejected:
                case Problem.NotJson:
                    return Failure.Settings;
                case Problem.NoConnection:
                case Problem.RateLimited:
                case Problem.Unavailable:
                case Problem.ProviderFailed:
                case Problem.EmptyAnswer:
                    return Failure.Transient;
                case Problem.Refused:
                case Problem.Filtered:
                case Problem.SendFailed:
                    return Failure.Declined;
                default:
                    return Failure.BadReply;
            }
        }

        /// <summary>
        /// The notice for a problem.
        /// </summary>
        /// <param name="detail">The service's own message or the network error; "" for none.</param>
        /// <param name="status">The HTTP status, for the problems that come from one.</param>
        /// <param name="label">What the request was for, such as "Kaho's graduation"; null for the connection test.</param>
        public static string Text(Problem problem, string detail, long status, Connection connection, string label)
        {
            string service = connection.ServiceName;
            string model = (connection.ResolvedModel != "" ? connection.ResolvedModel : "the model") + " on " + service;
            detail = Providers.Shorten((detail ?? "").Trim(), DetailLength).TrimEnd('.');
            string http = "HTTP " + status + (detail != "" ? ": " + detail : "");
            string quoted = detail != "" ? " (" + detail + ")" : "";
            // Only an OpenAI-compatible service uses the Base URL
            bool usesBaseUrl = connection.Provider == Provider.Compatible;
            switch (problem)
            {
                case Problem.KeyRejected:
                    return service + " rejected the API key (" + http + "). " + Stopped + "fix the key" + StartAgain;
                case Problem.Forbidden:
                    return service + " refused access (" + http + "). " + Stopped + "check the model and key" + StartAgain;
                case Problem.NoCredit:
                    return service + ": out of credit (" + http + "). " + Stopped + "add credit" + StartAgain;
                case Problem.NotFound:
                    return service + " doesn't know model " + connection.ResolvedModel + " or the address (" + http + "). " + Stopped
                        + (usesBaseUrl ? "fix the model or Base URL" : "fix the model") + StartAgain;
                case Problem.Rejected:
                    return service + " rejected the request (" + http + "). " + Stopped + "check the settings" + StartAgain;
                case Problem.NotJson:
                    return service + " isn't answering like an AI service. " + Stopped + (usesBaseUrl ? "check the Base URL" : "check the settings") + StartAgain;
            }
            string what;
            switch (problem)
            {
                case Problem.NoConnection:
                    what = "can't reach " + service + quoted + ". " + TryNow;
                    break;
                case Problem.RateLimited:
                    what = service + " is limiting requests (" + http + "). " + TryingAgain;
                    break;
                case Problem.Unavailable:
                    what = service + " is unavailable (" + http + "). " + TryNow;
                    break;
                case Problem.ProviderFailed:
                    what = service + "'s provider failed" + quoted + ". " + TryNow;
                    break;
                case Problem.EmptyAnswer:
                    what = service + " sent an empty answer. " + TryNow;
                    break;
                case Problem.OutOfTokens:
                    what = model + " hit its " + Providers.MaxOutputTokens + "-token limit. " + Stopped + "choose another model" + StartAgain;
                    break;
                case Problem.EmptyReply:
                    what = model + " sent an empty reply. " + Stopped + "choose another model" + StartAgain;
                    break;
                case Problem.Unreadable:
                    what = model + " didn't use the reply format. " + Stopped + "choose another model" + StartAgain;
                    break;
                case Problem.Refused:
                    what = model + " declined. " + AIMod.OthersGoOn;
                    break;
                case Problem.Filtered:
                    what = service + "'s content filter blocked it. " + AIMod.OthersGoOn;
                    break;
                case Problem.SendFailed:
                    what = "couldn't send to " + service + quoted + ". The next moment will try again.";
                    break;
                default:
                    what = service + " had a problem" + quoted + ".";
                    break;
            }
            if (label != null)
            {
                return AIMod.Missed + " for " + label + ": " + what;
            }
            // Only the mod's own words get a capital; a service or model name keeps its own spelling
            return what.StartsWith("can't", StringComparison.Ordinal) || what.StartsWith("couldn't", StringComparison.Ordinal)
                ? char.ToUpperInvariant(what[0]) + what.Substring(1) : what;
        }
    }
}
