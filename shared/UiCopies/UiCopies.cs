using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UiCopies
{
    /// <summary>
    /// The game's own parts of a UI object: the paths of the objects under it ("Panel/Text") and the scripts on
    /// the object itself, as read from the game with no mods loaded.
    /// </summary>
    public sealed class VanillaShape
    {
        public readonly HashSet<string> Paths;
        public readonly HashSet<Type> RootComponents;

        public VanillaShape(Type[] rootComponents, string[] paths)
        {
            RootComponents = new HashSet<Type>(rootComponents);
            Paths = new HashSet<string>(paths);
        }
    }

    /// <summary>
    /// Copies a game UI object without what other mods have added to it. Objects under it that the game doesn't
    /// put there are left out, and so are scripts on the object itself (where a mod would put something that runs
    /// the popup or panel). Scripts on the game's own objects inside are kept, so mods that add to every label or
    /// button still apply to the copy.
    /// </summary>
    public static class UiCopy
    {
        public const string HOLDER_NAME = "UiCopyHolder";

        /// <summary>
        /// Copies source under parent, as Object.Instantiate(source, parent, false) does, then leaves out what isn't
        /// in shape. The copy is made under an inactive holder, so nothing on it wakes up before the extras are
        /// removed; the source is never changed. A part that can't be removed is logged and kept.
        /// </summary>
        /// <param name="logPrefix">The mod's log prefix, such as "[ModMenus]".</param>
        /// <param name="what">What the copy is, for the log, such as "difficulty settings popup".</param>
        public static GameObject CopyGameObject(GameObject source, Transform parent, VanillaShape shape, string logPrefix, string what)
        {
            GameObject holder = new(HOLDER_NAME, typeof(RectTransform));
            holder.SetActive(false);
            try
            {
                GameObject copy = Object.Instantiate(source, holder.transform, false);
                RemoveUnknownChildren(copy.transform, null, shape, logPrefix, what);
                RemoveUnknownComponents(copy, shape, logPrefix, what);
                copy.transform.SetParent(parent, false);
                return copy;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// Removes each object under node whose path isn't in the shape, and looks inside the ones that are.
        /// </summary>
        private static void RemoveUnknownChildren(Transform node, string path, VanillaShape shape, string logPrefix, string what)
        {
            for (int i = node.childCount - 1; i >= 0; i--)
            {
                Transform child = node.GetChild(i);
                string childPath = path == null ? child.name : path + "/" + child.name;
                if (shape.Paths.Contains(childPath))
                    RemoveUnknownChildren(child, childPath, shape, logPrefix, what);
                else
                    Remove(child.gameObject, "'" + childPath + "'", logPrefix, what);
            }
        }

        /// <summary>
        /// Removes the scripts on the copy itself that the game doesn't put there, last first, so a script another
        /// one requires goes after it.
        /// </summary>
        private static void RemoveUnknownComponents(GameObject copy, VanillaShape shape, string logPrefix, string what)
        {
            Component[] components = copy.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                // A missing script has no type to check, and the transform can't be removed
                if (component == null || component is Transform || shape.RootComponents.Contains(component.GetType()))
                    continue;
                Remove(component, "the " + component.GetType().Name + " script", logPrefix, what);
            }
        }

        private static void Remove(Object part, string name, string logPrefix, string what)
        {
            try
            {
                Object.DestroyImmediate(part);
            }
            catch (Exception e)
            {
                Debug.LogWarning(logPrefix + " Couldn't leave " + name + " out of the copied " + what + ": " + e.Message);
                return;
            }
            Debug.Log(logPrefix + " Left another mod's " + name + " out of the copied " + what + ".");
        }
    }
}
