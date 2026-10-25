using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InGameTests.TelMods
{
    /// <summary>
    /// Checks a mod's VanillaShape (the shared UiCopies helper, compiled into each mod that copies a game UI
    /// object) against the game's real object. The shape lists the game's parts, read with no mods loaded; the
    /// copy leaves out everything else. Only the real object can show the list is complete: any part of it
    /// missing from the shape would be left out of the mod's copy.
    /// </summary>
    internal static class VanillaShapes
    {
        public static void AssertCovers(TestContext ctx, Type owner, string field, GameObject gameObject, string what)
        {
            object shape = owner?.GetField(field)?.GetValue(null);
            if (shape == null)
            {
                ctx.Fail(owner + " has no " + field);
                return;
            }
            HashSet<string> paths = (HashSet<string>)shape.GetType().GetField("Paths").GetValue(shape);
            HashSet<Type> rootComponents = (HashSet<Type>)shape.GetType().GetField("RootComponents").GetValue(shape);

            string[] missingPaths = PathsUnder(gameObject.transform, null).Where(p => !paths.Contains(p)).ToArray();
            string[] missingComponents = gameObject.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform) && !rootComponents.Contains(c.GetType()))
                .Select(c => c.GetType().Name).ToArray();
            ctx.Assert(missingPaths.Length == 0, "The game's " + what + " has parts " + field + " doesn't list: " + string.Join(", ", missingPaths));
            ctx.Assert(missingComponents.Length == 0, "The game's " + what + " has scripts " + field + " doesn't list: " + string.Join(", ", missingComponents));
        }

        private static IEnumerable<string> PathsUnder(Transform node, string path)
        {
            foreach (Transform child in node)
            {
                string childPath = path == null ? child.name : path + "/" + child.name;
                yield return childPath;
                foreach (string below in PathsUnder(child, childPath))
                    yield return below;
            }
        }
    }
}
