using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AIFanChatter
{
    /// <summary>
    /// The feed's recent posts for the prompt, so fans keep up with what was said and don't repeat it.
    /// </summary>
    public static class FeedHistory
    {
        public const int MaxPosts = 15;
        public const int MaxReplies = 3;
        public const int MaxText = 150;

        private const string NoparseOpen = "<noparse>";
        private const string NoparseClose = "</noparse>";

        /// <summary>
        /// The newest posts, oldest first, each with its first replies: "- F_HC_T: text" and
        /// "  - reply from M_C_A: text". "" when there are none.
        /// </summary>
        public static string Describe(IList<SNS_Manager._message> posts)
        {
            if (posts == null)
            {
                return "";
            }
            List<SNS_Manager._message> usable = posts.Where(p => p != null && Text(p.Message) != "").ToList();
            StringBuilder sb = new();
            foreach (SNS_Manager._message post in usable.Skip(Math.Max(0, usable.Count - MaxPosts)))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append("- ").Append(User(post.User)).Append(": ").Append(Text(post.Message));
                if (post.Replies == null)
                {
                    continue;
                }
                foreach (SNS_Manager._message reply in post.Replies.Where(r => r != null && Text(r.Message) != "").Take(MaxReplies))
                {
                    sb.Append("\n  - reply from ").Append(User(reply.User)).Append(": ").Append(Text(reply.Message));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// A post's text on one line, cut to the limit. The mod's own posts are wrapped in noparse and can hold
        /// "&lt;3", so they lose only the wrapper; the game's posts lose their colour tags.
        /// </summary>
        public static string Text(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }
            string text = message.Trim();
            if (text.StartsWith(NoparseOpen, StringComparison.OrdinalIgnoreCase) && text.EndsWith(NoparseClose, StringComparison.OrdinalIgnoreCase)
                && text.Length >= NoparseOpen.Length + NoparseClose.Length)
            {
                text = text.Substring(NoparseOpen.Length, text.Length - NoparseOpen.Length - NoparseClose.Length).Replace('\r', ' ').Replace('\n', ' ').Trim();
            }
            else
            {
                text = Moment.Clean(text);
            }
            return Members.Cut(text, MaxText);
        }

        private static string User(string user)
        {
            string code = Moment.Clean(user);
            return code == "" ? "fan" : code;
        }

        /// <summary>
        /// The queued posts as messages, each queued reply under its queued post. A reply whose post is
        /// already in the feed is left for the feed to show.
        /// </summary>
        public static List<SNS_Manager._message> Pending(IEnumerable<QueuedItem> items)
        {
            List<SNS_Manager._message> posts = new();
            if (items == null)
            {
                return posts;
            }
            foreach (QueuedItem item in items)
            {
                if (item == null)
                {
                    continue;
                }
                SNS_Manager._message message = new() { User = item.User, Message = item.Text };
                if (!item.IsReply)
                {
                    posts.Add(message);
                    continue;
                }
                FeedStore.FindParent(posts, item.ParentUser, item.ParentText)?.Replies.Add(message);
            }
            return posts;
        }
    }
}
