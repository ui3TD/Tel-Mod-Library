using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using TelModTests.Common;
using Xunit;

namespace HarmonyChecker.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "HarmonyChecker";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(HarmonyCheckerStatus).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(HarmonyCheckerStatus).Assembly, ModNamespace);
        }

        /// <summary>
        /// The late refresh relies on the game's mod-loading coroutine stopping the spinner after LoadMods,
        /// which is where IM-HI applies the Workshop mods' patches (this one included).
        /// </summary>
        [Fact]
        public void ModLoading_StopsSpinnerAfterLoadingMods()
        {
            // Read with Cecil: the coroutine also references Steam types whose assembly the tests don't load.
            using ModuleDefinition module = ModuleDefinition.ReadModule(typeof(Mods).Assembly.Location);
            TypeDefinition coroutine = module.GetType(nameof(Mods)).NestedTypes.Single(t => t.Name.Contains(nameof(Mods.LoadModsCoroutine)));
            List<MethodReference> calls = coroutine.Methods.Single(m => m.Name == "MoveNext").Body.Instructions
                .Select(i => i.Operand as MethodReference)
                .Where(m => m != null)
                .ToList();

            int loadMods = calls.FindIndex(m => m.DeclaringType.Name == nameof(Mods) && m.Name == nameof(Mods.LoadMods));
            int stopSpinner = calls.FindIndex(m => m.DeclaringType.Name == nameof(Mods) && m.Name == nameof(Mods.StopSpinner));
            Assert.True(loadMods >= 0, "LoadModsCoroutine no longer calls LoadMods");
            Assert.True(stopSpinner > loadMods, "LoadModsCoroutine no longer calls StopSpinner after LoadMods");
        }
    }
}
