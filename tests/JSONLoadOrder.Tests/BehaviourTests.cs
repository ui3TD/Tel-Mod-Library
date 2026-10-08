using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JSONLoadOrder.Tests
{
    /// <summary>
    /// Reading each mod's JSONLoadOrder from its info.json.
    /// </summary>
    public class ReadOrderTests
    {
        public ReadOrderTests() => TestGame.Reset();

        [Theory]
        [InlineData("100", 100)]
        [InlineData("0", 0)]
        [InlineData("-5", -5)]
        [InlineData("\"7\"", 7)]
        public void WholeNumber_IsTheModsOrder(string json, int expected)
        {
            TestGame.Mod("A", json);

            TestGame.LoadLanguage();

            Assert.Equal(expected, ModLoadOrder.modOrders["A"]);
        }

        [Fact]
        public void NoJSONLoadOrder_DefaultsToZero()
        {
            TestGame.Mod("A");

            TestGame.LoadLanguage();

            Assert.Equal(0, ModLoadOrder.modOrders["A"]);
        }

        /// <summary>
        /// Anything that isn't a whole number is treated as the default instead of breaking the load.
        /// </summary>
        [Theory]
        [InlineData("1.5")]
        [InlineData("\"late\"")]
        [InlineData("\"\"")]
        [InlineData("true")]
        [InlineData("null")]
        [InlineData("[1]")]
        [InlineData("{\"order\": 1}")]
        [InlineData("99999999999")]
        public void NotAWholeNumber_DefaultsToZero(string json)
        {
            TestGame.Mod("A", json);

            TestGame.LoadLanguage();

            Assert.Equal(0, ModLoadOrder.modOrders["A"]);
        }

        /// <summary>
        /// The field name is case-sensitive, as the guide spells it.
        /// </summary>
        [Fact]
        public void MisspelledField_IsIgnored()
        {
            TestGame.ModWithInfo("A", "{\"jsonLoadOrder\": 100}");

            TestGame.LoadLanguage();

            Assert.Equal(0, ModLoadOrder.modOrders["A"]);
        }

        /// <summary>
        /// Mod folders can come with a trailing separator (Steam's install path); info.json is still found.
        /// </summary>
        [Fact]
        public void PathWithTrailingSeparator_StillReadsInfoJson()
        {
            Mods._mod mod = TestGame.Mod("A", "3");
            mod.Path += Path.DirectorySeparatorChar;

            TestGame.LoadLanguage();

            Assert.Equal(3, ModLoadOrder.modOrders["A"]);
        }

        /// <summary>
        /// Every mod gets an order, including mods without a DLL or JSONLoadOrder (e.g. pure JSON mods).
        /// </summary>
        [Fact]
        public void EveryLoadedMod_GetsAnOrder()
        {
            TestGame.Mod("A", "1");
            TestGame.Mod("B");
            TestGame.ModWithInfo("C", "{}");

            TestGame.LoadLanguage();

            Assert.Equal(new Dictionary<string, int> { ["A"] = 1, ["B"] = 0, ["C"] = 0 }, ModLoadOrder.modOrders);
        }
    }

    /// <summary>
    /// Sorting the game's mod list, which every JSON load reads in order.
    /// </summary>
    public class SortTests
    {
        public SortTests() => TestGame.Reset();

        [Fact]
        public void LowerNumbersLoadFirst()
        {
            TestGame.Mod("Late", "100");
            TestGame.Mod("Default");
            TestGame.Mod("Early", "-10");
            TestGame.Mod("Middle", "5");

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "Early", "Default", "Middle", "Late" }, TestGame.LoadedNames());
        }

        /// <summary>
        /// Mods with the same number keep the order the game loaded them in.
        /// </summary>
        [Fact]
        public void EqualNumbers_KeepTheGamesOrder()
        {
            TestGame.Mod("C");
            TestGame.Mod("A", "1");
            TestGame.Mod("B");
            TestGame.Mod("D", "1");
            TestGame.Mod("E", "0");

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "C", "B", "E", "A", "D" }, TestGame.LoadedNames());
        }

        [Fact]
        public void SortingKeepsEveryMod()
        {
            Mods._mod a = TestGame.Mod("A", "2");
            Mods._mod b = TestGame.Mod("B", "1");
            Mods._mod c = TestGame.Mod("C");

            TestGame.LoadLanguage();

            Assert.Equal(new[] { c, b, a }, Mods._Mods);
        }

        /// <summary>
        /// Disabled mods are still sorted, so turning one back on doesn't need a re-sort to put it in place.
        /// </summary>
        [Fact]
        public void DisabledMods_AreSortedToo()
        {
            TestGame.Mod("A", "5");
            Mods._mod b = TestGame.Mod("B", "-5");
            TestGame.Disable(b);

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "B", "A" }, TestGame.LoadedNames());
        }

        [Fact]
        public void NoMods_LeavesTheListAlone()
        {
            List<Mods._mod> before = Mods._Mods;

            TestGame.LoadLanguage();

            Assert.Same(before, Mods._Mods);
            Assert.Empty(ModLoadOrder.modOrders);
        }

        /// <summary>
        /// The game reloads language data again when the player changes language; sorting again changes nothing.
        /// </summary>
        [Fact]
        public void SortingAgain_GivesTheSameOrder()
        {
            TestGame.Mod("C", "1");
            TestGame.Mod("A");
            TestGame.Mod("B", "-1");
            TestGame.Mod("D");

            TestGame.LoadLanguage();
            List<string> first = TestGame.LoadedNames();
            TestGame.LoadLanguage();

            Assert.Equal(first, TestGame.LoadedNames());
        }

        /// <summary>
        /// A changed info.json is read again on the next language load.
        /// </summary>
        [Fact]
        public void ChangedInfoJson_IsReadOnTheNextLoad()
        {
            Mods._mod a = TestGame.Mod("A", "1");
            TestGame.Mod("B", "2");
            TestGame.LoadLanguage();

            File.WriteAllText(Path.Combine(a.Path, "info.json"), "{\"JSONLoadOrder\": 3}");
            TestGame.LoadLanguage();

            Assert.Equal(new[] { "B", "A" }, TestGame.LoadedNames());
            Assert.Equal(3, ModLoadOrder.modOrders["A"]);
        }
    }

    /// <summary>
    /// The sorted list is what the game's JSON loading reads: files are loaded in list order, so a later mod's
    /// entries overwrite an earlier mod's, and Mods.GetFilePath picks the last mod that has the file.
    /// </summary>
    public class FileOrderTests
    {
        public FileOrderTests() => TestGame.Reset();

        private static List<string> ConstantsFiles() =>
            Mods.GetFilePaths(Path.Combine("JSON", "Constants", "constants.json"), OnlyMods: true)
                .Select(path => Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))))
                .ToList();

        [Fact]
        public void ModFiles_LoadInJSONLoadOrder()
        {
            TestGame.Mod("Translation", "100");
            TestGame.Mod("Content");
            TestGame.Mod("Base", "-1");

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "Base", "Content", "Translation" }, ConstantsFiles());
        }

        /// <summary>
        /// The guide's use case: a translation that loads after the mod it translates, though the game found it first.
        /// </summary>
        [Fact]
        public void HigherNumber_LoadsAfterTheModItChanges()
        {
            TestGame.Mod("AAA Translation", "1");
            TestGame.Mod("ZZZ Content");

            Assert.Equal(new[] { "AAA Translation", "ZZZ Content" }, ConstantsFiles());

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "ZZZ Content", "AAA Translation" }, ConstantsFiles());
        }

        [Fact]
        public void DisabledMods_AreStillSkipped()
        {
            TestGame.Mod("A", "1");
            Mods._mod b = TestGame.Mod("B");
            TestGame.Disable(b);

            TestGame.LoadLanguage();

            Assert.Equal(new[] { "A" }, ConstantsFiles());
        }
    }
}
