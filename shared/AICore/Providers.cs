using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AICore
{
    public enum Provider
    {
        Anthropic = 0,
        OpenAI = 1,
        Compatible = 2
    }

    public enum Failure
    {
        None,
        /// <summary>The settings are wrong (no URL, no model, a rejected key or model): retrying won't help.</summary>
        Settings,
        /// <summary>The service is busy, down or unreachable: try again later.</summary>
        Transient,
        /// <summary>The model's reply can't be used (out of room, empty, the wrong format): this model will keep failing.</summary>
        BadReply,
        /// <summary>Only this request failed (a refusal, a content filter): the next one may well work.</summary>
        Declined
    }

    public sealed class ProviderReply
    {
        public Failure Failure;
        public Problem Problem;
        /// <summary>The HTTP status, for the problems that come from one.</summary>
        public long Status;
        /// <summary>The service's own message or the network error, for the notice; "" for none.</summary>
        public string Detail = "";
        public string Text = "";
        /// <summary>What went wrong, for the log.</summary>
        public string Error = "";
        /// <summary>The answer came in the reply's reasoning field rather than its content.</summary>
        public bool FromReasoning;

        public bool Ok => Failure == Failure.None;

        public static ProviderReply Fail(Problem problem, string error, string detail = "", long status = 0) =>
            new() { Failure = Notices.FailureOf(problem), Problem = problem, Error = error, Detail = detail ?? "", Status = status };
    }

    /// <summary>
    /// The three kinds of service: Anthropic's Messages API, OpenAI's Chat Completions, and services that
    /// copy Chat Completions (OpenRouter, Ollama, LM Studio and others) at a URL the player sets.
    /// </summary>
    public static class Providers
    {
        public const string AnthropicUrl = "https://api.anthropic.com/v1/messages";
        public const string OpenAIUrl = "https://api.openai.com/v1/chat/completions";
        public const string AnthropicVersion = "2023-06-01";

        /// <summary>
        /// The model a blank Model setting uses on OpenAI, the default service.
        /// </summary>
        public const string DefaultOpenAIModel = "gpt-6-luna";

        /// <summary>
        /// The model a blank Model setting uses on Anthropic. Other services need one typed in.
        /// </summary>
        public const string DefaultAnthropicModel = "claude-haiku-5-5";

        /// <summary>
        /// Room for the reply, and for the reasoning that some models do first and count against the limit.
        /// </summary>
        public const int MaxOutputTokens = 4096;

        /// <summary>
        /// The URL to post to; null with an error when there is none. Anthropic and OpenAI always use their own
        /// address; the Base URL is only for an OpenAI-compatible service. A full endpoint is used as typed. An address with a path gets /chat/completions added, as the OpenAI
        /// SDKs add it to the base URL every service documents (openrouter.ai/api/v1, api.groq.com/openai/v1,
        /// generativelanguage.googleapis.com/v1beta/openai, open.bigmodel.cn/api/paas/v4, models.github.ai/inference);
        /// a bare host gets /v1/chat/completions, so "http://localhost:11434" works for Ollama.
        /// </summary>
        public static string ResolveUrl(Connection connection, out string error)
        {
            error = null;
            switch (connection.Provider)
            {
                case Provider.Anthropic: return AnthropicUrl;
                case Provider.OpenAI: return OpenAIUrl;
            }
            string url = (connection.BaseUrl ?? "").Trim();
            if (url == "")
            {
                error = "Set the service's address (Base URL) for an OpenAI-compatible service.";
                return null;
            }
            url = WithScheme(url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "The Base URL must be a web address, such as openrouter.ai/api/v1.";
                return null;
            }
            const string endpoint = "chat/completions";
            url = url.TrimEnd('/');
            if (url.EndsWith("/" + endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            bool hasPath = uri.AbsolutePath.Trim('/') != "";
            return hasPath ? url + "/" + endpoint : url + "/v1/" + endpoint;
        }

        /// <summary>
        /// An address typed without http:// or https:// gets one: http:// for this PC and the local network,
        /// where services such as Ollama and LM Studio run without encryption, and https:// for anything else.
        /// </summary>
        internal static string WithScheme(string url)
        {
            url = (url ?? "").Trim();
            if (url == "" || url.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return url;
            }
            string host = url.Split('/')[0];
            host = host.StartsWith("[", StringComparison.Ordinal) ? host.Substring(0, host.IndexOf(']') + 1) : host.Split(':')[0];
            host = host.ToLowerInvariant();
            bool local = host == "localhost" || host == "[::1]" || host == "0.0.0.0" || host.EndsWith(".local", StringComparison.Ordinal)
                || host.StartsWith("127.", StringComparison.Ordinal) || host.StartsWith("10.", StringComparison.Ordinal)
                || host.StartsWith("192.168.", StringComparison.Ordinal) || IsPrivate172(host);
            return (local ? "http://" : "https://") + url;
        }

        private static bool IsPrivate172(string host)
        {
            string[] parts = host.Split('.');
            return parts.Length == 4 && parts[0] == "172" && int.TryParse(parts[1], out int second) && second >= 16 && second <= 31;
        }

        /// <summary>
        /// The request for a system prompt and one user message; null with an error when the settings are incomplete.
        /// </summary>
        /// <param name="schema">A JSON schema the reply must follow, for services that support one; null for none.</param>
        /// <param name="lowEffort">Asks a model that reasons before it answers to reason only a little: Anthropic's
        /// effort, OpenAI's reasoning_effort, and for compatible services both the top-level reasoning_effort that
        /// most of them read (OpenRouter, Ollama, LM Studio, Gemini, Groq) and OpenRouter's own reasoning setting;
        /// most services ignore the one they don't know. A model that can't take it may reject the request.</param>
        /// <param name="routerEffort">With lowEffort, also sends OpenRouter's reasoning setting to a compatible service.
        /// Some services reject a field they don't know (Google's), so it can be left out and the top-level one kept.</param>
        public static HttpRequestSpec BuildRequest(Connection connection, string system, string user, int maxTokens, out string error, string schema = null,
            bool lowEffort = false, bool routerEffort = true)
        {
            string url = ResolveUrl(connection, out error);
            if (url == null)
            {
                return null;
            }
            string model = connection.ResolvedModel;
            if (model == "")
            {
                error = "Set a model name for " + connection.ServiceName + ".";
                return null;
            }
            bool hasKey = !string.IsNullOrEmpty(connection.ApiKey);
            if (!hasKey && connection.Provider != Provider.Compatible)
            {
                error = "Save an API key for " + connection.ServiceName + " in Mod Settings.";
                return null;
            }

            HttpRequestSpec request = new() { Url = url };
            request.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));
            StringBuilder body = new();
            if (connection.Provider == Provider.Anthropic)
            {
                request.Headers.Add(new KeyValuePair<string, string>("x-api-key", connection.ApiKey));
                request.Headers.Add(new KeyValuePair<string, string>("anthropic-version", AnthropicVersion));
                body.Append("{\"model\":").Append(Json.Quote(model))
                    .Append(",\"max_tokens\":").Append(Json.Number(maxTokens));
                if (schema != null || lowEffort)
                {
                    List<string> config = new();
                    if (schema != null)
                    {
                        config.Add("\"format\":{\"type\":\"json_schema\",\"schema\":" + schema + "}");
                    }
                    if (lowEffort)
                    {
                        config.Add("\"effort\":\"low\"");
                    }
                    body.Append(",\"output_config\":{").Append(string.Join(",", config.ToArray())).Append('}');
                }
                body.Append(",\"system\":").Append(Json.Quote(system))
                    .Append(",\"messages\":[{\"role\":\"user\",\"content\":").Append(Json.Quote(user)).Append("}]}");
            }
            else
            {
                if (hasKey)
                {
                    request.Headers.Add(new KeyValuePair<string, string>("Authorization", "Bearer " + connection.ApiKey));
                }
                // OpenAI's newer models reject max_tokens; services that copy the API mostly only know max_tokens
                string limit = connection.Provider == Provider.OpenAI ? "max_completion_tokens" : "max_tokens";
                body.Append("{\"model\":").Append(Json.Quote(model))
                    .Append(",\"").Append(limit).Append("\":").Append(Json.Number(maxTokens));
                if (schema != null)
                {
                    body.Append(",\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":").Append(Json.Quote(AIMod.SchemaName)).Append(",\"strict\":true,\"schema\":")
                        .Append(schema).Append("}}");
                }
                if (lowEffort)
                {
                    body.Append(",\"reasoning_effort\":\"low\"");
                    if (connection.Provider == Provider.Compatible && routerEffort)
                    {
                        body.Append(",\"reasoning\":{\"effort\":\"low\"}");
                    }
                }
                body.Append(",\"messages\":[{\"role\":\"system\",\"content\":").Append(Json.Quote(system))
                    .Append("},{\"role\":\"user\",\"content\":").Append(Json.Quote(user)).Append("}]}");
            }
            request.Body = body.ToString();
            return request;
        }

        private const string OutOfRoom = "The model ran out of room before finishing, by reasoning or writing too much.";

        /// <summary>
        /// How a reply ended, from its stop reason. Services that copy OpenAI's API mostly send OpenAI's
        /// words; OpenRouter keeps the model's own in native_finish_reason, and some services pass on
        /// Google's or Anthropic's words directly. Unknown and missing reasons are normal: the text decides.
        /// </summary>
        private enum Ending { Normal, OutOfTokens, Refused, Filtered, Error }

        private static Ending EndingOf(string reason)
        {
            switch ((reason ?? "").Trim().ToLowerInvariant())
            {
                case "length":
                case "max_tokens":
                case "model_context_window_exceeded":
                    return Ending.OutOfTokens;
                case "refusal":
                    return Ending.Refused;
                case "content_filter":
                case "safety":
                case "recitation":
                case "prohibited_content":
                case "blocklist":
                case "spii":
                    return Ending.Filtered;
                case "error":
                    return Ending.Error;
                default:
                    return Ending.Normal;
            }
        }

        /// <summary>
        /// The model's text from a reply, or why there is none.
        /// </summary>
        public static ProviderReply ParseResponse(Provider provider, HttpResult result)
        {
            if (result == null || result.Status == 0)
            {
                string network = result?.NetworkError ?? "unknown error";
                return ProviderReply.Fail(Problem.NoConnection, "No connection to the service: " + network, network);
            }

            JSONNode root = Json.TryParse(result.Body);
            if (result.Status < 200 || result.Status >= 300)
            {
                string message = Shorten(ErrorMessage(root), 200);
                string error = "The service replied HTTP " + result.Status + (message != "" ? ": " + message : "");
                return ProviderReply.Fail(HttpProblem(result.Status, message), error, message, result.Status);
            }
            if (string.IsNullOrWhiteSpace(result.Body))
            {
                return ProviderReply.Fail(Problem.EmptyAnswer, "The service sent an empty answer.");
            }
            if (root is not JSONClass)
            {
                return ProviderReply.Fail(Problem.NotJson, "The service's reply isn't JSON." + (provider == Provider.Compatible ? " Check the Base URL." : ""));
            }
            // A provider behind the service can fail after the request started and still send a 2xx
            string failed = Json.Obj(root, "error") != null ? ErrorMessage(root) : Json.Str(root, "error");
            // Some services always send the field, as null
            if (failed == "null")
            {
                failed = "";
            }
            if (Json.Obj(root, "error") != null || failed != "")
            {
                failed = Shorten(failed, 200);
                return ProviderReply.Fail(Problem.ProviderFailed, "The service's provider failed: " + failed, failed);
            }

            if (provider == Provider.Anthropic)
            {
                switch (EndingOf(Json.Str(root, "stop_reason")))
                {
                    case Ending.OutOfTokens:
                        return ProviderReply.Fail(Problem.OutOfTokens, OutOfRoom);
                    case Ending.Refused:
                        return ProviderReply.Fail(Problem.Refused, AIMod.RefusalText);
                }
                JSONArray content = Json.Arr(root, "content");
                if (content == null)
                {
                    return ProviderReply.Fail(Problem.EmptyAnswer, "The reply has no content.");
                }
                // Thinking blocks come first on models that think; only text blocks are the answer
                StringBuilder text = new();
                for (int i = 0; i < content.Count; i++)
                {
                    if (Json.Str(content[i], "type") == "text")
                    {
                        text.Append(Json.Str(content[i], "text"));
                    }
                }
                return new ProviderReply { Text = text.ToString() };
            }

            JSONArray choices = Json.Arr(root, "choices");
            if (choices == null || choices.Count == 0)
            {
                return ProviderReply.Fail(Problem.EmptyAnswer, "The reply has no choices.");
            }
            JSONNode first = choices[0];
            if (Json.Obj(first, "error") != null)
            {
                string message = Shorten(Json.Str(Json.Obj(first, "error"), "message"), 200);
                return ProviderReply.Fail(Problem.ProviderFailed, "The service's provider failed: " + message, message);
            }
            Ending ending = EndingOf(Json.Str(first, "finish_reason"));
            if (ending == Ending.Normal)
            {
                ending = EndingOf(Json.Str(first, "native_finish_reason"));
            }
            switch (ending)
            {
                case Ending.OutOfTokens:
                    return ProviderReply.Fail(Problem.OutOfTokens, OutOfRoom);
                case Ending.Refused:
                    return ProviderReply.Fail(Problem.Refused, AIMod.RefusalText);
                case Ending.Filtered:
                    return ProviderReply.Fail(Problem.Filtered, "The service's content filter stopped the reply.");
                case Ending.Error:
                    return ProviderReply.Fail(Problem.ProviderFailed, "The service's provider failed.");
            }
            JSONClass choice = Json.Obj(first, "message");
            // A refusal comes in a field of its own, with no content to read; most services always send the field, as null
            string refusal = Json.Str(choice, "refusal");
            if (refusal != "" && refusal != "null")
            {
                return ProviderReply.Fail(Problem.Refused, AIMod.RefusalText);
            }
            string reply = TextOf(choice?["content"]);
            if (reply.Trim() == "")
            {
                // Some providers put the whole answer in the reasoning field and leave the content empty
                foreach (string field in new[] { "reasoning", "reasoning_content" })
                {
                    string answer = LastAnswer(TextOf(choice?[field]));
                    if (answer != "")
                    {
                        return new ProviderReply { Text = answer, FromReasoning = true };
                    }
                }
            }
            return new ProviderReply { Text = reply };
        }

        /// <summary>
        /// The text of a content field: a string, or a list of parts whose text is joined. SimpleJSON reads
        /// a JSON null as the text "null", which is no text.
        /// </summary>
        private static string TextOf(JSONNode node)
        {
            if (node is JSONData)
            {
                string text = node.Value ?? "";
                return text == "null" ? "" : text;
            }
            if (node is JSONArray parts)
            {
                StringBuilder text = new();
                for (int i = 0; i < parts.Count; i++)
                {
                    string type = Json.Str(parts[i], "type");
                    if (type == "" || type == "text" || type == "output_text")
                    {
                        text.Append(Json.Str(parts[i], "text"));
                    }
                }
                return text.ToString();
            }
            return "";
        }

        /// <summary>
        /// The last JSON object in a text that starts with the mod's answer key, up to the end of the text;
        /// "" when there is none. Reasoning can hold drafts, so the last one is the answer.
        /// </summary>
        internal static string LastAnswer(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            MatchCollection starts = Regex.Matches(text, "\\{\\s*\"" + Regex.Escape(AIMod.AnswerKey) + "\"\\s*:");
            return starts.Count == 0 ? "" : text.Substring(starts[starts.Count - 1].Index).Trim();
        }

        /// <summary>
        /// What an HTTP error status means: 408 and 429 are timing, 5xx is the service, and every other 4xx is
        /// the request, so the settings.
        /// </summary>
        /// <param name="message">The service's own message. OpenRouter answers 404 "Provider returned error"
        /// when the model exists but none of its providers could take the request, which passes.</param>
        private static Problem HttpProblem(long status, string message)
        {
            if (status == 429)
                return Problem.RateLimited;
            if (status == 408 || status >= 500)
                return Problem.Unavailable;
            if (status == 404 && (message ?? "").IndexOf("provider returned error", StringComparison.OrdinalIgnoreCase) >= 0)
                return Problem.Unavailable;
            if (status == 401)
                return Problem.KeyRejected;
            if (status == 403)
                return Problem.Forbidden;
            if (status == 402)
                return Problem.NoCredit;
            if (status == 404)
                return Problem.NotFound;
            return Problem.Rejected;
        }

        /// <summary>
        /// The message in an error reply: Anthropic's and OpenAI's are both error.message; some services
        /// send a plain string or a top-level message. Google wraps its error in a list: [{"error": {...}}].
        /// </summary>
        private static string ErrorMessage(JSONNode root)
        {
            if (root is JSONArray list && list.Count > 0)
            {
                root = list[0];
            }
            if (root is not JSONClass)
            {
                return "";
            }
            string message = Json.Str(Json.Obj(root, "error"), "message");
            if (message == "")
            {
                message = Json.Str(root, "error");
            }
            if (message == "")
            {
                message = Json.Str(root, "message");
            }
            return message;
        }

        internal static string Shorten(string text, int max) => text == null || text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
