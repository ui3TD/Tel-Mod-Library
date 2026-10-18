using HarmonyLib;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Not a test: a reporting tool. It sends three moments from the fixture save (the main group's latest
    /// single, the latest finished concert and the graduated idol, replayed) to a real AI service through AI Fan Chatter's own
    /// dispatcher, once per model, and writes every request, reply and parsed post to a JSON file for
    /// tools/ai_fan_chatter_live_report.py. It costs money, so it runs only when that tool has written its
    /// request file; any other run, `--scope "AI Fan Chatter"` and `--suite all` included, skips it.
    /// </summary>
    [ModUnderTest(HarmonyId)]
    internal static class AIFanChatterLiveReport
    {
        public const string Suite = "live-report";
        private const string HarmonyId = "com.tel.aifanchatter";
        private const string Prefix = "AIFanChatter_";
        public const string RequestFileName = "aifanchatter-live-report.json";

        private static JSONArray calls;

        [InGameTest(Suite = Suite)]
        private static IEnumerator RecordLiveReplies(TestContext ctx)
        {
            string requestFile = Path.Combine(Path.Combine(Application.persistentDataPath, "InGameTests"), RequestFileName);
            if (!File.Exists(requestFile))
            {
                ctx.Note("No request file from tools/ai_fan_chatter_live_report.py; nothing sent");
                yield break;
            }
            if (!ModTest.Require(ctx, HarmonyId, out Assembly mod))
                yield break;
            JSONNode request = JSON.Parse(File.ReadAllText(requestFile));
            string outFile = request["out"].Value;
            double gap = request["gapSeconds"].AsDouble;
            yield return Game.CloseAllPopups(ctx);

            Type dispatcherType = mod.GetType("AIFanChatter.Dispatcher", true);
            Type queueType = mod.GetType("AIFanChatter.FeedQueue", true);
            Type contextType = mod.GetType("AIFanChatter.MomentContext", true);
            PropertyInfo instance = AccessTools.Property(dispatcherType, "Instance");
            object original = instance.GetValue(null, null);
            IList queue = (IList)AccessTools.Field(queueType, "Items").GetValue(null);
            List<SNS_Manager._message> feedBefore = SNS_Manager.Messages.ToList();

            Groups._group main = Groups.Groups_.FirstOrDefault(g => g != null && g.IsMain());
            singles._single single = singles.Singles
                .Where(s => s != null && s.status == singles._single._status.released && s.ReleaseData != null && (main == null || s.GetGroup() == main))
                .OrderBy(s => s.ReleaseData.ReleaseDate).LastOrDefault();
            // The latest finished concert, replayed: one still in production has no audience or hype yet
            SEvent_Concerts._concert concert = SEvent_Concerts.Concerts
                .Where(c => c != null && c.Status == SEvent_Tour.tour._status.finished)
                .OrderBy(c => c.FinishDate).LastOrDefault();
            data_girls.girls graduate = data_girls.girl.FirstOrDefault(g => g != null && g.status == data_girls._status.graduated);
            var moments = new List<KeyValuePair<string, Func<object>>>
            {
                new("a single release", () => AccessTools.Method(contextType, "Single").Invoke(null, new object[] { single })),
                new("a concert", () => AccessTools.Method(contextType, "Concert").Invoke(null, new object[] { concert })),
                new("a graduation", () => AccessTools.Method(contextType, "Graduation").Invoke(null, new object[] { graduate, null }))
            };

            JSONClass output = new();
            output["saveDate"] = staticVars.dateTime.ToString("yyyy-MM-dd HH:mm");
            output["language"] = staticVars.Settings.Language ?? "";
            JSONArray runs = new();
            output["runs"] = runs;

            using (Game.Variable(Prefix + "Provider", request["provider"].Value))
            using (Game.Variable(Prefix + "BaseURL", request["baseUrl"].Value))
            using (Game.Variable(Prefix + "PerWeek", "7"))
            using (Game.Variable(Prefix + "Enabled", "1"))
            using (TestTools.Restore(() =>
            {
                AccessTools.PropertySetter(dispatcherType, "Instance").Invoke(null, new[] { original });
                AccessTools.Method(queueType, "Clear").Invoke(null, null);
                SNS_Manager.Messages.Clear();
                SNS_Manager.Messages.AddRange(feedBefore);
            }))
            using (TestTools.Spy(AccessTools.Method(mod.GetType("AIFanChatter.NetTransport", true), "Send"), prefix: AccessTools.Method(typeof(AIFanChatterLiveReport), nameof(OnSend))))
            using (TestTools.Spy(AccessTools.Method(mod.GetType("AIFanChatter.Providers", true), "ParseResponse"), prefix: AccessTools.Method(typeof(AIFanChatterLiveReport), nameof(OnResponse))))
            {
                foreach (JSONNode model in request["models"].AsArray)
                {
                    // Each model starts from the save's own feed, with a fresh dispatcher and its limits
                    AccessTools.Method(queueType, "Clear").Invoke(null, null);
                    SNS_Manager.Messages.Clear();
                    SNS_Manager.Messages.AddRange(feedBefore);
                    object dispatcher = Activator.CreateInstance(dispatcherType,
                        Activator.CreateInstance(mod.GetType("AIFanChatter.GameHost", true), true),
                        Activator.CreateInstance(mod.GetType("AIFanChatter.NetTransport", true), true),
                        new System.Random());
                    AccessTools.PropertySetter(dispatcherType, "Instance").Invoke(null, new[] { dispatcher });
                    MethodInfo blocked = AccessTools.Method(dispatcherType, "Blocked");
                    PropertyInfo inFlight = AccessTools.Property(dispatcherType, "InFlight");

                    JSONClass run = new();
                    run["model"] = model.Value;
                    JSONArray results = new();
                    run["moments"] = results;
                    runs.Add(run);

                    using (Game.Variable(Prefix + "Model", model.Value))
                    {
                        double last = double.NegativeInfinity;
                        foreach (var moment in moments)
                        {
                            JSONClass result = new();
                            result["name"] = moment.Key;
                            results.Add(result);
                            float wait = (float)Math.Max(0, gap - (Time.realtimeSinceStartup - last));
                            yield return new WaitForSecondsRealtime(wait);
                            yield return TestTools.WaitFor(ctx, () => blocked.Invoke(dispatcher, null) == null, 120f, "the dispatcher to allow a request");

                            calls = new JSONArray();
                            result["calls"] = calls;
                            object built;
                            try
                            {
                                built = moment.Value();
                            }
                            catch (Exception e)
                            {
                                result["error"] = "Couldn't build the moment: " + (e.InnerException ?? e).Message;
                                calls = null;
                                continue;
                            }
                            int batchesBefore = queue.Count == 0 ? 0 : queue.Cast<object>().Max(i => (int)AccessTools.Field(i.GetType(), "Batch").GetValue(i));
                            last = Time.realtimeSinceStartup;
                            bool sent = (bool)AccessTools.Method(dispatcherType, "Request").Invoke(dispatcher, new[] { built });
                            if (!sent)
                            {
                                result["error"] = "Not sent: " + blocked.Invoke(dispatcher, null);
                                calls = null;
                                continue;
                            }
                            yield return TestTools.WaitFor(ctx, () => !(bool)inFlight.GetValue(dispatcher, null), 300f, "the reply from " + model.Value);

                            JSONArray queued = new();
                            foreach (object item in queue)
                            {
                                if ((int)AccessTools.Field(item.GetType(), "Batch").GetValue(item) <= batchesBefore)
                                    continue;
                                JSONClass entry = new();
                                entry["user"] = (string)AccessTools.Field(item.GetType(), "User").GetValue(item);
                                entry["text"] = (string)AccessTools.Field(item.GetType(), "Text").GetValue(item);
                                entry["isReply"] = ((bool)AccessTools.Field(item.GetType(), "IsReply").GetValue(item)) ? "true" : "false";
                                entry["parentUser"] = (string)AccessTools.Field(item.GetType(), "ParentUser").GetValue(item);
                                entry["parentText"] = (string)AccessTools.Field(item.GetType(), "ParentText").GetValue(item);
                                entry["due"] = ((DateTime)AccessTools.Field(item.GetType(), "Due").GetValue(item)).ToString("yyyy-MM-dd HH:mm");
                                queued.Add(entry);
                            }
                            result["queued"] = queued;
                            ctx.Record(model.Value + " / " + moment.Key, calls.Count + " calls, " + queued.Count + " queued");
                            // Requests outside a moment, such as the connection test a settings change starts, aren't recorded
                            calls = null;
                        }
                    }
                }
                calls = null;
            }

            File.WriteAllText(outFile, output.ToString());
            ctx.Record("output", outFile);
        }

        private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static void OnSend(object __0)
        {
            if (calls == null)
                return;
            JSONClass call = new();
            call["sentAt"] = Number(Time.realtimeSinceStartup);
            call["url"] = Traverse.Create(__0).Field("Url").GetValue<string>();
            call["request"] = Traverse.Create(__0).Field("Body").GetValue<string>();
            calls.Add(call);
        }

        private static void OnResponse(object __1)
        {
            if (calls == null || calls.Count == 0)
                return;
            JSONNode call = calls[calls.Count - 1];
            call["repliedAt"] = Number(Time.realtimeSinceStartup);
            call["status"] = Number(Traverse.Create(__1).Field("Status").GetValue<long>());
            call["response"] = Traverse.Create(__1).Field("Body").GetValue<string>() ?? "";
            call["networkError"] = Traverse.Create(__1).Field("NetworkError").GetValue<string>() ?? "";
        }
    }
}
