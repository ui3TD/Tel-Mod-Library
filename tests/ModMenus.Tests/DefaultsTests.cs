using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ModMenus.Tests
{
    /// <summary>
    /// When a game starts or loads, every setting without a saved value gets its default, so mods read the
    /// menu's default even if the player never opens Mod Settings.
    /// </summary>
    public class DefaultsTests
    {
        public DefaultsTests() => TestGame.Reset();

        private const string MenuJson = """
            [
                { "type": "text", "labelID": "TEST__INTRO" },
                { "type": "slider", "varID": "A_Volume", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 5 },
                { "type": "slider", "varID": "A_Plain", "labelID": "TEST__VOLUME" },
                { "type": "slider", "varID": "A_Half", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20 },
                { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD", "defaultValue": true },
                { "type": "checkbox", "varID": "A_Quiet", "labelID": "TEST__LOUD" },
                { "type": "dropdown", "varID": "A_Pick", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND", "TEST__THIRD"], "defaultValue": 1 },
                { "type": "dropdown", "varID": "A_First", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND"] },
                { "type": "dropdown", "varID": "A_Past", "labelID": "TEST__PICK", "itemIDList": ["TEST__FIRST", "TEST__SECOND"], "defaultValue": 7 },
                { "type": "input", "varID": "A_Motto", "labelID": "TEST__MOTTO", "defaultValue": "Hello world" },
                { "type": "input", "varID": "A_Blank", "labelID": "TEST__MOTTO" },
                { "type": "slider", "varID": "A_Stepped", "labelID": "TEST__VOLUME", "minValue": 0, "maxValue": 100, "defaultValue": 12, "step": 5 },
                { "type": "input", "varID": "A_Seats", "labelID": "TEST__MOTTO", "inputType": "integer", "minValue": 1, "maxValue": 99, "defaultValue": "250" },
                { "type": "input", "varID": "A_Rate", "labelID": "TEST__MOTTO", "inputType": "decimal", "minValue": 0.5 },
                { "type": "checkbox", "varID": "A_Hidden", "labelID": "TEST__LOUD", "defaultValue": true, "enabledIf": "A_Quiet", "reset": false }
            ]
            """;

        private static Dictionary<string, string> AllSaved() => variables.variable.ToDictionary(v => v.name, v => v.value);

        [Fact]
        public void EverySettingGetsItsDefault()
        {
            TestGame.AddMod("Mod A", MenuJson);

            ModMenusUtils.SaveMissingDefaults();

            Assert.Equal(new Dictionary<string, string>
            {
                ["A_Volume"] = "5",
                ["A_Plain"] = "50",
                ["A_Half"] = "10",
                ["A_Loud"] = "1",
                ["A_Quiet"] = "0",
                ["A_Pick"] = "1",
                ["A_First"] = "0",
                ["A_Past"] = "1",
                ["A_Motto"] = "Hello world",
                ["A_Blank"] = "",
                // A stepped slider's default moves to its nearest step, and a number field's is checked as typing is
                ["A_Stepped"] = "10",
                ["A_Seats"] = "99",
                ["A_Rate"] = "0.5",
                // A row hidden by enabledIf still has its default
                ["A_Hidden"] = "1",
            }, AllSaved());
        }

        /// <summary>
        /// The defaults are the values Apply saves when the player opens the menu and changes nothing.
        /// </summary>
        [Fact]
        public void DefaultsAreWhatApplySavesUnchanged()
        {
            TestGame.AddMod("Mod A", MenuJson);
            Menu menu = TestGame.BuildMenu();
            menu.Open();
            menu.Apply();
            Dictionary<string, string> applied = AllSaved();

            variables.variable.Clear();
            ModMenusUtils.SaveMissingDefaults();

            Assert.Equal(applied, AllSaved());
        }

        [Fact]
        public void SavedSettingsAreKept()
        {
            TestGame.AddMod("Mod A", MenuJson);
            TestGame.Save("A_Volume", "12");
            TestGame.Save("A_Loud", "0");

            ModMenusUtils.SaveMissingDefaults();

            Assert.Equal("12", TestGame.Saved("A_Volume"));
            Assert.Equal("0", TestGame.Saved("A_Loud"));
            Assert.Equal("10", TestGame.Saved("A_Half"));
            Assert.Single(variables.variable, v => v.name == "A_Volume");
        }

        /// <summary>
        /// Settings the menu leaves out get no default either.
        /// </summary>
        [Fact]
        public void SettingsTheMenuLeavesOutGetNone()
        {
            TestGame.AddMod("Mod A", """
                [
                    { "type": "slider", "varID": "A_Ignored", "labelID": "TEST__VOLUME", "ignore": true },
                    { "type": "slider", "labelID": "TEST__VOLUME" },
                    { "type": "slider", "varID": "A_NoLabel" },
                    { "type": "slider", "varID": "A_OutOfRange", "labelID": "TEST__VOLUME", "minValue": 1, "maxValue": 20, "defaultValue": 30 },
                    { "type": "dropdown", "varID": "A_NoItems", "labelID": "TEST__PICK" },
                    { "type": "text", "varID": "A_Text", "labelID": "TEST__INTRO" },
                    { "type": "input", "labelID": "TEST__MOTTO", "defaultValue": "Hello" },
                    { "type": "input", "varID": "A_InputNoLabel", "defaultValue": "Hello" },
                    { "type": "input", "varID": "A_InputIgnored", "labelID": "TEST__MOTTO", "ignore": true },
                    { "type": "colour", "varID": "A_Unknown", "labelID": "TEST__INTRO" }
                ]
                """);

            ModMenusUtils.SaveMissingDefaults();

            Assert.Empty(variables.variable);
        }

        [Fact]
        public void DisabledModsAndBrokenFilesGetNone()
        {
            TestGame.AddMod("Mod A", """[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]""", enabled: false);
            TestGame.AddMod("Mod B", """[ { "type": "checkbox", "varID": """);
            TestGame.AddMod("Mod C", """[ { "type": "checkbox", "varID": "C_Loud", "labelID": "TEST__LOUD" } ]""");

            ModMenusUtils.SaveMissingDefaults();

            Assert.Equal(new Dictionary<string, string> { ["C_Loud"] = "0" }, AllSaved());
        }

        /// <summary>
        /// The defaults are saved when a game starts and when a save loads.
        /// </summary>
        [Fact]
        public void SavedWhenAGameStartsOrLoads()
        {
            Assert.Equal(AccessTools.Method(typeof(variables), "Awake"), typeof(variables_Awake).GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Select(a => AccessTools.Method(a.info.declaringType, a.info.methodName)).Single());
            Assert.Equal(AccessTools.Method(typeof(variables), nameof(variables.LoadFunction)), typeof(variables_LoadFunction).GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>().Select(a => AccessTools.Method(a.info.declaringType, a.info.methodName)).Single());

            TestGame.AddMod("Mod A", """[ { "type": "checkbox", "varID": "A_Loud", "labelID": "TEST__LOUD" } ]""");
            variables_LoadFunction.Postfix();
            Assert.Equal("0", TestGame.Saved("A_Loud"));
        }

        /// <summary>
        /// Our mods' shipped menus all get a default for every setting they show.
        /// </summary>
        [Theory]
        [InlineData("Extended SSK")]
        [InlineData("FastForward")]
        [InlineData("Targeted Auditions")]
        public void ShippedMenusGetADefaultForEverySetting(string mod)
        {
            TestGame.AddShippedMod(mod);
            Menu menu = TestGame.BuildMenu();

            ModMenusUtils.SaveMissingDefaults();

            Assert.Equal(menu.Items.Select(i => i.varID).OrderBy(id => id), AllSaved().Keys.OrderBy(id => id));
        }
    }
}
