using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MenuHotkeys
{
    /// <summary>
    /// The player's keys from ModMenus' key rows, found by name, so the mod also runs without ModMenus (or with a
    /// ModMenus too old to have key rows): then each key is its default. FastForward has the same helper.
    /// </summary>
    public static class ModMenusKeys
    {
        public static Func<string, bool> HasKeybind;
        public static Func<string, bool> KeybindPressed;

        private static bool looked;
        private static bool waiting;

        /// <summary>
        /// Whether the key row's key was pressed this frame: the player's key while ModMenus has the row,
        /// otherwise the default key.
        /// </summary>
        public static bool Pressed(string varID, KeyCode key)
        {
            Find();
            return HasKeybind != null && HasKeybind(varID) ? KeybindPressed(varID) : Input.GetKeyDown(key);
        }

        /// <summary>
        /// Finds ModMenus. Mods load one by one, so until it's found it's looked for again whenever another
        /// assembly loads.
        /// </summary>
        public static void Find()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            Type utils = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ModMenus.ModMenusUtils", false))
                .FirstOrDefault(type => type != null);
            Func<string, bool> has = Method(utils, "HasKeybind");
            Func<string, bool> pressed = Method(utils, "KeybindPressed");
            if (has == null || pressed == null)
            {
                if (!waiting)
                {
                    waiting = true;
                    AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => looked = HasKeybind != null;
                }
                return;
            }
            HasKeybind = has;
            KeybindPressed = pressed;
        }

        private static Func<string, bool> Method(Type type, string name)
        {
            MethodInfo method = type?.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            return method != null && method.ReturnType == typeof(bool)
                ? (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), method)
                : null;
        }

        /// <summary>
        /// For tests: forget ModMenus, and whether it was looked for.
        /// </summary>
        public static void Reset(bool looked)
        {
            HasKeybind = KeybindPressed = null;
            ModMenusKeys.looked = looked;
        }
    }
}
