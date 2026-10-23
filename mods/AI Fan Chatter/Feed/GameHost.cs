using AICore;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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

        public IDictionary<string, int> FanOpinions
        {
            get
            {
                Dictionary<string, int> opinions = new();
                foreach (resources._fan fan in resources.Fans)
                {
                    try
                    {
                        opinions[Prompt.CodeFor(fan.gender, fan.hardcoreness, fan.age)] = Prompt.OpinionOutOf100(resources.GetFanOpinion(fan));
                    }
                    catch (Exception)
                    {
                        // That fan type goes without an opinion
                    }
                }
                return opinions;
            }
        }

        /// <summary>
        /// The fans of the idols who haven't graduated, as the game counts its total. Not resources.Fans:
        /// the game only refreshes its numbers when the fans tooltip opens.
        /// </summary>
        public IDictionary<string, long> FanCounts => CountFans(data_girls.girl);

        private static readonly resources.fanType[] Genders = { resources.fanType.male, resources.fanType.female };
        private static readonly resources.fanType[] Kinds = { resources.fanType.casual, resources.fanType.hardcore };
        private static readonly resources.fanType[] Ages = { resources.fanType.teen, resources.fanType.youngAdult, resources.fanType.adult };

        /// <summary>
        /// People per fan code, only the codes with any. A fan entry with a value the game doesn't use
        /// (another mod's) is left out, as it has no code.
        /// </summary>
        internal static Dictionary<string, long> CountFans(IEnumerable<data_girls.girls> girls)
        {
            Dictionary<string, long> counts = new();
            if (girls == null)
            {
                return counts;
            }
            foreach (data_girls.girls girl in girls)
            {
                try
                {
                    if (girl == null || girl.status == data_girls._status.graduated || girl.Fans == null)
                    {
                        continue;
                    }
                    foreach (resources._fan fan in girl.Fans)
                    {
                        if (fan == null || fan.people <= 0 || !Genders.Contains(fan.gender) || !Kinds.Contains(fan.hardcoreness) || !Ages.Contains(fan.age))
                        {
                            continue;
                        }
                        string code = Prompt.CodeFor(fan.gender, fan.hardcoreness, fan.age);
                        counts.TryGetValue(code, out long people);
                        counts[code] = people + fan.people;
                    }
                }
                catch (Exception)
                {
                    // That idol's fans are left out
                }
            }
            return counts;
        }

        /// <summary>
        /// Read fresh for every request: loading a save replaces the feed's list.
        /// </summary>
        public IList<SNS_Manager._message> RecentPosts
        {
            get
            {
                List<SNS_Manager._message> posts = new();
                if (SNS_Manager.Messages != null)
                {
                    posts.AddRange(SNS_Manager.Messages);
                }
                posts.AddRange(FeedHistory.Pending(FeedQueue.Items));
                return posts;
            }
        }

        /// <summary>
        /// Shows a notice at the top of the social media tab, with the tab's red dot when it's closed. The result
        /// of Apply also pops up, as the player is in Settings then; a connection that works only pops up.
        /// </summary>
        public void Notify(string text, NoticeKind kind, bool fromApply)
        {
            if (TabNotices.Add(text, kind))
            {
                MarkTab();
            }
            if (fromApply || kind == NoticeKind.Connected)
            {
                NotificationManager.AddNotification(kind == NoticeKind.Connected ? AIMod.Name + " connected: " + text : AIMod.Name + ": " + text,
                    kind == NoticeKind.Connected ? mainScript.green32 : mainScript.red32);
            }
        }

        /// <summary>The social media tab's red dot, unless the tab is open or there's no agency (the main menu).</summary>
        internal static void MarkTab()
        {
            try
            {
                if (mainScript.IsMainMenu() || Tabs_Manager.GetOpenTabType() == Tabs_Manager._tab._type.sns)
                {
                    return;
                }
                Camera.main.GetComponent<mainScript>().Data.GetComponent<Tabs_Manager>().GetTab(Tabs_Manager._tab._type.sns)
                    .Button.GetComponent<LeftBarButton>().WarningMark(show: true);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't mark the social media tab: " + e.Message);
            }
        }

        public void NextFrame(Action action)
        {
            // A sequence holding only a callback runs it on DOTween's next update, ignoring time scale. The
            // extension methods are called statically: the game's DOTween build lacks their markers.
            TweenSettingsExtensions.SetUpdate(TweenSettingsExtensions.AppendCallback(DOTween.Sequence(), () => action()), true);
        }

        public void Queue(List<SNS_Manager._message> posts, DateTime moment)
        {
            FeedQueue.AddBatch(posts, moment, random);
            // Posts whose time passed while the request ran appear now, even with the clock paused
            FeedRelease.OnTimeTick();
        }

        public void ShowNow(List<SNS_Manager._message> posts)
        {
            // Turned off while the request ran: dropped, as queued posts are
            if (!Settings.Enabled)
            {
                return;
            }
            List<QueuedItem> items = new();
            foreach (SNS_Manager._message post in posts)
            {
                items.Add(new QueuedItem { Due = staticVars.dateTime, User = post.User, Text = post.Message });
                items.AddRange(post.Replies.Select(reply => new QueuedItem
                {
                    Due = staticVars.dateTime, User = reply.User, Text = reply.Message, IsReply = true, ParentUser = post.User, ParentText = post.Message
                }));
            }
            FeedRelease.ReleaseNow(items);
        }
    }

    /// <summary>
    /// Puts queued posts and replies into the game's feed when they're due, or at once for the Refresh bar.
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
            // Nothing may escape into the game's clock tick, so every step catches its own errors
            List<QueuedItem> due;
            try
            {
                if (FeedQueue.Items.Count == 0 || !Settings.Enabled)
                {
                    return;
                }
                // A post comes before a reply due at the same time, so a reply can follow its post in the same tick
                due = FeedQueue.TakeAllDue(staticVars.dateTime);
            }
            catch (Exception e)
            {
                ReportTickError(e);
                return;
            }
            foreach (QueuedItem item in due)
            {
                try
                {
                    Release(item, quiet: !item.First);
                }
                catch (Exception e)
                {
                    ReportTickError(e);
                }
            }
        }

        /// <summary>
        /// Puts posts and replies into the feed at once, in order, without a notification: the player asked
        /// for them with Refresh. The feed scrolls to the top, where the new posts are.
        /// </summary>
        public static void ReleaseNow(List<QueuedItem> items)
        {
            foreach (QueuedItem item in items)
            {
                try
                {
                    Release(item, quiet: true);
                }
                catch (Exception e)
                {
                    ReportTickError(e);
                }
            }
            try
            {
                ScrollRect scroll = Camera.main.GetComponent<mainScript>().Data.GetComponent<SNS_Manager>().Container.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1f;
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't scroll the feed to the new posts: " + e.Message);
            }
        }

        private static void ReportTickError(Exception e)
        {
            if (!tickErrorLogged)
            {
                tickErrorLogged = true;
                Log.Warn("Couldn't add a queued post: " + e);
            }
        }

        private static void Release(QueuedItem item, bool quiet)
        {
            if (!item.IsReply)
            {
                // Through the game's own method, so other mods' patches on it see every post
                Quiet = quiet;
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
