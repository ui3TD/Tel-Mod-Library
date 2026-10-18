using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AIFanChatter
{
    public sealed class QueuedItem
    {
        public int Batch;
        /// <summary>The batch's first post, the only one that shows the game's notification.</summary>
        public bool First;
        public DateTime Due;
        public string User = "";
        public string Text = "";
        /// <summary>For a reply, the post it answers, found in the feed by its fan and text.</summary>
        public bool IsReply;
        public string ParentUser = "";
        public string ParentText = "";
    }

    /// <summary>
    /// Posts and replies waiting for their in-game release time, so the feed fills up gradually. The mod
    /// sets the times, not the model. Saved in the save's variables, so a reload keeps them.
    /// </summary>
    public static class FeedQueue
    {
        public const string QueueVar = Settings.Prefix + "Queue";
        public const int MaxBatches = 3;
        /// <summary>The in-game hours after the moment that its posts and replies come in over.</summary>
        public const int WindowHours = 48;
        /// <summary>The most in-game hours a reply comes after its post.</summary>
        public const int MaxReplyDelayHours = 6;

        public static readonly List<QueuedItem> Items = new();
        private static int lastBatch;

        public static void Clear()
        {
            Items.Clear();
            lastBatch = 0;
        }

        /// <summary>
        /// Queues a batch over the 48 in-game hours after its moment: the hours are split into one stretch per
        /// post, in order, and each post comes at a random time in its stretch. Each reply comes with its post
        /// or up to 6 hours after it, within the 48 hours. Times already past when the reply arrives are due at
        /// once. The oldest batches still waiting are dropped past three.
        /// </summary>
        /// <param name="moment">When the moment happened: the request was sent then.</param>
        public static void AddBatch(List<SNS_Manager._message> posts, DateTime moment, Random random)
        {
            if (posts == null || posts.Count == 0)
            {
                return;
            }
            int batch = ++lastBatch;
            int stretch = WindowHours * 60 / posts.Count;
            DateTime end = moment.AddHours(WindowHours);
            for (int i = 0; i < posts.Count; i++)
            {
                SNS_Manager._message post = posts[i];
                DateTime due = moment.AddMinutes(i * stretch + random.Next(stretch));
                Items.Add(new QueuedItem { Batch = batch, First = i == 0, Due = due, User = post.User, Text = post.Message });
                foreach (SNS_Manager._message reply in post.Replies)
                {
                    // Half the replies come with the post, the rest trickle in after it
                    DateTime replyDue = random.Next(2) == 0 ? due : due.AddMinutes(random.Next(1, MaxReplyDelayHours * 60 + 1));
                    if (replyDue > end)
                    {
                        replyDue = end;
                    }
                    Items.Add(new QueuedItem
                    {
                        Batch = batch,
                        Due = replyDue,
                        User = reply.User,
                        Text = reply.Message,
                        IsReply = true,
                        ParentUser = post.User,
                        ParentText = post.Message
                    });
                }
            }

            List<int> batches = Items.Select(item => item.Batch).Distinct().OrderBy(b => b).ToList();
            for (int i = 0; i < batches.Count - MaxBatches; i++)
            {
                Items.RemoveAll(item => item.Batch == batches[i]);
            }
        }

        /// <summary>
        /// Takes every item that's due, in release order, so fast-forward doesn't leave posts trailing behind
        /// the clock.
        /// </summary>
        public static List<QueuedItem> TakeAllDue(DateTime now)
        {
            List<QueuedItem> due = new();
            QueuedItem item;
            while ((item = TakeDue(now)) != null)
            {
                due.Add(item);
            }
            return due;
        }

        /// <summary>
        /// Takes the item that is due first, if it's due: a post before its replies due at the same time.
        /// </summary>
        public static QueuedItem TakeDue(DateTime now)
        {
            QueuedItem next = null;
            foreach (QueuedItem item in Items)
            {
                if (item.Due > now)
                {
                    continue;
                }
                if (next == null || item.Due < next.Due || (item.Due == next.Due && !item.IsReply && next.IsReply))
                {
                    next = item;
                }
            }
            if (next != null)
            {
                Items.Remove(next);
            }
            return next;
        }

        public static string Serialize()
        {
            StringBuilder sb = new();
            sb.Append('[');
            for (int i = 0; i < Items.Count; i++)
            {
                QueuedItem item = Items[i];
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append("{\"batch\":").Append(Json.Quote(item.Batch.ToString(CultureInfo.InvariantCulture)))
                    .Append(",\"first\":").Append(Json.Quote(item.First ? "1" : "0"))
                    .Append(",\"due\":").Append(Json.Quote(item.Due.ToString("o", CultureInfo.InvariantCulture)))
                    .Append(",\"user\":").Append(Json.Quote(item.User))
                    .Append(",\"text\":").Append(Json.Quote(item.Text));
                if (item.IsReply)
                {
                    sb.Append(",\"parentUser\":").Append(Json.Quote(item.ParentUser))
                        .Append(",\"parentText\":").Append(Json.Quote(item.ParentText));
                }
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Replaces the queue with one written by Serialize. Broken items are skipped.
        /// </summary>
        public static void Load(string text)
        {
            Clear();
            if (Json.TryParse(text) is not JSONArray items)
            {
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                JSONNode node = items[i];
                if (!int.TryParse(Json.Str(node, "batch"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int batch)
                    || !DateTime.TryParse(Json.Str(node, "due"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime due)
                    || Json.Str(node, "text") == "")
                {
                    continue;
                }
                QueuedItem item = new()
                {
                    Batch = batch,
                    First = Json.Str(node, "first") == "1",
                    Due = due,
                    User = Json.Str(node, "user"),
                    Text = Json.Str(node, "text"),
                    ParentUser = Json.Str(node, "parentUser"),
                    ParentText = Json.Str(node, "parentText")
                };
                item.IsReply = item.ParentText != "";
                Items.Add(item);
                lastBatch = Math.Max(lastBatch, batch);
            }
        }
    }
}
