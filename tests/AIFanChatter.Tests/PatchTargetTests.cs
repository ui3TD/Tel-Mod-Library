using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TelModTests.Common;
using Xunit;

namespace AIFanChatter.Tests
{
    public class PatchTargetTests
    {
        private const string ModNamespace = "AIFanChatter";

        [Fact]
        public void AllPatchTargetsExistInGame()
        {
            PatchTargetAssert.TargetsResolve(typeof(Settings).Assembly, ModNamespace);
        }

        [Fact]
        public void AllPatchesApplyToGame()
        {
            PatchTargetAssert.PatchesApply(typeof(Settings).Assembly, ModNamespace);
        }

        /// <summary>
        /// The mod reads its settings through the game's variables, never ModMenus' code, so it loads without it.
        /// </summary>
        [Fact]
        public void ModDll_DoesNotReferenceModMenus()
        {
            string dll = ModDll();
            Assert.True(File.Exists(dll), "Build the mod in Release first: " + dll);
            AssemblyName[] references = Assembly.ReflectionOnlyLoadFrom(Path.GetFullPath(dll)).GetReferencedAssemblies();
            Assert.DoesNotContain(references, r => r.Name.Contains("modmenus"));
        }

        private static string ModDll([CallerFilePath] string testFile = "") =>
            Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "AI Fan Chatter", "bin", "Release", "net46", "com.tel.aifanchatter.dll");
    }
}
