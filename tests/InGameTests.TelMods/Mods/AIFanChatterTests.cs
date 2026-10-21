using HarmonyLib;
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
    /// AI Fan Chatter in the real game: the key row in ModMenus' real menu, a request through the real
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
        /// A key typed into ModMenus' real key field goes to the key file on Apply, the save gets "", the
        /// reopened field is empty, and a later Apply with the field left blank keeps the key. ModMenus' own
        /// test checks that password fields show asterisks.
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
            using (RestoreVariables())
            {
                const string typed = "sk-ingame-test-WXYZ";
                Component[] rows = null;
                yield return OpenMenu(ctx, popup => rows = popup.GetComponentsInChildren<Transform>(true).Select(t => (Component)t).ToArray());
                Transform row = rows?.OfType<Transform>().FirstOrDefault(t => t.name == "ModMenuInput_" + KeyVar);
                if (row == null)
                {
                    ctx.Fail("The mod menu has no row ModMenuInput_" + KeyVar);
                    yield return Game.CloseAllPopups(ctx);
                    yield break;
                }
                TMP_InputField field = row.GetComponentInChildren<TMP_InputField>(true);

                // Typing goes through the field's own event, as a player's typing does
                field.text = typed;
                yield return Apply(ctx, row);
                ctx.Assert(variables.Get(KeyVar) == "", "Apply saved the key in the save: " + (variables.Get(KeyVar) ?? "null"));
                // The key file of the service the menu has selected
                Type settings = mod.GetType("AICore.ConnectionSettings", true);
                string file = (string)AccessTools.Method(mod.GetType("AICore.KeyStore", true), "FileFor")
                    .Invoke(null, new[] { AccessTools.Property(settings, "Provider").GetValue(null, null) });
                ctx.Assert(File.Exists(file) && File.ReadAllText(file) == typed, "Apply didn't write the key file");

                yield return OpenMenu(ctx, _ => { });
                ctx.Assert(field.text == "", "The reopened field shows text: " + field.text.Length + " characters");

                yield return Apply(ctx, row);
                ctx.Assert(File.Exists(file) && File.ReadAllText(file) == typed, "A blank Apply removed the key");
            }
            yield return Game.CloseAllPopups(ctx);
            Tabs_Manager.OpenTab_(Tabs_Manager._tab._type.closed);
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
                bool sent = (bool)AccessTools.Method(dispatcher.GetType(), "Request").Invoke(dispatcher, new[] { moment });
                ctx.Assert(sent, "The request wasn't sent: " + AccessTools.Method(dispatcher.GetType(), "Blocked").Invoke(dispatcher, null));
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
            Component manager = row.GetComponentInParent(modMenus.GetType("ModMenus.ModMenusUtils+ModMenuManager", true));
            AccessTools.Method(manager.GetType(), "OnApply").Invoke(manager, null);
            yield return TestTools.WaitFor(ctx, () => PopupManager.GetOpenPopupType() != ModMenuPopup && !row.gameObject.activeInHierarchy, 5f, "the mod menu to close");
        }

        /// <summary>
        /// Points the mod's key folder at a temp folder, so the player's own keys are never read or changed.
        /// With it set, the mod looks in no old key folder either.
        /// </summary>
        private static IDisposable TempKeyFolder(Assembly mod, out string folder)
        {
            Type store = mod.GetType("AICore.KeyStore", true);
            FieldInfo overrideField = AccessTools.Field(store, "FolderOverride");
            MethodInfo clear = AccessTools.Method(store, "ClearCache");
            object previous = overrideField.GetValue(null);
            string temp = Path.Combine(Path.GetTempPath(), "AIFanChatterInGame", Guid.NewGuid().ToString("N"));
            folder = temp;
            overrideField.SetValue(null, temp);
            clear.Invoke(null, null);
            return TestTools.Restore(() =>
            {
                overrideField.SetValue(null, previous);
                clear.Invoke(null, null);
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
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

        /// <summary>
        /// Answers every request with the same OpenAI-style reply over plain http, and counts them.
        /// </summary>
        private sealed class LocalServer : IDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            private readonly byte[] response;
            private int requests;

            public string Url => "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            public int Requests => Volatile.Read(ref requests);

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
