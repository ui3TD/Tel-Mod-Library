using SimpleJSON;
using System.Collections.Generic;
using System.Text;

namespace AIFanChatter
{
    /// <summary>
    /// The feed's history. The game keeps 20 posts; the mod keeps 100. A save holds the newest 20 where
    /// the game saves them and the older ones in a variable of the mod's own, so a save without the mod
    /// loads a normal 20-post feed and the game's limit of exactly 20 keeps working.
    /// </summary>
    public static class FeedStore
    {
        public const int VanillaCap = 20;
        public const int Cap = 100;

        public const string OlderPostsVar = Settings.Prefix + "FeedOlder";

        /// <summary>
        /// The feed's limit, for the transpiler on SNS_Manager.AddMessage.
        /// </summary>
        public static int CapValue() => Cap;

        /// <summary>
        /// Drops the oldest posts past the limit.
        /// </summary>
        public static void TrimToCap(List<SNS_Manager._message> feed)
        {
            if (feed != null && feed.Count > Cap)
            {
                feed.RemoveRange(0, feed.Count - Cap);
            }
        }

        /// <summary>
        /// Splits the feed a save is about to store: the newest 20, for the game's own field, and the rest.
        /// Null older posts when there is nothing to split.
        /// </summary>
        public static List<SNS_Manager._message> SplitForSave(List<SNS_Manager._message> saved, out List<SNS_Manager._message> older)
        {
            older = null;
            if (saved == null || saved.Count <= VanillaCap)
            {
                return saved;
            }
            int split = saved.Count - VanillaCap;
            older = saved.GetRange(0, split);
            return saved.GetRange(split, VanillaCap);
        }

        /// <summary>
        /// Puts the older posts back in front of a loaded feed and returns how many were added. Nothing is
        /// added when the loaded feed already holds more than 20 posts: something else restored the history.
        /// </summary>
        public static int MergeOnLoad(List<SNS_Manager._message> loaded, List<SNS_Manager._message> older)
        {
            if (loaded == null || older == null || older.Count == 0 || loaded.Count > VanillaCap)
            {
                return 0;
            }
            loaded.InsertRange(0, older);
            int before = loaded.Count;
            TrimToCap(loaded);
            return older.Count - (before - loaded.Count);
        }

        /// <summary>
        /// The post a late reply belongs to, newest first, by its fan and text; null when it's gone.
        /// </summary>
        public static SNS_Manager._message FindParent(List<SNS_Manager._message> feed, string user, string text)
        {
            if (feed == null)
            {
                return null;
            }
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                SNS_Manager._message post = feed[i];
                if (post != null && post.User == user && post.Message == text)
                {
                    return post;
                }
            }
            return null;
        }

        public static string Serialize(List<SNS_Manager._message> posts)
        {
            StringBuilder sb = new();
            sb.Append('[');
            for (int i = 0; i < posts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                Write(sb, posts[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, SNS_Manager._message post)
        {
            sb.Append("{\"chara\":").Append(Json.Quote(((int)(post?.Chara ?? SNS_Manager._chara.NONE)).ToString()))
                .Append(",\"user\":").Append(Json.Quote(post?.User ?? ""))
                .Append(",\"text\":").Append(Json.Quote(post?.Message ?? ""))
                .Append(",\"replies\":[");
            List<SNS_Manager._message> replies = post?.Replies;
            for (int i = 0; replies != null && i < replies.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                Write(sb, replies[i]);
            }
            sb.Append("]}");
        }

        /// <summary>
        /// The posts in text written by Serialize; an empty list when it's missing or broken.
        /// </summary>
        public static List<SNS_Manager._message> Deserialize(string text)
        {
            List<SNS_Manager._message> posts = new();
            if (Json.TryParse(text) is JSONArray items)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    SNS_Manager._message post = Read(items[i]);
                    if (post != null)
                    {
                        posts.Add(post);
                    }
                }
            }
            return posts;
        }

        private static SNS_Manager._message Read(JSONNode item)
        {
            if (item is not JSONClass)
            {
                return null;
            }
            SNS_Manager._message post = new()
            {
                User = Json.Str(item, "user"),
                Message = Json.Str(item, "text")
            };
            if (int.TryParse(Json.Str(item, "chara"), out int chara) && System.Enum.IsDefined(typeof(SNS_Manager._chara), chara))
            {
                post.Chara = (SNS_Manager._chara)chara;
            }
            JSONArray replies = Json.Arr(item, "replies");
            for (int i = 0; replies != null && i < replies.Count; i++)
            {
                SNS_Manager._message reply = Read(replies[i]);
                if (reply != null)
                {
                    post.Replies.Add(reply);
                }
            }
            return post;
        }

        /// <summary>
        /// Sets or, for null, removes a variable in a variables list, without variables.Set: that logs the
        /// value and raises the game's task events.
        /// </summary>
        public static void SetVariable(List<variables._variable> list, string name, string value)
        {
            if (list == null)
            {
                return;
            }
            if (value == null)
            {
                list.RemoveAll(v => v != null && v.name == name);
                return;
            }
            foreach (variables._variable variable in list)
            {
                if (variable != null && variable.name == name)
                {
                    variable.value = value;
                    return;
                }
            }
            list.Add(new variables._variable { name = name, value = value });
        }

        public static string GetVariable(List<variables._variable> list, string name)
        {
            if (list == null)
            {
                return null;
            }
            foreach (variables._variable variable in list)
            {
                if (variable != null && variable.name == name)
                {
                    return variable.value;
                }
            }
            return null;
        }
    }
}
