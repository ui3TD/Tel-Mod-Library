using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AICore
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

    public interface IHttpTransport
    {
        /// <summary>Sends the request and calls done on the game's main thread with the result.</summary>
        void Send(HttpRequestSpec request, Action<HttpResult> done);
    }

    /// <summary>
    /// HTTPS through .NET's HttpWebRequest on a background thread. The game's UnityWebRequest module is
    /// stripped down to downloads (no upload handler, no request headers), and its .NET has no HttpClient;
    /// HttpWebRequest comes from the full System.dll that IM-HarmonyIntegration installs in UnstrippedLibs.
    /// </summary>
    internal sealed class NetTransport : IHttpTransport
    {
        public const int TimeoutMilliseconds = 60000;

        public void Send(HttpRequestSpec spec, Action<HttpResult> done)
        {
            MainThread.Ensure();
            Thread thread = new(() =>
            {
                HttpResult result = Exchange(spec);
                MainThread.Post(() => done(result));
            })
            {
                IsBackground = true,
                Name = AIMod.Name + " request"
            };
            thread.Start();
        }

        internal static HttpResult Exchange(HttpRequestSpec spec)
        {
            HttpResult result = new();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(spec.Url);
                request.Method = "POST";
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                foreach (KeyValuePair<string, string> header in spec.Headers)
                {
                    // Content-Type is a property of its own; setting it as a header throws
                    if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        request.ContentType = header.Value;
                    }
                    else
                    {
                        request.Headers[header.Key] = header.Value;
                    }
                }
                byte[] body = Encoding.UTF8.GetBytes(spec.Body ?? "");
                request.ContentLength = body.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }
                using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                result.Status = (long)response.StatusCode;
                result.Body = ReadBody(response);
            }
            catch (WebException e) when (e.Response is HttpWebResponse response)
            {
                // An HTTP error still has a reply, with the service's message in its body
                using (response)
                {
                    result.Status = (long)response.StatusCode;
                    result.Body = ReadBody(response);
                }
            }
            catch (Exception e)
            {
                result.Status = 0;
                result.NetworkError = e.Message;
            }
            return result;
        }

        private static string ReadBody(HttpWebResponse response)
        {
            using Stream stream = response.GetResponseStream();
            if (stream == null)
            {
                return "";
            }
            using StreamReader reader = new(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Runs work handed over from the request threads on the game's main thread, every frame, also while
    /// the game clock is paused.
    /// </summary>
    internal sealed class MainThread : MonoBehaviour
    {
        private static readonly Queue<Action> Pending = new();
        private static MainThread instance;

        /// <summary>
        /// Creates the pump; called on the main thread before a request starts.
        /// </summary>
        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }
            GameObject host = new(AIMod.Name);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MainThread>();
        }

        public static void Post(Action action)
        {
            lock (Pending)
            {
                Pending.Enqueue(action);
            }
        }

        public void Update()
        {
            while (true)
            {
                Action action;
                lock (Pending)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }
                    action = Pending.Dequeue();
                }
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Log.Warn("Couldn't handle a reply: " + e);
                }
            }
        }
    }
}
