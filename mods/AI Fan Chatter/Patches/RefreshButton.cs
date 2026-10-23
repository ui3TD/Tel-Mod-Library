using AICore;
using HarmonyLib;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AIFanChatter
{
    /// <summary>
    /// Adds the Refresh bar to the social media tab the first time the tab opens: a full-width button under
    /// the tab's title, with the posts and their scroll bar moved down to make room. When the tab doesn't
    /// have its title, post list and scroll bar, another mod may have changed it, and no bar is added.
    /// </summary>
    [HarmonyPatch(typeof(Tabs_Manager), nameof(Tabs_Manager.OpenTab))]
    public class Tabs_Manager_OpenTab
    {
        public static void Postfix(Tabs_Manager __instance, Tabs_Manager._tab._type Type)
        {
            try
            {
                if (Type == Tabs_Manager._tab._type.sns)
                {
                    RefreshButton.AddTo(__instance.GetTab(Type)?.Tab);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't add the Refresh bar: " + e.Message);
            }
        }
    }

    /// <summary>
    /// The social media tab's Refresh bar, with the mod's notices under it: both hidden while the mod is off
    /// (the posts move back up). The bar is faded, unclickable and reads "Refreshing..." while a request runs.
    /// </summary>
    internal class RefreshButton : MonoBehaviour
    {
        public const string ButtonName = "AIFanChatter_Refresh";
        public const string TitleName = "Title";
        public const string PostsName = "ScrollRect";
        public const string ScrollBarName = "Singles_Slider";
        public const string LabelKey = "AIFANCHATTER__SNS__REFRESH";
        public const string WaitingKey = "AIFANCHATTER__SNS__REFRESHING";
        public const string FallbackLabel = "Refresh feed";
        public const string FallbackWaiting = "Refreshing...";
        public const string PostsWaitingKey = "AIFANCHATTER__SNS__REFRESH_WAITING";
        public const string FallbackPostsWaiting = "Refresh feed ({count} waiting)";
        public const string RequestKey = "AIFANCHATTER__SNS__REFRESH_REQUEST";
        public const string FallbackRequest = "Refresh feed (AI request)";
        public const string FreeTooltipKey = "AIFANCHATTER__SNS__TOOLTIP_WAITING";
        public const string FallbackFreeTooltip = "Shows the next 5 waiting posts. No AI request.";
        public const string RequestTooltipKey = "AIFANCHATTER__SNS__TOOLTIP_REQUEST";
        public const string FallbackRequestTooltip = "Asks your AI service for new posts. Uses one request on your AI account, not counted toward requests per week.";
        /// <summary>The bar's height and the space under it, in the tab's units (the title is about 18 high).</summary>
        public const float Height = 22f;
        public const float Gap = 4f;
        /// <summary>The bar's space from the tab's sides, as the posts have on the left.</summary>
        public const float Side = 6f;
        /// <summary>How far the label sits above the bar's middle, for the picture's shadow along its bottom.</summary>
        public const float LabelRaise = 1.5f;
        /// <summary>The game's standard blue button picture, as on the Research tab's buttons.</summary>
        public const string ButtonSprite = "blue";

        private RectTransform posts;
        private RectTransform scrollBar;
        private TextMeshProUGUI label;
        private Button click;
        private ButtonDefault buttonDefault;
        private Image background;
        private TabNoticeArea notices;
        /// <summary>Where the posts and their scroll bar started, before the bar and notices moved them down.</summary>
        private float postsTop;
        private float scrollBarTop;
        private float drop;
        private int state = -1;

        private const int Off = 0, Refreshing = 1, Busy = 2, Ready = 3;

        public static void AddTo(GameObject tab)
        {
            if (tab == null || tab.transform.Find(ButtonName) != null)
            {
                return;
            }
            TMP_Text title = tab.transform.Find(TitleName)?.GetComponent<TMP_Text>();
            RectTransform posts = tab.transform.Find(PostsName) as RectTransform;
            RectTransform scrollBar = tab.transform.Find(ScrollBarName) as RectTransform;
            if (title == null || posts == null || scrollBar == null)
            {
                Log.Warn("The social media tab isn't laid out as expected; there's no Refresh bar.");
                return;
            }

            GameObject bar = new(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)bar.transform;
            rect.SetParent(tab.transform, false);
            rect.SetSiblingIndex(posts.GetSiblingIndex() + 1);
            // Across the tab, where the posts start now
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Side, posts.offsetMax.y - Height);
            rect.offsetMax = new Vector2(-Side, posts.offsetMax.y);

            Image image = bar.GetComponent<Image>();
            // Drawn as the game's own blue buttons are; without one loaded, a plain bar in the game's blue
            Image style = GameButton();
            if (style != null)
            {
                image.sprite = style.sprite;
                image.type = style.type;
                image.pixelsPerUnitMultiplier = style.pixelsPerUnitMultiplier;
            }
            Button button = bar.GetComponent<Button>();
            button.targetGraphic = image;
            // Its hover and press look too
            Button styleButton = style?.GetComponent<Button>();
            if (styleButton != null)
            {
                button.transition = styleButton.transition;
                button.colors = styleButton.colors;
                button.spriteState = styleButton.spriteState;
            }
            else
            {
                image.color = mainScript.blue32;
            }
            button.onClick.AddListener(FeedRefresh.Press);
            // The game's click sound, hover sound and pointer cursor
            ButtonDefault buttonDefault = bar.AddComponent<ButtonDefault>();

            GameObject text = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            // Centred on the picture's face, above its shadow
            textRect.offsetMin = new Vector2(0f, LabelRaise);
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = text.GetComponent<TextMeshProUGUI>();
            label.font = title.font;
            label.fontSharedMaterial = title.fontSharedMaterial;
            label.fontSize = title.fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;

            RefreshButton refresh = bar.AddComponent<RefreshButton>();
            refresh.posts = posts;
            refresh.scrollBar = scrollBar;
            refresh.label = label;
            refresh.click = button;
            refresh.buttonDefault = buttonDefault;
            refresh.background = image;
            refresh.postsTop = posts.offsetMax.y;
            refresh.scrollBarTop = scrollBar.offsetMax.y;
            refresh.notices = TabNoticeArea.Create((RectTransform)tab.transform, title);
            refresh.notices.transform.SetSiblingIndex(rect.GetSiblingIndex() + 1);
            refresh.Update();
        }

        /// <summary>A loaded game button drawn with the standard blue picture, or null.</summary>
        private static Image GameButton()
        {
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image != null && image.sprite != null && image.sprite.name == ButtonSprite && image.type == Image.Type.Sliced
                    && image.GetComponent<Button>() != null)
                {
                    return image;
                }
            }
            return null;
        }

        internal static string Text(string key, string fallback) =>
            Language.Data != null && Language.Data.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text) ? text : fallback;

        /// <summary>Moves the posts and their scroll bar down under the bar and the notices, or back up.</summary>
        private void MovePosts(float by)
        {
            if (Mathf.Approximately(by, drop))
            {
                return;
            }
            drop = by;
            posts.offsetMax = new Vector2(posts.offsetMax.x, postsTop - by);
            scrollBar.offsetMax = new Vector2(scrollBar.offsetMax.x, scrollBarTop - by);
        }

        private void Update()
        {
            try
            {
                // Off; waiting for the player's own refresh; another moment's request running with no posts to
                // show; ready. Waiting posts can always be shown: that needs no request.
                bool inFlight = Dispatcher.Instance.InFlight;
                int waiting = FeedQueue.PostCount;
                int now = !Settings.Enabled ? Off
                    : FeedRefresh.Waiting ? Refreshing
                    : inFlight && waiting == 0 ? Busy
                    : Ready;
                // Ready, it says whether a press is free (and how many posts are waiting) or asks the AI service
                string text = now == Refreshing ? Text(WaitingKey, FallbackWaiting)
                    : now != Ready ? Text(LabelKey, FallbackLabel)
                    : waiting > 0 ? Text(PostsWaitingKey, FallbackPostsWaiting).Replace("{count}", waiting.ToString())
                    : Text(RequestKey, FallbackRequest);
                // The notices, under the bar, and the posts under both
                Dispatcher dispatcher = Dispatcher.Instance;
                TabNotices.Sync(dispatcher.Suspended, dispatcher.Paused);
                notices.Place(postsTop - Height - Gap, Side, now != Off);
                MovePosts(now == Off ? 0f : Height + Gap + (notices.Height > 0f ? notices.Height + Gap : 0f));
                if (now == state && text == label.text)
                {
                    return;
                }
                state = now;
                // Without its image the bar takes no clicks
                background.enabled = now != Off;
                label.enabled = now != Off;
                // Unclickable while waiting, with the game's sounds and pointer cursor quiet too
                click.interactable = now == Ready;
                buttonDefault.Activate(now == Ready);
                label.text = text;
                label.alpha = now == Ready ? 1f : 0.5f;
                buttonDefault.SetTooltip(now != Ready ? "" : waiting > 0 ? Text(FreeTooltipKey, FallbackFreeTooltip) : Text(RequestTooltipKey, FallbackRequestTooltip));
            }
            catch (Exception e)
            {
                state = -1;
                enabled = false;
                Log.Warn("The Refresh bar stopped updating: " + e.Message);
            }
        }
    }

    /// <summary>
    /// What the Refresh bar does: shows the next queued posts at once, or with nothing queued, asks for
    /// everyday chatter and shows it as soon as it arrives. It doesn't count toward the request limits.
    /// </summary>
    internal static class FeedRefresh
    {
        /// <summary>The queued posts one press shows, each with its replies.</summary>
        public const int Posts = 5;
        public const string NoFansKey = "AIFANCHATTER__SNS__NOFANS";
        public const string FallbackNoFans = "Nobody is posting about the group yet. Win over some fans first.";

        private static readonly System.Random random = new();
        private static bool sent;

        /// <summary>Whether the player's own refresh is waiting for its reply.</summary>
        public static bool Waiting
        {
            get
            {
                // The request is over, answered or not
                if (sent && !Dispatcher.Instance.InFlight)
                {
                    sent = false;
                }
                return sent;
            }
        }

        public static void Press()
        {
            try
            {
                if (!Settings.Enabled)
                {
                    return;
                }
                if (FeedQueue.HasPosts)
                {
                    FeedRelease.ReleaseNow(FeedQueue.TakeNext(Posts));
                    return;
                }
                Dispatcher dispatcher = Dispatcher.Instance;
                if (dispatcher.TooFewFans)
                {
                    // Worded as the game words social media news, since it's about the agency, not the mod
                    TabNotices.Add(RefreshButton.Text(NoFansKey, FallbackNoFans), NoticeKind.Missed);
                    return;
                }
                // Stopped or paused: the notice saying so is already at the top of the tab
                if (dispatcher.Suspended || dispatcher.Paused)
                {
                    return;
                }
                string blocked = dispatcher.Blocked(manual: true);
                if (blocked != null)
                {
                    TabNotices.Add("No refresh: " + blocked + ".", NoticeKind.Missed);
                    return;
                }
                sent = dispatcher.Request(MomentContext.Chatter(random), manual: true);
            }
            catch (Exception e)
            {
                Log.Warn("Couldn't refresh the feed: " + e);
            }
        }
    }
}
