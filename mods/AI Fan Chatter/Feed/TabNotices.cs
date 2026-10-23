using AICore;
using System.Collections.Generic;
using System.Linq;

namespace AIFanChatter
{
    public sealed class TabNotice
    {
        public string Text = "";
        public NoticeKind Kind;
        /// <summary>Says requests are stopped or paused: it stays while they are, and can't be closed.</summary>
        public bool IsStatus => Kind == NoticeKind.Stopped || Kind == NoticeKind.Paused;
    }

    /// <summary>
    /// The mod's notices shown at the top of the social media tab: the one saying requests are stopped or
    /// paused, while they are, then the others, newest first, until the player closes them. A notice that's
    /// already shown moves to the top instead of showing twice. Nothing here is saved.
    /// </summary>
    public static class TabNotices
    {
        /// <summary>The most notices shown besides the stopped or paused one.</summary>
        public const int MaxOthers = 3;

        private static TabNotice status;
        private static readonly List<TabNotice> others = new();

        /// <summary>Goes up on every change, so the tab knows to draw the notices again.</summary>
        public static int Version { get; private set; }

        /// <summary>The notices in the order they're shown.</summary>
        public static List<TabNotice> Shown => (status == null ? others : new[] { status }.Concat(others)).ToList();

        /// <summary>
        /// Adds a notice, or moves the same one to the top. A connection that works has no notice here.
        /// Returns whether anything changed.
        /// </summary>
        public static bool Add(string text, NoticeKind kind)
        {
            if (kind == NoticeKind.Connected || string.IsNullOrEmpty(text))
            {
                return false;
            }
            TabNotice notice = new() { Text = text, Kind = kind };
            if (notice.IsStatus)
            {
                if (status != null && status.Text == text && status.Kind == kind)
                {
                    return false;
                }
                status = notice;
            }
            else
            {
                if (others.Count > 0 && others[0].Text == text)
                {
                    return false;
                }
                others.RemoveAll(n => n.Text == text);
                others.Insert(0, notice);
                if (others.Count > MaxOthers)
                {
                    others.RemoveRange(MaxOthers, others.Count - MaxOthers);
                }
            }
            Version++;
            return true;
        }

        public static void Close(TabNotice notice)
        {
            if (others.Remove(notice))
            {
                Version++;
            }
        }

        /// <summary>Drops the stopped or paused notice once requests aren't.</summary>
        public static void Sync(bool stopped, bool paused)
        {
            if (status != null && !(status.Kind == NoticeKind.Stopped ? stopped : paused))
            {
                status = null;
                Version++;
            }
        }

        /// <summary>
        /// A new game or a load starts without the earlier notices. The stopped or paused one stays while
        /// requests are, as they don't depend on the save.
        /// </summary>
        public static void Clear()
        {
            if (others.Count > 0)
            {
                others.Clear();
                Version++;
            }
        }

        /// <summary>For tests: nothing shown.</summary>
        internal static void Reset()
        {
            status = null;
            others.Clear();
            Version++;
        }
    }
}
