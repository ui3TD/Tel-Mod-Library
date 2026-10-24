using UnityEngine;

namespace ModMenus.GameKeys
{
    /// <summary>
    /// What the game's key reads call instead (see SiteTranspiler): the key row's key, or, if ModMenus has no
    /// such row (its menu file couldn't be read, say), the game's own key, read as the game reads it.
    /// </summary>
    public static class Sites
    {
        public static bool GetKeyDown(KeyCode key, string varID) =>
            ModMenusUtils.HasKeybind(varID) ? ModMenusUtils.KeybindPressed(varID) : Input.GetKeyDown(key);

        public static bool GetKey(KeyCode key, string varID) =>
            ModMenusUtils.HasKeybind(varID) ? ModMenusUtils.KeybindHeld(varID) : Input.GetKey(key);

        public static bool GetKeyUp(KeyCode key, string varID) =>
            ModMenusUtils.HasKeybind(varID) ? ModMenusUtils.KeybindReleased(varID) : Input.GetKeyUp(key);

        public static bool GetKeyUpByName(string name, string varID) =>
            ModMenusUtils.HasKeybind(varID) ? ModMenusUtils.KeybindReleased(varID) : Input.GetKeyUp(name);

        public static bool GetMouseButtonDown(int button, string varID) =>
            ModMenusUtils.HasKeybind(varID) ? ModMenusUtils.KeybindPressed(varID) : Input.GetMouseButtonDown(button);
    }
}
