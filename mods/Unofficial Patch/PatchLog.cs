using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace UnofficialPatch
{
    // Centralized logging utilities for Unofficial Patch patches.
    internal static class PatchLog
    {
        // Shared logging prefix for Unofficial Patch output.
        private const string LogPrefix = "[UnofficialPatch] ";
        // Marker used when Harmony metadata cannot be read.
        private const string UnknownTarget = "UnknownTarget";

        // Protects the once-only warning cache.
        private static readonly object OnceLock = new object();
        // Tracks warnings that were already emitted to prevent log spam.
        private static readonly HashSet<string> OnceKeys = new HashSet<string>();

        // Logs a warning for the given patch type.
        public static void Warn<TPatch>(string message)
        {
            Warn(typeof(TPatch), message);
        }

        // Logs an error for the given patch type, including the resolved Harmony target.
        public static void Error<TPatch>(string message)
        {
            bool usedFallback;
            string target = GetTargetName(typeof(TPatch), out usedFallback);
            Debug.LogError(LogPrefix + target + ": " + message);
        }

        // Set by the tests, so a patch that can't be applied fails the test instead of being logged.
        internal static bool RethrowPatchFailures = false;

        // Harmony's Cleanup for every patch class. A patch that can't be applied (e.g. another mod's transpiler
        // changed the method first) is logged and skipped, so the mod's other fixes still apply.
        public static Exception PatchFailed<TPatch>(Exception ex)
        {
            if (ex == null || RethrowPatchFailures)
                return ex;
            Error<TPatch>("couldn't be applied, so this fix is off: " + (ex.InnerException?.Message ?? ex.Message));
            return null;
        }

        // Warns, once per patch, that a result another mod had already changed was left as it was.
        public static void WarnResultChanged<TPatch>()
        {
            WarnOncePerPatch<TPatch>("the game's result was already changed by another mod; left unchanged.");
        }

        // Logs a warning once per patch + message.
        public static void WarnOnce<TPatch>(string message)
        {
            WarnOnce(typeof(TPatch), message);
        }

        // Logs a warning once per patch type, regardless of message.
        public static void WarnOncePerPatch<TPatch>(string message)
        {
            WarnOncePerPatch(typeof(TPatch), message);
        }

        // Logs a warning for the given patch type, including the resolved Harmony target if available.
        public static void Warn(Type patchType, string message)
        {
            bool usedFallback;
            string target = GetTargetName(patchType, out usedFallback);
            string prefix = LogPrefix + target;
            if (usedFallback && patchType != null)
            {
                prefix += " (patch: " + patchType.Name + ")";
            }
            Debug.LogWarning(prefix + ": " + message);
        }

        // Logs a warning once per patch + message combination.
        public static void WarnOnce(Type patchType, string message)
        {
            string key = GetOnceKey(patchType, message);
            lock (OnceLock)
            {
                if (OnceKeys.Contains(key))
                    return;
                OnceKeys.Add(key);
            }
            Warn(patchType, message);
        }

        // Logs a warning once per patch type to avoid repeated spam.
        public static void WarnOncePerPatch(Type patchType, string message)
        {
            string key = GetOnceKey(patchType, string.Empty);
            lock (OnceLock)
            {
                if (OnceKeys.Contains(key))
                    return;
                OnceKeys.Add(key);
            }
            Warn(patchType, message);
        }

        // Builds a readable Harmony target name from the patch class's [HarmonyPatch] attributes, falling back to
        // UnknownTarget if it has none.
        private static string GetTargetName(Type patchType, out bool usedFallback)
        {
            usedFallback = true;
            if (patchType == null)
                return UnknownTarget;

            List<string> targets = patchType.GetCustomAttributes(typeof(HarmonyPatch), true)
                .Cast<HarmonyPatch>()
                .Select(attribute => GetTargetName(attribute.info))
                .Where(target => !string.IsNullOrEmpty(target))
                .Distinct()
                .ToList();
            if (targets.Count == 0)
                return UnknownTarget;

            usedFallback = false;
            return string.Join(", ", targets.ToArray());
        }

        // One attribute's target, from Harmony's public HarmonyMethod info.
        private static string GetTargetName(HarmonyMethod info)
        {
            if (info == null)
                return null;

            string typeName = info.declaringType != null ? (info.declaringType.FullName ?? info.declaringType.Name) : null;
            string signature = FormatArgs(info.argumentTypes);

            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(info.methodName))
                return typeName + "." + info.methodName + signature;

            if (!string.IsNullOrEmpty(typeName))
                return typeName;

            if (!string.IsNullOrEmpty(info.methodName))
                return info.methodName + signature;

            return info.methodType?.ToString();
        }

        // Formats argument type lists to help log target signatures.
        private static string FormatArgs(Type[] argumentTypes)
        {
            if (argumentTypes == null)
                return string.Empty;
            if (argumentTypes.Length == 0)
                return "()";
            return "(" + string.Join(", ", argumentTypes.Select(t => t != null ? t.Name : "null")) + ")";
        }

        // Builds a stable cache key for once-only warnings.
        private static string GetOnceKey(Type patchType, string message)
        {
            string typeName = patchType != null ? patchType.FullName : "null";
            return typeName + "|" + message;
        }
    }
}
