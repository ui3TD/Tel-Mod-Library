using HarmonyLib;
using Michsky.UI.ModernUIPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InGameTests.TelMods
{
    /// <summary>
    /// AI Fan Chatter in the real game: the key rows in ModMenus' real menu, a request through the real
    /// transport and clock into the real feed, and the feed and queue across a real save and load. The
    /// unit tests cover the settings, requests, replies, limits and queue timing on stand-ins.
    /// Requests go to a server on this PC; nothing reaches an AI service, and the player's own key is
    /// never touched.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class AIFanChatterTests
    {
        private const string HarmonyId = "com.tel.aifanchatter";
        private const string ModMenusId = "com.tel.modmenus";
        private const PopupManager._type ModMenuPopup = (PopupManager._type)999;
        private const string Prefix = "AIFanChatter_";
        private const string KeyVar = Prefix + "APIKey";

        /// <summary>
        /// A key typed into the picked service's secret row in ModMenus' real menu goes on Apply to the key file
        /// the mod reads, never to the save. The reopened field is empty, with the key's ending under it; a
        /// later Apply with the field left empty keeps the key, and Remove then Apply removes it. ModMenus' own
        /// test checks that secret fields show asterisks.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator KeyIsKeptOutOfTheSave(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            if (!ModTest.IsActive(ModMenusId))
            {
                ctx.Note("ModMenus isn't enabled; skipped");
                yield break;
            }
            yield return Game.CloseAllPopups(ctx);

            using (TempKeyFolder(mod, out _))
            using (Game.Variable(Prefix + "Enabled", "0"))
            using (Game.Variable(Prefix + "Provider", "1"))
            using (RestoreVariables())
            {
                const string typed = "sk-ingame-test-WXYZ";
                Component[] rows = null;
                yield return OpenMenu(ctx, popup => rows = popup.GetComponentsInChildren<Transform>(true).Select(t => (Component)t).ToArray());
                // OpenAI's row: Unity's Find would read a '/' in the name as a path, so the row has '\'
                const string rowName = "ModMenuSecret_Tel AI Mods\\openai";
                Transform row = rows?.OfType<Transform>().FirstOrDefault(t => t.name == rowName);
                if (row == null)
                {
                    ctx.Fail("The mod menu has no row " + rowName);
                    yield return Game.CloseAllPopups(ctx);
                    yield break;
                }
                ctx.Assert(row.gameObject.activeSelf, "OpenAI's key row is hidden with OpenAI picked");
                TMP_InputField field = row.GetComponentInChildren<TMP_InputField>(true);
                TextMeshProUGUI status = row.Find("Status").GetComponent<TextMeshProUGUI>();

                // Typing goes through the field's own event, as a player's typing does
                field.text = typed;
                yield return Apply(ctx, row);
                ctx.Assert(!variables.variable.Any(v => v.value != null && v.value.Contains(typed)), "Apply put the key in the save");
                // The key file the mod reads for OpenAI, which the mod reads back
                Type store = mod.GetType("AICore.KeyStore", true);
                Type provider = mod.GetType("AICore.Provider", true);
                object openAI = Enum.Parse(provider, "OpenAI");
                string file = (string)AccessTools.Method(store, "FileFor").Invoke(null, new[] { openAI });
                ctx.Assert(File.Exists(file) && File.ReadAllText(file) == typed, "Apply didn't write the key file the mod reads");
                ctx.Assert((string)AccessTools.Method(store, "Get").Invoke(null, new[] { openAI }) == typed, "The mod doesn't read the key ModMenus saved");

                // Reopened: empty, masked, with the key's ending under it; applied empty, the key is kept
                yield return OpenMenu(ctx, _ => { });
                ctx.Assert(field.text == "", "The reopened field holds " + field.text.Length + " characters");
                ctx.Assert(field.contentType == TMP_InputField.ContentType.Password, "The key field isn't masked");
                ctx.Assert(status.text.Contains("WXYZ"), "The line under the field says '" + status.text + "'");
                yield return Apply(ctx, row);
                ctx.Assert(File.Exists(file) && File.ReadAllText(file) == typed, "Applying the empty field changed the key");

                // Remove, then Apply: the key is gone
                yield return OpenMenu(ctx, _ => { });
                row.Find("Remove").GetComponent<Button>().onClick.Invoke();
                yield return Apply(ctx, row);
                ctx.Assert(!File.Exists(file), "Remove didn't remove the key file");
                yield return OpenMenu(ctx, _ => { });
                ctx.Assert(!row.Find("Remove").gameObject.activeSelf, "Remove still shows after the key was removed");
                Game.Main.Data.GetComponent<PopupManager>().Close();
            }
            yield return Game.CloseAllPopups(ctx);
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
        }

        /// <summary>
        /// In ModMenus' real menu, the Base URL row follows the AI service the player picks, before Apply: the
        /// mod reads the pick with ModMenus' real MenuValue, found by name. The unit tests use a stand-in for it.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator BaseUrlRow_FollowsTheProviderPick(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            if (!ModTest.IsActive(ModMenusId))
            {
                ctx.Note("ModMenus isn't enabled; skipped");
                yield break;
            }
            yield return Game.CloseAllPopups(ctx);

            // The mod is off, so nothing is sent; the Base URL row doesn't depend on it
            using (TempKeyFolder(mod, out _))
            using (Game.Variable(Prefix + "Enabled", "0"))
            using (Game.Variable(Prefix + "Provider", "2"))
            using (RestoreVariables())
            {
                GameObject popup = null;
                yield return OpenMenu(ctx, found => popup = found);
                Transform[] rows = popup != null ? popup.GetComponentsInChildren<Transform>(true) : new Transform[0];
                Transform baseUrl = rows.FirstOrDefault(t => t.name == "ModMenuInput_" + Prefix + "BaseURL");
                Transform provider = rows.FirstOrDefault(t => t.name == "ModMenuDropdown_" + Prefix + "Provider");
                CustomDropdown dropdown = provider != null ? provider.GetComponentInChildren<CustomDropdown>(true) : null;
                if (baseUrl == null || dropdown == null)
                {
                    ctx.Fail("The mod menu has no Base URL row or AI service dropdown");
                    yield return Game.CloseAllPopups(ctx);
                    yield break;
                }

                // The player shows the mod's tab, as a click on it does
                Assembly modMenus = TelMod.ByHarmonyId(ModMenusId).Assembly();
                Type tabType = modMenus.GetType("ModMenus.ModMenusUtils+ModMenuTab", true);
                Component tab = popup.GetComponentsInChildren(tabType, true)
                    .FirstOrDefault(t => AccessTools.Field(tabType, "page").GetValue(t) is GameObject page && provider.IsChildOf(page.transform));
                if (tab == null)
                {
                    ctx.Fail("The mod menu has no AI Fan Chatter tab");
                    yield return Game.CloseAllPopups(ctx);
                    yield break;
                }
                AccessTools.Method(tabType, "OnClick").Invoke(tab, null);
                // The game's dropdown sets itself up in its first frame shown, and opens its list only after that
                yield return TestTools.WaitFor(ctx, () => dropdown.gameObject.activeInHierarchy, 2f, "the AI service dropdown to show");
                yield return null;
                if (!dropdown.gameObject.activeInHierarchy)
                {
                    Type managerType = modMenus.GetType("ModMenus.ModMenusUtils+ModMenuManager", true);
                    Component manager = popup.GetComponentInChildren(managerType, true);
                    Component selected = (Component)AccessTools.Field(managerType, "selected").GetValue(manager);
                    ctx.Fail("The AI service dropdown isn't shown after its tab was clicked: popup active " + popup.activeInHierarchy
                        + ", page active " + provider.parent.gameObject.activeSelf + ", row active " + provider.gameObject.activeSelf
                        + ", tab shown " + (selected == null ? "none" : selected.name) + ", open popup " + PopupManager.GetOpenPopupType());
                    yield return Game.CloseAllPopups(ctx);
                    yield break;
                }
                ctx.Assert(baseUrl.gameObject.activeSelf, "The Base URL row is hidden with OpenAI-compatible saved");

                // Picks in the open list, as the player's clicks make them; nothing is applied
                IEnumerator Pick(int item)
                {
                    dropdown.Animate();
                    yield return new WaitForSecondsRealtime(0.5f);
                    dropdown.GetButton(item).GetComponent<Button>().onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return Pick(1);
                ctx.Assert(!baseUrl.gameObject.activeSelf, "Picking OpenAI didn't hide the Base URL row before Apply");
                yield return Pick(2);
                ctx.Assert(baseUrl.gameObject.activeSelf, "Picking OpenAI-compatible didn't show the Base URL row before Apply");
                Game.Main.Data.GetComponent<PopupManager>().Close();
            }
            yield return Game.CloseAllPopups(ctx);
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
        }

        /// <summary>
        /// The Refresh bar on the real social media tab, in every state the player can see: across the tab
        /// under its title and above the posts, drawn and tinted as the game's blue buttons are, and reached
        /// by a click. A click with posts queued shows the next 5 with their replies and scrolls up to them;
        /// with nothing queued, it sends a request and shows the posts when the reply comes, reading
        /// Refreshing... and taking no clicks meanwhile. While another moment's request runs it's faded with
        /// nothing queued and clickable with posts queued. Turned off, it hides, the posts move up, and a
        /// waiting refresh's posts are dropped. Screenshots go to %TEMP%\AIFanChatterRefreshScreens.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator RefreshButton_ShowsQueuedPostsAndAsksForMore(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            const string reply = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"posts\\\":[" +
                "{\\\"user\\\":\\\"F_HC_T\\\",\\\"text\\\":\\\"refresh one\\\",\\\"replies\\\":[{\\\"user\\\":\\\"M_C_A\\\",\\\"text\\\":\\\"refresh reply\\\"}]}," +
                "{\\\"user\\\":\\\"M_C_YA\\\",\\\"text\\\":\\\"refresh two\\\"}]}\"},\"finish_reason\":\"stop\"}]}";
            string shots = Path.Combine(Path.GetTempPath(), "AIFanChatterRefreshScreens");
            Directory.CreateDirectory(shots);

            List<SNS_Manager._message> before = SNS_Manager.Messages.ToList();
            Type queueType = mod.GetType("AIFanChatter.FeedQueue", true);
            IList queue = (IList)AccessTools.Field(queueType, "Items").GetValue(null);
            using (LocalServer server = new(reply))
            using (TempKeyFolder(mod, out _))
            using (Game.Variable(Prefix + "Provider", "2"))
            using (Game.Variable(Prefix + "BaseURL", server.Url))
            using (Game.Variable(Prefix + "Model", "test-model"))
            using (Game.Variable(Prefix + "PerWeek", "7"))
            using (Game.Variable(Prefix + "Enabled", "1"))
            using (TestTools.Restore(() => RestoreFeed(mod, before)))
            using (TestTools.Restore(() => Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed)))
            using (TestTools.Restore(() => ResetRequestLimits(mod)))
            {
                yield return TestTools.WaitFor(ctx, () => server.Requests >= 1, 10f, "the connection test");
                yield return new WaitForSecondsRealtime(1f);
                AccessTools.Method(queueType, "Clear").Invoke(null, null);

                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.sns);
                // The tab fades in over 0.3 seconds
                yield return new WaitForSecondsRealtime(0.6f);
                Transform tab = Tabs_Manager.GetTabObject(Tabs_Manager._tab._type.sns).transform;
                Transform button = tab.Find("AIFanChatter_Refresh");
                if (button == null)
                {
                    ctx.Fail("The social media tab has no Refresh button");
                    yield break;
                }
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                // Nothing queued: a press asks the AI service, and the bar says so
                FieldInfo tooltipField = AccessTools.Field(typeof(ButtonDefault), "tooltipText");
                string Tooltip() => (string)tooltipField.GetValue(button.GetComponent<ButtonDefault>()) ?? "";
                ctx.Assert(label != null && label.text == "Refresh feed (AI request)", "With nothing queued the bar reads " + (label == null ? "nothing" : "\"" + label.text + "\""));
                ctx.Assert(Tooltip().StartsWith("Asks your AI service"), "With nothing queued the tooltip reads \"" + Tooltip() + "\"");
                ctx.Assert(label == null || label.textInfo.characterCount == label.text.Length && !label.isTextOverflowing, "The bar's label doesn't fit");
                ctx.Assert(button.GetComponent<Button>().interactable, "The bar can't be clicked");
                Image barImage = button.GetComponent<Image>();
                ctx.Assert(barImage.sprite != null && barImage.sprite.name == "blue" && barImage.type == Image.Type.Sliced, "The bar isn't drawn as the game's blue buttons are");

                RectTransform postsRect = (RectTransform)tab.Find("ScrollRect");
                Rect own = ScreenRect(button), title = ScreenRect(tab.Find("Title")), posts = ScreenRect(postsRect),
                    scrollBar = ScreenRect(tab.Find("Singles_Slider")), panel = ScreenRect(tab);
                ctx.Record("barRect", own);
                ctx.Assert(own.yMax <= title.yMin + 1f, "The bar reaches into the title: " + own + " and " + title);
                ctx.Assert(own.yMin >= posts.yMax && own.yMin >= scrollBar.yMax, "The bar covers the posts or their scroll bar: " + own + ", " + posts + ", " + scrollBar);
                ctx.Assert(own.xMin >= panel.xMin && own.xMax <= panel.xMax && own.width >= panel.width * 0.9f, "The bar isn't across the tab: " + own + " in " + panel);
                GameObject hit = TopHit(own.center);
                ctx.Assert(hit != null && hit.transform.IsChildOf(button), "A click on the bar reaches " + (hit == null ? "nothing" : hit.name));
                yield return Screenshot(Path.Combine(shots, "1-refresh-button.png"));

                // The longest count a player could see still fits; the bar puts its own text back next frame
                label.text = "Refresh feed (999 waiting)";
                label.ForceMeshUpdate();
                ctx.Assert(!label.isTextOverflowing && label.preferredWidth <= ((RectTransform)label.transform).rect.width, "\"Refresh feed (999 waiting)\" doesn't fit the bar");
                yield return null;
                ctx.Assert(label.text == "Refresh feed (AI request)", "The bar didn't put its label back: \"" + label.text + "\"");

                // Hovered and pressed, as the game's blue buttons look
                Button barButton = button.GetComponent<Button>();
                ctx.Record("transition", barButton.transition + ", hover " + (Color32)barButton.colors.highlightedColor + ", pressed " + (Color32)barButton.colors.pressedColor
                    + ", hover sprite " + (barButton.spriteState.highlightedSprite == null ? "none" : barButton.spriteState.highlightedSprite.name));
                Color normalTint = barImage.canvasRenderer.GetColor();
                var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(0.3f);
                Color hoverTint = barImage.canvasRenderer.GetColor();
                Sprite hoverSprite = barImage.overrideSprite;
                yield return Screenshot(Path.Combine(shots, "1b-hover.png"));
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.3f);
                Color pressedTint = barImage.canvasRenderer.GetColor();
                yield return Screenshot(Path.Combine(shots, "1c-pressed.png"));
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(0.3f);
                ctx.Record("tints", "normal " + (Color32)normalTint + ", hover " + (Color32)hoverTint + ", pressed " + (Color32)pressedTint);
                ctx.Assert(hoverTint != normalTint || hoverSprite != barImage.sprite, "Hovering doesn't change the bar");
                ctx.Assert(pressedTint != hoverTint, "Pressing doesn't change the bar");

                Button barButton2 = button.GetComponent<Button>();
                ButtonDefault barDefault = button.GetComponent<ButtonDefault>();
                ScrollRect scroll = postsRect.GetComponent<ScrollRect>();
                bool Ready() => label.text.StartsWith("Refresh feed (") && barButton2.interactable && barDefault.active && label.alpha == 1f;
                string Looks() => "\"" + label.text + "\", " + (barButton2.interactable ? "clickable" : "not clickable") + ", sounds " + (barDefault.active ? "on" : "off") + ", label alpha " + label.alpha;
                List<SNS_Manager._message> Batch(string tag, int posts) => Enumerable.Range(1, posts).Select(i => new SNS_Manager._message
                {
                    User = "F_C_T",
                    Message = "<noparse>" + tag + " " + i + "</noparse>",
                    Replies = new List<SNS_Manager._message> { new() { User = "M_HC_A", Message = "<noparse>reply to " + tag + " " + i + "</noparse>" } }
                }).ToList();
                void Queue(List<SNS_Manager._message> batch) =>
                    AccessTools.Method(queueType, "AddBatch").Invoke(null, new object[] { batch, staticVars.dateTime.AddDays(10), new System.Random(1) });
                object dispatcher = AccessTools.Property(mod.GetType("AIFanChatter.Dispatcher", true), "Instance").GetValue(null, null);
                bool InFlight() => (bool)AccessTools.Property(dispatcher.GetType(), "InFlight").GetValue(dispatcher, null);

                // Enough posts to scroll, scrolled to the bottom
                for (int i = 0; i < 15; i++)
                    SNS_Manager.Messages.Add(new SNS_Manager._message { User = "M_C_T", Message = "<noparse>older " + i + "</noparse>" });
                Game.Main.Data.GetComponent<SNS_Manager>().RenderMessages();
                yield return null;
                scroll.verticalNormalizedPosition = 0f;
                yield return null;

                // 7 posts queued for days from now: a click shows the first 5 with their replies, and the feed scrolls up to them
                Queue(Batch("queued", 7));
                yield return null;
                ctx.Assert(label.text == "Refresh feed (7 waiting)" && Ready(), "With 7 posts queued the bar is " + Looks());
                ctx.Assert(Tooltip().StartsWith("Shows the next 5 waiting posts"), "With posts queued the tooltip reads \"" + Tooltip() + "\"");
                yield return Screenshot(Path.Combine(shots, "1d-posts-waiting.png"));
                DateTime clock = staticVars.dateTime;
                int feedBefore = SNS_Manager.Messages.Count;
                Click(button.gameObject);
                yield return null;
                List<string> shown = SNS_Manager.Messages.Skip(feedBefore).Select(m => m.Message).ToList();
                ctx.Record("shownFromQueue", shown.Count);
                ctx.Assert(shown.SequenceEqual(Enumerable.Range(1, 5).Select(i => "<noparse>queued " + i + "</noparse>")), "The click showed " + string.Join(", ", shown.ToArray()));
                ctx.Assert(SNS_Manager.Messages.Skip(feedBefore).All(m => m.Replies.Count == 1), "A shown post is missing its reply");
                ctx.Assert(queue.Count == 4, "The queue holds " + queue.Count + " items, not the last 2 posts and their replies");
                yield return null;
                ctx.Assert(label.text == "Refresh feed (2 waiting)", "After showing 5 of 7 the bar reads \"" + label.text + "\"");
                ctx.Assert(server.Received == 1, "The click sent a request although posts were queued");
                ctx.Assert(scroll.verticalNormalizedPosition > 0.99f, "The feed didn't scroll up to the new posts: " + scroll.verticalNormalizedPosition);

                // Nothing queued: a click asks for more. While it waits, the bar reads Refreshing..., is faded,
                // and a click does nothing; the posts appear when the reply comes, at the top
                AccessTools.Method(queueType, "Clear").Invoke(null, null);
                feedBefore = SNS_Manager.Messages.Count;
                server.DelayMs = 1500;
                int receivedBefore = server.Received;
                Click(button.gameObject);
                yield return TestTools.WaitFor(ctx, () => server.Received > receivedBefore, 5f, "the refresh request");
                yield return null;
                ctx.Assert(label.text == "Refreshing..." && !barButton2.interactable && !barDefault.active && label.alpha < 1f, "While waiting the bar is " + Looks());
                int requestsWaiting = server.Received;
                Click(button.gameObject);
                yield return Screenshot(Path.Combine(shots, "2-refreshing.png"));
                scroll.verticalNormalizedPosition = 0f;
                yield return TestTools.WaitFor(ctx, () => SNS_Manager.Messages.Count >= feedBefore + 2, 15f, "the refreshed posts");
                yield return null;
                ctx.Assert(Ready(), "After the reply the bar is " + Looks());
                ctx.Assert(server.Received == requestsWaiting, "A click while waiting sent another request");
                SNS_Manager._message one = SNS_Manager.Messages.LastOrDefault(m => m.Message == "<noparse>refresh one</noparse>");
                ctx.Assert(one != null && SNS_Manager.Messages.Any(m => m.Message == "<noparse>refresh two</noparse>"), "The refreshed posts aren't in the feed");
                ctx.Assert(one != null && one.Replies.Any(r => r.Message == "<noparse>refresh reply</noparse>"), "The refreshed reply isn't under its post");
                ctx.Assert(queue.Count == 0, "The refreshed posts went to the queue: " + queue.Count);
                ctx.Assert(scroll.verticalNormalizedPosition > 0.99f, "The feed didn't scroll up to the refreshed posts: " + scroll.verticalNormalizedPosition);
                ctx.Assert(staticVars.dateTime == clock, "The clock ran during the test");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Screenshot(Path.Combine(shots, "3-after-refresh.png"));

                // Another moment's request running: with nothing queued the bar is faded and still reads Refresh feed;
                // once posts are queued it can show them, without waiting for that request
                object moment = Activator.CreateInstance(mod.GetType("AIFanChatter.Moment", true), "an in-game test");
                bool sent = (bool)AccessTools.Method(dispatcher.GetType(), "Request").Invoke(dispatcher, new[] { moment, false });
                ctx.Assert(sent, "The other moment's request wasn't sent: " + AccessTools.Method(dispatcher.GetType(), "Blocked").Invoke(dispatcher, new object[] { false }));
                yield return null;
                yield return null;
                ctx.Assert(label.text == "Refresh feed" && !barButton2.interactable && !barDefault.active && label.alpha < 1f, "During another request with nothing queued the bar is " + Looks());
                yield return Screenshot(Path.Combine(shots, "4-other-request.png"));
                Queue(Batch("waiting", 2));
                yield return null;
                ctx.Assert(Ready(), "During another request with posts queued the bar is " + Looks());
                feedBefore = SNS_Manager.Messages.Count;
                Click(button.gameObject);
                yield return null;
                ctx.Assert(SNS_Manager.Messages.Count == feedBefore + 2, "During another request a click showed " + (SNS_Manager.Messages.Count - feedBefore) + " queued posts, not 2");
                yield return TestTools.WaitFor(ctx, () => !InFlight(), 15f, "the other request to finish");
                yield return null;
                ctx.Assert(Ready(), "After the other request the bar is " + Looks());
                AccessTools.Method(queueType, "Clear").Invoke(null, null);

                // Turned off while a refresh waits: its posts are dropped
                feedBefore = SNS_Manager.Messages.Count;
                Click(button.gameObject);
                yield return null;
                using (Game.Variable(Prefix + "Enabled", "0"))
                {
                    yield return TestTools.WaitFor(ctx, () => !InFlight(), 15f, "the refresh to finish");
                    yield return null;
                    ctx.Assert(SNS_Manager.Messages.Count == feedBefore, "With the mod off the refresh still showed " + (SNS_Manager.Messages.Count - feedBefore) + " posts");

                    // Off: the bar is gone and the posts start where it was
                    Rect postsOff = ScreenRect(postsRect);
                    ctx.Assert(!button.GetComponent<Image>().enabled && !label.enabled, "The bar still shows with the mod off");
                    ctx.Assert(postsOff.yMax >= own.yMax - 1f, "With the mod off the posts start at " + postsOff.yMax + ", below where the bar was (" + own.yMax + ")");
                    // Turning it back on tests the connection; answered at once, so it's over before the server stops
                    server.DelayMs = 0;
                }
                yield return null;
                yield return null;
                ctx.Assert(Math.Abs(ScreenRect(postsRect).yMax - posts.yMax) < 0.5f, "Turned back on, the posts didn't move back under the bar");
                yield return new WaitForSecondsRealtime(1f);
                ctx.Record("requests", server.Requests);
                ctx.Record("screenshots", shots);
            }
        }

        /// <summary>
        /// The mod's notices at the top of the real social media tab, through the mod's own Notify: a notice
        /// gives the closed tab its red dot and doesn't pop up; in the tab, the stopped notice comes first, in
        /// red, without a close button, then the others, newest first, a repeat moved to the top. The cards
        /// sit between the Refresh bar and the posts, which move down under them, and the longest text fits
        /// its card. Closing one moves the posts back up by its height; the stopped notice goes once requests
        /// run again. Only the result of Apply also pops up. Screenshots go to %TEMP%\AIFanChatterRefreshScreens.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator Notices_ShowAtTheTopOfTheTab(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);
            NotificationsContainer popups = UnityEngine.Object.FindObjectOfType<NotificationsContainer>();
            if (popups == null)
            {
                ctx.Fail("The game has no notification panel");
                yield break;
            }

            const string reply = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}";
            string shots = Path.Combine(Path.GetTempPath(), "AIFanChatterRefreshScreens");
            Directory.CreateDirectory(shots);
            Type noticesType = mod.GetType("AIFanChatter.TabNotices", true);
            Type kinds = mod.GetType("AICore.NoticeKind", true);
            object host = Activator.CreateInstance(mod.GetType("AIFanChatter.GameHost", true), true);
            MethodInfo notify = AccessTools.Method(host.GetType(), "Notify");
            void Notify(string text, string kind, bool fromApply) => notify.Invoke(host, new[] { text, Enum.Parse(kinds, kind), fromApply });
            void ResetNotices() => AccessTools.Method(noticesType, "Reset").Invoke(null, null);
            object dispatcher = AccessTools.Property(mod.GetType("AIFanChatter.Dispatcher", true), "Instance").GetValue(null, null);
            object client = AccessTools.Field(dispatcher.GetType(), "client").GetValue(dispatcher);
            void Stopped(bool stopped) => AccessTools.Field(client.GetType(), "suspended").SetValue(client, stopped);
            GameObject redDot = Game.Main.Data.GetComponent<Tabs_Manager>().GetTab(Tabs_Manager._tab._type.sns).Button.GetComponent<LeftBarButton>().warningMark;

            const string stoppedText = "openrouter.ai rejected the API key (HTTP 401: invalid key). Requests stopped: fix the key, then press Apply in Mod Settings.";
            string longText = "No fan posts for the event Producer caught dating an idol: deepseek/deepseek-v4-pro-0813 on openrouter.ai's provider failed ("
                + string.Concat(Enumerable.Repeat("The upstream provider returned an error. ", 4)).Substring(0, 100) + "…). Other moments still get posts.";
            const string shortText = "No fan posts for the single Midnight Platform: the model declined. Other moments still get posts.";

            using (LocalServer server = new(reply))
            using (TempKeyFolder(mod, out _))
            using (Game.Variable(Prefix + "Provider", "2"))
            using (Game.Variable(Prefix + "BaseURL", server.Url))
            using (Game.Variable(Prefix + "Model", "test-model"))
            using (Game.Variable(Prefix + "Enabled", "1"))
            using (TestTools.Restore(() => { ResetNotices(); Stopped(false); if (redDot != null) redDot.SetActive(false); }))
            using (TestTools.Restore(() => Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed)))
            {
                yield return TestTools.WaitFor(ctx, () => server.Requests >= 1, 10f, "the connection test");
                yield return new WaitForSecondsRealtime(1f);
                ResetNotices();
                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
                yield return null;
                if (redDot != null)
                    redDot.SetActive(false);
                // Pop-ups are told apart by their text: the connection test's own may still be on screen
                bool PoppedUp(string text) => popups.GetComponentsInChildren<Notification>(true)
                    .Any(n => n.TextObj != null && n.TextObj.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains(text)));

                // With the tab closed: the red dot, and no pop-up
                Stopped(true);
                Notify(stoppedText, "Stopped", false);
                ctx.Assert(redDot == null || redDot.activeSelf, "A notice didn't give the social media tab its red dot");
                Notify(shortText, "Missed", false);
                Notify(longText, "Missed", false);
                Notify(shortText, "Missed", false);
                yield return new WaitForSecondsRealtime(0.5f);
                ctx.Assert(!PoppedUp(stoppedText) && !PoppedUp(shortText) && !PoppedUp("deepseek"), "A notice popped up");

                Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.sns);
                yield return new WaitForSecondsRealtime(0.6f);
                Transform tab = Tabs_Manager.GetTabObject(Tabs_Manager._tab._type.sns).transform;
                Transform area = tab.Find("AIFanChatter_Notices");
                Transform bar = tab.Find("AIFanChatter_Refresh");
                RectTransform postsRect = (RectTransform)tab.Find("ScrollRect");
                if (area == null || bar == null)
                {
                    ctx.Fail("The social media tab has no " + (area == null ? "notice area" : "Refresh bar"));
                    yield break;
                }
                List<Transform> cards = area.Cast<Transform>().ToList();
                string CardText(Transform card) => card.Find("Text").GetComponent<TMP_Text>().text;
                ctx.Record("cards", cards.Count);
                ctx.Assert(cards.Count == 3, "The tab shows " + cards.Count + " notices, not 3");
                if (cards.Count != 3)
                    yield break;
                ctx.Assert(CardText(cards[0]).Contains(stoppedText) && cards[0].Find("Close") == null, "The stopped notice isn't first, without a close button");
                ctx.Assert(CardText(cards[1]).Contains(shortText) && CardText(cards[2]).Contains("deepseek"), "The others aren't newest first, with the repeat moved to the top");
                ctx.Assert(cards[1].Find("Close") != null && cards[2].Find("Close") != null, "A closable notice has no close button");
                ctx.Assert(cards[0].GetComponent<Image>().color.r > cards[1].GetComponent<Image>().color.r, "The stopped notice isn't redder than the others");

                Rect barRect = ScreenRect(bar), areaRect = ScreenRect(area), posts = ScreenRect(postsRect);
                ctx.Assert(areaRect.yMax <= barRect.yMin && posts.yMax <= areaRect.yMin, "The notices aren't between the bar and the posts: " + barRect + ", " + areaRect + ", " + posts);
                foreach (Transform card in cards)
                {
                    TMP_Text text = card.Find("Text").GetComponent<TMP_Text>();
                    text.ForceMeshUpdate();
                    ctx.Assert(!text.isTextOverflowing && text.preferredHeight <= ((RectTransform)text.transform).rect.height + 1f,
                        "A notice's text doesn't fit its card: " + text.preferredHeight + " high in " + ((RectTransform)text.transform).rect.height);
                }
                yield return Screenshot(Path.Combine(shots, "7-notices.png"));

                // Closing one: the posts move back up by its height
                Transform close = cards[2].Find("Close");
                GameObject hit = TopHit(ScreenRect(close).center);
                ctx.Assert(hit != null && hit.transform.IsChildOf(close), "A click on the close button reaches " + (hit == null ? "nothing" : hit.name));
                Click(close.gameObject);
                yield return null;
                yield return null;
                ctx.Assert(area.Cast<Transform>().Count() == 2, "Closing a notice left " + area.Cast<Transform>().Count() + " cards");
                ctx.Assert(ScreenRect(postsRect).yMax > posts.yMax + 1f, "The posts didn't move up after a notice closed");

                // Requests run again: the stopped notice goes by itself
                Stopped(false);
                yield return null;
                yield return null;
                ctx.Assert(area.Cast<Transform>().Count() == 1 && CardText(area.GetChild(0)).Contains(shortText), "The stopped notice stayed after requests ran again");

                // The result of Apply also pops up, and is in the tab too
                Stopped(true);
                Notify(stoppedText, "Stopped", true);
                yield return TestTools.WaitFor(ctx, () => PoppedUp(stoppedText), 3f, "the Apply result to pop up");
                yield return null;
                yield return null;
                ctx.Assert(CardText(area.GetChild(0)).Contains(stoppedText), "The Apply result isn't in the tab");
                ctx.Record("screenshots", shots);
            }
        }

        /// <summary>
        /// Forgets the automatic request a test sent, so later tests aren't held back by the 30 seconds between
        /// requests or the weekly and hourly counts.
        /// </summary>
        private static void ResetRequestLimits(Assembly mod)
        {
            object dispatcher = AccessTools.Property(mod.GetType("AIFanChatter.Dispatcher", true), "Instance").GetValue(null, null);
            object client = AccessTools.Field(dispatcher.GetType(), "client").GetValue(dispatcher);
            AccessTools.Field(client.GetType(), "lastSend").SetValue(client, double.NegativeInfinity);
            ((IList)AccessTools.Field(client.GetType(), "sentThisHour").GetValue(client)).Clear();
            AccessTools.Field(client.GetType(), "sentThisWeek").SetValue(client, 0);
        }

        private static Rect ScreenRect(Transform t)
        {
            Vector3[] c = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(c);
            Camera cam = t.GetComponentInParent<Canvas>()?.rootCanvas.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static GameObject TopHit(Vector2 point)
        {
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point }, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static void Click(GameObject target)
        {
            UnityEngine.EventSystems.ExecuteEvents.Execute(target, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Screenshot(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float start = Time.realtimeSinceStartup;
            while (!File.Exists(path) && Time.realtimeSinceStartup - start < 5f)
                yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        /// <summary>
        /// The mod's longest notice, with a long model name and a long message from the service, fits the
        /// game's real notification panel: TextMesh Pro doesn't cut it off, the text stays inside its box,
        /// and the notification stays on screen. Only the engine lays the text out; the unit tests check
        /// every notice's wording.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator LongestNoticeFitsTheNotificationPanel(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);
            NotificationsContainer container = UnityEngine.Object.FindObjectOfType<NotificationsContainer>();
            if (container == null)
            {
                ctx.Fail("The game has no notification panel");
                yield break;
            }

            // The longest notice the mod can show, built by the mod's own code
            Type problems = mod.GetType("AICore.Problem", true);
            Type connectionType = mod.GetType("AICore.Connection", true);
            object connection = Activator.CreateInstance(connectionType);
            AccessTools.Field(connectionType, "Provider").SetValue(connection, Enum.ToObject(mod.GetType("AICore.Provider", true), 2));
            AccessTools.Field(connectionType, "BaseUrl").SetValue(connection, "https://openrouter.ai/api/v1");
            AccessTools.Field(connectionType, "Model").SetValue(connection, "deepseek/deepseek-v4-pro-0813");
            MethodInfo text = AccessTools.Method(mod.GetType("AICore.Notices", true), "Text");
            string detail = string.Concat(Enumerable.Repeat("The upstream provider returned an error while handling the request. ", 4)).Substring(0, 200);
            string longest = Enum.GetValues(problems).Cast<object>().Where(p => (int)p != 0)
                .Select(p => (string)text.Invoke(null, new[] { p, detail, 503L, connection, "the event Producer caught dating an idol" }))
                .OrderByDescending(t => t.Length).First();
            string notice = "AI Fan Chatter: " + longest;
            ctx.Record("characters", notice.Length);

            int before = container.transform.childCount;
            ctx.Record("shownBefore", before);
            NotificationManager.AddNotification(notice, mainScript.red32);
            yield return TestTools.WaitFor(ctx, () => container.transform.childCount > before, 3f, "the notice to show");
            if (container.transform.childCount <= before)
                yield break;
            // Layout and TMP's mesh settle over the next frames
            yield return null;
            yield return null;

            // The game's notifications use Unity's UI Text in a panel that grows to fit it
            Notification shown = container.GetComponentsInChildren<Notification>(true).LastOrDefault();
            Text label = shown?.TextObj?.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                ctx.Fail("The notification has no text: " + (shown == null ? "no Notification component"
                    : string.Join(", ", shown.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct().ToArray())));
                yield break;
            }
            Canvas.ForceUpdateCanvases();
            TextGenerator drawn = label.cachedTextGenerator;
            ctx.Record("lines", drawn.lineCount);
            ctx.Assert(label.text == notice, "The notification shows other text than the notice");
            // Spaces and line breaks aren't drawn, so the drawn characters are the notice without them
            int drawable = notice.Count(c => !char.IsWhiteSpace(c));
            ctx.Assert(drawn.characterCountVisible >= drawable,
                "The notification cut the notice off: " + drawn.characterCountVisible + " of " + drawable + " characters drawn");
            Rect box = label.rectTransform.rect;
            ctx.Assert(label.preferredHeight <= box.height + 1f, "The text (" + label.preferredHeight + " high) is taller than its box (" + box.height + ")");

            Canvas canvas = shown.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            ((RectTransform)shown.transform).GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                ctx.Assert(point.x >= -1f && point.x <= Screen.width + 1f && point.y >= -1f && point.y <= Screen.height + 1f,
                    "The notification reaches off screen at " + point);
            }

            string shots = Path.Combine(Path.GetTempPath(), "AIFanChatterNoticeScreens");
            Directory.CreateDirectory(shots);
            string shot = Path.Combine(shots, "longest-notice.png");
            if (File.Exists(shot))
                File.Delete(shot);
            ScreenCapture.CaptureScreenshot(shot);
            float start = Time.realtimeSinceStartup;
            while (!File.Exists(shot) && Time.realtimeSinceStartup - start < 5f)
                yield return null;
            ctx.Record("screenshot", shot);
        }

        /// <summary>
        /// A request through the real transport, to a plain-http server on this PC: the reply comes back to
        /// the main thread, and as the game clock runs every post and reply lands in the real feed, with
        /// the game's avatars. Running the clock can't be undone (training and treatment finish), so it's
        /// in the clock suite, after the checks that rely on the fixture as it loads.
        /// </summary>
        [InGameTest(Suite = ModTest.ClockSuite, Order = 10)]
        private static IEnumerator RequestFillsTheFeedAsTheClockRuns(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            const string reply = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"posts\\\":[" +
                "{\\\"user\\\":\\\"F_HC_T\\\",\\\"text\\\":\\\"ingame one\\\",\\\"replies\\\":[{\\\"user\\\":\\\"M_C_A\\\",\\\"text\\\":\\\"ingame reply\\\"}]}," +
                "{\\\"user\\\":\\\"M_C_YA\\\",\\\"text\\\":\\\"ingame two\\\"}]}\"},\"finish_reason\":\"stop\"}]}";

            List<SNS_Manager._message> before = SNS_Manager.Messages.ToList();
            using (LocalServer server = new(reply))
            using (TempKeyFolder(mod, out _))
            using (Game.Variable(Prefix + "Provider", "2"))
            using (Game.Variable(Prefix + "BaseURL", server.Url))
            using (Game.Variable(Prefix + "Model", "test-model"))
            using (Game.Variable(Prefix + "PerWeek", "7"))
            using (Game.Variable(Prefix + "Enabled", "1"))
            using (TestTools.Restore(() => RestoreFeed(mod, before)))
            {
                // Changing the settings started a connection test; let it finish first
                yield return TestTools.WaitFor(ctx, () => server.Requests >= 1, 10f, "the connection test");
                yield return new WaitForSecondsRealtime(1f);

                // The fans are counted from the game's idols, matching the game's own total
                object host = Activator.CreateInstance(mod.GetType("AIFanChatter.GameHost", true), true);
                IDictionary<string, long> counts = (IDictionary<string, long>)AccessTools.Property(host.GetType(), "FanCounts").GetValue(host, null);
                ctx.Record("fanKinds", counts.Count);
                ctx.Assert(counts.Values.Sum() == resources.GetFansTotal(), "The fan counts add up to " + counts.Values.Sum() + ", the game's total is " + resources.GetFansTotal());

                // The prompt's wording reached the game's loaded constants, with every placeholder filled in
                string prompt = (string)AccessTools.Method(mod.GetType("AIFanChatter.Prompt", true), "System")
                    .Invoke(null, new object[] { "English", counts.Keys.ToList(), null, null });
                ctx.Assert(prompt.StartsWith("You write fan posts") && prompt.Contains("\n- Write 3 to 6 posts") && !prompt.Contains("{language}"),
                    "The prompt didn't come from constants.json: " + prompt.Substring(0, Math.Min(200, prompt.Length)));

                object dispatcher = AccessTools.Property(mod.GetType("AIFanChatter.Dispatcher", true), "Instance").GetValue(null, null);
                object moment = Activator.CreateInstance(mod.GetType("AIFanChatter.Moment", true), "an in-game test");
                bool sent = (bool)AccessTools.Method(dispatcher.GetType(), "Request").Invoke(dispatcher, new[] { moment, false });
                ctx.Assert(sent, "The request wasn't sent: " + AccessTools.Method(dispatcher.GetType(), "Blocked").Invoke(dispatcher, new object[] { false }));
                if (!sent)
                    yield break;

                IList queue = (IList)AccessTools.Field(mod.GetType("AIFanChatter.FeedQueue", true), "Items").GetValue(null);
                yield return TestTools.WaitFor(ctx, () => queue.Count > 0, 15f, "the reply to be queued");
                ctx.Record("queued", queue.Count);

                yield return Game.AdvanceDays(ctx, 3);
                SNS_Manager._message one = SNS_Manager.Messages.LastOrDefault(m => m.Message == "<noparse>ingame one</noparse>");
                SNS_Manager._message two = SNS_Manager.Messages.LastOrDefault(m => m.Message == "<noparse>ingame two</noparse>");
                ctx.Assert(one != null && two != null, "The posts aren't in the feed");
                ctx.Assert(one != null && one.Replies.Any(r => r.Message == "<noparse>ingame reply</noparse>"), "The reply isn't under its post");
                ctx.Assert(one == null || SNS_Manager.Messages.IndexOf(one) < SNS_Manager.Messages.IndexOf(two), "The posts are out of order");
                ctx.Assert(one == null || one.GetSprite() != null, "The post has no avatar");
                ctx.Assert(queue.Count == 0, "Items are still queued after 3 days: " + queue.Count);
                ctx.Record("requests", server.Requests);
            }
        }

        /// <summary>
        /// Past 20 posts, a real save keeps the newest 20 where the game saves them, and a real load puts
        /// the whole feed and the queue back.
        /// </summary>
        [InGameTest(Suite = ModTest.Suite)]
        private static IEnumerator SaveAndLoadKeepTheFeedAndQueue(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            List<SNS_Manager._message> before = SNS_Manager.Messages.ToList();
            using (TestTools.Restore(() => RestoreFeed(mod, before)))
            {
                for (int i = 0; SNS_Manager.Messages.Count < 30; i++)
                {
                    SNS_Manager.Messages.Add(new SNS_Manager._message { User = "F_C_T", Message = "saved " + i });
                }
                Type queueType = mod.GetType("AIFanChatter.FeedQueue", true);
                List<SNS_Manager._message> batch = new() { new SNS_Manager._message { User = "M_HC_A", Message = "queued one" }, new SNS_Manager._message { User = "F_C_YA", Message = "queued two" } };
                AccessTools.Method(queueType, "AddBatch").Invoke(null, new object[] { batch, staticVars.dateTime.AddDays(10), new System.Random(1) });
                IList queue = (IList)AccessTools.Field(queueType, "Items").GetValue(null);
                List<string> feedBefore = SNS_Manager.Messages.Select(m => m.Message).ToList();
                int queuedBefore = queue.Count;

                yield return Game.Quicksave(ctx);
                int savedField = Game.Main.GetSavedData().SNS_Manager__Messages?.Count ?? -1;
                ctx.Record("savedField", savedField);
                ctx.Assert(savedField == 20, "The game's own save field holds " + savedField + " posts, not 20");

                yield return Game.Quickload(ctx);
                List<string> feedAfter = SNS_Manager.Messages.Select(m => m.Message).ToList();
                ctx.Assert(feedAfter.SequenceEqual(feedBefore), "The loaded feed differs: " + feedBefore.Count + " posts saved, " + feedAfter.Count + " loaded");
                ctx.Assert(queue.Count == queuedBefore, "The loaded queue holds " + queue.Count + " items, not " + queuedBefore);
            }
        }

        private static IEnumerator OpenMenu(TestContext ctx, Action<GameObject> found)
        {
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.settings);
            yield return null;
            Transform settingsTab = Tabs_Manager.GetTabObject(Tabs_Manager._tab._type.settings).transform;
            Transform button = settingsTab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ModMenuButton");
            if (button == null)
            {
                ctx.Fail("No Mod Settings button in the Settings tab");
                yield break;
            }
            button.GetComponent<Button>().onClick.Invoke();
            yield return TestTools.WaitFor(ctx, () => PopupManager.GetOpenPopupType() == ModMenuPopup, 5f, "the mod menu popup to open");
            GameObject popup = Game.Main.Data.GetComponent<PopupManager>().GetByType(ModMenuPopup)?.obj;
            if (popup != null)
                found(popup);
        }

        /// <summary>
        /// Presses Apply and waits for the menu to close, so reopening it runs its rows' OnEnable again.
        /// </summary>
        private static IEnumerator Apply(TestContext ctx, Transform row)
        {
            Assembly modMenus = TelMod.ByHarmonyId(ModMenusId).Assembly();
            // From the popup: GetComponentInParent skips a row whose tab isn't the one shown
            GameObject popup = Game.Main.Data.GetComponent<PopupManager>().GetByType(ModMenuPopup)?.obj;
            Component manager = popup != null ? popup.GetComponentInChildren(modMenus.GetType("ModMenus.ModMenusUtils+ModMenuManager", true), true) : null;
            if (manager == null)
            {
                ctx.Fail("No open mod menu to apply");
                yield break;
            }
            AccessTools.Method(manager.GetType(), "OnApply").Invoke(manager, null);
            // Until the popup has finished hiding: one reopened during its fade is switched off when the fade ends.
            // The row says nothing, as it's hidden all along while another tab is shown.
            yield return TestTools.WaitFor(ctx, () => PopupManager.GetOpenPopupType() != ModMenuPopup && !popup.activeSelf, 5f, "the mod menu to close");
        }

        /// <summary>
        /// Points the mod's key folder, and ModMenus' secrets with it, at a temp folder, so the player's own keys
        /// are never read or changed. With it set, the mod looks in no old key folder either.
        /// </summary>
        private static IDisposable TempKeyFolder(Assembly mod, out string folder)
        {
            Type store = mod.GetType("AICore.KeyStore", true);
            FieldInfo overrideField = AccessTools.Field(store, "FolderOverride");
            MethodInfo clear = AccessTools.Method(store, "ClearCache");
            object previous = overrideField.GetValue(null);
            // ModMenus keeps the keys in <its folder>\Tel AI Mods, as the mod does in ModConfig
            string configFolder = Path.Combine(Path.Combine(Path.GetTempPath(), "AIFanChatterInGame"), Guid.NewGuid().ToString("N"));
            string temp = Path.Combine(configFolder, (string)AccessTools.Field(store, "SharedFolderName").GetRawConstantValue());
            folder = temp;
            overrideField.SetValue(null, temp);
            clear.Invoke(null, null);

            Type secrets = ModTest.IsActive(ModMenusId) ? TelMod.ByHarmonyId(ModMenusId).Assembly().GetType("ModMenus.SecretStore") : null;
            FieldInfo secretsField = secrets != null ? AccessTools.Field(secrets, "FolderOverride") : null;
            MethodInfo secretsClear = secrets != null ? AccessTools.Method(secrets, "ClearCache") : null;
            object secretsPrevious = secretsField?.GetValue(null);
            secretsField?.SetValue(null, configFolder);
            secretsClear?.Invoke(null, null);
            return TestTools.Restore(() =>
            {
                overrideField.SetValue(null, previous);
                clear.Invoke(null, null);
                secretsField?.SetValue(null, secretsPrevious);
                secretsClear?.Invoke(null, null);
                if (Directory.Exists(configFolder))
                    Directory.Delete(configFolder, true);
            });
        }

        /// <summary>
        /// Puts every mod setting back as it was; Apply saves every row of every mod.
        /// </summary>
        private static IDisposable RestoreVariables()
        {
            List<variables._variable> saved = variables.variable.Select(v => new variables._variable { name = v.name, value = v.value }).ToList();
            return TestTools.Restore(() =>
            {
                variables.variable.Clear();
                variables.variable.AddRange(saved);
            });
        }

        private static void RestoreFeed(Assembly mod, List<SNS_Manager._message> before)
        {
            AccessTools.Method(mod.GetType("AIFanChatter.FeedQueue", true), "Clear").Invoke(null, null);
            SNS_Manager.Messages.Clear();
            SNS_Manager.Messages.AddRange(before);
            Game.Main.Data.GetComponent<SNS_Manager>().RenderMessages();
        }

        private static readonly List<object> graduationsPosted = new();

        /// <summary>
        /// The real goodbye scene, played by the game's dialogue controller: the successor the player asks
        /// for is hired before the scene ends, and the graduation is sent once, when it ends, with her. Only
        /// the engine plays a dialogue; the facts and every other case are unit-tested.
        /// </summary>
        [InGameTest(Suite = ModTest.GraduationSuite, Order = 10)]
        private static IEnumerator GoodbyeScene_SendsTheGraduationWithHerSuccessor(TestContext ctx)
        {
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            yield return Game.CloseAllPopups(ctx);

            AccessTools.Method(mod.GetType("AIFanChatter.Moments", true), "ResetGraduations").Invoke(null, null);
            graduationsPosted.Clear();
            data_girls.girls leaving = data_girls.GetActiveGirls().Last();
            var dialogue = Game.Main.Data.GetComponent<ActiveDialogueController>();
            string successorChoice = Language.Data["DATE__SUCCESSOR"];
            // She always agrees to bring in a successor: the game asks for high satisfaction
            using (TestTools.Spy(AccessTools.Method(typeof(Date_Graduation), nameof(Date_Graduation.GetSatisfactionCoeff)),
                       postfix: AccessTools.Method(typeof(AIFanChatterTests), nameof(Satisfied))))
            using (TestTools.Spy(AccessTools.Method(mod.GetType("AIFanChatter.GraduationScene", true), "Post"),
                       postfix: AccessTools.Method(typeof(AIFanChatterTests), nameof(RecordGraduation))))
            {
                leaving.Graduate(dialogue: true);
                yield return TestTools.WaitFor(ctx, () => ActiveDialogueController.ShowingDialogue, 10f, "the goodbye scene");
                ctx.Assert(graduationsPosted.Count == 0, "The graduation was sent before its goodbye scene ended");

                float start = Time.realtimeSinceStartup;
                while (ActiveDialogueController.ShowingDialogue && Time.realtimeSinceStartup - start < 60f)
                {
                    vnButton successor = dialogue.buttonsContainer.GetComponentsInChildren<vnButton>()
                        .FirstOrDefault(b => b.GetComponentInChildren<TextMeshProUGUI>()?.text.Contains(successorChoice) == true);
                    if (successor != null)
                    {
                        successor.onClick();
                    }
                    else
                    {
                        Game.ClickDialogue(dialogue);
                    }
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }

            ctx.Record("graduationsSent", graduationsPosted.Count);
            ctx.Assert(graduationsPosted.Count == 1, $"The graduation was sent {graduationsPosted.Count} times; expected once, when the scene ended");
            object hired = graduationsPosted.Count == 1 ? graduationsPosted[0]?.GetType().GetField("Successor")?.GetValue(graduationsPosted[0]) : null;
            ctx.Assert(hired is data_girls.girls, "The graduation was sent without the successor the scene hired");

            // The game shows the new idol's introduction a few seconds later; click through it as a player would
            yield return TestTools.WaitFor(ctx, () => ActiveDialogueController.ShowingDialogue, 15f, "the successor's introduction");
            float introStart = Time.realtimeSinceStartup;
            while (ActiveDialogueController.ShowingDialogue && Time.realtimeSinceStartup - introStart < 60f)
            {
                Game.ClickDialogue(dialogue);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            yield return Game.CloseAllPopups(ctx);
        }

        private static void Satisfied(ref float __result) => __result = 1f;

        private static void RecordGraduation(object[] __args)
        {
            if (__args[0] != null)
            {
                graduationsPosted.Add(__args[0]);
            }
        }

        /// <summary>
        /// Answers every request with the same OpenAI-style reply over plain http, and counts them.
        /// </summary>
        private sealed class LocalServer : IDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            private readonly byte[] response;
            private int requests;
            private int received;

            public string Url => "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            /// <summary>Requests answered.</summary>
            public int Requests => Volatile.Read(ref requests);
            /// <summary>Requests received, answered yet or not.</summary>
            public int Received => Volatile.Read(ref received);
            /// <summary>How long the server waits before answering, in milliseconds.</summary>
            public volatile int DelayMs;

            public LocalServer(string body)
            {
                byte[] content = Encoding.UTF8.GetBytes(body);
                response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n")
                    .Concat(content).ToArray();
                listener.Start();
                new Thread(Serve) { IsBackground = true }.Start();
            }

            private void Serve()
            {
                try
                {
                    while (true)
                    {
                        using TcpClient client = listener.AcceptTcpClient();
                        NetworkStream stream = client.GetStream();
                        ReadRequest(stream);
                        Interlocked.Increment(ref received);
                        Thread.Sleep(DelayMs);
                        stream.Write(response, 0, response.Length);
                        Interlocked.Increment(ref requests);
                    }
                }
                catch (Exception)
                {
                    // Stopped
                }
            }

            private static void ReadRequest(Stream stream)
            {
                StringBuilder text = new();
                byte[] chunk = new byte[4096];
                int length = -1, headerEnd = -1, total = 0;
                while (headerEnd < 0 || total < headerEnd + 4 + length)
                {
                    int read = stream.Read(chunk, 0, chunk.Length);
                    if (read <= 0)
                        return;
                    total += read;
                    text.Append(Encoding.ASCII.GetString(chunk, 0, read));
                    if (headerEnd < 0 && (headerEnd = text.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal)) >= 0)
                    {
                        string header = text.ToString(0, headerEnd);
                        string line = header.Split('\n').FirstOrDefault(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        length = line != null ? int.Parse(line.Substring(15).Trim()) : 0;
                    }
                }
            }

            public void Dispose() => listener.Stop();
        }
    }
}
