using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AIFanChatter
{
    /// <summary>
    /// Turns the model's text into feed posts. Models don't always follow the format exactly, so this
    /// takes the JSON out of any surrounding text or code fence, and replaces or cuts what doesn't fit.
    /// </summary>
    public static class PostParser
    {
        /// <summary>
        /// The most characters a post or reply keeps. The prompt asks for under Prompt.MaxLength, and models
        /// sometimes write a little more, which the feed has room for.
        /// </summary>
        public const int MaxShownLength = 200;

        /// <summary>
        /// The posts, or an empty list when there are none usable.
        /// </summary>
        public static List<SNS_Manager._message> Parse(string text, Random random)
        {
            List<SNS_Manager._message> posts = new();
            if (string.IsNullOrEmpty(text))
            {
                return posts;
            }
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return posts;
            }
            JSONArray items = Json.Arr(Json.TryParse(text.Substring(start, end - start + 1)), "posts");
            if (items == null)
            {
                return posts;
            }
            for (int i = 0; i < items.Count && posts.Count < Prompt.MaxPosts; i++)
            {
                SNS_Manager._message post = Message(items[i], random);
                if (post == null)
                {
                    continue;
                }
                JSONArray replies = Json.Arr(items[i], "replies");
                for (int j = 0; replies != null && j < replies.Count && post.Replies.Count < Prompt.MaxReplies; j++)
                {
                    SNS_Manager._message reply = Message(replies[j], random);
                    if (reply != null)
                    {
                        post.Replies.Add(reply);
                    }
                }
                posts.Add(post);
            }
            return posts;
        }

        private static SNS_Manager._message Message(JSONNode item, Random random)
        {
            string text = Clean(Json.Str(item, "text"));
            if (text == "")
            {
                return null;
            }
            string user = Json.Str(item, "user").Trim().ToUpperInvariant();
            if (!Prompt.FanCodes.Contains(user))
            {
                user = Prompt.FanCodes[random.Next(Prompt.FanCodes.Length)];
            }
            return new SNS_Manager._message { User = user, Message = ForFeed(text) };
        }

        /// <summary>
        /// Text the feed's font can show, on one line and cut to the length limit.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            StringBuilder sb = new(text.Length);
            foreach (char c in text)
            {
                // Emoji are surrogate pairs, often joined (U+200D) or styled (U+FE0F)
                if (char.IsSurrogate(c) || c == (char)0x200D || c == (char)0xFE0F)
                {
                    continue;
                }
                // The feed's font has ♪ and ★ but no heart; fans write <3 too
                if (c == (char)0x2764)
                {
                    sb.Append("<3");
                    continue;
                }
                sb.Append(char.IsControl(c) ? ' ' : c);
            }
            text = sb.ToString().Trim();
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }
            if (text.Length > MaxShownLength)
            {
                text = text.Substring(0, MaxShownLength - 3).TrimEnd() + "...";
            }
            return text;
        }

        /// <summary>
        /// The feed's text renders &lt;...&gt; as formatting; inside noparse it shows as typed, so a fan's
        /// "&lt;3" stays a heart and nothing can change the feed's look.
        /// </summary>
        public static string ForFeed(string text)
        {
            return "<noparse>" + Regex.Replace(text, "</?noparse>", "", RegexOptions.IgnoreCase) + "</noparse>";
        }
    }
}
