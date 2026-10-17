using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIFanChatter
{
    public sealed class HttpRequestSpec
    {
        public string Url;
        public readonly List<KeyValuePair<string, string>> Headers = new();
        public string Body;

        public string Header(string name)
        {
            foreach (KeyValuePair<string, string> header in Headers)
            {
                if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }
            return null;
        }
    }

    public sealed class HttpResult
    {
        /// <summary>The HTTP status, or 0 when no reply arrived.</summary>
        public long Status;
        public string Body = "";
        /// <summary>Why no reply arrived (no connection, timeout, TLS); null when one did.</summary>
        public string NetworkError;
    }

    public enum Failure
    {
        None,
        /// <summary>The settings are wrong (no URL, no model, a rejected key or model): retrying won't help.</summary>
        Settings,
        /// <summary>The service is busy, down or unreachable: try again later.</summary>
        Transient,
        /// <summary>A reply arrived but held nothing usable.</summary>
        BadReply
    }

    public sealed class ProviderReply
    {
        public Failure Failure;
        public string Text = "";
        public string Error = "";

        public bool Ok => Failure == Failure.None;

        public static ProviderReply Fail(Failure failure, string error) => new() { Failure = failure, Error = error };
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
        /// Room for the posts, and for the reasoning that some models do first and count against the limit.
        /// </summary>
        public const int MaxOutputTokens = 4096;

        private static string Endpoint(Provider provider) => provider == Provider.Anthropic ? "messages" : "chat/completions";

        /// <summary>
        /// The URL to post to; null with an error when there is none. A blank setting is the provider's own
        /// address. A full endpoint is used as typed; an address ending in /v1 gets the endpoint added, and
        /// any other gets /v1/ and the endpoint, so "http://localhost:11434" works for Ollama.
        /// </summary>
        public static string ResolveUrl(Connection connection, out string error)
        {
            error = null;
            string url = (connection.BaseUrl ?? "").Trim();
            if (url == "")
            {
                switch (connection.Provider)
                {
                    case Provider.Anthropic: return AnthropicUrl;
                    case Provider.OpenAI: return OpenAIUrl;
                    default:
                        error = "Set the service's address (Base URL) for an OpenAI-compatible service.";
                        return null;
                }
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "The Base URL must start with http:// or https://.";
                return null;
            }
            string endpoint = Endpoint(connection.Provider);
            url = url.TrimEnd('/');
            if (url.EndsWith("/" + endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return url + "/" + endpoint;
            }
            return url + "/v1/" + endpoint;
        }

        /// <summary>
        /// The request for a system prompt and one user message; null with an error when the settings are incomplete.
        /// </summary>
        public static HttpRequestSpec BuildRequest(Connection connection, string system, string user, int maxTokens, out string error)
        {
            string url = ResolveUrl(connection, out error);
            if (url == null)
            {
                return null;
            }
            string model = connection.ResolvedModel;
            if (model == "")
            {
                error = "Set a model name for " + connection.Provider + ".";
                return null;
            }
            bool hasKey = !string.IsNullOrEmpty(connection.ApiKey);
            if (!hasKey && connection.Provider != Provider.Compatible)
            {
                error = "Save an API key for " + connection.Provider + " in Mod Settings.";
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
                    .Append(",\"max_tokens\":").Append(Json.Number(maxTokens))
                    .Append(",\"system\":").Append(Json.Quote(system))
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
                    .Append(",\"").Append(limit).Append("\":").Append(Json.Number(maxTokens))
                    .Append(",\"messages\":[{\"role\":\"system\",\"content\":").Append(Json.Quote(system))
                    .Append("},{\"role\":\"user\",\"content\":").Append(Json.Quote(user)).Append("}]}");
            }
            request.Body = body.ToString();
            return request;
        }

        /// <summary>
        /// The model's text from a reply, or why there is none.
        /// </summary>
        public static ProviderReply ParseResponse(Provider provider, HttpResult result)
        {
            if (result == null || result.Status == 0)
            {
                return ProviderReply.Fail(Failure.Transient, "No connection to the service: " + (result?.NetworkError ?? "unknown error"));
            }

            JSONNode root = Json.TryParse(result.Body);
            if (result.Status < 200 || result.Status >= 300)
            {
                string message = ErrorMessage(root);
                string error = "The service replied HTTP " + result.Status + (message != "" ? ": " + Shorten(message, 200) : "");
                // 408 and 429 are timing; 5xx is the service; every other 4xx is the request, so the settings
                bool transient = result.Status == 408 || result.Status == 429 || result.Status >= 500;
                return ProviderReply.Fail(transient ? Failure.Transient : Failure.Settings, error);
            }
            if (root is not JSONClass)
            {
                return ProviderReply.Fail(Failure.BadReply, "The service's reply isn't JSON. Check the Base URL.");
            }

            if (provider == Provider.Anthropic)
            {
                string stop = Json.Str(root, "stop_reason");
                if (stop == "refusal")
                {
                    return ProviderReply.Fail(Failure.BadReply, "The model declined to write the posts.");
                }
                if (stop == "max_tokens")
                {
                    return ProviderReply.Fail(Failure.BadReply, "The model ran out of room before finishing.");
                }
                JSONArray content = Json.Arr(root, "content");
                if (content == null)
                {
                    return ProviderReply.Fail(Failure.BadReply, "The reply has no content.");
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
                return ProviderReply.Fail(Failure.BadReply, "The reply has no choices.");
            }
            string finish = Json.Str(choices[0], "finish_reason");
            if (finish == "length")
            {
                return ProviderReply.Fail(Failure.BadReply, "The model ran out of room before finishing.");
            }
            if (finish == "content_filter")
            {
                return ProviderReply.Fail(Failure.BadReply, "The service's content filter stopped the reply.");
            }
            string reply = Json.Str(Json.Obj(choices[0], "message"), "content");
            // SimpleJSON reads a JSON null as the text "null"
            return new ProviderReply { Text = reply == "null" ? "" : reply };
        }

        /// <summary>
        /// The message in an error reply: Anthropic's and OpenAI's are both error.message; some services
        /// send a plain string or a top-level message.
        /// </summary>
        private static string ErrorMessage(JSONNode root)
        {
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
