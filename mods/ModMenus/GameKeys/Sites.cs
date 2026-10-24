using System;
using UnityEngine;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// What the game's key reads call instead (see SiteTranspiler): the key row's key, or, if ModMenus has no
    /// such row (its menu file couldn't be read, say), the game's own key, read as the game reads it. These run
    /// inside the game's own Update methods, so an error reading the row also gives the game's own key, and is
    /// logged once.
    /// </summary>
    public static class Sites
    {
        private const int PRESSED = 0, HELD = 1, RELEASED = 2;

        private static bool errorLogged;

        public static bool GetKeyDown(KeyCode key, string varID) =>
            Read(varID, PRESSED, out bool pressed) ? pressed : Input.GetKeyDown(key);

        public static bool GetKey(KeyCode key, string varID) =>
            Read(varID, HELD, out bool held) ? held : Input.GetKey(key);

        public static bool GetKeyUp(KeyCode key, string varID) =>
            Read(varID, RELEASED, out bool released) ? released : Input.GetKeyUp(key);

        public static bool GetKeyUpByName(string name, string varID) =>
            Read(varID, RELEASED, out bool released) ? released : Input.GetKeyUp(name);

        public static bool GetMouseButtonDown(int button, string varID) =>
            Read(varID, PRESSED, out bool pressed) ? pressed : Input.GetMouseButtonDown(button);

        /// <summary>
        /// Reads the key row's key.
        /// </summary>
        /// <returns>False if there's no such row, or reading it failed: the game reads its own key.</returns>
        private static bool Read(string varID, int kind, out bool result)
        {
            result = false;
            try
            {
                if (!ModMenusUtils.HasKeybind(varID))
                {
                    return false;
                }
                result = kind == PRESSED ? ModMenusUtils.KeybindPressed(varID)
                    : kind == HELD ? ModMenusUtils.KeybindHeld(varID)
                    : ModMenusUtils.KeybindReleased(varID);
                return true;
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Debug.LogError("[ModMenus] Couldn't read a key row's key, so the game reads its own key: " + e);
                }
                return false;
            }
        }
    }
}
