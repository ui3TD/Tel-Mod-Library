using System.Collections.Generic;
using UnityEngine;

namespace NationalTour
{
    /// <summary>
    /// Which modules are working. A module that can't find what it patches (a screen object, or a transpiler's
    /// anchor) reports it here, and the features that need it stay off: national tours are never half on.
    /// </summary>
    public static class Readiness
    {
        public enum Module
        {
            Pictures,
            Prefectures,
            Text,
            NationalRecord,
            NationalTab,
            NewTourMap,
            ResultsPictures,
            NationalHandler,
            Routing,
            TourMethods,
            Labels,
            Save,
        }

        /// <summary>
        /// The modules this build has.
        /// </summary>
        public static readonly HashSet<Module> Built = new()
        {
            Module.Pictures, Module.Prefectures, Module.Text, Module.NationalRecord,
            Module.NationalTab, Module.NewTourMap, Module.ResultsPictures,
            Module.NationalHandler, Module.Routing, Module.TourMethods, Module.Labels, Module.Save,
        };

        /// <summary>
        /// A national tour can be created: it has a handler of its own, the game's buttons reach it, its tour
        /// methods use the national record, and it is saved.
        /// </summary>
        public static bool CanCreateNationalTours() =>
            AreReady(Module.NationalHandler, Module.Routing, Module.TourMethods, Module.Save) && NationalHandler.Instance is not null;

        private static readonly Dictionary<Module, string> Failed = new();

        public static bool IsReady(Module module) => Built.Contains(module) && !Failed.ContainsKey(module);

        public static bool AreReady(params Module[] modules)
        {
            foreach (Module module in modules)
            {
                if (!IsReady(module))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Turns a module off for the rest of the session. Logged once per module.
        /// </summary>
        public static void Fail(Module module, string why)
        {
            if (Failed.ContainsKey(module))
                return;
            Failed[module] = why;
            Debug.LogError($"[National Tour] {module} is off: {why}. National tours that need it stay off; world tours are unaffected");
        }

        public static string WhyFailed(Module module) => Failed.TryGetValue(module, out string why) ? why : null;

        /// <summary>
        /// For tests.
        /// </summary>
        internal static void Reset() => Failed.Clear();
    }
}
