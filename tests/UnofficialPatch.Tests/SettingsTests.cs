using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using _type = Theaters._theater._schedule._type;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// The fixes a player can turn off in ModMenus. Unticked ("0"), the game plays as without the fix; ticked
    /// ("1") or without ModMenus (no variable), the fix is on. The setting is read on every use.
    /// </summary>
    public class SettingsTests : IDisposable
    {
        public SettingsTests()
        {
            TestGame.Reset();
            Language.Data["MENTAL_STAMINA"] = "Mental stamina";
            Language.Data["PT"] = "pt";
        }

        public void Dispose() => MonthlyEvent.Detach();

        public static IEnumerable<object[]> OnValues => new[] { new object[] { null }, new object[] { "1" } };

        private static void Set(string varID, string value)
        {
            if (value != null)
                variables.Set(varID, value);
        }

        [Theory]
        [MemberData(nameof(OnValues))]
        public void TheaterStamina_On_ShowsCostStamina(string value)
        {
            Set(Settings.TheaterStamina, value);
            Assert.Equal(5f, Theaters.GetStaminaCost(_type.performance));
        }

        [Fact]
        public void TheaterStamina_Off_ShowsAreFree()
        {
            variables.Set(Settings.TheaterStamina, "0");
            Assert.Equal(0f, Theaters.GetStaminaCost(_type.performance));
            Assert.Equal(0f, Theaters.GetStaminaCost(_type.manzai));
        }

        [Fact]
        public void Setting_IsReadOnEveryUse()
        {
            variables.Set(Settings.TheaterStamina, "0");
            Assert.Equal(0f, Theaters.GetStaminaCost(_type.performance));
            variables.Set(Settings.TheaterStamina, "1");
            Assert.Equal(5f, Theaters.GetStaminaCost(_type.performance));
        }

        // Fame level 4 at a level 5 venue: 28% attendance with the fix. The game compares fame points, which are
        // always far above twice the venue level, so it never lowers attendance.
        [Theory]
        [MemberData(nameof(OnValues))]
        public void TourFame_On_LowFameLowersAttendance(string value)
        {
            Set(Settings.TourFame, value);
            resources.resource[(int)resources.type.fame] = (long)resources.FameLevelToPoints(4);
            Assert.Equal(0.28f, SEvent_Tour.GetFamePenalty(5), 4);
        }

        [Fact]
        public void TourFame_Off_FameDoesNotMatter()
        {
            variables.Set(Settings.TourFame, "0");
            resources.resource[(int)resources.type.fame] = (long)resources.FameLevelToPoints(4);
            Assert.Equal(1f, SEvent_Tour.GetFamePenalty(5), 4);
        }

        [Theory]
        [MemberData(nameof(OnValues))]
        public void ScandalStamina_On_IdolLosesMentalStamina(string value)
        {
            Set(Settings.ScandalStamina, value);
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());

            Assert.Contains("-25", ScandalDamage(girl));
            Assert.Equal(new[] { (girl, data_girls._paramType.mentalStamina, -25f) }, Seams.ParamsAdded);
        }

        [Theory]
        [InlineData(traits._trait._type.Thick_Skin)]
        [InlineData(traits._trait._type.Shameless)]
        [InlineData(traits._trait._type.Clumsy)]
        public void ScandalStamina_Off_EveryIdolIsSpared(traits._trait._type trait)
        {
            variables.Set(Settings.ScandalStamina, "0");
            data_girls.girls girl = TestGame.Hire(TestGame.Idol());
            girl.trait = trait;

            Assert.Equal("", ScandalDamage(girl));
            Assert.Empty(Seams.ParamsAdded);
        }

        private static string ScandalDamage(data_girls.girls girl)
        {
            Event_Templates._active_template template = new();
            template.Actors.Add(new Event_Manager._activeEvent._actor { girl = girl });
            return (string)TestGame.CallPrivate(template, typeof(Event_Templates._active_template), "MentalDamageDating", -25f, false);
        }

        // The monthly event with the game's two subscribers and another mod's, for an idol who earned 500 this
        // month and has an injury (a Best Employer strike). Returns how often the other mod's subscriber ran.
        private Func<int> RaiseMonth(out data_girls.girls idol)
        {
            mainScript main = TestGame.Component<mainScript>();
            idol = TestGame.Registered(1, data_girls._status.injured);
            idol.Earnings_CurrentMonth = 500;
            Awards.Checks = new Awards._checks();
            int otherMod = 0;
            main.onNewMonth += TestGame.Component<Awards>().OnNewMonth;
            main.onNewMonth += TestGame.Component<data_girls>().OnNewMonth;
            main.onNewMonth += () => otherMod++;
            MonthlyEvent.Attach(main);

            staticVars.SetTime(new DateTime(2023, 3, 1));
            Awards_OnNewDay.Prefix();
            MonthlyEvent.OnNewDay();
            MonthlyEvent.OnTimeTick();
            return () => otherMod;
        }

        [Theory]
        [MemberData(nameof(OnValues))]
        public void MonthlyEvent_On_BothGameSubscribersRun(string value)
        {
            Set(Settings.BestEmployer, value);
            Set(Settings.SalaryWishes, value);

            Func<int> otherMod = RaiseMonth(out data_girls.girls idol);

            Assert.Equal(1, Awards.Checks.Best_Employer_Counter);
            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
            Assert.Equal(1, otherMod());
        }

        [Fact]
        public void BestEmployer_Off_NoStrike_SalariesStillRecorded()
        {
            variables.Set(Settings.BestEmployer, "0");

            Func<int> otherMod = RaiseMonth(out data_girls.girls idol);

            Assert.Equal(0, Awards.Checks.Best_Employer_Counter);
            Assert.Equal(new List<long> { 500 }, idol.Earnings_History);
            Assert.Equal(1, otherMod());
        }

        [Fact]
        public void SalaryWishes_Off_EarningsKeepAddingUp_StrikeStillCounted()
        {
            variables.Set(Settings.SalaryWishes, "0");

            Func<int> otherMod = RaiseMonth(out data_girls.girls idol);

            // As in the game: no monthly history, so salary wishes average over all-time earnings
            Assert.Empty(idol.Earnings_History);
            Assert.Equal(500, idol.Earnings_CurrentMonth);
            Assert.Equal(1, Awards.Checks.Best_Employer_Counter);
            Assert.Equal(1, otherMod());
        }

        private static string ModAsset(string relative, [CallerFilePath] string testFile = "") =>
            Path.Combine(Path.GetDirectoryName(testFile), "..", "..", "mods", "Unofficial Patch", "assets", "JSON", relative);

        private static List<SimpleJSON.JSONNode> Items(string relative)
        {
            SimpleJSON.JSONArray items = SimpleJSON.JSON.Parse(File.ReadAllText(ModAsset(relative))).AsArray;
            return Enumerable.Range(0, items.Count).Select(i => items[i]).ToList();
        }

        /// <summary>
        /// Every setting has a ticked checkbox (the code's default) with a label, and the menu has no other setting.
        /// </summary>
        [Fact]
        public void ModMenu_HasATickedCheckboxForEverySetting()
        {
            List<SimpleJSON.JSONNode> menu = Items(Path.Combine("Mod Menu", "modmenu.json"));
            HashSet<string> labels = new(Items(Path.Combine("Constants", "constants.json")).Select(c => c["id"].Value));

            List<SimpleJSON.JSONNode> boxes = menu.Where(i => i["type"].Value == "checkbox").ToList();
            Assert.Equal(Settings.All, boxes.Select(b => b["varID"].Value));
            Assert.All(boxes, b => Assert.True(b["defaultValue"].AsBool));
            Assert.All(menu.Where(i => i["type"].Value != "text"), i => Assert.Equal("checkbox", i["type"].Value));
            Assert.All(menu, i => Assert.Contains(i["labelID"].Value, labels));
        }
    }
}
