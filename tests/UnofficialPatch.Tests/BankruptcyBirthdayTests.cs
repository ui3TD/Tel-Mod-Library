using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace UnofficialPatch.Tests
{
    /// <summary>
    /// Loading a save in the red keeps its bankruptcy deadline instead of starting a new month.
    /// </summary>
    public class BankruptcyTests
    {
        public BankruptcyTests() => TestGame.Reset();

        [Theory]
        [InlineData(-5, true)]
        [InlineData(0, false)]
        [InlineData(1000, false)]
        public void BankruptcyWarningRestoredAfterLoad(long money, bool danger)
        {
            resources.resource[(int)resources.type.money] = money;
            loans.BankruptcyDanger = false;
            DateTime deadline = new(2023, 4, 1);
            loans.BankruptcyDate = deadline;

            Bankruptcy_Restore.AfterLoad();

            Assert.Equal(danger, loans.BankruptcyDanger);
            Assert.Equal(deadline, loans.BankruptcyDate);
        }
    }

    /// <summary>
    /// The English birthday title reads 'Mio turns 21!' instead of 'Mio's 21th birthday'.
    /// </summary>
    public class BirthdayTitleTests
    {
        public BirthdayTitleTests() => TestGame.Reset();

        // The game's English title text, then this mod's constants file, as the game loads them
        [Theory]
        [InlineData("en", "Mio's 21th birthday", "Mio turns 21!")]
        [InlineData("jp", "Mio's 21th birthday", null)]
        [InlineData("en", "Mio's 21st birthday", null)]
        public void BirthdayTitleUsesThisModsText(string language, string title, string expected)
        {
            Language.Data["BD__TITLE_1"] = "@'s";
            Language.Data["BD__TITLE_2"] = "@th birthday";
            string constants = Path.Combine(TestGame.ModDir(), "assets", "JSON", "Constants", "constants.json");
            foreach (SimpleJSON.JSONNode item in mainScript.ProcessInboundData(File.ReadAllText(constants)).AsArray)
                Language.Data[item["id"]] = item["text"];

            Assert.Equal(expected, Birthday_Popup_RenderTitle.NewTitle(language, title, "Mio", "21"));
        }
    }

    /// <summary>
    /// Balance: the game works out several changes to an idol's graduation date and then discards them (injury,
    /// depression, a best friend or clique member graduating, accepted business proposals, the weekly change).
    /// Storing them changes balance, so no fix may. Checked on the patch classes, because Graduate can't be patched
    /// outside the game.
    /// </summary>
    public class GraduationDatePolicyTests
    {
        [Theory]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Set_Injured))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Set_Depressed))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduate))]
        [InlineData(typeof(data_girls.girls), nameof(data_girls.girls.Graduation_Date_Update))]
        [InlineData(typeof(business), nameof(business.Accept))]
        public void GraduationDateChangesAreLeftAlone(Type type, string method)
        {
            IEnumerable<Type> transpilers = typeof(Theaters_CompleteDay).Assembly.GetTypes()
                .Where(t => t.Namespace == "UnofficialPatch" && AccessTools.Method(t, "Transpiler") != null)
                .Where(t => t.GetCustomAttributes<HarmonyPatch>().Any(a => a.info.declaringType == type && a.info.methodName == method));

            Assert.Empty(transpilers);
        }
    }
}
