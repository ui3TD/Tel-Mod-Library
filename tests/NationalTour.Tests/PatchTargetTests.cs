using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TelModTests.Common;
using Xunit;

namespace NationalTour.Tests
{
    public class PatchTargetTests
    {
        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Pictures).Assembly, TestGame.ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Pictures).Assembly, TestGame.ModNamespace);
        }

        /// <summary>
        /// The game runs HarmonyX, which reads a patch parameter named like the original's own "__" parameter
        /// (the game's __country) as a special argument and drops every patch of the mod. Such parameters are
        /// taken by position (__0) instead.
        /// </summary>
        [Fact]
        public void OriginalParametersStartingWithUnderscores_AreTakenByPosition()
        {
            List<string> failures = new();
            foreach (System.Type patchClass in PatchTargetAssert.PatchClasses(typeof(Pictures).Assembly, TestGame.ModNamespace))
            {
                HarmonyMethod info = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));
                MethodBase original = AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
                HashSet<string> originalNames = new(original.GetParameters().Select(p => p.Name).Where(n => n.StartsWith("__")));
                foreach (MethodInfo patch in patchClass.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    foreach (ParameterInfo parameter in patch.GetParameters().Where(p => originalNames.Contains(p.Name)))
                        failures.Add($"{patchClass.Name}.{patch.Name}({parameter.Name})");
                }
            }
            Assert.True(failures.Count == 0, string.Join(", ", failures));
        }
    }
}
