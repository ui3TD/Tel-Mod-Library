using DG.Tweening;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AIFanChatter
{
    /// <summary>
    /// The dispatcher's view of the running game.
    /// </summary>
    internal sealed class GameHost : IGameHost
    {
        private readonly System.Random random = new();

        public double RealTime => Time.realtimeSinceStartup;
        public DateTime GameTime => staticVars.dateTime;
        public object Session => FeedRelease.Session;
        public Connection ReadConnection() => Settings.Current();
        public bool Enabled => Settings.Enabled;
        public int PerWeek => Settings.PerWeek;
        public string LanguageName => Prompt.LanguageName(staticVars.Settings?.Language);

        public IDictionary<string, string> FanHandles
        {
            get
            {
                Dictionary<string, string> handles = new();
                foreach (string code in Prompt.FanCodes)
                {
                    // The game's names for the fan accounts: SNS__MCT for M_C_T and so on
                    if (Language.Data != null && Language.Data.TryGetValue("SNS__" + code.Replace("_", ""), out string handle))
                    {
                        handles[code] = Moment.Clean(handle);
                    }
                }
                return handles;
            }
        }

        public void Notify(string text, bool error)
        {
            NotificationManager.AddNotification(text, error ? mainScript.red32 : mainScript.green32);
        }

        public void NextFrame(Action action)
        {
            // A sequence holding only a callback runs it on DOTween's next update, ignoring time scale. The
            // extension methods are called statically: the game's DOTween build lacks their markers.
            TweenSettingsExtensions.SetUpdate(TweenSettingsExtensions.AppendCallback(DOTween.Sequence(), () => action()), true);
        }

        public void Queue(List<SNS_Manager._message> posts)
        {
            FeedQueue.AddBatch(posts, staticVars.dateTime, random);
        }
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
                Name = "AI Fan Chatter request"
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
            GameObject host = new("AI Fan Chatter");
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

    /// <summary>
    /// Puts queued posts and replies into the game's feed when they're due.
    /// </summary>
    internal static class FeedRelease
    {
        /// <summary>
        /// Set while a post that shouldn't show a notification is added; the AddNotification patch reads it.
        /// </summary>
        internal static bool Quiet;

        /// <summary>
        /// Replaced when a game starts or loads, so a reply to a request sent in another game is dropped.
        /// </summary>
        internal static object Session = new();

        private static bool tickErrorLogged;

        public static void NewSession()
        {
            Session = new object();
        }

        public static void OnTimeTick()
        {
            try
            {
                if (FeedQueue.Items.Count == 0 || !Settings.Enabled)
                {
                    return;
                }
                QueuedItem item = FeedQueue.TakeDue(staticVars.dateTime);
                if (item != null)
                {
                    Release(item);
                }
            }
            catch (Exception e)
            {
                if (!tickErrorLogged)
                {
                    tickErrorLogged = true;
                    Log.Warn("Couldn't add a queued post: " + e);
                }
            }
        }

        private static void Release(QueuedItem item)
        {
            if (!item.IsReply)
            {
                // Through the game's own method, so other mods' patches on it see every post
                Quiet = !item.First;
                try
                {
                    SNS_Manager.AddMessage(new SNS_Manager._message { User = item.User, Message = item.Text });
                }
                finally
                {
                    Quiet = false;
                }
                return;
            }

            SNS_Manager._message parent = FeedStore.FindParent(SNS_Manager.Messages, item.ParentUser, item.ParentText);
            if (parent == null)
            {
                return;
            }
            parent.Replies.Add(new SNS_Manager._message { User = item.User, Message = item.Text });
            GameObject data = Camera.main.GetComponent<mainScript>().Data;
            data.GetComponent<SNS_Manager>().RenderMessages();
            data.GetComponent<Tabs_Manager>().GetTab(Tabs_Manager._tab._type.sns).Button.GetComponent<LeftBarButton>().WarningMark(show: true);
        }
    }
}
