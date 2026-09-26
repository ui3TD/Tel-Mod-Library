using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CustomAuditions
{
    /// <summary>
    /// Temporary hard-crash instrumentation for audition candidate generation and portrait caching.
    /// It intentionally observes only: no candidate selection, queue, cache, Unicode path, or Assistant
    /// Manager behavior is changed. Every line is synchronously appended with WriteThrough so the last
    /// completed checkpoint is likely to survive a native Unity termination.
    /// </summary>
    internal static class AuditionCrashDiagnostics
    {
        // Set to true and rebuild only when hard-crash instrumentation is needed.
        // False means no diagnostic Harmony patches, no Unity/BepInEx log mirroring, and no diagnostic file writes.
        internal static readonly bool Enabled = false;

        private const string DiagnosticHarmonyId = "com.tel.customauditions.auditioncrashdiag";
        private const string DiagnosticFileName = "audition_crash_diagnostics.log";
        private const long RotateAtBytes = 8L * 1024L * 1024L;

        private static readonly object FileLock = new object();
        private static readonly Harmony IteratorHarmony = new Harmony(DiagnosticHarmonyId);
        private static readonly FieldInfo QueueField = AccessTools.Field(typeof(data_girls_textures), "Queue");
        private static readonly FieldInfo ActiveQueueField = AccessTools.Field(typeof(data_girls_textures), "ActiveQueue");

        private static long sequence;
        private static int auditionSequence;
        private static int candidateSequence;
        private static int currentAudition;
        private static int currentCandidate;
        private static bool iteratorPatchesInstalled;
        private static bool headerWritten;

        internal static string LogPath
        {
            get
            {
                try
                {
                    return Path.Combine(Application.persistentDataPath, DiagnosticFileName);
                }
                catch
                {
                    return DiagnosticFileName;
                }
            }
        }

        internal static void BeginAudition(Auditions.data data)
        {
            if (!Enabled) return;
            EnsureHeader();
            EnsureIteratorPatchesInstalled();
            currentAudition = Interlocked.Increment(ref auditionSequence);
            currentCandidate = 0;
            Write(
                "AUDITION BEGIN id=" + currentAudition +
                " type=" + Safe(() => data == null ? "<null>" : data.Type.ToString()) +
                " requested_count=" + Safe(() => Camera.main.GetComponent<mainScript>().Data.GetComponent<Auditions>().NumberOfGirls.ToString()) +
                " assistant_owner=" + DescribeAssistantOwnership(data));
            WritePatchOwners();
        }

        internal static void EndAudition(Auditions.data data, Exception exception)
        {
            Write(
                "AUDITION END id=" + currentAudition +
                " generated_count=" + Safe(() => data == null || data.Girls == null ? "<null>" : data.Girls.Count.ToString()) +
                " exception=" + DescribeException(exception));
        }

        internal static void BeginGenerateGirl(bool genTextures, Auditions.data._girl._type type, data_girls_textures._textureAsset bodyAsset)
        {
            currentCandidate = Interlocked.Increment(ref candidateSequence);
            Write(
                "GenerateGirl ENTER audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " genTextures=" + genTextures +
                " requested_type=" + type +
                " supplied_body=" + DescribeRawAsset(bodyAsset));
        }

        internal static void EndGenerateGirl(data_girls.girls result, Exception exception)
        {
            Write(
                "GenerateGirl EXIT audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " result=" + DescribeGirl(result) +
                " exception=" + DescribeException(exception));
        }

        internal static void GenerateParamsEnter(data_girls.girls girl, Auditions.data._girl._type type)
        {
            Write(
                "GenerateParams ENTER audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " girl=" + DescribeGirlBrief(girl) +
                " type=" + type);
        }

        internal static void GenerateParamsExit(data_girls.girls girl, Exception exception)
        {
            Write(
                "GenerateParams EXIT audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " girl=" + DescribeGirlBrief(girl) +
                " exception=" + DescribeException(exception));
        }

        internal static void AddToQueueEnter(data_girls.girls girl, GameObject target)
        {
            EnsureIteratorPatchesInstalled();
            Write(
                "AddToQueue ENTER audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " target=" + DescribeUnityObject(target) +
                " queue=" + DescribeQueue() +
                " girl=" + DescribeGirl(girl));
        }

        internal static void AddToQueueExit(data_girls.girls girl, Exception exception)
        {
            Write(
                "AddToQueue EXIT audition=" + currentAudition +
                " candidate=" + currentCandidate +
                " queue=" + DescribeQueue() +
                " girl=" + DescribeGirlBrief(girl) +
                " exception=" + DescribeException(exception));
        }

        internal static void RunQueueEnter(bool init)
        {
            Write("RunTheQueue ENTER init=" + init + " queue=" + DescribeQueue());
        }

        internal static void RunQueueExit(bool init, Exception exception)
        {
            Write("RunTheQueue EXIT init=" + init + " queue=" + DescribeQueue() + " exception=" + DescribeException(exception));
        }

        internal static void DoThingEnter(data_girls.girls girl, GameObject target)
        {
            Write("DoTheThing ENTER target=" + DescribeUnityObject(target) + " girl=" + DescribeGirlBrief(girl));
        }

        internal static void DoThingExit(data_girls.girls girl, Exception exception)
        {
            Write("DoTheThing EXIT girl=" + DescribeGirlBrief(girl) + " exception=" + DescribeException(exception));
        }

        internal static void CacheTexturesEnter(Portrait_Renderer renderer, data_girls.girls girl)
        {
            Write(
                "Portrait_Renderer.CacheTextures ENTER renderer=" + DescribeUnityObject(renderer) +
                " girl=" + DescribeGirl(girl));
        }

        internal static void CacheTexturesExit(data_girls.girls girl, Exception exception)
        {
            Write("Portrait_Renderer.CacheTextures EXIT girl=" + DescribeGirlBrief(girl) + " exception=" + DescribeException(exception));
        }

        internal static void CreateFilesEnter(Portrait_Renderer renderer)
        {
            Write("Portrait_Renderer.CreateFiles ENTER renderer=" + DescribeUnityObject(renderer));
        }

        internal static void CreateFilesExit(Exception exception)
        {
            Write("Portrait_Renderer.CreateFiles EXIT exception=" + DescribeException(exception));
        }

        private static void EnsureHeader()
        {
            if (!Enabled) return;
            if (headerWritten)
            {
                return;
            }

            lock (FileLock)
            {
                if (headerWritten)
                {
                    return;
                }

                try
                {
                    string path = LogPath;
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    if (File.Exists(path) && new FileInfo(path).Length > RotateAtBytes)
                    {
                        string previous = path + ".previous";
                        if (File.Exists(previous))
                        {
                            File.Delete(previous);
                        }
                        File.Move(path, previous);
                    }
                }
                catch
                {
                    // Diagnostic logging must never disturb gameplay.
                }

                headerWritten = true;
            }

            Write(
                "SESSION START diag=1 targeted_auditions=2.0.7" +
                " unity=" + Application.unityVersion +
                " persistentDataPath=" + Quote(Application.persistentDataPath) +
                " currentCulture=" + System.Globalization.CultureInfo.CurrentCulture.Name +
                " uiCulture=" + System.Globalization.CultureInfo.CurrentUICulture.Name);
        }

        internal static void Write(string message)
        {
            if (!Enabled) return;
            try
            {
                long id = Interlocked.Increment(ref sequence);
                string line =
                    DateTime.UtcNow.ToString("O") +
                    " #" + id.ToString("D6") +
                    " T" + Thread.CurrentThread.ManagedThreadId +
                    " " + message;

                Debug.Log("[AuditionCrashDiag] " + line);

                lock (FileLock)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                    using (FileStream stream = new FileStream(
                        LogPath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.LogWarning("[AuditionCrashDiag] Diagnostic write failed: " + ex.GetType().Name + ": " + ex.Message);
                }
                catch
                {
                }
            }
        }

        private static void EnsureIteratorPatchesInstalled()
        {
            if (!Enabled) return;
            if (iteratorPatchesInstalled)
            {
                return;
            }

            lock (FileLock)
            {
                if (iteratorPatchesInstalled)
                {
                    return;
                }
                iteratorPatchesInstalled = true;
            }

            TryPatchIterator(typeof(data_girls_textures), "<NEW_Cache_Portrait>d__", "NEW_Cache_Portrait");
            TryPatchIterator(typeof(data_girls_textures), "<GetTextureFromCache>d__", "GetTextureFromCache");
            TryPatchIterator(typeof(Portrait_Renderer), "<Do>d__", "Portrait_Renderer.Do");
            TryPatchIterator(typeof(Portrait_Renderer), "<LoadSprite>d__", "Portrait_Renderer.LoadSprite");
            TryPatchIterator(typeof(Portrait_Renderer), "<_SetupAndApply>d__", "Portrait_Renderer._SetupAndApply");
        }

        private static void TryPatchIterator(Type declaringType, string nestedNamePrefix, string logicalName)
        {
            try
            {
                Type iteratorType = declaringType
                    .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(type => type.Name.StartsWith(nestedNamePrefix, StringComparison.Ordinal));
                if (iteratorType == null)
                {
                    Write("ITERATOR PATCH SKIP logical=" + logicalName + " reason=nested_type_not_found");
                    return;
                }

                MethodInfo moveNext = iteratorType.GetMethod(
                    "MoveNext",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (moveNext == null)
                {
                    Write("ITERATOR PATCH SKIP logical=" + logicalName + " reason=MoveNext_not_found type=" + iteratorType.FullName);
                    return;
                }

                IteratorHarmony.Patch(
                    moveNext,
                    prefix: new HarmonyMethod(typeof(AuditionCrashDiagnostics), nameof(IteratorMoveNextPrefix)),
                    postfix: new HarmonyMethod(typeof(AuditionCrashDiagnostics), nameof(IteratorMoveNextPostfix)),
                    finalizer: new HarmonyMethod(typeof(AuditionCrashDiagnostics), nameof(IteratorMoveNextFinalizer)));

                Write("ITERATOR PATCH OK logical=" + logicalName + " type=" + iteratorType.FullName);
            }
            catch (Exception ex)
            {
                Write("ITERATOR PATCH FAIL logical=" + logicalName + " exception=" + DescribeException(ex));
            }
        }

        public static void IteratorMoveNextPrefix(object __instance, MethodBase __originalMethod)
        {
            string logical = DescribeIteratorLogicalName(__instance);
            int state = ReadIntField(__instance, "<>1__state", int.MinValue);
            Write(
                "ITERATOR ENTER logical=" + logical +
                " state=" + state +
                " detail=" + DescribeIterator(__instance, state));
        }

        public static void IteratorMoveNextPostfix(object __instance, MethodBase __originalMethod, bool __result)
        {
            string logical = DescribeIteratorLogicalName(__instance);
            int state = ReadIntField(__instance, "<>1__state", int.MinValue);
            Write(
                "ITERATOR EXIT logical=" + logical +
                " state=" + state +
                " result=" + __result +
                " detail=" + DescribeIterator(__instance, state));
        }

        public static Exception IteratorMoveNextFinalizer(object __instance, MethodBase __originalMethod, Exception __exception)
        {
            if (__exception != null)
            {
                Write(
                    "ITERATOR EXCEPTION logical=" + DescribeIteratorLogicalName(__instance) +
                    " state=" + ReadIntField(__instance, "<>1__state", int.MinValue) +
                    " exception=" + DescribeException(__exception));
            }
            return __exception;
        }

        private static string DescribeIteratorLogicalName(object instance)
        {
            if (instance == null)
            {
                return "<null>";
            }
            string name = instance.GetType().Name;
            if (name.Contains("NEW_Cache_Portrait")) return "NEW_Cache_Portrait.MoveNext";
            if (name.Contains("GetTextureFromCache")) return "GetTextureFromCache.MoveNext";
            if (name.Contains("LoadSprite")) return "Portrait_Renderer.LoadSprite.MoveNext";
            if (name.Contains("_SetupAndApply")) return "Portrait_Renderer._SetupAndApply.MoveNext";
            if (name.Contains("<Do>")) return "Portrait_Renderer.Do.MoveNext";
            return instance.GetType().FullName;
        }

        private static string DescribeIterator(object instance, int state)
        {
            if (instance == null)
            {
                return "<null>";
            }

            try
            {
                Type type = instance.GetType();
                if (type.Name.Contains("GetTextureFromCache"))
                {
                    object textureObject = ReadField(instance, "texture");
                    object sizeObject = ReadField(instance, "size");
                    data_girls.girls._texture texture = textureObject as data_girls.girls._texture;
                    string size = sizeObject == null ? "<null>" : sizeObject.ToString();
                    return DescribeCacheRequest(texture, size);
                }

                if (type.Name.Contains("NEW_Cache_Portrait"))
                {
                    data_girls.girls girl = ReadField(instance, "Girl") as data_girls.girls;
                    return DescribeGirlBrief(girl) + " texture=" + DescribeTexture(girl == null ? null : girl.texture);
                }

                if (type.Name.Contains("LoadSprite"))
                {
                    object wrappedAsset = ReadField(instance, "asset");
                    return "asset=" + DescribeObjectFields(wrappedAsset, new[] { "type", "asset" });
                }

                return "type=" + type.FullName;
            }
            catch (Exception ex)
            {
                return "<describe_failed " + ex.GetType().Name + ": " + ex.Message + ">";
            }
        }

        private static string DescribeCacheRequest(data_girls.girls._texture texture, string size)
        {
            if (texture == null)
            {
                return "texture=<null> size=" + size;
            }

            string textureString = Safe(() => texture.GetTextureString());
            string filename = SizeName(size);
            string path = Path.Combine(Application.persistentDataPath, "Cache", "Portraits", textureString, filename);
            string rawUri = "file://" + path;
            string canonicalUri = Safe(() => new Uri(path).AbsoluteUri);
            return
                "size=" + size +
                " textureString=" + Quote(textureString) +
                " modName=" + Quote(Safe(() => texture.ModName)) +
                " nonAscii=" + ContainsNonAscii(textureString) +
                " cacheExists=" + Safe(() => File.Exists(path).ToString()) +
                " rawUri=" + Quote(rawUri) +
                " canonicalUri=" + Quote(canonicalUri);
        }

        private static string SizeName(string size)
        {
            if (string.Equals(size, "small", StringComparison.OrdinalIgnoreCase)) return "small.png";
            if (string.Equals(size, "medium", StringComparison.OrdinalIgnoreCase)) return "medium.png";
            if (string.Equals(size, "big", StringComparison.OrdinalIgnoreCase)) return "big.png";
            return size + ".png";
        }

        private static string DescribeGirl(data_girls.girls girl)
        {
            if (girl == null)
            {
                return "<null>";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(DescribeGirlBrief(girl));
            builder.Append(" texture=").Append(DescribeTexture(girl.texture));
            builder.Append(" assets=[");
            try
            {
                if (girl.textureAssets != null)
                {
                    for (int i = 0; i < girl.textureAssets.Count; i++)
                    {
                        if (i > 0) builder.Append("; ");
                        data_girls.girls._textureAsset wrapped = girl.textureAssets[i];
                        builder.Append(wrapped == null ? "<null>" : wrapped.type + ":" + DescribeRawAsset(wrapped.asset));
                    }
                }
            }
            catch (Exception ex)
            {
                builder.Append("<asset_list_failed ").Append(ex.GetType().Name).Append('>');
            }
            builder.Append(']');
            return builder.ToString();
        }

        private static string DescribeGirlBrief(data_girls.girls girl)
        {
            if (girl == null) return "<null>";
            return
                "girl{id=" + Safe(() => girl.id.ToString()) +
                ",name=" + Quote(Safe(() => girl.firstName + " " + girl.lastName)) +
                ",type=" + Safe(() => girl.Type.ToString()) +
                ",age=" + Safe(() => girl.age.ToString()) +
                "}";
        }

        private static string DescribeTexture(data_girls.girls._texture texture)
        {
            if (texture == null) return "<null>";
            return
                "texture{key=" + Quote(Safe(() => texture.GetTextureString())) +
                ",mod=" + Quote(Safe(() => texture.ModName)) +
                ",body=" + Safe(() => texture.body_id) +
                ",hair=" + Safe(() => texture.hair_id) +
                ",face=" + Safe(() => texture.face_id) +
                ",acc=" + Quote(Safe(() => texture.accessory_id)) +
                ",found=" + Safe(() => texture.found.ToString()) +
                ",cached=" + Safe(() => texture.cached.ToString()) +
                "}";
        }

        private static string DescribeRawAsset(data_girls_textures._textureAsset asset)
        {
            if (asset == null) return "<null>";

            string file = asset.path ?? "";
            string dims = ReadPngDimensions(file);
            return
                "asset{type=" + asset.type +
                ",body=" + asset.body_id +
                ",part=" + asset.part_id +
                ",mod=" + Quote(asset.ModName) +
                ",modNonAscii=" + ContainsNonAscii(asset.ModName) +
                ",unique=" + asset.Unique +
                ",rarity=" + Quote(asset.Value) +
                ",age=" + asset.Age +
                ",left=" + asset.left +
                ",top=" + asset.top +
                ",path=" + Quote(file) +
                ",pathNonAscii=" + ContainsNonAscii(file) +
                ",exists=" + Safe(() => File.Exists(file).ToString()) +
                ",bytes=" + Safe(() => File.Exists(file) ? new FileInfo(file).Length.ToString() : "0") +
                ",png=" + dims +
                "}";
        }

        private static string ReadPngDimensions(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "<missing>";
                byte[] header = new byte[24];
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Read(header, 0, header.Length) != header.Length) return "<short>";
                }
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                for (int i = 0; i < signature.Length; i++)
                {
                    if (header[i] != signature[i]) return "<not_png>";
                }
                int width = ReadBigEndianInt32(header, 16);
                int height = ReadBigEndianInt32(header, 20);
                return width + "x" + height;
            }
            catch (Exception ex)
            {
                return "<read_failed " + ex.GetType().Name + ">";
            }
        }

        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static string DescribeQueue()
        {
            try
            {
                IList queue = QueueField == null ? null : QueueField.GetValue(null) as IList;
                object active = ActiveQueueField == null ? null : ActiveQueueField.GetValue(null);
                return "{count=" + (queue == null ? "<null>" : queue.Count.ToString()) + ",active=" + (active ?? "<null>") + "}";
            }
            catch (Exception ex)
            {
                return "<queue_failed " + ex.GetType().Name + ">";
            }
        }

        private static string DescribeAssistantOwnership(Auditions.data data)
        {
            try
            {
                Type tracking = FindTypeQuietly("AssitantManagerMod.AssistantManagerAuditionTracking");
                if (tracking == null) return "assistant_manager_not_loaded";
                FieldInfo ownersField = tracking.GetField("ownerManagerByAudition", BindingFlags.Static | BindingFlags.NonPublic);
                IDictionary owners = ownersField == null ? null : ownersField.GetValue(null) as IDictionary;
                if (owners == null) return "assistant_manager_loaded_owner_map_unavailable";
                if (data == null || !owners.Contains(data)) return "player_or_unowned";
                object owner = owners[data];
                return "manager_id=" + (owner == null ? "<null>" : owner.ToString());
            }
            catch (Exception ex)
            {
                return "assistant_check_failed:" + ex.GetType().Name + ":" + ex.Message;
            }
        }

        private static Type FindTypeQuietly(string fullName)
        {
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
            }
            catch
            {
            }
            return null;
        }

        private static void WritePatchOwners()
        {
            WritePatchOwners(typeof(Auditions), "GenerateGirls");
            WritePatchOwners(typeof(Auditions), "GenerateAudition");
            WritePatchOwners(typeof(data_girls), "GenerateGirl");
            WritePatchOwners(typeof(data_girls), "GenerateParams");
            WritePatchOwners(typeof(data_girls_textures), "AddToQueue");
            WritePatchOwners(typeof(Popup_Audition), "Set");
        }

        private static void WritePatchOwners(Type type, string methodName)
        {
            try
            {
                MethodBase method = AccessTools.Method(type, methodName);
                Patches patches = method == null ? null : Harmony.GetPatchInfo(method);
                if (method == null)
                {
                    Write("PATCH OWNERS " + type.Name + "." + methodName + " method_not_found");
                    return;
                }
                if (patches == null)
                {
                    Write("PATCH OWNERS " + type.Name + "." + methodName + " none");
                    return;
                }

                IEnumerable<string> owners = patches.Owners == null
                    ? Enumerable.Empty<string>()
                    : patches.Owners.OrderBy(owner => owner, StringComparer.Ordinal);
                Write("PATCH OWNERS " + type.Name + "." + methodName + " [" + string.Join(",", owners.ToArray()) + "]");
            }
            catch (Exception ex)
            {
                Write("PATCH OWNERS " + type.Name + "." + methodName + " failed=" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        private static object ReadField(object instance, string name)
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

        private static int ReadIntField(object instance, string name, int fallback)
        {
            try
            {
                object value = ReadField(instance, name);
                return value is int ? (int)value : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string DescribeObjectFields(object value, string[] fieldNames)
        {
            if (value == null) return "<null>";
            StringBuilder builder = new StringBuilder();
            builder.Append(value.GetType().Name).Append('{');
            for (int i = 0; i < fieldNames.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(fieldNames[i]).Append('=').Append(Safe(() => ReadField(value, fieldNames[i])));
            }
            builder.Append('}');
            return builder.ToString();
        }

        private static string DescribeUnityObject(UnityEngine.Object value)
        {
            if (value == null) return "<null>";
            return value.GetType().Name + "#" + value.GetInstanceID() + ":" + value.name;
        }

        private static string DescribeException(Exception exception)
        {
            return exception == null ? "<none>" : exception.GetType().FullName + ": " + exception.Message + " | " + exception.StackTrace;
        }

        private static bool ContainsNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 127) return true;
            }
            return false;
        }

        private static string Quote(string value)
        {
            if (value == null) return "<null>";
            return "\"" + value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"";
        }

        private static string Safe(Func<object> func)
        {
            try
            {
                object value = func();
                return value == null ? "<null>" : value.ToString();
            }
            catch (Exception ex)
            {
                return "<" + ex.GetType().Name + ":" + ex.Message + ">";
            }
        }

        private static string Safe(Func<string> func)
        {
            try
            {
                return func() ?? "<null>";
            }
            catch (Exception ex)
            {
                return "<" + ex.GetType().Name + ":" + ex.Message + ">";
            }
        }
    }

    [HarmonyPatch(typeof(Auditions), "GenerateGirls")]
    internal static class AuditionCrashDiagGenerateGirlsPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(Auditions.data _data)
        {
            AuditionCrashDiagnostics.BeginAudition(_data);
        }

        private static void Postfix(Auditions.data _data)
        {
            AuditionCrashDiagnostics.EndAudition(_data, null);
        }

        private static Exception Finalizer(Auditions.data _data, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.EndAudition(_data, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls), "GenerateGirl")]
    internal static class AuditionCrashDiagGenerateGirlPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(bool genTextures, Auditions.data._girl._type Type, data_girls_textures._textureAsset BodyAsset)
        {
            AuditionCrashDiagnostics.BeginGenerateGirl(genTextures, Type, BodyAsset);
        }

        private static void Postfix(data_girls.girls __result)
        {
            AuditionCrashDiagnostics.EndGenerateGirl(__result, null);
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.EndGenerateGirl(null, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls), "GenerateParams")]
    internal static class AuditionCrashDiagGenerateParamsPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(data_girls.girls Girl, Auditions.data._girl._type Type)
        {
            AuditionCrashDiagnostics.GenerateParamsEnter(Girl, Type);
        }

        private static void Postfix(data_girls.girls Girl)
        {
            AuditionCrashDiagnostics.GenerateParamsExit(Girl, null);
        }

        private static Exception Finalizer(data_girls.girls Girl, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.GenerateParamsExit(Girl, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls_textures), "AddToQueue")]
    internal static class AuditionCrashDiagAddToQueuePatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(data_girls.girls Girl, GameObject Target_Object)
        {
            AuditionCrashDiagnostics.AddToQueueEnter(Girl, Target_Object);
        }

        private static void Postfix(data_girls.girls Girl)
        {
            AuditionCrashDiagnostics.AddToQueueExit(Girl, null);
        }

        private static Exception Finalizer(data_girls.girls Girl, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.AddToQueueExit(Girl, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls_textures), "RunTheQueue")]
    internal static class AuditionCrashDiagRunTheQueuePatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(bool init)
        {
            AuditionCrashDiagnostics.RunQueueEnter(init);
        }

        private static void Postfix(bool init)
        {
            AuditionCrashDiagnostics.RunQueueExit(init, null);
        }

        private static Exception Finalizer(bool init, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.RunQueueExit(init, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(data_girls_textures), "DoTheThing")]
    internal static class AuditionCrashDiagDoTheThingPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(data_girls.girls Girl, GameObject Target_Object)
        {
            AuditionCrashDiagnostics.DoThingEnter(Girl, Target_Object);
        }

        private static void Postfix(data_girls.girls Girl)
        {
            AuditionCrashDiagnostics.DoThingExit(Girl, null);
        }

        private static Exception Finalizer(data_girls.girls Girl, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.DoThingExit(Girl, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Portrait_Renderer), "CacheTextures")]
    internal static class AuditionCrashDiagCacheTexturesPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(Portrait_Renderer __instance, data_girls.girls _Girl)
        {
            AuditionCrashDiagnostics.CacheTexturesEnter(__instance, _Girl);
        }

        private static void Postfix(data_girls.girls _Girl)
        {
            AuditionCrashDiagnostics.CacheTexturesExit(_Girl, null);
        }

        private static Exception Finalizer(data_girls.girls _Girl, Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.CacheTexturesExit(_Girl, __exception);
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Portrait_Renderer), "CreateFiles")]
    internal static class AuditionCrashDiagCreateFilesPatch
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return AuditionCrashDiagnostics.Enabled;
        }

        private static void Prefix(Portrait_Renderer __instance)
        {
            AuditionCrashDiagnostics.CreateFilesEnter(__instance);
        }

        private static void Postfix()
        {
            AuditionCrashDiagnostics.CreateFilesExit(null);
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                AuditionCrashDiagnostics.CreateFilesExit(__exception);
            }
            return __exception;
        }
    }
}
