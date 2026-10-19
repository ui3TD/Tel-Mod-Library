using AICore;
using SimpleJSON;
using System.Linq;
using Xunit;

namespace AIFanChatter.Tests
{
    /// <summary>
    /// The reply schema the mod sends; the shared code places it in each service's request.
    /// </summary>
    public class ReplySchemaTests
    {
        private static string[] Values(JSONNode node)
        {
            JSONArray array = node.AsArray;
            return Enumerable.Range(0, array.Count).Select(i => array[i].Value).ToArray();
        }

        [Fact]
        public void ReplySchema_AllowsOnlyTheFeedsFanCodes()
        {
            JSONNode schema = JSON.Parse(Prompt.ReplySchema);
            JSONNode post = schema["properties"]["posts"]["items"];
            JSONNode reply = post["properties"]["replies"]["items"];
            foreach (JSONNode user in new[] { post["properties"]["user"], reply["properties"]["user"] })
            {
                Assert.Equal(Prompt.FanCodes, Values(user["enum"]));
            }
        }

        /// <summary>
        /// OpenAI's strict mode and Anthropic both need every object closed and every property required.
        /// </summary>
        [Fact]
        public void ReplySchema_ClosesEveryObject_AndRequiresEveryProperty()
        {
            int objects = 0;
            void Check(JSONNode node)
            {
                if (node is JSONClass obj)
                {
                    if (obj["type"].Value == "object")
                    {
                        objects++;
                        Assert.Equal("false", obj["additionalProperties"].Value);
                        string[] properties = ((JSONClass)obj["properties"]).Cast<System.Collections.Generic.KeyValuePair<string, JSONNode>>().Select(p => p.Key).ToArray();
                        Assert.Equal(properties, Values(obj["required"]));
                    }
                    foreach (System.Collections.Generic.KeyValuePair<string, JSONNode> member in obj)
                    {
                        Check(member.Value);
                    }
                }
            }
            Check(JSON.Parse(Prompt.ReplySchema));
            Assert.Equal(3, objects);
        }
    }

    public class PostParserTests
    {
        private static readonly System.Random Random = new(7);

        [Fact]
        public void ParsesPostsAndReplies()
        {
            var posts = PostParser.Parse(Replies.Posts, Random);
            Assert.Equal(3, posts.Count);
            Assert.Equal("F_HC_T", posts[0].User);
            Assert.Equal("<noparse>Best single yet!</noparse>", posts[0].Message);
            Assert.Equal("<noparse>Agreed</noparse>", posts[0].Replies.Single().Message);
            Assert.Empty(posts[1].Replies);
        }

        /// <summary>
        /// A reply from the post's own fan is dropped, and doesn't count towards the replies a post can have.
        /// </summary>
        [Fact]
        public void SelfReplies_AreDropped()
        {
            var posts = PostParser.Parse("{\"posts\":[{\"user\":\"F_HC_T\",\"text\":\"Best single yet!\",\"replies\":["
                + "{\"user\":\"F_HC_T\",\"text\":\"me again\"},{\"user\":\"M_C_A\",\"text\":\"one\"},{\"user\":\"f_hc_t\",\"text\":\"and again\"},"
                + "{\"user\":\"M_C_T\",\"text\":\"two\"},{\"user\":\"F_C_A\",\"text\":\"three\"}]}]}", Random);
            Assert.Equal(new[] { "<noparse>one</noparse>", "<noparse>two</noparse>", "<noparse>three</noparse>" },
                posts.Single().Replies.Select(r => r.Message));
        }

        [Fact]
        public void FindsTheJsonInsideProseAndCodeFences()
        {
            Assert.Equal(3, PostParser.Parse("Here you go:\n```json\n" + Replies.Posts + "\n```\nEnjoy!", Random).Count);
        }

        [Fact]
        public void UnknownFanCodes_BecomeValidOnes()
        {
            var posts = PostParser.Parse("{\"posts\":[{\"user\":\"superfan99\",\"text\":\"hi\"},{\"user\":\"f_hc_ya\",\"text\":\"yo\"}]}", Random);
            Assert.Contains(posts[0].User, Prompt.FanCodes);
            Assert.Equal("F_HC_YA", posts[1].User);
        }

        /// <summary>
        /// The feed's text renders tags; inside noparse they show as typed, and a closing noparse can't escape it.
        /// </summary>
        [Fact]
        public void AngleBrackets_ShowAsTyped()
        {
            string text = PostParser.Parse("{\"posts\":[{\"user\":\"M_C_T\",\"text\":\"<color=red>wow</color> <3 </noparse><b>\"}]}", Random)[0].Message;
            Assert.Equal("<noparse><color=red>wow</color> <3 <b></noparse>", text);
        }

        [Fact]
        public void CountsAndLengths_AreCapped()
        {
            string longText = new('a', 500);
            string replies = string.Join(",", Enumerable.Repeat("{\"user\":\"M_C_T\",\"text\":\"r\"}", 10));
            string post = "{\"user\":\"F_C_T\",\"text\":\"" + longText + "\",\"replies\":[" + replies + "]}";
            var posts = PostParser.Parse("{\"posts\":[" + string.Join(",", Enumerable.Repeat(post, 10)) + "]}", Random);
            Assert.Equal(Prompt.MaxPosts, posts.Count);
            Assert.Equal(Prompt.MaxReplies, posts[0].Replies.Count);
            Assert.Equal(PostParser.MaxShownLength, PostParser.Clean(longText).Length);
            Assert.Equal(150, PostParser.Clean(new string('b', 150)).Length);
            Assert.EndsWith("...", PostParser.Clean(longText));
            Assert.Equal(PostParser.ForFeed(PostParser.Clean(longText)), posts[0].Message);
        }

        /// <summary>
        /// The feed's font has no emoji and no heart (they'd show as boxes), but has ♪ and ★.
        /// </summary>
        [Fact]
        public void Emoji_AreRemoved()
        {
            Assert.Equal("so good! <3 ナナ♪★", PostParser.Clean("so good! 🎤❤️ ナナ♪★ 👩‍🎤"));
        }

        [Fact]
        public void LineBreaks_BecomeSpaces()
        {
            Assert.Equal("one two three", PostParser.Clean("one\ntwo\r\n\tthree"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Sorry, I can't help with that.")]
        [InlineData("{\"posts\":[]}")]
        [InlineData("{\"posts\":[{\"user\":\"M_C_T\",\"text\":\"\"}]}")]
        [InlineData("{\"posts\":\"none\"}")]
        [InlineData("{\"posts\":[{\"text\":\"broken \\u12")]
        [InlineData("}{")]
        public void UnusableReplies_GiveNoPosts(string text)
        {
            Assert.Empty(PostParser.Parse(text, Random));
        }
    }
}
