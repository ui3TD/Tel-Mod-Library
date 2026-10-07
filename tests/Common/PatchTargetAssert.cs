using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace TelModTests.Common
{
    /// <summary>
    /// Level 1 checks: every Harmony patch class in a namespace still matches the game.
    /// </summary>
    public static class PatchTargetAssert
    {
        /// <summary>
        /// Returns every class in the namespace that carries a [HarmonyPatch] attribute.
        /// </summary>
        public static List<Type> PatchClasses(Assembly assembly, string ns)
        {
            return assembly.GetTypes()
                .Where(t => t.Namespace == ns && t.IsClass)
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                .ToList();
        }

        /// <summary>
        /// Asserts each patch class resolves to an existing game method.
        /// </summary>
        public static void TargetsResolve(Assembly assembly, string ns)
        {
            List<Type> classes = PatchClasses(assembly, ns);
            Assert.NotEmpty(classes);

            List<string> failures = new();
            foreach (Type patchClass in classes)
            {
                HarmonyMethod info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
                MethodBase target = ResolveTarget(info);
                if (target == null)
                    failures.Add($"{patchClass.Name}: {info.declaringType?.FullName ?? "?"}.{info.methodName ?? "?"} not found");
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Applies every patch in the namespace for real, which also validates injected
        /// parameter names and types, then removes them again.
        /// </summary>
        public static void PatchesApply(Assembly assembly, string ns)
        {
            Harmony harmony = new("tests." + ns);
            try
            {
                foreach (Type patchClass in PatchClasses(assembly, ns))
                {
                    try
                    {
                        harmony.CreateClassProcessor(patchClass).Patch();
                    }
                    catch (Exception e)
                    {
                        Assert.Fail($"{patchClass.Name} failed to patch: {e.InnerException?.Message ?? e.Message}");
                    }
                }
            }
            finally
            {
                harmony.UnpatchSelf();
            }
        }

        private static MethodBase ResolveTarget(HarmonyMethod info)
        {
            if (info.declaringType == null)
                return null;

            switch (info.methodType ?? MethodType.Normal)
            {
                case MethodType.Constructor:
                    return AccessTools.Constructor(info.declaringType, info.argumentTypes);
                case MethodType.Getter:
                    return AccessTools.PropertyGetter(info.declaringType, info.methodName);
                case MethodType.Setter:
                    return AccessTools.PropertySetter(info.declaringType, info.methodName);
                default:
                    return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
            }
        }
    }
}
